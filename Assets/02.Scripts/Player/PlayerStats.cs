using System;
using UnityEngine;

// 플레이어의 '스탯'을 관리하는 컴포넌트
public class PlayerStats : MonoBehaviour, IDamageable
{
    // 고민해볼 거리
    // 1. 옵저버 패턴은 어떻게 해야지?
    // 2. ConsumableStat의 Regenerate는 PlayerStats에서만 호출 가능하게 하고 싶다. 다른 속성/기능은 다른 클래스에서 사용할 수 있다.
    
    // 이벤트: 피격 시 UI(피 화면 효과 등)에 알림
    public event Action OnDamaged;
    
    public ConsumableStat Health;
    public ConsumableStat Stamina;
    public ValueStat Damage;
    public ValueStat MoveSpeed;
    public ValueStat RunSpeed;
    public ValueStat JumpPower;

    [Header("재화")]
    [SerializeField] private int _gold = 0;
    
    /// <summary>
    /// 현재 보유 골드량 (읽기 전용)
    /// </summary>
    public int Gold => _gold;



    private void Start()
    {
        // ConsumableStats 초기화
        Health.Initialize();
        Stamina.Initialize();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        
        Health.Regenerate(deltaTime);
        Stamina.Regenerate(deltaTime);

        if (Health.Value <= 0)
        {
            Death();
        }
    } 
    /// <summary>
    /// IDamageable 구현. 데미지 적용 + UI 이벤트 발행.
    /// </summary>
    /// <param name="damage">데미지 정보 (값, 피격위치, 공격자)</param>
    /// <returns>항상 true (플레이어는 무적 시스템 없음)</returns>
    public bool TryTakeDamage(Damage damage)
    {
        Health.Decrease(damage.Value);
        Debug.Log($"플레이어 피격! 공격자: {damage.Who?.name ?? "Unknown"}, 남은 체력: {Health.Value}");
        
        // 피격 이벤트 발행 → UI_DamageEffect 등이 구독
        OnDamaged?.Invoke();
        
        return true;
    }

    
    /// <summary>
    /// 골드 추가. 획득 시스템에서 호출.
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        
        _gold += amount;
        // TODO: UI 업데이트 이벤트 발행 가능
    }
private void Death()
    {
        GameManager.Instance.GameOver();
    }
    
}