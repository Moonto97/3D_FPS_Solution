using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Monster : MonoBehaviour
{
    #region 설계 의도
    // 리퍼식 점프 시스템 v3:
    // 1. 고저차 감지 → 점프로 도달 가능한지 물리 계산
    // 2. 경로 없음 OR 경로가 직선거리보다 훨씬 길면 → 점프 선택
    // 3. 플레이어가 있는 "높이의 지면"으로 착지 (정확한 플레이어 위치 X)
    // 4. 고정된 점프력으로 자연스러운 포물선
    #endregion

    public EMonsterState State = EMonsterState.Idle;

    [SerializeField] private GameObject _player;
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private NavMeshAgent _agent;

    private float _attackTimer = 0f;
    private Vector3 _defaultPosition;
    private Vector3 _knockbackVelocity;

    #region 점프 변수
    
    private Vector3 _jumpStartPosition;
    private Vector3 _jumpTargetPosition;
    private Vector3 _jumpVelocity;
    private float _jumpCooldownTimer;
    
    private const float GRAVITY = 20f;
    private const float DEFAULT_JUMP_FORCE = 10f;
    private const float DEFAULT_HORIZONTAL_SPEED = 5f;
    private const float JUMP_COOLDOWN = 1.5f;
    private const float JUMP_FAIL_COOLDOWN = 3.0f;
    
    // 경로 vs 직선 비율 (이 배수 이상 돌아가면 점프 선택)
    private const float PATH_DETOUR_THRESHOLD = 2.0f;
    
    #endregion

    #region 점프 감지 설정 (Inspector)
    
    [Header("점프 설정")]
    [SerializeField, Tooltip("점프력 (수직 초기 속도)")]
    private float _jumpForce = 10f;
    
    [SerializeField, Tooltip("점프 중 수평 이동 속도")]
    private float _jumpHorizontalSpeed = 5f;

    [SerializeField, Tooltip("점프 판단 최소 고저차 (m)")]
    private float _minHeightDiffForJump = 0.5f;

    [SerializeField, Tooltip("지형 감지 레이어")]
    private LayerMask _groundLayer;
    
    #endregion

    #region 경로 막힘 감지 변수
    
    private Vector3 _lastPosition;
    private float _stuckTimer;
    private const float STUCK_THRESHOLD = 0.5f;
    
    #endregion

    private void Start()
    {
        _defaultPosition = transform.position;

        _agent.speed = _monsterStats.MoveSpeed.Value;
        _agent.stoppingDistance = _monsterStats.AttackDistance.Value;
        
        _lastPosition = transform.position;
        _stuckTimer = 0f;
        _jumpCooldownTimer = 0f;
        
        if (!_agent.isOnNavMesh)
        {
            Debug.LogError($"[Monster] {gameObject.name}이 NavMesh 위에 없습니다!", this);
        }
    }

    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;

        if (_jumpCooldownTimer > 0f)
        {
            _jumpCooldownTimer -= Time.deltaTime;
        }

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
                ExecuteJump();
                break;
            case EMonsterState.Attack:
                Attack();
                break;
        }
    }

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

        _agent.SetDestination(_player.transform.position);

        // 점프 필요 여부 판단
        if (ShouldAttemptJump())
        {
            TryStartJump();
        }

        UpdateStuckDetection();
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

    #region 점프 판단 로직

    /// <summary>
    /// 점프를 시도해야 하는 상황인지 판단한다.
    /// 핵심: 고저차가 있고 점프로 도달 가능하면 → 경로 상태 확인
    /// </summary>
