using UnityEngine;
using TMPro;

/// <summary>
/// 폭탄 보유량을 "3/5" 형식으로 표시하는 UI 컴포넌트.
/// PlayerBombFire의 OnBombCountChanged 이벤트를 구독하여 값 변경 시에만 갱신.
/// </summary>
public class UI_BombDisplay : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // Inspector 설정
    // ─────────────────────────────────────────────────────────
    [Header("참조")]
    [SerializeField] private PlayerBombFire _playerBombFire;
    [SerializeField] private TextMeshProUGUI _bombCountText;
    
    [Header("표시 형식")]
    [Tooltip("폭탄 개수 표시 형식. {0}=현재, {1}=최대")]
    [SerializeField] private string _displayFormat = "💣 {0}/{1}";
    
    private void Awake()
    {
        // 필수 참조 검증
        if (_playerBombFire == null)
        {
            Debug.LogError($"[UI_BombDisplay] PlayerBombFire 참조가 없습니다! {gameObject.name}");
            return;
        }
        
        if (_bombCountText == null)
        {
            Debug.LogError($"[UI_BombDisplay] TextMeshProUGUI 참조가 없습니다! {gameObject.name}");
            return;
        }
    }
    
    private void OnEnable()
    {
        // 이벤트 구독: 폭탄 개수가 변경될 때 UI 갱신
        if (_playerBombFire != null)
        {
            _playerBombFire.OnBombCountChanged += UpdateDisplay;
        }
    }
    
    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_playerBombFire != null)
        {
            _playerBombFire.OnBombCountChanged -= UpdateDisplay;
        }
    }
    
    /// <summary>
    /// 폭탄 개수 표시 갱신. 이벤트 발생 시에만 호출됨.
    /// </summary>
    /// <param name="current">현재 폭탄 개수</param>
    /// <param name="max">최대 폭탄 개수</param>
    private void UpdateDisplay(int current, int max)
    {
        if (_bombCountText == null) return;
        
        _bombCountText.text = string.Format(_displayFormat, current, max);
    }
}
