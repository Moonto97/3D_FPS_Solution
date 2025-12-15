using System;
using UnityEngine;

[Serializable]
public class ConsumableStat
{
    // 유니티 인스팩터에서 직접 설정해줄 값
    [SerializeField] private float _maxValue;
    [SerializeField] private float _value;
    [SerializeField] private float _regenValue;

    public float MaxValue => _maxValue;
    public float Value    => _value;

    // 이벤트: 값이 변경될 때 호출 (현재값, 최대값)
    public event Action<float, float> OnValueChanged;
    
    public void Initialize()
    {
        SetValue(_maxValue);
    }
    
    public void Regenerate(float time)
    {
        float oldValue = _value;
        _value += _regenValue * time;

        if (_value > _maxValue)
        {
            _value = _maxValue;
        }

        // 값이 실제로 변경되었을 때만 이벤트 발생
        if (oldValue != _value)
        {
            OnValueChanged?.Invoke(_value, _maxValue);
        }
    }

    public bool TryConsume(float amount)
    {
        if (_value < amount) return false;
        
        Consume(amount);
        
        return true;
    }
    

    public void Consume(float amount)
    {
        _value -= amount;
        OnValueChanged?.Invoke(_value, _maxValue);
    }
    
    public void IncreaseMax(float amount)
    {
        _maxValue += amount;
    }
    public void Increase(float amount)
    {
        SetValue(_value + amount);
    }

    public void DecreaseMax(float amount)
    {
        _maxValue -= amount;
    }
    public void Decrease(float amount)
    {
        _value -= amount;
        OnValueChanged?.Invoke(_value, _maxValue);
    }


    public void SetMaxValue(float value)
    {
        _maxValue = value;
    }
    public void SetValue(float value)
    {
        _value = value;

        if (_value > _maxValue)
        {
            _value = _maxValue;
        }

        OnValueChanged?.Invoke(_value, _maxValue);
    }
    
}