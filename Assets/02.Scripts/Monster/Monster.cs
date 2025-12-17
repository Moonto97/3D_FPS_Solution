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
    
    // 목적지 캐싱 (매 프레임 SetDestination 방지)
    private Vector3 _currentDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 1.0f;
    
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
            _jumpController.Initialize(_player.transform, _agent, _groundLayer);
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
        _agent.isStopped = true;
        _agent.ResetPath();

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
        if (_knockbackVelocity.sqrMagnitude < 0.01f)
        {
            _knockbackVelocity = Vector3.zero;
            return;
        }

        _controller.Move(_knockbackVelocity * Time.deltaTime);
        _knockbackVelocity = Vector3.Lerp(
            _knockbackVelocity,
            Vector3.zero,
            _monsterStats.KnockbackDecay.Value * Time.deltaTime
        );
    }

    private IEnumerator Hit_Coroutine()
    {
        yield return new WaitForSeconds(0.2f);
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
