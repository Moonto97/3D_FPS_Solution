using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 메인 컨트롤러. 상태 머신, 이동, 전투를 담당.
/// 점프 로직은 MonsterJumpController로 위임.
/// 
/// [피격 시스템 설계]
/// - 무적: 짧은 타이머(_invincibilityTimer)로 연타 방지. State와 독립.
/// - 넉백: _isKnockbackActive로 관리. 넉백 중에도 추가 피격 가능(무적 끝나면).
/// - 애니메이션: 매 피격마다 "Hit" 트리거 재발동.
/// </summary>
public class Monster : MonoBehaviour
{
    #region 상태 및 참조
    
    public EMonsterState State = EMonsterState.Idle;

    [Header("필수 참조")]
    [SerializeField] private GameObject _player;
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Animator _animator;
    
    [Header("점프 컨트롤러")]
    [SerializeField] private MonsterJumpController _jumpController;
    
    [Header("골드 드롭")]
    [SerializeField, Tooltip("골드 코인 프리팩")]
    private GameObject _goldCoinPrefab;
    
    [SerializeField, Range(0f, 1f), Tooltip("골드 드롭 확률 (0~1)")]
    private float _goldDropChance = 0.5f;
    
    [SerializeField, Tooltip("드롭 시 스폰되는 높이")]
    private float _goldDropHeight = 1.5f;
    
    [SerializeField, Tooltip("드랍할 코인 최소 개수")]
    private int _goldDropCountMin = 3;
    
    [SerializeField, Tooltip("드랍할 코인 최대 개수")]
    private int _goldDropCountMax = 7;
    
    [Header("지형 설정")]
    [SerializeField] private LayerMask _groundLayer;
    
    #endregion

    #region 내부 상태
    
    private float _attackTimer = 0f;
    private Vector3 _defaultPosition;
    private Vector3 _knockbackVelocity;
    
    // === 무적 시스템 (State와 독립) ===
    private float _invincibilityTimer;
    private const float DEFAULT_INVINCIBILITY = 0.1f;  // MonsterStats 없을 때 폴백
    
    // === 넉백 시스템 ===
    private bool _isKnockbackActive;
    private bool _isAirborneKnockback;    // 공중 넉백 여부 (점프 중 피격)
    private float _verticalKnockbackVelocity;
    private float _knockbackTimer;
    private const float KNOCKBACK_GRAVITY = 20f;
    
    // 넉백 전 상태 저장 (복귀용)
    private EMonsterState _preKnockbackState;
    
    // 목적지 캐싱 (매 프레임 SetDestination 방지)
    private Vector3 _currentDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 1.0f;
    
    #endregion

    #region 순찰 상태
    
    // 현재 순찰 목표 지점
    private Vector3 _patrolTarget;
    
    // 포인트 도착 후 대기 타이머
    private float _patrolWaitTimer;
    
    // 대기 중 여부
    private bool _isWaitingAtPatrolPoint;
    
    // 도착 판정 거리
    private const float PATROL_ARRIVAL_THRESHOLD = 0.5f;
    
    // NavMesh 샘플링 최대 시도 횟수
    private const int MAX_PATROL_SAMPLE_ATTEMPTS = 10;
    
    #endregion

    #region Properties
    
    /// <summary>무적 상태 여부. UI나 디버그용.</summary>
    public bool IsInvincible => _invincibilityTimer > 0f;
    
    /// <summary>넉백 진행 중 여부.</summary>
    public bool IsKnockbackActive => _isKnockbackActive;
    
    #endregion

    #region Gizmos용 Public 접근자
    
    public Vector3 CurrentDestination => _currentDestination;
    public MonsterJumpController JumpController => _jumpController;
    public Vector3 DefaultPosition => _defaultPosition;
    public Vector3 PatrolTarget => _patrolTarget;
    
    #endregion

    #region 초기화

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }
    
