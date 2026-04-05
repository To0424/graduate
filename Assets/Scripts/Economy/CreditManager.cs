using UnityEngine;
using System;

public class CreditManager : MonoBehaviour
{
    public static CreditManager Instance { get; private set; }

    private const int CREDITS_TO_GRADUATE = 240;

    [SerializeField] private int totalCredits = 0;

    public int TotalCredits => totalCredits;
    public int CreditsToGraduate => CREDITS_TO_GRADUATE;

    public static event Action<int> OnCreditsChanged;
    public static event Action OnGraduated;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddCredits(int amount)
    {
        totalCredits += amount;
        OnCreditsChanged?.Invoke(totalCredits);

        if (totalCredits >= CREDITS_TO_GRADUATE)
        {
            OnGraduated?.Invoke();
        }

        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetInt("TotalCredits", totalCredits);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        totalCredits = PlayerPrefs.GetInt("TotalCredits", 0);
        OnCreditsChanged?.Invoke(totalCredits);
    }

    public void ResetCredits()
    {
        totalCredits = 0;
        PlayerPrefs.DeleteKey("TotalCredits");
        OnCreditsChanged?.Invoke(totalCredits);
    }
}
