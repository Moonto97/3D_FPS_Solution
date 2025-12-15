using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Monster))]
public class MonsterHealthBar : MonoBehaviour
{
    private Monster _monster;
    [SerializeField] private MonsterStats _monsterStats;
    [SerializeField] private Transform _healthBarTransform;
    [SerializeField] private Image _guageImage;

    private float _lastHealth = -1;
    private void Awake()
    {
        _monster = gameObject.GetComponent<Monster>();
        float mx = _monsterStats.Health.MaxValue;
        float _mshealth = _monsterStats.Health.Value;
    }

    private void LateUpdate()
    {
        // 0 ~ 1
        // UI가 알고있는 몬스터 체력값과 다를 경우에만 fillAmount를 수정한다.
        if (_lastHealth != _monsterStats.Health.Value)
        {
            _lastHealth = _monsterStats.Health.Value;
            _guageImage.fillAmount = _monsterStats.Health.Value / _monsterStats.Health.MaxValue;
        }
        
        // 빌보드 기법: 카메라의 위치와 회전에 상관없이 항상 정면을 바라보게하는 기법
        _healthBarTransform.forward = Camera.main.transform.forward;
    }
}