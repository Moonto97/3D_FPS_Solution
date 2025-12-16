using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재장전 진행바 UI
/// 역할: PlayerGunFire의 재장전 이벤트를 구독해서 CrossHair 아래에 진행바 표시
/// Slider: Unity UI의 진행바 컴포넌트. value(0~1)로 진행률 표시
/// </summary>
public class UI_ReloadProgress : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("재장전 진행바 (Slider 컴포넌트)")]
    [SerializeField] private Slider _progressSlider;
    
    [Tooltip("진행바를 담고 있는 오브젝트 (표시/숨김용)")]
    [SerializeField] private GameObject _progressContainer;

    [Header("Player Reference")]
    [Tooltip("null이면 씬에서 자동 탐색")]
    [SerializeField] private PlayerGunFire _playerGunFire;

    [Header("Visual Settings")]
    [Tooltip("진행바 채움 색상")]
    [SerializeField] private Color _fillColor = new Color(0.2f, 0.8f, 0.2f);  // 밝은 초록

    private Image _fillImage;

    private void Start()
    {
        InitializeReferences();
        InitializeVisuals();
        SubscribeToEvents();
        
        // 시작 시 숨김
        SetProgressVisible(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// 참조 초기화
    /// </summary>
    private void InitializeReferences()
    {
        // Slider 없으면 자식에서 탐색
        if (_progressSlider == null)
        {
            _progressSlider = GetComponentInChildren<Slider>();
        }

        // Container 없으면 자신으로 설정
        if (_progressContainer == null && _progressSlider != null)
        {
            _progressContainer = _progressSlider.gameObject;
        }

        // PlayerGunFire 없으면 씬에서 탐색
        if (_playerGunFire == null)
        {
            _playerGunFire = FindFirstObjectByType<PlayerGunFire>();
        }

        // 필수 참조 검증
        if (_progressSlider == null)
        {
            Debug.LogError("[UI_ReloadProgress] Slider not found!");
        }

        if (_playerGunFire == null)
        {
            Debug.LogError("[UI_ReloadProgress] PlayerGunFire not found!");
        }
    }

    /// <summary>
    /// 비주얼 초기화
    /// </summary>
    private void InitializeVisuals()
    {
        if (_progressSlider == null) return;

        // Slider 설정: 인터랙션 비활성화 (표시 전용)
        _progressSlider.interactable = false;
        _progressSlider.minValue = 0f;
        _progressSlider.maxValue = 1f;

        // Fill 이미지 색상 설정
        _fillImage = _progressSlider.fillRect?.GetComponent<Image>();
        if (_fillImage != null)
        {
            _fillImage.color = _fillColor;
        }
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeToEvents()
    {
        if (_playerGunFire == null) return;

        // 재장전 상태 변경 시 표시/숨김
        _playerGunFire.OnReloadStateChanged += OnReloadStateChanged;
        
        // 재장전 진행률 업데이트
        _playerGunFire.OnReloadProgress += OnReloadProgress;
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_playerGunFire == null) return;

        _playerGunFire.OnReloadStateChanged -= OnReloadStateChanged;
        _playerGunFire.OnReloadProgress -= OnReloadProgress;
    }

    /// <summary>
    /// 재장전 상태 변경 콜백
    /// </summary>
    /// <param name="isReloading">재장전 시작(true) / 종료(false)</param>
    private void OnReloadStateChanged(bool isReloading)
    {
        SetProgressVisible(isReloading);

        // 재장전 시작 시 진행바 초기화
        if (isReloading && _progressSlider != null)
        {
            _progressSlider.value = 0f;
        }
    }

    /// <summary>
    /// 재장전 진행률 업데이트 콜백
    /// </summary>
    /// <param name="progress">진행률 (0~1)</param>
    private void OnReloadProgress(float progress)
    {
        if (_progressSlider == null) return;

        _progressSlider.value = progress;
    }

    /// <summary>
    /// 진행바 표시/숨김
    /// </summary>
    private void SetProgressVisible(bool visible)
    {
        if (_progressContainer != null)
        {
            _progressContainer.SetActive(visible);
        }
    }
}
