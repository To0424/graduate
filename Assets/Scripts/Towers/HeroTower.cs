using UnityEngine;
using System;

/// <summary>
/// Automatically added to a Tower's GameObject when that Tower is a Professor type
/// with a HeroSkillData assigned.  Handles active-skill cooldowns, execution, and
/// the brief bullet-time feedback on activation.
///
/// This is a standalone MonoBehaviour (not a Tower subclass) so it can be
/// dropped onto the existing prefab without modifying the Tower component.
/// </summary>
[RequireComponent(typeof(Tower))]
public class HeroTower : MonoBehaviour
{
    // ── Public API ────────────────────────────────────────────────────────────

    public HeroSkillData skillData    { get; private set; }
    public Tower         tower        { get; private set; }

    /// 0 = ready, 1 = cooldown just started
    public float CooldownFraction => skillData != null && skillData.cooldown > 0f
        ? Mathf.Clamp01(_cooldownTimer / skillData.cooldown)
        : 0f;

    public bool SkillReady => _cooldownTimer <= 0f;

    // ── Events ────────────────────────────────────────────────────────────────

    public static event Action<HeroTower> OnHeroRegistered;
    public static event Action<HeroTower> OnHeroUnregistered;
    public static event Action<HeroTower> OnSkillActivated;

    // ── Private state ─────────────────────────────────────────────────────────

    private float _cooldownTimer  = 0f;

    // Bullet-time
    private float _bulletTimeTimer    = 0f;
    private const float k_BTDuration  = 0.35f;
    private const float k_BTScale     = 0.15f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void InitializeHero(HeroSkillData skill)
    {
        skillData      = skill;
        tower          = GetComponent<Tower>();
        _cooldownTimer = 0f;
        OnHeroRegistered?.Invoke(this);
    }

    void OnDestroy()
    {
        // Restore time scale if destroyed mid-bullet-time
        if (_bulletTimeTimer > 0f) Time.timeScale = 1f;
        OnHeroUnregistered?.Invoke(this);
    }

    void Update()
    {
        // Tick bullet-time (uses unscaled time so it always counts down)
        if (_bulletTimeTimer > 0f)
        {
            _bulletTimeTimer -= Time.unscaledDeltaTime;
            if (_bulletTimeTimer <= 0f)
                Time.timeScale = 1f;
        }

        // Tick skill cooldown (unscaled so it still progresses during bullet-time),
        // but PAUSE it during the preparation phase between rounds so heroes don't
        // get a free recharge while the player is still building.
        if (_cooldownTimer > 0f && IsRoundInProgress())
            _cooldownTimer -= Time.unscaledDeltaTime;
    }

    static bool IsRoundInProgress()
    {
        var ws = WaveSpawner.Instance;
        if (ws == null) return true;          // no wave system = always-on (e.g. menus)
        return ws.isSpawning || ws.enemiesAlive > 0;
    }

    /// <summary>True when the hero's skill is a placeable AOE (i.e. the player
    /// picks a target position on the map). Used by the hero-card UI to enable
    /// drag-to-deploy.</summary>
    public bool IsPlaceableAOE
    {
        get
        {
            if (skillData == null) return false;
            switch (skillData.effect)
            {
                case HeroSkillEffect.AoEBlast:
                case HeroSkillEffect.SlowField:
                case HeroSkillEffect.EmpowerAllies:
                case HeroSkillEffect.GroundTargetedAOE:
                    return true;
                default:
                    return false;
            }
        }
    }

    // ── Skill activation ──────────────────────────────────────────────────────

    public void ActivateSkill()
    {
        if (!SkillReady || skillData == null) return;

        // All AOE-style skills are placed at a player-chosen position now.
        if (IsPlaceableAOE)
        {
            BeginSkillTargeting();
            return;
        }

        // Non-placeable skills (e.g. RestoreLives) fire instantly.
        _cooldownTimer = skillData.cooldown;
        OnSkillActivated?.Invoke(this);
        Time.timeScale   = k_BTScale;
        _bulletTimeTimer = k_BTDuration;
        ExecuteSkillAt(transform.position);
    }

