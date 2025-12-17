using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Monster : MonoBehaviour
{
    #region 설계 의도
    // 리퍼식 점프 시스템 v8:
    // 1. 플레이어가 공중에 있어도 플레이어 "지면" 방향으로 이동
    // 2. 플레이어 지면 "끝단"까지 거리로 점프 판단 (NavMesh.Raycast)
    // 3. 목적지 캐싱으로 부드러운 이동
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
    private float _jumpTargetHeight;  // 하강 점프 시 목표 높이 (재시도 판단용)
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
    
    // 목적지 캐싱 (매 프레임 SetDestination 방지)
    private Vector3 _currentDestination;
    private const float DESTINATION_UPDATE_THRESHOLD = 1.0f;
    
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

    /// <summary>
    /// 추적 목표 설정. 목적지가 1m 이상 변했을 때만 갱신하여 부드러운 이동 보장.
    /// </summary>
private void SetTraceDestination()
    {
        NavMeshPath path = new NavMeshPath();
        Vector3 newDestination = Vector3.zero;
        bool needsJumpApproach = false;  // 점프를 위해 끝단으로 이동 중인지
        
        // 1순위: 플레이어 직접 경로
        if (_agent.CalculatePath(_player.transform.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            newDestination = _player.transform.position;
        }
        // 2순위: 플레이어 지면 경로
        else if (_lastKnownPlayerGroundPos != Vector3.zero &&
                 _agent.CalculatePath(_lastKnownPlayerGroundPos, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            newDestination = _lastKnownPlayerGroundPos;
        }
        // 3순위: 몬스터 NavMesh 끝단으로 이동 (점프 준비)
        else if (TryGetMonsterNavMeshEdge(out Vector3 edgePos))
        {
            newDestination = edgePos;
            needsJumpApproach = true;
        }
        
        // 목적지가 유효하고, 이전 목적지와 충분히 다를 때만 갱신
        if (newDestination != Vector3.zero)
        {
            float distFromCurrent = Vector3.Distance(_currentDestination, newDestination);
            
            if (_currentDestination == Vector3.zero || distFromCurrent > DESTINATION_UPDATE_THRESHOLD)
            {
                _currentDestination = newDestination;
                _agent.SetDestination(_currentDestination);
            }
            
            // 점프 준비 중이면 stoppingDistance를 0으로 (끝단까지 완전히 이동)
            // 아니면 원래 공격 거리로 복원
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

    #region 점프 판단 로직

    /// <summary>
    /// 플레이어 지면의 끝단(몬스터 방향)을 찾는다.
    /// </summary>
    private bool TryGetPlatformEdge(out Vector3 edgePos, out float distanceToEdge)
    {
        edgePos = Vector3.zero;
        distanceToEdge = 0f;
        
        if (_lastKnownPlayerGroundPos == Vector3.zero) return false;
        
        Vector3 playerGround = _lastKnownPlayerGroundPos;
        Vector3 dirToMonster = transform.position - playerGround;
        dirToMonster.y = 0;
        
        if (dirToMonster.sqrMagnitude < 0.01f) return false;
        
        dirToMonster.Normalize();
        
        Vector3 rayEnd = playerGround + dirToMonster * 50f;
        
        if (NavMesh.Raycast(playerGround, rayEnd, out NavMeshHit hit, NavMesh.AllAreas))
        {
            edgePos = hit.position;
            
            Vector3 toEdge = edgePos - transform.position;
            toEdge.y = 0;
            distanceToEdge = toEdge.magnitude;
            
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 몬스터 NavMesh의 끝단(플레이어 방향)을 찾는다.
    /// </summary>
    private bool TryGetMonsterNavMeshEdge(out Vector3 edgePos)
    {
        edgePos = Vector3.zero;
        
        Vector3 dirToPlayer = _player.transform.position - transform.position;
        dirToPlayer.y = 0;
        
        if (dirToPlayer.sqrMagnitude < 0.01f) return false;
        
        dirToPlayer.Normalize();
        
        // 몬스터 위치에서 플레이어 방향으로 NavMesh 경계 탐색
        Vector3 rayEnd = transform.position + dirToPlayer * 50f;
        
        if (NavMesh.Raycast(transform.position, rayEnd, out NavMeshHit hit, NavMesh.AllAreas))
        {
            // 경계 찾음 = 몬스터 NavMesh의 끝단
            edgePos = hit.position;
            return true;
        }
        
        // 경계 없음 = 플레이어와 같은 NavMesh
        return false;
    }

    /// <summary>
    /// 하강 점프 시 착지 예상 지점을 계산한다.
    /// 몬스터 끝단에서 아래로 Raycast하여 실제 착지 가능 지점 반환.
    /// </summary>
    private bool TryGetDownwardLandingPosition(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        
        // 몬스터 끝단 찾기
        if (!TryGetMonsterNavMeshEdge(out Vector3 edgePos))
        {
            return false;
        }
        
        // 끝단에서 살짝 플레이어 방향으로 이동한 위치 (끝단 바깥)
        Vector3 dirToPlayer = _player.transform.position - edgePos;
        dirToPlayer.y = 0;
        
        if (dirToPlayer.sqrMagnitude < 0.01f) return false;
        
        dirToPlayer.Normalize();
        
        // 끝단에서 1m 더 나간 위치에서 아래로 Raycast
        Vector3 rayStart = edgePos + dirToPlayer * 1.0f + Vector3.up * 0.5f;
        
        // Physics Raycast로 실제 지형 감지
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, MAX_FALL_HEIGHT + 5f, _groundLayer))
        {
            // NavMesh 위치로 보정
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                landingPos = navHit.position;
                return true;
            }
            
            // NavMesh 없으면 Physics 결과 사용
            landingPos = hit.point;
            return true;
        }
        
        // Physics 실패 시 플레이어 지면 위치 사용
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            landingPos = _lastKnownPlayerGroundPos;
            return true;
        }
        
        return false;
    }



private bool ShouldAttemptJump()
    {
        if (_jumpCooldownTimer > 0f) return false;

        float targetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _player.transform.position.y;
            
        float heightDiff = targetHeight - transform.position.y;
        float maxJumpHeight = CalculateMaxJumpHeight();

        bool isTargetAbove = heightDiff >= _minHeightDiffForJump;
        bool isTargetBelow = heightDiff <= -_minHeightDiffForJump;
        
        // 높이 조건 체크
        if (isTargetAbove && heightDiff > maxJumpHeight) return false;
        if (isTargetBelow && Mathf.Abs(heightDiff) > MAX_FALL_HEIGHT) return false;
        if (!isTargetAbove && !isTargetBelow) return false;

        // ========== 상승/하강에 따른 수평 거리 계산 ==========
        float horizontalDist;
        
        if (isTargetAbove)
        {
            // 상승: 플레이어 플랫폼 끝단까지 거리
            if (TryGetPlatformEdge(out Vector3 platformEdge, out float distToEdge))
            {
                horizontalDist = distToEdge;
            }
            else
            {
                return false;
            }
        }
        else
        {
            // 하강: 착지 예상 지점이 실제로 아래인지 확인
            if (!TryGetDownwardLandingPosition(out Vector3 landingPos))
            {
                return false;
            }
            
            // 착지 지점이 목표(플레이어 지면)와 비슷한 높이인지 확인
            float landingHeightDiff = Mathf.Abs(landingPos.y - targetHeight);
            if (landingHeightDiff > 1.5f)
            {
                // 착지 지점이 목표와 너무 다름 = 중간 플랫폼에 착지할 것
                return false;
            }
            
            // 몬스터 끝단 → 착지 지점까지 수평 거리
            if (TryGetMonsterNavMeshEdge(out Vector3 monsterEdge))
            {
                Vector3 toLanding = landingPos - monsterEdge;
                toLanding.y = 0;
                horizontalDist = toLanding.magnitude;
            }
            else
            {
                return false;
            }
        }

        float effectiveMaxDist = isTargetBelow 
            ? CalculateMaxFallDistance(Mathf.Abs(heightDiff)) 
            : CalculateMaxJumpDistance();
            
        if (horizontalDist > effectiveMaxDist) return false;

        // ========== 경로 기반 점프 필요성 판단 ==========
        Vector3 targetPos = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos 
            : _player.transform.position;
            
        NavMeshPath path = new NavMeshPath();
        bool hasPath = _agent.CalculatePath(targetPos, path);

        string jumpType = isTargetAbove ? "상승" : "하강";

        if (!hasPath || path.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.Log($"[Monster] {jumpType} 점프 판단: 경로 없음");
            return true;
        }

        if (path.status == NavMeshPathStatus.PathPartial)
        {
            Debug.Log($"[Monster] {jumpType} 점프 판단: 경로 불완전");
            return true;
        }

        float directDistance = Vector3.Distance(transform.position, targetPos);
        float pathDistance = CalculatePathDistance(path);

        if (pathDistance > directDistance * PATH_DETOUR_THRESHOLD)
        {
            Debug.Log($"[Monster] {jumpType} 점프 판단: 우회 경로");
            return true;
        }

        if (_stuckTimer >= STUCK_THRESHOLD)
        {
            Debug.Log($"[Monster] {jumpType} 점프 판단: 이동 막힘");
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

        // ========== 상승 점프 시 천장 체크 ==========
        if (isJumpingUp)
        {
            float maxJumpHeight = CalculateMaxJumpHeight();
            
            // 몬스터 바로 위로 Raycast - 천장 있으면 점프 불가
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.up, maxJumpHeight, _groundLayer))
            {
                _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
                Debug.Log("[Monster] 점프 실패: 위에 천장 있음");
                return;
            }
        }

        _jumpStartPosition = transform.position;
        _jumpTargetPosition = landingPos;
        
        // 하강 점프 시 재시도 판단용 목표 높이 저장
        _jumpTargetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _player.transform.position.y;
        
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

        // 점프 시작 시 목적지 캐시 초기화 (착지 후 새로 계산하도록)
        _currentDestination = Vector3.zero;

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

        // ========== 착지 높이 검증: 목표보다 너무 높으면 재시도 ==========
        float landedHeight = transform.position.y;
        float heightDiffFromTarget = landedHeight - _jumpTargetHeight;
        
        if (heightDiffFromTarget > 1.5f)
        {
            // 목표보다 1.5m 이상 높은 곳에 착지 = 중간 플랫폼에 걸림
            // 쿨다운 없이 재시도 가능하게
            _jumpCooldownTimer = 0f;
            Debug.Log($"[Monster] 점프 재시도 필요: 착지높이:{landedHeight:F2}m, 목표:{_jumpTargetHeight:F2}m");
        }

        State = EMonsterState.Trace;
        Debug.Log($"[Monster] 점프 완료 - 높이: {landedHeight:F2}m");
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
        
        float targetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _player.transform.position.y;
        float heightDiff = targetHeight - transform.position.y;
        bool isTargetBelow = heightDiff <= -_minHeightDiffForJump;

        // 최대 점프 높이 (파란색 박스)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (maxJumpHeight / 2f), 
                           new Vector3(1f, maxJumpHeight, 1f));

        // 점프 가능 거리 (노란색 원)
        Gizmos.color = Color.yellow;
        DrawWireCircle(transform.position, maxJumpDistance, 32);

        // 몬스터 NavMesh 끝단 (청록색)
        if (TryGetMonsterNavMeshEdge(out Vector3 monsterEdge))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(monsterEdge, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, monsterEdge + Vector3.up * 0.2f);
        }

        // 플레이어 지면 (마젤타)
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_lastKnownPlayerGroundPos, 0.5f);
            
            if (isTargetBelow)
            {
                // ========== 하강 점프: 몬스터 끝단 → 플레이어 지면 ==========
                if (TryGetMonsterNavMeshEdge(out Vector3 edge))
                {
                    Vector3 toPlayerGround = _lastKnownPlayerGroundPos - edge;
                    toPlayerGround.y = 0;
                    float dist = toPlayerGround.magnitude;
                    float maxFallDist = CalculateMaxFallDistance(Mathf.Abs(heightDiff));
                    
                    bool canJump = dist <= maxFallDist;
                    Gizmos.color = canJump ? Color.green : Color.red;
                    
                    // 몬스터 끝단 → 플레이어 지면 (점프 판단 거리)
                    Vector3 edgeAtPlayerHeight = new Vector3(edge.x, _lastKnownPlayerGroundPos.y, edge.z);
                    Gizmos.DrawLine(edge + Vector3.up * 0.3f, edgeAtPlayerHeight + Vector3.up * 0.3f);
                    Gizmos.DrawLine(edgeAtPlayerHeight + Vector3.up * 0.3f, _lastKnownPlayerGroundPos + Vector3.up * 0.3f);
                }
            }
            else
            {
                // ========== 상승 점프: 몬스터 → 플랫폼 끝단 ==========
                if (TryGetPlatformEdge(out Vector3 platformEdge, out float distToEdge))
                {
                    // 플랫폼 끝단 (주황색)
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawSphere(platformEdge, 0.4f);
                    
                    // 점프 가능 여부
                    bool canJump = distToEdge <= maxJumpDistance;
                    Gizmos.color = canJump ? Color.green : Color.red;
                    Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, platformEdge + Vector3.up * 0.5f);
                }
            }
        }

        // 현재 목적지 (흰색)
        if (_currentDestination != Vector3.zero)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(_currentDestination, 0.3f);
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
