using TMPro;
using UnityEngine;

/// <summary>
/// 클릭 횟수 UI 표시
/// 역할: ClickManager의 OnDataChanged 이벤트를 구독해서 클릭 횟수 표시
/// </summary>
public class UI_Click : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _leftClickCountTextUI;
    [SerializeField] private TextMeshProUGUI _rightClickCountTextUI;

    private void Start()
    {
        ValidateReferences();
        SubscribeToEvents();
        
        // 초기 상태 표시
        Refresh();
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void ValidateReferences()
    {
        if (_leftClickCountTextUI == null)
        {
            Debug.LogError("[UI_Click] Left Click Text 참조가 없습니다!", this);
        }
        
        if (_rightClickCountTextUI == null)
        {
            Debug.LogError("[UI_Click] Right Click Text 참조가 없습니다!", this);
        }
        
        if (ClickManager.Instance == null)
        {
            Debug.LogError("[UI_Click] ClickManager.Instance가 없습니다!", this);
        }
    }
    
    private void SubscribeToEvents()
    {
        if (ClickManager.Instance == null) return;
        
        ClickManager.Instance.OnDataChanged += Refresh;
    }
    
    private void UnsubscribeFromEvents()
    {
        if (ClickManager.Instance == null) return;
        
        ClickManager.Instance.OnDataChanged -= Refresh;
    }
    
    /// <summary>
    /// 클릭 횟수 UI 갱신 (이벤트 콜백)
    /// </summary>
    private void Refresh()
    {
        if (ClickManager.Instance == null) return;
        
        if (_leftClickCountTextUI != null)
        {
            _leftClickCountTextUI.text = $"왼쪽 클릭 : {ClickManager.Instance.LeftClickCount}번";
        }
        
        if (_rightClickCountTextUI != null)
        {
            _rightClickCountTextUI.text = $"오른쪽 클릭 : {ClickManager.Instance.RightClickCount}번";
        }
    }
}
