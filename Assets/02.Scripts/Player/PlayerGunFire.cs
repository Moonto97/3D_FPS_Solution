using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 총 발사 처리
/// 책임: 총 발사 + 탄약 관리 + 재장전 + 발사 모드 전환
/// 풀링은 ObjectPoolManager에, 데이터는 GunData에 위임
/// </summary>
public class PlayerGunFire : MonoBehaviour
{
    #region ========== Inspector Fields ==========

    [Header("=== Data ===")]
    [Tooltip("총기 데이터. 없으면 기본값 사용")]
    [SerializeField] private GunData _gunData;

    [Header("=== Fire ===")]
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private Animator _animator;

    [Header("=== Recoil ===")]
    [Tooltip("null이면 Main Camera에서 자동 탐색")]
    [SerializeField] private CameraRecoil _cameraRecoil;

    [Header("=== Muzzle Flash ===")]
    [Tooltip("총구 화염 프리팹 (Easy FPS MuzzelFlash 폴더에서 선택)")]
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [Tooltip("총구 화염 생성 위치 (총구 끝)")]
    [SerializeField] private Transform _muzzleFlashSpawnPoint;

    [Header("=== Hit Effect (Pool) ===")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private string _hitEffectPoolTag = "HitEffect";
    [SerializeField] private int _poolInitialSize = 10;
    [SerializeField] private float _effectDuration = 2f;

    #endregion

    #region ========== Constants ==========

    // GunData 없을 때 사용하는 폴백 값
    private const int DEFAULT_MAGAZINE_SIZE = 30;
    private const int DEFAULT_RESERVE_AMMO = 120;
    private const float DEFAULT_RELOAD_TIME = 1.6f;
    private const float DEFAULT_FIRE_RATE = 0.1f;
    private const int BURST_COUNT = 3;  // 3점사

    #endregion

    #region ========== Runtime State ==========

    // 탄약 상태
    private int _currentAmmo;       // 현재 탄창의 탄약
    private int _reserveAmmo;       // 예비 탄약
    private bool _isReloading;      // 재장전 중 여부
    private float _nextFireTime;    // 다음 발사 가능 시간

    // 발사 모드 상태
    private EFireMode _currentFireMode = EFireMode.Auto;
    private int _burstShotsRemaining;   // 점사 모드에서 남은 발사 수
    private bool _isBurstFiring;        // 점사 중 여부

    // 풀 상태
    private bool _isPoolInitialized;

    #endregion

    #region ========== Events (UI 연동용) ==========

    /// <summary>현재탄약, 예비탄약이 변경될 때</summary>
    public event Action<int, int> OnAmmoChanged;

    /// <summary>재장전 진행률(0~1) 업데이트</summary>
    public event Action<float> OnReloadProgress;

    /// <summary>재장전 시작(true)/종료(false)</summary>
    public event Action<bool> OnReloadStateChanged;

    /// <summary>발사 모드 변경 시</summary>
    public event Action<EFireMode> OnFireModeChanged;

    #endregion

    #region ========== Properties (외부 읽기용) ==========

    public int CurrentAmmo => _currentAmmo;
    public int ReserveAmmo => _reserveAmmo;
    public bool IsReloading => _isReloading;
    public EFireMode CurrentFireMode => _currentFireMode;

    #endregion

    #region ========== Initialization ==========

    private void Start()
    {
        InitializeHitEffectPool();
        InitializeRecoil();
        InitializeAmmo();
    }

    /// <summary>
    /// 탄약 초기화 + UI에 초기 상태 전달
    /// </summary>
    private void InitializeAmmo()
    {
        int magazineSize = _gunData != null ? _gunData.MagazineSize : DEFAULT_MAGAZINE_SIZE;
        int startingReserve = _gunData != null ? _gunData.StartingReserveAmmo : DEFAULT_RESERVE_AMMO;

        _currentAmmo = magazineSize;
        _reserveAmmo = startingReserve;
        _isReloading = false;
        _nextFireTime = 0f;

        // UI에 초기 상태 알림
        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);
        OnFireModeChanged?.Invoke(_currentFireMode);

        if (_gunData == null)
        {
            Debug.LogWarning("[PlayerGunFire] GunData not assigned. Using default values.");
        }
    }

    /// <summary>
    /// 반동 컴포넌트 자동 탐색 (Inspector에서 할당 안 했을 때)
    /// </summary>
    private void InitializeRecoil()
    {
        if (_cameraRecoil == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraRecoil = mainCam.GetComponent<CameraRecoil>();
            }
        }

