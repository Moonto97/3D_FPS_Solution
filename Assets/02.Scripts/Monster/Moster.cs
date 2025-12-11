using System.Collections;
using UnityEngine;

public class Monster : MonoBehaviour
{
    #region Comment
    // 목표: 처음에는 가만히 있지만 플레이어가 다가가면 쫓아오는 좀비 몬스터를 만들고 싶다.
    //       ㄴ 쫓아 오다가 너무 멀어지면 제자리로 돌아간다.
    
    // Idle   : 가만히 있는다.
    //   I  (플레이어가 가까이 오면) (컨디션, 트랜지션)
    // Trace  : 플레이러를 쫒아간다.
    //   I  (플레이어와 너무 멀어지면)
    // Return : 제자리로 돌아가는 상태
    //   I  (제자리에 도착했다면)
    //  Idle
    // 공격
    // 피격
    // 죽음
    
    
    
    // 몬스터 인공지능(AI) : 사람처럼 행동하는 똑똑한 시스템/알고리즘
    // - 규칙 기반 인공지능 : 정해진 규칙에 따라 조건문/반복문등을 이용해서 코딩하는 것
    //                     -> ex) FSM(유한 상태머신), BT(행동 트리)
    // - 학습 기반 인공지능: 머신러닝(딥러닝, 강화학습 .. )
    
    // Finite State Machine(유한 상태 머신)
    // N개의 상태를 가지고 있고, 상태마다 행동이 다르다.
    

    #endregion

    public EMonsterState State = EMonsterState.Idle;

    [SerializeField] private GameObject _player;
    [SerializeField] private CharacterController _controller;
    // 스텟 스크립트 따로 만들어서 관리하기
    public float DetectDistance = 4f;
    public float AttackDistance = 1.2f;
    
    public float MoveSpeed   = 5f;
    public float AttackSpeed = 2f;
    public float AttackTimer = 0f;
    

    private void Update()
    {
        // 몬스터의 상태에 따라 다른 행동을한다. (다른 메서드를 호출한다.)
        switch (State)
        {
            case EMonsterState.Idle:
                Idle();
                break;
            
            case EMonsterState.Trace:
                Trace();
                break;
            
            case EMonsterState.Comeback:
                Comeback();
                break;
            
            case EMonsterState.Attack:
                Attack();
                break;
        }
    }
    
    // 1. 함수는 한 가지 일만 잘해야 한다.
    // 2. 상태별 행동을 함수로 만든다.
    private void Idle()
    {
        // 대기하는 상태
        // Todo. Idle 애니메이션 실행
            
        // 플레이어가 탐지범위 안에 있다면...
        if(Vector3.Distance(transform.position, _player.transform.position) <= DetectDistance)
        {
            State = EMonsterState.Trace;
            Debug.Log("상태 전환: Idle -> Trace");
        }
    }

    private void Trace()
    {
        // 플레이어를 쫓아가는 상태
        // Todo. Run 애니메이션 실행
        
        // Comback 과제
        
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        // 1. 플레이어를 향하는 방향을 구한다.
        Vector3 direction = (_player.transform.position - transform.position).normalized;
        // 2. 방향에 따라 이동한다.
        _controller.Move(direction * MoveSpeed * Time.deltaTime);

        // 플레이어와의 거리가 공격범위내라면
        if (distance <= AttackDistance)
        {
            State = EMonsterState.Attack;
            Debug.Log("상태 전환: Trace -> Attack");
        }
    }

    private void Comeback()
    {
        // 과제 1. 제자리로 복귀하는 상태
    }

    private void Attack()
    {
        // 플레이어를 공격하는 상태
        
        // 플레이어와의 거리가 멀다면 다시 쫒아오는 상태로 전환
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance > AttackDistance)
        {
            State = EMonsterState.Trace;
            Debug.Log("상태 전환: Attack -> Trace");
            return;
        }

        AttackTimer += Time.deltaTime;
        if (AttackTimer >= AttackSpeed)
        {
            AttackTimer = 0f;
            Debug.Log("플레이어 공격!");
            
            // 과제 2번. 플레이어 공격하기
        }
    }


    public float Health = 100;
    public bool TryTakeDamage(float damage)
    {
        if (State == EMonsterState.Hit || State == EMonsterState.Death)
        {
            return false;
        }
        
        Health -= damage;

        if (Health > 0)
        {
            // 히트상태
            Debug.Log($"상태 전환: {State} -> Hit");
            State = EMonsterState.Hit;

            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            // 데스상태
            Debug.Log($"상태 전환: {State} -> Death");
            State = EMonsterState.Death;
            StartCoroutine(Death_Coroutine());
        }

        return true;
    }
    
    private IEnumerator Hit_Coroutine()
    {
        // Todo. Hit 애니메이션 실행
        
        yield return new WaitForSeconds(0.2f);
        State = EMonsterState.Idle;
    }

    private IEnumerator Death_Coroutine()
    {
        // Todo. Death 애니메이션 실행
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
    
}