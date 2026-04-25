using UnityEngine;

public enum HeroSkillEffect
{
    AoEBlast,             // instant damage to every enemy inside radius around the hero
    SlowField,            // slows all enemies inside radius for a duration
    EmpowerAllies,        // temporarily boosts damage of all placed towers
    RestoreLives,         // restores lost lives
    GroundTargetedAOE     // player picks a world position, then a DoT zone is placed there
}

[CreateAssetMenu(fileName = "NewHeroSkill", menuName = "Graduation/Hero Skill")]
public class HeroSkillData : ScriptableObject
{
    public string skillName   = "Lecture Burst";
    public string description = "Deals damage to nearby enemies.";
    public float  cooldown    = 12f;
    public HeroSkillEffect effect;

    [Header("AoE Blast / Slow Field")]
    [Tooltip("Radius around the hero in which the skill takes effect.")]
    public float radius = 4f;

    [Header("AoE Blast")]
    public int blastDamage = 80;

    [Header("Slow Field")]
    [Tooltip("Speed multiplier applied to hit enemies. 0.4 = 40% of normal speed.")]
    public float slowMultiplier = 0.4f;
    public float slowDuration   = 3.5f;

    [Header("Empower Allies")]
    [Tooltip("Damage multiplier applied to all towers during the buff window.")]
    public float empowerMultiplier = 1.75f;
    public float empowerDuration   = 8f;

    [Header("Restore Lives")]
    public int livesRestored = 2;

    [Header("Ground-Targeted AOE (DoT zone)")]
    [Tooltip("Damage per tick applied to every enemy inside the placed AOE.")]
    public int   aoeDamagePerTick = 12;
    [Tooltip("Seconds between damage ticks.")]
    public float aoeTickInterval  = 0.4f;
    [Tooltip("Total lifetime of the AOE in seconds.")]
    public float aoeDuration      = 3f;
    [Tooltip("Damage type dealt by each tick.")]
    public DamageType aoeDamageType = DamageType.Pierce;
}
