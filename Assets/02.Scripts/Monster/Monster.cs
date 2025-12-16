using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] private NavMeshAgent _agent;
    
    private float _attackTimer = 0f;
    private Vector3 _defaultPosition;
    
    
    // 점프(OffMeshLink 통과) 관련 변수
    private Vector3 _jumpStartPosition;
    private Vector3 _jumpEndPosition;
    private float _jumpProgress;           // 점프 진행도 (0~1)
    private const float JUMP_DURATION = 0.5f;  // 점프 소요 시간
    private const float JUMP_HEIGHT = 1.5f;    // 점프 최대 높이
private Vector3 _knockbackVelocity;  // 넉백 시 밀려날 방향과 힘

private void Start()
    {
        _defaultPosition = transform.position;

        _agent.speed = _monsterStats.MoveSpeed.Value;
        _agent.stoppingDistance = _monsterStats.AttackDistance.Value;
        
        // OffMeshLink 수동 제어 (포물선 점프를 위해)
        _agent.autoTraverseOffMeshLink = false;
        
        // NavMesh 배치 검증
        if (!_agent.isOnNavMesh)
        {
            Debug.LogError($"[Monster] {gameObject.name}이 NavMesh 위에 없습니다! NavMesh 베이크 확인 필요.", this);
        }
    }
    
    private void Update()
    {
            if (GameManager.Instance.State != EGameState.Playing) return;

        
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
            
            
            
            case EMonsterState.Jump:
                Jump();
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
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        // NavMeshAgent.SetDestination은 목적지 근처의 NavMesh 지점을 자동으로 찾음
        // SamplePosition을 수동으로 하면 높이가 다른 NavMesh를 못 찾는 문제 발생
        // 그래서 플레이어 위치를 직접 전달하고 NavMeshAgent가 알아서 처리하게 함
        _agent.SetDestination(_player.transform.position);
        
        // 디버그: 경로 상태 확인
        if (!_agent.hasPath && !_agent.pathPending)
        {
            Debug.LogWarning($"[Monster] 경로 없음! 목적지:{_player.transform.position}, 상태:{_agent.pathStatus}");
        }
        
        // 플레이어와의 거리가 공격범위내라면
        if (distance <= _monsterStats.AttackDistance.Value)
        {
            State = EMonsterState.Attack;
            Debug.Log($"상태 전환: Trace -> Attack");
            return;
        }
        
        // 플레이어와의 거리가 감지범위 밖이라면
        if (distance >= _monsterStats.DetectDistance.Value)
        {
            State = EMonsterState.Comeback;
            Debug.Log($"상태 전환: Trace -> Comeback");
            return;
        }
        
        // OffMeshLink 감지
        TryHandleOffMeshLink();
    }
    
private void Comeback()
    {
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        
        // 플레이어가 다시 감지범위에 들어오면 추적 재개
        if (distance <= _monsterStats.DetectDistance.Value)
        {
            Debug.Log($"상태 전환: Comeback -> Trace");
            State = EMonsterState.Trace;
            return;
        }
        
        // 초기 위치로 복귀
        _agent.SetDestination(_defaultPosition);
        
        // 복귀 중에도 단차 만나면 점프
        TryHandleOffMeshLink();
    }

/// <summary>
    /// OffMeshLink(NavMesh의 단차/점프 구간) 감지 및 Jump 상태 전환
    /// NavMeshSurface의 Generate Links로 자동 생성된 링크 포함
    /// </summary>
    private void TryHandleOffMeshLink()
    {
        // 링크 위에 있지 않으면 무시
        if (!_agent.isOnOffMeshLink) return;
        
        // 링크 데이터 추출
        OffMeshLinkData linkData = _agent.currentOffMeshLinkData;
        _jumpStartPosition = linkData.startPos;
        _jumpEndPosition = linkData.endPos;
        _jumpProgress = 0f;
        
        // NavMeshAgent 일시 정지 (수동으로 이동할 것이므로)
        _agent.isStopped = true;
        
        float heightDiff = _jumpEndPosition.y - _jumpStartPosition.y;
        Debug.Log($"링크 감지 - 높이차: {heightDiff:F2}m");
        
        State = EMonsterState.Jump;
        Debug.Log($"상태 전환: -> Jump");
    }

/// <summary>
    /// 포물선 점프로 OffMeshLink 통과
    /// </summary>
    private void Jump()
    {
        _jumpProgress += Time.deltaTime / JUMP_DURATION;
        
        if (_jumpProgress >= 1f)
        {
            // 점프 완료
            _jumpProgress = 1f;
            transform.position = _jumpEndPosition;
            
            // OffMeshLink 통과 완료 알림
            _agent.CompleteOffMeshLink();
            
            // NavMeshAgent 재활성화
            _agent.isStopped = false;
            
            State = EMonsterState.Trace;
            Debug.Log($"상태 전환: Jump -> Trace");
            return;
        }
        
        // 포물선 이동 계산
        // 수평 이동: 시작점에서 끝점으로 선형 보간
        Vector3 horizontalPos = Vector3.Lerp(_jumpStartPosition, _jumpEndPosition, _jumpProgress);
        
        // 수직 이동: 포물선 (0에서 시작해서 0.5에서 최대, 1에서 0)
        // 공식: 4 * h * t * (1 - t) 로 최대 높이 h에 도달
        float parabola = 4f * JUMP_HEIGHT * _jumpProgress * (1f - _jumpProgress);
        
        // 최종 위치 = 수평 위치 + 포물선 높이
        transform.position = new Vector3(
            horizontalPos.x,
            horizontalPos.y + parabola,
            horizontalPos.z
        );
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
        
        _agent.isStopped = true;    // 이동 일시정지
        _agent.ResetPath();         // 경로 (목적지) 삭제 
        
        _knockbackVelocity = (transform.position -  _player.transform.position).normalized * _monsterStats.KnockbackForce.Value;
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