using UnityEngine;
using TMPro;

/// <summary>
/// 발사 모드 UI 표시
/// 역할: PlayerGunFire의 OnFireModeChanged 이벤트를 구독해서 현재 발사 모드 표시
/// B키로 모드 변경 시 UI가 자동 업데이트됨
/// </summary>
public class UI_FireModeDisplay : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("발사 모드를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI _fireModeText;

    [Header("Player Reference")]
    [Tooltip("null이면 씬에서 자동 탐색")]
    [SerializeField] private PlayerGunFire _playerGunFire;

    [Header("Display Settings")]
    [Tooltip("Auto 모드 색상")]
    [SerializeField] private Color _autoColor = Color.green;
    [Tooltip("Single 모드 색상")]
    [SerializeField] private Color _singleColor = Color.yellow;
    [Tooltip("Burst 모드 색상")]
    [SerializeField] private Color _burstColor = Color.cyan;

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
        if (_fireModeText == null)
        {
            _fireModeText = GetComponentInChildren<TextMeshProUGUI>();
        }

        // PlayerGunFire 없으면 씬에서 탐색
        if (_playerGunFire == null)
        {
            _playerGunFire = FindFirstObjectByType<PlayerGunFire>();
        }

        // 필수 참조 검증
        if (_fireModeText == null)
        {
            Debug.LogError("[UI_FireModeDisplay] TextMeshProUGUI not found!");
        }

        if (_playerGunFire == null)
        {
            Debug.LogError("[UI_FireModeDisplay] PlayerGunFire not found!");
        }
    }

    /// <summary>
    /// 이벤트 구독 (시작 시)
    /// </summary>
    private void SubscribeToEvents()
    {
        if (_playerGunFire == null) return;

        // OnFireModeChanged 이벤트 구독
        _playerGunFire.OnFireModeChanged += UpdateFireModeDisplay;

        // 초기 상태 표시
        UpdateFireModeDisplay(_playerGunFire.CurrentFireMode);
    }

    /// <summary>
    /// 이벤트 구독 해제 (파괴 시)
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_playerGunFire == null) return;

        _playerGunFire.OnFireModeChanged -= UpdateFireModeDisplay;
    }

    /// <summary>
    /// 발사 모드 UI 업데이트 (이벤트 콜백)
    /// </summary>
    /// <param name="fireMode">현재 발사 모드</param>
    private void UpdateFireModeDisplay(EFireMode fireMode)
    {
        if (_fireModeText == null) return;

        // 발사 모드에 따른 텍스트와 색상 설정
        switch (fireMode)
        {
            case EFireMode.Auto:
                _fireModeText.text = "AUTO";
                _fireModeText.color = _autoColor;
                break;
            case EFireMode.Single:
                _fireModeText.text = "SINGLE";
                _fireModeText.color = _singleColor;
                break;
            case EFireMode.Burst:
                _fireModeText.text = "BURST";
                _fireModeText.color = _burstColor;
                break;
        }
    }
}