    /// <summary>Begin the targeting reticle for this hero's AOE.
    /// Returns false if the skill is not ready or not placeable.</summary>
    public bool BeginSkillTargeting()
    {
        if (!SkillReady || skillData == null || !IsPlaceableAOE) return false;
        SkillTargetingController.EnsureExists();
        SkillTargetingController.Instance.BeginTargeting(
            Mathf.Max(0.5f, skillData.radius),
            ReticleColorFor(skillData.effect),
            onSelected: pos =>
            {
                _cooldownTimer = skillData.cooldown;
                OnSkillActivated?.Invoke(this);
                Time.timeScale   = k_BTScale;
                _bulletTimeTimer = k_BTDuration;
                ExecuteSkillAt(pos);
            });
        return true;
    }

    static Color ReticleColorFor(HeroSkillEffect effect)
    {
        switch (effect)
        {
            case HeroSkillEffect.AoEBlast:           return new Color(1f, 0.45f, 0.1f, 1f);
            case HeroSkillEffect.SlowField:          return new Color(0.3f, 0.7f, 1f, 1f);
            case HeroSkillEffect.EmpowerAllies:      return new Color(1f, 0.85f, 0.2f, 1f);
            case HeroSkillEffect.GroundTargetedAOE:  return new Color(1f, 0.45f, 0.1f, 1f);
            default:                                 return Color.white;
        }
    }

    void ExecuteSkillAt(Vector3 worldPos)
    {
        switch (skillData.effect)
        {
            case HeroSkillEffect.AoEBlast:          DoAoEBlast(worldPos);      break;
            case HeroSkillEffect.SlowField:         DoSlowField(worldPos);     break;
            case HeroSkillEffect.EmpowerAllies:     DoEmpowerAllies(worldPos); break;
            case HeroSkillEffect.GroundTargetedAOE: DamagingZone.Spawn(worldPos, skillData); break;
            case HeroSkillEffect.RestoreLives:      DoRestoreLives();          break;
        }
        FlashSelf(ReticleColorFor(skillData.effect));
    }

    // ── AoE Blast ─────────────────────────────────────────────────────────────

    void DoAoEBlast(Vector3 center)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy e in all)
        {
            if (Vector3.Distance(center, e.transform.position) <= skillData.radius)
                e.TakeDamage(skillData.blastDamage, DamageType.Pierce); // blast ignores shields
        }
    }

    // ── Slow Field ────────────────────────────────────────────────────────────

    void DoSlowField(Vector3 center)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy e in all)
        {
            if (Vector3.Distance(center, e.transform.position) <= skillData.radius)
                e.ApplySlow(skillData.slowMultiplier, skillData.slowDuration);
        }
    }

    // ── Empower Allies (now AOE-only) ─────────────────────────────────────────

    void DoEmpowerAllies(Vector3 center)
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        float r = Mathf.Max(0.1f, skillData.radius);
        foreach (Tower t in towers)
        {
            if (Vector3.Distance(center, t.transform.position) <= r)
                t.ApplyTemporaryDamageBoost(skillData.empowerMultiplier, skillData.empowerDuration);
        }
    }

    // ── Restore Lives ─────────────────────────────────────────────────────────

    void DoRestoreLives()
    {
        LivesManager.Instance?.RestoreLives(skillData.livesRestored);
    }

    // ── Visual ───────────────────────────────────────────────────────────────

    void FlashSelf(Color color)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) StartCoroutine(FlashRoutine(sr, color));
    }

    System.Collections.IEnumerator FlashRoutine(SpriteRenderer sr, Color flashColor)
    {
        Color original = sr.color;
        sr.color = flashColor;
        yield return new WaitForSecondsRealtime(k_BTDuration);
        if (sr != null) sr.color = original;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (skillData == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, skillData.radius);
    }
}