        if (_cameraRecoil == null)
        {
            Debug.LogWarning("[PlayerGunFire] CameraRecoil not found. Recoil disabled.");
        }
    }

    /// <summary>
    /// 히트 이펙트 오브젝트 풀 생성
    /// </summary>
    private void InitializeHitEffectPool()
    {
        if (_hitEffectPrefab == null)
        {
            Debug.LogError("[PlayerGunFire] Hit Effect Prefab is not assigned!");
            return;
        }

        if (!ObjectPoolManager.Instance.HasPool(_hitEffectPoolTag))
        {
            ObjectPoolManager.Instance.CreatePool(_hitEffectPoolTag, _hitEffectPrefab, _poolInitialSize);
        }

        _isPoolInitialized = true;
    }

    #endregion

    #region ========== Update & Input ==========

    private void Update()
    {
        // 게임 상태 체크: Playing이 아니면 입력 무시
        if (GameManager.Instance.State != EGameState.Playing) return;

        HandleFireModeInput();
        HandleFireInput();
        HandleReloadInput();
    }

    /// <summary>
    /// 발사 모드 전환 (B키): Auto → Single → Burst → Auto
    /// </summary>
    private void HandleFireModeInput()
    {
        if (!Input.GetKeyDown(KeyCode.B)) return;
        if (_isBurstFiring || _isReloading) return;  // 점사/재장전 중 변경 불가

        CycleFireMode();
    }

    /// <summary>
    /// 발사 입력 처리 (마우스 좌클릭)
    /// </summary>
private void HandleFireInput()
    {
        switch (_currentFireMode)
        {
            case EFireMode.Auto:
                // 연사: 홀드 중 계속 발사
                if (Input.GetMouseButton(0))
                {
                    TryFire();
                }
                else
                {
                    // 마우스 떼면 발사 애니메이션 종료
                    _animator.SetBool("Fire", false);
                }
                break;

            case EFireMode.Single:
                // 단발: 클릭당 1발
                if (Input.GetMouseButtonDown(0))
                {
                    TryFire();
                }
                else if (!Input.GetMouseButton(0))
                {
                    // 마우스 안 누르면 애니메이션 종료
                    _animator.SetBool("Fire", false);
                }
                break;

            case EFireMode.Burst:
                // 점사: 클릭당 3발 연속
                if (Input.GetMouseButtonDown(0) && !_isBurstFiring)
                {
                    StartCoroutine(BurstFireCoroutine());
                }
                break;
        }
    }

    /// <summary>
    /// 재장전 입력 (R키)
    /// </summary>
    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
    }

    #endregion

    #region ========== Fire System ==========

    /// <summary>
    /// 발사 모드 순환: Auto → Single → Burst → Auto
    /// </summary>
    private void CycleFireMode()
    {
        _currentFireMode = _currentFireMode switch
        {
            EFireMode.Auto => EFireMode.Single,
            EFireMode.Single => EFireMode.Burst,
            EFireMode.Burst => EFireMode.Auto,
            _ => EFireMode.Auto
        };

        OnFireModeChanged?.Invoke(_currentFireMode);
        Debug.Log($"[PlayerGunFire] Fire mode: {_currentFireMode}");
    }

    /// <summary>
    /// 발사 시도 (조건 체크)
    /// </summary>
    private void TryFire()
    {
        if (_isReloading) return;                   // 재장전 중 발사 불가
        if (Time.time < _nextFireTime) return;      // 연사 쿨다운 중

        // 탄약 없으면 자동 재장전
        if (_currentAmmo <= 0)
        {
            TryReload();
            return;
        }

        Fire();
    }

    /// <summary>
    /// 실제 발사 실행
    /// </summary>
private void Fire()
    {
        // 발사 애니메이션: 매 발사마다 처음부터 재생
        _animator.SetBool("Fire", true);
        _animator.Play("Fire", 1, 0f);  // Layer 1 = Fire Layer

        // 시각 효과
        PlayMuzzleFlash();

        // 탄약 소모 + UI 알림
        _currentAmmo--;
        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);

        // 다음 발사 쿨다운 설정
        float fireRate = _gunData != null ? _gunData.FireRate : DEFAULT_FIRE_RATE;
        _nextFireTime = Time.time + fireRate;

        // 반동
        ApplyRecoil();

        // 레이캐스트 (카메라 중앙 → 전방)
        Ray ray = new Ray(_fireTransform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            PlayHitEffect(hitInfo.point, hitInfo.normal);
            ProcessDamage(hitInfo);
        }
    }

    /// <summary>
    /// 점사 코루틴: 3발을 연사 속도로 연속 발사
    /// </summary>
private IEnumerator BurstFireCoroutine()
    {
        _isBurstFiring = true;
        _burstShotsRemaining = BURST_COUNT;

        float fireRate = _gunData != null ? _gunData.FireRate : DEFAULT_FIRE_RATE;

        while (_burstShotsRemaining > 0)
        {
            // 재장전 시작되면 점사 중단
            if (_isReloading) break;

            // 탄약 소진 시 재장전 후 중단
            if (_currentAmmo <= 0)
            {
                TryReload();
                break;
            }

            Fire();
            _burstShotsRemaining--;

            // 다음 발사까지 대기
            if (_burstShotsRemaining > 0)
            {
                yield return new WaitForSeconds(fireRate);
            }
        }

        // 점사 종료 시 애니메이션 종료
        _animator.SetBool("Fire", false);
        _isBurstFiring = false;
    }

    /// <summary>
    /// 피격 대상에게 데미지 전달
    /// </summary>
    private void ProcessDamage(RaycastHit hitInfo)
    {
        Monster monster = hitInfo.collider.GetComponent<Monster>();
        if (monster != null)
        {
            monster.TryTakeDamage(_damage);
        }
    }

    #endregion

    #region ========== Reload System ==========

    /// <summary>
    /// 재장전 시도 (조건 체크)
    /// </summary>
