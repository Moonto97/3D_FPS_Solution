using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_OptionPopup : MonoBehaviour
{
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Start()
    {
        Hide();
    }

    private void GameContinue()
    {
        
    }

private void GameReset()
    {
        SceneManager.LoadScene(0);
    }
    
    private void GameExit()
    {
        GameManager.Instance.Quit();
    }
}
