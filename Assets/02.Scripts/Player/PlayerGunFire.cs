using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 총 발사 처리
/// 책임: 총 발사 + 탄약 관리 + 재장전
/// 풀링은 ObjectPoolManager에, 데이터는 GunData에 위임
/// </summary>
public class PlayerGunFire : MonoBehaviour
{
    [Header("Gun Data (ScriptableObject)")]
    [Tooltip("총기 데이터. 없으면 기본값 사용")]
    [SerializeField] private GunData _gunData;

    [Header("Fire Settings")]
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private float _damage = 10f;

    [Header("Recoil (자동 탐색)")]
    [Tooltip("null이면 Main Camera에서 자동 탐색")]
    [SerializeField] private CameraRecoil _cameraRecoil;

    [Header("Hit Effect (Pool)")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private string _hitEffectPoolTag = "HitEffect";
    [SerializeField] private int _poolInitialSize = 10;
    [SerializeField] private float _effectDuration = 2f;

    // === 탄약 상태 (런타임) ===
    private int _currentAmmo;      // 현재 탄창의 탄약
    private int _reserveAmmo;      // 예비 탄약
    private bool _isReloading;     // 재장전 중 여부
    private float _nextFireTime;   // 다음 발사 가능 시간

    // === 이벤트: UI 연동용 ===
    // Action<현재탄약, 예비탄약>: UI가 이 이벤트를 구독해서 탄약 수 업데이트
    public event Action<int, int> OnAmmoChanged;
    
    // Action<진행률(0~1)>: 재장전 진행바 업데이트용
    public event Action<float> OnReloadProgress;
    
    // Action<bool>: 재장전 시작/종료 알림
    public event Action<bool> OnReloadStateChanged;

    // === 외부에서 읽기용 프로퍼티 ===
    public int CurrentAmmo => _currentAmmo;
    public int ReserveAmmo => _reserveAmmo;
    public bool IsReloading => _isReloading;

    private bool _isPoolInitialized = false;

    // === 기본값 (GunData 없을 때 폴백) ===
    private const int DEFAULT_MAGAZINE_SIZE = 30;
    private const int DEFAULT_RESERVE_AMMO = 120;
    private const float DEFAULT_RELOAD_TIME = 1.6f;
    private const float DEFAULT_FIRE_RATE = 0.1f;

    private void Start()
    {
        InitializeHitEffectPool();
        InitializeRecoil();
        InitializeAmmo();
    }

    /// <summary>
    /// 탄약 초기화 (게임 시작 시)
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

        if (_gunData == null)
        {
            Debug.LogWarning("[PlayerGunFire] GunData not assigned. Using default values.");
        }
    }

    /// <summary>
    /// 반동 시스템 초기화 (자동 탐색)
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
    /// 히트 이펙트 풀 초기화
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

    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;