private void TryReload()
    {
        if (_isReloading) return;  // 이미 재장전 중

        int magazineSize = _gunData != null ? _gunData.MagazineSize : DEFAULT_MAGAZINE_SIZE;
        if (_currentAmmo >= magazineSize) return;  // 탄창 가득 참

        if (_reserveAmmo <= 0)
        {
            Debug.Log("[PlayerGunFire] No reserve ammo!");
            return;
        }

        // 재장전 시작 시 발사 애니메이션 종료
        _animator.SetBool("Fire", false);

        StartCoroutine(ReloadCoroutine());
    }

    /// <summary>
    /// 재장전 코루틴
    /// - 코루틴: 여러 프레임에 걸쳐 실행되는 함수
    /// - yield return null: 다음 프레임까지 대기
    /// </summary>
    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;
        OnReloadStateChanged?.Invoke(true);

        float reloadTime = _gunData != null ? _gunData.ReloadTime : DEFAULT_RELOAD_TIME;
        float elapsedTime = 0f;

        // 매 프레임 진행률 업데이트 (UI 프로그레스 바용)
        while (elapsedTime < reloadTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / reloadTime);
            OnReloadProgress?.Invoke(progress);

            yield return null;
        }

        CompleteReload();

        _isReloading = false;
        OnReloadStateChanged?.Invoke(false);
    }

    /// <summary>
    /// 재장전 완료: 예비 탄약 → 탄창으로 이동
    /// </summary>
    private void CompleteReload()
    {
        int magazineSize = _gunData != null ? _gunData.MagazineSize : DEFAULT_MAGAZINE_SIZE;

        // 필요량 = 탄창 최대 - 현재 보유
        int ammoNeeded = magazineSize - _currentAmmo;

        // 실제 충전량 = min(필요량, 예비 보유량)
        int ammoToLoad = Mathf.Min(ammoNeeded, _reserveAmmo);

        _currentAmmo += ammoToLoad;
        _reserveAmmo -= ammoToLoad;

        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);
    }

    #endregion

    #region ========== Effects ==========

    /// <summary>
    /// 반동 적용 (CameraRecoil에 위임)
    /// </summary>
    private void ApplyRecoil()
    {
        if (_cameraRecoil != null)
        {
            _cameraRecoil.ApplyRecoil();
        }
    }

    /// <summary>
    /// 총구 화염 생성 (프리팹 Instantiate)
    /// - 프리팹 내 DestroyAfterTimeParticle이 자동 파괴 처리
    /// </summary>
    private void PlayMuzzleFlash()
    {
        if (_muzzleFlashPrefab == null || _muzzleFlashSpawnPoint == null) return;

        // Z축 랜덤 회전으로 시각적 변화
        float randomZ = UnityEngine.Random.Range(0f, 360f);
        Quaternion rotation = _muzzleFlashSpawnPoint.rotation * Quaternion.Euler(0f, 0f, randomZ);

        GameObject flash = Instantiate(_muzzleFlashPrefab, _muzzleFlashSpawnPoint.position, rotation);
        flash.transform.SetParent(_muzzleFlashSpawnPoint);
    }

    /// <summary>
    /// 피격 이펙트 재생 (오브젝트 풀 사용)
    /// </summary>
    private void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (!_isPoolInitialized) return;

        // 풀에서 꺼내서 위치/회전 설정
        GameObject effect = ObjectPoolManager.Instance.Spawn(
            _hitEffectPoolTag,
            position,
            Quaternion.LookRotation(normal)  // 표면 법선 방향으로 회전
        );

        if (effect != null)
        {
            ParticleSystem particle = effect.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                particle.Play();
            }

            // 일정 시간 후 풀로 반환
            ObjectPoolManager.Instance.Despawn(_hitEffectPoolTag, effect, _effectDuration);
        }
    }

    #endregion

    #region ========== Public API ==========

    /// <summary>
    /// 예비 탄약 추가 (탄약 픽업 등 외부에서 호출)
    /// </summary>
    public void AddReserveAmmo(int amount)
    {
        int maxReserve = _gunData != null ? _gunData.MaxReserveAmmo : DEFAULT_RESERVE_AMMO * 2;
        _reserveAmmo = Mathf.Min(_reserveAmmo + amount, maxReserve);
        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);
    }

    #endregion
}
