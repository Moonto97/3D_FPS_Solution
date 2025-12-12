using Unity.VisualScripting;
using UnityEngine;

public class Barrel : MonoBehaviour
{
    public ValueStat Health;
    public float ExplosionRadius = 3f;
    public float Damage = 30f;
    public float ExplosionForce = 500f;
    [SerializeField] private GameObject ExplosionVFX;

    private Rigidbody _rigidbody;
    private bool _isExploding = false;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Health.Value <= 0f && !_isExploding)
        {
            Explode();
        }
    }

    public void TakeDamage(float damage)
    {
        Health.Decrease(damage);
    }

    private void Explode()
    {
        _isExploding = true;

        // 하늘로 솟구침
        if (_rigidbody != null)
        {
            _rigidbody.AddForce(Vector3.up * ExplosionForce);
        }

        // 폭발 이펙트
        GameObject explosionVFX = Instantiate(ExplosionVFX, transform.position, Quaternion.identity);

        // 주위에 대미지 (몬스터, 플레이어, 드럼통)
        Collider[] colliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

        for (int i = 0; i < colliders.Length; i++)
        {
            // 자기 자신은 제외
            if (colliders[i].gameObject == gameObject)
                continue;

            // 거리 계산
            float distance = Vector3.Distance(transform.position, colliders[i].transform.position);
            if (distance == 0) distance = 0.1f; // 0으로 나누기 방지

            // 거리에 반비례하는 대미지
            float finalDamage = Damage / distance;

            // Barrel 대미지
            Barrel barrel = colliders[i].GetComponent<Barrel>();
            if (barrel != null)
            {
                barrel.TakeDamage(finalDamage);
                continue;
            }

            // Player 대미지
            PlayerStats player = colliders[i].GetComponent<PlayerStats>();
            if (player != null)
            {
                player.TakeDamage(finalDamage);
                continue;
            }

            // Monster 대미지 (만약 있다면)
            // MonsterStats monster = colliders[i].GetComponent<MonsterStats>();
            // if (monster != null)
            // {
            //     monster.TakeDamage(finalDamage);
            //     continue;
            // }
        }

        Destroy(gameObject);
    }
}
