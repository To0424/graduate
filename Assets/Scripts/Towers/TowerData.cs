using UnityEngine;

public enum TowerType
{
    Rapid,
    Balanced,
    Sniper,
    Professor,
    Cannon,    // AOE splash damage on hit
    Frost      // small splash + slows enemies on hit
}

public enum DamageType
{
    Normal,   // blocked by Shielded enemies' shield
    Pierce    // bypasses Shielded shields entirely
}

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Graduation/Tower Data")]
public class TowerData : ScriptableObject
{
    public string towerName;
    public TowerType towerType = TowerType.Balanced;
    public int cost = 50;
    public float range = 3f;
    public float fireRate = 1f;  // shots per second
    public int damage = 25;
    [Tooltip("Sprite shown on the placed tower in the world.")]
    public Sprite sprite;
    [Tooltip("Optional icon shown in the radial build palette. If empty, uses tower sprite.")]
    public Sprite buildMenuIcon;
    public GameObject projectilePrefab;

    [Header("Professor Tower")]
    public bool isProfessorTower = false;
    public string requiredFaculty;  // faculty that must be cleared to unlock
    [Tooltip("If true, only one of this TowerData may be deployed per level (e.g. the player\u2019s Self).")]
    public bool unique = false;

    [Header("Damage Type")]
    [Tooltip("Pierce bypasses Shielded enemy shields.")]
    public DamageType damageType = DamageType.Normal;

    [Header("Detection")]
    [Tooltip("When true, this tower reveals nearby Stealth enemies within its range.")]
    public bool hasDetection = false;

    [Header("Splash / AOE on Hit")]
    [Tooltip("If > 0, projectiles deal damage to all enemies within this radius of the impact.")]
    public float splashRadius = 0f;
    [Tooltip("Damage applied to non-primary targets caught in the splash, as a fraction of base damage.")]
    [Range(0f, 1f)] public float splashDamageFraction = 0.6f;

    [Header("Slow on Hit")]
    [Tooltip("Speed multiplier applied to enemies hit (and splashed). 1 = no slow, 0.5 = half speed, 0 = fully stopped (stun-like).")]
    [Range(0f, 1f)] public float slowOnHitMultiplier = 1f;
    [Tooltip("Duration in seconds of the on-hit slow.")]
    public float slowOnHitDuration = 0f;

    [Header("Gold Aura (passive while deployed)")]
    [Tooltip("While at least one tower with this >0 is alive, every gold gain in the game is multiplied by (1 + sum of auras). 0.25 = +25% gold.")]
    public float goldGainAura = 0f;

    [Header("Projectile Trajectory")]
    [Tooltip("If > 0, the projectile follows a parabolic arc to its target (peak height in world units). Use this for cannon-style lobs. 0 = straight-line homing.")]
    public float arcHeight = 0f;
    [Tooltip("Optional sprite to use for the projectile (overrides the prefab's sprite if assigned). Lets you swap the visual without making a new prefab.")]
    public Sprite projectileSpriteOverride;

    [Header("Hero Skill (Professor only)")]
    [Tooltip("Assign a HeroSkillData asset to make this tower a Hero with an active skill.")]
    public HeroSkillData heroSkill;

    [Header("Upgrade Paths (data-driven)")]
    [Tooltip("Sequential upgrades along path 1. Buy in order from index 0.")]
    public TowerUpgrade[] path1Upgrades;
    [Tooltip("Optional: replace this tower with an evolved TowerData when path1Tier reaches the threshold.")]
    public TowerData path1Evolution;
    public int path1EvolveAtTier = 3;

    [Tooltip("Sequential upgrades along path 2.")]
    public TowerUpgrade[] path2Upgrades;
    public TowerData path2Evolution;
    public int path2EvolveAtTier = 3;

    [Header("Capstone Perks (one-shot, mutually exclusive)")]
    [Tooltip("Total upgrade tiers (path1 + path2) needed before capstones unlock.")]
    public int capstoneRequiredTotalTiers = 5;
    [Tooltip("Capstone perk that complements path 1's identity.")]
    public TowerUpgrade capstonePath1;
    [Tooltip("Capstone perk that complements path 2's identity.")]
    public TowerUpgrade capstonePath2;
}

[System.Serializable]
public class TowerUpgrade
{
    public string upgradeName = "Upgrade";
    [TextArea] public string description = "";
    public int   cost              = 30;
    public float damageMultiplier  = 1f;
    public int   bonusDamage       = 0;
    public float rangeMultiplier   = 1f;
    public float bonusRange        = 0f;
    public float fireRateMultiplier = 1f;
    public float bonusFireRate     = 0f;

    [Header("Capstone-style payloads (optional)")]
    [Tooltip("Extra projectiles fired per volley. 1 = fires twice per shot, etc.")]
    public int   extraShotsPerVolley = 0;
    [Tooltip("Adds to TowerData.splashRadius. Use to grant or grow AOE.")]
    public float bonusSplashRadius   = 0f;
    [Tooltip("Multiplies the splash damage fraction.")]
    public float splashFractionMultiplier = 1f;
    [Tooltip("Multiplies slow strength (lower = stronger slow). 1 = no change.")]
    public float slowMultiplierScale = 1f;
    [Tooltip("Adds to slow duration in seconds.")]
    public float bonusSlowDuration   = 0f;

    [Header("Hero skill modifiers (only meaningful on hero upgrades)")]
    [Tooltip("Multiplies the hero's active-skill cooldown. 0.85 = -15% cooldown. Stacks multiplicatively across upgrades.")]
    public float upgradeSkillCooldownMultiplier = 1f;
    [Tooltip("Multiplies the hero's active-skill effect magnitude (blast damage, DoT, slow strength, pull strength, attack-speed/empower bonus). Stacks multiplicatively.")]
    public float upgradeSkillEffectMultiplier  = 1f;
    [Tooltip("Adds to the hero's active-skill radius. Stacks additively.")]
    public float upgradeSkillRadiusBonus       = 0f;
    [Tooltip("Adds to the hero's active-skill duration. Stacks additively.")]
    public float upgradeSkillDurationBonus     = 0f;
}
