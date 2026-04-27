using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Configuration")]
    public TowerData data;
    public TowerSlot slot;

    [Header("Runtime")]
    public float currentRange;
    public float currentFireRate;
    public int currentDamage;

    protected Transform firePoint;
    protected float fireTimer;
    protected Enemy targetEnemy;

    // Temporary damage boost from EmpowerAllies hero skill
    private float _tempDamageMult  = 1f;
    private float _tempDamageTimer = 0f;

    // Temporary fire-rate boost from AttackSpeedAura hero skill
    private float _tempFireRateMult  = 1f;
    private float _tempFireRateTimer = 0f;

    // Gold aura we registered with CurrencyManager (so we can unregister on death/replace).
    private float _registeredGoldAura = 0f;

    // ── Upgrade tracking ─────────────────────────────────────────────────────
    public int path1Tier = 0;
    public int path2Tier = 0;    /// <summary>0 = none, 1 = capstone1 bought, 2 = capstone2 bought.</summary>
    public int capstoneTier = 0;

    // Capstone-driven runtime modifiers (re-applied after every upgrade).
    int   _extraShotsPerVolley = 0;
    float _bonusSplashRadius   = 0f;
    float _splashFractionScale = 1f;
    float _slowMultiplierScale = 1f;
    float _bonusSlowDuration   = 0f;

    // Hero-skill modifiers stacked from upgrades. HeroTower reads these via
    // the public getters below to compute its effective skill values.
    float _skillCooldownMul   = 1f;
    float _skillEffectMul     = 1f;
    float _skillRadiusBonus   = 0f;
    float _skillDurationBonus = 0f;
    public float SkillCooldownMul   => _skillCooldownMul;
    public float SkillEffectMul     => _skillEffectMul;
    public float SkillRadiusBonus   => _skillRadiusBonus;
    public float SkillDurationBonus => _skillDurationBonus;
    public virtual void Initialize(TowerData towerData, TowerSlot towerSlot)
    {
        data = towerData;
        slot = towerSlot;

        // Apply base stats (run buffs applied on top)
        currentRange = data.range;
        currentFireRate = data.fireRate;
        currentDamage = data.damage;

        ApplyRunBuffs();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.WhiteSquare;
            if (data.sprite == null) sr.color = GetTowerColor(data.towerType);
            sr.sortingOrder = 4;
        }

        firePoint = transform.Find("FirePoint");
        if (firePoint == null)
            firePoint = transform;

        // Heroes (towers with a HeroSkillData) get an active-skill component automatically.
        if (data.heroSkill != null && GetComponent<HeroTower>() == null)
        {
            HeroTower hero = gameObject.AddComponent<HeroTower>();
            hero.InitializeHero(data.heroSkill);
        }

        // Register passive gold aura with CurrencyManager.
        if (data.goldGainAura > 0f)
        {
            _registeredGoldAura = data.goldGainAura;
            CurrencyManager.RegisterAura(_registeredGoldAura);
        }
    }

    void OnDestroy()
    {
        if (_registeredGoldAura > 0f)
            CurrencyManager.UnregisterAura(_registeredGoldAura);
    }

    /// <summary>Buy an upgrade on path 1 or 2. Returns false if the next tier is unavailable.</summary>
    public bool TryBuyUpgrade(int pathIndex)
    {
        if (data == null) return false;
        TowerUpgrade[] path = pathIndex == 1 ? data.path1Upgrades : data.path2Upgrades;
        int tier            = pathIndex == 1 ? path1Tier         : path2Tier;
        if (path == null || tier >= path.Length) return false;

        TowerUpgrade up = path[tier];
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGold(up.cost)) return false;

        ApplyUpgradeStats(up);

        if (pathIndex == 1) path1Tier++;
        else                path2Tier++;

        // Auto-evolve when a path reaches its evolution threshold.
        TowerData evo  = pathIndex == 1 ? data.path1Evolution : data.path2Evolution;
        int       trig = pathIndex == 1 ? data.path1EvolveAtTier : data.path2EvolveAtTier;
        int       newTier = pathIndex == 1 ? path1Tier : path2Tier;
        if (evo != null && newTier >= trig) EvolveTo(evo);
        return true;
    }

    /// <summary>True when the player has bought enough upgrades to access the
    /// capstone choices and hasn't already locked one in.</summary>
    public bool CanBuyCapstone()
    {
        if (data == null) return false;
        if (capstoneTier != 0) return false;
        return (path1Tier + path2Tier) >= Mathf.Max(1, data.capstoneRequiredTotalTiers);
    }

    /// <summary>Buy capstone 1 (if pathIndex==1) or capstone 2. One per tower.</summary>
    public bool TryBuyCapstone(int pathIndex)
    {
        if (data == null || capstoneTier != 0) return false;
        if ((path1Tier + path2Tier) < Mathf.Max(1, data.capstoneRequiredTotalTiers)) return false;
        TowerUpgrade cap = pathIndex == 1 ? data.capstonePath1 : data.capstonePath2;
        if (cap == null) return false;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGold(cap.cost)) return false;

        ApplyUpgradeStats(cap);
        capstoneTier = pathIndex;
        return true;
    }

    void ApplyUpgradeStats(TowerUpgrade up)
    {
        currentDamage   = Mathf.RoundToInt(currentDamage * up.damageMultiplier) + up.bonusDamage;
        currentRange    = currentRange    * up.rangeMultiplier   + up.bonusRange;
        currentFireRate = currentFireRate * up.fireRateMultiplier + up.bonusFireRate;

        // Capstone-style extras stack additively across upgrades.
        _extraShotsPerVolley += Mathf.Max(0, up.extraShotsPerVolley);
        _bonusSplashRadius   += up.bonusSplashRadius;
        _splashFractionScale *= Mathf.Max(0.01f, up.splashFractionMultiplier);
        _slowMultiplierScale *= Mathf.Max(0.01f, up.slowMultiplierScale);
        _bonusSlowDuration   += up.bonusSlowDuration;

        // Hero-skill mods (no-op for non-hero towers; HeroTower reads them).
        _skillCooldownMul   *= Mathf.Max(0.05f, up.upgradeSkillCooldownMultiplier);
        _skillEffectMul     *= Mathf.Max(0.05f, up.upgradeSkillEffectMultiplier);
        _skillRadiusBonus   += up.upgradeSkillRadiusBonus;
        _skillDurationBonus += up.upgradeSkillDurationBonus;
    }

    /// <summary>Replace this tower's TowerData with an evolved version (preserves slot and upgrade state).</summary>
    public void EvolveTo(TowerData newData)
    {
        if (newData == null) return;
        data = newData;
        currentRange    = newData.range;
        currentFireRate = newData.fireRate;
        currentDamage   = newData.damage;
        ApplyRunBuffs();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = newData.sprite != null ? newData.sprite : RuntimeSprite.WhiteSquare;
            if (newData.sprite == null) sr.color = GetTowerColor(newData.towerType);
        }
    }

    /// <summary>Called by EmpowerAllies to temporarily increase this tower's shot damage.</summary>
    public void ApplyTemporaryDamageBoost(float multiplier, float duration)
    {
        _tempDamageMult  = multiplier;
        _tempDamageTimer = duration;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) StartCoroutine(EmpowerFlash(sr));
    }

    /// <summary>Called by AttackSpeedAura to temporarily increase this tower's fire rate.</summary>
    public void ApplyTemporaryFireRateBoost(float multiplier, float duration)
    {
        _tempFireRateMult  = Mathf.Max(0.1f, multiplier);
        _tempFireRateTimer = duration;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) StartCoroutine(FireRateFlash(sr, duration));
    }

    System.Collections.IEnumerator FireRateFlash(SpriteRenderer sr, float duration)
    {
        Color original = sr.color;
        sr.color = new Color(0.95f, 0.55f, 0.10f);
        yield return new WaitForSeconds(duration);
        if (sr != null) sr.color = original;
    }

    System.Collections.IEnumerator EmpowerFlash(SpriteRenderer sr)
    {
        Color original = sr.color;
        sr.color = new Color(1f, 0.85f, 0.2f);
        yield return new WaitForSeconds(_tempDamageTimer);
        if (sr != null) sr.color = original;
    }

    public static Color GetTowerColor(TowerType type)
    {
        switch (type)
        {
            case TowerType.Rapid:    return new Color(0.2f, 0.8f, 0.9f); // cyan
            case TowerType.Balanced: return new Color(0.3f, 0.6f, 1.0f); // blue
            case TowerType.Sniper:   return new Color(0.8f, 0.3f, 0.8f); // purple
            case TowerType.Professor:return new Color(1.0f, 0.7f, 0.1f); // gold
            case TowerType.Cannon:   return new Color(0.95f, 0.45f, 0.1f); // orange
            case TowerType.Frost:    return new Color(0.55f, 0.85f, 1.0f); // ice-blue
            default:                 return Color.white;
        }
    }

    void ApplyRunBuffs()
    {
        BuffEffect total = new BuffEffect();
        if (RunBuffs.Instance != null)        total.AddBuff(RunBuffs.Instance.stats);
        currentDamage   = Mathf.RoundToInt(data.damage * total.damageMultiplier);
        currentRange    = data.range    * total.rangeMultiplier;
        currentFireRate = data.fireRate * total.fireRateMultiplier;
    }

    /// <summary>Re-apply run-buff stats to every tower currently
    /// placed. Called by RunBuffs.Apply when a marathon buff is picked.</summary>
    public static void RefreshAllStats()
    {
        Tower[] all = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (Tower t in all) if (t != null) t.ApplyRunBuffs();
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        // Tick temporary damage boost timer
        if (_tempDamageTimer > 0f)
        {
            _tempDamageTimer -= Time.deltaTime;
            if (_tempDamageTimer <= 0f)
                _tempDamageMult = 1f;
        }

        // Tick temporary fire-rate boost timer
        if (_tempFireRateTimer > 0f)
        {
            _tempFireRateTimer -= Time.deltaTime;
            if (_tempFireRateTimer <= 0f)
                _tempFireRateMult = 1f;
        }

        // Detection towers reveal / conceal stealth enemies every frame
        if (data != null && data.hasDetection)
            HandleDetection();

        if (targetEnemy == null || !IsInRange(targetEnemy) || !targetEnemy.IsTargetable())
        {
            targetEnemy = FindClosestEnemy();
        }

        if (targetEnemy != null && fireTimer >= 1f / (currentFireRate * _tempFireRateMult))
        {
            Shoot();
            fireTimer = 0f;
        }

        RotateTowardsTarget();
    }

    void HandleDetection()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy e in all)
        {
            if (IsInRange(e)) e.Reveal();
            // Note: Conceal() is called by DetectionRevealTracker on the enemy itself
            // when no detection tower is in range — see comment in Enemy.cs.
        }
    }

    protected virtual void Shoot()
    {
        if (data.projectilePrefab == null || targetEnemy == null) return;

        int boostedDamage = Mathf.RoundToInt(currentDamage * _tempDamageMult);
        FireProjectile(targetEnemy, boostedDamage);

        // Capstone "fires twice" / multi-shot: spawn extra projectiles. They
        // pick the next-best targets so a single tank doesn't soak all shots.
        if (_extraShotsPerVolley > 0)
        {
            Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            for (int n = 0; n < _extraShotsPerVolley; n++)
            {
                Enemy alt = FindNextTarget(all, targetEnemy, n + 1);
                if (alt != null) FireProjectile(alt, boostedDamage);
                else             FireProjectile(targetEnemy, boostedDamage);
            }
        }
    }

    void FireProjectile(Enemy target, int dmg)
    {
        GameObject projObj = Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj != null)
        {
            float effSplash = data.splashRadius + _bonusSplashRadius;
            float effFrac   = Mathf.Clamp01(data.splashDamageFraction * _splashFractionScale);
            float effSlow   = Mathf.Clamp(data.slowOnHitMultiplier * _slowMultiplierScale, 0.05f, 1f);
            float effDur    = data.slowOnHitDuration + _bonusSlowDuration;
            proj.Initialize(target, dmg, data.damageType,
                            effSplash, effFrac,
                            effSlow, effDur,
                            data.arcHeight, data.projectileSpriteOverride);
        }
    }

    Enemy FindNextTarget(Enemy[] all, Enemy exclude, int skip)
    {
        // Pick the Nth closest enemy in range, skipping the primary target.
        Enemy best = null; float bestDist = float.MaxValue; int seen = 0;
        // Two-pass: collect in-range, then sort-ish by linear scan.
        for (int i = 0; i < all.Length; i++)
        {
            Enemy e = all[i];
            if (e == null || e == exclude || !e.IsTargetable()) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d > currentRange) continue;
            if (d < bestDist) { bestDist = d; best = e; seen++; }
        }
        return best;
    }

    Enemy FindClosestEnemy()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy closest = null;
        float closestDist = float.MaxValue;

        foreach (Enemy e in enemies)
        {
            if (!e.IsTargetable()) continue;   // skip unrevealed Stealth enemies
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist <= currentRange && dist < closestDist)
            {
                closestDist = dist;
                closest = e;
            }
        }
        return closest;
    }

    bool IsInRange(Enemy enemy)
    {
        if (enemy == null) return false;
        return Vector3.Distance(transform.position, enemy.transform.position) <= currentRange;
    }

    void RotateTowardsTarget()
    {
        if (targetEnemy == null) return;
        Vector3 dir = targetEnemy.transform.position - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        float r = data != null ? data.range : currentRange;
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
