using UnityEngine;

/// <summary>
/// 플레이어 이동 관련 설정 데이터
/// ScriptableObject: 에셋으로 저장, 런타임 중 수정 가능
/// </summary>
[CreateAssetMenu(fileName = "MoveConfig", menuName = "Player/Move Config")]
public class MoveConfig : ScriptableObject
{
    [Header("중력")]
    [Tooltip("중력 가속도 (음수 값)")]
    [SerializeField] private float _gravity = -20f;
    
    [Header("스태미나 소모량")]
    [Tooltip("달리기 시 초당 스태미나 소모량")]
    [SerializeField] private float _runStamina = 10f;
    
    [Tooltip("점프 시 스태미나 소모량")]
    [SerializeField] private float _jumpStamina = 15f;
    
    // 읽기 전용 프로퍼티
    public float Gravity => _gravity;
    public float RunStamina => _runStamina;
    public float JumpStamina => _jumpStamina;
}
