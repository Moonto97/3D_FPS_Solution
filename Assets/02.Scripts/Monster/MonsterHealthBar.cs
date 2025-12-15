using UnityEngine;
using UnityEngine.UI;

public class MonsterHealthBar : MonoBehaviour
{
    private Monster _monster;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private Transform _healthBarTransform;
    [SerializeField] private Image _guageImage;

    private Transform _cameraTransform;

    private void Awake()
    {
        // null 체크
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

        // 카메라 캐싱
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        // 이벤트 구독
        _monsterStats.Health.OnValueChanged += OnHealthChanged;

        // 초기 fillAmount 설정
        _guageImage.fillAmount = _monsterStats.Health.Value / _monsterStats.Health.MaxValue;

        Debug.Log($"MonsterHealthBar 초기화 완료 - Health: {_monsterStats.Health.Value}/{_monsterStats.Health.MaxValue}");
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (_monsterStats != null)
        {
            _monsterStats.Health.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float currentValue, float maxValue)
    {
        if (_guageImage != null)
        {
            _guageImage.fillAmount = currentValue / maxValue;
        }
    }

    private void LateUpdate()
    {
        // 빌보드 기법: 카메라의 위치와 회전에 상관없이 항상 정면을 바라보게하는 기법
        if (_cameraTransform != null && _healthBarTransform != null)
        {
            _healthBarTransform.forward = _cameraTransform.forward;
        }
    }
}