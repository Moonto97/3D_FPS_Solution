using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 몬스터 체력바 UI 컨트롤러
/// - 현재 체력(초록)과 딜레이 체력(빨강)을 표시
/// - 피격 시: 초록 즉시 감소, 빨강은 딜레이 후 따라감
/// - 회복 시: 둘 다 즉시 증가
/// </summary>
public class MonsterHealthBar : MonoBehaviour
{
    [Header("필수 참조")]
    private Monster _monster;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private Transform _healthBarTransform;
    [SerializeField] private Image _guageImage;              // 현재 체력 바 (초록)

    [Header("딜레이 체력바")]
    [SerializeField] private Image _delayGaugeImage;         // 딜레이 바 (빨강) - Inspector 연결
    [SerializeField] private float _delayWaitTime = 0.5f;    // 딜레이 시작 전 대기 시간
    [SerializeField] private float _delayDuration = 0.8f;    // 따라오는 애니메이션 시간

    private Transform _cameraTransform;
    private Tween _delayTween;  // DOTween 참조 (취소/재사용용)

    private void Awake()
    {
        // 필수 참조 null 체크
        if (_monsterStats == null)
        {
            Debug.LogError("MonsterHealthBar: _monsterStats가 할당되지 않았습니다!");
            return;
        }
        if (_guageImage == null)
        {
            Debug.LogError("MonsterHealthBar: _guageImage가 할당되지 않았습니다!");
            return;
        }
        if (_healthBarTransform == null)
        {
            Debug.LogError("MonsterHealthBar: _healthBarTransform이 할당되지 않았습니다!");
            return;
        }

        // 딜레이 바 null 체크 (선택적 - 없어도 기존 기능 동작)
        if (_delayGaugeImage == null)
        {
            Debug.LogWarning("MonsterHealthBar: _delayGaugeImage가 없습니다. 딜레이 효과 비활성화.");
        }

        // 카메라 캐싱
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        // 이벤트 구독
        _monsterStats.Health.OnValueChanged += OnHealthChanged;

        // 초기 fillAmount 설정
        float initialFill = _monsterStats.Health.Value / _monsterStats.Health.MaxValue;
        _guageImage.fillAmount = initialFill;
        
        // 딜레이 바도 동일하게 초기화
        if (_delayGaugeImage != null)
        {
            _delayGaugeImage.fillAmount = initialFill;
        }

        Debug.Log($"MonsterHealthBar 초기화 완료 - Health: {_monsterStats.Health.Value}/{_monsterStats.Health.MaxValue}");
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (_monsterStats != null)
        {
            _monsterStats.Health.OnValueChanged -= OnHealthChanged;
        }

        // DOTween 정리 (메모리 누수 방지)
        _delayTween?.Kill();
    }

    /// <summary>
    /// 체력 변경 시 호출되는 콜백
    /// - 현재 바: 즉시 반영
    /// - 딜레이 바: 피해 시 딜레이 후 따라감, 회복 시 즉시 반영
    /// </summary>
    private void OnHealthChanged(float currentValue, float maxValue)
    {
        float targetFill = currentValue / maxValue;

        // 현재 체력 바는 즉시 감소/증가
        _guageImage.fillAmount = targetFill;

        // 딜레이 바 처리 (없으면 스킵)
        if (_delayGaugeImage == null) return;

        // 피해 시: 딜레이 바가 현재 바보다 큼 → 천천히 따라감
        if (_delayGaugeImage.fillAmount > targetFill)
        {
            // 기존 Tween 취소 후 새로 시작 (연속 피격 대응)
            _delayTween?.Kill();
            _delayTween = _delayGaugeImage
                .DOFillAmount(targetFill, _delayDuration)
                .SetDelay(_delayWaitTime)
                .SetEase(Ease.OutQuad);
        }
        else
        {
            // 회복 시: 딜레이 바도 즉시 따라감
            _delayTween?.Kill();
            _delayGaugeImage.fillAmount = targetFill;
        }
    }

    /// <summary>
    /// 빌보드: 체력바가 항상 카메라를 향하도록 회전
    /// </summary>
    private void LateUpdate()
    {
        if (_cameraTransform != null && _healthBarTransform != null)
        {
            _healthBarTransform.forward = _cameraTransform.forward;
        }
    }
}
