using UnityEngine;

/// <summary>
/// Aggregator for in-run BuffEffects granted by the marathon-mode buff
/// selection screens. Towers query this on top of <see cref="SkillTreeManager"/>.
/// Reset every time the gameplay scene loads.
/// </summary>
public class RunBuffs : MonoBehaviour
{
    public static RunBuffs Instance { get; private set; }

    public BuffEffect stats = new BuffEffect
    {
        damageMultiplier   = 1f,
        rangeMultiplier    = 1f,
        fireRateMultiplier = 1f,
    };

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

    public void Reset()
    {
        stats = new BuffEffect
        {
            damageMultiplier   = 1f,
            rangeMultiplier    = 1f,
            fireRateMultiplier = 1f,
        };
    }

    public void Apply(BuffEffect b)
    {
        // Multipliers compound; flat bonuses sum.
        stats.damageMultiplier   *= Mathf.Max(0.01f, b.damageMultiplier   == 0 ? 1f : b.damageMultiplier);
        stats.rangeMultiplier    *= Mathf.Max(0.01f, b.rangeMultiplier    == 0 ? 1f : b.rangeMultiplier);
        stats.fireRateMultiplier *= Mathf.Max(0.01f, b.fireRateMultiplier == 0 ? 1f : b.fireRateMultiplier);
        stats.bonusLives        += b.bonusLives;
        stats.bonusStartGold    += b.bonusStartGold;
        stats.bonusGoldPerRound += b.bonusGoldPerRound;

        // Push fresh stats to all already-placed towers.
        Tower.RefreshAllStats();
    }
}
