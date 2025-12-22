using UnityEngine;

/// <summary>
/// 골드 코인 동작 담당.
/// 드랍 시 튀어올랐다가 착지 → 플레이어 접근 시 자석처럼 끌려감 → 충돌 시 획득.
/// IPoolable 구현으로 풀링 시스템과 연동.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GoldCoin : MonoBehaviour, IPoolable
{
    #region 설정값 (인스펙터 튜닝)
    
    [Header("획득 설정")]
    [SerializeField, Tooltip("플레이어가 이 거리 안에 들어오면 끌려감")]
    private float _attractRadius = 5f;
    
    [SerializeField, Tooltip("플레이어와 충돌하여 획득되는 거리")]
    private float _collectRadius = 0.5f;
    
    [SerializeField, Tooltip("획득 시 얻는 골드량")]
    private int _goldValue = 10;
    
    [Header("드랍 설정")]
    [SerializeField, Tooltip("드랍 시 수직 힘")]
    private float _dropUpForce = 5f;
    
    [SerializeField, Tooltip("드랍 시 수평 산개 힘")]
    private float _dropSpreadForce = 3f;
    
    [SerializeField, Tooltip("드랍 후 자석 활성화 대기 시간")]
    private float _dropSettleTime = 0.5f;
    
    [Header("이동 설정")]
    [SerializeField, Tooltip("이동 시작 속도")]
    private float _initialSpeed = 2f;
    
    [SerializeField, Tooltip("최대 이동 속도")]
    private float _maxSpeed = 20f;
    
    [SerializeField, Tooltip("가속도 (속도 증가율)")]
    private float _acceleration = 15f;
    
    [Header("회전 설정")]
    [SerializeField, Tooltip("코인 회전 속도")]
    private float _rotationSpeed = 180f;
    
    [Header("자동 회수")]
    [SerializeField, Tooltip("바닥 아래로 떨어지면 자동 회수하는 Y 좌표")]
    private float _destroyBelowY = -10f;
    
    #endregion

    #region 내부 상태
    
    private Transform _playerTransform;
    private PlayerStats _playerStats;
    private Rigidbody _rb;
    
    private bool _isDropping;            // 드랍 튀어오르는 중
    private bool _isAttracted;           // 플레이어에게 끌리는 중
    private float _currentSpeed;         // 현재 이동 속도
    private float _dropTimer;            // 드랍 후 경과 시간
    
    // 풀링용 태그 (ObjectPoolManager에서 사용)
    public const string POOL_TAG = "GoldCoin";
    
    #endregion

    #region Unity 라이프사이클
    
    private void Awake()
    {
        // Rigidbody 캐싱
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            Debug.LogError("[GoldCoin] Rigidbody가 없습니다!", this);
        }
    }
    
    private void Start()
    {
        CachePlayerReference();
    }
    
    private void Update()
    {
        // 낙하 방지: Y좌표가 너무 낮으면 회수
        if (transform.position.y < _destroyBelowY)
        {
            ReturnToPoolOrDestroy();
            return;
        }
        
        // 시각 효과: 코인 회전
        RotateCoin();
        
        // 플레이어 참조 없으면 재시도
        if (_playerTransform == null)
        {
            CachePlayerReference();
            return;
        }
        
        // 드랍 중일 때: 타이머 증가, 착지 대기
        if (_isDropping)
        {
            _dropTimer += Time.deltaTime;
            
            // 착지 감지: 타이머 + Rigidbody 속도 감소
            bool hasSettled = _dropTimer >= _dropSettleTime && 
                              _rb.linearVelocity.magnitude < 0.5f;
            
            if (hasSettled)
            {
                CompleteDrop();
            }
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        
        // 획득 판정
        if (distanceToPlayer <= _collectRadius)
        {
            Collect();
            return;
        }
        
        // 끌림 시작 판정
        if (!_isAttracted && distanceToPlayer <= _attractRadius)
        {
            StartAttraction();
        }
        
        // 끌림 중이면 플레이어를 향해 이동
        if (_isAttracted)
        {
            MoveTowardsPlayer();
        }
    }
    
    #endregion

    #region 드랍 효과
    
    /// <summary>
    /// 몬스터가 드랍할 때 호출. 랜덤 방향으로 튀어오름.
    /// 단일 코인 드랍 시 사용.
    /// </summary>
    public void LaunchDrop()
    {
        _isDropping = true;
        _isAttracted = false;
        _dropTimer = 0f;
        
        // Rigidbody 활성화
        _rb.isKinematic = false;
        _rb.useGravity = true;
        
        // 랜덤 방향으로 힘 적용 (위 + 수평 산개)
        Vector2 randomCircle = Random.insideUnitCircle;
        Vector3 spreadDir = new Vector3(randomCircle.x, 0f, randomCircle.y).normalized;
        Vector3 launchForce = Vector3.up * _dropUpForce + spreadDir * _dropSpreadForce;
        
        _rb.linearVelocity = Vector3.zero;  // 기존 속도 초기화
        _rb.AddForce(launchForce, ForceMode.Impulse);
        
        // 회전도 약간 추가 (시각적 재미)
        _rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
    }
    
    /// <summary>
    /// 소닉 스타일 방사형 드랍. 360°를 totalCount로 나눈 각도로 발사.
    /// 여러 코인이 폭발하듯 균등하게 퍼지는 효과.
    /// </summary>
    /// <param name="index">현재 코인 인덱스 (0 ~ totalCount-1)</param>
    /// <param name="totalCount">전체 코인 개수</param>
    public void LaunchRadial(int index, int totalCount)
    {
        _isDropping = true;
        _isAttracted = false;
        _dropTimer = 0f;
        
        // Rigidbody 활성화
        _rb.isKinematic = false;
        _rb.useGravity = true;
        
        // 균등 각도 계산: 360° / 총 개수 = 각 코인 간격
        float angleStep = 360f / totalCount;
        float angle = angleStep * index;
        float angleRad = angle * Mathf.Deg2Rad;
        
        // 방향 벡터 (XZ 평면에서 원형 배치)
        Vector3 spreadDir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
        
        // 힘: 위로 + 수평 산개 (포물선 궤적)
        Vector3 launchForce = Vector3.up * _dropUpForce + spreadDir * _dropSpreadForce;
        
        // [디버그] 실제 발사 각도 확인
        Debug.Log($"[GoldCoin] index={index}, angle={angle}°, dir=({spreadDir.x:F2}, {spreadDir.z:F2}), force={launchForce}");
        
        _rb.linearVelocity = Vector3.zero;
        _rb.AddForce(launchForce, ForceMode.Impulse);
        
        // 약간의 랜덤 회전 (시각적 재미)
        _rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
    }
    
    /// <summary>
    /// 드랍 착지 완료: 물리 비활성화, 자석 효과 준비
    /// </summary>
    private void CompleteDrop()
    {
        _isDropping = false;
        
        // Rigidbody 정지 (자석 이동은 Transform으로 처리)
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }
    
    #endregion

    #region 자석 효과
    
    /// <summary>
    /// 플레이어 참조 캐싱. Player 태그로 검색.
    /// </summary>
    private void CachePlayerReference()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerStats = player.GetComponent<PlayerStats>();
            
            if (_playerStats == null)
            {
                Debug.LogWarning("[GoldCoin] Player에 PlayerStats 컴포넌트가 없습니다.", this);
            }
        }
    }
    
    /// <summary>
    /// 코인 회전 애니메이션 (Y축 기준)
    /// </summary>
    private void RotateCoin()
    {
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);
    }
    
    /// <summary>
    /// 끌림 시작: 초기 속도 설정
    /// </summary>
    private void StartAttraction()
    {
        _isAttracted = true;
        _currentSpeed = _initialSpeed;
    }
    
    /// <summary>
    /// 플레이어 방향으로 가속 이동.
    /// </summary>
    private void MoveTowardsPlayer()
    {
        // 가속
        _currentSpeed += _acceleration * Time.deltaTime;
        _currentSpeed = Mathf.Min(_currentSpeed, _maxSpeed);
        
        // 플레이어 방향 (캐릭터 중심 약간 위)
        Vector3 targetPos = _playerTransform.position + Vector3.up * 0.5f;
        Vector3 direction = (targetPos - transform.position).normalized;
        
        transform.position += direction * _currentSpeed * Time.deltaTime;
    }
    
    /// <summary>
    /// 골드 획득 처리
    /// </summary>
    private void Collect()
    {
        if (_playerStats != null)
        {
            _playerStats.AddGold(_goldValue);
            Debug.Log($"[GoldCoin] 골드 획득! +{_goldValue} (총: {_playerStats.Gold})");
        }
        
        // TODO: 획득 이펙트/사운드 추가 가능
        
        ReturnToPoolOrDestroy();
    }
    
    /// <summary>
    /// 풀로 반환 또는 파괴
    /// </summary>
    private void ReturnToPoolOrDestroy()
    {
        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.HasPool(POOL_TAG))
        {
            ObjectPoolManager.Instance.Despawn(POOL_TAG, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    #endregion

    #region IPoolable 구현
    
    public void OnSpawnFromPool()
    {
        _isDropping = false;
        _isAttracted = false;
        _currentSpeed = 0f;
        _dropTimer = 0f;
        
        // Rigidbody 초기화
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        
        if (_playerTransform == null)
        {
            CachePlayerReference();
        }
    }
    
    public void OnReturnToPool()
    {
        _isDropping = false;
        _isAttracted = false;
        _currentSpeed = 0f;
    }
    
    #endregion

    #region 디버그
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attractRadius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _collectRadius);
    }
    
    #endregion
}
