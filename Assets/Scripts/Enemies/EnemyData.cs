using UnityEngine;

public enum EnemyArchetype
{
    Standard,    // basic walker
    Shielded,    // absorbs damage with a shield HP pool; Pierce damage bypasses it
    Stealth,     // invisible to towers unless a detection tower is nearby
    Boss,        // large HP pool, scaled-up visuals, bonus gold, triggers OnBossDefeated
    Splitter,    // on death, spawns N smaller copies (defined by splitInto / splitCount)
    ShieldAura   // periodically grants shield HP to nearby allies
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Graduation/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "ELEC1001 Bug";
    public float moveSpeed = 2f;
    public int maxHealth = 100;
    public int goldReward = 10;

    [Range(1, 4)]
    public int courseTier = 1;

    public Sprite sprite;

    [Header("Archetype")]
    public EnemyArchetype archetype = EnemyArchetype.Standard;

    [Header("Shielded Settings")]
    [Tooltip("Shield HP absorbs all incoming damage until depleted (bypassed by Pierce).")]
    public int shieldHealth = 0;

    [Header("Boss Settings")]
    [Tooltip("Uniform scale multiplier applied to the Boss sprite.")]
    public float bossScale = 2.5f;

    [Header("Splitter Settings")]
    [Tooltip("Enemy template spawned when this enemy dies. Required for Splitter archetype.")]
    public EnemyData splitInto;
    [Range(1, 6)]
    public int splitCount = 2;

    [Header("Shield Aura Settings")]
    [Tooltip("Radius in world units used to find nearby allies to shield.")]
    public float shieldAuraRadius = 2.5f;
    [Tooltip("Shield HP granted to each ally on every aura tick.")]
    public int shieldAuraAmount = 30;
    [Tooltip("Seconds between aura ticks.")]
    public float shieldAuraInterval = 3f;

    // ── Boss / Miniboss abilities ─────────────────────────────────────────
    [Header("Boss Abilities (Bosses & Minibosses only)")]
    [Tooltip("Bitmask of mechanics this enemy uses. Combinable.")]
    public BossAbilityFlags bossAbilities = BossAbilityFlags.None;

    [Tooltip("Seconds between teleport jumps.")]
    public float teleportInterval = 6f;
    [Tooltip("How many waypoints forward the boss jumps each teleport.")]
    public int   teleportSkipWaypoints = 2;

    [Tooltip("HP healed per second by the Regen ability.")]
    public int   regenPerSecond = 5;

    [Tooltip("HP fraction (0..1) at which Enrage triggers (one-shot, permanent).")]
    [Range(0f, 1f)] public float enrageHpThreshold = 0.4f;
    [Tooltip("Speed multiplier applied when Enrage triggers.")]
    public float enrageSpeedMult = 1.7f;

    [Tooltip("Seconds between summon pulses.")]
    public float summonInterval = 8f;
    [Tooltip("Number of minions per summon pulse.")]
    public int   summonCount    = 2;
    [Tooltip("Enemy template used when this enemy summons (e.g. assign basic enemy).")]
    public EnemyData summonTemplate;
}

[System.Flags]
public enum BossAbilityFlags
{
    None     = 0,
    Teleport = 1 << 0,   // jumps forward along its path periodically
    Regen    = 1 << 1,   // regenerates HP each second
    Enrage   = 1 << 2,   // permanent speed boost when low HP
    Summon   = 1 << 3,   // periodically spawns minions at its position
}