private void Start()
    {
        _defaultPosition = transform.position;
        _agent.speed = _monsterStats.MoveSpeed.Value;
        
        // 점프 컨트롤러 초기화
        if (_jumpController == null)
        {
            _jumpController = GetComponent<MonsterJumpController>();
        }
        
        if (_jumpController != null)
        {
            _jumpController.Initialize(_player.transform, _agent, _groundLayer, _monsterStats);
            _jumpController.OnJumpStarted += HandleJumpStarted;
            _jumpController.OnJumpCompleted += HandleJumpCompleted;
        }
        else
        {
            Debug.LogWarning($"[Monster] {gameObject.name}에 MonsterJumpController가 없습니다.", this);
        }
        
        if (!_agent.isOnNavMesh)
        {
            Debug.LogError($"[Monster] {gameObject.name}이 NavMesh 위에 없습니다!", this);
        }
        
        // 순찰 상태로 시작 (EnterPatrolState로 초기화 통일)
        EnterPatrolState();
        
        // Patrol도 Trace 애니메이션 사용 (움직이는 모션)
        _animator.SetTrigger("IdleToTrace");
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_jumpController != null)
        {
            _jumpController.OnJumpStarted -= HandleJumpStarted;
            _jumpController.OnJumpCompleted -= HandleJumpCompleted;
        }
    }
    
    #endregion

    #region 점프 이벤트 핸들러
    
    private void HandleJumpStarted()
    {
        _currentDestination = Vector3.zero;
        State = EMonsterState.Jump;
    }
    
    private void HandleJumpCompleted()
    {
        State = EMonsterState.Trace;
    }
    
    #endregion


    #region Update 루프
    
    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;

        // 무적 타이머 감소 (State와 독립적으로 항상 실행)
        UpdateInvincibility();
        
        _jumpController?.UpdateCooldown();
        ApplyKnockBack();

        // 사망 또는 넉백 중이면 AI 로직 스킵 (Agent 비활성 상태)
        if (State == EMonsterState.Death) return;
        if (_isKnockbackActive) return;

        switch (State)
        {
            case EMonsterState.Idle:
                Idle();
                break;
            case EMonsterState.Patrol:
                Patrol();
                break;
            case EMonsterState.Trace:
                Trace();
                break;
            case EMonsterState.Comeback:
                Comeback();
                break;
            case EMonsterState.Jump:
                _jumpController?.UpdateJump();
                break;
            case EMonsterState.Attack:
                Attack();
                break;
        }
    }
    
    /// <summary>
    /// 무적 타이머 감소. 매 프레임 호출.
    /// </summary>
    private void UpdateInvincibility()
    {
        if (_invincibilityTimer > 0f)
        {
            _invincibilityTimer -= Time.deltaTime;
        }
    }
    
    #endregion

    #region 기본 상태 (Idle / Attack)

    private void Idle()
    {
        if (Vector3.Distance(transform.position, _player.transform.position) <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            _animator.SetTrigger("IdleToTrace");
            Debug.Log($"상태 전환: Idle -> Trace");
        }
    }

    #endregion

    #region 순찰 상태 (Patrol)

    /// <summary>
    /// 순찰 상태: 랜덤 지점을 느린 속도로 이동.
    /// 플레이어 감지 시 추격으로 전환.
    /// </summary>
private void Patrol()
    {
        // 방어: Agent 비활성 시 스킵
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        
        // 1순위: 플레이어 감지 → Trace 전이
        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);
        if (distanceToPlayer <= _monsterStats.DetectDistance.Value)
        {
            TransitionToTrace();
            return;
        }
        
        // 2. 대기 중이면 타이머 처리
        if (_isWaitingAtPatrolPoint)
        {
            HandlePatrolWait();
            return;
        }
        
        // 3. 이동 중 - 도착 체크 (NavMesh 경로 기준)
        bool hasReachedDestination = !_agent.pathPending && 
                                     (_agent.remainingDistance <= PATROL_ARRIVAL_THRESHOLD || 
                                      !_agent.hasPath);
        
        if (hasReachedDestination && _currentDestination != Vector3.zero)
        {
            // 도착 → 대기 시작
            _isWaitingAtPatrolPoint = true;
            _patrolWaitTimer = _monsterStats.PatrolWaitTime.Value;
            _agent.ResetPath();
            return;
        }
        
        // 4. 목표 지점으로 이동
        if (_currentDestination != _patrolTarget)
        {
            _currentDestination = _patrolTarget;
            _agent.SetDestination(_patrolTarget);
            _agent.speed = _monsterStats.MoveSpeed.Value;
        }
    }

    /// <summary>
    /// 순찰 포인트 대기 처리. 타이머 완료 시 새 목표 설정.
    /// </summary>
