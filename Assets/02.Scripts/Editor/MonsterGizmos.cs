using UnityEngine;
using UnityEditor;

/// <summary>
/// Monster/MonsterJumpController의 디버그 시각화.
/// Editor 전용 - 빌드에 포함되지 않음.
/// [DrawGizmo] attribute로 선택 시 자동 표시.
/// </summary>
public static class MonsterGizmos
{
    /// <summary>
    /// Monster 선택 시 Gizmo 표시.
    /// JumpController를 가져와서 점프 관련 Gizmo 표시.
    /// </summary>
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    private static void DrawMonsterGizmos(Monster monster, GizmoType gizmoType)
    {
        if (monster == null) return;
        
        MonsterJumpController jumpController = monster.JumpController;
        
        // ========== 순찰 범위 표시 (연두색) ==========
        MonsterStats stats = monster.GetComponent<MonsterStats>();
        if (stats != null && monster.DefaultPosition != Vector3.zero)
        {
            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);  // 반투명 연두색
            DrawWireCircle(monster.DefaultPosition, stats.PatrolRadius.Value, 32);
            
            // 기본 위치 표시
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(monster.DefaultPosition, Vector3.one * 0.5f);
            
            // 현재 순찰 목표 표시 (Patrol 상태일 때만)
            if (monster.State == EMonsterState.Patrol && monster.PatrolTarget != Vector3.zero)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f);  // 청록색
                Gizmos.DrawSphere(monster.PatrolTarget, 0.4f);
                Gizmos.DrawLine(monster.transform.position, monster.PatrolTarget);
            }
        }
        
        // 현재 목적지 표시 (흰색)
        if (monster.CurrentDestination != Vector3.zero)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(monster.CurrentDestination, 0.3f);
            Gizmos.DrawLine(monster.transform.position, monster.CurrentDestination);
        }
        
        // JumpController가 있으면 점프 Gizmo 표시
        if (jumpController != null)
        {
            DrawJumpGizmos(jumpController);
        }
    }

    /// <summary>
    /// 점프 관련 Gizmo 표시 (Monster/JumpController 공용)
    /// </summary>
    private static void DrawJumpGizmos(MonsterJumpController jumpController)
    {
        // 초기화 전이면 Gizmo 표시 안 함 (에디터에서 플레이 전 상태)
        if (jumpController == null || !jumpController.IsInitialized) return;
        
        Vector3 playerGroundPos = jumpController.LastKnownPlayerGroundPos;
        
        float maxJumpHeight = jumpController.GetMaxJumpHeight();
        float maxJumpDistance = jumpController.GetMaxJumpDistance();
        float minHeightDiff = jumpController.MinHeightDiffForJump;
        
        Vector3 monsterPos = jumpController.transform.position;
        
        // ========== 최대 점프 높이 (파란색 박스) ==========
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            monsterPos + Vector3.up * (maxJumpHeight / 2f), 
            new Vector3(1f, maxJumpHeight, 1f)
        );

        // ========== 점프 가능 거리 (노란색 원) ==========
        Gizmos.color = Color.yellow;
        DrawWireCircle(monsterPos, maxJumpDistance, 32);

        // ========== 몬스터 NavMesh 끝단 (청록색) ==========
        if (jumpController.TryGetNavMeshEdge(out Vector3 monsterEdge))
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(monsterEdge, 0.5f);
            Gizmos.DrawLine(monsterPos + Vector3.up * 0.2f, monsterEdge + Vector3.up * 0.2f);
        }

        // ========== 플레이어 지면 관련 시각화 ==========
        if (playerGroundPos != Vector3.zero)
        {
            float heightDiff = playerGroundPos.y - monsterPos.y;
            bool isTargetBelow = heightDiff <= -minHeightDiff;
            
            // 플레이어 지면 (마젠타)
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(playerGroundPos, 0.5f);
            
            if (isTargetBelow)
            {
                // ========== 하강 점프 시각화 ==========
                DrawDownwardJumpGizmos(jumpController, monsterEdge, playerGroundPos, heightDiff);
            }
            else if (heightDiff >= minHeightDiff)
            {
                // ========== 상승 점프 시각화 ==========
                DrawUpwardJumpGizmos(jumpController, playerGroundPos, maxJumpDistance);
            }
        }

        // ========== 점프 중 시각화 ==========
        if (jumpController.IsJumping)
        {
            DrawActiveJumpGizmos(jumpController);
        }
    }

    /// <summary>
    /// 하강 점프 시각화: 몬스터 끝단 → 플레이어 지면
    /// </summary>
    private static void DrawDownwardJumpGizmos(
        MonsterJumpController jumpController, 
        Vector3 monsterEdge, 
        Vector3 playerGroundPos,
        float heightDiff)
    {
        if (monsterEdge == Vector3.zero) return;
        
        Vector3 toPlayerGround = playerGroundPos - monsterEdge;
        toPlayerGround.y = 0;
        float horizontalDist = toPlayerGround.magnitude;
        float maxFallDist = jumpController.GetMaxFallDistance(Mathf.Abs(heightDiff));
        
        bool canJump = horizontalDist <= maxFallDist;
        Gizmos.color = canJump ? Color.green : Color.red;
        
        // 몬스터 끝단 → 플레이어 지면 높이 → 플레이어 지면
        Vector3 edgeAtPlayerHeight = new Vector3(monsterEdge.x, playerGroundPos.y, monsterEdge.z);
        Gizmos.DrawLine(monsterEdge + Vector3.up * 0.3f, edgeAtPlayerHeight + Vector3.up * 0.3f);
        Gizmos.DrawLine(edgeAtPlayerHeight + Vector3.up * 0.3f, playerGroundPos + Vector3.up * 0.3f);
    }

    /// <summary>
    /// 상승 점프 시각화: 몬스터 → 플랫폼 끝단
    /// </summary>
    private static void DrawUpwardJumpGizmos(
        MonsterJumpController jumpController,
        Vector3 playerGroundPos,
        float maxJumpDistance)
    {
        if (jumpController.TryGetPlatformEdgeForGizmo(out Vector3 platformEdge, out float distToEdge))
        {
            // 플랫폼 끝단 (주황색)
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawSphere(platformEdge, 0.4f);
            
            // 점프 가능 여부
            bool canJump = distToEdge <= maxJumpDistance;
            Gizmos.color = canJump ? Color.green : Color.red;
            Gizmos.DrawLine(
                jumpController.transform.position + Vector3.up * 0.5f, 
                platformEdge + Vector3.up * 0.5f
            );
        }
    }

    /// <summary>
    /// 점프 진행 중 시각화
    /// </summary>
    private static void DrawActiveJumpGizmos(MonsterJumpController jumpController)
    {
        Vector3 startPos = jumpController.JumpStartPosition;
        Vector3 targetPos = jumpController.JumpTargetPosition;
        
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(startPos, 0.3f);
        Gizmos.DrawSphere(targetPos, 0.3f);
        Gizmos.DrawLine(startPos, targetPos);
    }

    /// <summary>
    /// 와이어 원 그리기 유틸리티
    /// </summary>
    private static void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius, 
                0, 
                Mathf.Sin(angle) * radius
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
