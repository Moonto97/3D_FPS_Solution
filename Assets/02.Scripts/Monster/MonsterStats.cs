using System;
using UnityEngine;

[Serializable]
public class MonsterStats : MonoBehaviour
{
    public ConsumableStat Health;
    public ValueStat MoveSpeed;
    public ValueStat AttackSpeed;
    public ValueStat DetectDistance;
    public ValueStat AttackDistance;
    public ValueStat Damage;
    public ValueStat KnockbackDecay;
    public ValueStat KnockbackForce;
}
