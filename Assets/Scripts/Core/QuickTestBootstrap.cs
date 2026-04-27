using UnityEngine;

/// <summary>
/// QUICK-TEST BOOTSTRAP
/// ────────────────────────────────────────────────────────────────────────────
/// Add this component to ANY empty GameObject in the Gameplay scene.
/// When you press Play directly from the Gameplay scene (bypassing MainMenu /
/// Overworld), it creates every required ScriptableObject and prefab GameObject
/// entirely in memory — no asset files needed.
///
/// The normal play-from-MainMenu flow is completely unaffected: this script
/// skips itself if GameManager is already initialised.
///
/// Inspector options:
///   startingGold    — gold you start with
///   startingLives   — lives you start with
///   roundCount      — how many waves to generate (1-5)
///   includeArchetypes — spawn Shielded / Stealth / Boss enemies in later rounds
///   includeProfessor  — add a test professor (hero) to the shop
/// </summary>
public class QuickTestBootstrap : MonoBehaviour
{
    [Header("Test Settings")]
    public int  startingGold      = 200;
    public int  startingLives     = 20;
    [Range(1, 5)]
    public int  roundCount        = 3;
    public bool includeArchetypes = true;
    public bool includeProfessor  = true;
    [Tooltip("If set, loads a saved custom map instead of the default test path.")]
    public string customMapName;

    [Header("Optional Prefab Overrides")]
    [Tooltip("If assigned, this prefab is used for spawned enemies instead of the procedural red-circle one. Drag your Assets/Prefabs/Enemy.prefab here to use the animated version.")]
    public GameObject enemyPrefabOverride;

    // ── Entry point ───────────────────────────────────────────────────────────

    void Awake()
    {
        bool endless = EndlessMode.LaunchRequested;
        EndlessMode.ConsumeRequest();

        bool marathon = MarathonMode.LaunchRequested;
        MarathonMode.ConsumeRequest();

        bool forced = TestingModeLauncher.TestingModeRequested || endless || marathon;
        TestingModeLauncher.ConsumeRequest();

        // Only bootstrap when there is no GameManager (direct scene play),
        // OR when we were explicitly launched via the main-menu Testing Mode
        // / Endless Mode buttons.
        if (GameManager.Instance != null && !forced) return;

        Debug.Log(forced
            ? (marathon
                ? "[QuickTest] Marathon Mode requested \u2014 bootstrapping 40-wave campus run."
                : endless
                    ? "[QuickTest] Endless Mode requested \u2014 bootstrapping endless session."
                    : "[QuickTest] Testing Mode requested \u2014 bootstrapping test session.")
            : "[QuickTest] No GameManager found \u2014 bootstrapping test session.");

        // Apply testing-mode overrides if the launcher injected any.
        if (!endless && !marathon) TestingModeLauncher.ApplyOverridesTo(this);
        else if (endless)
        {
            // Endless preset: more lives, more starting gold, all archetypes,
            // professor available so the player has tools to survive.
            startingGold      = 350;
            startingLives     = 30;
            includeArchetypes = true;
            includeProfessor  = true;
            roundCount        = 1;          // ignored — endless replaces this
            customMapName     = null;       // ignored — endless map below
        }
        else // marathon
        {
            startingGold      = 250;
            startingLives     = 25;
            includeArchetypes = true;
            includeProfessor  = false;       // heroes come from buff offers
            roundCount        = 1;           // ignored — marathon replaces this
            customMapName     = null;
        }

        // 1. Persistent managers (skip if we already have a GameManager from the menu)
        if (GameManager.Instance == null)
            BootstrapManagers();

        // 2. Runtime prefab GameObjects (kept inactive, used as Instantiate templates)
        GameObject projectilePrefab = MakeProjectilePrefab();
        GameObject enemyPrefab      = MakeEnemyPrefab();
        GameObject towerPrefab      = MakeTowerPrefab();

        // 3. Data assets (all in-memory ScriptableObjects)
        EnemyData[]       enemies  = MakeEnemyData();
        TowerData[]       towers   = MakeTowerData(projectilePrefab, includeProfessor);
        WaveData[]        waves    = MakeWaves(enemies, roundCount, includeArchetypes);
        PathPatternData   path     = marathon ? MakeMarathonPath()
                                   : endless  ? MakeEndlessPath()
                                              : MakePath();
        LevelData         level    = MakeLevel(waves, path);
        FacultyData       faculty  = MakeFaculty(level, towers, includeProfessor);

        if (endless)
        {
            // Wire endless mode: provider + enemy pool + spawn-point count.
            EndlessMode.IsActive       = true;
            EndlessMode.EnemyPool      = enemies;
            EndlessMode.SpawnPointCount = path.spawnPointPositions != null
                                          ? path.spawnPointPositions.Length : 1;
            EndlessMode.CurrentRound   = 0;
        }

        if (marathon)
        {
            // Wire marathon mode: 40-wave generator + buff selection cadence.
            MarathonMode.IsActive        = true;
            MarathonMode.EnemyPool       = enemies;
            MarathonMode.SpawnPointCount = path.spawnPointPositions != null
                                          ? path.spawnPointPositions.Length : 1;
            MarathonMode.CurrentWave     = 0;

            if (RunBuffs.Instance == null)
            {
                var rb = new GameObject("--- RunBuffs ---");
                rb.AddComponent<RunBuffs>();
            }
            else { RunBuffs.Instance.Reset(); }
            if (BuffSelectionUI.Instance == null)
            {
                var ui = new GameObject("--- BuffSelectionUI ---");
                ui.AddComponent<BuffSelectionUI>();
            }
            if (MarathonRunController.Instance == null)
            {
                var mc = new GameObject("--- MarathonRunController ---");
                mc.AddComponent<MarathonRunController>();
            }
            MarathonMode.BuffPool = MakeBuffPool(towers, projectilePrefab);
            // Marathon starts with cannon AND frost unlocked — every regular
            // tower is offered in the side shop from round 1.
            Debug.Log($"[Marathon] Bootstrapped \u2014 path '{path.patternName}', " +
                      $"{MarathonMode.SpawnPointCount} spawns defined, " +
                      $"{MarathonMode.BuffPool.Count} buff offers in pool, " +
                      $"{towers.Length} starter towers (cannon/frost included).");
        }

        // 4. Tell GameManager which level to run
        GameManager.Instance.currentFaculty    = faculty;
        GameManager.Instance.currentCourseIndex = 0;
        GameManager.Instance.SetState(GameState.Playing);

        // 5. Configure GameplayAutoSetup if present in scene
        GameplayAutoSetup setup = FindAnyObjectByType<GameplayAutoSetup>();
        if (setup != null)
        {
            setup.enemyPrefab     = enemyPrefab;
            setup.towerPrefab     = towerPrefab;
            setup.easyPatterns    = new PathPatternData[] { path };
            setup.mediumPatterns  = new PathPatternData[0];
            setup.hardPatterns    = new PathPatternData[0];
            setup.expertPatterns  = new PathPatternData[0];
            setup.availableTowers = towers;
            if (MarathonMode.IsActive)
                Debug.Log($"[Marathon] GameplayAutoSetup configured — will load path '{path.patternName}'.");
        }
        else
        {
            Debug.LogWarning("[QuickTest] No GameplayAutoSetup found in scene. " +
                             "Add one to an empty GameObject in the Gameplay scene.");
        }
    }

    // ── Persistent managers ───────────────────────────────────────────────────

    void BootstrapManagers()
    {
        GameObject go = new GameObject("--- QuickTest Managers ---");
        DontDestroyOnLoad(go);

        GameManager gm = go.AddComponent<GameManager>();

        go.AddComponent<CreditManager>();

        gm.allFaculties = new FacultyData[0]; // filled later
    }

    // ── Enemy prefab ──────────────────────────────────────────────────────────

