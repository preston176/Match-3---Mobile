using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGame : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public UIPauseWindow pausePopup;
    public UIGameOverPopup gameOverPopup;
    public UIGameWinPopup gameWinPopup;
    public TMP_Text scoreText;
    public TMP_Text goalText;
    public TMP_Text multiplierText;
    public TMP_Text movesRemaining;
    public TMP_Text timerText;
    public Button pauseButton;
    public Slider scoreSlider;

    private void Update()
    {
        // only show when in playing state
        panel.SetActive(GameManager.Instance.State == GameState.Playing);

        if (panel.activeSelf)
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        scoreText.text = GameManager.Instance.Score.ToString();
        goalText.text = GameManager.Instance.ScoreToWin.ToString();
        multiplierText.text = "x" + Board.Instance.GetCascadeComboMultiplier().ToString();
        movesRemaining.text = GameManager.Instance.MovesRemaining.ToString();
        pauseButton.interactable = GameManager.Instance.State == GameState.Playing;
        scoreSlider.value = GameManager.Instance.ScoreProgressPercent;
    }

    public void Pause()
    {
        if (pausePopup.window.IsOpen)
            return;

        pausePopup.Pause();
    }
}
