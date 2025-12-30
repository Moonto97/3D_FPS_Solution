using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScene : MonoBehaviour
{
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TextMeshProUGUI _progressBarText;
    
    // Unity 비동기 로딩은 allowSceneActivation=false일 때 이 값에서 멈춤
    private const float MAX_ASYNC_PROGRESS = 0.9f;
    
    private void Start()
    {
        StartCoroutine(LoadScene_Coroutine());
    }

    private IEnumerator LoadScene_Coroutine()
    {
        Debug.Log("[LoadingScene] 씬 로딩 시작: NewScene");
        
        AsyncOperation ao = SceneManager.LoadSceneAsync("NewScene");
        
        if (ao == null)
        {
            Debug.LogError("[LoadingScene] AsyncOperation이 null입니다. 씬 이름을 확인하세요.");
            yield break;
        }
        
        // 로딩 완료 전까지 씬 전환 대기
        ao.allowSceneActivation = false;
        
        while (!ao.isDone)
        {
            // 0~0.9 범위를 0~1로 정규화하여 UI에 표시
            float normalizedProgress = Mathf.Clamp01(ao.progress / MAX_ASYNC_PROGRESS);
            
            _progressBar.value = normalizedProgress;
            _progressBarText.text = $"{normalizedProgress * 100:F0}%";
            
            // 디버그: 실제 progress 값 확인 (문제 해결 후 제거 가능)
            Debug.Log($"[LoadingScene] 로딩 진행률: {ao.progress:F2} (UI: {normalizedProgress * 100:F0}%)");

            // 로딩 완료 시 씬 활성화
            if (ao.progress >= MAX_ASYNC_PROGRESS)
            {
                Debug.Log("[LoadingScene] 로딩 완료, 씬 전환 시작");
                _progressBar.value = 1f;
                _progressBarText.text = "100%";
                
                ao.allowSceneActivation = true;
            }
            
            yield return null;
        }
        
        Debug.Log("[LoadingScene] 씬 전환 완료");
    }
}