    /// <summary>
    /// Hidden parent that holds prefab templates. We DON'T mark the templates
    /// themselves SetActive(false), because Instantiate would copy that state
    /// and the spawned instance would be inactive (towers wouldn't render,
    /// enemies wouldn't move, etc.). Instead we keep them active but parent
    /// them under this inactive holder so they don't update or render in the
    /// scene. Spawned instances live at root and are fully active.
    /// </summary>
    static Transform _prefabHolder;
    static Transform GetPrefabHolder()
    {
        if (_prefabHolder != null) return _prefabHolder;
        var go = new GameObject("__QuickTestPrefabHolder");
        go.SetActive(false);
        DontDestroyOnLoad(go);
        _prefabHolder = go.transform;
        return _prefabHolder;
    }

    GameObject MakeEnemyPrefab()
    {
        // 1. Inspector override (used when QuickTestBootstrap is placed in the
        //    scene manually with a prefab assigned).
        if (enemyPrefabOverride != null)
            return enemyPrefabOverride;

        // 2. Resources fallback. Marathon/Endless modes spawn a fresh
        //    QuickTestBootstrap GameObject at runtime, so there is no scene
        //    object to drag a reference into. Drop a copy of your animated
        //    Enemy prefab into Assets/Resources/Enemy.prefab and it will be
        //    picked up automatically.
        var resPrefab = Resources.Load<GameObject>("Enemy");
        if (resPrefab != null)
            return resPrefab;

        GameObject obj  = new GameObject("__EnemyPrefab");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color        = Color.red;
        sr.sortingOrder = 3;
        obj.AddComponent<Enemy>();
        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.isTrigger  = true;
        col.radius     = 0.3f;
        obj.transform.SetParent(GetPrefabHolder(), worldPositionStays: false);
        return obj;
    }

    // ── Tower prefab ──────────────────────────────────────────────────────────

    GameObject MakeTowerPrefab()
    {
        GameObject obj  = new GameObject("__TowerPrefab");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color        = Color.blue;
        sr.sortingOrder = 4;
        obj.AddComponent<Tower>();
        // FirePoint child
        GameObject fp = new GameObject("FirePoint");
        fp.transform.SetParent(obj.transform);
        fp.transform.localPosition = new Vector3(0, 0.4f, 0);
        obj.transform.SetParent(GetPrefabHolder(), worldPositionStays: false);
        return obj;
    }

    // ── Projectile prefab ─────────────────────────────────────────────────────

    GameObject MakeProjectilePrefab()
    {
        GameObject obj  = new GameObject("__ProjectilePrefab");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color        = Color.yellow;
        sr.sortingOrder = 5;
        obj.AddComponent<Projectile>();
        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.isTrigger  = true;
        col.radius     = 0.15f;
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.isKinematic  = true;
        obj.transform.SetParent(GetPrefabHolder(), worldPositionStays: false);
        return obj;
    }

    // ── Enemy data ────────────────────────────────────────────────────────────

    EnemyData[] MakeEnemyData()
    {
        EnemyData basic = ScriptableObject.CreateInstance<EnemyData>();
        basic.enemyName  = "Basic Bug";
        basic.moveSpeed  = 2f;
        basic.maxHealth  = 80;
        basic.goldReward = 10;
        basic.courseTier = 1;
        basic.artFacesRight = true;
        basic.visualScale   = 0.5f;  
        basic.archetype  = EnemyArchetype.Standard;

        EnemyData fast = ScriptableObject.CreateInstance<EnemyData>();
        fast.enemyName   = "Fast Bug";
        fast.moveSpeed   = 4f;
        fast.maxHealth   = 50;
        fast.goldReward  = 15;
        fast.courseTier  = 1;
        fast.archetype   = EnemyArchetype.Standard;
        // Speedy enemies use the original dorotest animation; everything else
        // falls back to the Enemy prefab's default controller (dog_baton).
        fast.animatorController = Resources.Load<RuntimeAnimatorController>("Animators/dorotest");

        EnemyData tank = ScriptableObject.CreateInstance<EnemyData>();
        tank.enemyName   = "Tank Bug";
        tank.moveSpeed   = 1.2f;
        tank.maxHealth   = 250;
        tank.goldReward  = 25;
        tank.courseTier  = 2;
        tank.archetype   = EnemyArchetype.Standard;
        tank.sprite  = Resources.Load<Sprite>("Sprites/catto");
        tank.animatorController  = null;
        tank.artFacesRight      = false;
        tank.visualScale = 1.1f;
        tank.playSpawnSound = true;


        EnemyData shielded = ScriptableObject.CreateInstance<EnemyData>();
        shielded.enemyName   = "Shielded Bug";
        shielded.moveSpeed   = 1.8f;
        shielded.maxHealth   = 120;
        shielded.goldReward  = 20;
        shielded.courseTier  = 2;
        shielded.archetype   = EnemyArchetype.Shielded;
        shielded.sprite       = Resources.Load<Sprite>("Sprites/tungtungtung");
        shielded.animatorController = null;
        shielded.artFacesRight      = false;
        shielded.shieldHealth = 80;
        shielded.visualScale = 1f;

        EnemyData stealth = ScriptableObject.CreateInstance<EnemyData>();
        stealth.enemyName  = "Stealth Bug";
        stealth.moveSpeed  = 3f;
        stealth.maxHealth  = 60;
        stealth.goldReward = 30;
        stealth.courseTier = 2;
        stealth.archetype  = EnemyArchetype.Stealth;
        stealth.animatorController = Resources.Load<RuntimeAnimatorController>("Animators/dad");
        stealth.artFacesRight      = true;   // or false, depending on the art
        stealth.visualScale        = 2f; 

        EnemyData boss = ScriptableObject.CreateInstance<EnemyData>();
        boss.enemyName   = "Chatterbox";
        boss.moveSpeed   = 0.9f;
        boss.maxHealth   = 800;
        boss.goldReward  = 80;
        boss.artFacesRight = true;
        boss.courseTier  = 3;
        boss.archetype   = EnemyArchetype.Boss;
        boss.bossScale   = 2.2f;
        boss.deathAnimatorOverride = Resources.Load<RuntimeAnimatorController>("Animators/dogrest");
        boss.deathFxDuration = 1.5f; // seconds the death animation plays before the FX is destroyed

        // Splitter child template — small basic that splits make.
        EnemyData splitChild = ScriptableObject.CreateInstance<EnemyData>();
        splitChild.enemyName   = "Splitter Spawn";
        splitChild.moveSpeed   = 3f;
        splitChild.maxHealth   = 35;
        splitChild.goldReward  = 5;
        splitChild.courseTier  = 1;
        splitChild.archetype   = EnemyArchetype.Standard;
        splitChild.sprite  = Resources.Load<Sprite>("Sprites/tralalelotralala");
        splitChild.animatorController  = null;
        splitChild.artFacesRight      = false;
        splitChild.visualScale = 0.5f;

    


        EnemyData splitter = ScriptableObject.CreateInstance<EnemyData>();
        splitter.enemyName  = "Splitter Bug";
        splitter.moveSpeed  = 1.6f;
        splitter.maxHealth  = 140;
        splitter.goldReward = 20;
        splitter.courseTier = 2;
        splitter.archetype  = EnemyArchetype.Splitter;
        splitter.splitInto  = splitChild;
        splitter.splitCount = 2;
        splitter.sprite  = Resources.Load<Sprite>("Sprites/tralalelotralala");
        splitter.animatorController  = null;
        splitter.artFacesRight      = false;
        splitter.visualScale = 1.1f;

        EnemyData shieldAura = ScriptableObject.CreateInstance<EnemyData>();
        shieldAura.enemyName          = "Aura Bug";
        shieldAura.moveSpeed          = 1.4f;
        shieldAura.maxHealth          = 120;
        shieldAura.goldReward         = 25;
        shieldAura.courseTier         = 3;
        shieldAura.archetype          = EnemyArchetype.ShieldAura;
        shieldAura.shieldAuraRadius   = 2.5f;
        shieldAura.shieldAuraAmount   = 30;
        shieldAura.shieldAuraInterval = 3f;
        shieldAura.sprite  = Resources.Load<Sprite>("Sprites/whatthedog");
        shieldAura.animatorController  = null;
        shieldAura.artFacesRight      = false;
        return new EnemyData[] { basic, fast, tank, shielded, stealth, boss, splitter, shieldAura };
    }

