using UnityEngine;
using System;

// 프로그래머

// 하드 스킬
// - 프로그래밍 언어, 엔진에 대한 이해, 최적화, 툴, 특정 도메인 지식
// - 취업을 하게 해준다.
// - 이 사람에게 이 기능을 맡기면 구현은 확실히 된다.

// 소프트 스킬
// - 커뮤니케이션
// - 문제를 정의하거나 보고 능력
// - 책임감, 협업 태도, 멘탈/시간 관리
// - 일을 효과적으로 오래 하게 해준다.
// - 리더로 갈 수록 소프트 스킬 역량이 높아야 한다.
[Serializable]
public class ValueStats
{
    [SerializeField]
    private float _value;
    public float Value => _value;
    
    public void Increase(float amount)
    {
        _value += amount;
    }
    
    public void Decrease(float amount)
    {
        _value -= amount;
    }
    
    public void SetValue(float value)
    {
        _value = value;
    }
}
