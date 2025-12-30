using System;
using UnityEngine;

/// <summary>
/// 몬스터 스탯 데이터. Inspector에서 튜닝.
/// 모든 수치 데이터를 중앙 관리하여 밸런싱 용이.
/// </summary>
[Serializable]
public class MonsterStats : MonoBehaviour
{
    [Header("기본 스탯")]
    public ConsumableStat Health;
    public ValueStat MoveSpeed;
    public ValueStat DetectDistance;
    
    [Header("전투 스탯")]
    public ValueStat AttackSpeed;
    public ValueStat AttackDistance;
    public ValueStat Damage;
    
    [Header("피격 설정")]
    [Tooltip("피격 후 무적 시간 (초). 연타 방지용. 0이면 무적 없음.")]
    public ValueStat InvincibilityDuration;
    
    [Header("넉백 설정")]
    public ValueStat KnockbackForce;
    public ValueStat KnockbackDecay;
    
    [Tooltip("넉백 최대 지속 시간 (초)")]
    public ValueStat KnockbackDuration;
    
    [Header("순찰 설정")]
    [Tooltip("순찰 이동 속도 (MoveSpeed보다 느림)")]
    public ValueStat PatrolSpeed;
    
    [Tooltip("기본 위치 기준 순찰 범위 반경")]
    public ValueStat PatrolRadius;
    
    [Tooltip("순찰 포인트 도착 후 대기 시간 (초)")]
    public ValueStat PatrolWaitTime;
    
    [Header("점프 설정")]
    [Tooltip("점프력 (수직 초기 속도, m/s)")]
    public ValueStat JumpForce;
    
    [Tooltip("점프 중 수평 이동 속도 (m/s)")]
    public ValueStat JumpHorizontalSpeed;
    
    [Tooltip("점프 판단 최소 고저차 (m)")]
    public ValueStat MinHeightDiffForJump;
    
    [Tooltip("이동 막힘 판정 시간 (초)")]
    public ValueStat StuckThreshold;
    
    [Header("골드 드롭")]
    [Tooltip("골드 코인 프리팹")]
    public GameObject GoldCoinPrefab;
    
    [Range(0f, 1f), Tooltip("골드 드롭 확률 (0~1)")]
    public float GoldDropChance = 0.5f;
    
    [Tooltip("드롭 시 스폰되는 높이")]
    public float GoldDropHeight = 1.5f;
    
    [Tooltip("드롭할 코인 최소 개수")]
    public int GoldDropCountMin = 3;
    
    [Tooltip("드롭할 코인 최대 개수")]
    public int GoldDropCountMax = 7;
}
