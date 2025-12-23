using UnityEngine;

/// <summary>
/// Animator가 있는 자식 오브젝트(Zombie1)에 부착.
/// Animation Event를 받아 부모의 Monster 컴포넌트로 전달.
/// 
/// [Animation Event 개념]
/// - 애니메이션 클립의 특정 프레임에서 메서드를 호출하는 Unity 기능
/// - 이벤트 함수는 Animator가 붙은 오브젝트에 있어야 호출됨
/// - 이 스크립트는 자식(Animator) → 부모(Monster) 브릿지 역할
/// </summary>
[RequireComponent(typeof(Animator))]
public class MonsterAnimationEventReceiver : MonoBehaviour
{
    private Monster _monster;
    
    private void Awake()
    {
        // 부모 계층에서 Monster 컴포넌트 찾기
        _monster = GetComponentInParent<Monster>();
        
        if (_monster == null)
        {
            Debug.LogError($"[AnimEventReceiver] {gameObject.name}: 부모에 Monster가 없습니다!", this);
        }
    }
    
    /// <summary>
    /// Animation Event: 점프 클립에서 발이 땅을 떠나는 순간 호출.
    /// X_Bot_Jump.fbx의 약 9프레임(0.3초) 지점에 설정.
    /// </summary>
    public void OnJumpTakeoff()
    {
        if (_monster == null) return;
        
        _monster.ExecutePhysicalJump();
    }
}
