using UnityEngine;
using UnityEngine.UI;

public class MonsterHealthBar : MonoBehaviour
{
    private Monster _monster;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private Transform _healthBarTransform;
    [SerializeField] private Image _guageImage;

    private float _lastHealth = -1;
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
        
        // 초기 fillAmount 설정
        _guageImage.fillAmount = _monsterStats.Health.Value / _monsterStats.Health.MaxValue;
        _lastHealth = _monsterStats.Health.Value;
        
        Debug.Log($"MonsterHealthBar 초기화 완료 - Health: {_monsterStats.Health.Value}/{_monsterStats.Health.MaxValue}");
    }

private void LateUpdate()
    {
        if (_monsterStats == null || _guageImage == null || _healthBarTransform == null)
            return;
            
        // UI가 알고있는 몬스터 체력값과 다를 경우에만 fillAmount를 수정한다.
        if (_lastHealth != _monsterStats.Health.Value)
        {
            _lastHealth = _monsterStats.Health.Value;
            float fillValue = _monsterStats.Health.Value / _monsterStats.Health.MaxValue;
            _guageImage.fillAmount = fillValue;
        }
        
        // 빌보드 기법: 카메라의 위치와 회전에 상관없이 항상 정면을 바라보게하는 기법
        if (Camera.main != null)
        {
            _healthBarTransform.forward = Camera.main.transform.forward;
        }
    }
}