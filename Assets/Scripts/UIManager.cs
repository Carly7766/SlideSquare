using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Text       turnText;
    public Text       phaseText;
    public GameObject slidePanel;
    public GameObject winOverlay;
    public Text       winText;
    public Button     restartButton;

    private void Start()
    {
        if (restartButton)
            restartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());

        ShowSlidePanel(false);
        ShowWinOverlay(false);
    }

    public void UpdateStatus(PlayerSide player, GamePhase phase)
    {
        string name = player == PlayerSide.PlayerA ? "Player A (Blue)" : "Player B (Red)";
        if (turnText)  turnText.text = "Turn: " + name;
        if (phaseText)
        {
            switch (phase)
            {
                case GamePhase.SelectPiece:      phaseText.text = "駒を選んでください";         break;
                case GamePhase.SelectDestination: phaseText.text = "移動先を選んでください";      break;
                case GamePhase.SelectSlide:       phaseText.text = "スライドする列を選んでください"; break;
                default:                          phaseText.text = "";                          break;
            }
        }
    }

    public void ShowSlidePanel(bool show)
    {
        if (slidePanel) slidePanel.SetActive(show);
    }

    public void ShowWinOverlay(bool show)
    {
        if (winOverlay) winOverlay.SetActive(show);
    }

    public void ShowWin(PlayerSide winner)
    {
        string name = winner == PlayerSide.PlayerA ? "Player A (Blue)" : "Player B (Red)";
        if (winText) winText.text = name + " の勝利！";
        ShowWinOverlay(true);
    }
}
