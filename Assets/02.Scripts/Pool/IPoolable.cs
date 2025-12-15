/// <summary>
/// 오브젝트 풀링이 가능한 객체가 구현해야 하는 인터페이스
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 풀에서 꺼내질 때 호출 (활성화 시 초기화)
    /// </summary>
    void OnSpawnFromPool();

    /// <summary>
    /// 풀로 반환될 때 호출 (비활성화 전 정리)
    /// </summary>
    void OnReturnToPool();
}