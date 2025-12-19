using UnityEngine;

/// <summary>
/// 카메라 회전 처리
/// 단일 책임: 마우스 입력에 따른 카메라 회전
/// 외부에서 회전값 조작 가능 (반동 시스템 등)
/// </summary>
public class CameraRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;
    
    // 읽기 전용 프로퍼티 (외부에서 현재 회전값 확인용)
    public float RotationSpeed => _rotationSpeed;

    // 유니티는 0~360 각도 체계이므로 -360 ~ 360 체계로 누적할 변수
    private float _accumulationX = 0;
    private float _accumulationY = 0;

    private void Update()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;

        // 1. 마우스 입력 받기
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 2. 마우스 입력을 누적한 방향을 구한다
        _accumulationX +=  mouseX * _rotationSpeed * Time.deltaTime;
        _accumulationY += -mouseY * _rotationSpeed * Time.deltaTime;

        // 3. 사람처럼 -90 ~ 90도 사이로 제한한다
        _accumulationY = Mathf.Clamp(_accumulationY, -90f, 90f);

        // 4. 누적한 회전 방향으로 카메라 회전하기
        transform.eulerAngles = new Vector3(_accumulationY, _accumulationX);
    }

    #region External Rotation Control (반동 시스템용)

    /// <summary>
    /// 수직 회전값 추가 (양수 = 아래로, 음수 = 위로)
    /// </summary>
    public void AddVerticalRotation(float amount)
    {
        _accumulationY += amount;
        _accumulationY = Mathf.Clamp(_accumulationY, -90f, 90f);
    }

    /// <summary>
    /// 수평 회전값 추가
    /// </summary>
    public void AddHorizontalRotation(float amount)
    {
        _accumulationX += amount;
    }

    /// <summary>
    /// 현재 수직 회전값 (읽기 전용)
    /// </summary>
    public float CurrentVerticalAngle => _accumulationY;

    /// <summary>
    /// 현재 수평 회전값 (읽기 전용)
    /// </summary>
    public float CurrentHorizontalAngle => _accumulationX;

    #endregion
}
