using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Text References")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI creditsText;

    [Header("Buttons")]
    public Button startRoundButton;
    public Button pauseButton;

    void OnEnable()
    {
        CurrencyManager.OnGoldChanged += UpdateGold;
        LivesManager.OnLivesChanged += UpdateLives;
        WaveSpawner.OnRoundStart += UpdateRound;
        CreditManager.OnCreditsChanged += UpdateCredits;
    }

    void OnDisable()
    {
        CurrencyManager.OnGoldChanged -= UpdateGold;
        LivesManager.OnLivesChanged -= UpdateLives;
        WaveSpawner.OnRoundStart -= UpdateRound;
        CreditManager.OnCreditsChanged -= UpdateCredits;
    }

    void Start()
    {
        if (startRoundButton != null)
            startRoundButton.onClick.AddListener(OnStartRound);
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPause);

        // Initialize display
        if (CurrencyManager.Instance != null) UpdateGold(CurrencyManager.Instance.Gold);
        if (LivesManager.Instance != null) UpdateLives(LivesManager.Instance.Lives);
        if (CreditManager.Instance != null) UpdateCredits(CreditManager.Instance.TotalCredits);
    }

    void UpdateGold(int gold)
    {
        if (goldText != null) goldText.text = $"Gold: {gold}";
    }

    void UpdateLives(int lives)
    {
        if (livesText != null) livesText.text = $"Lives: {lives}";
    }

    void UpdateRound(int round)
    {
        if (roundText == null) return;
        int total = WaveSpawner.Instance != null ? WaveSpawner.Instance.rounds.Length : 0;
        roundText.text = $"Round: {round + 1}/{total}";
    }

    void UpdateCredits(int credits)
    {
        if (creditsText != null) creditsText.text = $"Credits: {credits}/240";
    }

    void OnStartRound()
    {
        WaveSpawner.Instance?.StartNextRound();
    }

    void OnPause()
    {
        GameManager.Instance?.SetState(GameState.Paused);
    }
}
