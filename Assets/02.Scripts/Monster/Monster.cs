using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 메인 컨트롤러. 상태 머신과 AI 행동을 담당.
/// 
/// [책임 분리]
/// - 점프: MonsterJumpController
/// - 넉백: MonsterKnockback
/// - 사망: MonsterDeath
/// 
/// [피격 시스템]
/// - 무적: 짧은 타이머로 연타 방지. State와 독립.
/// - TryTakeDamage()에서 피격 판정 → 넉백/사망 컴포넌트에 위임.
/// </summary>
public class Monster : MonoBehaviour, IDamageable
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
    
    [Header("분리된 컴포넌트")]
    [SerializeField] private MonsterJumpController _jumpController;
    [SerializeField] private MonsterKnockback _knockback;
    [SerializeField] private MonsterDeath _death;
    
    [Header("지형 설정")]
    [SerializeField] private LayerMask _groundLayer;
    
    #endregion

    #region 내부 상태
    
    private float _attackTimer;
    private Vector3 _defaultPosition;
    
    // 무적 시스템 (State와 독립)
    private float _invincibilityTimer;
    private const float DEFAULT_INVINCIBILITY = 0.1f;
    
    // 목적지 캐싱 (매 프레임 SetDestination 방지)
    private Vector3 _currentDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 1.0f;
    
    // 순찰 상태
    private Vector3 _patrolTarget;
    private float _patrolWaitTimer;
    private bool _isWaitingAtPatrolPoint;
    private const float PATROL_ARRIVAL_THRESHOLD = 0.5f;
    private const int MAX_PATROL_SAMPLE_ATTEMPTS = 10;
    
    // 점프 애니메이션 상수 (X_Bot_Jump.fbx 기준)
    private const float JUMP_CLIP_LENGTH = 2.167f;    // 전체 클립 길이 (초)
    private const float JUMP_PREPARE_TIME = 0.3f;     // 준비동작 시간 (약 9프레임 @30fps)
    
    #endregion

    #region Properties
    
    /// <summary>무적 상태 여부. UI나 디버그용.</summary>
    public bool IsInvincible => _invincibilityTimer > 0f;
    
    /// <summary>넉백 진행 중 여부.</summary>
    public bool IsKnockbackActive => _knockback != null && _knockback.IsActive;
    
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
        // 컴포넌트 자동 획득 (Inspector 미설정 시)
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_knockback == null) _knockback = GetComponent<MonsterKnockback>();
        if (_death == null) _death = GetComponent<MonsterDeath>();
        if (_jumpController == null) _jumpController = GetComponent<MonsterJumpController>();
        
        // Agent Type 자동 보정: NavMeshSurface가 Humanoid(0)로 Bake되어 있으면 일치시킴
        // 프리팩이 커스텀 Agent Type을 사용하는 경우 맵 변경 시 불일치 발생 가능
        const int HumanoidAgentTypeID = 0;
        if (_agent != null && _agent.agentTypeID != HumanoidAgentTypeID)
        {
            _agent.agentTypeID = HumanoidAgentTypeID;
        }
    }
    
private void Start()
    {
        // NavMesh 위로 자동 배치 (맵 변경 시 높이 불일치 방지)
        WarpToNavMesh();
        
        _defaultPosition = transform.position;
        _agent.speed = _monsterStats.MoveSpeed.Value;
        
        InitializeSubComponents();
        ValidateReferences();
        
        // 순찰 상태로 시작
        EnterPatrolState();
        _animator.SetTrigger("IdleToTrace");
    }
    
    /// <summary>
    /// 분리된 컴포넌트들 초기화. 의존성 주입.
    /// </summary>
    private void InitializeSubComponents()
    {
        // 점프 컨트롤러 초기화
        if (_jumpController != null)
        {
            _jumpController.Initialize(_player.transform, _agent, _groundLayer, _monsterStats);
            _jumpController.OnJumpPrepare += HandleJumpPrepare;
            _jumpController.OnJumpStarted += HandleJumpStarted;
            _jumpController.OnJumpCompleted += HandleJumpCompleted;
        }
        
        // 넉백 컨트롤러 초기화
        if (_knockback != null)
        {
            _knockback.Initialize(_controller, _agent, _animator, _monsterStats, 
                                 _jumpController, _defaultPosition, _groundLayer);
            _knockback.OnKnockbackStarted += HandleKnockbackStarted;
            _knockback.OnKnockbackCompleted += HandleKnockbackCompleted;
        }
        
        // 사망 컨트롤러 초기화 (MonsterStats로 골드 드롭 설정 전달)
        if (_death != null)
        {
            _death.Initialize(_animator, _controller, _monsterStats, () => IsKnockbackActive);
            _death.OnDeathStarted += HandleDeathStarted;
        }
    }
    
    /// <summary>
    /// 필수 참조 검증. 누락 시 경고 로그.
    /// </summary>
    private void ValidateReferences()
    {
        if (_jumpController == null)
            Debug.LogWarning($"[Monster] {gameObject.name}에 MonsterJumpController가 없습니다.", this);
        
        if (_knockback == null)
            Debug.LogWarning($"[Monster] {gameObject.name}에 MonsterKnockback이 없습니다.", this);
        
        if (_death == null)
            Debug.LogWarning($"[Monster] {gameObject.name}에 MonsterDeath가 없습니다.", this);
        
        if (!_agent.isOnNavMesh)
            Debug.LogError($"[Monster] {gameObject.name}이 NavMesh 위에 없습니다!", this);
    }

