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
    
    [Header("점프 설정")]
    public ValueStat MaxJumpHeight;      // 점프로 도달 가능한 최대 높이
    public ValueStat JumpDuration;       // 점프 소요 시간
    public ValueStat StuckThreshold;     // 막힘 판정 시간(초)
}
