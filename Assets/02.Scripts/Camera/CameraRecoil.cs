using UnityEngine;

/// <summary>
/// 카메라 반동 처리
/// 단일 책임: 반동 적용 및 회복만 담당
/// CameraRotate의 누적값을 직접 조작하여 자연스러운 반동 구현
/// </summary>
public class CameraRecoil : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraRotate _cameraRotate;

    [Header("Recoil Data")]
    [SerializeField] private RecoilData _recoilData;

    // 반동 회복 관련
    private float _targetVerticalRecoil;    // 회복해야 할 수직 반동량
    private float _timeSinceLastShot;       // 마지막 발사 후 경과 시간
    private bool _isRecovering;             // 회복 중인지 여부

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

    private void Update()
    {
        UpdateRecovery();
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

        // 회복을 위해 반동량 누적
        _targetVerticalRecoil += verticalKick;
        _timeSinceLastShot = 0f;
        _isRecovering = false;
    }

    /// <summary>
    /// 반동 회복 처리 (자동으로 원래 위치로 돌아감)
    /// </summary>
    private void UpdateRecovery()
    {
        if (_recoilData == null) return;

        _timeSinceLastShot += Time.deltaTime;

        // 회복 딜레이 후 회복 시작
        if (_timeSinceLastShot >= _recoilData.RecoveryDelay)
        {
            _isRecovering = true;
        }

        // 회복 중이고 아직 회복할 반동이 남아있으면
        if (_isRecovering && _targetVerticalRecoil > 0.01f)
        {
            // 부드럽게 원래 위치로 회복
            float recoveryAmount = _recoilData.RecoverySpeed * Time.deltaTime;
            float actualRecovery = Mathf.Min(recoveryAmount, _targetVerticalRecoil);

            // 카메라를 아래로 (원래 위치로)
            _cameraRotate.AddVerticalRotation(actualRecovery);
            _targetVerticalRecoil -= actualRecovery;
        }
        else if (_targetVerticalRecoil <= 0.01f)
        {
            // 회복 완료
            _targetVerticalRecoil = 0f;
            _isRecovering = false;
        }
    }

    /// <summary>
    /// 반동 데이터 변경 (무기 교체 시 사용)
    /// </summary>
    public void SetRecoilData(RecoilData newData)
    {
        _recoilData = newData;
    }

    /// <summary>
    /// 반동 초기화 (무기 교체, 재장전 등)
    /// </summary>
    public void ResetRecoil()
    {
        _targetVerticalRecoil = 0f;
        _isRecovering = false;
    }
}
