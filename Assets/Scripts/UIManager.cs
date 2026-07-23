using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // Matches GamePiece's PlayerABg / PlayerBBg so the turn text reads as the same color as that player's pieces.
    private static readonly Color PlayerAColor = new Color(0.15f, 0.35f, 0.85f);
    private static readonly Color PlayerBColor = new Color(0.85f, 0.15f, 0.15f);

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
        if (turnText)
        {
            turnText.text  = "Turn: " + name;
            turnText.color = player == PlayerSide.PlayerA ? PlayerAColor : PlayerBColor;
        }
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

    public void ShowDraw()
    {
        if (winText) winText.text = "引き分け（千日手）";
        ShowWinOverlay(true);
    }
}
