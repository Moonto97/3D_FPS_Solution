using System;
using UnityEngine;

// 키보드를 누르면 캐릭터를 그 방향으로 이동 시키고 싶다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMove : MonoBehaviour
{
    [Serializable] // json, sciptableObject 혹은 DB에서 읽어오게 하면된다.
    public class MoveConfig
    {
        public float Gravity;
        public float RunStamina;
        public float JumpStamina;
    }

    public MoveConfig _config;

    
    private CharacterController _controller;
    private PlayerStats _stats;
    
    private float _yVelocity = 0f;   // 중력에 의해 누적될 y값 변수
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        // 0. 중력을 누적한다.
        _yVelocity += _config.Gravity * Time.deltaTime;
        
        // 1. 키보드 입력 받기
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        
        // 2. 입력에 따른 방향 구하기 
        // 현재는 유니티 세상의 절대적인 방향이 기준 (글로벌/월드 좌표계)
        // 내가 원하는 것은 카메라가 쳐다보는 방향이 기준으로
        
        // - 글로벌 좌표 방향을 구한다. 
        Vector3 direction = new Vector3(x, 0, y);
        direction.Normalize();

        
        
        // - 점프! : 점프 키를 누르고 && 땅이라면
        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
        {
            _yVelocity = _stats.JumpPower.Value;
        }
        
        // - 카메라가 쳐다보는 방향으로 변환한다. (월드 -> 로컬)
        direction = Camera.main.transform.TransformDirection(direction);
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