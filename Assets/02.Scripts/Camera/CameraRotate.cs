using UnityEngine;

// 카메라 회전 기능
// 마우스를 조작하면 카메라를 그 방향으로 회전하고 싶다.
public class CameraRotate : MonoBehaviour
{
    public float RotationSpeed = 200f; // 0 ~ 360

    
    // 유니티는 0~360각도 체계이므로 우리가 따로 저장할 -360 ~ 360 쳬계로 누적할 변수
    private float _accumulationX = 0;
    private float _accumulationY = 0;
    
    private void Update()
    {
        
        // 게임 시작하면 y축이 0도에서 -> -1도
        
        // 1. 마우스 입력 받기
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        
        // 2. 마우스 입력을 누적한 방향을 구한다.
        _accumulationX +=  mouseX * RotationSpeed * Time.deltaTime; // 범위가 없다.
        _accumulationY += -mouseY * RotationSpeed * Time.deltaTime; // 범위가 없다.
        
        
        // 3. 사람처럼 -90 ~ 90도 사이로 제한한다.
        _accumulationY = Mathf.Clamp(_accumulationY, -90f, 90f);
        
        // 4. 누적한 회전 방향으로 카메라 회전하기
        transform.eulerAngles = new Vector3(_accumulationY, _accumulationX);
       
        
        // 쿼터니언: 사원수 : 쓰는 이유는 짐벌락 현상 방지
        // 공부: 짐벌락, 쿼터니언을 왜 쓰냐 (게임 수학/물리)
        
        // 문제 : 잘 되긴되는데 한번씩 세상이 뒤집어진다...
        
    }
   
}