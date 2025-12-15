using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 풀 중앙 관리자 (싱글톤)
/// 다양한 타입의 오브젝트 풀을 등록하고 관리합니다.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    #region Singleton
    private static ObjectPoolManager _instance;
    public static ObjectPoolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에서 찾기
                _instance = FindObjectOfType<ObjectPoolManager>();
                
                // 없으면 새로 생성
                if (_instance == null)
                {
                    GameObject go = new GameObject("ObjectPoolManager");
                    _instance = go.AddComponent<ObjectPoolManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    /// <summary>
    /// 개별 풀 정보를 담는 클래스
    /// </summary>
    [System.Serializable]
    public class Pool
    {
        public string tag;              // 풀 식별자
        public GameObject prefab;       // 프리팹
        public int initialSize = 10;    // 초기 생성 개수
        public bool autoExpand = true;  // 풀 부족 시 자동 확장
    }

    [Header("Pool Settings")]
    [SerializeField] private List<Pool> _poolSettings = new List<Pool>();

    // 실제 풀 저장소: tag -> Queue<GameObject>
    private Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
    
    // 프리팹 참조 저장: tag -> Prefab (자동 확장용)
    private Dictionary<string, Pool> _poolInfos = new Dictionary<string, Pool>();
    
    // 풀 컨테이너들의 부모
    private Transform _poolContainer;

    private void Start()
    {
        InitializePools();
    }

    /// <summary>
    /// Inspector에서 설정한 풀들을 초기화
    /// </summary>
    private void InitializePools()
    {
        _poolContainer = new GameObject("PoolContainer").transform;
        _poolContainer.SetParent(transform);

        foreach (Pool poolInfo in _poolSettings)
        {
            CreatePool(poolInfo);
        }
    }

    /// <summary>
    /// 새로운 풀 생성 (런타임에서도 호출 가능)
    /// </summary>
    public void CreatePool(Pool poolInfo)
    {
        if (_pools.ContainsKey(poolInfo.tag))
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag '{poolInfo.tag}' already exists!");
            return;
        }

        Queue<GameObject> objectPool = new Queue<GameObject>();

        // 풀 전용 컨테이너 생성
        Transform container = new GameObject($"Pool_{poolInfo.tag}").transform;
        container.SetParent(_poolContainer);

        // 초기 개수만큼 생성
        for (int i = 0; i < poolInfo.initialSize; i++)
        {
            GameObject obj = CreateNewObject(poolInfo.prefab, container);
            objectPool.Enqueue(obj);
        }

        _pools.Add(poolInfo.tag, objectPool);
        _poolInfos.Add(poolInfo.tag, poolInfo);

        Debug.Log($"[ObjectPoolManager] Pool '{poolInfo.tag}' created with {poolInfo.initialSize} objects.");
    }

    /// <summary>
    /// 런타임에서 간단하게 풀 생성
    /// </summary>
    public void CreatePool(string tag, GameObject prefab, int initialSize = 10, bool autoExpand = true)
    {
        Pool poolInfo = new Pool
        {
            tag = tag,
            prefab = prefab,
            initialSize = initialSize,
            autoExpand = autoExpand
        };
        CreatePool(poolInfo);
    }

    /// <summary>
    /// 풀에서 오브젝트 가져오기
    /// </summary>
    public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
    {
        if (!_pools.ContainsKey(tag))
        {
            Debug.LogError($"[ObjectPoolManager] Pool with tag '{tag}' doesn't exist!");
            return null;
        }

        Queue<GameObject> pool = _pools[tag];
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            // 풀이 비어있으면
            Pool poolInfo = _poolInfos[tag];
            
            if (poolInfo.autoExpand)
            {
                // 자동 확장
                Transform container = _poolContainer.Find($"Pool_{tag}");
                obj = CreateNewObject(poolInfo.prefab, container);
                Debug.Log($"[ObjectPoolManager] Pool '{tag}' expanded. Consider increasing initial size.");
            }
            else
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool '{tag}' is empty and autoExpand is disabled!");
                return null;
            }
        }

        // 위치 및 회전 설정
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        // IPoolable 인터페이스 호출
        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnSpawnFromPool();

        return obj;
    }

    /// <summary>
    /// 풀에서 오브젝트 가져오기 (컴포넌트 타입으로 반환)
    /// </summary>
    public T Spawn<T>(string tag, Vector3 position, Quaternion rotation) where T : Component
    {
        GameObject obj = Spawn(tag, position, rotation);
        return obj?.GetComponent<T>();
    }

    /// <summary>
    /// 오브젝트를 풀로 반환
    /// </summary>
    public void Despawn(string tag, GameObject obj)
    {
        if (!_pools.ContainsKey(tag))
        {
            Debug.LogError($"[ObjectPoolManager] Pool with tag '{tag}' doesn't exist!");
            Destroy(obj);
            return;
        }

        // IPoolable 인터페이스 호출
        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnReturnToPool();

        obj.SetActive(false);
        
        // 컨테이너로 이동
        Transform container = _poolContainer.Find($"Pool_{tag}");
        if (container != null)
        {
            obj.transform.SetParent(container);
        }

        _pools[tag].Enqueue(obj);
    }

    /// <summary>
    /// 지연 후 오브젝트를 풀로 반환
    /// </summary>
    public void Despawn(string tag, GameObject obj, float delay)
    {
        StartCoroutine(DespawnCoroutine(tag, obj, delay));
    }

    private System.Collections.IEnumerator DespawnCoroutine(string tag, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (obj != null)
        {
            Despawn(tag, obj);
        }
    }

    /// <summary>
    /// 새 오브젝트 생성 (비활성화 상태로)
    /// </summary>
    private GameObject CreateNewObject(GameObject prefab, Transform parent)
    {
        GameObject obj = Instantiate(prefab, parent);
        obj.SetActive(false);
        return obj;
    }

    /// <summary>
    /// 특정 풀의 현재 사용 가능한 개수
    /// </summary>
    public int GetPoolCount(string tag)
    {
        if (_pools.ContainsKey(tag))
        {
            return _pools[tag].Count;
        }
        return 0;
    }

    /// <summary>
    /// 풀 존재 여부 확인
    /// </summary>
    public bool HasPool(string tag)
    {
        return _pools.ContainsKey(tag);
    }

    /// <summary>
    /// 모든 풀 상태 출력 (디버깅용)
    /// </summary>
    [ContextMenu("Print Pool Status")]
    public void PrintPoolStatus()
    {
        Debug.Log("=== Object Pool Status ===");
        foreach (var kvp in _pools)
        {
            Debug.Log($"  [{kvp.Key}] Available: {kvp.Value.Count}");
        }
    }
}