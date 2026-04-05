using UnityEngine;
using System;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Gold (resets each level)")]
    [SerializeField] private int gold = 100;

    public int Gold => gold;
    public static event Action<int> OnGoldChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetStartingGold(int amount)
    {
        gold = amount;

        // Apply skill tree bonus
        if (SkillTreeManager.Instance != null)
        {
            BuffEffect buffs = SkillTreeManager.Instance.GetTotalBuffs();
            gold += buffs.bonusStartGold;
        }

        OnGoldChanged?.Invoke(gold);
    }

    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke(gold);
    }

    public bool CanAfford(int cost)
    {
        return gold >= cost;
    }

    public bool SpendGold(int cost)
    {
        if (!CanAfford(cost)) return false;
        gold -= cost;
        OnGoldChanged?.Invoke(gold);
        return true;
    }
}
