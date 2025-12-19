using System;
using UnityEngine;

public class PlayerBombFire : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // 발사 설정
    // ─────────────────────────────────────────────────────────
    [Header("발사 설정")]
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;
    
    // ─────────────────────────────────────────────────────────
    // 폭탄 보유량 설정 (Inspector에서 튜닝)
    // ─────────────────────────────────────────────────────────
    [Header("폭탄 보유량")]
    [Tooltip("최대 5발, 3초마다 1발 회복")]
    [SerializeField] private ConsumableStat _bombCount = new ConsumableStat();
    
    // UI 연동용: 외부에서 폭탄 개수 변화를 구독할 수 있는 이벤트
    // Action<현재개수, 최대개수>
    public event Action<int, int> OnBombCountChanged;
    
    // 외부에서 현재 폭탄 상태를 읽기 위한 프로퍼티
    public int CurrentBombCount => Mathf.FloorToInt(_bombCount.Value);
    public int MaxBombCount => Mathf.FloorToInt(_bombCount.MaxValue);
    
    private void Awake()
    {
        // 게임 시작 시 폭탄 개수를 최대치로 초기화
        _bombCount.Initialize();
        
        // ConsumableStat 내부 이벤트를 외부용 이벤트로 브릿지
        // ConsumableStat은 float 기반이지만, 폭탄은 정수 단위로 표시
        _bombCount.OnValueChanged += HandleBombCountChanged;
    }
    
    private void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        _bombCount.OnValueChanged -= HandleBombCountChanged;
    }
    
    private void Start()
    {
        // UI 초기화를 위해 시작 시 이벤트 발생
        OnBombCountChanged?.Invoke(CurrentBombCount, MaxBombCount);
    }
    
    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;
        
        // 매 프레임 폭탄 자동 회복 (ConsumableStat 내부에서 최대치 제한)
        _bombCount.Regenerate(Time.deltaTime);
        
        // 마우스 오른쪽 버튼: 폭탄 발사
        if (Input.GetMouseButtonDown(1))
        {
            TryFireBomb();
        }
    }
    
    /// <summary>
    /// 폭탄 발사 시도. 보유량이 1 이상일 때만 발사.
    /// </summary>
    private void TryFireBomb()
    {
        // 폭탄 1개 소비 시도 (정수 단위로 1개 소비)
        if (!_bombCount.TryConsume(1f))
        {
            // 폭탄 부족 시 발사 불가 (필요하면 여기에 사운드/UI 피드백 추가)
            Debug.Log("[PlayerBombFire] 폭탄이 부족합니다!");
            return;
        }
        
        // 폭탄 생성 및 발사
        Bomb bomb = Instantiate(_bombPrefab, _fireTransform.position, Quaternion.identity);
        Rigidbody rigidbody = bomb.GetComponent<Rigidbody>();
        rigidbody.AddForce(Camera.main.transform.forward * _throwPower, ForceMode.Impulse);
    }
    
    /// <summary>
    /// ConsumableStat의 float 값 변경을 정수 기반 외부 이벤트로 변환
    /// </summary>
    private void HandleBombCountChanged(float current, float max)
    {
        // float → int 변환 (바닥 함수로 실제 사용 가능한 폭탄 개수 계산)
        int currentInt = Mathf.FloorToInt(current);
        int maxInt = Mathf.FloorToInt(max);
        
        OnBombCountChanged?.Invoke(currentInt, maxInt);
    }
}
