using UnityEngine;
using System;

public class Enemy : MonoBehaviour
{
    [Header("Runtime Data")]
    public EnemyData data;
    public int currentHealth;
    public float moveSpeed;

    // ── Archetype state ───────────────────────────────────────────────────────
    public int   currentShield { get; private set; }
    public bool  IsRevealed    { get; private set; }   // Stealth: set true by a detection tower
    private bool _isDead;

    // ── Slow state ────────────────────────────────────────────────────────────
    private float _slowMultiplier = 1f;
    private float _slowTimer      = 0f;

    // ── Shield Aura state ─────────────────────────────────────────────────────
    private float _auraTickTimer = 0f;

    // ── Boss-ability state ────────────────────────────────────────────────────
    private float _teleportTimer  = 0f;
    private float _summonTimer    = 0f;
    private float _regenAccum     = 0f;
    private bool  _enrageTriggered = false;

    // ── Path ──────────────────────────────────────────────────────────────────
    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    // ── HP / Shield bar constants ─────────────────────────────────────────────
    private const float BAR_WIDTH    = 0.6f;
    private const float BAR_HEIGHT   = 0.08f;
    private const float HP_BAR_Y     = 0.45f;
    private const float SHIELD_BAR_Y = 0.57f;

    private SpriteRenderer hpBarBg;
    private SpriteRenderer hpBarFill;
    private SpriteRenderer shieldBarBg;
    private SpriteRenderer shieldBarFill;

    // ── Events ────────────────────────────────────────────────────────────────
    public static event Action<Enemy> OnEnemyDeath;
    public static event Action<Enemy> OnEnemyReachedExit;
    public static event Action<Enemy> OnBossDefeated;

    // ── Initialise ────────────────────────────────────────────────────────────

    public void Initialize(EnemyData enemyData, Transform[] path)
    {
        data                  = enemyData;
        currentHealth         = data.maxHealth;
        currentShield         = data.shieldHealth;
        moveSpeed             = data.moveSpeed;
        waypoints             = path;
        currentWaypointIndex  = 0;
        _isDead               = false;
        IsRevealed            = (data.archetype != EnemyArchetype.Stealth);

        ApplyArchetypeVisuals();
        CreateHPBar();
        if ((data.archetype == EnemyArchetype.Shielded && data.shieldHealth > 0) ||
            data.archetype == EnemyArchetype.ShieldAura)
            CreateShieldBar();
        if (data.archetype == EnemyArchetype.Stealth)
            gameObject.AddComponent<DetectionRevealTracker>();

        // Reset aura tick timer for ShieldAura enemies.
        _auraTickTimer = data.archetype == EnemyArchetype.ShieldAura ? data.shieldAuraInterval : 0f;

        // Reset boss-ability timers.
        _teleportTimer   = data.teleportInterval;
        _summonTimer     = data.summonInterval;
        _regenAccum      = 0f;
        _enrageTriggered = false;

        if (waypoints.Length > 0)
            transform.position = waypoints[0].position;
    }

    void ApplyArchetypeVisuals()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // Per-enemy animator override: lets each EnemyData supply its own
        // animation clips so different enemy types look different.
        var animComp = GetComponent<Animator>();
        if (animComp != null && data.animatorController != null)
            animComp.runtimeAnimatorController = data.animatorController;

        // If this prefab has an Animator driving the SpriteRenderer, don't
        // overwrite its sprite or tint — let the animation play as authored.
        bool hasAnimator = animComp != null && animComp.runtimeAnimatorController != null;
        if (hasAnimator)
        {
            sr.color = Color.white;
            // Boss still needs scale + label even when animated.
            if (data.archetype == EnemyArchetype.Boss)
            {
                transform.localScale = Vector3.one * data.bossScale;
                CreateBossLabel();
            }
            else if (data.archetype == EnemyArchetype.ShieldAura)
            {
                CreateAuraRing();
            }
            return;
        }

        sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.Circle;

