using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 넉백 시스템. 지상/공중 넉백 물리와 NavMesh 복귀를 담당.
/// Monster.cs에서 분리되어 단일 책임 원칙(SRP)을 따름.
/// 
/// [설계]
/// - 넉백 중: Agent 비활성화, CharacterController로 직접 이동
/// - 완료 시: NavMesh 위치 검증 후 Agent 복원, 이벤트로 Monster에 알림
/// </summary>
public class MonsterKnockback : MonoBehaviour
{
    #region 이벤트 (Monster.cs와 통신용)
    
    /// <summary>넉백 시작 시 발생. Monster가 AI 로직을 일시정지해야 함.</summary>
    public event Action OnKnockbackStarted;
    
    /// <summary>넉백 완료 시 발생. validPosition = NavMesh 복귀 위치.</summary>
    public event Action<Vector3> OnKnockbackCompleted;
    
    #endregion

    #region 외부 참조 (Monster.cs에서 주입)
    
    private CharacterController _controller;
    private NavMeshAgent _agent;
    private Animator _animator;
    private MonsterStats _stats;
    private MonsterJumpController _jumpController;
    private Vector3 _defaultPosition;
    private LayerMask _groundLayer;
    
    #endregion

    #region 상수
    
    private const float KNOCKBACK_GRAVITY = 20f;
    private const float DEFAULT_KNOCKBACK_DURATION = 0.3f;
    private const string ANIM_PARAM_HIT_SPEED = "HitSpeed";
    
    #endregion

    #region 내부 상태
    
    private Vector3 _knockbackVelocity;
    private float _knockbackTimer;
    private float _hitAnimationLength = 1f;
    
    // 공중 넉백 (점프 중 피격)
    private bool _isAirborneKnockback;
    private float _verticalKnockbackVelocity;
    
    #endregion

    #region Properties
    
    /// <summary>넉백 진행 중 여부. Monster.Update에서 AI 스킵 조건으로 사용.</summary>
    public bool IsActive { get; private set; }
    
    /// <summary>초기화 완료 여부.</summary>
    public bool IsInitialized => _controller != null;
    
    #endregion

    #region 초기화
    
    /// <summary>
    /// Monster.cs에서 호출하여 필요한 참조를 주입한다.
    /// </summary>
    public void Initialize(
        CharacterController controller,
        NavMeshAgent agent,
        Animator animator,
        MonsterStats stats,
        MonsterJumpController jumpController,
        Vector3 defaultPosition,
        LayerMask groundLayer)
    {
        _controller = controller;
        _agent = agent;
        _animator = animator;
        _stats = stats;
        _jumpController = jumpController;
        _defaultPosition = defaultPosition;
        _groundLayer = groundLayer;
        
        CacheHitAnimationLength();
    }
    
