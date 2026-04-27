using UnityEngine;
using System;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Gold (resets each level)")]
    [SerializeField] private int gold = 100;

    public int Gold => gold;
    public static event Action<int> OnGoldChanged;

    /// <summary>
    /// Multiplier applied to every <see cref="AddGold"/> call. Towers with
    /// <c>TowerData.goldGainAura &gt; 0</c> register their aura on Awake and
    /// unregister on OnDestroy via <see cref="RegisterAura"/>. The final
    /// multiplier is <c>1 + sum(auras)</c>, so a single +25% Wilton on the
    /// field grants 1.25x gold from kills.
    /// </summary>
    public static float GlobalGoldMultiplier { get; private set; } = 1f;
    private static float _auraSum = 0f;

    public static void RegisterAura(float bonusFraction)
    {
        if (bonusFraction <= 0f) return;
        _auraSum += bonusFraction;
        GlobalGoldMultiplier = 1f + _auraSum;
    }

    public static void UnregisterAura(float bonusFraction)
    {
        if (bonusFraction <= 0f) return;
        _auraSum = Mathf.Max(0f, _auraSum - bonusFraction);
        GlobalGoldMultiplier = 1f + _auraSum;
    }

    /// <summary>Reset all gold auras (call when a new run starts).</summary>
    public static void ResetAuras()
    {
        _auraSum = 0f;
        GlobalGoldMultiplier = 1f;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetStartingGold(int amount)
    {
        gold = amount;

        OnGoldChanged?.Invoke(gold);
    }

    public void AddGold(int amount)
    {
        if (amount > 0 && GlobalGoldMultiplier != 1f)
            amount = Mathf.RoundToInt(amount * GlobalGoldMultiplier);
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
