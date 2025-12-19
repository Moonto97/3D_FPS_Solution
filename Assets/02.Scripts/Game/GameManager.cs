using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance => _instance;
    
    private EGameState _state = EGameState.Ready;
    public EGameState State => _state;

    [Header("UI 참조")]
    [SerializeField] private TextMeshProUGUI _stateTextUI;
    [SerializeField] private CursorManager _cursorManager;

    [Header("타이밍")]
    [SerializeField] private float _showReadyUI = 2f;
    [SerializeField] private float _showStartUI = 0.5f;

    // 외부에서 상태 변경 감지용 이벤트
    public event Action<EGameState> OnStateChanged;

    private void Awake()
    {
        _instance = this;
    }
    
    private void Start()
    {
        ValidateReferences();
        
        _stateTextUI.gameObject.SetActive(true);
        _state = EGameState.Ready;
        _stateTextUI.text = "준비중...";

        StartCoroutine(StartToPlay_Coroutine());
    }

    private void Update()
    {
        // ESC 키로 Playing <-> UIMode 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleUIMode();
        }
    }

    private void ValidateReferences()
    {
        if (_cursorManager == null)
        {
            _cursorManager = FindFirstObjectByType<CursorManager>();
            if (_cursorManager == null)
            {
                Debug.LogWarning("[GameManager] CursorManager를 찾을 수 없습니다.");
            }
        }
    }

    private IEnumerator StartToPlay_Coroutine()
    {
        yield return new WaitForSeconds(_showReadyUI);

        _stateTextUI.text = "시작!";
        
        yield return new WaitForSeconds(_showStartUI);

        SetState(EGameState.Playing);
        _stateTextUI.gameObject.SetActive(false);
    }

    /// <summary>
    /// Playing <-> UIMode 전환
    /// UIMode: 커서 표시, 마우스로 UI 조작 가능
    /// </summary>
    public void ToggleUIMode()
    {
        if (_state == EGameState.Playing)
        {
            SetState(EGameState.UIMode);
            _cursorManager?.UnlockCursor();
        }
        else if (_state == EGameState.UIMode)
        {
            SetState(EGameState.Playing);
            _cursorManager?.LockCursor();
        }
        // Ready, GameOver 상태에서는 토글 무시
    }

    private void SetState(EGameState newState)
    {
        _state = newState;
        OnStateChanged?.Invoke(_state);
    }

    public void GameOver()
    {
        _stateTextUI.gameObject.SetActive(true);
        SetState(EGameState.GameOver);
        _stateTextUI.text = "게임 오버..";
        _cursorManager?.UnlockCursor();
    }
}