private void HandlePatrolWait()
    {
        _patrolWaitTimer -= Time.deltaTime;
        
        if (_patrolWaitTimer <= 0f)
        {
            // 대기 완료 → 새 순찰 목표 설정 + 즉시 이동 시작
            _isWaitingAtPatrolPoint = false;
            _patrolTarget = GetRandomPatrolPoint();
            _currentDestination = _patrolTarget;
            _agent.SetDestination(_patrolTarget);
        }
    }

    /// <summary>
    /// 순찰 → 추격 전환. 속도 변경 + 상태 전이.
    /// </summary>
/// <summary>
    /// Patrol 상태 진입 시 필요한 초기화 수행.
    /// </summary>
    private void EnterPatrolState()
    {
        State = EMonsterState.Patrol;
        _patrolTarget = GetRandomPatrolPoint();
        _agent.speed = _monsterStats.MoveSpeed.Value;
        _agent.stoppingDistance = 0.1f;
        
        // 순찰 상태 초기화 (이전 상태 잔여값 제거)
        _isWaitingAtPatrolPoint = false;
        _currentDestination = Vector3.zero;  // 새 목표로 이동하도록 리셋
    }

    
private void TransitionToTrace()
    {
        State = EMonsterState.Trace;
        _agent.speed = _monsterStats.MoveSpeed.Value;
        _isWaitingAtPatrolPoint = false;
        // Patrol도 Trace 애니메이션 사용 중이므로 트리거 호출 불필요
        Debug.Log($"상태 전환: Patrol -> Trace");
    }

    /// <summary>
    /// 랜덤 순찰 지점 생성. NavMesh 유효성 검증 포함.
    /// </summary>
    /// <returns>유효한 NavMesh 위치. 실패 시 기본 위치 반환.</returns>