private bool ShouldAttemptJump()
    {
        // 쏨다운 체크
        if (_jumpCooldownTimer > 0f)
        {
            return false;
        }

        // 점프로 도달 가능한 고저차 체크
        float heightDiff = _player.transform.position.y - transform.position.y;
        float maxJumpHeight = CalculateMaxJumpHeight();
        
        if (heightDiff < _minHeightDiffForJump)
        {
            // 플레이어가 아래에 있거나 같은 높이
            return false;
        }
        
        if (heightDiff > maxJumpHeight)
        {
            Debug.Log($"[Monster] 점프 불가: 높이차({heightDiff:F2}m) > 최대점프({maxJumpHeight:F2}m) - jumpForce 증가 필요");
            return false;
        }

        // 경로 상태 확인
        NavMeshPath path = new NavMeshPath();
        bool hasPath = _agent.CalculatePath(_player.transform.position, path);

        // Case A: 경로가 없거나 불완전
        if (!hasPath || path.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.Log("[Monster] 점프 판단: 경로 없음 → 점프 시도");
            return true;
        }

        if (path.status == NavMeshPathStatus.PathPartial)
        {
            Debug.Log("[Monster] 점프 판단: 경로 불완전 → 점프 시도");
            return true;
        }

        // Case B: 경로가 직선거리 대비 너무 멀면 점프 유리
        float directDistance = Vector3.Distance(transform.position, _player.transform.position);
        float pathDistance = CalculatePathDistance(path);

        if (pathDistance > directDistance * PATH_DETOUR_THRESHOLD)
        {
            Debug.Log($"[Monster] 점프 판단: 우회 경로 ({pathDistance:F1}m > 직선 {directDistance:F1}m x {PATH_DETOUR_THRESHOLD}) → 점프 시도");
            return true;
        }

        // Case C: 막혀서 못 움직이는 상태
        if (_stuckTimer >= STUCK_THRESHOLD)
        {
            Debug.Log("[Monster] 점프 판단: 이동 막힘 → 점프 시도");
            return true;
        }

        return false;
    }

    /// <summary>
    /// NavMeshPath의 총 거리 계산
    /// </summary>
    private float CalculatePathDistance(NavMeshPath path)
    {
        if (path.corners.Length < 2) return 0f;

        float distance = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return distance;
    }

    /// <summary>
    /// 점프력으로 플레이어 높이에 도달할 수 있는지 물리 계산
    /// 최대 점프 높이 = v² / (2g)
    /// </summary>
    private bool CanJumpToPlayerHeight()
    {
        float heightDiff = _player.transform.position.y - transform.position.y;
        
        // 플레이어가 아래에 있거나 너무 가까운 높이면 점프 불필요
        if (heightDiff < _minHeightDiffForJump)
        {
            return false;
        }

        // 점프력으로 도달 가능한 최대 높이
        float maxJumpHeight = CalculateMaxJumpHeight();
        
        // 여유 10% 두고 판단 (정확히 맞추기 어려우므로)
        bool canReach = heightDiff <= maxJumpHeight * 0.9f;
        
        return canReach;
    }

    /// <summary>
    /// 현재 점프력으로 도달 가능한 최대 높이
    /// </summary>
    private float CalculateMaxJumpHeight()
    {
        float jumpForce = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        return (jumpForce * jumpForce) / (2f * GRAVITY);
    }

    private void UpdateStuckDetection()
    {
        float movedDistance = Vector3.Distance(transform.position, _lastPosition);

        if (movedDistance < 0.05f)
        {
            _stuckTimer += Time.deltaTime;
        }
        else
        {
            _stuckTimer = 0f;
            _lastPosition = transform.position;
        }
    }

    #endregion

    #region 점프 실행 로직

    /// <summary>
    /// 점프 시작 - 플레이어 높이의 지면을 찾아 착지점으로 설정
    /// </summary>
    private void TryStartJump()
    {
        // 플레이어 높이의 착지 가능 지면 찾기
        if (!TryFindLandingAtPlayerHeight(out Vector3 landingPos))
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            Debug.Log("[Monster] 점프 실패: 착지점을 찾지 못함");
            return;
        }

        // 착지점이 현재보다 높은지 최종 확인
        float heightGain = landingPos.y - transform.position.y;
        if (heightGain < _minHeightDiffForJump)
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            Debug.Log($"[Monster] 점프 취소: 착지점이 낮음 (높이차: {heightGain:F2}m)");
            return;
        }

        // 점프 시작 설정
        _jumpStartPosition = transform.position;
        _jumpTargetPosition = landingPos;
        
        // 수평 방향 (착지점 방향)
        Vector3 horizontalDir = (landingPos - transform.position);
        horizontalDir.y = 0;
        
        if (horizontalDir.sqrMagnitude > 0.01f)
        {
            horizontalDir.Normalize();
        }
        else
        {
            horizontalDir = transform.forward;
        }

        // 고정된 초기 속도 설정
        float horizontalSpeed = _jumpHorizontalSpeed > 0 ? _jumpHorizontalSpeed : DEFAULT_HORIZONTAL_SPEED;
        float verticalSpeed = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        
        _jumpVelocity = horizontalDir * horizontalSpeed + Vector3.up * verticalSpeed;
        
        _jumpCooldownTimer = JUMP_COOLDOWN;

        // NavMeshAgent 비활성화
        _agent.isStopped = true;
        _agent.updatePosition = false;

        State = EMonsterState.Jump;

        Debug.Log($"[Monster] 점프 시작 - 착지점:{landingPos}, 높이차:{heightGain:F2}m, 최대도달:{CalculateMaxJumpHeight():F2}m");
    }

    /// <summary>
    /// 플레이어가 있는 높이의 NavMesh 지면을 찾는다.
    /// </summary>
