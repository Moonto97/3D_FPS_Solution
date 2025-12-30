using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 체력/스태미나 UI 표시
/// 역할: PlayerStats의 ConsumableStat.OnValueChanged 이벤트를 구독해서 슬라이더 갱신
/// </summary>
public class UI_PlayerStats : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStats _stats;
    
    [Header("UI References")]
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Slider _staminaSlider;

    private void Start()
    {
        ValidateReferences();
        SubscribeToEvents();
        
        // 초기 상태 표시
        UpdateHealthSlider(_stats.Health.Value, _stats.Health.MaxValue);
        UpdateStaminaSlider(_stats.Stamina.Value, _stats.Stamina.MaxValue);
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void ValidateReferences()
    {
        if (_stats == null)
        {
            _stats = FindFirstObjectByType<PlayerStats>();
        }
        
        if (_stats == null)
        {
            Debug.LogError("[UI_PlayerStats] PlayerStats 참조가 없습니다!", this);
        }
        
        if (_healthSlider == null)
        {
            Debug.LogError("[UI_PlayerStats] Health Slider 참조가 없습니다!", this);
        }
        
        if (_staminaSlider == null)
        {
            Debug.LogError("[UI_PlayerStats] Stamina Slider 참조가 없습니다!", this);
        }
    }
    
    private void SubscribeToEvents()
    {
        if (_stats == null) return;
        
        _stats.Health.OnValueChanged += UpdateHealthSlider;
        _stats.Stamina.OnValueChanged += UpdateStaminaSlider;
    }
    
    private void UnsubscribeFromEvents()
    {
        if (_stats == null) return;
        
        _stats.Health.OnValueChanged -= UpdateHealthSlider;
        _stats.Stamina.OnValueChanged -= UpdateStaminaSlider;
    }
    
    /// <summary>
    /// 체력 슬라이더 갱신 (이벤트 콜백)
    /// </summary>
    private void UpdateHealthSlider(float current, float max)
    {
        if (_healthSlider == null) return;
        
        _healthSlider.value = (max > 0f) ? current / max : 0f;
    }
    
    /// <summary>
    /// 스태미나 슬라이더 갱신 (이벤트 콜백)
    /// </summary>
    private void UpdateStaminaSlider(float current, float max)
    {
        if (_staminaSlider == null) return;
        
        _staminaSlider.value = (max > 0f) ? current / max : 0f;
    }
}
