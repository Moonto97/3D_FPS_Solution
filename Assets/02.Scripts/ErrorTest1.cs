using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]     // 컴포넌트의 존재를 강제한다 -> 유니티 에디터에 스크립트를 붙이면 컴포넌트가 자동으로 생성되며 삭제가 불가하다.

public class ErrorTest1 : MonoBehaviour
{
    // 오류 : 프로그램이 비정상적으로 동작하는 문제
    // 예외 : 프로그램이 실행 중 발생하고 개발자가 처리할 수 있는 문제
    
    // 크게 3가지
    // 1. 문법 오류 : 문법에 맞지 않는 코드나 오타로 인해 발생 (런타임에러) -> 대부분 ide가 잡아준다.
    // 2. 런타임 오류 : 실행할 때 발생하는 오류 -> 테스트를 진행하면서 잡아주면 된다.(에디터 콘솔창에 비교적 명확하게 출력)
    // 3. 알고리즘 or 휴먼 오류 AI오류 : 주어진 문제에 대해 잘못된 해석이나 구현으로 내가 원하지 않는 결과물이 나오는 오류
    // 3. --> 가장 해결하기 어렵다. 공부 자료수집 분석 + 많은 경험을 통해 오류를 찾아내고 해결해내는 능력 키우기
    
    // 유니티에서 런타임(플레이중 )에 주로 나타나는 오류 (예외)
    
    
    
    private void Start()
    {
        // MissingComponentException
        // 사용하고자 하는 컴포넌트가 null일 때 
        Rigidbody2D rigidBody = GetComponent<Rigidbody2D>();
        Debug.Log(rigidBody.linearVelocity);
        // NullReferenceException or MissingComponentException
        // 사용하고자 하는 객체가 null 값일 때 그 객체의 필드나 메서드에 접근하려고 할 때
        Rigidbody2D rigidBody2 = null;
        Debug.Log(rigidBody2.linearVelocity);
        
        // 초기화시에 null 검사 하는 방어 코드를 작성
        // 방어코드 -> null 검사
        if (rigidBody == null)
        {
            // 적절한 처리
            // AddComponent
            // 오류 로깅
            // TryGetComponent 알아보기
        }
    }
}
