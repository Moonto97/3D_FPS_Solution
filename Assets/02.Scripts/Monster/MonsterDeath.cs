using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 몬스터 사망 처리. 애니메이션, 골드 드롭, 오브젝트 파괴를 담당.
/// Monster.cs에서 분리되어 단일 책임 원칙(SRP)을 따름.
/// 
/// [흐름]
/// 1. Die() 호출 → 넉백 완료 대기
/// 2. Death 애니메이션 재생
/// 3. 골드 드롭 (확률적, 설정은 MonsterStats에서 관리)
/// 4. 일정 시간 후 오브젝트 파괴
/// </summary>
public class MonsterDeath : MonoBehaviour
{
    #region 이벤트
    
    /// <summary>사망 처리 시작 시 발생. Monster가 상태를 Death로 설정해야 함.</summary>
    public event Action OnDeathStarted;
    
    /// <summary>사망 완료(파괴 직전) 시 발생.</summary>
    public event Action OnDeathCompleted;
    
    #endregion

    #region 외부 참조 (Monster.cs에서 주입)
    
    private Animator _animator;
    private CharacterController _controller;
    private MonsterStats _stats;
    private Func<bool> _isKnockbackActive;  // 넉백 상태 체크용 델리게이트
    
    #endregion

    #region 상수
    
    private const string DEATH_CLIP_NAME = "Standing React Death Right 1";
    private const float DEATH_LINGER_TIME = 2f;  // 애니메이션 후 바닥에 누워있는 시간
    private const float DEFAULT_DEATH_ANIM_LENGTH = 3.5f;
    
    #endregion

    #region 내부 상태
    
    private float _deathAnimationLength = DEFAULT_DEATH_ANIM_LENGTH;
    
    #endregion

    #region Properties
    
    /// <summary>사망 처리 중 여부.</summary>
    public bool IsDying { get; private set; }
    
    /// <summary>초기화 완료 여부.</summary>
    public bool IsInitialized => _animator != null;
    
    #endregion

    #region 초기화
    
    /// <summary>
    /// Monster.cs에서 호출하여 필요한 참조를 주입한다.
    /// </summary>
    /// <param name="animator">애니메이터</param>
    /// <param name="controller">CharacterController (사망 시 비활성화용)</param>
    /// <param name="stats">몬스터 스탯 (골드 드롭 설정 포함)</param>
    /// <param name="isKnockbackActive">넉백 상태 체크 델리게이트</param>
    public void Initialize(
        Animator animator, 
        CharacterController controller,
        MonsterStats stats,
        Func<bool> isKnockbackActive)
    {
        _animator = animator;
        _controller = controller;
        _stats = stats;
        _isKnockbackActive = isKnockbackActive;
        
        CacheDeathAnimationLength();
    }
    
    /// <summary>
    /// Death 애니메이션 클립 길이 캐싱.
    /// Destroy 타이밍 계산에 사용 (애니메이션 길이 + DEATH_LINGER_TIME).
    /// </summary>
    private void CacheDeathAnimationLength()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[Death] Animator가 없어 Death 애니메이션 길이를 가져올 수 없습니다.", this);
            return;
        }
        
        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == DEATH_CLIP_NAME)
            {
                _deathAnimationLength = clip.length;
                return;
            }
        }
        
        Debug.LogWarning($"[Death] '{DEATH_CLIP_NAME}' 클립을 찾을 수 없습니다. 기본값 사용.", this);
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// 사망 처리 시작. 코루틴으로 비동기 처리.
    /// </summary>
    public void Die()
    {
        if (IsDying) return;  // 중복 호출 방지
        
        IsDying = true;
        OnDeathStarted?.Invoke();
        StartCoroutine(DeathSequence());
    }
    
    #endregion

    #region 사망 시퀀스
    
    /// <summary>
    /// 사망 코루틴. 넉백 대기 → 애니메이션 → 골드 드롭 → 파괴.
    /// </summary>
    private IEnumerator DeathSequence()
    {
        // 1. 넉백 완료 대기 (죽으면서 밀려나는 연출)
        if (_isKnockbackActive != null)
        {
            yield return new WaitUntil(() => !_isKnockbackActive());
        }
        
        // 2. 추가 피격 방지 (CharacterController는 Collider 역할도 수행)
        if (_controller != null)
        {
            _controller.enabled = false;
        }
        
        // 3. Death 애니메이션 시작
        _animator.SetTrigger("Death");
        
        // 4. 골드 드롭
        TryDropGold();
        
        // 5. 애니메이션 + 바닥 대기 시간 후 파괴
        yield return new WaitForSeconds(_deathAnimationLength + DEATH_LINGER_TIME);
        
        OnDeathCompleted?.Invoke();
        Destroy(gameObject);
    }
    
    #endregion

    #region 골드 드롭
    
    /// <summary>
    /// 확률에 따라 골드 코인 드롭. 설정은 MonsterStats에서 가져옴.
    /// </summary>
    private void TryDropGold()
    {
        // 스탯 또는 프리팹 없으면 스킵
        if (_stats == null || _stats.GoldCoinPrefab == null)
        {
            Debug.LogWarning("[Death] MonsterStats 또는 골드 프리팹이 설정되지 않았습니다.", this);
            return;
        }
        
        // 확률 체크
        if (UnityEngine.Random.value > _stats.GoldDropChance) return;
        
        int dropCount = UnityEngine.Random.Range(_stats.GoldDropCountMin, _stats.GoldDropCountMax + 1);
        Vector3 dropPosition = transform.position + Vector3.up * _stats.GoldDropHeight;
        
        for (int i = 0; i < dropCount; i++)
        {
            GameObject coin = SpawnGoldCoin(dropPosition);
            
            // 소닉 스타일: 균등 각도로 방사형 폭발
            if (coin != null && coin.TryGetComponent(out GoldCoin goldCoin))
            {
                goldCoin.LaunchRadial(i, dropCount);
            }
        }
    }
    
    /// <summary>
    /// 골드 코인 1개 생성. 오브젝트 풀 우선, 없으면 Instantiate.
    /// </summary>
    private GameObject SpawnGoldCoin(Vector3 position)
    {
        // 오브젝트 풀 시스템 사용 시도
        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.HasPool(GoldCoin.POOL_TAG))
        {
            return ObjectPoolManager.Instance.Spawn(GoldCoin.POOL_TAG, position, Quaternion.identity);
        }
        
        // 폴백: 직접 생성
        return Instantiate(_stats.GoldCoinPrefab, position, Quaternion.identity);
    }
    
    #endregion
}
