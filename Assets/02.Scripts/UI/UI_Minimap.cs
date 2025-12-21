using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵 UI 컨트롤러: RawImage에 RenderTexture 표시 + 확대/축소 버튼
/// </summary>
public class UI_Minimap : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private RawImage _minimapImage;        // RenderTexture 표시용
    [SerializeField] private MinimapCamera _minimapCamera;  // 줌 컨트롤용
    [SerializeField] private Button _zoomInButton;          // + 버튼
    [SerializeField] private Button _zoomOutButton;         // - 버튼
    
    private void Start()
    {
        ValidateReferences();
        SetupButtons();
    }
    
    private void ValidateReferences()
    {
        if (_minimapImage == null)
        {
            Debug.LogError("[UI_Minimap] RawImage 참조가 없습니다!", this);
        }
        
        if (_minimapCamera == null)
        {
            _minimapCamera = FindFirstObjectByType<MinimapCamera>();
            if (_minimapCamera == null)
            {
                Debug.LogWarning("[UI_Minimap] MinimapCamera를 찾을 수 없습니다.", this);
            }
        }
    }
    
    private void SetupButtons()
    {
        // 버튼 이벤트 연결
        if (_zoomInButton != null)
        {
            _zoomInButton.onClick.AddListener(OnZoomInClicked);
        }
        
        if (_zoomOutButton != null)
        {
            _zoomOutButton.onClick.AddListener(OnZoomOutClicked);
        }
    }
    
    private void OnZoomInClicked()
    {
        _minimapCamera?.ZoomIn();
    }
    
    private void OnZoomOutClicked()
    {
        _minimapCamera?.ZoomOut();
    }
    
    private void OnDestroy()
    {
        // 이벤트 정리
        if (_zoomInButton != null)
        {
            _zoomInButton.onClick.RemoveListener(OnZoomInClicked);
        }
        
        if (_zoomOutButton != null)
        {
            _zoomOutButton.onClick.RemoveListener(OnZoomOutClicked);
        }
    }
}
