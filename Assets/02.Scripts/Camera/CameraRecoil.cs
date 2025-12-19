using UnityEngine;

/// <summary>
/// 카메라 반동 처리
/// 단일 책임: 반동 적용만 담당 (회복 없음)
/// </summary>
public class CameraRecoil : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraRotate _cameraRotate;

    [Header("Recoil Data")]
    [SerializeField] private RecoilData _recoilData;

    private void Awake()
    {
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        // CameraRotate 자동 찾기 (같은 오브젝트에 있을 경우)
        if (_cameraRotate == null)
        {
            _cameraRotate = GetComponent<CameraRotate>();
        }

        if (_cameraRotate == null)
        {
            Debug.LogError("[CameraRecoil] CameraRotate reference is missing!");
        }

        if (_recoilData == null)
        {
            Debug.LogError("[CameraRecoil] RecoilData is not assigned! Create via: Create > Weapon > Recoil Data");
        }
    }

    /// <summary>
    /// 반동 적용 (외부에서 호출)
    /// </summary>
    public void ApplyRecoil()
    {
        if (_cameraRotate == null || _recoilData == null) return;

        // 수직 반동: 카메라를 위로 (Y 누적값 감소 = 위를 봄)
        float verticalKick = _recoilData.GetRandomizedVerticalRecoil();
        _cameraRotate.AddVerticalRotation(-verticalKick);

        // 수평 반동: 좌우 랜덤 흔들림
        float horizontalKick = _recoilData.GetRandomizedHorizontalRecoil();
        _cameraRotate.AddHorizontalRotation(horizontalKick);
    }

    /// <summary>
    /// 반동 데이터 변경 (무기 교체 시 사용)
    /// </summary>
    public void SetRecoilData(RecoilData newData)
    {
        _recoilData = newData;
    }
}