    // ── Tower data ────────────────────────────────────────────────────────────

    TowerData[] MakeTowerData(GameObject projPrefab, bool withProfessor)
    {
        TowerData rapid = ScriptableObject.CreateInstance<TowerData>();
        rapid.towerName        = "Rapid Tower";
        rapid.towerType        = TowerType.Rapid;
        rapid.cost             = 40;
        rapid.range            = 2.5f;
        rapid.fireRate         = 3f;
        rapid.damage           = 10;
        rapid.projectilePrefab = projPrefab;
        rapid.damageType       = DamageType.Normal;

        TowerData balanced = ScriptableObject.CreateInstance<TowerData>();
        balanced.towerName        = "Balanced Tower";
        balanced.towerType        = TowerType.Balanced;
        balanced.cost             = 60;
        balanced.range            = 3f;
        balanced.fireRate         = 1.5f;
        balanced.damage           = 25;
        balanced.projectilePrefab = projPrefab;
        balanced.damageType       = DamageType.Normal;

        TowerData sniper = ScriptableObject.CreateInstance<TowerData>();
        sniper.towerName        = "Sniper Tower";
        sniper.towerType        = TowerType.Sniper;
        sniper.cost             = 100;
        sniper.range            = 5f;
        sniper.fireRate         = 0.5f;
        sniper.damage           = 80;
        sniper.projectilePrefab = projPrefab;
        sniper.damageType       = DamageType.Pierce;  // pierce test
        sniper.hasDetection     = true; // can reveal stealth enemies

        // AOE Cannon — splash damage in a small radius around impact.
        TowerData cannon = ScriptableObject.CreateInstance<TowerData>();
        cannon.towerName            = "AOE Cannon";
        cannon.towerType            = TowerType.Cannon;
        cannon.cost                 = 90;
        cannon.range                = 2.8f;
        cannon.fireRate             = 0.7f;
        cannon.damage               = 35;
        cannon.projectilePrefab     = projPrefab;
        cannon.damageType           = DamageType.Normal;
        cannon.splashRadius         = 1.4f;
        cannon.splashDamageFraction = 0.7f;
        // Lob the shot in a parabola so AOE feels like a mortar instead of
        // a straight bullet. Tweak arcHeight in the inspector or here to
        // taste; 1.2 world-units gives a nice mid-flight peak.
        cannon.arcHeight            = 1.2f;

        // Frost Tower — small splash + slows on hit.
        TowerData frost = ScriptableObject.CreateInstance<TowerData>();
        frost.towerName            = "Frost Tower";
        frost.towerType            = TowerType.Frost;
        frost.cost                 = 80;
        frost.range                = 3f;
        frost.fireRate             = 1.2f;
        frost.damage               = 8;
        frost.projectilePrefab     = projPrefab;
        frost.damageType           = DamageType.Normal;
        frost.splashRadius         = 0.8f;
        frost.splashDamageFraction = 1f;
        frost.slowOnHitMultiplier  = 0.5f;
        frost.slowOnHitDuration    = 2f;

        // Wire upgrade paths + capstone perks for each regular tower.
        AssignTowerUpgrades(rapid, balanced, sniper, cannon, frost);

        if (!withProfessor)
            return new TowerData[] { rapid, balanced, sniper, cannon, frost };

        // Professor / hero
        HeroSkillData blastSkill = ScriptableObject.CreateInstance<HeroSkillData>();
        blastSkill.skillName        = "Lecture Burst";
        blastSkill.description      = "Deals heavy damage to nearby enemies.";
        blastSkill.cooldown         = 12f;
        blastSkill.effect           = HeroSkillEffect.AoEBlast;
        blastSkill.radius           = 3.5f;
        blastSkill.blastDamage      = 100;

        HeroSkillData slowSkill = ScriptableObject.CreateInstance<HeroSkillData>();
        slowSkill.skillName       = "Freeze Frame";
        slowSkill.description     = "Slows all nearby enemies.";
        slowSkill.cooldown        = 15f;
        slowSkill.effect          = HeroSkillEffect.SlowField;
        slowSkill.radius          = 4f;
        slowSkill.slowMultiplier  = 0.35f;
        slowSkill.slowDuration    = 4f;

        // "Self 2" — placeholder ground-targeted DoT skill (framework demo)
        HeroSkillData groundSkill = ScriptableObject.CreateInstance<HeroSkillData>();
        groundSkill.skillName        = "Study Group";
        groundSkill.description      = "Place an AOE that deals damage over time for 3s.";
        groundSkill.cooldown         = 20f;
        groundSkill.effect           = HeroSkillEffect.GroundTargetedAOE;
        groundSkill.radius           = 2.5f;
        groundSkill.aoeDamagePerTick = 15;
        groundSkill.aoeTickInterval  = 0.4f;
        groundSkill.aoeDuration      = 3f;
        groundSkill.aoeDamageType    = DamageType.Pierce;

        TowerData prof1 = ScriptableObject.CreateInstance<TowerData>();
        prof1.towerName        = "Prof. Lee";
        prof1.towerType        = TowerType.Professor;
        prof1.cost             = 120;
        prof1.isProfessorTower = true;
        prof1.unique           = true;
        prof1.heroSkill        = blastSkill;
        prof1.projectilePrefab = projPrefab;  // needed for the underlying tower shot

        TowerData prof2 = ScriptableObject.CreateInstance<TowerData>();
        prof2.towerName        = "Prof. Chan";
        prof2.towerType        = TowerType.Professor;
        prof2.cost             = 140;
        prof2.isProfessorTower = true;
        prof2.unique           = true;
        prof2.heroSkill        = slowSkill;
        prof2.hasDetection     = true; // can reveal stealth enemies
        prof2.projectilePrefab = projPrefab;

        TowerData self2 = ScriptableObject.CreateInstance<TowerData>();
        self2.towerName        = "Self 2";
        self2.towerType        = TowerType.Professor;
        self2.cost             = 100;
        self2.isProfessorTower = true;
        self2.unique           = true;
        self2.heroSkill        = groundSkill;
        self2.projectilePrefab = projPrefab;

        // ──────────────────────────────────────────────────────────────────
        // FACULTY PROFESSORS (TW Chim, Hayden So, Greg Wu, Wilton)
        // ──────────────────────────────────────────────────────────────────
        TowerData[] faculty = MakeFacultyProfessors(projPrefab);

        var all = new System.Collections.Generic.List<TowerData> {
            rapid, balanced, sniper, cannon, frost, prof1, prof2, self2
        };
        all.AddRange(faculty);
        return all.ToArray();
    }

    // ── Wave data ─────────────────────────────────────────────────────────────

