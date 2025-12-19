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
        _playerStats.TakeDamage(_monsterStats.Damage.Value);
    }
}
