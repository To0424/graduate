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
}
