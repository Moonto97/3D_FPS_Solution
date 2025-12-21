using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 시 화면에 피 효과(Blood Overlay) 표시
/// 역할: PlayerStats.OnDamaged 이벤트를 구독하여 피격 시 Image를 보여주고 서서히 페이드아웃
/// 코루틴: 프레임 단위 애니메이션에 적합 (async는 I/O용)
/// </summary>
public class UI_DamageEffect : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("피 효과를 표시할 Image (Canvas에 전체 화면으로 배치)")]
    [SerializeField] private Image _damageOverlay;
    
    [Header("Player Reference")]
    [Tooltip("null이면 씬에서 자동 탐색")]
    [SerializeField] private PlayerStats _playerStats;
    
    [Header("Effect Settings")]
    [Tooltip("피 효과가 사라지는 데 걸리는 시간(초)")]
    [SerializeField] private float _flashDuration = 0.5f;
    
    [Tooltip("피 효과의 최대 투명도 (0~1)")]
    [SerializeField] private float _maxAlpha = 0.6f;
    
    [Header("Random Sprites (선택)")]
    [Tooltip("여러 피 스프라이트를 등록하면 피격마다 랜덤 선택")]
    [SerializeField] private Sprite[] _bloodSprites;
    
    // 현재 실행 중인 페이드아웃 코루틴 (중복 방지용)
    private Coroutine _fadeCoroutine;

    private void Start()
    {
        InitializeReferences();
        SubscribeToEvents();
        
        // 초기 상태: 완전 투명
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지: 이벤트 구독 해제
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// 참조 초기화 (자동 탐색)
    /// </summary>
    private void InitializeReferences()
    {
        // Image 없으면 자기 자신에서 가져오기
        if (_damageOverlay == null)
        {
            _damageOverlay = GetComponent<Image>();
        }
        
        // PlayerStats 없으면 씬에서 탐색
        if (_playerStats == null)
        {
            _playerStats = FindFirstObjectByType<PlayerStats>();
        }
        
        // 필수 참조 검증
        if (_damageOverlay == null)
        {
            Debug.LogError("[UI_DamageEffect] Image component not found!");
        }
        
        if (_playerStats == null)
        {
            Debug.LogError("[UI_DamageEffect] PlayerStats not found!");
        }
    }

    /// <summary>
    /// 이벤트 구독 (시작 시)
    /// </summary>
    private void SubscribeToEvents()
    {
        if (_playerStats == null) return;
        
        _playerStats.OnDamaged += ShowDamageEffect;
    }

    /// <summary>
    /// 이벤트 구독 해제 (파괴 시)
    /// 메모리 누수 방지: 구독한 이벤트는 반드시 해제해야 함
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (_playerStats == null) return;
        
        _playerStats.OnDamaged -= ShowDamageEffect;
    }

    /// <summary>
    /// 피격 효과 표시 (이벤트 콜백)
    /// 랜덤 스프라이트 선택 후 페이드아웃 시작
    /// </summary>
    private void ShowDamageEffect()
    {
        if (_damageOverlay == null) return;
        
        // 랜덤 스프라이트 선택 (배열이 있을 경우)
        ApplyRandomSprite();
        
        // 기존 코루틴 중지 (연속 피격 시 즉시 갱신)
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        
        // 최대 알파로 시작 → 페이드아웃
        SetOverlayAlpha(_maxAlpha);
        _fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }

    /// <summary>
    /// 랜덤 스프라이트 적용 (배열이 있을 경우)
    /// </summary>
    private void ApplyRandomSprite()
    {
        if (_bloodSprites == null || _bloodSprites.Length == 0) return;
        
        int randomIndex = Random.Range(0, _bloodSprites.Length);
        _damageOverlay.sprite = _bloodSprites[randomIndex];
    }

    /// <summary>
    /// 페이드아웃 코루틴
    /// Lerp로 alpha를 _maxAlpha에서 0까지 서서히 감소
    /// </summary>
    private IEnumerator FadeOutCoroutine()
    {
        float elapsedTime = 0f;
        float startAlpha = _maxAlpha;
        
        while (elapsedTime < _flashDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // 0~1 비율 계산
            float t = elapsedTime / _flashDuration;
            
            // Lerp로 부드럽게 감소
            float currentAlpha = Mathf.Lerp(startAlpha, 0f, t);
            SetOverlayAlpha(currentAlpha);
            
            yield return null;
        }
        
        // 완전히 투명하게 마무리
        SetOverlayAlpha(0f);
        _fadeCoroutine = null;
    }

    /// <summary>
    /// 오버레이 알파값 설정 (헬퍼)
    /// </summary>
    private void SetOverlayAlpha(float alpha)
    {
        if (_damageOverlay == null) return;
        
        Color color = _damageOverlay.color;
        color.a = alpha;
        _damageOverlay.color = color;
    }
}
