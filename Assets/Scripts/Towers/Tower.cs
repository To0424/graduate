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

    // ── Upgrade tracking ─────────────────────────────────────────────────────
    public int path1Tier = 0;
    public int path2Tier = 0;

    public virtual void Initialize(TowerData towerData, TowerSlot towerSlot)
    {
        data = towerData;
        slot = towerSlot;

        // Apply base stats (skill tree buffs applied on top)
        currentRange = data.range;
        currentFireRate = data.fireRate;
        currentDamage = data.damage;

        ApplySkillTreeBuffs();

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

        currentDamage   = Mathf.RoundToInt(currentDamage * up.damageMultiplier) + up.bonusDamage;
        currentRange    = currentRange    * up.rangeMultiplier   + up.bonusRange;
        currentFireRate = currentFireRate * up.fireRateMultiplier + up.bonusFireRate;

        if (pathIndex == 1) path1Tier++;
        else                path2Tier++;

        // Auto-evolve when a path reaches its evolution threshold.
        TowerData evo  = pathIndex == 1 ? data.path1Evolution : data.path2Evolution;
        int       trig = pathIndex == 1 ? data.path1EvolveAtTier : data.path2EvolveAtTier;
        int       newTier = pathIndex == 1 ? path1Tier : path2Tier;
        if (evo != null && newTier >= trig) EvolveTo(evo);
        return true;
    }

    /// <summary>Replace this tower's TowerData with an evolved version (preserves slot and upgrade state).</summary>
    public void EvolveTo(TowerData newData)
    {
        if (newData == null) return;
        data = newData;
        currentRange    = newData.range;
        currentFireRate = newData.fireRate;
        currentDamage   = newData.damage;
        ApplySkillTreeBuffs();

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

    void ApplySkillTreeBuffs()
    {
        BuffEffect total = new BuffEffect();
        if (SkillTreeManager.Instance != null) total.AddBuff(SkillTreeManager.Instance.GetTotalBuffs());
        if (RunBuffs.Instance != null)        total.AddBuff(RunBuffs.Instance.stats);
        currentDamage   = Mathf.RoundToInt(data.damage * total.damageMultiplier);
        currentRange    = data.range    * total.rangeMultiplier;
        currentFireRate = data.fireRate * total.fireRateMultiplier;
    }

    /// <summary>Re-apply skill-tree + run-buff stats to every tower currently
    /// placed. Called by RunBuffs.Apply when a marathon buff is picked.</summary>
    public static void RefreshAllStats()
    {
        Tower[] all = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (Tower t in all) if (t != null) t.ApplySkillTreeBuffs();
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

        // Detection towers reveal / conceal stealth enemies every frame
        if (data != null && data.hasDetection)
            HandleDetection();

        if (targetEnemy == null || !IsInRange(targetEnemy) || !targetEnemy.IsTargetable())
        {
            targetEnemy = FindClosestEnemy();
        }

        if (targetEnemy != null && fireTimer >= 1f / currentFireRate)
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
        GameObject projObj = Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(targetEnemy, boostedDamage, data.damageType,
                            data.splashRadius, data.splashDamageFraction,
                            data.slowOnHitMultiplier, data.slowOnHitDuration);
        }
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
