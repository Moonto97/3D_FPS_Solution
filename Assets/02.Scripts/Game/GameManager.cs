using System.Collections;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance => _instance;
    
    private EGameState _state = EGameState.Ready;
    public EGameState State => _state;

    [SerializeField] private TextMeshProUGUI _stateTextUI;

    private float _showReadyUI = 2f;
    private float _showStartUI = 0.5f;

    private void Awake()
    {
        _instance = this;
    }
    
    private void Start()
    {
        _stateTextUI.gameObject.SetActive(true);

        _state = EGameState.Ready;
        _stateTextUI.text = "준비중...";

        StartCoroutine(StartToPlay_Coroutine());
    }

    private IEnumerator StartToPlay_Coroutine()
    {
        yield return new WaitForSeconds(_showReadyUI);

        _stateTextUI.text = "시작!";
        
        yield return new WaitForSeconds(_showStartUI);

        _state = EGameState.Playing;
        
        _stateTextUI.gameObject.SetActive(false);
    }
}