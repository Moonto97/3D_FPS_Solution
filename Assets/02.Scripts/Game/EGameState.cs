using UnityEngine;

public enum EGameState
{
    Ready,
    Playing,
    UIMode,     // ESC로 진입: 커서 표시, 발사/회전 비활성화
    GameOver
}
