using UnityEngine;
// 인터페이스 : 클래스 간의 약속
public interface IDamageable
{
    // IDamageable 약속을 지켜야 하는 클래스는 무조건 아래 메서드를 구현해야 한다.
    public bool TryTakeDamage(Damage damage);
}
