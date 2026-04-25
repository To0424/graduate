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
    public Sprite sprite;
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
    [Tooltip("Speed multiplier applied to enemies hit (and splashed). 1 = no slow, 0.5 = half speed.")]
    [Range(0.1f, 1f)] public float slowOnHitMultiplier = 1f;
    [Tooltip("Duration in seconds of the on-hit slow.")]
    public float slowOnHitDuration = 0f;

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
}
