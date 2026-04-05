using UnityEngine;
using System;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance { get; private set; }

    [SerializeField] private int lives = 20;

    public int Lives => lives;
    public static event Action<int> OnLivesChanged;
    public static event Action OnAllLivesLost;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetStartingLives(int amount)
    {
        lives = amount;

        if (SkillTreeManager.Instance != null)
        {
            BuffEffect buffs = SkillTreeManager.Instance.GetTotalBuffs();
            lives += buffs.bonusLives;
        }

        OnLivesChanged?.Invoke(lives);
    }

    public void LoseLife(int amount)
    {
        lives -= amount;
        OnLivesChanged?.Invoke(lives);

        if (lives <= 0)
        {
            lives = 0;
            OnAllLivesLost?.Invoke();
        }
    }
}
