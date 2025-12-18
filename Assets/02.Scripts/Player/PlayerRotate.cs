using UnityEngine;

/// <summary>
/// 플레이어 Y축 회전 - 카메라 수평 회전과 동기화
/// 1인칭/3인칭 모두 카메라가 바라보는 방향으로 플레이어 회전
/// </summary>
public class PlayerRotate : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CameraRotate _cameraRotate;
    
    private void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (_cameraRotate != null) return;
        
        // Inspector 미설정 시 자동 검색 시도
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            _cameraRotate = mainCam.GetComponent<CameraRotate>();
        }
        
        if (_cameraRotate == null)
        {
            Debug.LogError("[PlayerRotate] CameraRotate 참조가 없습니다! Inspector에서 설정해주세요.", this);
        }
    }

    /// <summary>
    /// LateUpdate: CameraRotate.Update() 이후 실행 보장
    /// 카메라 회전 완료 후 플레이어 회전 적용
    /// </summary>
    private void LateUpdate()
    {
        if (GameManager.Instance.State != EGameState.Playing) return;
        if (_cameraRotate == null) return;
        
        // CameraRotate의 누적 회전값을 직접 사용 (변환 오차 없음)
        float yRotation = _cameraRotate.CurrentHorizontalAngle;
        transform.eulerAngles = new Vector3(0f, yRotation, 0f);
    }
}