private bool TryFindLandingAtPlayerHeight(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        float targetHeight = _player.transform.position.y;
        float myHeight = transform.position.y;
        float maxJumpHeight = CalculateMaxJumpHeight();

        Debug.Log($"[Monster] 착지점 탐색 - 플레이어:{targetHeight:F2}, 몬스터:{myHeight:F2}, 최대점프높이:{maxJumpHeight:F2}");

        Vector3 dirToPlayer = _player.transform.position - transform.position;
        dirToPlayer.y = 0;
        if (dirToPlayer.sqrMagnitude < 0.01f) dirToPlayer = transform.forward;
        dirToPlayer.Normalize();

        // 탐색할 높이 범위: 몬스터 높이에서 최대 점프 높이까지
        float[] searchHeights = {
            myHeight + maxJumpHeight,           // 최대 점프 도달 높이
            myHeight + maxJumpHeight * 0.8f,
            myHeight + maxJumpHeight * 0.6f,
            myHeight + maxJumpHeight * 0.4f,
            targetHeight                        // 플레이어 현재 높이
        };

        float[] distances = { 3f, 4f, 5f, 6f, 2f, 7f, 8f };
        
        float bestHeight = -999f;
        Vector3 bestLanding = Vector3.zero;

        // 플레이어 방향으로 여러 높이/거리에서 탐색
        foreach (float dist in distances)
        {
            foreach (float height in searchHeights)
            {
                Vector3 searchPos = transform.position + dirToPlayer * dist;
                searchPos.y = height;

                // 반경 2f로 NavMesh 탐색
                if (NavMesh.SamplePosition(searchPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    float foundHeight = hit.position.y;
                    
                    // 조건: 몬스터보다 높고 + 점프로 도달 가능
                    float heightGain = foundHeight - myHeight;
                    if (heightGain >= _minHeightDiffForJump && heightGain <= maxJumpHeight)
                    {
                        // 가장 높은 위치 선택 (플레이어에게 가까워지는 위치)
                        if (foundHeight > bestHeight)
                        {
                            bestHeight = foundHeight;
                            bestLanding = hit.position;
                        }
                    }
                }
            }
        }

        // 착지점 발견
        if (bestHeight > -999f)
        {
            landingPos = bestLanding;
            Debug.Log($"[Monster] 착지점 확정: {landingPos}, 높이상승:{bestHeight - myHeight:F2}m");
            return true;
        }

        // 플레이어 위치 직접 탐색 (백업)
        if (NavMesh.SamplePosition(_player.transform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
        {
            float heightGain = playerHit.position.y - myHeight;
            if (heightGain >= _minHeightDiffForJump && heightGain <= maxJumpHeight)
            {
                landingPos = playerHit.position;
                Debug.Log($"[Monster] 착지점 확정 (플레이어 위치): {landingPos}, 높이상승:{heightGain:F2}m");
                return true;
            }
        }

        Debug.Log("[Monster] 점프 가능한 지형을 찾지 못함");
        return false;
    }

    /// <summary>
    /// 물리 기반 점프 실행 (매 프레임)
    /// </summary>
    private void ExecuteJump()
    {
        // 중력 적용
        _jumpVelocity.y -= GRAVITY * Time.deltaTime;

        // 위치 업데이트
        transform.position += _jumpVelocity * Time.deltaTime;

        // 이동 방향으로 회전
        Vector3 horizontalVel = new Vector3(_jumpVelocity.x, 0, _jumpVelocity.z);
        if (horizontalVel.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalVel),
                Time.deltaTime * 10f
            );
        }

        // 착지 감지: 하강 중이고 지면에 닿았을 때
        if (_jumpVelocity.y < 0)
        {
            // 발 아래 지면 체크
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.4f, _groundLayer))
            {
                CompleteJump(hit.point);
                return;
            }

            // NavMesh 체크 (백업)
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 0.5f, NavMesh.AllAreas))
            {
                if (transform.position.y <= navHit.position.y + 0.3f)
                {
                    CompleteJump(navHit.position);
                    return;
                }
            }
        }

        // 안전장치: 시작점보다 너무 아래로 떨어지면 강제 착지
        if (transform.position.y < _jumpStartPosition.y - 3f)
        {
            Debug.LogWarning("[Monster] 점프 타임아웃 - 강제 착지");
            ForceCompleteJump();
        }
    }

    /// <summary>
    /// 점프 완료 처리
    /// </summary>
    private void CompleteJump(Vector3 landingPosition)
    {
        // NavMesh 위로 Warp
        if (NavMesh.SamplePosition(landingPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }
        else
        {
            _agent.Warp(landingPosition);
            Debug.LogWarning($"[Monster] 착지점이 NavMesh 밖: {landingPosition}");
        }

        // NavMeshAgent 재활성화
        _agent.updatePosition = true;
        _agent.isStopped = false;
        
        // 경로 재설정 (즉시 추적 재개)
        _agent.ResetPath();
        _agent.SetDestination(_player.transform.position);

        _stuckTimer = 0f;
        _lastPosition = transform.position;

        State = EMonsterState.Trace;
        Debug.Log($"[Monster] 점프 완료 - 착지: {transform.position}, 높이: {transform.position.y:F2}m");
    }

    /// <summary>
    /// 강제 착지 (안전장치)
    /// </summary>
    private void ForceCompleteJump()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }
        else
        {
            _agent.Warp(_jumpStartPosition);
        }

        _agent.updatePosition = true;
        _agent.isStopped = false;
        _agent.ResetPath();
        _agent.SetDestination(_player.transform.position);

        _stuckTimer = 0f;
        _lastPosition = transform.position;
        _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;

        State = EMonsterState.Trace;
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
        State = EMonsterState.Idle;
    }

    private IEnumerator Death_Coroutine()
    {
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    #endregion

    #region 디버그 시각화

    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;

        float maxJumpHeight = CalculateMaxJumpHeight();

        // 최대 점프 높이 (파란색)
        Gizmos.color = Color.cyan;
        Vector3 maxHeightPos = transform.position + Vector3.up * maxJumpHeight;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (maxJumpHeight / 2f), 
                           new Vector3(1f, maxJumpHeight, 1f));

        // 플레이어 높이 라인 (녹색)
        Gizmos.color = Color.green;
        Vector3 playerHeightLine = new Vector3(transform.position.x, _player.transform.position.y, transform.position.z);
        Gizmos.DrawWireSphere(playerHeightLine, 0.3f);
        Gizmos.DrawLine(transform.position, playerHeightLine);

        // 도달 가능 여부 표시
        float heightDiff = _player.transform.position.y - transform.position.y;
        bool canReach = heightDiff > 0 && heightDiff <= maxJumpHeight * 0.9f;
        
        Gizmos.color = canReach ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position + Vector3.up * maxJumpHeight, 
                       new Vector3(transform.position.x, _player.transform.position.y, transform.position.z));

        // 점프 중이면 궤적 표시 (빨간색)
        if (State == EMonsterState.Jump)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_jumpStartPosition, 0.3f);
            Gizmos.DrawSphere(_jumpTargetPosition, 0.3f);
            Gizmos.DrawLine(_jumpStartPosition, _jumpTargetPosition);
        }
    }

    #endregion
}
