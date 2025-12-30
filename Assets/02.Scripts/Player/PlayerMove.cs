using UnityEngine;

/// <summary>
/// 플레이어 이동 처리
/// 책임: 키보드 입력에 따른 이동, 점프, 달리기
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMove : MonoBehaviour
{
    [Header("이동 설정 (ScriptableObject)")]
    [SerializeField] private MoveConfig _config;

    
    private CharacterController _controller;
    private PlayerStats _stats;
    private Camera _mainCamera;
    
    private Animator _animator;
    
    private float _yVelocity = 0f;   // 중력에 의해 누적될 y값 변수
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _animator = GetComponentInChildren<Animator>();
        _mainCamera = Camera.main;
        
        ValidateReferences();
    }
    
    private void ValidateReferences()
    {
        if (_config == null)
        {
            Debug.LogError("[PlayerMove] MoveConfig가 할당되지 않았습니다! Create > Player > Move Config로 생성 후 연결하세요.", this);
        }
        
        if (_mainCamera == null)
        {
            Debug.LogError("[PlayerMove] Main Camera를 찾을 수 없습니다!", this);
        }
    }

private void Update()
    {
        // 중력은 게임 상태와 무관하게 항상 적용
        _yVelocity += _config.Gravity * Time.deltaTime;
        
        // Playing 상태가 아니면 중력만 적용하고 입력은 무시
        if (GameManager.Instance.State != EGameState.Playing)
        {
            _controller.Move(new Vector3(0, _yVelocity, 0) * Time.deltaTime);
            return;
        }
        
        // 1. 키보드 입력 받기
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        
        // 2. 입력에 따른 방향 구하기 
        // 현재는 유니티 세상의 절대적인 방향이 기준 (글로벌/월드 좌표계)
        // 내가 원하는 것은 카메라가 쳐다보는 방향이 기준으로
        
        // - 글로벌 좌표 방향을 구한다. 
        Vector3 direction = new Vector3(x, 0, y);
        _animator.SetFloat("Speed", direction.magnitude);
        direction.Normalize();

        
        
        // - 점프! : 점프 키를 누르고 && 땅이라면
        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
        {
            _yVelocity = _stats.JumpPower.Value;
        }
        
        // - 카메라가 쳐다보는 방향으로 변환한다. (월드 -> 로컬)
        direction = _mainCamera.transform.TransformDirection(direction);
        direction.y = _yVelocity; // 중력 적용



        float moveSpeed = _stats.MoveSpeed.Value;
        if (Input.GetKey(KeyCode.LeftShift) && _stats.Stamina.TryConsume(_config.RunStamina * Time.deltaTime))
        {
            moveSpeed = _stats.RunSpeed.Value;
        }
        
        // 3. 방향으로 이동시키기  
        _controller.Move(direction * moveSpeed * Time.deltaTime);
    }
    
}