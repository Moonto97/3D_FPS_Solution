using UnityEngine;

/// <summary>
/// 총기 데이터 ScriptableObject
/// 역할: 탄창 크기, 재장전 시간 등 총기별 고정 데이터를 저장
/// 사용법: Assets/ScriptableObjects/Weapons에서 Create > Weapon > Gun Data로 생성
/// </summary>
[CreateAssetMenu(fileName = "NewGunData", menuName = "Weapon/Gun Data")]
public class GunData : ScriptableObject
{
    [Header("탄약 설정")]
    [Tooltip("한 탄창에 들어가는 최대 탄약 수")]
    [SerializeField] private int _magazineSize = 30;
    
    [Tooltip("게임 시작 시 보유하는 예비 탄약 수")]
    [SerializeField] private int _startingReserveAmmo = 120;
    
    [Tooltip("보유 가능한 최대 예비 탄약 수")]
    [SerializeField] private int _maxReserveAmmo = 300;

    [Header("재장전 설정")]
    [Tooltip("재장전에 걸리는 시간 (초)")]
    [SerializeField] private float _reloadTime = 1.6f;

    [Header("발사 설정")]
    [Tooltip("발사 간격 (초). 낮을수록 연사 속도 빠름")]
    [SerializeField] private float _fireRate = 0.1f;

    [Header("탄퍼짐 설정")]
    [Tooltip("기본 탄퍼짐 각도 (도)")]
    [SerializeField] private float _baseSpread = 0.5f;
    
    [Tooltip("최대 탄퍼짐 각도 (도)")]
    [SerializeField] private float _maxSpread = 5f;
    
    [Tooltip("발사당 탄퍼짐 증가량 (도)")]
    [SerializeField] private float _spreadIncrement = 0.3f;
    
    [Tooltip("초당 탄퍼짐 회복량 (도)")]
    [SerializeField] private float _spreadRecovery = 8f;


    // === 읽기 전용 프로퍼티 ===
    // 외부에서는 데이터를 읽기만 가능, 수정 불가 (데이터 무결성)
    public int MagazineSize => _magazineSize;
    public int StartingReserveAmmo => _startingReserveAmmo;
    public int MaxReserveAmmo => _maxReserveAmmo;
    public float ReloadTime => _reloadTime;
    public float FireRate => _fireRate;
    public float BaseSpread => _baseSpread;
    public float MaxSpread => _maxSpread;
    public float SpreadIncrement => _spreadIncrement;
    public float SpreadRecovery => _spreadRecovery;

}
