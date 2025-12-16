using UnityEngine;
using TMPro;

/// <summary>
/// 탄약 UI 표시
/// 역할: PlayerGunFire의 OnAmmoChanged 이벤트를 구독해서 "30/120" 형식으로 표시
/// 이벤트: 옵저버 패턴. 발행자(PlayerGunFire)가 상태 변경 시 구독자(UI)에게 알림
/// </summary>
public class UI_AmmoDisplay : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("탄약 수를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI _ammoText;

    [Header("Player Reference")]
    [Tooltip("null이면 씬에서 자동 탐색")]
    [SerializeField] private PlayerGunFire _playerGunFire;

    [Header("Display Format")]
    [Tooltip("탄창이 비었을 때 색상")]
    [SerializeField] private Color _emptyColor = Color.red;
    [Tooltip("정상 상태 색상")]
    [SerializeField] private Color _normalColor = Color.white;
    [Tooltip("적은 탄약(임계값 이하) 색상")]
    [SerializeField] private Color _lowAmmoColor = Color.yellow;
    [Tooltip("적은 탄약 임계값")]
    [SerializeField] private int _lowAmmoThreshold = 10;

    private void Start()
    {
        InitializeReferences();
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// 참조 초기화 (자동 탐색)
    /// </summary>
    private void InitializeReferences()
    {
        // TextMeshPro 없으면 자식에서 탐색
        if (_ammoText == null)
        {
            _ammoText = GetComponentInChildren<TextMeshProUGUI>();
        }

        // PlayerGunFire 없으면 씬에서 탐색
        if (_playerGunFire == null)
        {
            _playerGunFire = FindFirstObjectByType<PlayerGunFire>();
        }

        // 필수 참조 검증
        if (_ammoText == null)
        {
            Debug.LogError("[UI_AmmoDisplay] TextMeshProUGUI not found!");
        }

        if (_playerGunFire == null)
        {
            Debug.LogError("[UI_AmmoDisplay] PlayerGunFire not found!");
        }
    }

    /// <summary>
    /// 이벤트 구독 (시작 시)
    /// </summary>
    private void SubscribeToEvents()
    {
        if (_playerGunFire == null) return;

        // OnAmmoChanged 이벤트 구독: 탄약 변경 시 UpdateAmmoDisplay 호출
        _playerGunFire.OnAmmoChanged += UpdateAmmoDisplay;

        // 초기 상태 표시
        UpdateAmmoDisplay(_playerGunFire.CurrentAmmo, _playerGunFire.ReserveAmmo);
    }

    /// <summary>
    /// 이벤트 구독 해제 (파괴 시)
    /// 메모리 누수 방지: 구독한 이벤트는 반드시 해제해야 함
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_playerGunFire == null) return;

        _playerGunFire.OnAmmoChanged -= UpdateAmmoDisplay;
    }

    /// <summary>
    /// 탄약 UI 업데이트 (이벤트 콜백)
    /// </summary>
    /// <param name="currentAmmo">현재 탄창의 탄약</param>
    /// <param name="reserveAmmo">예비 탄약</param>
    private void UpdateAmmoDisplay(int currentAmmo, int reserveAmmo)
    {
        if (_ammoText == null) return;

        // "30 / 120" 형식으로 표시
        _ammoText.text = $"{currentAmmo} / {reserveAmmo}";

        // 탄약 상태에 따른 색상 변경
        _ammoText.color = GetAmmoColor(currentAmmo);
    }

    /// <summary>
    /// 탄약 상태에 따른 색상 결정
    /// </summary>
    private Color GetAmmoColor(int currentAmmo)
    {
        if (currentAmmo <= 0)
        {
            return _emptyColor;  // 빨강: 탄창 비었음
        }
        
        if (currentAmmo <= _lowAmmoThreshold)
        {
            return _lowAmmoColor;  // 노랑: 탄약 부족
        }
        
        return _normalColor;  // 흰색: 정상
    }
}
