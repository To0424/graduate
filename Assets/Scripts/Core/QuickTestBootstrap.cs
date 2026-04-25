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
            Debug.Log($"[Marathon] Bootstrapped \u2014 path '{path.patternName}', " +
                      $"{MarathonMode.SpawnPointCount} spawns defined, " +
                      $"{MarathonMode.BuffPool.Count} buff offers in pool.");
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

        SkillTreeData st = ScriptableObject.CreateInstance<SkillTreeData>();
        st.nodes = new SkillNode[]
        {
            new SkillNode
            {
                nodeName              = "Tutoring",
                description           = "+10% damage",
                cost                  = 1,
                section               = SkillSection.Social,
                buff                  = new BuffEffect { damageMultiplier = 1.1f,
                                                         rangeMultiplier  = 1f,
                                                         fireRateMultiplier = 1f },
                prerequisiteNodeNames = new string[0]
            },
            new SkillNode
            {
                nodeName              = "Study Group",
                description           = "+1 bonus life",
                cost                  = 1,
                section               = SkillSection.Social,
                buff                  = new BuffEffect { bonusLives = 1 },
                prerequisiteNodeNames = new string[] { "Tutoring" }
            },
            new SkillNode
            {
                nodeName              = "Part-time Cashier",
                description           = "+20 starting gold",
                cost                  = 1,
                section               = SkillSection.PartTimeWork,
                buff                  = new BuffEffect { bonusStartGold = 20 },
                prerequisiteNodeNames = new string[0]
            },
            new SkillNode
            {
                nodeName              = "Cup of Coffee",
                description           = "+20 gold at the start of every round.",
                cost                  = 1,
                section               = SkillSection.PartTimeWork,
                buff                  = new BuffEffect { bonusGoldPerRound = 20 },
                prerequisiteNodeNames = new string[0]
            }
        };

        SkillTreeManager stm = go.AddComponent<SkillTreeManager>();
        stm.skillTreeData = st;

        go.AddComponent<CreditManager>();
        go.AddComponent<SkillPointManager>();

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
        basic.archetype  = EnemyArchetype.Standard;

        EnemyData fast = ScriptableObject.CreateInstance<EnemyData>();
        fast.enemyName   = "Fast Bug";
        fast.moveSpeed   = 4f;
        fast.maxHealth   = 50;
        fast.goldReward  = 15;
        fast.courseTier  = 1;
        fast.archetype   = EnemyArchetype.Standard;

        EnemyData tank = ScriptableObject.CreateInstance<EnemyData>();
        tank.enemyName   = "Tank Bug";
        tank.moveSpeed   = 1.2f;
        tank.maxHealth   = 250;
        tank.goldReward  = 25;
        tank.courseTier  = 2;
        tank.archetype   = EnemyArchetype.Standard;

        EnemyData shielded = ScriptableObject.CreateInstance<EnemyData>();
        shielded.enemyName   = "Shielded Bug";
        shielded.moveSpeed   = 1.8f;
        shielded.maxHealth   = 120;
        shielded.goldReward  = 20;
        shielded.courseTier  = 2;
        shielded.archetype   = EnemyArchetype.Shielded;
        shielded.shieldHealth = 80;

        EnemyData stealth = ScriptableObject.CreateInstance<EnemyData>();
        stealth.enemyName  = "Stealth Bug";
        stealth.moveSpeed  = 3f;
        stealth.maxHealth  = 60;
        stealth.goldReward = 30;
        stealth.courseTier = 2;
        stealth.archetype  = EnemyArchetype.Stealth;

        EnemyData boss = ScriptableObject.CreateInstance<EnemyData>();
        boss.enemyName   = "Chatterbox";
        boss.moveSpeed   = 0.9f;
        boss.maxHealth   = 800;
        boss.goldReward  = 80;
        boss.courseTier  = 3;
        boss.archetype   = EnemyArchetype.Boss;
        boss.bossScale   = 2.2f;

        // Splitter child template — small basic that splits make.
        EnemyData splitChild = ScriptableObject.CreateInstance<EnemyData>();
        splitChild.enemyName   = "Splitter Spawn";
        splitChild.moveSpeed   = 3f;
        splitChild.maxHealth   = 35;
        splitChild.goldReward  = 5;
        splitChild.courseTier  = 1;
        splitChild.archetype   = EnemyArchetype.Standard;

        EnemyData splitter = ScriptableObject.CreateInstance<EnemyData>();
        splitter.enemyName  = "Splitter Bug";
        splitter.moveSpeed  = 1.6f;
        splitter.maxHealth  = 140;
        splitter.goldReward = 20;
        splitter.courseTier = 2;
        splitter.archetype  = EnemyArchetype.Splitter;
        splitter.splitInto  = splitChild;
        splitter.splitCount = 2;

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

        return new TowerData[] { rapid, balanced, sniper, cannon, frost, prof1, prof2, self2 };
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
    /// Marathon prototype map — a long horizontal campus layout shaped after
    /// the HKU campus reference image. Three spawn points line the western
    /// edge (only spawn 0 is active at the start; <c>MarathonRunController</c>
    /// unlocks the others at waves 10 and 25). All three routes converge on a
    /// single home base on the eastern edge.
    /// </summary>
    PathPatternData MakeMarathonPath()
    {
        PathPatternData p = ScriptableObject.CreateInstance<PathPatternData>();
        p.patternName    = "Marathon Campus";
        p.difficultyTier = 2;

        Vector3 home = new Vector3(13f, 0f, 0f);

        // Three western entrances (top / centre / bottom).
        p.spawnPointPositions = new Vector3[]
        {
            new Vector3(-14f,  0f,  0f),  // mid — active from wave 1
            new Vector3(-14f,  4f,  0f),  // top — unlocks wave 10
            new Vector3(-14f, -4f,  0f),  // bottom — unlocks wave 25
        };

        var chains = new PathPatternData.SpawnWaypoints[3];
        // Mid route — main avenue down the middle of campus
        chains[0] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-14f,  0f, 0f),
            new Vector3( -8f,  0f, 0f),
            new Vector3( -3f,  1f, 0f),
            new Vector3(  2f,  0f, 0f),
            new Vector3(  7f,  1f, 0f),
            new Vector3( 11f,  0f, 0f),
            home,
        }};
        // Top route — the high road, looping around the upper buildings
        chains[1] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-14f,  4f, 0f),
            new Vector3( -9f,  4f, 0f),
            new Vector3( -5f,  4f, 0f),
            new Vector3(  0f,  4f, 0f),
            new Vector3(  5f,  3f, 0f),
            new Vector3(  9f,  2f, 0f),
            new Vector3( 11f,  0f, 0f),
            home,
        }};
        // Bottom route — around the southern footpath
        chains[2] = new PathPatternData.SpawnWaypoints { positions = new Vector3[] {
            new Vector3(-14f, -4f, 0f),
            new Vector3( -9f, -4f, 0f),
            new Vector3( -4f, -3f, 0f),
            new Vector3(  1f, -2f, 0f),
            new Vector3(  6f, -2f, 0f),
            new Vector3(  9f, -1f, 0f),
            new Vector3( 11f,  0f, 0f),
            home,
        }};
        p.spawnWaypointPositions = chains;
        p.waypointPositions = chains[0].positions;
        p.exitPosition      = home;

        // Tower slots scattered along the safe areas between routes.
        p.towerSlotPositions = new Vector3[]
        {
            // Upper safe band (between top & mid routes)
            new Vector3(-10f,  2f, 0f), new Vector3(-6f,  2f, 0f), new Vector3(-2f,  2.5f, 0f),
            new Vector3( 2f,  2f, 0f), new Vector3( 6f,  2f, 0f),  new Vector3( 9f,  1f, 0f),
            // Lower safe band (between mid & bottom routes)
            new Vector3(-10f, -2f, 0f), new Vector3(-6f, -1.5f, 0f), new Vector3(-2f, -1f, 0f),
            new Vector3( 2f, -1f, 0f), new Vector3( 6f, -0.5f, 0f), new Vector3( 9f, -1f, 0f),
            // Approach to the home base — last line of defence
            new Vector3(11.5f, 1.5f, 0f), new Vector3(11.5f, -1.5f, 0f),
            // Outer flanks (off the paths)
            new Vector3(-12f,  2f, 0f), new Vector3(-12f, -2f, 0f),
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
        // Pull cannon / frost out of the available list.
        TowerData cannon = null, frost = null;
        foreach (var t in towersInPool)
        {
            if (t == null) continue;
            if (t.towerType == TowerType.Cannon) cannon = t;
            if (t.towerType == TowerType.Frost)  frost  = t;
        }
        if (cannon != null)
            list.Add(new BuffOffer {
                title = "Engineering Lab", description = "Unlocks the AOE Cannon tower.",
                rarity = BuffRarity.Epic, kind = BuffOfferKind.UnlockTower, towerToUnlock = cannon
            });
        if (frost != null)
            list.Add(new BuffOffer {
                title = "Cold Storage", description = "Unlocks the Frost Tower (slows enemies).",
                rarity = BuffRarity.Epic, kind = BuffOfferKind.UnlockTower, towerToUnlock = frost
            });

        // ── Hero unlocks (rare drops, hero pity guarantees one within 5 picks) ──
        // Build hero TowerData on-the-fly so they always exist regardless of
        // includeProfessor (marathon hides professors from initial shop).
        TowerData[] heroes = MakeMarathonHeroes(projPrefab);
        list.Add(new BuffOffer {
            title = "Hire: Prof. Lee", description = "Unlocks Prof. Lee \u2014 active AOE blast.",
            rarity = BuffRarity.Hero, kind = BuffOfferKind.UnlockHero, towerToUnlock = heroes[0]
        });
        list.Add(new BuffOffer {
            title = "Hire: Prof. Chan", description = "Unlocks Prof. Chan \u2014 slow field + stealth detection.",
            rarity = BuffRarity.Hero, kind = BuffOfferKind.UnlockHero, towerToUnlock = heroes[1]
        });
        list.Add(new BuffOffer {
            title = "Hire: Self 2", description = "Unlocks Self 2 \u2014 ground-targeted DoT pool.",
            rarity = BuffRarity.Hero, kind = BuffOfferKind.UnlockHero, towerToUnlock = heroes[2]
        });

        return list;
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
