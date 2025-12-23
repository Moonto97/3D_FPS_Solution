using UnityEngine;

/// <summary>
/// 크로스헤어 동적 애니메이션 컨트롤러
/// 책임: 발사 시 크로스헤어 확대 → 자연 축소 애니메이션
/// 연동: PlayerGunFire.OnFired 이벤트 구독
/// </summary>
public class CrosshairController : MonoBehaviour
{
    #region ========== Inspector Fields ==========

    [Header("=== References ===")]
    [Tooltip("PlayerGunFire 컴포넌트 (null이면 Player 태그에서 자동 탐색)")]
    [SerializeField] private PlayerGunFire _gunFire;
    
    [Tooltip("크로스헤어 RectTransform (null이면 자기 자신)")]
    [SerializeField] private RectTransform _crosshairRect;

    [Header("=== Animation Settings ===")]
    [Tooltip("기본 스케일")]
    [SerializeField] private float _baseScale = 1f;
    
    [Tooltip("발사 시 확대될 최대 스케일")]
    [SerializeField] private float _maxScale = 1.5f;
    
    [Tooltip("발사당 스케일 증가량")]
    [SerializeField] private float _expandAmount = 0.15f;
    
    [Tooltip("초당 스케일 축소 속도")]
    [SerializeField] private float _shrinkSpeed = 5f;

    #endregion

    #region ========== Runtime State ==========

    private float _currentScale;  // 현재 스케일 값

    #endregion

    #region ========== Initialization ==========

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnEnable()
    {
        // 이벤트 구독: PlayerGunFire가 발사할 때 OnGunFired 호출
        if (_gunFire != null)
        {
            _gunFire.OnFired += OnGunFired;
        }
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_gunFire != null)
        {
            _gunFire.OnFired -= OnGunFired;
        }
    }

    /// <summary>
    /// 필수 참조 초기화 + 검증
    /// </summary>
    private void InitializeReferences()
    {
        // RectTransform 자동 할당
        if (_crosshairRect == null)
        {
            _crosshairRect = GetComponent<RectTransform>();
        }

        if (_crosshairRect == null)
        {
            Debug.LogError("[CrosshairController] RectTransform not found!");
            enabled = false;
            return;
        }

        // PlayerGunFire 자동 탐색
        if (_gunFire == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _gunFire = player.GetComponent<PlayerGunFire>();
            }
        }

        if (_gunFire == null)
        {
            Debug.LogWarning("[CrosshairController] PlayerGunFire not found. Crosshair animation disabled.");
        }

        // 초기 스케일 설정
        _currentScale = _baseScale;
        ApplyScale();
    }

    #endregion

    #region ========== Update ==========

    private void Update()
    {
        ShrinkCrosshair();
    }

    /// <summary>
    /// 매 프레임 크로스헤어 축소 (기본 스케일로 복귀)
    /// Lerp 대신 선형 감소로 예측 가능한 동작
    /// </summary>
    /// <summary>
    /// 매 프레임 크로스헤어 축소 (기본 스케일로 복귀)
    /// Lerp: 처음 빠르게 → 끝에서 부드럽게 축소
    /// </summary>
    private void ShrinkCrosshair()
    {
        if (_currentScale <= _baseScale) return;

        // Lerp 보간: 현재값에서 목표값으로 부드럽게 이동
        // 처음엔 빠르게, 목표에 가까워질수록 느려짐
        _currentScale = Mathf.Lerp(_currentScale, _baseScale, _shrinkSpeed * Time.deltaTime);
        
        // 미세한 차이는 강제 정리 (불필요한 연산 방지)
        if (Mathf.Abs(_currentScale - _baseScale) < 0.001f)
        {
            _currentScale = _baseScale;
        }
        
        ApplyScale();
    }

    #endregion

    #region ========== Event Handlers ==========

    /// <summary>
    /// PlayerGunFire.OnFired 이벤트 핸들러
    /// 발사 시 크로스헤어 즉시 확대
    /// </summary>
    /// <param name="spread">현재 탄퍼짐 값 (크로스헤어 크기에 반영 가능)</param>
    private void OnGunFired(float spread)
    {
        ExpandCrosshair();
    }

    #endregion

    #region ========== Crosshair Animation ==========

    /// <summary>
    /// 크로스헤어 확대 (발사 시 호출)
    /// </summary>
    private void ExpandCrosshair()
    {
        // 확대량 누적 (최대값 제한)
        _currentScale = Mathf.Min(_currentScale + _expandAmount, _maxScale);
        ApplyScale();
    }

    /// <summary>
    /// 현재 스케일 값을 RectTransform에 적용
    /// </summary>
    private void ApplyScale()
    {
        _crosshairRect.localScale = new Vector3(_currentScale, _currentScale, 1f);
    }

    #endregion
}
