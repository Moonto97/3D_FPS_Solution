using UnityEngine;
using UnityEditor;

public class AutoLODGroupSetup : EditorWindow
{
    private float cullPercent = 0.05f;  // 5%에서 컬링

    [MenuItem("Tools/Auto LOD Group Setup")]
    static void ShowWindow()
    {
        GetWindow<AutoLODGroupSetup>("Auto LOD Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("LOD Group 자동 설정", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        cullPercent = EditorGUILayout.Slider("Cull Percent", cullPercent, 0.01f, 0.2f);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("선택한 오브젝트에 LOD Group 적용"))
        {
            ApplyLODGroup();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("선택한 오브젝트에서 LOD Group 제거"))
        {
            RemoveLODGroup();
        }
    }

    void ApplyLODGroup()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            // 기존 LODGroup 제거
            LODGroup existing = obj.GetComponent<LODGroup>();
            if (existing != null)
                DestroyImmediate(existing);

            // 새 LODGroup 추가
            LODGroup lodGroup = obj.AddComponent<LODGroup>();

            // 모든 자식 Renderer 찾기
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

            // LOD 설정 (LOD0 + Culled)
            LOD[] lods = new LOD[2];
            lods[0] = new LOD(cullPercent, renderers);  // 지정 퍼센트에서 컬링
            lods[1] = new LOD(0, new Renderer[0]);      // Culled

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            // 프리팹이면 저장
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            }

            Debug.Log($"✅ {obj.name}: LODGroup 적용 완료 (Renderer {renderers.Length}개)");
        }
    }

    void RemoveLODGroup()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            LODGroup lodGroup = obj.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                DestroyImmediate(lodGroup);
                Debug.Log($"❌ {obj.name}: LODGroup 제거됨");
            }
        }
    }
}
