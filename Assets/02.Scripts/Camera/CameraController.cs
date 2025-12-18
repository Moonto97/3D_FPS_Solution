using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 카메라 위치 제어 및 시점 전환 담당 (DOTween 버전)
/// 단일 책임: 카메라 위치 결정 (회전은 CameraRotate가 담당)
/// 
/// DOTween: 트위닝(Tweening) 라이브러리
/// 트위닝: 시작값에서 끝값으로 부드럽게 변화시키는 애니메이션 기법
/// Ease: 변화 속도 곡선 (빠르게 시작, 천천히 끝 등)
/// </summary>
public class CameraController : MonoBehaviour
{
    #region Serialized Fields (인스펙터에서 설정)
    
    [Header("참조")]
    [SerializeField] private Transform _player;
    
    [Header("1인칭 설정")]
    [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 1.6f, 0f);
    
    [Header("3인칭 설정")]
    [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0f, 2f, -4f);
    [SerializeField] private LayerMask _collisionLayers;  // 충돌 검사 레이어
    [SerializeField] private float _collisionBuffer = 0.3f;  // 벽에서 떨어질 거리
    
    [Header("DOTween 전환 설정")]
    [SerializeField] private float _transitionDuration = 0.5f;  // 전환 시간 (초)
    [SerializeField] private Ease _transitionEase = Ease.OutQuad;  // 전환 이징
    [SerializeField] private EViewMode _startViewMode = EViewMode.FirstPerson;
    
    [Header("추적 설정")]
    [SerializeField] private float _followSpeed = 15f;  // 플레이어 추적 속도
    [SerializeField] private bool _useInstantFollow = false;  // 즉시 추적 여부
    
    #endregion
    
    #region Private Fields
    
    private EViewMode _currentViewMode;
    private Vector3 _activeOffset;      // 현재 적용 중인 오프셋 (DOTween이 조작)
    private bool _isTransitioning;      // 전환 중 여부
    private Tweener _transitionTween;   // 현재 진행 중인 트윈 참조
    
    #endregion
    
    #region Public Properties and Events
    
    /// <summary>
    /// 현재 시점 모드 (읽기 전용)
    /// </summary>
    public EViewMode CurrentViewMode => _currentViewMode;
    
    /// <summary>
    /// 전환 중 여부 (읽기 전용)
    /// </summary>
    public bool IsTransitioning => _isTransitioning;
    
    /// <summary>
    /// 시점 변경 시 발생하는 이벤트
    /// 사용법: cameraController.OnViewModeChanged += HandleViewModeChanged;
    /// </summary>
    public event Action<EViewMode> OnViewModeChanged;
    
