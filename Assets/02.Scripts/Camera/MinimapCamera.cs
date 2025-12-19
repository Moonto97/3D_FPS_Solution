using UnityEngine;

/// <summary>
/// 미니맵 카메라: 플레이어 위에서 아래를 내려다보며 따라다님
/// Orthographic 카메라 + RenderTexture 출력
/// </summary>
public class MinimapCamera : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform _target;
    
    [Header("카메라 설정")]
    [SerializeField] private float _height = 50f;           // 플레이어 위 높이
    [SerializeField] private float _defaultZoom = 30f;      // 기본 Orthographic Size
    [SerializeField] private float _minZoom = 10f;          // 최대 확대 (숫자 작을수록 확대)
    [SerializeField] private float _maxZoom = 100f;         // 최대 축소
    [SerializeField] private float _zoomStep = 5f;          // 버튼 클릭당 줌 변화량
    
    private Camera _camera;
    
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        ValidateSetup();
    }
    
    private void Start()
    {
        // 초기 줌 레벨 설정
        if (_camera != null)
        {
            _camera.orthographicSize = _defaultZoom;
        }
    }
    
    private void ValidateSetup()
    {
        if (_camera == null)
        {
            Debug.LogError("[MinimapCamera] Camera 컴포넌트가 없습니다!", this);
            return;
        }
        
        // Orthographic 모드 확인
        if (!_camera.orthographic)
        {
            Debug.LogWarning("[MinimapCamera] Orthographic 모드로 전환합니다.", this);
            _camera.orthographic = true;
        }
        
        if (_target == null)
        {
            // Player 태그로 자동 검색 시도
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
            }
            else
            {
                Debug.LogWarning("[MinimapCamera] Target이 설정되지 않았습니다. Inspector에서 설정해주세요.", this);
            }
        }
    }
    
    /// <summary>
    /// LateUpdate: 플레이어 이동 후 카메라 위치 갱신
    /// </summary>
    private void LateUpdate()
    {
        if (_target == null) return;
        
        // 플레이어 XZ 위치 추적, Y는 고정 높이
        Vector3 targetPosition = _target.position;
        transform.position = new Vector3(targetPosition.x, targetPosition.y + _height, targetPosition.z);
        
        // 아래를 내려다보는 각도 (X축 90도)
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
    
    #region 줌 컨트롤 (UI 버튼에서 호출)
    
    /// <summary>
    /// 확대 (+버튼): Orthographic Size 감소
    /// </summary>
    public void ZoomIn()
    {
        if (_camera == null) return;
        
        _camera.orthographicSize = Mathf.Max(_minZoom, _camera.orthographicSize - _zoomStep);
    }
    
    /// <summary>
    /// 축소 (-버튼): Orthographic Size 증가
    /// </summary>
    public void ZoomOut()
    {
        if (_camera == null) return;
        
        _camera.orthographicSize = Mathf.Min(_maxZoom, _camera.orthographicSize + _zoomStep);
    }
    
    /// <summary>
    /// 현재 줌 레벨 (0~1, 0=최대확대, 1=최대축소)
    /// </summary>
    public float ZoomLevel => (_camera.orthographicSize - _minZoom) / (_maxZoom - _minZoom);
    
    #endregion
}
