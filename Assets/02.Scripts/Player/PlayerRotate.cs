using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    public float RotationSpeed = 200f; // 0 ~ 360
    private float _accumulationX = 0;

    private void Update()
    {
        if (!Input.GetMouseButton(1))
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X");
        _accumulationX += mouseX * RotationSpeed * Time.deltaTime; // 범위가 없다.
        
        transform.eulerAngles = new Vector3(0, _accumulationX);
    }
}