    /// <summary>
    /// Hit 애니메이션 클립 길이 캐싱. 넉백 시간에 맞춰 재생 속도 조절에 사용.
    /// </summary>
    private void CacheHitAnimationLength()
    {
        const string HIT_CLIP_NAME = "Zombie Reaction Hit";
        
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[Knockback] Animator가 없어 Hit 애니메이션 길이를 가져올 수 없습니다.", this);
            return;
        }
        
        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == HIT_CLIP_NAME)
            {
                _hitAnimationLength = clip.length;
                return;
            }
        }
        
        Debug.LogWarning($"[Knockback] '{HIT_CLIP_NAME}' 클립을 찾을 수 없습니다.", this);
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// 넉백 시작. 기존 넉백 중이면 리셋하고 새로 시작.
    /// </summary>
    /// <param name="attackerPosition">공격자 위치. 반대 방향으로 밀려남.</param>
    /// <param name="wasJumping">점프 중 피격 여부.</param>
    /// <param name="jumpVerticalVelocity">점프 취소 시 수직 속도 (공중 넉백용).</param>
    public void StartKnockback(Vector3 attackerPosition, bool wasJumping = false, float jumpVerticalVelocity = 0f)
    {
        // 공중 넉백 처리 (점프 중 피격)
        if (wasJumping)
        {
            _verticalKnockbackVelocity = jumpVerticalVelocity;
            _isAirborneKnockback = true;
            _agent.updatePosition = true;
        }

        // 넉백 활성화
        IsActive = true;
        _knockbackTimer = 0f;
        _agent.enabled = false;  // Agent 비활성화 (간섭 차단)

        // 넉백 방향: 공격자 반대 방향
        float knockbackForce = _stats?.KnockbackForce?.Value ?? 5f;
        _knockbackVelocity = (transform.position - attackerPosition).normalized * knockbackForce;
        
        // Hit 애니메이션 속도 조절: KnockbackDuration에 맞춤
        AdjustHitAnimationSpeed();
        
        OnKnockbackStarted?.Invoke();
    }
    
    /// <summary>
    /// 넉백 물리 적용. Monster.Update에서 매 프레임 호출.
    /// </summary>
    public void UpdateKnockback()
    {
        if (!IsActive) return;
        
        // 공중 넉백 vs 지상 넉백 분기
        if (_isAirborneKnockback)
        {
            ApplyAirborneKnockback();
        }
        else
        {
            ApplyGroundKnockback();
        }
    }
    
    #endregion

    #region 지상 넉백
    
    /// <summary>
    /// 지상 넉백 물리. 수평 이동 + 감쇠.
    /// </summary>
    private void ApplyGroundKnockback()
    {
        _knockbackTimer += Time.deltaTime;
        
        float knockbackDuration = _stats?.KnockbackDuration?.Value ?? DEFAULT_KNOCKBACK_DURATION;
        bool isFinished = _knockbackVelocity.sqrMagnitude < 0.01f 
                         || _knockbackTimer >= knockbackDuration;
        
        if (isFinished)
        {
            CompleteKnockback(transform.position);
            return;
        }

        // 넉백 이동 적용
        _controller.Move(_knockbackVelocity * Time.deltaTime);
        
        // 속도 감쇠
        float decay = _stats?.KnockbackDecay?.Value ?? 5f;
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, decay * Time.deltaTime);
    }
    
    #endregion

    #region 공중 넉백
    
    /// <summary>
    /// 공중 넉백: 중력 적용 + 포물선 낙하 + 착지 감지.
    /// </summary>
    private void ApplyAirborneKnockback()
    {
        _knockbackTimer += Time.deltaTime;
        
        // 타임아웃 체크
        float knockbackDuration = _stats?.KnockbackDuration?.Value ?? DEFAULT_KNOCKBACK_DURATION;
        if (_knockbackTimer >= knockbackDuration)
        {
            CompleteKnockback(transform.position);
            return;
        }
        
        // 중력 적용
        _verticalKnockbackVelocity -= KNOCKBACK_GRAVITY * Time.deltaTime;
        
        // 이동: 수평 넉백 + 수직 낙하
        Vector3 movement = _knockbackVelocity + Vector3.up * _verticalKnockbackVelocity;
        _controller.Move(movement * Time.deltaTime);
        
        // 수평 넉백 감쇠
        float decay = _stats?.KnockbackDecay?.Value ?? 5f;
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, decay * Time.deltaTime);
        
        // 착지 감지: 하강 중에만
        if (_verticalKnockbackVelocity < 0)
        {
            if (TryDetectLanding(out Vector3 landingPoint))
            {
                CompleteKnockback(landingPoint);
            }
        }
    }
    
    /// <summary>
    /// 착지 감지. Raycast + CharacterController.isGrounded 병용.
    /// </summary>
    private bool TryDetectLanding(out Vector3 landingPoint)
    {
        landingPoint = transform.position;
        
        // Raycast 착지 감지
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 
                           out RaycastHit hit, 0.5f, _groundLayer))
        {
            landingPoint = hit.point;
            return true;
        }
        
        // CharacterController 착지 감지
        if (_controller.isGrounded)
        {
            return true;
        }
        
        return false;
    }
    
    #endregion

    #region 넉백 완료
    
    /// <summary>
    /// 넉백 완료 처리. NavMesh 복구 + 이벤트 발생.
    /// </summary>
    private void CompleteKnockback(Vector3 landingPoint)
    {
        // 상태 리셋
        _knockbackVelocity = Vector3.zero;
        _verticalKnockbackVelocity = 0f;
        _isAirborneKnockback = false;
        IsActive = false;
        
        // NavMesh 위치 검증 후 Agent 복원
        Vector3 validPos = FindValidNavMeshPosition(landingPoint);
        
        transform.position = validPos;
        _agent.enabled = true;
        _agent.Warp(validPos);
        
        // 점프 컨트롤러 리셋 (막힘 감지 초기화)
        _jumpController?.ResetStuckDetection();
        
        OnKnockbackCompleted?.Invoke(validPos);
    }
    
    /// <summary>
    /// 유효한 NavMesh 위치 찾기. 못 찾으면 기본 위치 반환.
    /// </summary>
    private Vector3 FindValidNavMeshPosition(Vector3 targetPosition)
    {
        // 1차: 가까운 거리에서 탐색
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // 2차: 더 넓은 범위에서 탐색
        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Knockback] NavMesh 복귀 - 확장 탐색: {hit.position}");
            return hit.position;
        }
        
        // 3차: 폴백 - 기본 위치
        Debug.LogWarning($"[Knockback] NavMesh 못 찾음 - 기본 위치로 복귀");
        return _defaultPosition;
    }
    
    #endregion

    #region 헬퍼
    
    /// <summary>
    /// Hit 애니메이션 속도 조절. 넉백 시간에 맞춰 동기화.
    /// </summary>
    private void AdjustHitAnimationSpeed()
    {
        if (_animator == null) return;
        
        float knockbackDuration = _stats?.KnockbackDuration?.Value ?? DEFAULT_KNOCKBACK_DURATION;
        float hitSpeed = (knockbackDuration > 0f) 
            ? _hitAnimationLength / knockbackDuration 
            : 1f;
        _animator.SetFloat(ANIM_PARAM_HIT_SPEED, hitSpeed);
    }
    
    #endregion
}