    WaveData[] MakeWaves(EnemyData[] e, int count, bool archetypes)
    {
        EnemyData basic    = e[0];
        EnemyData fast     = e[1];
        EnemyData tank     = e[2];
        EnemyData shielded = e[3];
        EnemyData stealth  = e[4];
        EnemyData boss     = e[5];

        WaveData w1 = ScriptableObject.CreateInstance<WaveData>();
        w1.waveName = "Round 1 — Basic";
        w1.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = basic, count = 8, spawnInterval = 1.2f, spawnPointIndex = 0 }
        };
        w1.delayBetweenGroups = 1.5f;

        WaveData w2 = ScriptableObject.CreateInstance<WaveData>();
        w2.waveName = "Round 2 — Mixed";
        w2.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = basic, count = 6, spawnInterval = 1f,  spawnPointIndex = 0 },
            new EnemyGroup { enemyType = fast,  count = 4, spawnInterval = 0.7f, spawnPointIndex = 0 }
        };
        w2.delayBetweenGroups = 1.5f;

        WaveData w3 = ScriptableObject.CreateInstance<WaveData>();
        w3.waveName = "Round 3 — Heavy";
        w3.enemyGroups = archetypes
            ? new EnemyGroup[]
              {
                  new EnemyGroup { enemyType = shielded, count = 4,  spawnInterval = 1.5f, spawnPointIndex = 0 },
                  new EnemyGroup { enemyType = stealth,  count = 3,  spawnInterval = 1.0f, spawnPointIndex = 0 },
                  new EnemyGroup { enemyType = tank,     count = 2,  spawnInterval = 2f,   spawnPointIndex = 0 }
              }
            : new EnemyGroup[]
              {
                  new EnemyGroup { enemyType = fast, count = 10, spawnInterval = 0.7f, spawnPointIndex = 0 },
                  new EnemyGroup { enemyType = tank, count = 3,  spawnInterval = 2f,   spawnPointIndex = 0 }
              };
        w3.delayBetweenGroups = 2f;

        WaveData w4 = ScriptableObject.CreateInstance<WaveData>();
        w4.waveName = "Round 4 — Swarm";
        w4.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = fast,  count = 15, spawnInterval = 0.5f, spawnPointIndex = 0 },
            new EnemyGroup { enemyType = basic, count = 8,  spawnInterval = 0.8f, spawnPointIndex = 0 }
        };
        w4.delayBetweenGroups = 1f;

        WaveData w5 = ScriptableObject.CreateInstance<WaveData>();
        w5.waveName = "Round 5 — Boss";
        w5.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = fast,     count = 8,  spawnInterval = 0.5f, spawnPointIndex = 0 },
            new EnemyGroup { enemyType = boss,      count = 1,  spawnInterval = 0f,   spawnPointIndex = 0 },
            new EnemyGroup { enemyType = shielded,  count = 3,  spawnInterval = 1.2f, spawnPointIndex = 0 }
        };
        w5.delayBetweenGroups = 2f;

        WaveData[] all = { w1, w2, w3, w4, w5 };
        WaveData[] result = new WaveData[Mathf.Clamp(count, 1, 5)];
        for (int i = 0; i < result.Length; i++) result[i] = all[i];
        return result;
    }

    // ── Path data ─────────────────────────────────────────────────────────────

    PathPatternData MakePath()
    {
        // If a saved custom map was requested, try to load it first.
        if (!string.IsNullOrEmpty(customMapName))
        {
            PathPatternData saved = CustomMapStore.Load(customMapName);
            if (saved != null)
            {
                Debug.Log($"[QuickTest] Using custom map '{customMapName}'.");
                return saved;
            }
            Debug.LogWarning($"[QuickTest] Custom map '{customMapName}' not found. Falling back to default test path.");
        }

        PathPatternData p = ScriptableObject.CreateInstance<PathPatternData>();
        p.patternName       = "QuickTest Path";
        p.difficultyTier    = 1;
        p.waypointPositions = new Vector3[]
        {
            new Vector3(-7,  0, 0),
            new Vector3(-3,  0, 0),
            new Vector3(-1,  2, 0),
            new Vector3( 1,  2, 0),
            new Vector3( 3,  0, 0),
            new Vector3( 7,  0, 0)
        };
        p.spawnPointPositions = new Vector3[] { new Vector3(-8, 0, 0) };
        p.exitPosition        = new Vector3(8, 0, 0);
        p.towerSlotPositions  = new Vector3[]
        {
            new Vector3(-3,  1.5f, 0),
            new Vector3(-1, -1.2f, 0),
            new Vector3( 1, -1.2f, 0),
            new Vector3( 3,  1.5f, 0),
            new Vector3( 0,  3.5f, 0),
            new Vector3(-5, -1.5f, 0),
            new Vector3( 5, -1.5f, 0)
        };
        return p;
    }

    /// <summary>
    /// Large multi-spawn / multi-home map used by Endless Mode. Enough screen
    /// real-estate that the player must pan/zoom (CameraController handles
    /// that) to see everything at once. Three independent paths each end at
    /// their own home; defending all three is the core challenge.
    /// </summary>
    PathPatternData MakeEndlessPath()
    {
        PathPatternData p = ScriptableObject.CreateInstance<PathPatternData>();
        p.patternName    = "Endless Campus";
        p.difficultyTier = 4;

        // Three spawn entry points around the perimeter
        p.spawnPointPositions = new Vector3[]
        {
            new Vector3(-18,  8, 0),   // top-left gate
            new Vector3( 18,  8, 0),   // top-right gate
            new Vector3(-18, -8, 0),   // bottom-left gate
        };

        // Per-spawn waypoint chains — each ending at its OWN home point.
        var chains = new PathPatternData.SpawnWaypoints[3];

        // Path A (top-left ➜ centre-right home)
        chains[0] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-18,  8, 0),
            new Vector3(-12,  8, 0),
            new Vector3(-12,  3, 0),
            new Vector3( -4,  3, 0),
            new Vector3( -4, -2, 0),
            new Vector3(  6, -2, 0),
            new Vector3(  6,  2, 0),
            new Vector3( 14,  2, 0),
            new Vector3( 16,  2, 0),    // HOME A
        }};

        // Path B (top-right ➜ centre-left home)
        chains[1] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3( 18,  8, 0),
            new Vector3( 12,  8, 0),
            new Vector3( 12,  4, 0),
            new Vector3(  2,  4, 0),
            new Vector3(  2, -5, 0),
            new Vector3( -8, -5, 0),
            new Vector3( -8, -1, 0),
            new Vector3(-14, -1, 0),
            new Vector3(-16, -1, 0),    // HOME B
        }};

        // Path C (bottom-left ➜ top-centre home)
        chains[2] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-18, -8, 0),
            new Vector3( -2, -8, 0),
            new Vector3( -2, -5, 0),
            new Vector3(  8, -5, 0),
            new Vector3(  8,  6, 0),
            new Vector3(  0,  6, 0),
            new Vector3(  0,  9, 0),    // HOME C
        }};

        p.spawnWaypointPositions = chains;

        // Legacy single-chain path is unused when per-spawn chains are set,
        // but we still set a sensible default for the rendering safety net.
        p.waypointPositions = chains[0].positions;
        p.exitPosition      = chains[0].positions[chains[0].positions.Length - 1];

        // Tower slots distributed around the safe areas next to the paths.
        p.towerSlotPositions = new Vector3[]
        {
            // Top corridor (between paths A & B)
            new Vector3(-10,  6, 0), new Vector3(-6,  6, 0), new Vector3( 0,  6, 0),
            new Vector3(  6,  6, 0), new Vector3(10,  6, 0),
            // Middle band (junctions of all three paths)
            new Vector3(-10,  1, 0), new Vector3(-6,  0, 0), new Vector3( 0,  0, 0),
            new Vector3(  6,  0, 0), new Vector3(10,  0, 0), new Vector3(14, -2, 0),
            // Lower band (path C territory)
            new Vector3(-12, -3, 0), new Vector3(-6, -3, 0), new Vector3( 0, -3, 0),
            new Vector3(  4, -3, 0), new Vector3(10, -3, 0),
            // Bottom strip
            new Vector3(-14, -6, 0), new Vector3(-8, -6, 0), new Vector3(-2, -6, 0),
            new Vector3(  4, -6, 0), new Vector3(10, -6, 0), new Vector3(14, -6, 0),
            // Outside flank (defends home A & home B)
            new Vector3( 14,  4, 0), new Vector3(-14,  1, 0), new Vector3(-14, -4, 0),
        };

        return p;
    }

    /// <summary>
    /// Marathon prototype map — long horizontal HKU campus layout.
    ///
    /// HOW TO EDIT THIS MAP:
    /// ─────────────────────
    /// • Move spawn points by editing <see cref="PathPatternData.spawnPointPositions"/>.
    /// • Reshape an enemy route by editing the corresponding entry of
    ///   <see cref="PathPatternData.spawnWaypointPositions"/>. Each route is a
    ///   list of waypoints starting at the spawn and ending at the home base.
    /// • Add / remove tower spots by editing
    ///   <see cref="PathPatternData.towerSlotPositions"/>.
    /// • The world units used here roughly span x∈[-15,14], y∈[-7,7]. Camera
    ///   orthographic size for marathon is 9 (set in GameplayAutoSetup).
    /// • Spawn 0 (left mid avenue) is always active. The remaining 5 spawns
    ///   unlock at waves 5, 10, 15, 25, and 35 — see
    ///   MarathonMode.ActiveSpawnCount to change unlock thresholds.
    /// </summary>
    PathPatternData MakeMarathonPath()
    {
        PathPatternData p = ScriptableObject.CreateInstance<PathPatternData>();
        p.patternName    = "Marathon Campus";
        p.difficultyTier = 2;

        // Home base sits just left of centre. Two western paths (active early)
        // converge here from the left; four eastern routes (unlocked later)
        // branch in from the right side.
        Vector3 home = new Vector3(1f, -1f, 0f);

        // Six entrances. Spawn 0 (left mid avenue) is always active. The
        // others unlock progressively — see MarathonMode.ActiveSpawnCount.
        p.spawnPointPositions = new Vector3[]
        {
            new Vector3(-14f,  1f,  0f),  // 0 left-mid avenue (always active)
            new Vector3( -9f,  6f,  0f),  // 1 top-left            unlocks wave 5
            new Vector3( 13f,  4f,  0f),  // 2 right-mid upper     unlocks wave 10
            new Vector3(  8f,  6f,  0f),  // 3 top-right           unlocks wave 15
            new Vector3( 14f, -1f,  0f),  // 4 right-mid lower     unlocks wave 25
            new Vector3( 10f, -6f,  0f),  // 5 bottom-right        unlocks wave 35
        };

        var chains = new PathPatternData.SpawnWaypoints[6];

        // 0 — Left mid avenue: long horizontal sweep that hooks down to home
        chains[0] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-14f,  1f, 0f),
            new Vector3(-11f,  1f, 0f),
            new Vector3( -8f,  1.5f, 0f),
            new Vector3( -5f,  1f, 0f),
            new Vector3( -2f,  0f, 0f),
            home,
        }};

        // 1 — Top-left: drops south, then east to merge into the mid avenue
        chains[1] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3( -9f,  6f, 0f),
            new Vector3( -9f,  4f, 0f),
            new Vector3( -7f,  3f, 0f),
            new Vector3( -4f,  2f, 0f),
            new Vector3( -1f,  0.5f, 0f),
            home,
        }};

        // 2 — Right-mid upper: cuts in past the upper buildings
        chains[2] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3( 13f,  4f, 0f),
            new Vector3( 10f,  3.5f, 0f),
            new Vector3(  7f,  2.5f, 0f),
            new Vector3(  4f,  1.5f, 0f),
            new Vector3(  2f,  0.5f, 0f),
            home,
        }};

        // 3 — Top-right: drops down through the eastern tower belt
        chains[3] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(  8f,  6f, 0f),
            new Vector3(  8f,  4f, 0f),
            new Vector3(  6f,  2.5f, 0f),
            new Vector3(  4f,  1f, 0f),
            new Vector3(  2f,  0f, 0f),
            home,
        }};

        // 4 — Right-mid lower: long horizontal in from the eastern edge
        chains[4] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3( 14f, -1f, 0f),
            new Vector3( 11f, -1f, 0f),
            new Vector3(  7f, -1f, 0f),
            new Vector3(  4f, -1f, 0f),
            home,
        }};

        // 5 — Bottom-right: rises through the southern flank
        chains[5] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3( 10f, -6f, 0f),
            new Vector3(  9f, -4f, 0f),
            new Vector3(  6f, -3f, 0f),
            new Vector3(  3f, -2f, 0f),
            home,
        }};

        p.spawnWaypointPositions = chains;
        p.waypointPositions = chains[0].positions;
        p.exitPosition      = home;

        // Tower slots — user-tuned layout.
        p.towerSlotPositions = new Vector3[]
        {
            new Vector3(-13f, 3f, 0f),
            new Vector3(-12f, 3f, 0f),
            new Vector3(-11f, -1f, 0f),
            new Vector3(-10f, 2f, 0f),
            new Vector3(-9f, 0f, 0f),
            new Vector3(-7f, 0f, 0f),
            new Vector3(-6.5f, 4f, 0f),
            new Vector3(-4.5f, 3f, 0f),
            new Vector3(-4f, -0.5f, 0f),
            new Vector3(-2.5f, 2.5f, 0f),
            new Vector3(-1.5f, -1.5f, 0f),
            new Vector3(-0.5f, -2.5f, 0f),
            new Vector3(-0.5f, 1.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(2f, -3f, 0f),
            new Vector3(3f, 2f, 0f),
            new Vector3(5f, -4f, 0f),
            new Vector3(5f, 3.5f, 0f),
            new Vector3(6f, 0.5f, 0f),
            new Vector3(7f, -4.5f, 0f),
            new Vector3(7f, -2f, 0f),
            new Vector3(8.5f, 2f, 0f),
            new Vector3(10f, 5f, 0f),
            new Vector3(11f, -3f, 0f),
            new Vector3(11f, 2f, 0f),
            new Vector3(12f, 1.5f, 0f),
        };
        return p;
    }

    /// <summary>
    /// Build the catalog of buff offers presented during marathon runs. Mix
    /// of stat upgrades (common/rare/epic) plus tower & hero unlocks (epic /
    /// hero rarity).
    /// </summary>
    System.Collections.Generic.List<BuffOffer> MakeBuffPool(TowerData[] towersInPool, GameObject projPrefab)
    {
        var list = new System.Collections.Generic.List<BuffOffer>();

        // ── Stat buffs ─────────────────────────────────────────────────
        list.Add(new BuffOffer {
            title = "Sharp Pencils", description = "+10% damage to all towers.",
            rarity = BuffRarity.Common, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1.10f, rangeMultiplier = 1f, fireRateMultiplier = 1f }
        });
        list.Add(new BuffOffer {
            title = "Caffeine Boost", description = "+15% fire rate to all towers.",
            rarity = BuffRarity.Common, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1f, fireRateMultiplier = 1.15f }
        });
        list.Add(new BuffOffer {
            title = "Reading Glasses", description = "+10% range to all towers.",
            rarity = BuffRarity.Common, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1.10f, fireRateMultiplier = 1f }
        });
        list.Add(new BuffOffer {
            title = "Pocket Money", description = "Gain 100 gold immediately.",
            rarity = BuffRarity.Common, kind = BuffOfferKind.BonusGold, amount = 100
        });
        list.Add(new BuffOffer {
            title = "Highlighter Tips", description = "+20% damage to all towers.",
            rarity = BuffRarity.Rare, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1.20f, rangeMultiplier = 1f, fireRateMultiplier = 1f }
        });
        list.Add(new BuffOffer {
            title = "Espresso Shot", description = "+25% fire rate to all towers.",
            rarity = BuffRarity.Rare, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1f, fireRateMultiplier = 1.25f }
        });
        list.Add(new BuffOffer {
            title = "Library Pass", description = "+1 life and +50 gold every round.",
            rarity = BuffRarity.Rare, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { bonusLives = 1, bonusGoldPerRound = 50 }
        });
        list.Add(new BuffOffer {
            title = "Thesis Mastery", description = "+35% damage and +15% range.",
            rarity = BuffRarity.Epic, kind = BuffOfferKind.StatBuff,
            statBuff = new BuffEffect { damageMultiplier = 1.35f, rangeMultiplier = 1.15f, fireRateMultiplier = 1f }
        });

        // ── Tower unlocks ──────────────────────────────────────────────
        // Cannon and Frost are now unlocked from the start, so they're no
        // longer offered as buff unlocks.

        // ── Hero unlocks (rare drops, hero pity guarantees one within 5 picks) ──
        // Self 2 is granted from the start, so only the two extra professors
        // remain as buff hires.
        TowerData[] heroes = MakeMarathonHeroes(projPrefab);
        list.Add(new BuffOffer {
            title = "Hire: Prof. Lee", description = "Unlocks Prof. Lee \u2014 active AOE blast.",
            rarity = BuffRarity.Hero, kind = BuffOfferKind.UnlockHero, towerToUnlock = heroes[0]
        });
        list.Add(new BuffOffer {
            title = "Hire: Prof. Chan", description = "Unlocks Prof. Chan \u2014 slow field + stealth detection.",
            rarity = BuffRarity.Hero, kind = BuffOfferKind.UnlockHero, towerToUnlock = heroes[1]
        });

        return list;
    }

    /// <summary>Wire each starter tower with two 4-tier upgrade paths plus
    /// two mutually-exclusive capstone perks. Numbers are tuned so a fully
    /// upgraded tower deals meaningful single-target damage but never
    /// out-scales late marathon enemies on its own — hero skills remain
    /// essential against bosses.</summary>
    void AssignTowerUpgrades(TowerData rapid, TowerData balanced, TowerData sniper,
                             TowerData cannon, TowerData frost)
    {
        // ── Rapid ──────────────────────────────────────────────────────────
        // Path 1 — Fire Rate, Path 2 — Damage
        rapid.path1Upgrades = new TowerUpgrade[] {
            U("Trigger Discipline", "+25% fire rate",       40, fireRateMul: 1.25f),
            U("Modded Spring",      "+30% fire rate",       80, fireRateMul: 1.30f),
            U("Hot Barrels",        "+35% fire rate, +10% dmg", 140, fireRateMul: 1.35f, dmgMul: 1.10f),
            U("Auto-Targeting",     "+40% fire rate",      220, fireRateMul: 1.40f),
        };
        rapid.path2Upgrades = new TowerUpgrade[] {
            U("Hollow Points",      "+30% damage",          40, dmgMul: 1.30f),
            U("Match Ammo",         "+35% damage",          80, dmgMul: 1.35f),
            U("Heavy Slug",         "+40% damage, +10% rate",140, dmgMul: 1.40f, fireRateMul: 1.10f),
            U("Tungsten Core",      "+50% damage",         220, dmgMul: 1.50f),
        };
        rapid.capstonePath1 = U("Bullet Storm",
            "Fires twice per shot. Devastates groups.",
            350, extraShots: 1);
        rapid.capstonePath2 = U("Sharpshooter",
            "+50% range, +35% damage. One-shots become normal.",
            350, dmgMul: 1.35f, rangeMul: 1.5f);

        // ── Balanced ───────────────────────────────────────────────────────
        // Path 1 — Damage, Path 2 — Range / utility
        balanced.path1Upgrades = new TowerUpgrade[] {
            U("Sharper Aim",     "+30% damage",          50, dmgMul: 1.30f),
            U("Reinforced Shot", "+35% damage",         100, dmgMul: 1.35f),
            U("Concussive Round","+40% damage, +10% rate",170, dmgMul: 1.40f, fireRateMul: 1.10f),
            U("Lethal Payload",  "+45% damage",         260, dmgMul: 1.45f),
        };
        balanced.path2Upgrades = new TowerUpgrade[] {
            U("Long Sights",   "+25% range",            50, rangeMul: 1.25f),
            U("Telemetry",     "+25% range, +15% rate", 100, rangeMul: 1.25f, fireRateMul: 1.15f),
            U("Recon Drone",   "+30% range",           170, rangeMul: 1.30f),
            U("Stable Mount",  "+25% rate, +15% dmg",  260, fireRateMul: 1.25f, dmgMul: 1.15f),
        };
        balanced.capstonePath1 = U("Heavy Hitter",
            "+60% damage, gains 0.6 splash radius.",
            380, dmgMul: 1.60f, bonusSplash: 0.6f);
        balanced.capstonePath2 = U("Battlefield Control",
            "+40% range, +30% fire rate.",
            380, rangeMul: 1.40f, fireRateMul: 1.30f);

        // ── Sniper ─────────────────────────────────────────────────────────
        // Path 1 — Damage, Path 2 — Range / Pierce utility
        sniper.path1Upgrades = new TowerUpgrade[] {
            U("Match Barrel",   "+35% damage",         70, dmgMul: 1.35f),
            U("HV Round",       "+40% damage",         140, dmgMul: 1.40f),
            U("Armour Piercer", "+45% damage, +10% rate",230, dmgMul: 1.45f, fireRateMul: 1.10f),
            U("Lethal Caliber", "+50% damage",         340, dmgMul: 1.50f),
        };
        sniper.path2Upgrades = new TowerUpgrade[] {
            U("Spotter",   "+25% range, +15% rate",   70, rangeMul: 1.25f, fireRateMul: 1.15f),
            U("Tripod",    "+20% range, +20% rate",  140, rangeMul: 1.20f, fireRateMul: 1.20f),
            U("Eagle Eye", "+30% range",             230, rangeMul: 1.30f),
            U("Rapid Bolt","+35% rate",              340, fireRateMul: 1.35f),
        };
        sniper.capstonePath1 = U("Execute",
            "Massive +120% damage. Annihilates elites.",
            450, dmgMul: 2.20f);
        sniper.capstonePath2 = U("Marksman's Volley",
            "Fires twice per shot at +20% range.",
            450, extraShots: 1, rangeMul: 1.20f);

        // ── AOE Cannon ─────────────────────────────────────────────────────
        // Path 1 — Splash radius / utility, Path 2 — Damage
        cannon.path1Upgrades = new TowerUpgrade[] {
            U("Wider Bore",     "+0.4 splash radius",      80, bonusSplash: 0.4f),
            U("Shrapnel Mix",   "+0.4 splash, +10% rate",  150, bonusSplash: 0.4f, fireRateMul: 1.10f),
            U("Cluster Shells", "+0.5 splash radius",      240, bonusSplash: 0.5f),
            U("Saturation Fire","+15% rate, +10% range",   340, fireRateMul: 1.15f, rangeMul: 1.10f),
        };
        cannon.path2Upgrades = new TowerUpgrade[] {
            U("Heavy Powder",   "+30% damage",         80, dmgMul: 1.30f),
            U("Dense Slug",     "+35% damage",         150, dmgMul: 1.35f),
            U("Demolition Charge","+40% damage, +0.2 splash",240, dmgMul: 1.40f, bonusSplash: 0.2f),
            U("HE Round",       "+45% damage",         340, dmgMul: 1.45f),
        };
        cannon.capstonePath1 = U("Earthshaker",
            "Massive +1.2 splash radius, splash deals 90% of base.",
            500, bonusSplash: 1.2f, splashFracMul: 1.5f);
        cannon.capstonePath2 = U("Demolition Expert",
            "+80% damage, +0.4 splash radius.",
            500, dmgMul: 1.80f, bonusSplash: 0.4f);

        // ── Frost Tower ────────────────────────────────────────────────────
        // Path 1 — Slow strength/duration, Path 2 — Damage / splash
        frost.path1Upgrades = new TowerUpgrade[] {
            U("Sharper Frost",  "Stronger slow",          70, slowMulScale: 0.85f),
            U("Cold Front",     "+1s slow duration",     140, bonusSlowDur: 1f),
            U("Glacial Touch",  "Stronger slow, +0.5s dur",230, slowMulScale: 0.85f, bonusSlowDur: 0.5f),
            U("Permafrost Aura","+0.3 splash radius",    320, bonusSplash: 0.3f),
        };
        frost.path2Upgrades = new TowerUpgrade[] {
            U("Ice Lance",   "+40% damage",          70, dmgMul: 1.40f),
            U("Shatter",     "+45% damage",         140, dmgMul: 1.45f),
            U("Frost Shards","+30% rate, +15% dmg", 230, fireRateMul: 1.30f, dmgMul: 1.15f),
            U("Cryo Charge", "+50% damage",         320, dmgMul: 1.50f),
        };
        frost.capstonePath1 = U("Deep Freeze",
            "Slows are near-stop and last +2.5s longer.",
            450, slowMulScale: 0.55f, bonusSlowDur: 2.5f);
        frost.capstonePath2 = U("Frostbite",
            "+70% damage, +0.4 splash, splash deals 100% of base.",
            450, dmgMul: 1.70f, bonusSplash: 0.4f, splashFracMul: 1.4f);
    }

    /// <summary>Compact constructor for an upgrade definition. Every numeric
    /// argument has a sensible default so callers only pass what changes.</summary>
    static TowerUpgrade U(string name, string desc, int cost,
                          float dmgMul = 1f, int bonusDmg = 0,
                          float rangeMul = 1f, float bonusRange = 0f,
                          float fireRateMul = 1f, float bonusFireRate = 0f,
                          int   extraShots = 0,
                          float bonusSplash = 0f, float splashFracMul = 1f,
                          float slowMulScale = 1f, float bonusSlowDur = 0f,
                          float skillCdMul = 1f, float skillEffMul = 1f,
                          float bonusSkillRadius = 0f, float bonusSkillDur = 0f)
    {
        return new TowerUpgrade {
            upgradeName = name, description = desc, cost = cost,
            damageMultiplier   = dmgMul,   bonusDamage = bonusDmg,
            rangeMultiplier    = rangeMul, bonusRange  = bonusRange,
            fireRateMultiplier = fireRateMul, bonusFireRate = bonusFireRate,
            extraShotsPerVolley = extraShots,
            bonusSplashRadius   = bonusSplash,
            splashFractionMultiplier = splashFracMul,
            slowMultiplierScale = slowMulScale,
            bonusSlowDuration   = bonusSlowDur,
            upgradeSkillCooldownMultiplier = skillCdMul,
            upgradeSkillEffectMultiplier   = skillEffMul,
            upgradeSkillRadiusBonus        = bonusSkillRadius,
            upgradeSkillDurationBonus      = bonusSkillDur,
        };
    }

    TowerData[] MakeMarathonHeroes(GameObject projPrefab)
    {
        HeroSkillData blastSkill = ScriptableObject.CreateInstance<HeroSkillData>();
        blastSkill.skillName   = "Lecture Burst"; blastSkill.cooldown = 12f;
        blastSkill.effect      = HeroSkillEffect.AoEBlast; blastSkill.radius = 3.5f; blastSkill.blastDamage = 100;

        HeroSkillData slowSkill = ScriptableObject.CreateInstance<HeroSkillData>();
        slowSkill.skillName    = "Freeze Frame"; slowSkill.cooldown = 15f;
        slowSkill.effect       = HeroSkillEffect.SlowField; slowSkill.radius = 4f;
        slowSkill.slowMultiplier = 0.35f; slowSkill.slowDuration = 4f;

        HeroSkillData ground   = ScriptableObject.CreateInstance<HeroSkillData>();
        ground.skillName       = "Study Group"; ground.cooldown = 20f;
        ground.effect          = HeroSkillEffect.GroundTargetedAOE; ground.radius = 2.5f;
        ground.aoeDamagePerTick = 15; ground.aoeTickInterval = 0.4f; ground.aoeDuration = 3f;
        ground.aoeDamageType   = DamageType.Pierce;

        TowerData prof1 = ScriptableObject.CreateInstance<TowerData>();
        prof1.towerName = "Prof. Lee"; prof1.towerType = TowerType.Professor;
        prof1.cost = 120; prof1.isProfessorTower = true; prof1.unique = true;
        prof1.heroSkill = blastSkill; prof1.projectilePrefab = projPrefab;

        TowerData prof2 = ScriptableObject.CreateInstance<TowerData>();
        prof2.towerName = "Prof. Chan"; prof2.towerType = TowerType.Professor;
        prof2.cost = 140; prof2.isProfessorTower = true; prof2.unique = true;
        prof2.heroSkill = slowSkill; prof2.hasDetection = true; prof2.projectilePrefab = projPrefab;

        TowerData self2 = ScriptableObject.CreateInstance<TowerData>();
        self2.towerName = "Self 2"; self2.towerType = TowerType.Professor;
        self2.cost = 100; self2.isProfessorTower = true; self2.unique = true;
        self2.heroSkill = ground; self2.projectilePrefab = projPrefab;

        return new TowerData[] { prof1, prof2, self2 };
    }

    // ── Faculty professors (TW Chim / Hayden So / Greg Wu / Wilton) ──────────
    //
    // Each professor has TWO upgrade paths:
    //   Path 1 = basic-attack scaling (damage / range / fire rate / on-hit gimmick)
    //   Path 2 = active-skill scaling (cooldown reduction, magnitude, radius, duration)
    //
    // Costs / numbers are tuned to feel like meaningful but not strictly-required
    // boosts; capstones lean into each professor's identity.
    TowerData[] MakeFacultyProfessors(GameObject projPrefab)
    {
        // ── TW Chim ──────────────────────────────────────────────────────
        // Active: King Crimson — instantly skips the current round (once per game).
        // Basic : Punchy single-target attacks that "stop" enemies for 0.3s
        //          (very deep slow approximating a stun) on every hit.
        HeroSkillData kingCrimson = ScriptableObject.CreateInstance<HeroSkillData>();
        kingCrimson.skillName     = "King Crimson";
        kingCrimson.description   = "Erase time. Instantly clear every enemy on the map and end the current round. Single-use per game.";
        kingCrimson.cooldown      = 999f;
        kingCrimson.effect        = HeroSkillEffect.SkipRound;
        kingCrimson.oncePerGame   = true;
        kingCrimson.audioClipResource = "kingcrimson";
        kingCrimson.audioVolume   = 0.9f;

        TowerData twChim = ScriptableObject.CreateInstance<TowerData>();
        twChim.towerName        = "TW Chim";
        twChim.towerType        = TowerType.Professor;
        twChim.cost             = 180;
        twChim.isProfessorTower = true;
        twChim.unique           = true;
        twChim.heroSkill        = kingCrimson;
        twChim.projectilePrefab = projPrefab;
        twChim.range            = 3.2f;
        twChim.fireRate         = 1.1f;
        twChim.damage           = 18;
        twChim.slowOnHitMultiplier = 0.05f;  // ~stop on hit
        twChim.slowOnHitDuration   = 0.3f;
        twChim.path1Upgrades = new TowerUpgrade[] {
            U("Iron Grip",      "On-hit stop lasts +0.1s.",                     60,  bonusSlowDur: 0.1f),
            U("Sharper Glare",  "+25% damage.",                                 110, dmgMul: 1.25f),
            U("Fast Hands",     "+25% fire rate.",                              170, fireRateMul: 1.25f),
            U("Time Halt",      "On-hit stop lasts +0.2s, +20% range.",         260, bonusSlowDur: 0.2f, rangeMul: 1.20f),
        };
        twChim.path2Upgrades = new TowerUpgrade[] {
            U("Echo of Time",   "King Crimson's bullet-time after-effect lingers (cosmetic).", 80),
            U("Faster Recall",  "(no extra cooldown reduction; already once-per-game.)",       150),
            U("Wider Erasure",  "King Crimson also refunds 100 gold on use.",                  220),
            U("Mastered Form",  "King Crimson also restores 1 life on use.",                   320),
        };
        twChim.capstonePath1 = U("Stand User", "+50% damage, +30% fire rate. Stops endure +0.3s.",
            400, dmgMul: 1.5f, fireRateMul: 1.30f, bonusSlowDur: 0.3f);
        twChim.capstonePath2 = U("Reset The Universe",
            "King Crimson can be cast a SECOND time per game.",
            400);
        // Note: the second-cast capstone is data-only; HeroTower's once-per-game
        // gate would need a runtime flag to honour it. Marked as a TODO below.

        // ── Hayden So ────────────────────────────────────────────────────
        // Active: Mucodec — drops an AOE that yanks enemies toward its center.
        // Basic : Standard projectile attack.
        HeroSkillData mucodec = ScriptableObject.CreateInstance<HeroSkillData>();
        mucodec.skillName        = "Mucodec";
        mucodec.description      = "Throw a research field that drags every enemy in radius toward its center for 1.5s.";
        mucodec.cooldown         = 14f;
        mucodec.effect           = HeroSkillEffect.PullToCenter;
        mucodec.radius           = 3.5f;
        mucodec.pullStrength     = 4.5f;
        mucodec.pullDuration     = 1.5f;
        mucodec.audioClipResource = "mucodec";

        TowerData hayden = ScriptableObject.CreateInstance<TowerData>();
        hayden.towerName        = "Hayden So";
        hayden.towerType        = TowerType.Professor;
        hayden.cost             = 150;
        hayden.isProfessorTower = true;
        hayden.unique           = true;
        hayden.heroSkill        = mucodec;
        hayden.projectilePrefab = projPrefab;
        hayden.range            = 4.0f;
        hayden.fireRate         = 1.0f;
        hayden.damage           = 22;
        hayden.path1Upgrades = new TowerUpgrade[] {
            U("Lecture Notes",    "+25% damage.",          50,  dmgMul: 1.25f),
            U("Diction Drills",   "+25% fire rate.",       100, fireRateMul: 1.25f),
            U("Wide Audience",    "+25% range.",           160, rangeMul: 1.25f),
            U("Voice Projection", "+30% damage, +15% rate.",240, dmgMul: 1.30f, fireRateMul: 1.15f),
        };
        hayden.path2Upgrades = new TowerUpgrade[] {
            U("Magnetic Theory",  "Mucodec radius +0.75.",                      70,  bonusSkillRadius: 0.75f),
            U("Stronger Pull",    "Mucodec drag strength +50%.",                130, skillEffMul: 1.5f),
            U("Long Hold",        "Mucodec lasts +1.0s.",                       200, bonusSkillDur: 1.0f),
            U("Refined Encoder",  "Mucodec cooldown -25%.",                     280, skillCdMul: 0.75f),
        };
        hayden.capstonePath1 = U("Vocal Snipe", "+60% damage, +30% range.",
            380, dmgMul: 1.6f, rangeMul: 1.30f);
        hayden.capstonePath2 = U("Black Hole Codec",
            "Mucodec radius +1.5 and pulls 2x as hard.",
            380, bonusSkillRadius: 1.5f, skillEffMul: 2f);

        // ── Greg Wu ──────────────────────────────────────────────────────
        // Active: Uh huh — empower aura that boosts surrounding tower damage.
        // Basic : Standard projectile attack.
        HeroSkillData uhhuh = ScriptableObject.CreateInstance<HeroSkillData>();
        uhhuh.skillName         = "Uh huh";
        uhhuh.description       = "Greg validates every nearby tower. All towers in radius deal 75% extra damage for 8s.";
        uhhuh.cooldown          = 18f;
        uhhuh.effect            = HeroSkillEffect.EmpowerAllies;
        uhhuh.radius            = 4f;
        uhhuh.empowerMultiplier = 1.75f;
        uhhuh.empowerDuration   = 8f;

        TowerData greg = ScriptableObject.CreateInstance<TowerData>();
        greg.towerName        = "Greg Wu";
        greg.towerType        = TowerType.Professor;
        greg.cost             = 140;
        greg.isProfessorTower = true;
        greg.unique           = true;
        greg.heroSkill        = uhhuh;
        greg.projectilePrefab = projPrefab;
        greg.range            = 3.5f;
        greg.fireRate         = 1.2f;
        greg.damage           = 20;
        greg.path1Upgrades = new TowerUpgrade[] {
            U("Pointed Question", "+25% damage.",       50,  dmgMul: 1.25f),
            U("Quick Tutorial",   "+30% fire rate.",    100, fireRateMul: 1.30f),
            U("Office Hours",     "+30% range.",        170, rangeMul: 1.30f),
            U("Marker Storm",     "+1 extra shot per volley.", 260, extraShots: 1),
        };
        greg.path2Upgrades = new TowerUpgrade[] {
            U("Big Class",         "Uh huh radius +1.0.",                  70,  bonusSkillRadius: 1.0f),
            U("Hype Speech",       "Uh huh damage bonus +50% stronger.",    130, skillEffMul: 1.5f),
            U("Long Lecture",      "Uh huh lasts +4s.",                     200, bonusSkillDur: 4f),
            U("Lightning Recall",  "Uh huh cooldown -30%.",                 280, skillCdMul: 0.70f),
        };
        greg.capstonePath1 = U("Lecture Hall", "+50% damage, +25% range, +1 extra shot.",
            380, dmgMul: 1.5f, rangeMul: 1.25f, extraShots: 1);
        greg.capstonePath2 = U("Standing Ovation",
            "Uh huh radius +2 and bonus is doubled.",
            380, bonusSkillRadius: 2f, skillEffMul: 2f);

        // ── Wilton ───────────────────────────────────────────────────────
        // Active: Make Engineering Great Again — attack-speed aura on towers in radius.
        // Basic : Weak attacks BUT a passive +25% gold aura.
        HeroSkillData mega = ScriptableObject.CreateInstance<HeroSkillData>();
        mega.skillName              = "Make Engineering Great Again";
        mega.description            = "Rallying call. Every tower in radius gains +60% fire rate for 6s.";
        mega.cooldown               = 16f;
        mega.effect                 = HeroSkillEffect.AttackSpeedAura;
        mega.radius                 = 4.5f;
        mega.attackSpeedMultiplier  = 1.6f;
        mega.attackSpeedDuration    = 6f;
        mega.audioClipResource      = "mega";

        TowerData wilton = ScriptableObject.CreateInstance<TowerData>();
        wilton.towerName        = "Wilton";
        wilton.towerType        = TowerType.Professor;
        wilton.cost             = 160;
        wilton.isProfessorTower = true;
        wilton.unique           = true;
        wilton.heroSkill        = mega;
        wilton.projectilePrefab = projPrefab;
        wilton.range            = 3.0f;
        wilton.fireRate         = 0.9f;
        wilton.damage           = 8;            // intentionally weak
        wilton.goldGainAura     = 0.25f;        // +25% gold while alive
        wilton.path1Upgrades = new TowerUpgrade[] {
            U("Bigger Wallet",    "Gold aura +10% (35% total).",              70),
            U("Sharper Suit",     "+50% damage (still light).",               110, dmgMul: 1.5f),
            U("Brand Deal",       "Gold aura +15% (50% total).",              180),
            U("Power Move",       "+50% damage, +20% fire rate.",             260, dmgMul: 1.5f, fireRateMul: 1.20f),
        };
        // Path 1 ramp on the gold aura is intentionally tracked via capstone
        // since base goldGainAura is a TowerData field; we use the capstone to
        // bump it. (Tier-by-tier aura growth would need extra plumbing.)
        wilton.path2Upgrades = new TowerUpgrade[] {
            U("Loud Speakers",    "MEGA radius +1.0.",                        70,  bonusSkillRadius: 1.0f),
            U("Rocket Fuel",      "MEGA buff +40% stronger.",                 130, skillEffMul: 1.4f),
            U("Rally Time",       "MEGA lasts +3s.",                          200, bonusSkillDur: 3f),
            U("Quick Slogans",    "MEGA cooldown -30%.",                      280, skillCdMul: 0.70f),
        };
        wilton.capstonePath1 = U("Sponsor Megadeal",
            "+100% damage. While Wilton is alive gold gain is doubled.",
            400, dmgMul: 2f);
        wilton.capstonePath2 = U("Industrial Revolution",
            "MEGA radius +2, lasts +4s, fires 30% faster cooldown.",
            400, bonusSkillRadius: 2f, bonusSkillDur: 4f, skillCdMul: 0.70f);

        return new TowerData[] { twChim, hayden, greg, wilton };
    }

    // ── Level & faculty data ──────────────────────────────────────────────────

    LevelData MakeLevel(WaveData[] waves, PathPatternData path)
    {
        LevelData ld = ScriptableObject.CreateInstance<LevelData>();
        ld.courseCode         = "QUICKTEST";
        ld.courseTier         = 1;
        ld.creditsReward      = 6;
        ld.skillPointsReward  = 2;
        ld.startingGold       = startingGold;
        ld.startingLives      = startingLives;
        ld.rounds             = waves;
        ld.pathDifficultyTier = 1;
        return ld;
    }

    FacultyData MakeFaculty(LevelData level, TowerData[] towers, bool withProfessor)
    {
        FacultyData f = ScriptableObject.CreateInstance<FacultyData>();
        f.facultyName = "TEST";
        f.fullName    = "Quick Test Faculty";
        f.courses     = new LevelData[] { level };

        // Wire the professor tower to the faculty so the lock-check passes
        if (withProfessor)
        {
            foreach (TowerData td in towers)
            {
                if (td.isProfessorTower) { f.professorTower = td; break; }
            }
        }

        // Tell GameManager about this faculty
        GameManager.Instance.allFaculties = new FacultyData[] { f };
        return f;
    }
}
