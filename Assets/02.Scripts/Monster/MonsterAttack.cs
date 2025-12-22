using System;
using UnityEngine;

public class MonsterAttack : MonoBehaviour
{
    private Monster _monster;
    
    [SerializeField] MonsterStats _monsterStats;
    [SerializeField] PlayerStats _playerStats;

    private void Awake()
    {
        if (_monster == null)
        {
            _monster = GetComponent<Monster>();
        }
    }

    public void AttackPlayer()
    {
        // Damage 구조체로 공격 정보 전달
        Damage damage = new Damage
        {
            Value = _monsterStats.Damage.Value,
            HitPoint = transform.position,  // 몬스터 위치 (피격 방향 계산용)
            Who = gameObject                // 공격한 몬스터
        };
        _playerStats.TryTakeDamage(damage);
    }
}
