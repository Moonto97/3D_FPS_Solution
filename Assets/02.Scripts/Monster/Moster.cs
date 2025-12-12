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
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private  MonsterStats _monsterStats;
    private float _attackTimer = 0f;
    private Vector3 _defaultPosition;
    private Vector3 _knockbackVelocity;  // 넉백 시 밀려날 방향과 힘

    private void Start()
    {
        _defaultPosition = transform.position;
    }
    
    private void Update()
    {
        ApplyKnockBack();
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
        if(Vector3.Distance(transform.position, _player.transform.position) <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: {State} -> Trace");
        }
    }

    private void Trace()
    {
        // 플레이어를 쫓아가는 상태
        // Todo. Run 애니메이션 실행
        
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        // 1. 플레이어를 향하는 방향을 구한다.
        Vector3 direction = (_player.transform.position - transform.position).normalized;
        // 2. 방향에 따라 이동한다.
        _controller.Move(direction * _monsterStats.MoveSpeed.Value * Time.deltaTime);

        // 플레이어와의 거리가 공격범위내라면
        if (distance <= _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Attack;
            Debug.Log($"상태 전환: {State} -> Attack");
        }
        // 플레이어와의 거리가 감지범위 밖이라면
        if (distance >= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Comeback;
            Debug.Log($"상태 전환: {State} -> Comeback");
        }
    }
    
    private void Comeback()
    {
        // 만약 플레이어가 다시 감지범위에 들어온다면 Trace
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance <= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: {State} -> Trace");
        }
        // 현재 몬스터 포지션에서 defaultPosition 으로의 방향으로 이동
        Vector3 direction = (_defaultPosition - transform.position).normalized;
        _controller.Move(direction * _monsterStats.MoveSpeed.Value * Time.deltaTime);
    }
    
    private void Attack()
    {
        // 플레이어를 공격하는 상태
        
        // 플레이어와의 거리가 멀다면 다시 쫒아오는 상태로 전환
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance > _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: {State} -> Trace");
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _monsterStats.AttackSpeed.Value)
        {
            _attackTimer = 0f;
            
            Debug.Log("플레이어 공격!");
            
            _playerStats.TakeDamage(_monsterStats.Damage.Value);
        }
    }

    
    public bool TryTakeDamage(float damage)
    {
        if (State == EMonsterState.Hit || State == EMonsterState.Death)
        {
            return false;
        }
        
        _monsterStats.Health.Decrease(damage);
        _knockbackVelocity = transform.position -  _player.transform.position;
        ApplyKnockBack();
        if (_monsterStats.Health.Value > 0)
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

    private void ApplyKnockBack()
    {
        // 상태 변화는 필요없을듯 하고,
        // 플레이어 반대 방향으로 살짝 밀려난 뒤 현재 상태에 맞는 행동을 하면 될듯
        // 밀려나는 속도는 점점 줄어들고 -> 줄어드는 양
        // 밀려나는 거리도 있고 -> 밀리는 거리
        // 밀려나는 방향도 있고 -> 방향벡터
        //
        // KnockbackVelocity >> 얼마나, 어디로 밀리게 할지 힘
        // KnockbackDecay >> 힘이 얼마나 빨리 사라지게 할지 -> 점점 사라지게 Lerp 이용

        // 넉백 속도가 충분히 작으면 적용하지 않음
        if (_knockbackVelocity.sqrMagnitude < 0.01f)
        {
            _knockbackVelocity = Vector3.zero;
            return;
        }

        _controller.Move(_knockbackVelocity * Time.deltaTime);
        _knockbackVelocity = Vector3.Lerp(
            _knockbackVelocity,
            Vector3.zero,
            _monsterStats.KnockbackDecay.Value * Time.deltaTime
        );
        Debug.Log("넉백!");
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