/// <summary>
    /// NavMesh 위로 자동 워프. 맵 변경 시 높이 불일치 문제 해결.
    /// NavMeshAgent는 NavMesh 표면에서 일정 거리 이내에 있어야 활성화됨.
    /// </summary>
    private void WarpToNavMesh()
    {
        // 이미 NavMesh 위에 있으면 스킵
        if (_agent.isOnNavMesh) return;
        
        // 현재 위치에서 가장 가까운 NavMesh 지점 찾기
        const float searchRadius = 50f;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            // Warp를 사용해 Agent를 NavMesh 위로 이동
            _agent.Warp(hit.position);
            Debug.Log($"[Monster] {gameObject.name} NavMesh 위로 워프: {hit.position}", this);
        }
        else
        {
            Debug.LogError($"[Monster] {gameObject.name} 주변 {searchRadius}m 내에 NavMesh가 없습니다!", this);
        }
    }


    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_jumpController != null)
        {
            _jumpController.OnJumpPrepare -= HandleJumpPrepare;
            _jumpController.OnJumpStarted -= HandleJumpStarted;
            _jumpController.OnJumpCompleted -= HandleJumpCompleted;
        }
        
        if (_knockback != null)
        {
            _knockback.OnKnockbackStarted -= HandleKnockbackStarted;
            _knockback.OnKnockbackCompleted -= HandleKnockbackCompleted;
        }
        
        if (_death != null)
        {
            _death.OnDeathStarted -= HandleDeathStarted;
        }
    }
    
    #endregion

    #region 이벤트 핸들러
    
    /// <summary>
    /// 점프 준비 시작. 애니메이션 트리거 + 속도 조정.
    /// airTime: 체공 시간 (물리 점프 시작부터 착지까지)
    /// </summary>
    private void HandleJumpPrepare(float airTime)
    {
        _currentDestination = Vector3.zero;
        State = EMonsterState.Jump;
        
        // 애니메이션 속도 계산: 체공 구간을 airTime에 맞춤
        // 전체 클립에서 준비동작 제외한 부분 = 체공 + 착지
        float animAirPortion = JUMP_CLIP_LENGTH - JUMP_PREPARE_TIME;
        float animSpeed = animAirPortion / Mathf.Max(0.1f, airTime);
        
        // 속도가 너무 느리거나 빠르면 클램프
        animSpeed = Mathf.Clamp(animSpeed, 0.5f, 2.0f);
        
        _animator.SetFloat("JumpSpeed", animSpeed);
        _animator.SetTrigger("TraceToJump");
        
        Debug.Log($"[Monster] 점프 준비 - 체공:{airTime:F2}초, 애니속도:{animSpeed:F2}");
    }
    
    private void HandleJumpStarted()
    {
        // 물리 점프 시작 시점 (Animation Event에서 트리거됨)
        // State 변경과 애니메이션 시작은 HandleJumpPrepare에서 이미 처리됨
        Debug.Log("[Monster] 물리 점프 시작");
    }
    
    private void HandleJumpCompleted()
    {
        State = EMonsterState.Trace;
        _animator.SetTrigger("JumpToTrace");
    }
    
    /// <summary>
    /// Animation Event에서 호출. 물리 점프를 시작한다.
    /// MonsterAnimationEventReceiver가 이 메서드를 호출.
    /// </summary>
    public void ExecutePhysicalJump()
    {
        _jumpController?.StartPhysicalJump();
    }
    
    private void HandleKnockbackStarted()
    {
        // 넉백 중에는 AI 로직 일시정지 (Update에서 IsKnockbackActive 체크)
    }
    
    private void HandleKnockbackCompleted(Vector3 validPosition)
    {
        // 사망 상태가 아니면 순찰로 복귀
        if (State != EMonsterState.Death)
        {
            EnterPatrolState();
            _animator.SetTrigger("HitToIdle");
            _animator.SetTrigger("IdleToTrace");
        }
    }
    
    private void HandleDeathStarted()
    {
        State = EMonsterState.Death;
    }
    
    #endregion

    #region Update 루프
    
    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;

        // 무적 타이머 감소 (항상 실행)
        UpdateInvincibility();
        
        // 점프 쿨다운 갱신
        _jumpController?.UpdateCooldown();
        
        // 넉백 물리 적용
        _knockback?.UpdateKnockback();

        // 사망 또는 넉백 중이면 AI 로직 스킵
        if (State == EMonsterState.Death) return;
        if (IsKnockbackActive) return;

        // 상태별 AI 로직
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
        }
    }

    private void Attack()
    {
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        if (distance > _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Trace;
            _animator.SetTrigger("AttackToTrace");
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _monsterStats.AttackSpeed.Value)
        {
            _attackTimer = 0f;
            _animator.SetTrigger("AttackableToAttack");
        }
    }

    #endregion

    #region 순찰 상태 (Patrol)

    private void Patrol()
    {
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
        
        // 3. 이동 중 - 도착 체크
        bool hasReachedDestination = !_agent.pathPending && 
                                     (_agent.remainingDistance <= PATROL_ARRIVAL_THRESHOLD || !_agent.hasPath);
        
        if (hasReachedDestination && _currentDestination != Vector3.zero)
        {
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

    private void HandlePatrolWait()
    {
        _patrolWaitTimer -= Time.deltaTime;
        
        if (_patrolWaitTimer <= 0f)
        {
            _isWaitingAtPatrolPoint = false;
            _patrolTarget = GetRandomPatrolPoint();
            _currentDestination = _patrolTarget;
            _agent.SetDestination(_patrolTarget);
        }
    }

    private void EnterPatrolState()
    {
        State = EMonsterState.Patrol;
        _patrolTarget = GetRandomPatrolPoint();
        _agent.speed = _monsterStats.MoveSpeed.Value;
        _agent.stoppingDistance = 0.1f;
        _isWaitingAtPatrolPoint = false;
        _currentDestination = Vector3.zero;
    }
    
    private void TransitionToTrace()
    {
        State = EMonsterState.Trace;
        _agent.speed = _monsterStats.MoveSpeed.Value;
        _isWaitingAtPatrolPoint = false;
    }

    private Vector3 GetRandomPatrolPoint()
    {
        float radius = _monsterStats.PatrolRadius.Value;
        const float MIN_DISTANCE = 2f;
        
        for (int i = 0; i < MAX_PATROL_SAMPLE_ATTEMPTS; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPoint = _defaultPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            if (Vector3.Distance(transform.position, randomPoint) < MIN_DISTANCE) continue;
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        
        return _defaultPosition;
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
            return;
        }

        if (distanceToPlayer >= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Comeback;
            return;
        }

        SetTraceDestination();
        _jumpController?.TryJumpDuringTrace();
    }

    private void SetTraceDestination()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        
        NavMeshPath path = new NavMeshPath();
        Vector3 newDestination = Vector3.zero;
        bool needsJumpApproach = false;
        
        Vector3 playerGroundPos = _jumpController?.GetPlayerGroundPosition() ?? Vector3.zero;

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
        if (!_agent.enabled || !_agent.isOnNavMesh) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);

        if (distanceToPlayer <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            return;
        }

        _agent.stoppingDistance = 0.1f;
        _agent.SetDestination(_defaultPosition);

        if (Vector3.Distance(transform.position, _defaultPosition) < 0.5f)
        {
            EnterPatrolState();
        }
    }

    #endregion

    #region 피격 (IDamageable)

    /// <summary>
    /// 데미지 적용 시도. 무적이면 false 반환.
    /// 넉백과 사망은 분리된 컴포넌트에 위임.
    /// </summary>
    public bool TryTakeDamage(Damage damage)
    {
        // 사망 상태면 완전 차단
        if (State == EMonsterState.Death) return false;
        if (_death != null && _death.IsDying) return false;

        // 무적 중이면 차단
        if (IsInvincible) return false;

        // 1. 데미지 적용
        _monsterStats.Health.Decrease(damage.Value);
        
        // 2. 무적 타이머 시작
        float invincibilityDuration = _monsterStats.InvincibilityDuration?.Value ?? DEFAULT_INVINCIBILITY;
        _invincibilityTimer = invincibilityDuration;
        
        // 3. 넉백 시작 (점프 중이면 공중 넉백)
        if (_knockback != null)
        {
            bool wasJumping = State == EMonsterState.Jump && _jumpController != null && _jumpController.IsJumping;
            float jumpVelocity = wasJumping ? _jumpController.CancelJump() : 0f;
            
            Vector3 attackerPos = damage.Who != null ? damage.Who.transform.position : _player.transform.position;
            _knockback.StartKnockback(attackerPos, wasJumping, jumpVelocity);
        }
        
        // 4. Hit 애니메이션
        _animator.SetTrigger("Hit");
        
        // 5. 사망 체크
        if (_monsterStats.Health.Value <= 0)
        {
            _death?.Die();
        }

        return true;
    }

    #endregion
}
