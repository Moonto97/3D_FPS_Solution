using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

/// <summary>
/// NavMesh 베이크를 위한 에디터 유틸리티.
/// 메뉴: Tools > NavMesh > Bake All Surfaces
/// </summary>
public static class NavMeshBaker
{
    [MenuItem("Tools/NavMesh/Bake All Surfaces")]
    public static void BakeAllNavMeshSurfaces()
    {
        // 씬의 모든 NavMeshSurface 찾기
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        
        if (surfaces.Length == 0)
        {
            Debug.LogWarning("[NavMeshBaker] 씬에 NavMeshSurface가 없습니다!");
            return;
        }
        
        foreach (NavMeshSurface surface in surfaces)
        {
            surface.BuildNavMesh();
            Debug.Log($"[NavMeshBaker] '{surface.gameObject.name}'의 NavMesh 베이크 완료!");
        }
        
        Debug.Log($"[NavMeshBaker] 총 {surfaces.Length}개의 NavMesh Surface 베이크 완료!");
    }
    
    [MenuItem("Tools/NavMesh/Clear All Surfaces")]
    public static void ClearAllNavMeshSurfaces()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        
        foreach (NavMeshSurface surface in surfaces)
        {
            surface.RemoveData();
        }
        
        Debug.Log($"[NavMeshBaker] 총 {surfaces.Length}개의 NavMesh 데이터 삭제 완료!");
    }
}
