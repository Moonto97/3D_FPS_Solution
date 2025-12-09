using System;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    public GameObject ExplosionObjectPrefab;
    private void OnCollisionEnter(Collision other)
    {
        GameObject effectObject = Instantiate(ExplosionObjectPrefab);
        effectObject.transform.position = transform.position;

        // 충돌하면 나 자신 삭제
        Destroy(gameObject);

    }
}
