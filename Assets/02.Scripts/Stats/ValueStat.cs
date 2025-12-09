using System;
using UnityEngine;

// 프로그래머

// 하드 스킬 (기술)
// - 프로그래밍 언어, 엔진에 대한 이해 , 최적화, 툴
// - 특정 도메인 지식 (게임을 많이 해보세요)
// - 취업을 하게 해준다.
// - 이 사람에게 이 기능을 맡기면 구현은 확실히 된다. 쓸만하다.

// 소프트 스킬
// - 커뮤니케이션 (말 알아듣기, 잘 설득하기, QA)
// - 문제를 정의하거나 보고 능력
// - 책임감, 협업태도, 멘탈/시간 관리
// 일을 안짤리고 오래하게 해준다. -> 리더로 갈 경우 소프트 스킬 역량이 더 높아져야 됩니다.


[Serializable]
public class ValueStat
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