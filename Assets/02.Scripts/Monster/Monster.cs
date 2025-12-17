using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 메인 컨트롤러. 상태 머신, 이동, 전투를 담당.
/// 점프 로직은 MonsterJumpController로 위임.
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
    
    [Header("점프 컨트롤러")]
    [SerializeField] private MonsterJumpController _jumpController;
    
    [Header("지형 설정")]
    [SerializeField] private LayerMask _groundLayer;
    
    #endregion

    #region 내부 상태
    
    private float _attackTimer = 0f;
    private Vector3 _defaultPosition;
    private Vector3 _knockbackVelocity;
    
    // 공중 넉백 (점프 중 피격 시)
    // 넉백 상태
    private bool _isKnockbackActive;      // 넉백 진행 중 여부
    private bool _isAirborneKnockback;    // 공중 넉백 여부
    private float _verticalKnockbackVelocity;
    private const float KNOCKBACK_GRAVITY = 20f;
    private float _knockbackTimer;
    
    // 목적지 캐싱 (매 프레임 SetDestination 방지)
    private Vector3 _currentDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 1.0f;
    
    #endregion

    #region Gizmos용 Public 접근자
    
    public Vector3 CurrentDestination => _currentDestination;
    public MonsterJumpController JumpController => _jumpController;
    
    #endregion

    #region 초기화
    
    private void Start()
    {
        _defaultPosition = transform.position;
        _agent.speed = _monsterStats.MoveSpeed.Value;
        _agent.stoppingDistance = _monsterStats.AttackDistance.Value;
        
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

        _jumpController?.UpdateCooldown();
        ApplyKnockBack();

        switch (State)
        {
            case EMonsterState.Idle:
                Idle();
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
    
    #endregion

    #region 기본 상태 (Idle / Attack)

    private void Idle()
    {
        if (Vector3.Distance(transform.position, _player.transform.position) <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: Idle -> Trace");
        }
    }

    private void Attack()
    {
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        if (distance > _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: Attack -> Trace");
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _monsterStats.AttackSpeed.Value)
        {
            _attackTimer = 0f;
            Debug.Log("플레이어 공격!");
            _playerStats.TakeDamage(_monsterStats.Damage.Value);
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
        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);

        if (distanceToPlayer <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: Comeback -> Trace");
            return;
        }

        _agent.SetDestination(_defaultPosition);

        if (Vector3.Distance(transform.position, _defaultPosition) < 0.5f)
        {
            State = EMonsterState.Idle;
            Debug.Log($"상태 전환: Comeback -> Idle");
        }
    }

    #endregion

    #region 피격 / 사망

    public bool TryTakeDamage(float damage)
    {
        if (State == EMonsterState.Hit || State == EMonsterState.Death)
        {
            return false;
        }

        _monsterStats.Health.Decrease(damage);
        // 점프 중 피격 시 공중 넉백 처리
        bool wasJumping = State == EMonsterState.Jump && _jumpController != null && _jumpController.IsJumping;
        if (wasJumping)
        {
            _verticalKnockbackVelocity = _jumpController.CancelJump();
            _isAirborneKnockback = true;
            _agent.updatePosition = true;
            Debug.Log($"[Monster] 공중 피격 - 수직속도: {_verticalKnockbackVelocity:F2}");
        }

        _isKnockbackActive = true;  // 넉백 시작
        _knockbackTimer = 0f;  // 타이머 초기화
        _agent.enabled = false;  // Agent 완전 비활성화 (간섭 차단)

        _knockbackVelocity = (transform.position - _player.transform.position).normalized 
                            * _monsterStats.KnockbackForce.Value;

        if (_monsterStats.Health.Value > 0)
        {
            Debug.Log($"상태 전환: {State} -> Hit");
            State = EMonsterState.Hit;
            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            Debug.Log($"상태 전환: {State} -> Death");
            State = EMonsterState.Death;
            StartCoroutine(Death_Coroutine());
        }

        return true;
    }

    private void ApplyKnockBack()
    {
        // 넉백 중 아니면 무시
        if (!_isKnockbackActive) return;
        
        // 공중 넉백 처리 (점프 중 피격)
        if (_isAirborneKnockback)
        {
            ApplyAirborneKnockback();
            return;
        }

        // 넉백 타이머 증가
        _knockbackTimer += Time.deltaTime;
        
        // 지상 넉백 완료 처리: 속도 감쇠 또는 타임아웃
        bool knockbackFinished = _knockbackVelocity.sqrMagnitude < 0.01f || _knockbackTimer >= _monsterStats.KnockbackDuration.Value;
        if (knockbackFinished)
        {
            _knockbackVelocity = Vector3.zero;
            _isKnockbackActive = false;
            
            // NavMesh 위치 검증 후 Agent 복원 (끄임 방지)
            Vector3 validPos = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                validPos = hit.position;
            }
            else if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                validPos = hit.position;
                Debug.LogWarning($"[Monster] 넉백 완료 - NavMesh 멀리 복귀: {hit.position}");
            }
            else
            {
                validPos = _defaultPosition;
                Debug.LogWarning($"[Monster] 넉백 완료 - NavMesh 못 찾음, 기본 위치로 복귀");
            }
            
            // 위치 설정 후 Agent 활성화
            transform.position = validPos;
            _agent.enabled = true;
            _agent.Warp(validPos);
            return;
        }

        _controller.Move(_knockbackVelocity * Time.deltaTime);
        _knockbackVelocity = Vector3.Lerp(
            _knockbackVelocity,
            Vector3.zero,
            _monsterStats.KnockbackDecay.Value * Time.deltaTime
        );
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
            // 타임아웃: 강제 착지
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
        
        // 착지 감지: 하강 중 + (지면 가까움 OR CharacterController 착지)
        if (_verticalKnockbackVelocity < 0)
        {
            // 방법 1: Raycast
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.5f, _groundLayer))
            {
                CompleteAirborneKnockback(hit.point);
                return;
            }
            
            // 방법 2: CharacterController.isGrounded
            if (_controller.isGrounded)
            {
                CompleteAirborneKnockback(transform.position);
                return;
            }
        }
    }

    /// <summary>
    /// 공중 넉백 착지 완료: NavMesh 복귀
    /// </summary>
    private void CompleteAirborneKnockback(Vector3 landingPoint)
    {
        _isAirborneKnockback = false;
        _isKnockbackActive = false;
        _verticalKnockbackVelocity = 0f;
        
        // NavMesh 위치 검증 후 Agent 활성화
        Vector3 validPos = landingPoint;
        if (NavMesh.SamplePosition(landingPoint, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            validPos = navHit.position;
        }
        else if (NavMesh.SamplePosition(landingPoint, out navHit, 5f, NavMesh.AllAreas))
        {
            validPos = navHit.position;
        }
        else
        {
            validPos = _defaultPosition;
        }
        
        // 위치 설정 후 Agent 활성화
        transform.position = validPos;
        _agent.enabled = true;
        _agent.Warp(validPos);
        
        Debug.Log($"[Monster] 공중 넉백 착지 - 위치: {transform.position}");
    }

    private IEnumerator Hit_Coroutine()
    {
        // 최소 피격 시간 대기
        yield return new WaitForSeconds(0.2f);
        
        // 넉백 완료 대기 (끄임 방지)
        yield return new WaitUntil(() => !_isKnockbackActive);
        
        _jumpController?.ResetStuckDetection();
        State = EMonsterState.Idle;
    }

    private IEnumerator Death_Coroutine()
    {
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    #endregion
}
