using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScene : MonoBehaviour
{
    [SerializeField] private Slider _progressBar;
    [SerializeField] private TextMeshProUGUI _progressBarText;
    
    private void Start()
    {
        StartCoroutine(LoadScene_Coroutine());
    }

    private IEnumerator LoadScene_Coroutine()
    {
        // 씬 로드 상황에 대한 데이터를 가지고 있는 객체를 반환
        AsyncOperation ao = SceneManager.LoadSceneAsync("SampleScene");
        
        // 로드되는 씬의 모습이 화면에 안보이게
        ao.allowSceneActivation = false;
        
        // 로드가 완료될 때 까지 
        while (!ao.isDone)
        {
            _progressBar.value = ao.progress;
            _progressBarText.text = $"{ao.progress * 100}%";

            if (ao.progress >= 0.9f)
            {
                ao.allowSceneActivation = true;
            }
            
            yield return null;
        }
    }
}
