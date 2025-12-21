using System;
using UnityEngine;

public class PlayerBombFire : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // 발사 설정
    // ─────────────────────────────────────────────────────────
    [Header("발사 설정")]
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private float _throwPower = 15f;
    
    // ─────────────────────────────────────────────────────────
    // 풀링 설정
    // ─────────────────────────────────────────────────────────
    [Header("풀링")]
    [Tooltip("ObjectPoolManager에 등록된 폭탄 풀 태그 (Inspector에서 미리 설정 필요)")]
    [SerializeField] private string _bombPoolTag = "Bomb";
    
    // ─────────────────────────────────────────────────────────
    // 폭탄 보유량 설정 (Inspector에서 튜닝)
    // ─────────────────────────────────────────────────────────
    [Header("폭탄 보유량")]
    [Tooltip("최대 5발, 3초마다 1발 회복")]
    [SerializeField] private ConsumableStat _bombCount = new ConsumableStat();
    
    // UI 연동용: 외부에서 폭탄 개수 변화를 구독할 수 있는 이벤트
    public event Action<int, int> OnBombCountChanged;
    
    // 외부에서 현재 폭탄 상태를 읽기 위한 프로퍼티
    public int CurrentBombCount => Mathf.FloorToInt(_bombCount.Value);
    public int MaxBombCount => Mathf.FloorToInt(_bombCount.MaxValue);

    
    private void Awake()
    {
        _bombCount.Initialize();
        _bombCount.OnValueChanged += HandleBombCountChanged;
    }
    
    private void OnDestroy()
    {
        _bombCount.OnValueChanged -= HandleBombCountChanged;
    }
    
    private void Start()
    {
        // 풀 존재 여부 검증 (ObjectPoolManager Inspector에서 미리 설정 필요)
        ValidateBombPool();
        
        // UI 초기화
        OnBombCountChanged?.Invoke(CurrentBombCount, MaxBombCount);
    }
    
    /// <summary>
    /// ObjectPoolManager에 Bomb 풀이 등록되어 있는지 검증
    /// 없으면 경고 로그 출력 (Inspector 설정 누락 안내)
    /// </summary>
    private void ValidateBombPool()
    {
        if (!ObjectPoolManager.Instance.HasPool(_bombPoolTag))
        {
            Debug.LogError($"[PlayerBombFire] ObjectPoolManager에 '{_bombPoolTag}' 풀이 없습니다!\n" +
                           "→ ObjectPoolManager Inspector에서 Pool Settings에 Bomb 풀을 추가하세요.");
        }
    }
    
    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;
        
        _bombCount.Regenerate(Time.deltaTime);
        
        if (Input.GetMouseButtonDown(1))
        {
            TryFireBomb();
        }
    }

    
    /// <summary>
    /// 폭탄 발사 시도. 보유량이 1 이상일 때만 발사.
    /// </summary>
    private void TryFireBomb()
    {
        if (!_bombCount.TryConsume(1f))
        {
            Debug.Log("[PlayerBombFire] 폭탄이 부족합니다!");
            return;
        }
        
        // 풀에서 폭탄 가져오기
        GameObject bombObj = ObjectPoolManager.Instance.Spawn(
            _bombPoolTag, 
            _fireTransform.position, 
            Quaternion.identity
        );
        
        if (bombObj == null)
        {
            Debug.LogError("[PlayerBombFire] 폭탄 Spawn 실패!");
            return;
        }
        
        // Rigidbody로 폭탄 발사
        Rigidbody rb = bombObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(Camera.main.transform.forward * _throwPower, ForceMode.Impulse);
        }
    }
    
    private void HandleBombCountChanged(float current, float max)
    {
        OnBombCountChanged?.Invoke(Mathf.FloorToInt(current), Mathf.FloorToInt(max));
    }
}
