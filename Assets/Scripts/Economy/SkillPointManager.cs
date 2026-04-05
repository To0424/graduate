using UnityEngine;
using System;

public class SkillPointManager : MonoBehaviour
{
    public static SkillPointManager Instance { get; private set; }

    [SerializeField] private int skillPoints = 0;

    public int SkillPoints => skillPoints;
    public static event Action<int> OnSkillPointsChanged;

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

    public void AddSkillPoints(int amount)
    {
        skillPoints += amount;
        OnSkillPointsChanged?.Invoke(skillPoints);
        Save();
    }

    public bool SpendSkillPoints(int cost)
    {
        if (skillPoints < cost) return false;
        skillPoints -= cost;
        OnSkillPointsChanged?.Invoke(skillPoints);
        Save();
        return true;
    }

    public void Save()
    {
        PlayerPrefs.SetInt("SkillPoints", skillPoints);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        skillPoints = PlayerPrefs.GetInt("SkillPoints", 0);
        OnSkillPointsChanged?.Invoke(skillPoints);
    }

    public void ResetSkillPoints()
    {
        skillPoints = 0;
        PlayerPrefs.DeleteKey("SkillPoints");
        OnSkillPointsChanged?.Invoke(skillPoints);
    }
}