        switch (data.archetype)
        {
            case EnemyArchetype.Standard:
                if (data.sprite == null) sr.color = Color.red;
                break;

            case EnemyArchetype.Shielded:
                if (data.sprite == null) sr.color = new Color(0.3f, 0.6f, 1f);  // blue
                break;

            case EnemyArchetype.Stealth:
                // Start invisible; alpha raised when revealed by a detection tower
                if (data.sprite == null) sr.color = new Color(0.7f, 0.3f, 0.9f, 0.15f);
                else sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.15f);
                break;

            case EnemyArchetype.Boss:
                if (data.sprite == null) sr.color = new Color(0.9f, 0.15f, 0.15f);
                transform.localScale = Vector3.one * data.bossScale;
                CreateBossLabel();
                break;

            case EnemyArchetype.Splitter:
                // Distinct yellow-orange so the player can plan splash hits.
                if (data.sprite == null) sr.color = new Color(1f, 0.65f, 0.15f);
                break;

            case EnemyArchetype.ShieldAura:
                // Greenish-blue: support unit telegraphing the aura.
                if (data.sprite == null) sr.color = new Color(0.4f, 0.85f, 0.7f);
                CreateAuraRing();
                break;
        }
    }

    void CreateAuraRing()
    {
        // A faint ring drawn around the aura unit so the player can see its range.
        GameObject ring = new GameObject("AuraRing");
        ring.transform.SetParent(transform);
        ring.transform.localPosition = Vector3.zero;
        SpriteRenderer rsr = ring.AddComponent<SpriteRenderer>();
        rsr.sprite = RuntimeSprite.Circle;
        rsr.color = new Color(0.4f, 0.85f, 0.7f, 0.18f);
        rsr.sortingOrder = 0;
        ring.transform.localScale = Vector3.one * data.shieldAuraRadius * 2f;
    }

    void CreateBossLabel()
    {
        // A simple world-space text mesh placed above the enemy
        GameObject lbl = new GameObject("BossLabel");
        lbl.transform.SetParent(transform);
        lbl.transform.localPosition  = new Vector3(0f, 0.9f / data.bossScale, 0f);
        lbl.transform.localScale     = Vector3.one / data.bossScale;
        lbl.transform.localRotation  = Quaternion.identity;

        TextMesh tm    = lbl.AddComponent<TextMesh>();
        tm.text        = "★ " + data.enemyName.ToUpper() + " ★";
        tm.fontSize    = 18;
        tm.color       = new Color(1f, 0.82f, 0.2f);
        tm.anchor      = TextAnchor.LowerCenter;
        tm.alignment   = TextAlignment.Center;
        MeshRenderer mr = lbl.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 12;
    }

    // ── HP bar ────────────────────────────────────────────────────────────────

    void CreateHPBar()
    {
        hpBarBg   = CreateBar("HPBarBg",   HP_BAR_Y, new Color(0.2f, 0.2f, 0.2f, 0.8f), 9);
        hpBarFill = CreateBar("HPBarFill", HP_BAR_Y, Color.green, 10);
    }

    void CreateShieldBar()
    {
        shieldBarBg   = CreateBar("ShieldBarBg",   SHIELD_BAR_Y, new Color(0.1f, 0.1f, 0.3f, 0.8f), 9);
        shieldBarFill = CreateBar("ShieldBarFill", SHIELD_BAR_Y, new Color(0.3f, 0.7f, 1f), 10);
    }

    SpriteRenderer CreateBar(string barName, float yOffset, Color color, int order)
    {
        GameObject obj            = new GameObject(barName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(0, yOffset, 0);
        obj.transform.localRotation = Quaternion.identity;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite       = RuntimeSprite.WhiteSquare;
        sr.color        = color;
        sr.sortingOrder = order;
        obj.transform.localScale = new Vector3(BAR_WIDTH, BAR_HEIGHT, 1f);
        return sr;
    }

    void UpdateHPBar()
    {
        if (hpBarFill == null) return;
        float ratio = Mathf.Clamp01((float)currentHealth / data.maxHealth);
        hpBarFill.transform.localScale    = new Vector3(BAR_WIDTH * ratio, BAR_HEIGHT, 1f);
        float offset = BAR_WIDTH * (1f - ratio) * -0.5f;
        hpBarFill.transform.localPosition = new Vector3(offset, HP_BAR_Y, 0);
        hpBarFill.color                   = Color.Lerp(Color.red, Color.green, ratio);
        if (hpBarBg   != null) hpBarBg.transform.rotation   = Quaternion.identity;
        hpBarFill.transform.rotation = Quaternion.identity;
    }

    void UpdateShieldBar()
    {
        if (shieldBarFill == null) return;
        // Use the larger of the data's shield max OR the current shield value
        // so aura-granted shields scale the bar visibly without ever exceeding it.
        int denom = Mathf.Max(1, Mathf.Max(data.shieldHealth, currentShield));
        float ratio = Mathf.Clamp01((float)currentShield / denom);
        shieldBarFill.transform.localScale    = new Vector3(BAR_WIDTH * ratio, BAR_HEIGHT, 1f);
        float offset = BAR_WIDTH * (1f - ratio) * -0.5f;
        shieldBarFill.transform.localPosition = new Vector3(offset, SHIELD_BAR_Y, 0);
        if (shieldBarBg   != null) shieldBarBg.transform.rotation   = Quaternion.identity;
        shieldBarFill.transform.rotation = Quaternion.identity;
    }

    // ── Update loop ───────────────────────────────────────────────────────────

    void Update()
    {
        if (_isDead || waypoints == null || waypoints.Length == 0) return;

        // Tick slow
        if (_slowTimer > 0f)
        {
            _slowTimer -= Time.deltaTime;
            if (_slowTimer <= 0f)
            {
                _slowMultiplier = 1f;
                moveSpeed       = data.moveSpeed;
                // Remove slow tint
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null && data.archetype == EnemyArchetype.Standard)
                    sr.color = data.sprite == null ? Color.red : Color.white;
            }
        }

        MoveAlongPath();
        UpdateHPBar();
        if (((data.archetype == EnemyArchetype.Shielded && data.shieldHealth > 0) ||
             data.archetype == EnemyArchetype.ShieldAura) && shieldBarFill != null)
            UpdateShieldBar();

        // Tick shield aura.
        if (data.archetype == EnemyArchetype.ShieldAura)
        {
            _auraTickTimer -= Time.deltaTime;
            if (_auraTickTimer <= 0f)
            {
                _auraTickTimer = data.shieldAuraInterval;
                EmitShieldAuraTick();
            }
        }

        // Tick boss / miniboss abilities.
        if (data.bossAbilities != BossAbilityFlags.None)
            TickBossAbilities();
    }

    // ── Boss / Miniboss abilities ─────────────────────────────────────────────

    void TickBossAbilities()
    {
        // Regen — heals over time.
        if ((data.bossAbilities & BossAbilityFlags.Regen) != 0 && data.regenPerSecond > 0)
        {
            _regenAccum += data.regenPerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(_regenAccum);
            if (whole > 0)
            {
                _regenAccum -= whole;
                currentHealth = Mathf.Min(data.maxHealth, currentHealth + whole);
                UpdateHPBar();
            }
        }

        // Teleport — jumps forward along the path periodically.
        if ((data.bossAbilities & BossAbilityFlags.Teleport) != 0
            && data.teleportSkipWaypoints > 0)
        {
            _teleportTimer -= Time.deltaTime;
            if (_teleportTimer <= 0f)
            {
                _teleportTimer = data.teleportInterval;
                int target = Mathf.Min(waypoints.Length - 1,
                                       currentWaypointIndex + data.teleportSkipWaypoints);
                if (target > currentWaypointIndex)
                {
                    StartCoroutine(TeleportFlash());
                    currentWaypointIndex = target;
                    transform.position = waypoints[target].position;
                }
            }
        }

        // Enrage — one-shot speed buff at low HP.
        if (!_enrageTriggered && (data.bossAbilities & BossAbilityFlags.Enrage) != 0)
        {
            float ratio = (float)currentHealth / Mathf.Max(1, data.maxHealth);
            if (ratio <= data.enrageHpThreshold)
            {
                _enrageTriggered = true;
                data = Instantiate(data); // private copy so we don't mutate the asset
                data.moveSpeed *= data.enrageSpeedMult;
                moveSpeed = data.moveSpeed * _slowMultiplier;
                StartCoroutine(EnrageFlash());
            }
        }

        // Summon — spawn N minions on its current position.
        if ((data.bossAbilities & BossAbilityFlags.Summon) != 0 && data.summonTemplate != null)
        {
            _summonTimer -= Time.deltaTime;
            if (_summonTimer <= 0f)
            {
                _summonTimer = data.summonInterval;
                SpawnMinions();
            }
        }
    }

    System.Collections.IEnumerator TeleportFlash()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color orig = sr.color;
        sr.color = new Color(1f, 1f, 1f, 0.3f);
        yield return new WaitForSeconds(0.15f);
        if (sr != null) sr.color = orig;
    }

    System.Collections.IEnumerator EnrageFlash()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color orig = sr.color;
        for (int i = 0; i < 4; i++)
        {
            sr.color = new Color(1f, 0.3f, 0.3f);
            yield return new WaitForSeconds(0.12f);
            if (sr == null) yield break;
            sr.color = orig;
            yield return new WaitForSeconds(0.12f);
        }
    }

    void SpawnMinions()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        // Reuse the standard enemy GameObject template so summoned minions
        // travel the same path. They start at the boss's current waypoint.
        for (int i = 0; i < data.summonCount; i++)
        {
            GameObject minionGO = new GameObject("Minion_" + data.summonTemplate.enemyName);
            minionGO.transform.position = transform.position + new Vector3(
                UnityEngine.Random.Range(-0.4f, 0.4f),
                UnityEngine.Random.Range(-0.4f, 0.4f), 0f);
            minionGO.AddComponent<SpriteRenderer>();
            Enemy minion = minionGO.AddComponent<Enemy>();
            // Build a sliced waypoint list starting from the current index so
            // the minion continues from where the boss currently is.
            int idx = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Length - 1);
            int len = waypoints.Length - idx;
            Transform[] sliced = new Transform[len];
            for (int k = 0; k < len; k++) sliced[k] = waypoints[idx + k];
            minion.Initialize(data.summonTemplate, sliced);
        }
    }

    void EmitShieldAuraTick()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        bool buffedAnyone = false;
        foreach (Enemy e in all)
        {
            if (e == this || e._isDead) continue;
            if (Vector3.Distance(e.transform.position, transform.position) > data.shieldAuraRadius) continue;
            e.ReceiveAuraShield(data.shieldAuraAmount);
            buffedAnyone = true;
        }
        if (buffedAnyone) StartCoroutine(AuraFlash());
    }

    System.Collections.IEnumerator AuraFlash()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color orig = sr.color;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.12f);
        if (sr != null) sr.color = orig;
    }

    /// <summary>Add temporary shield HP from a friendly Shield-Aura enemy.
    /// Uses the same shield bar as the Shielded archetype, so a normal enemy
    /// becomes briefly shielded while an aura is active.</summary>
    public void ReceiveAuraShield(int amount)
    {
        if (_isDead) return;
        if (currentShield <= 0 && shieldBarFill == null) CreateShieldBar();
        currentShield += amount;
        UpdateShieldBar();
    }

    void MoveAlongPath()
    {
        if (currentWaypointIndex >= waypoints.Length) { ReachExit(); return; }

        Transform target  = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // Flip the sprite to face the direction of travel. The art is authored
        // facing left, so FlipX = true when moving right.
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = direction.x > 0f;
        }

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
            currentWaypointIndex++;
    }

    // ── Targeting / reveal ────────────────────────────────────────────────────

    /// <summary>Called by a Hero's SlowField skill. Applies or refreshes a speed penalty.</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        // Only apply if stronger or refreshing
        if (multiplier < _slowMultiplier || _slowTimer <= 0f)
        {
            _slowMultiplier = multiplier;
            moveSpeed       = data.moveSpeed * _slowMultiplier;
            // Ice-blue tint
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.5f, 0.8f, 1f);
        }
        // Always refresh duration
        if (duration > _slowTimer) _slowTimer = duration;
    }

    /// <summary>Returns false for unrevealed Stealth enemies so towers won't target them.</summary>
    public bool IsTargetable() => IsRevealed;

    /// <summary>Called by a Tower with hasDetection = true when this enemy enters its range.</summary>
    public void Reveal()
    {
        if (IsRevealed || data.archetype != EnemyArchetype.Stealth) return;
        IsRevealed = true;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a     = 1f;
            sr.color = c;
        }
    }

    /// <summary>Called when the enemy exits all detection tower ranges.</summary>
    public void Conceal()
    {
        if (data.archetype != EnemyArchetype.Stealth) return;
        IsRevealed = false;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a     = 0.15f;
            sr.color = c;
        }
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deal damage to this enemy.
    /// Pierce damage bypasses the Shielded archetype's shield entirely.
    /// </summary>
    public void TakeDamage(int damage, DamageType type = DamageType.Normal)
    {
        if (_isDead) return;

        // Shield absorbs the hit when the enemy is Shielded OR currently has
        // aura-granted shield HP. Pierce always bypasses.
        bool hasShield = currentShield > 0 &&
                         (data.archetype == EnemyArchetype.Shielded ||
                          data.archetype == EnemyArchetype.ShieldAura ||
                          shieldBarFill != null);
        if (hasShield && type != DamageType.Pierce)
        {
            currentShield -= damage;
            if (currentShield < 0) currentShield = 0;
            return;
        }

        currentHealth -= damage;
        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        int reward = data.archetype == EnemyArchetype.Boss
            ? data.goldReward * 3
            : data.goldReward;

        CurrencyManager.Instance?.AddGold(reward);

        // Splitter: spawn smaller copies BEFORE notifying the wave spawner of
        // the death. Doing it in this order means enemiesAlive is incremented
        // by the children before being decremented by the parent's death —
        // otherwise a splitter dying as the last enemy on screen would
        // momentarily hit zero and trigger a premature round-complete.
        if (data.archetype == EnemyArchetype.Splitter && data.splitInto != null && WaveSpawner.Instance != null)
        {
            WaveSpawner.Instance.SpawnSplit(data.splitInto, transform.position, waypoints, currentWaypointIndex, data.splitCount);
        }

        OnEnemyDeath?.Invoke(this);
        if (data.archetype == EnemyArchetype.Boss)
            OnBossDefeated?.Invoke(this);

        Destroy(gameObject);
    }

    void ReachExit()
    {
        if (_isDead) return;
        _isDead = true;

        int livesLost = data.archetype == EnemyArchetype.Boss ? 3 : 1;
        LivesManager.Instance?.LoseLife(livesLost);
        OnEnemyReachedExit?.Invoke(this);
        Destroy(gameObject);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    public float GetDistanceToExit()
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Length) return 0f;
        float dist = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        for (int i = currentWaypointIndex; i < waypoints.Length - 1; i++)
            dist += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
        return dist;
    }

    /// <summary>Used by split-spawned enemies so they continue along the
    /// parent's path instead of restarting at the beginning.</summary>
    public void SetWaypointIndex(int index)
    {
        if (waypoints == null) return;
        currentWaypointIndex = Mathf.Clamp(index, 0, waypoints.Length - 1);
    }
}
