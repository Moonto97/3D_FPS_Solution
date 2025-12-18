using UnityEngine;

/// <summary>
/// 무기별 반동 패턴 데이터
/// ScriptableObject: 데이터 에셋으로 저장, 여러 무기가 공유/참조 가능
/// </summary>
[CreateAssetMenu(fileName = "RecoilData", menuName = "Weapon/Recoil Data")]
public class RecoilData : ScriptableObject
{
    [Header("Vertical Recoil (위로 튀는 정도)")]
    [Tooltip("발사 시 카메라가 위로 올라가는 각도")]
    [SerializeField, Range(0f, 10f)] private float _verticalRecoil = 2f;
    
    [Tooltip("수직 반동의 랜덤 범위 (±)")]
    [SerializeField, Range(0f, 2f)] private float _verticalRandomness = 0.5f;

    [Header("Horizontal Recoil (좌우 흔들림)")]
    [Tooltip("좌우 흔들림 최대 각도 (±)")]
    [SerializeField, Range(0f, 5f)] private float _horizontalRecoil = 1f;

    // 읽기 전용 프로퍼티
    public float VerticalRecoil => _verticalRecoil;
    public float VerticalRandomness => _verticalRandomness;
    public float HorizontalRecoil => _horizontalRecoil;

    /// <summary>
    /// 랜덤성이 적용된 수직 반동값 반환
    /// </summary>
    public float GetRandomizedVerticalRecoil()
    {
        return _verticalRecoil + Random.Range(-_verticalRandomness, _verticalRandomness);
    }

    /// <summary>
    /// 랜덤 수평 반동값 반환 (좌 또는 우)
    /// </summary>
    public float GetRandomizedHorizontalRecoil()
    {
        return Random.Range(-_horizontalRecoil, _horizontalRecoil);
    }
}
