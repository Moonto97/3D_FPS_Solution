using System;
using UnityEngine;


// 게임에서 마우스 왼쪽 클릭과 오른족 클릭을 깍각 몇 번 했는지 추적하는 클래스
public class ClickManager : MonoBehaviour
{
    public static ClickManager Instance;

    private int _leftClickCount = 0;
    private int _rightClickCount = 0;

    public int LeftClickCount => _leftClickCount;
    public int RightClickCount => _rightClickCount;

    // 우주하마는 유튜버로써 구독자 목록을 가지고 있고, 영상을 올릴때 마다 구독자들의 알람 함수를 호출해준다.
    // 클래기 매니저는 구독 함수 목록을 가지고 있고, 데이터가 변경될때 마다 그 함수들을 모두 호출해준다.
    public event Action OnDataChanged;
    
    
    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _leftClickCount++;
            
            OnDataChanged?.Invoke();    // OnDataChanged 가 null이 아닐 경우 OnDataChanged 를 호출한다.
        }

        if (Input.GetMouseButtonDown(1))
        {
            _rightClickCount++;
            
            OnDataChanged?.Invoke();
        }
    }

}
