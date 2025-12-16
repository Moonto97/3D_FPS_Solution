using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Monster : MonoBehaviour
{
    #region 설계 의도
    // 리퍼식 점프 시스템 v7:
    // 1. 플레이어가 공중에 있어도 플레이어 "지면" 방향으로 이동
    // 2. 플레이어 지면 "끝단"까지 거리로 점프 판단 (NavMesh.Raycast)
    // 3. 상승/하강 점프 모두 지원
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
    private const float PATH_DETOUR_THRESHOLD = 2.0f;
    private const float MAX_FALL_HEIGHT = 10f;
    
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
    
    // 플레이어 지면 추적용
    private Vector3 _lastKnownPlayerGroundPos;
    
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

        UpdatePlayerGroundPosition();
        SetTraceDestination();

        if (ShouldAttemptJump())
        {
            TryStartJump();
        }

        UpdateStuckDetection();
    }

    private void UpdatePlayerGroundPosition()
    {
        Vector3 playerPos = _player.transform.position;
        
        if (NavMesh.SamplePosition(playerPos, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
        {
            _lastKnownPlayerGroundPos = navHit.position;
            return;
        }
        
        if (_groundLayer.value != 0)
        {
            if (Physics.Raycast(playerPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 20f, _groundLayer))
            {
                if (NavMesh.SamplePosition(hit.point, out NavMeshHit groundNavHit, 2f, NavMesh.AllAreas))
                {
                    _lastKnownPlayerGroundPos = groundNavHit.position;
                    return;
                }
            }
        }
        
        Vector3 sameHeightPos = new Vector3(playerPos.x, transform.position.y, playerPos.z);
        if (NavMesh.SamplePosition(sameHeightPos, out NavMeshHit sameHeightHit, 5f, NavMesh.AllAreas))
        {
            _lastKnownPlayerGroundPos = sameHeightHit.position;
        }
    }

    private void SetTraceDestination()
    {
        NavMeshPath path = new NavMeshPath();
        
        if (_agent.CalculatePath(_player.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            _agent.SetDestination(_player.transform.position);
            return;
        }
        
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            if (_agent.CalculatePath(_lastKnownPlayerGroundPos, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                _agent.SetDestination(_lastKnownPlayerGroundPos);
                return;
            }
        }
        
        Vector3 dirToPlayer = _player.transform.position - transform.position;
        dirToPlayer.y = 0;
        
        if (dirToPlayer.sqrMagnitude < 0.1f) return;
        
        dirToPlayer.Normalize();
        
        Vector3 bestTarget = Vector3.zero;
        float bestDist = 0f;
        
        for (float dist = 1f; dist <= 15f; dist += 1f)
        {
            Vector3 targetPos = transform.position + dirToPlayer * dist;
            
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                if (_agent.CalculatePath(navHit.position, path) && 
                    (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial))
                {
                    bestTarget = navHit.position;
                    bestDist = dist;
                }
            }
        }
        
        if (bestDist > 0f)
        {
            _agent.SetDestination(bestTarget);
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

    #region 점프 판단 로직

    /// <summary>
    /// 플레이어 지면의 끝단(몬스터 방향)을 찾는다.
    /// NavMesh.Raycast로 경계 탐색.
    /// </summary>
    /// <returns>true: 끝단 찾음 (다른 NavMesh), false: 같은 NavMesh</returns>
    private bool TryGetPlatformEdge(out Vector3 edgePos, out float distanceToEdge)
    {
        edgePos = Vector3.zero;
        distanceToEdge = 0f;
        
        if (_lastKnownPlayerGroundPos == Vector3.zero) return false;
        
        // 플레이어 지면에서 몬스터 방향 계산
        Vector3 playerGround = _lastKnownPlayerGroundPos;
        Vector3 dirToMonster = transform.position - playerGround;
        dirToMonster.y = 0;
        
        if (dirToMonster.sqrMagnitude < 0.01f) return false;
        
        dirToMonster.Normalize();
        
        // NavMesh 경계 탐색 (플레이어 지면 → 몬스터 방향)
        Vector3 rayEnd = playerGround + dirToMonster * 50f;
        
        if (NavMesh.Raycast(playerGround, rayEnd, out NavMeshHit hit, NavMesh.AllAreas))
        {
            // 경계 찾음 = 다른 NavMesh 영역
            edgePos = hit.position;
            
            // 몬스터와 끝단 사이의 수평 거리
            Vector3 toEdge = edgePos - transform.position;
            toEdge.y = 0;
            distanceToEdge = toEdge.magnitude;
            
            return true;
        }
        
        // 경계 없음 = 같은 NavMesh (걸어가면 됨)
        return false;
    }

    private bool ShouldAttemptJump()
    {
        if (_jumpCooldownTimer > 0f) return false;

        // 플레이어 지면 기준으로 높이차 계산
        float targetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _player.transform.position.y;
            
        float heightDiff = targetHeight - transform.position.y;
        float maxJumpHeight = CalculateMaxJumpHeight();

        // 높이 조건 체크
        bool isTargetAbove = heightDiff >= _minHeightDiffForJump;
        bool isTargetBelow = heightDiff <= -_minHeightDiffForJump;
        
        if (isTargetAbove && heightDiff > maxJumpHeight) return false;
        if (isTargetBelow && Mathf.Abs(heightDiff) > MAX_FALL_HEIGHT) return false;
        if (!isTargetAbove && !isTargetBelow) return false;

        // 수평 거리 조건: 플레이어 지면 끝단까지 거리로 판단
        float horizontalDist;
        
        if (TryGetPlatformEdge(out Vector3 edgePos, out float distToEdge))
        {
            // 끝단 찾음 → 끝단까지 거리로 판단
            horizontalDist = distToEdge;
        }
        else
        {
            // 같은 NavMesh → 점프 불필요
            return false;
        }

        // 최대 점프 거리 계산
        float effectiveMaxDist = isTargetBelow 
            ? CalculateMaxFallDistance(Mathf.Abs(heightDiff)) 
            : CalculateMaxJumpDistance();
            
        if (horizontalDist > effectiveMaxDist) return false;

        // 경로 상태 확인
        Vector3 targetPos = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos 
            : _player.transform.position;
            
        NavMeshPath path = new NavMeshPath();
        bool hasPath = _agent.CalculatePath(targetPos, path);

        if (!hasPath || path.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.Log($"[Monster] 점프 판단: 경로 없음 → 점프 (높이차:{heightDiff:F2}m, 끝단거리:{horizontalDist:F2}m)");
            return true;
        }

        if (path.status == NavMeshPathStatus.PathPartial)
        {
            Debug.Log($"[Monster] 점프 판단: 경로 불완전 → 점프 (끝단거리:{horizontalDist:F2}m)");
            return true;
        }

        float directDistance = Vector3.Distance(transform.position, targetPos);
        float pathDistance = CalculatePathDistance(path);

        if (pathDistance > directDistance * PATH_DETOUR_THRESHOLD)
        {
            Debug.Log($"[Monster] 점프 판단: 우회 경로 → 점프");
            return true;
        }

        if (_stuckTimer >= STUCK_THRESHOLD)
        {
            Debug.Log("[Monster] 점프 판단: 이동 막힘 → 점프");
            return true;
        }

        return false;
    }

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

    private float CalculateMaxJumpHeight()
    {
        float jumpForce = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        return (jumpForce * jumpForce) / (2f * GRAVITY);
    }

    private float CalculateMaxJumpDistance()
    {
        float jumpForce = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        float horizontalSpeed = _jumpHorizontalSpeed > 0 ? _jumpHorizontalSpeed : DEFAULT_HORIZONTAL_SPEED;
        
        float airTime = 2f * jumpForce / GRAVITY;
        return airTime * horizontalSpeed * 0.7f;
    }

    private float CalculateMaxFallDistance(float fallHeight)
    {
        float horizontalSpeed = _jumpHorizontalSpeed > 0 ? _jumpHorizontalSpeed : DEFAULT_HORIZONTAL_SPEED;
        float jumpForce = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        
        float riseTime = jumpForce / GRAVITY;
        float totalFallHeight = fallHeight + CalculateMaxJumpHeight();
        float fallTime = Mathf.Sqrt(2f * totalFallHeight / GRAVITY);
        
        float totalAirTime = riseTime + fallTime;
        
        return totalAirTime * horizontalSpeed * 0.7f;
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

    private void TryStartJump()
    {
        if (!TryFindLandingPosition(out Vector3 landingPos))
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            Debug.Log("[Monster] 점프 실패: 착지점을 찾지 못함");
            return;
        }

        float heightDiff = landingPos.y - transform.position.y;
        bool isJumpingUp = heightDiff > 0;
        
        if (Mathf.Abs(heightDiff) < _minHeightDiffForJump)
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            return;
        }

        _jumpStartPosition = transform.position;
        _jumpTargetPosition = landingPos;
        
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

        float horizontalSpeed = _jumpHorizontalSpeed > 0 ? _jumpHorizontalSpeed : DEFAULT_HORIZONTAL_SPEED;
        float verticalSpeed = _jumpForce > 0 ? _jumpForce : DEFAULT_JUMP_FORCE;
        
        _jumpVelocity = horizontalDir * horizontalSpeed + Vector3.up * verticalSpeed;
        _jumpCooldownTimer = JUMP_COOLDOWN;

        _agent.isStopped = true;
        _agent.updatePosition = false;

        State = EMonsterState.Jump;

        Vector3 toTarget = landingPos - transform.position;
        toTarget.y = 0;
        string jumpType = isJumpingUp ? "상승" : "하강";
        Debug.Log($"[Monster] {jumpType} 점프 - 높이:{heightDiff:F2}m, 거리:{toTarget.magnitude:F2}m");
    }

    private bool TryFindLandingPosition(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        float myHeight = transform.position.y;
        
        float targetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _player.transform.position.y;
        float heightDiff = targetHeight - myHeight;
        float maxJumpHeight = CalculateMaxJumpHeight();
        
        bool isJumpingUp = heightDiff > 0;
        float maxJumpDistance = isJumpingUp 
            ? CalculateMaxJumpDistance() 
            : CalculateMaxFallDistance(Mathf.Abs(heightDiff));

        Vector3 targetPos = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos 
            : _player.transform.position;
        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0;
        if (dirToTarget.sqrMagnitude < 0.01f) dirToTarget = transform.forward;
        dirToTarget.Normalize();

        float[] searchHeights;
        if (isJumpingUp)
        {
            searchHeights = new float[] {
                myHeight + maxJumpHeight,
                myHeight + maxJumpHeight * 0.8f,
                myHeight + maxJumpHeight * 0.6f,
                targetHeight
            };
        }
        else
        {
            searchHeights = new float[] {
                targetHeight,
                targetHeight + 0.5f,
                targetHeight - 0.5f,
                targetHeight + 1f
            };
        }

        float[] distances = { 2f, 3f, 4f, 5f, 6f, 7f, 8f };
        
        float bestScore = float.MinValue;
        Vector3 bestLanding = Vector3.zero;
        bool found = false;

        foreach (float dist in distances)
        {
            if (dist > maxJumpDistance) continue;

            foreach (float height in searchHeights)
            {
                Vector3 searchPos = transform.position + dirToTarget * dist;
                searchPos.y = height;

                if (NavMesh.SamplePosition(searchPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    float foundHeight = hit.position.y;
                    float foundHeightDiff = foundHeight - myHeight;
                    
                    bool validHeight;
                    if (isJumpingUp)
                    {
                        validHeight = foundHeightDiff >= _minHeightDiffForJump && foundHeightDiff <= maxJumpHeight;
                    }
                    else
                    {
                        validHeight = foundHeightDiff <= -_minHeightDiffForJump && Mathf.Abs(foundHeightDiff) <= MAX_FALL_HEIGHT;
                    }
                    
                    if (validHeight)
                    {
                        Vector3 toHit = hit.position - transform.position;
                        toHit.y = 0;
                        float horizontalDist = toHit.magnitude;

                        if (horizontalDist <= maxJumpDistance)
                        {
                            float heightScore = -Mathf.Abs(foundHeight - targetHeight);
                            float score = heightScore * 2f - horizontalDist * 0.3f;
                            
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestLanding = hit.position;
                                found = true;
                            }
                        }
                    }
                }
            }
        }

        if (found)
        {
            landingPos = bestLanding;
            return true;
        }

        // 백업: 플레이어 지면 위치 직접 탐색
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            if (NavMesh.SamplePosition(_lastKnownPlayerGroundPos, out NavMeshHit groundHit, 3f, NavMesh.AllAreas))
            {
                float foundHeightDiff = groundHit.position.y - myHeight;
                Vector3 toGround = groundHit.position - transform.position;
                toGround.y = 0;
                float horizontalDist = toGround.magnitude;

                bool validHeight = isJumpingUp
                    ? (foundHeightDiff >= _minHeightDiffForJump && foundHeightDiff <= maxJumpHeight)
                    : (foundHeightDiff <= -_minHeightDiffForJump && Mathf.Abs(foundHeightDiff) <= MAX_FALL_HEIGHT);

                if (validHeight && horizontalDist <= maxJumpDistance)
                {
                    landingPos = groundHit.position;
                    return true;
                }
            }
        }

        return false;
    }

    private void ExecuteJump()
    {
        _jumpVelocity.y -= GRAVITY * Time.deltaTime;
        transform.position += _jumpVelocity * Time.deltaTime;

        Vector3 horizontalVel = new Vector3(_jumpVelocity.x, 0, _jumpVelocity.z);
        if (horizontalVel.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalVel),
                Time.deltaTime * 10f
            );
        }

        if (_jumpVelocity.y < 0)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.5f, _groundLayer))
            {
                CompleteJump(hit.point);
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 0.6f, NavMesh.AllAreas))
            {
                if (transform.position.y <= navHit.position.y + 0.4f)
                {
                    CompleteJump(navHit.position);
                    return;
                }
            }
        }

        if (transform.position.y < _jumpStartPosition.y - MAX_FALL_HEIGHT - 2f)
        {
            Debug.LogWarning("[Monster] 점프 타임아웃 - 강제 착지");
            ForceCompleteJump();
        }
    }

    private void CompleteJump(Vector3 landingPosition)
    {
        if (NavMesh.SamplePosition(landingPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }
        else
        {
            _agent.Warp(landingPosition);
        }

        _agent.updatePosition = true;
        _agent.isStopped = false;
        _agent.ResetPath();
        _agent.SetDestination(_player.transform.position);

        _stuckTimer = 0f;
        _lastPosition = transform.position;

        State = EMonsterState.Trace;
        Debug.Log($"[Monster] 점프 완료 - 높이: {transform.position.y:F2}m");
    }

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
        float maxJumpDistance = CalculateMaxJumpDistance();

        // 최대 점프 높이 (파란색)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (maxJumpHeight / 2f), 
                           new Vector3(1f, maxJumpHeight, 1f));

        // 상승 점프 거리 (노란색)
        Gizmos.color = Color.yellow;
        DrawWireCircle(transform.position, maxJumpDistance, 32);

        // 플레이어 지면 위치 (마젠타)
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_lastKnownPlayerGroundPos, 0.5f);
            
            // ========== NavMesh.Raycast 시각화 ==========
            Vector3 playerGround = _lastKnownPlayerGroundPos;
            Vector3 dirToMonster = transform.position - playerGround;
            dirToMonster.y = 0;
            
            if (dirToMonster.sqrMagnitude > 0.01f)
            {
                dirToMonster.Normalize();
                Vector3 rayEnd = playerGround + dirToMonster * 50f;
                
                // 레이캐스트 시작점 (흰색 구)
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(playerGround + Vector3.up * 0.1f, 0.2f);
                
                if (NavMesh.Raycast(playerGround, rayEnd, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    // 경계 찾음: 시작 → 끝단 (녹색), 끝단 → 레이 끝 (빨간 점선)
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(playerGround + Vector3.up * 0.1f, hit.position + Vector3.up * 0.1f);
                    
                    // 끝단 위치 (주황색 구)
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawSphere(hit.position, 0.4f);
                    
                    // 끝단 이후 (빨간색 - NavMesh 없는 구간)
                    Gizmos.color = Color.red;
                    DrawDashedLine(hit.position + Vector3.up * 0.1f, rayEnd + Vector3.up * 0.1f, 0.5f);
                    
                    // 몬스터 ↔ 끝단 거리 (점프 판단 거리)
                    Vector3 toEdge = hit.position - transform.position;
                    toEdge.y = 0;
                    float distToEdge = toEdge.magnitude;
                    
                    // 점프 가능 여부 표시
                    bool canJump = distToEdge <= maxJumpDistance;
                    Gizmos.color = canJump ? Color.green : Color.red;
                    Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, hit.position + Vector3.up * 0.5f);
                    
                    // 거리 텍스트 위치에 구 표시
                    Gizmos.DrawSphere(hit.position + Vector3.up * 1f, 0.15f);
                }
                else
                {
                    // 경계 없음: 전체 레이 (파란색) - 같은 NavMesh
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(playerGround + Vector3.up * 0.1f, rayEnd + Vector3.up * 0.1f);
                }
            }
        }

        // 점프 중
        if (State == EMonsterState.Jump)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_jumpStartPosition, 0.3f);
            Gizmos.DrawSphere(_jumpTargetPosition, 0.3f);
            Gizmos.DrawLine(_jumpStartPosition, _jumpTargetPosition);
        }
    }

    private void DrawDashedLine(Vector3 start, Vector3 end, float dashLength)
    {
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        
        for (float i = 0; i < distance; i += dashLength * 2)
        {
            Vector3 dashStart = start + direction * i;
            Vector3 dashEnd = start + direction * Mathf.Min(i + dashLength, distance);
            Gizmos.DrawLine(dashStart, dashEnd);
        }
    }

    private void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    #endregion
}
