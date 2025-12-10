using System.Collections;
using UnityEngine;

public class CoroutineExample2 : MonoBehaviour
{
    private Coroutine _coroutine; // 코루틴 핸들러
    
    // 시간을 제어하고 싶을때 코루틴을 많이 쓸것이다.
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine); // 현재 실행중인 코루틴을 중지한다.
                StopAllCoroutines();       // 현재 실행중인 모든 코루틴을 중지한다. (특별한 의도가 없으면 사용 X)
            }
           
            _coroutine = StartCoroutine(Reload_Coroutine());
        }
    }

    private IEnumerator Reload_Coroutine()
    {
        
        // 실행 제어 관점에서의 코루틴
        // - 특정 프레임, 시간 단위로 실행을 제어하거나
        // - 특정 조건이 만족할 때까지 실행을 제어할 수 있다. 
        yield return new WaitForSeconds(1.6f);
        
        Debug.Log("총알이 재장전됐습니다.");
        // Reload();
    }
    
}