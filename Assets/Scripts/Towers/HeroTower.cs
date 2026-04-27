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
    public float CooldownFraction => skillData != null && EffectiveCooldown > 0f
        ? Mathf.Clamp01(_cooldownTimer / EffectiveCooldown)
        : 0f;

    public bool SkillReady => _cooldownTimer <= 0f && !IsLockedOncePerGame;

    /// <summary>True when the skill is once-per-game and has already been spent this run.</summary>
    public bool IsLockedOncePerGame =>
        skillData != null && skillData.oncePerGame && _usedOncePerGame.Contains(skillData);

    // ── Effective skill values (applied after tower upgrades) ────────────────

    float CooldownMul   => tower != null ? tower.SkillCooldownMul   : 1f;
    float EffectScale   => tower != null ? tower.SkillEffectMul     : 1f;
    float RadiusBonus   => tower != null ? tower.SkillRadiusBonus   : 0f;
    float DurationBonus => tower != null ? tower.SkillDurationBonus : 0f;

    public float EffectiveCooldown => skillData != null ? skillData.cooldown * CooldownMul : 0f;
    public float EffectiveRadius   => skillData != null ? Mathf.Max(0.1f, skillData.radius + RadiusBonus) : 0f;

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

    // Once-per-game skill tracking. Reset by ResetOncePerGameUsage() at level
    // start (called from GameplayAutoSetup).
    private static readonly System.Collections.Generic.HashSet<HeroSkillData> _usedOncePerGame
        = new System.Collections.Generic.HashSet<HeroSkillData>();
    public static void ResetOncePerGameUsage() { _usedOncePerGame.Clear(); }

    // Cached audio clips so we don't hit Resources.Load every cast.
    private static readonly System.Collections.Generic.Dictionary<string, AudioClip> _clipCache
        = new System.Collections.Generic.Dictionary<string, AudioClip>();

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
                case HeroSkillEffect.PullToCenter:
                case HeroSkillEffect.AttackSpeedAura:
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

        // Non-placeable skills (e.g. RestoreLives, SkipRound) fire instantly.
        StartCooldown();
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
            Mathf.Max(0.5f, EffectiveRadius),
            ReticleColorFor(skillData.effect),
            onSelected: pos =>
            {
                StartCooldown();
                OnSkillActivated?.Invoke(this);
                Time.timeScale   = k_BTScale;
                _bulletTimeTimer = k_BTDuration;
                ExecuteSkillAt(pos);
            });
        return true;
    }

    void StartCooldown()
    {
        _cooldownTimer = EffectiveCooldown;
        if (skillData != null && skillData.oncePerGame)
            _usedOncePerGame.Add(skillData);
    }

    static Color ReticleColorFor(HeroSkillEffect effect)
    {
        switch (effect)
        {
            case HeroSkillEffect.AoEBlast:           return new Color(1f, 0.45f, 0.1f, 1f);
            case HeroSkillEffect.SlowField:          return new Color(0.3f, 0.7f, 1f, 1f);
            case HeroSkillEffect.EmpowerAllies:      return new Color(1f, 0.85f, 0.2f, 1f);
            case HeroSkillEffect.GroundTargetedAOE:  return new Color(1f, 0.45f, 0.1f, 1f);
            case HeroSkillEffect.PullToCenter:       return new Color(0.55f, 0.25f, 0.95f, 1f);
            case HeroSkillEffect.AttackSpeedAura:    return new Color(0.95f, 0.55f, 0.10f, 1f);
            case HeroSkillEffect.SkipRound:          return new Color(0.95f, 0.10f, 0.10f, 1f);
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
            case HeroSkillEffect.SkipRound:         DoSkipRound();             break;
            case HeroSkillEffect.PullToCenter:      DoPullToCenter(worldPos);  break;
            case HeroSkillEffect.AttackSpeedAura:   DoAttackSpeedAura(worldPos); break;
        }
        PlaySkillSound();
        FlashSelf(ReticleColorFor(skillData.effect));
    }

    void PlaySkillSound()
    {
        if (skillData == null || string.IsNullOrEmpty(skillData.audioClipResource)) return;
        if (!_clipCache.TryGetValue(skillData.audioClipResource, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(skillData.audioClipResource);
            _clipCache[skillData.audioClipResource] = clip;
        }
        if (clip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, skillData.audioVolume);
    }

    // ── AoE Blast ─────────────────────────────────────────────────────────────

    void DoAoEBlast(Vector3 center)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        int dmg = Mathf.Max(1, Mathf.RoundToInt(skillData.blastDamage * EffectScale));
        float r = EffectiveRadius;
        foreach (Enemy e in all)
        {
            if (Vector3.Distance(center, e.transform.position) <= r)
                e.TakeDamage(dmg, DamageType.Pierce); // blast ignores shields
        }
    }

    // ── Slow Field ────────────────────────────────────────────────────────────

    void DoSlowField(Vector3 center)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        // Stronger EffectScale -> deeper slow (multiplier shrinks).
        float mult = Mathf.Clamp(skillData.slowMultiplier / Mathf.Max(0.1f, EffectScale), 0.05f, 1f);
        float dur  = skillData.slowDuration + DurationBonus;
        float r    = EffectiveRadius;
        foreach (Enemy e in all)
        {
            if (Vector3.Distance(center, e.transform.position) <= r)
                e.ApplySlow(mult, dur);
        }
    }

    // ── Empower Allies (now AOE-only) ─────────────────────────────────────────

    void DoEmpowerAllies(Vector3 center)
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        float r   = Mathf.Max(0.1f, EffectiveRadius);
        float mul = 1f + (skillData.empowerMultiplier - 1f) * EffectScale;
        float dur = skillData.empowerDuration + DurationBonus;
        foreach (Tower t in towers)
        {
            if (Vector3.Distance(center, t.transform.position) <= r)
                t.ApplyTemporaryDamageBoost(mul, dur);
        }
    }

    // ── Restore Lives ─────────────────────────────────────────────────────────

    void DoRestoreLives()
    {
        LivesManager.Instance?.RestoreLives(skillData.livesRestored);
    }

    // ── Skip Round ────────────────────────────────────────────────────────────

    void DoSkipRound()
    {
        var ws = WaveSpawner.Instance;
        if (ws != null) ws.SkipCurrentRound();
    }

    // ── Pull To Center ────────────────────────────────────────────────────────

    void DoPullToCenter(Vector3 center)
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        float r        = EffectiveRadius;
        float strength = skillData.pullStrength * EffectScale;
        float dur      = skillData.pullDuration + DurationBonus;
        foreach (Enemy e in all)
        {
            if (Vector3.Distance(center, e.transform.position) <= r)
                e.ApplyPull(center, strength, dur);
        }
    }

    // ── Attack Speed Aura ─────────────────────────────────────────────────────

    void DoAttackSpeedAura(Vector3 center)
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        float r   = Mathf.Max(0.1f, EffectiveRadius);
        float mul = 1f + (skillData.attackSpeedMultiplier - 1f) * EffectScale;
        float dur = skillData.attackSpeedDuration + DurationBonus;
        foreach (Tower t in towers)
        {
            if (Vector3.Distance(center, t.transform.position) <= r)
                t.ApplyTemporaryFireRateBoost(mul, dur);
        }
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
        Gizmos.DrawWireSphere(transform.position, EffectiveRadius);
    }
}
