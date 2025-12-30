using UnityEngine;

public class LayerCullDistance : MonoBehaviour
{
    [System.Serializable]
    public class LayerCullSetting
    {
        public string layerName;
        public float cullDistance;
    }

    [Header("레이어별 컬링 거리")]
    public LayerCullSetting[] cullSettings = new LayerCullSetting[]
    {
        new LayerCullSetting { layerName = "SmallProps", cullDistance = 50f },
        new LayerCullSetting { layerName = "Buildings", cullDistance = 200f },
        new LayerCullSetting { layerName = "Lights", cullDistance = 80f },
    };

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        ApplyCullDistances();
    }

    void ApplyCullDistances()
    {
        if (cam == null) return;

        float[] distances = new float[32];

        foreach (var setting in cullSettings)
        {
            int layer = LayerMask.NameToLayer(setting.layerName);
            if (layer >= 0 && layer < 32)
            {
                distances[layer] = setting.cullDistance;
                Debug.Log($"[LayerCull] {setting.layerName} (Layer {layer}): {setting.cullDistance}m");
            }
            else
            {
                Debug.LogWarning($"[LayerCull] Layer '{setting.layerName}' not found!");
            }
        }

        cam.layerCullDistances = distances;
        cam.layerCullSpherical = true;
    }

    // Inspector에서 값 변경 시 실시간 적용
    void OnValidate()
    {
        if (Application.isPlaying && cam != null)
        {
            ApplyCullDistances();
        }
    }
}
