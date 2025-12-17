using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 점프 판단/실행/착지를 담당하는 컨트롤러.
/// Monster.cs에서 분리되어 단일 책임 원칙(SRP)을 따름.
/// 점프 관련 스탯은 MonsterStats에서 관리.
/// </summary>
public class MonsterJumpController : MonoBehaviour
{
    #region 이벤트 (Monster.cs와 통신용)
    
    /// <summary>점프 시작 시 발생. Monster가 상태를 Jump로 전환해야 함.</summary>
    public event Action OnJumpStarted;
    
    /// <summary>점프 완료 시 발생. Monster가 상태를 Trace로 전환해야 함.</summary>
    public event Action OnJumpCompleted;
    
    #endregion

    #region 외부 참조 (Monster.cs에서 주입)
    
    private Transform _playerTransform;
    private NavMeshAgent _agent;
    private LayerMask _groundLayer;
    private MonsterStats _stats;
    
    #endregion

    #region 상수
    
    private const float GRAVITY = 20f;
    private const float JUMP_COOLDOWN = 1.5f;
    private const float JUMP_FAIL_COOLDOWN = 3.0f;
    private const float PATH_DETOUR_THRESHOLD = 2.0f;
    private const float MAX_FALL_HEIGHT = 10f;
    
    #endregion

    #region 스탯 접근자 (MonsterStats에서 값 가져오기)
    
    // null 체크 + 기본값으로 안전하게 접근
    private float JumpForce => _stats?.JumpForce?.Value ?? 10f;
    private float JumpHorizontalSpeed => _stats?.JumpHorizontalSpeed?.Value ?? 5f;
    private float MinHeightDiff => _stats?.MinHeightDiffForJump?.Value ?? 0.5f;
    private float StuckThreshold => _stats?.StuckThreshold?.Value ?? 0.5f;
    
    #endregion

    #region 내부 상태
    
    private Vector3 _jumpStartPosition;
    private Vector3 _jumpTargetPosition;
    private Vector3 _jumpVelocity;
    private float _jumpTargetHeight;
    private float _jumpCooldownTimer;
    
    private Vector3 _lastKnownPlayerGroundPos;
    private Vector3 _lastPosition;
    private float _stuckTimer;
    
    public bool IsJumping { get; private set; }
    public bool IsInitialized => _playerTransform != null;
    
    #endregion

    #region 초기화
    
    /// <summary>
    /// Monster.cs에서 호출하여 필요한 참조를 주입한다.
    /// </summary>
    public void Initialize(Transform playerTransform, NavMeshAgent agent, LayerMask groundLayer, MonsterStats stats)
    {
        _playerTransform = playerTransform;
        _agent = agent;
        _groundLayer = groundLayer;
        _stats = stats;
        
        _lastPosition = transform.position;
        _jumpCooldownTimer = 0f;
        _stuckTimer = 0f;
        IsJumping = false;
        
        if (_stats == null)
        {
            Debug.LogWarning($"[JumpController] {gameObject.name}: MonsterStats가 null입니다. 기본값 사용.", this);
        }
    }
    
    #endregion

    #region Public API
    
    public void UpdateCooldown()
    {
        if (_jumpCooldownTimer > 0f)
        {
            _jumpCooldownTimer -= Time.deltaTime;
        }
    }
    
    public void TryJumpDuringTrace()
    {
        UpdatePlayerGroundPosition();
        
        if (ShouldAttemptJump())
        {
            TryStartJump();
        }
        
        UpdateStuckDetection();
    }
    
    public void UpdateJump()
    {
        if (!IsJumping) return;
        ExecuteJump();
    }
    
    public Vector3 GetPlayerGroundPosition() => _lastKnownPlayerGroundPos;
    
    public bool TryGetNavMeshEdge(out Vector3 edgePos) => TryGetMonsterNavMeshEdge(out edgePos);
    
