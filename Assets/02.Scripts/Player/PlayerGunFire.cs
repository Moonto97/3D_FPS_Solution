using UnityEngine;

/// <summary>
/// 플레이어 총 발사 처리
/// 단일 책임: 총 발사 로직만 담당, 풀링은 ObjectPoolManager에 위임
/// </summary>
public class PlayerGunFire : MonoBehaviour
{
    [Header("Fire Settings")]
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private float _damage = 10f;

    [Header("Hit Effect (Pool)")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private string _hitEffectPoolTag = "HitEffect";
    [SerializeField] private int _poolInitialSize = 10;
    [SerializeField] private float _effectDuration = 2f;

    private bool _isPoolInitialized = false;

    private void Start()
    {
        InitializeHitEffectPool();
    }

    /// <summary>
    /// 히트 이펙트 풀 초기화 (ObjectPoolManager에 등록)
    /// </summary>
    private void InitializeHitEffectPool()
    {
        if (_hitEffectPrefab == null)
        {
            Debug.LogError("[PlayerGunFire] Hit Effect Prefab is not assigned!");
            return;
        }

        // 풀이 이미 존재하는지 확인 (다른 곳에서 먼저 등록했을 수 있음)
        if (!ObjectPoolManager.Instance.HasPool(_hitEffectPoolTag))
        {
            ObjectPoolManager.Instance.CreatePool(_hitEffectPoolTag, _hitEffectPrefab, _poolInitialSize);
        }

        _isPoolInitialized = true;
    }

    private void Update()
    {
        // 마우스 왼쪽 버튼이 눌린다면
        if (Input.GetMouseButtonDown(0))
        {
            Fire();
        }
    }

    /// <summary>
    /// 총 발사
    /// </summary>
    private void Fire()
    {
        // Ray 생성 및 발사
        Ray ray = new Ray(_fireTransform.position, Camera.main.transform.forward);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo))
        {
            // 피격 이펙트 표시
            PlayHitEffect(hitInfo.point, hitInfo.normal);

            // 데미지 처리
            ProcessDamage(hitInfo);
        }
    }

    /// <summary>
    /// 히트 이펙트 재생 (ObjectPoolManager 사용)
    /// </summary>
    private void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (!_isPoolInitialized) return;

        // 풀에서 이펙트 가져오기
        GameObject effect = ObjectPoolManager.Instance.Spawn(
            _hitEffectPoolTag,
            position,
            Quaternion.LookRotation(normal)
        );

        if (effect != null)
        {
            // 파티클 재생
            ParticleSystem particle = effect.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                particle.Play();
            }

            // 일정 시간 후 풀로 반환
            ObjectPoolManager.Instance.Despawn(_hitEffectPoolTag, effect, _effectDuration);
        }
    }

    /// <summary>
    /// 데미지 처리
    /// </summary>
    private void ProcessDamage(RaycastHit hitInfo)
    {
        Monster monster = hitInfo.collider.GetComponent<Monster>();
        if (monster != null)
        {
            monster.TryTakeDamage(_damage);
        }
    }
}