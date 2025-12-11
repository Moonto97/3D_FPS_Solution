using Unity.VisualScripting;
using UnityEngine;

public class PlayerGunFire : MonoBehaviour
{
    // 목표: 마우스의 왼쪽 버튼을 누르면 바라보는 방향으로 총을 발사하고 싶다. (총알을 날리고 싶다.)
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private ParticleSystem _hitEffect;
    private float _damage = 10f;
    private void Update()
    {
        // 1. 마우슨 왼쪽 버튼이 눌린다면..
        if (Input.GetMouseButtonDown(0))
        {
            // 2. Ray를 생성하고 발사할 위치, 방향, 거리를 설정한다. (쏜다.)
            Ray ray = new Ray(_fireTransform.position, Camera.main.transform.forward);
            
            // 3. RayCastHit(충돌한 대상의 정보)를 저장할 변수를 생성한다.
            RaycastHit hitInfo = new RaycastHit();
            
            // 4. 발사하고,
            bool isHit = Physics.Raycast(ray, out hitInfo);
            if (isHit)
            {
                // 5.  충돌했다면... 피격 이펙트 표시
                Debug.Log(hitInfo.transform.name);
                
                // 파티클 생성과 플레이 방식
                // 1. Instantiate 방식 (+ 풀링) -> 한 화면에 여러가지 수정 후 여러개 그릴경우
                // 2. 하나를 캐싱해두고 Play     -> 인스펙터 설정 그대로 그릴 경우
                // 3. 하나를 캐싱해두고 Emit     -> 인스펙터 설정을 수정 후 그릴 경우
                
                _hitEffect.transform.position = hitInfo.point;
                _hitEffect.transform.forward = hitInfo.normal;

                _hitEffect.Play();

                // 레이어, 컴포넌트, 태그 방식을 선택하는 기준 여기서는 컴포넌트를 쓴 이유
                Monster monster = hitInfo.collider.gameObject.GetComponent<Monster>();
                if (monster != null)
                {
                    monster.TryTakeDamage(_damage);
                }
            }
        }
        
        // Ray: 레이저(시작위치, 방향, 거리)
        // RayCast: 레이저를 발사
        // RayCastHit: 레이저가 물체와 충돌했다면 그 정보를 저장하는 구조체
    }

}