        HandleFireInput();
        HandleReloadInput();
    }

    /// <summary>
    /// 발사 입력 처리 (마우스 좌클릭 홀드 = 연사)
    /// </summary>
    private void HandleFireInput()
    {
        // 연사: GetMouseButton (홀드 중 계속 true)
        if (Input.GetMouseButton(0))
        {
            TryFire();
        }
    }

    /// <summary>
    /// 재장전 입력 처리 (R키)
    /// </summary>
    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
    }

    /// <summary>
    /// 발사 시도 (조건 체크 후 실행)
    /// </summary>
    private void TryFire()
    {
        // 재장전 중이면 발사 불가
        if (_isReloading) return;

        // 연사 속도 제한 (아직 쿨다운 중이면 발사 불가)
        if (Time.time < _nextFireTime) return;

        // 탄약 없으면 발사 불가 + 자동 재장전 시도
        if (_currentAmmo <= 0)
        {
            TryReload();
            return;
        }

        Fire();
    }

    /// <summary>
    /// 실제 총 발사 처리
    /// </summary>
    private void Fire()
    {
        // 탄약 소모
        _currentAmmo--;
        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);

        // 다음 발사 시간 설정 (연사 속도)
        float fireRate = _gunData != null ? _gunData.FireRate : DEFAULT_FIRE_RATE;
        _nextFireTime = Time.time + fireRate;

        // 반동 적용
        ApplyRecoil();

        // Ray 발사 (카메라 방향으로)
        Ray ray = new Ray(_fireTransform.position, Camera.main.transform.forward);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo))
        {
            PlayHitEffect(hitInfo.point, hitInfo.normal);
            ProcessDamage(hitInfo);
        }
    }

    /// <summary>
    /// 재장전 시도 (조건 체크 후 시작)
    /// </summary>
    private void TryReload()
    {
        // 이미 재장전 중이면 무시
        if (_isReloading) return;

        int magazineSize = _gunData != null ? _gunData.MagazineSize : DEFAULT_MAGAZINE_SIZE;

        // 탄창이 이미 가득 찼으면 재장전 불필요
        if (_currentAmmo >= magazineSize) return;

        // 예비 탄약 없으면 재장전 불가
        if (_reserveAmmo <= 0)
        {
            Debug.Log("[PlayerGunFire] No reserve ammo!");
            return;
        }

        StartCoroutine(ReloadCoroutine());
    }

    /// <summary>
    /// 재장전 코루틴 (시간 경과 + UI 업데이트)
    /// 코루틴: Unity에서 시간에 걸친 작업을 처리하는 방법
    /// yield return으로 프레임마다 실행을 일시정지했다 재개
    /// </summary>
    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;
        OnReloadStateChanged?.Invoke(true);

        float reloadTime = _gunData != null ? _gunData.ReloadTime : DEFAULT_RELOAD_TIME;
        float elapsedTime = 0f;

        // 재장전 진행 (매 프레임 진행률 업데이트)
        while (elapsedTime < reloadTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / reloadTime);
            OnReloadProgress?.Invoke(progress);
            
            yield return null; // 다음 프레임까지 대기
        }

        CompleteReload();

        _isReloading = false;
        OnReloadStateChanged?.Invoke(false);
    }

    /// <summary>
    /// 재장전 완료 처리 (탄약 이동 계산)
    /// </summary>
    private void CompleteReload()
    {
        int magazineSize = _gunData != null ? _gunData.MagazineSize : DEFAULT_MAGAZINE_SIZE;

        // 필요한 탄약 수 = 탄창 최대 - 현재 탄약
        int ammoNeeded = magazineSize - _currentAmmo;

        // 실제로 채울 수 있는 탄약 = min(필요량, 보유량)
        int ammoToLoad = Mathf.Min(ammoNeeded, _reserveAmmo);

        _currentAmmo += ammoToLoad;
        _reserveAmmo -= ammoToLoad;

        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);
    }

    /// <summary>
    /// 외부에서 탄약 추가 (탄약 픽업 등)
    /// </summary>
    public void AddReserveAmmo(int amount)
    {
        int maxReserve = _gunData != null ? _gunData.MaxReserveAmmo : DEFAULT_RESERVE_AMMO * 2;
        _reserveAmmo = Mathf.Min(_reserveAmmo + amount, maxReserve);
        OnAmmoChanged?.Invoke(_currentAmmo, _reserveAmmo);
    }

    private void ApplyRecoil()
    {
        if (_cameraRecoil != null)
        {
            _cameraRecoil.ApplyRecoil();
        }
    }

    private void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (!_isPoolInitialized) return;

        GameObject effect = ObjectPoolManager.Instance.Spawn(
            _hitEffectPoolTag,
            position,
            Quaternion.LookRotation(normal)
        );

        if (effect != null)
        {
            ParticleSystem particle = effect.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                particle.Play();
            }

            ObjectPoolManager.Instance.Despawn(_hitEffectPoolTag, effect, _effectDuration);
        }
    }

    private void ProcessDamage(RaycastHit hitInfo)
    {
        Monster monster = hitInfo.collider.GetComponent<Monster>();
        if (monster != null)
        {
            monster.TryTakeDamage(_damage);
        }
    }
}