    /// <summary>
    /// 시점 전환 완료 시 발생하는 이벤트
    /// </summary>
    public event Action<EViewMode> OnTransitionComplete;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        ValidateReferences();
    }
    
    private void Start()
    {
        InitializeViewMode();
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void LateUpdate()
    {
        UpdateCameraPosition();
    }
    
    private void OnDestroy()
    {
        // DOTween 정리: 오브젝트 파괴 시 진행 중인 트윈 제거
        _transitionTween?.Kill();
    }
    
    #endregion
    
    #region Initialization
    
    private void ValidateReferences()
    {
        if (_player == null)
        {
            Debug.LogError("[CameraController] Player 참조가 없습니다! Inspector에서 설정해주세요.", this);
        }
    }
    
    private void InitializeViewMode()
    {
        _currentViewMode = _startViewMode;
        
        // 시작 시점에 맞는 오프셋으로 즉시 설정 (전환 애니메이션 없이)
        _activeOffset = GetTargetOffset(_currentViewMode);
    }
    
    #endregion
    
    #region Input Handling
    
    private void HandleInput()
    {
        // Playing 상태가 아니면 입력 무시
        if (GameManager.Instance.State != EGameState.Playing) return;
        
        // T키로 시점 전환
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleViewMode();
        }
    }
    
    #endregion
    
    #region View Mode Control
    
    /// <summary>
    /// 시점 모드 토글 (1인칭 ↔ 3인칭)
    /// DOTween으로 부드러운 전환 애니메이션 실행
    /// </summary>
    public void ToggleViewMode()
    {
        // 전환 중이면 무시 (중복 입력 방지)
        if (_isTransitioning) return;
        
        // 새 모드 결정
        EViewMode newMode = (_currentViewMode == EViewMode.FirstPerson) 
            ? EViewMode.ThirdPerson 
            : EViewMode.FirstPerson;
        
        // 전환 실행
        TransitionToMode(newMode);
    }
    
    /// <summary>
    /// 특정 시점 모드로 전환
    /// </summary>
    public void SetViewMode(EViewMode mode)
    {
        if (_currentViewMode == mode || _isTransitioning) return;
        TransitionToMode(mode);
    }
    
    /// <summary>
    /// DOTween을 사용한 시점 전환 실행
    /// </summary>
    private void TransitionToMode(EViewMode newMode)
    {
        _isTransitioning = true;
        _currentViewMode = newMode;
        
        // 이벤트 발생 (전환 시작 알림)
        OnViewModeChanged?.Invoke(_currentViewMode);
        
        // 기존 트윈이 있으면 정지
        _transitionTween?.Kill();
        
        // 목표 오프셋
        Vector3 targetOffset = GetTargetOffset(newMode);
        
        // DOTween으로 오프셋 트위닝
        // DOTween.To: 값을 시작→끝으로 부드럽게 변경
        // getter: 현재값 반환, setter: 새 값 적용, endValue: 목표값, duration: 시간
        _transitionTween = DOTween.To(
            () => _activeOffset,           // getter: 현재 오프셋 반환
            x => _activeOffset = x,        // setter: 새 오프셋 적용
            targetOffset,                  // 목표 오프셋
            _transitionDuration            // 전환 시간
        )
        .SetEase(_transitionEase)          // 이징 적용
        .OnComplete(OnTransitionFinished); // 완료 콜백
        
        Debug.Log($"[CameraController] 시점 전환 시작: {newMode} (Duration: {_transitionDuration}s, Ease: {_transitionEase})");
    }
    
    /// <summary>
    /// 전환 완료 시 호출되는 콜백
    /// </summary>
    private void OnTransitionFinished()
    {
        _isTransitioning = false;
        _transitionTween = null;
        
        // 전환 완료 이벤트 발생
        OnTransitionComplete?.Invoke(_currentViewMode);
        
        Debug.Log($"[CameraController] 시점 전환 완료: {_currentViewMode}");
    }
    
    /// <summary>
    /// 시점 모드에 해당하는 오프셋 반환
    /// </summary>
    private Vector3 GetTargetOffset(EViewMode mode)
    {
        return mode switch
        {
            EViewMode.FirstPerson => _firstPersonOffset,
            EViewMode.ThirdPerson => _thirdPersonOffset,
            _ => _firstPersonOffset
        };
    }
    
    #endregion
    
    #region Camera Position Update
    
    /// <summary>
    /// 카메라 위치 업데이트 (LateUpdate에서 호출)
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (_player == null) return;
        
        // 목표 위치 계산 (현재 오프셋 기반)
        Vector3 targetPosition = CalculateTargetPosition();
        
        // 위치 적용
        if (_useInstantFollow || _isTransitioning)
        {
            // 전환 중이거나 즉시 추적 모드면 바로 적용
            transform.position = targetPosition;
        }
        else
        {
            // 평소에는 부드럽게 추적
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                _followSpeed * Time.deltaTime
            );
        }
    }
    
    /// <summary>
    /// 현재 오프셋 기반 목표 위치 계산
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        // 1인칭에 가까울수록 단순 오프셋, 3인칭에 가까울수록 회전 고려
        float thirdPersonFactor = CalculateThirdPersonFactor();
        
        if (thirdPersonFactor < 0.1f)
        {
            // 거의 1인칭: 단순 오프셋
            return _player.position + _activeOffset;
        }
        else
        {
            // 3인칭 요소 있음: 회전 고려 + 충돌 감지
            return CalculateThirdPersonPosition();
        }
    }
    
    /// <summary>
    /// 현재 오프셋이 3인칭에 얼마나 가까운지 계산 (0~1)
    /// 전환 중 부드러운 행동 전환을 위해 사용
    /// </summary>
    private float CalculateThirdPersonFactor()
    {
        // Z 오프셋으로 판단 (3인칭은 뒤로 빠지므로 음수)
        float firstZ = _firstPersonOffset.z;
        float thirdZ = _thirdPersonOffset.z;
        float currentZ = _activeOffset.z;
        
        if (Mathf.Approximately(firstZ, thirdZ)) return 0f;
        
        return Mathf.InverseLerp(firstZ, thirdZ, currentZ);
    }
    
    /// <summary>
    /// 3인칭 위치 계산 (회전 적용 + 충돌 감지)
    /// </summary>
    private Vector3 CalculateThirdPersonPosition()
    {
        // 피벗 포인트: 플레이어 + 높이 오프셋
        Vector3 pivotPoint = _player.position + Vector3.up * _activeOffset.y;
        
        // Y축(수평) 회전만 추출 - 반동/상하 시선이 카메라 위치에 영향 주지 않도록
        // 이유: 위를 보든 아래를 보든 카메라는 플레이어 뒤에 있어야 함
        Quaternion horizontalRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 rotatedOffset = horizontalRotation * new Vector3(0f, 0f, _activeOffset.z);
        Vector3 desiredPosition = pivotPoint + rotatedOffset;
        
        // 충돌 감지 (3인칭일 때만 의미 있음)
        if (_collisionLayers != 0)
        {
            desiredPosition = ApplyCollisionAvoidance(pivotPoint, desiredPosition);
        }
        
        return desiredPosition;
    }
    
    /// <summary>
    /// 벽 충돌 회피 처리
    /// </summary>
    private Vector3 ApplyCollisionAvoidance(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - pivot;
        float distance = direction.magnitude;
        
        // Physics.Raycast: 광선을 쏘아 충돌 검사
        if (Physics.Raycast(pivot, direction.normalized, out RaycastHit hit, distance, _collisionLayers))
        {
            // 충돌 시: 충돌 지점에서 버퍼만큼 앞으로
            return hit.point - direction.normalized * _collisionBuffer;
        }
        
        return desiredPosition;
    }
    
    #endregion
    
    #region Editor Helpers
    
    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;
        
        // 1인칭 위치 (녹색)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_player.position + _firstPersonOffset, 0.1f);
        Gizmos.DrawLine(_player.position, _player.position + _firstPersonOffset);
        
        // 3인칭 위치 (파란색)
        Gizmos.color = Color.blue;
        Vector3 tpPivot = _player.position + Vector3.up * _thirdPersonOffset.y;
        Vector3 tpOffset = transform.rotation * new Vector3(0f, 0f, _thirdPersonOffset.z);
        Gizmos.DrawWireSphere(tpPivot + tpOffset, 0.15f);
        Gizmos.DrawLine(tpPivot, tpPivot + tpOffset);
        
        // 현재 활성 오프셋 (노란색) - Play 모드에서만
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 currentPos = CalculateTargetPosition();
            Gizmos.DrawWireSphere(currentPos, 0.12f);
        }
    }
    
    #endregion
}
