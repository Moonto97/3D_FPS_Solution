using UnityEngine;

public class PlayerGunFire : MonoBehaviour
{
    // 목표 : 마우스의 왼쪽 버튼을 누르면 바라보는 방향으로 총을 발사하고 싶다.
    [SerializeField] private Transform _fireTransform;
    [SerializeField] private ParticleSystem _hitEffect;
    private void Update()
    {
        // 1. 마우스 왼쪽 버튼이 눌린다면
        if (Input.GetMouseButton(0))
        {
            // 2. Ray 를 생성하고 발사할 위치, 방향, 거리를 설정한다. (쏜다.)
            Ray ray = new Ray(_fireTransform.position, Camera.main.transform.forward);
            // 3. RayCastHit 충돌한 대상의 정보를 저장할 변수 생성
            RaycastHit hitInfo = new RaycastHit();
            // 4. 발사하고
            bool isHit = Physics.Raycast(ray, out hitInfo);
            if (isHit)
            {
                // 5. 충돌했다면 피격 이팩트 표시
                Debug.Log(hitInfo.transform.name);
                // 파티클 생성과 플레이 방식
                // 1. Instantiate 방식 (+풀링) -> 새로 생성 (메모리, cpu 사용 많음) -> 한 화면에 여러가지 수정 후 여러개 그릴 경우 쓴다.
                // 2. 하나를 캐싱해두고 Play -> 재실행이므로 기존 파티클 삭제.. -> 한 화면에 한번만 그릴 경우 쓴다
                // 3. 하나를 캐싱해두고 Emit -> 한 화면에 위치만 수정 후 여러개 그릴 경우 쓴다
                
                
                //_hitEffect.transform.position = hitInfo.point;
                //_hitEffect.transform.forward = hitInfo.normal;
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
                emitParams.position = hitInfo.point;
                emitParams.rotation3D = Quaternion.LookRotation(hitInfo.normal).eulerAngles;
                _hitEffect.Emit(emitParams, 1);
            }
        }
        // Ray : 레이저 (시작 위치, 방향, 거리)
        // RayCast : 레이저를 발사
        // RayCastHit : 레이저가 물체와 충돌했다면 그 정보를 저장하는 구조체
    }
}
