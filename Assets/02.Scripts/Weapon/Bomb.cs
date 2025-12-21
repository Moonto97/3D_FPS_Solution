using UnityEngine;

/// <summary>
/// 폭탄 오브젝트: 충돌 시 범위 데미지를 주고 풀로 반환됨
/// </summary>
public class Bomb : MonoBehaviour, IPoolable
{
    // ─────────────────────────────────────────────────────────
    // 폭발 설정 (Inspector 튜닝)
    // ─────────────────────────────────────────────────────────
    [Header("폭발 설정")]
    [SerializeField] private GameObject _explosionEffectPrefab;
    [SerializeField] private float _explosionRadius = 2f;
    [SerializeField] private float _damage = 1000f;
    
    // ─────────────────────────────────────────────────────────
    // 풀링 설정 (프리팹 Inspector에서 설정)
    // ─────────────────────────────────────────────────────────
    [Header("풀링")]
    [Tooltip("ObjectPoolManager에 등록된 태그와 일치해야 함")]
    [SerializeField] private string _poolTag = "Bomb";
    
    private Rigidbody _rigidbody;
    
    // 구버전 호환용 프로퍼티
    public float ExplosionRadius => _explosionRadius;
    public float Damage => _damage;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_rigidbody == null)
        {
            Debug.LogError($"[Bomb] Rigidbody가 없습니다! GameObject: {gameObject.name}");
        }
    }


    // ─────────────────────────────────────────────────────────
    // IPoolable 구현
    // ─────────────────────────────────────────────────────────
    
    /// <summary>
    /// 풀에서 꺼내질 때: 이전 물리 상태 초기화
    /// </summary>
    public void OnSpawnFromPool()
    {
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 풀로 반환될 때: 정리 작업 (현재는 없음)
    /// </summary>
    public void OnReturnToPool()
    {
        // 향후 파티클, 사운드 등 추가 시 여기서 정리
    }

    // ─────────────────────────────────────────────────────────
    // 충돌 처리
    // ─────────────────────────────────────────────────────────
    
    private void OnCollisionEnter(Collision collision)
    {
        SpawnExplosionEffect();
        DealDamageToMonstersInRadius();
        ReturnToPool();
    }


    private void SpawnExplosionEffect()
    {
        if (_explosionEffectPrefab == null)
        {
            Debug.LogWarning("[Bomb] 폭발 이펙트 프리팹이 할당되지 않았습니다!");
            return;
        }
        
        Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
    }

    private void DealDamageToMonstersInRadius()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, 
            _explosionRadius, 
            LayerMask.GetMask("Monster")
        );
        
        for (int i = 0; i < colliders.Length; i++)
        {
            Monster monster = colliders[i].GetComponent<Monster>();
            if (monster == null) continue;
            
            float distance = Vector3.Distance(transform.position, monster.transform.position);
            distance = Mathf.Max(1f, distance);
            
            monster.TryTakeDamage(_damage / distance);
        }
    }

    private void ReturnToPool()
    {
        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogWarning("[Bomb] ObjectPoolManager가 없어 Destroy로 대체합니다.");
            Destroy(gameObject);
            return;
        }
        
        ObjectPoolManager.Instance.Despawn(_poolTag, gameObject);
    }
}