    /// <summary>
    /// 점프를 강제 취소한다. 피격 시 호출.
    /// 현재 수직 속도를 반환하여 넉백에 사용.
    /// </summary>
    public float CancelJump()
    {
        if (!IsJumping) return 0f;
        
        float currentVerticalVelocity = _jumpVelocity.y;
        
        // 점프 상태만 정리 (Agent는 Monster가 관리)
        IsJumping = false;
        _jumpVelocity = Vector3.zero;
        
        Debug.Log($"[JumpController] 점프 취소 - 수직속도: {currentVerticalVelocity:F2}");
        
        return currentVerticalVelocity;
    }
    
    
public void ResetStuckDetection()
    {
        _stuckTimer = 0f;
        _lastPosition = transform.position;
    }
    
    #endregion

    #region 플레이어 지면 추적
    
    private void UpdatePlayerGroundPosition()
    {
        Vector3 playerPos = _playerTransform.position;
        
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
    
    #endregion

    #region 점프 판단 로직
    
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

    private bool TryGetMonsterNavMeshEdge(out Vector3 edgePos)
    {
        edgePos = Vector3.zero;
        
        Vector3 dirToPlayer = _playerTransform.position - transform.position;
        dirToPlayer.y = 0;
        
        if (dirToPlayer.sqrMagnitude < 0.01f) return false;
        dirToPlayer.Normalize();
        
        Vector3 rayEnd = transform.position + dirToPlayer * 50f;
        
        if (NavMesh.Raycast(transform.position, rayEnd, out NavMeshHit hit, NavMesh.AllAreas))
        {
            edgePos = hit.position;
            return true;
        }
        
        return false;
    }

    private bool TryGetDownwardLandingPosition(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        
        if (!TryGetMonsterNavMeshEdge(out Vector3 edgePos)) return false;
        
        Vector3 dirToPlayer = _playerTransform.position - edgePos;
        dirToPlayer.y = 0;
        
        if (dirToPlayer.sqrMagnitude < 0.01f) return false;
        dirToPlayer.Normalize();
        
        Vector3 rayStart = edgePos + dirToPlayer * 1.0f + Vector3.up * 0.5f;
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, MAX_FALL_HEIGHT + 5f, _groundLayer))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                landingPos = navHit.position;
                return true;
            }
            landingPos = hit.point;
            return true;
        }
        
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
            : _playerTransform.position.y;
            
        float heightDiff = targetHeight - transform.position.y;
        float maxJumpHeight = CalculateMaxJumpHeight();

        bool isTargetAbove = heightDiff >= MinHeightDiff;
        bool isTargetBelow = heightDiff <= -MinHeightDiff;
        
        if (isTargetAbove && heightDiff > maxJumpHeight) return false;
        if (isTargetBelow && Mathf.Abs(heightDiff) > MAX_FALL_HEIGHT) return false;
        if (!isTargetAbove && !isTargetBelow) return false;

        float horizontalDist;
        
        if (isTargetAbove)
        {
            if (!TryGetPlatformEdge(out _, out float distToEdge)) return false;
            horizontalDist = distToEdge;
        }
        else
        {
            if (!TryGetDownwardLandingPosition(out Vector3 landingPos)) return false;
            
            float landingHeightDiff = Mathf.Abs(landingPos.y - targetHeight);
            if (landingHeightDiff > 1.5f) return false;
            
            if (!TryGetMonsterNavMeshEdge(out Vector3 monsterEdge)) return false;
            
            Vector3 toLanding = landingPos - monsterEdge;
            toLanding.y = 0;
            horizontalDist = toLanding.magnitude;
        }

        float effectiveMaxDist = isTargetBelow 
            ? CalculateMaxFallDistance(Mathf.Abs(heightDiff)) 
            : CalculateMaxJumpDistance();
            
        if (horizontalDist > effectiveMaxDist) return false;

        Vector3 targetPos = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos 
            : _playerTransform.position;
            
        NavMeshPath path = new NavMeshPath();
        bool hasPath = _agent.CalculatePath(targetPos, path);

        string jumpType = isTargetAbove ? "상승" : "하강";

        if (!hasPath || path.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.Log($"[JumpController] {jumpType} 점프 판단: 경로 없음");
            return true;
        }

        if (path.status == NavMeshPathStatus.PathPartial)
        {
            Debug.Log($"[JumpController] {jumpType} 점프 판단: 경로 불완전");
            return true;
        }

        float directDistance = Vector3.Distance(transform.position, targetPos);
        float pathDistance = CalculatePathDistance(path);

        if (pathDistance > directDistance * PATH_DETOUR_THRESHOLD)
        {
            Debug.Log($"[JumpController] {jumpType} 점프 판단: 우회 경로");
            return true;
        }

        if (_stuckTimer >= StuckThreshold)
        {
            Debug.Log($"[JumpController] {jumpType} 점프 판단: 이동 막힘");
            return true;
        }

        return false;
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

    #region 점프 계산 유틸리티
    
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
        return (JumpForce * JumpForce) / (2f * GRAVITY);
    }

    private float CalculateMaxJumpDistance()
    {
        float airTime = 2f * JumpForce / GRAVITY;
        return airTime * JumpHorizontalSpeed * 0.7f;
    }

    private float CalculateMaxFallDistance(float fallHeight)
    {
        float riseTime = JumpForce / GRAVITY;
        float totalFallHeight = fallHeight + CalculateMaxJumpHeight();
        float fallTime = Mathf.Sqrt(2f * totalFallHeight / GRAVITY);
        float totalAirTime = riseTime + fallTime;
        
        return totalAirTime * JumpHorizontalSpeed * 0.7f;
    }
    
    #endregion

    #region 점프 실행 로직
    
    private void TryStartJump()
    {
        if (!TryFindLandingPosition(out Vector3 landingPos))
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            Debug.Log("[JumpController] 점프 실패: 착지점을 찾지 못함");
            return;
        }

        float heightDiff = landingPos.y - transform.position.y;
        bool isJumpingUp = heightDiff > 0;
        
        if (Mathf.Abs(heightDiff) < MinHeightDiff)
        {
            _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
            return;
        }

        // 상승 점프 시 천장 체크
        if (isJumpingUp)
        {
            float maxJumpHeight = CalculateMaxJumpHeight();
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.up, maxJumpHeight, _groundLayer))
            {
                _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
                Debug.Log("[JumpController] 점프 실패: 위에 천장 있음");
                return;
            }
        }

        _jumpStartPosition = transform.position;
        _jumpTargetPosition = landingPos;
        _jumpTargetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _playerTransform.position.y;
        
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

        _jumpVelocity = horizontalDir * JumpHorizontalSpeed + Vector3.up * JumpForce;
        _jumpCooldownTimer = JUMP_COOLDOWN;

        _agent.isStopped = true;
        _agent.updatePosition = false;

        IsJumping = true;
        OnJumpStarted?.Invoke();

        string jumpType = isJumpingUp ? "상승" : "하강";
        Vector3 toTarget = landingPos - transform.position;
        toTarget.y = 0;
        Debug.Log($"[JumpController] {jumpType} 점프 시작 - 높이:{heightDiff:F2}m, 거리:{toTarget.magnitude:F2}m");
    }

    private bool TryFindLandingPosition(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        float myHeight = transform.position.y;
        
        float targetHeight = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos.y 
            : _playerTransform.position.y;
        float heightDiff = targetHeight - myHeight;
        float maxJumpHeight = CalculateMaxJumpHeight();
        
        bool isJumpingUp = heightDiff > 0;
        float maxJumpDistance = isJumpingUp 
            ? CalculateMaxJumpDistance() 
            : CalculateMaxFallDistance(Mathf.Abs(heightDiff));

        Vector3 targetPos = _lastKnownPlayerGroundPos != Vector3.zero 
            ? _lastKnownPlayerGroundPos 
            : _playerTransform.position;
        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0;
        if (dirToTarget.sqrMagnitude < 0.01f) dirToTarget = transform.forward;
        dirToTarget.Normalize();

        float[] searchHeights = isJumpingUp
            ? new float[] { myHeight + maxJumpHeight, myHeight + maxJumpHeight * 0.8f, 
                           myHeight + maxJumpHeight * 0.6f, targetHeight }
            : new float[] { targetHeight, targetHeight + 0.5f, targetHeight - 0.5f, targetHeight + 1f };

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
                    
                    bool validHeight = isJumpingUp
                        ? (foundHeightDiff >= MinHeightDiff && foundHeightDiff <= maxJumpHeight)
                        : (foundHeightDiff <= -MinHeightDiff && Mathf.Abs(foundHeightDiff) <= MAX_FALL_HEIGHT);
                    
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

        // 폴백: 플레이어 지면 직접 사용
        if (_lastKnownPlayerGroundPos != Vector3.zero)
        {
            if (NavMesh.SamplePosition(_lastKnownPlayerGroundPos, out NavMeshHit groundHit, 3f, NavMesh.AllAreas))
            {
                float foundHeightDiff = groundHit.position.y - myHeight;
                Vector3 toGround = groundHit.position - transform.position;
                toGround.y = 0;
                float horizontalDist = toGround.magnitude;

                bool validHeight = isJumpingUp
                    ? (foundHeightDiff >= MinHeightDiff && foundHeightDiff <= maxJumpHeight)
                    : (foundHeightDiff <= -MinHeightDiff && Mathf.Abs(foundHeightDiff) <= MAX_FALL_HEIGHT);

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

        // 하강 중 착지 감지
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

        // 안전장치: 너무 많이 떨어지면 강제 착지
        if (transform.position.y < _jumpStartPosition.y - MAX_FALL_HEIGHT - 2f)
        {
            Debug.LogWarning("[JumpController] 점프 타임아웃 - 강제 착지");
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
        _agent.SetDestination(_playerTransform.position);

        _stuckTimer = 0f;
        _lastPosition = transform.position;
        IsJumping = false;

        // 착지 높이 검증: 목표보다 너무 높으면 재시도 허용
        float landedHeight = transform.position.y;
        float heightDiffFromTarget = landedHeight - _jumpTargetHeight;
        
        if (heightDiffFromTarget > 1.5f)
        {
            _jumpCooldownTimer = 0f;
            Debug.Log($"[JumpController] 점프 재시도 필요: 착지높이:{landedHeight:F2}m, 목표:{_jumpTargetHeight:F2}m");
        }

        OnJumpCompleted?.Invoke();
        Debug.Log($"[JumpController] 점프 완료 - 높이: {landedHeight:F2}m");
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
        _agent.SetDestination(_playerTransform.position);

        _stuckTimer = 0f;
        _lastPosition = transform.position;
        _jumpCooldownTimer = JUMP_FAIL_COOLDOWN;
        IsJumping = false;

        OnJumpCompleted?.Invoke();
    }
    
    #endregion

    #region Gizmos용 Public 접근자
    
    public float GetMaxJumpHeight() => CalculateMaxJumpHeight();
    public float GetMaxJumpDistance() => CalculateMaxJumpDistance();
    public float GetMaxFallDistance(float fallHeight) => CalculateMaxFallDistance(fallHeight);
    public float MinHeightDiffForJump => MinHeightDiff;
    public Vector3 LastKnownPlayerGroundPos => _lastKnownPlayerGroundPos;
    public Vector3 JumpStartPosition => _jumpStartPosition;
    public Vector3 JumpTargetPosition => _jumpTargetPosition;
    
    public bool TryGetPlatformEdgeForGizmo(out Vector3 edgePos, out float dist) 
        => TryGetPlatformEdge(out edgePos, out dist);
    
    #endregion
}