private Vector3 GetRandomPatrolPoint()
    {
        float radius = _monsterStats.PatrolRadius.Value;
        const float MIN_DISTANCE_FROM_CURRENT = 2f;  // 현재 위치에서 최소 2m 이상
        
        for (int i = 0; i < MAX_PATROL_SAMPLE_ATTEMPTS; i++)
        {
            // XZ 평면에서 랜덤 방향 + 랜덤 거리
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPoint = _defaultPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            // 현재 위치와 최소 거리 체크 (너무 가까우면 재시도)
            if (Vector3.Distance(transform.position, randomPoint) < MIN_DISTANCE_FROM_CURRENT)
            {
                continue;
            }
            
            // NavMesh 유효성 검증
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        
        // 실패 시 기본 위치로 폴백
        Debug.LogWarning($"[Monster] 순찰 지점 샘플링 실패 - 기본 위치로 복귀");
        return _defaultPosition;
    }

    #endregion

    #region 공격 상태 (Attack)

    private void Attack()
    {
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        if (distance > _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Trace;
            _animator.SetTrigger("AttackToTrace");
            Debug.Log($"상태 전환: Attack -> Trace");
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _monsterStats.AttackSpeed.Value)
        {
            _attackTimer = 0f;
            _animator.SetTrigger("AttackableToAttack");
            Debug.Log("플레이어 공격!");
        }
    }

    #endregion

    #region 추적 상태 (Trace)

    private void Trace()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);

        if (distanceToPlayer <= _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Attack;
            _animator.SetTrigger("TraceToAttackable");
            Debug.Log($"상태 전환: Trace -> Attack");
            return;
        }

        if (distanceToPlayer >= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Comeback;
            Debug.Log($"상태 전환: Trace -> Comeback");
            return;
        }

        SetTraceDestination();
        _jumpController?.TryJumpDuringTrace();
    }

    private void SetTraceDestination()
    {
        // 방어: Agent 비활성 시 스킵
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        
        NavMeshPath path = new NavMeshPath();
        Vector3 newDestination = Vector3.zero;
        bool needsJumpApproach = false;
        
        Vector3 playerGroundPos = _jumpController != null 
            ? _jumpController.GetPlayerGroundPosition() 
            : Vector3.zero;

        // 1순위: 플레이어 직접 경로
        if (_agent.CalculatePath(_player.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            newDestination = _player.transform.position;
        }
        // 2순위: 플레이어 지면 경로
        else if (playerGroundPos != Vector3.zero &&
                 _agent.CalculatePath(playerGroundPos, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            newDestination = playerGroundPos;
        }
        // 3순위: NavMesh 끝단으로 이동 (점프 준비)
        else if (_jumpController != null && _jumpController.TryGetNavMeshEdge(out Vector3 edgePos))
        {
            newDestination = edgePos;
            needsJumpApproach = true;
        }
        
        if (newDestination != Vector3.zero)
        {
            float distFromCurrent = Vector3.Distance(_currentDestination, newDestination);
            
            if (_currentDestination == Vector3.zero || distFromCurrent > DESTINATION_UPDATE_THRESHOLD)
            {
                _currentDestination = newDestination;
                _agent.SetDestination(_currentDestination);
            }
            
            _agent.stoppingDistance = needsJumpApproach ? 0.1f : _monsterStats.AttackDistance.Value;
        }
    }

    #endregion


    #region 복귀 상태 (Comeback)

private void Comeback()
    {
        // 방어: Agent 비활성 시 스킵
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);

        if (distanceToPlayer <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: Comeback -> Trace");
            return;
        }

        // 복귀 시 stoppingDistance를 작게 설정
        _agent.stoppingDistance = 0.1f;
        _agent.SetDestination(_defaultPosition);

        if (Vector3.Distance(transform.position, _defaultPosition) < 0.5f)
        {
            // 복귀 완료 → 순찰로 전환 (초기화 포함)
            EnterPatrolState();
            Debug.Log($"상태 전환: Comeback -> Patrol");
        }
    }

    #endregion

    #region 피격 / 사망

    /// <summary>
    /// 데미지 적용 시도. 무적이면 false 반환.
    /// 
    /// [흐름]
    /// 1. 사망/무적 체크
    /// 2. 데미지 적용
    /// 3. 무적 타이머 시작
    /// 4. 넉백 시작
    /// 5. 애니메이션 재생
    /// 6. 사망 체크
    /// </summary>
    public bool TryTakeDamage(float damage)
    {
        // 사망 상태면 완전 차단
        if (State == EMonsterState.Death)
        {
            return false;
        }

        // 무적 중이면 차단 (짧은 시간만)
        if (IsInvincible)
        {
            return false;
        }

        // === 피격 처리 시작 ===
        
        // 1. 데미지 적용
        _monsterStats.Health.Decrease(damage);
        
        // 2. 무적 타이머 시작 (연타 방지)
        float invincibilityDuration = _monsterStats.InvincibilityDuration?.Value ?? DEFAULT_INVINCIBILITY;
        _invincibilityTimer = invincibilityDuration;
        
        // 3. 넉백 시작 (기존 넉백 중이면 리셋)
        StartKnockback();
        
        // 4. 애니메이션 재생 (매 피격마다)
        _animator.SetTrigger("Hit");
        
        // 5. 사망 체크
        if (_monsterStats.Health.Value <= 0)
        {
            Debug.Log($"상태 전환: {State} -> Death");
            State = EMonsterState.Death;
            StartCoroutine(Death_Coroutine());
        }

        return true;
    }

    /// <summary>
    /// 넉백 시작. 기존 넉백 중이면 리셋하고 새로 시작.
    /// </summary>
    private void StartKnockback()
    {
        // 점프 중 피격 시 공중 넉백 처리
        bool wasJumping = State == EMonsterState.Jump && _jumpController != null && _jumpController.IsJumping;
        if (wasJumping)
        {
            _verticalKnockbackVelocity = _jumpController.CancelJump();
            _isAirborneKnockback = true;
            _agent.updatePosition = true;
            Debug.Log($"[Monster] 공중 피격 - 수직속도: {_verticalKnockbackVelocity:F2}");
        }
        
        // 넉백 전 상태 저장 (첫 피격 시만)
        if (!_isKnockbackActive)
        {
            _preKnockbackState = State;
        }

        // 넉백 활성화
        _isKnockbackActive = true;
        _knockbackTimer = 0f;
        _agent.enabled = false;  // Agent 비활성화 (간섭 차단)

        // 넉백 방향: 플레이어 반대 방향
        _knockbackVelocity = (transform.position - _player.transform.position).normalized 
                            * _monsterStats.KnockbackForce.Value;
    }

    /// <summary>
    /// 넉백 물리 적용. Update에서 매 프레임 호출.
    /// </summary>
    private void ApplyKnockBack()
    {
        if (!_isKnockbackActive) return;
        
        // 공중 넉백 처리 (점프 중 피격)
        if (_isAirborneKnockback)
        {
            ApplyAirborneKnockback();
            return;
        }

        // 넉백 타이머 증가
        _knockbackTimer += Time.deltaTime;
        
        // 넉백 완료 조건: 속도 감쇠 완료 OR 타임아웃
        bool knockbackFinished = _knockbackVelocity.sqrMagnitude < 0.01f 
                                || _knockbackTimer >= _monsterStats.KnockbackDuration.Value;
        
        if (knockbackFinished)
        {
            CompleteGroundKnockback();
            return;
        }

        // 넉백 이동 적용
        _controller.Move(_knockbackVelocity * Time.deltaTime);
        
        // 속도 감쇠
        _knockbackVelocity = Vector3.Lerp(
            _knockbackVelocity,
            Vector3.zero,
            _monsterStats.KnockbackDecay.Value * Time.deltaTime
        );
    }


    /// <summary>
    /// 지상 넉백 완료 처리. NavMesh 복구 + 상태 복귀.
    /// </summary>
private void CompleteGroundKnockback()
    {
        _knockbackVelocity = Vector3.zero;
        _isKnockbackActive = false;
        
        // NavMesh 위치 검증 후 Agent 복원
        Vector3 validPos = FindValidNavMeshPosition(transform.position);
        
        transform.position = validPos;
        _agent.enabled = true;
        _agent.Warp(validPos);
        
        // 상태 복구: 사망이 아니면 순찰로 전환
        if (State != EMonsterState.Death)
        {
            EnterPatrolState();
            
            // Hit → Idle → Trace 애니메이션 전환
            _animator.SetTrigger("HitToIdle");
            _animator.SetTrigger("IdleToTrace");
            
            _jumpController?.ResetStuckDetection();
        }
    }

    /// <summary>
    /// 공중 넉백: 중력 적용 + 포물선 낙하 + 착지 감지
    /// </summary>
    private void ApplyAirborneKnockback()
    {
        // 타이머 증가 + 타임아웃 체크
        _knockbackTimer += Time.deltaTime;
        if (_knockbackTimer >= _monsterStats.KnockbackDuration.Value)
        {
            CompleteAirborneKnockback(transform.position);
            return;
        }
        
        // 중력 적용
        _verticalKnockbackVelocity -= KNOCKBACK_GRAVITY * Time.deltaTime;
        
        // 이동: 수평 넉백 + 수직 낙하
        Vector3 movement = _knockbackVelocity + Vector3.up * _verticalKnockbackVelocity;
        _controller.Move(movement * Time.deltaTime);
        
        // 수평 넉백 감쇠
        _knockbackVelocity = Vector3.Lerp(
            _knockbackVelocity,
            Vector3.zero,
            _monsterStats.KnockbackDecay.Value * Time.deltaTime
        );
        
        // 착지 감지: 하강 중에만
        if (_verticalKnockbackVelocity < 0)
        {
            // Raycast 착지 감지
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.5f, _groundLayer))
            {
                CompleteAirborneKnockback(hit.point);
                return;
            }
            
            // CharacterController 착지 감지
            if (_controller.isGrounded)
            {
                CompleteAirborneKnockback(transform.position);
                return;
            }
        }
    }

    /// <summary>
    /// 공중 넉백 착지 완료: NavMesh 복귀 + 상태 복구
    /// </summary>
