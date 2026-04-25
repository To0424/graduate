using System;
using UnityEngine;

public enum BuffRarity { Common, Rare, Epic, Hero }

public enum BuffOfferKind
{
    StatBuff,        // adds to RunBuffs.stats — applies to all towers
    UnlockTower,     // adds towerToUnlock to availableTowers (regular)
    UnlockHero,      // adds towerToUnlock to availableTowers (hero / professor)
    BonusGold,       // immediate gold reward
    BonusLife        // immediate +max life
}

/// <summary>
/// One option presented to the player on a marathon buff-selection screen.
/// Created at run start by the bootstrap from a fixed catalog (see
/// QuickTestBootstrap.MakeBuffPool).
/// </summary>
[Serializable]
public class BuffOffer
{
    public string title;
    public string description;
    public BuffRarity rarity = BuffRarity.Common;
    public BuffOfferKind kind = BuffOfferKind.StatBuff;
    public Color accentColor = new Color(0.7f, 0.7f, 0.7f);

    // StatBuff payload (applied as multiplicative + additive to RunBuffs).
    public BuffEffect statBuff;

    // UnlockTower / UnlockHero payload.
    public TowerData towerToUnlock;

    // BonusGold / BonusLife payload.
    public int amount;

    public Color RarityColor()
    {
        switch (rarity)
        {
            case BuffRarity.Common: return new Color(0.7f, 0.7f, 0.7f);
            case BuffRarity.Rare:   return new Color(0.45f, 0.7f, 1f);
            case BuffRarity.Epic:   return new Color(0.85f, 0.45f, 1f);
            case BuffRarity.Hero:   return new Color(1f, 0.78f, 0.2f);
            default: return Color.white;
        }
    }

    public string RarityLabel()
    {
        switch (rarity)
        {
            case BuffRarity.Common: return "COMMON";
            case BuffRarity.Rare:   return "RARE";
            case BuffRarity.Epic:   return "EPIC";
            case BuffRarity.Hero:   return "HERO";
            default: return "";
        }
    }
}
