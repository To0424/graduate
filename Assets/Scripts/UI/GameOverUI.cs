using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject levelWonPanel;
    public GameObject levelLostPanel;
    public GameObject graduationPanel;

    [Header("Level Won")]
    public TextMeshProUGUI creditsEarnedText;
    public Button continueButton;

    [Header("Level Lost")]
    public Button retryButton;
    public Button quitToMapButton;

    [Header("Graduation")]
    public TextMeshProUGUI graduationMessage;

    void Start()
    {
        HideAll();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetry);
        if (quitToMapButton != null)
            quitToMapButton.onClick.AddListener(OnQuitToMap);
    }

    void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleStateChange;
    }

    void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleStateChange;
    }

    void HandleStateChange(GameState state)
    {
        HideAll();

        switch (state)
        {
            case GameState.LevelWon:
                ShowLevelWon();
                break;
            case GameState.LevelLost:
                ShowLevelLost();
                break;
            case GameState.Graduated:
                ShowGraduation();
                break;
        }
    }

    void ShowLevelWon()
    {
        if (levelWonPanel != null) levelWonPanel.SetActive(true);

        LevelData level = GameManager.Instance?.GetCurrentLevel();
        if (level != null)
        {
            if (creditsEarnedText != null)
                creditsEarnedText.text = $"+{level.creditsReward} Credits";
        }
    }

    void ShowLevelLost()
    {
        if (levelLostPanel != null) levelLostPanel.SetActive(true);
    }

    void ShowGraduation()
    {
        if (graduationPanel != null) graduationPanel.SetActive(true);
        if (graduationMessage != null)
            graduationMessage.text = "Congratulations! You have graduated from HKU!";
    }

    void HideAll()
    {
        if (levelWonPanel != null) levelWonPanel.SetActive(false);
        if (levelLostPanel != null) levelLostPanel.SetActive(false);
        if (graduationPanel != null) graduationPanel.SetActive(false);
    }

    void OnContinue()
    {
        GameManager.Instance?.GoToOverworld();
    }

    void OnRetry()
    {
        GameManager.Instance?.RetryLevel();
    }

    void OnQuitToMap()
    {
        GameManager.Instance?.GoToOverworld();
    }
}