private void CompleteAirborneKnockback(Vector3 landingPoint)
    {
        _isAirborneKnockback = false;
        _isKnockbackActive = false;
        _verticalKnockbackVelocity = 0f;
        
        // NavMesh 위치 검증 후 Agent 활성화
        Vector3 validPos = FindValidNavMeshPosition(landingPoint);
        
        transform.position = validPos;
        _agent.enabled = true;
        _agent.Warp(validPos);
        
        // 상태 복구: 사망이 아니면 순찰로 전환
        if (State != EMonsterState.Death)
        {
            EnterPatrolState();
            
            // Hit → Idle → Trace 애니메이션 전환
            _animator.SetTrigger("HitToIdle");
            _animator.SetTrigger("IdleToTrace");
            
            _jumpController?.ResetStuckDetection();
        }
        
        Debug.Log($"[Monster] 공중 넥백 착지 - 위치: {transform.position}");
    }

    /// <summary>
    /// 유효한 NavMesh 위치 찾기. 못 찾으면 기본 위치 반환.
    /// </summary>
    private Vector3 FindValidNavMeshPosition(Vector3 targetPosition)
    {
        // 1차: 가까운 거리에서 탐색
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // 2차: 더 넓은 범위에서 탐색
        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Monster] NavMesh 복귀 - 확장 탐색: {hit.position}");
            return hit.position;
        }
        
        // 3차: 폴백 - 기본 위치
        Debug.LogWarning($"[Monster] NavMesh 못 찾음 - 기본 위치로 복귀");
        return _defaultPosition;
    }


    /// <summary>
    /// 사망 코루틴. 골드 드롭 + 파괴.
    /// </summary>
    private IEnumerator Death_Coroutine()
    {
        // 넉백 완료 대기 (죽으면서 밀려나는 연출)
        yield return new WaitUntil(() => !_isKnockbackActive);
        
        // 사망 시 골드 드롭 시도
        TryDropGold();
        
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
    
    /// <summary>
    /// 확률에 따라 골드 코인 드롭.
    /// </summary>
    private void TryDropGold()
    {
        if (_goldCoinPrefab == null)
        {
            Debug.LogWarning("[Monster] 골드 프리팩이 설정되지 않았습니다.", this);
            return;
        }
        
        if (Random.value > _goldDropChance) return;
        
        int dropCount = Random.Range(_goldDropCountMin, _goldDropCountMax + 1);
        Vector3 dropPosition = transform.position + Vector3.up * _goldDropHeight;
        
        for (int i = 0; i < dropCount; i++)
        {
            GameObject coin = SpawnGoldCoin(dropPosition);
            
            if (coin != null && coin.TryGetComponent(out GoldCoin goldCoin))
            {
                goldCoin.LaunchDrop();
            }
        }
        
        Debug.Log($"[Monster] {gameObject.name}이 골드 {dropCount}개를 드롭했습니다!");
    }
    
    /// <summary>
    /// 골드 코인 1개 생성. 풀링 우선.
    /// </summary>
    private GameObject SpawnGoldCoin(Vector3 position)
    {
        GameObject coin;
        
        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.HasPool(GoldCoin.POOL_TAG))
        {
            coin = ObjectPoolManager.Instance.Spawn(GoldCoin.POOL_TAG, position, Quaternion.identity);
        }
        else
        {
            coin = Instantiate(_goldCoinPrefab, position, Quaternion.identity);
        }
        
        return coin;
    }

    #endregion
}
