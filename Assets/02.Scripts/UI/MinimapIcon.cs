using UnityEngine;

/// <summary>
/// 미니맵 아이콘: 엔티티(플레이어/몬스터) 위에 배치되어 미니맵에 표시됨
/// MinimapOnly 레이어로 설정하여 메인 카메라에서는 보이지 않음
/// </summary>
public class MinimapIcon : MonoBehaviour
{
    [Header("아이콘 설정")]
    [SerializeField] private float _heightOffset = 10f;     // 부모 위 높이
    [SerializeField] private float _iconSize = 2f;          // Quad 크기
    [SerializeField] private bool _rotateWithParent = true; // 부모 회전 따라가기
    
    private Transform _parent;
    
    private void Start()
    {
        _parent = transform.parent;
        
        if (_parent == null)
        {
            Debug.LogWarning("[MinimapIcon] 부모 오브젝트가 없습니다!", this);
        }
        
        // 아이콘 크기 설정
        transform.localScale = new Vector3(_iconSize, _iconSize, _iconSize);
    }
    
    private void LateUpdate()
    {
        if (_parent == null) return;
        
        // 부모 위치 + 높이 오프셋
        transform.position = _parent.position + Vector3.up * _heightOffset;
        
        // 미니맵 카메라를 향해 누워있도록 (X축 90도)
        // 부모 회전을 따라가려면 Y축 회전만 적용
        if (_rotateWithParent)
        {
            float yRotation = _parent.eulerAngles.y;
            transform.rotation = Quaternion.Euler(90f, yRotation, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
