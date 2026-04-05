#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// One-click wizard that creates all starter prefabs, ScriptableObjects, and wires them together.
/// Access via menu: Graduation → Setup Starter Data
/// </summary>
public class ProjectSetupWizard
{
    [MenuItem("Graduation/Setup Starter Data")]
    public static void SetupAll()
    {
        // Create folders
        CreateFolder("Assets", "Prefabs");
        CreateFolder("Assets", "Data");
        CreateFolder("Assets/Data", "Enemies");
        CreateFolder("Assets/Data", "Towers");
        CreateFolder("Assets/Data", "Levels");
        CreateFolder("Assets/Data", "Faculties");
        CreateFolder("Assets/Data", "Waves");
        CreateFolder("Assets/Data", "Paths");

        // 1. Create Projectile prefab
        GameObject projObj = new GameObject("Projectile");
        SpriteRenderer projSR = projObj.AddComponent<SpriteRenderer>();
        projSR.color = Color.yellow;
        projSR.sortingOrder = 5;
        projObj.AddComponent<Projectile>();
        CircleCollider2D projCol = projObj.AddComponent<CircleCollider2D>();
        projCol.isTrigger = true;
        projCol.radius = 0.15f;
        Rigidbody2D projRB = projObj.AddComponent<Rigidbody2D>();
        projRB.gravityScale = 0;
        projRB.isKinematic = true;
        GameObject projPrefab = SavePrefab(projObj, "Assets/Prefabs/Projectile.prefab");
        Object.DestroyImmediate(projObj);

        // 2. Create Enemy prefab
        GameObject enemyObj = new GameObject("Enemy");
        SpriteRenderer enemySR = enemyObj.AddComponent<SpriteRenderer>();
        enemySR.color = Color.red;
        enemySR.sortingOrder = 3;
        enemyObj.AddComponent<Enemy>();
        CircleCollider2D enemyCol = enemyObj.AddComponent<CircleCollider2D>();
        enemyCol.isTrigger = true;
        enemyCol.radius = 0.3f;
        GameObject enemyPrefab = SavePrefab(enemyObj, "Assets/Prefabs/Enemy.prefab");
        Object.DestroyImmediate(enemyObj);

        // 3. Create Tower prefab
        GameObject towerObj = new GameObject("Tower");
        SpriteRenderer towerSR = towerObj.AddComponent<SpriteRenderer>();
        towerSR.color = Color.blue;
        towerSR.sortingOrder = 4;
        towerObj.AddComponent<Tower>();
        // FirePoint child
        GameObject fp = new GameObject("FirePoint");
        fp.transform.SetParent(towerObj.transform);
        fp.transform.localPosition = new Vector3(0, 0.4f, 0);
        GameObject towerPrefab = SavePrefab(towerObj, "Assets/Prefabs/Tower.prefab");
        Object.DestroyImmediate(towerObj);

        // 4. Create EnemyData assets
        EnemyData basicBug = CreateAsset<EnemyData>("Assets/Data/Enemies/BasicBug.asset");
        basicBug.enemyName = "Basic Bug";
        basicBug.moveSpeed = 2f;
        basicBug.maxHealth = 80;
        basicBug.goldReward = 10;
        basicBug.courseTier = 1;
        EditorUtility.SetDirty(basicBug);

        EnemyData fastBug = CreateAsset<EnemyData>("Assets/Data/Enemies/FastBug.asset");
        fastBug.enemyName = "Fast Bug";
        fastBug.moveSpeed = 4f;
        fastBug.maxHealth = 50;
        fastBug.goldReward = 15;
        fastBug.courseTier = 1;
        EditorUtility.SetDirty(fastBug);

        EnemyData tankBug = CreateAsset<EnemyData>("Assets/Data/Enemies/TankBug.asset");
        tankBug.enemyName = "Tank Bug";
        tankBug.moveSpeed = 1.2f;
        tankBug.maxHealth = 250;
        tankBug.goldReward = 25;
        tankBug.courseTier = 2;
        EditorUtility.SetDirty(tankBug);

        // 5. Create TowerData assets  (with projectile prefab reference)
        TowerData rapidTower = CreateAsset<TowerData>("Assets/Data/Towers/RapidTower.asset");
        rapidTower.towerName = "Rapid Tower";
        rapidTower.towerType = TowerType.Rapid;
        rapidTower.cost = 40;
        rapidTower.range = 2.5f;
        rapidTower.fireRate = 3f;
        rapidTower.damage = 10;
        rapidTower.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(rapidTower);

        TowerData balancedTower = CreateAsset<TowerData>("Assets/Data/Towers/BalancedTower.asset");
        balancedTower.towerName = "Balanced Tower";
        balancedTower.towerType = TowerType.Balanced;
        balancedTower.cost = 60;
        balancedTower.range = 3f;
        balancedTower.fireRate = 1.5f;
        balancedTower.damage = 25;
        balancedTower.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(balancedTower);

        TowerData sniperTower = CreateAsset<TowerData>("Assets/Data/Towers/SniperTower.asset");
        sniperTower.towerName = "Sniper Tower";
        sniperTower.towerType = TowerType.Sniper;
        sniperTower.cost = 100;
        sniperTower.range = 5f;
        sniperTower.fireRate = 0.5f;
        sniperTower.damage = 80;
        sniperTower.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(sniperTower);

        TowerData profTower = CreateAsset<TowerData>("Assets/Data/Towers/ProfessorTower_EEE.asset");
        profTower.towerName = "Prof. EEE";
        profTower.towerType = TowerType.Professor;
        profTower.cost = 150;
        profTower.range = 4f;
        profTower.fireRate = 2f;
        profTower.damage = 40;
        profTower.isProfessorTower = true;
        profTower.requiredFaculty = "EEE";
        profTower.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(profTower);

        // 6. Create WaveData assets
        WaveData wave1 = CreateAsset<WaveData>("Assets/Data/Waves/Wave_R1.asset");
        wave1.waveName = "Round 1";
        wave1.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = basicBug, count = 5, spawnInterval = 1.2f, spawnPointIndex = 0 }
        };
        wave1.delayBetweenGroups = 2f;
        EditorUtility.SetDirty(wave1);

        WaveData wave2 = CreateAsset<WaveData>("Assets/Data/Waves/Wave_R2.asset");
        wave2.waveName = "Round 2";
        wave2.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = basicBug, count = 8, spawnInterval = 1f, spawnPointIndex = 0 },
            new EnemyGroup { enemyType = fastBug, count = 3, spawnInterval = 0.8f, spawnPointIndex = 0 }
        };
        wave2.delayBetweenGroups = 2f;
        EditorUtility.SetDirty(wave2);

        WaveData wave3 = CreateAsset<WaveData>("Assets/Data/Waves/Wave_R3.asset");
        wave3.waveName = "Round 3";
        wave3.enemyGroups = new EnemyGroup[]
        {
            new EnemyGroup { enemyType = fastBug, count = 10, spawnInterval = 0.7f, spawnPointIndex = 0 },
            new EnemyGroup { enemyType = tankBug, count = 2, spawnInterval = 2f, spawnPointIndex = 0 }
        };
        wave3.delayBetweenGroups = 2f;
        EditorUtility.SetDirty(wave3);

        // 7. Create PathPatternData
        PathPatternData easyPath = CreateAsset<PathPatternData>("Assets/Data/Paths/EasyPath_01.asset");
        easyPath.patternName = "Easy Straight";
        easyPath.difficultyTier = 1;
        easyPath.waypointPositions = new Vector3[]
        {
            new Vector3(-7, 0, 0),
            new Vector3(-3, 0, 0),
            new Vector3(-1, 2, 0),
            new Vector3(1, 2, 0),
            new Vector3(3, 0, 0),
            new Vector3(7, 0, 0)
        };
        easyPath.spawnPointPositions = new Vector3[] { new Vector3(-8, 0, 0) };
        easyPath.exitPosition = new Vector3(8, 0, 0);
        easyPath.towerSlotPositions = new Vector3[]
        {
            new Vector3(-3, 1.5f, 0),
            new Vector3(-1, -1, 0),
            new Vector3(1, -1, 0),
            new Vector3(3, 1.5f, 0),
            new Vector3(0, 3.5f, 0),
            new Vector3(-5, -1.5f, 0)
        };
        EditorUtility.SetDirty(easyPath);

        // 7b. Second easy path pattern (S-shape)
        PathPatternData easyPath2 = CreateAsset<PathPatternData>("Assets/Data/Paths/EasyPath_02.asset");
        easyPath2.patternName = "Easy S-Curve";
        easyPath2.difficultyTier = 1;
        easyPath2.waypointPositions = new Vector3[]
        {
            new Vector3(-7, -3, 0),
            new Vector3(-3, -3, 0),
            new Vector3(-1, -1, 0),
            new Vector3(1, 1, 0),
            new Vector3(3, 3, 0),
            new Vector3(7, 3, 0)
        };
        easyPath2.spawnPointPositions = new Vector3[] { new Vector3(-8, -3, 0) };
        easyPath2.exitPosition = new Vector3(8, 3, 0);
        easyPath2.towerSlotPositions = new Vector3[]
        {
            new Vector3(-5, -1.5f, 0),
            new Vector3(-2, 0.5f, 0),
            new Vector3(0, -2.5f, 0),
            new Vector3(2, -0.5f, 0),
            new Vector3(5, 1.5f, 0),
            new Vector3(0, 2.5f, 0)
        };
        EditorUtility.SetDirty(easyPath2);

        // 7c. Third easy path pattern (zigzag)
        PathPatternData easyPath3 = CreateAsset<PathPatternData>("Assets/Data/Paths/EasyPath_03.asset");
        easyPath3.patternName = "Easy Zigzag";
        easyPath3.difficultyTier = 1;
        easyPath3.waypointPositions = new Vector3[]
        {
            new Vector3(-7, 2, 0),
            new Vector3(-3, -2, 0),
            new Vector3(0, 2, 0),
            new Vector3(3, -2, 0),
            new Vector3(7, 2, 0)
        };
        easyPath3.spawnPointPositions = new Vector3[] { new Vector3(-8, 2, 0) };
        easyPath3.exitPosition = new Vector3(8, 2, 0);
        easyPath3.towerSlotPositions = new Vector3[]
        {
            new Vector3(-5, 1.5f, 0),
            new Vector3(-5, -1, 0),
            new Vector3(-1.5f, -1.5f, 0),
            new Vector3(1.5f, 1.5f, 0),
            new Vector3(5, 1, 0),
            new Vector3(5, -1.5f, 0),
            new Vector3(0, -2.5f, 0)
        };
        EditorUtility.SetDirty(easyPath3);

        // 8. Create LevelData
        LevelData level1 = CreateAsset<LevelData>("Assets/Data/Levels/ELEC1001.asset");
        level1.courseCode = "ELEC1001";
        level1.courseTier = 1;
        level1.creditsReward = 6;
        level1.skillPointsReward = 1;
        level1.startingGold = 150;
        level1.startingLives = 20;
        level1.rounds = new WaveData[] { wave1, wave2, wave3 };
        level1.pathDifficultyTier = 1;
        EditorUtility.SetDirty(level1);

        LevelData level2 = CreateAsset<LevelData>("Assets/Data/Levels/ELEC1002.asset");
        level2.courseCode = "ELEC1002";
        level2.courseTier = 1;
        level2.creditsReward = 6;
        level2.skillPointsReward = 1;
        level2.startingGold = 120;
        level2.startingLives = 18;
        level2.rounds = new WaveData[] { wave2, wave3 };
        level2.pathDifficultyTier = 1;
        EditorUtility.SetDirty(level2);

        // 9. Create SkillTreeData
        SkillTreeData skillTree = CreateAsset<SkillTreeData>("Assets/Data/SkillTree.asset");
        skillTree.nodes = new SkillNode[]
        {
            new SkillNode
            {
                nodeName = "Tutoring",
                description = "+10% damage",
                cost = 1,
                section = SkillSection.Social,
                buff = new BuffEffect { damageMultiplier = 1.1f, rangeMultiplier = 1f, fireRateMultiplier = 1f },
                prerequisiteNodeNames = new string[0]
            },
            new SkillNode
            {
                nodeName = "Study Group",
                description = "+1 bonus life",
                cost = 1,
                section = SkillSection.Social,
                buff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1f, fireRateMultiplier = 1f, bonusLives = 1 },
                prerequisiteNodeNames = new string[] { "Tutoring" }
            },
            new SkillNode
            {
                nodeName = "Part-time Cashier",
                description = "+20 starting gold",
                cost = 1,
                section = SkillSection.PartTimeWork,
                buff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1f, fireRateMultiplier = 1f, bonusStartGold = 20 },
                prerequisiteNodeNames = new string[0]
            }
        };
        EditorUtility.SetDirty(skillTree);

        // 10. Create FacultyData
        FacultyData eee = CreateAsset<FacultyData>("Assets/Data/Faculties/Faculty_EEE.asset");
        eee.facultyName = "EEE";
        eee.fullName = "Electrical & Electronic Engineering";
        eee.courses = new LevelData[] { level1, level2 };
        eee.professorTower = profTower;
        EditorUtility.SetDirty(eee);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== Graduation Setup Complete! ===\n" +
            "Created:\n" +
            "  3 Prefabs (Assets/Prefabs/)\n" +
            "  3 EnemyData (Assets/Data/Enemies/)\n" +
            "  4 TowerData (Assets/Data/Towers/)\n" +
            "  3 WaveData (Assets/Data/Waves/)\n" +
            "  3 PathPatternData (Assets/Data/Paths/)\n" +
            "  2 LevelData (Assets/Data/Levels/)\n" +
            "  1 FacultyData (Assets/Data/Faculties/)\n" +
            "  1 SkillTreeData (Assets/Data/)\n\n" +
            "Now follow the scene setup instructions!");

        EditorUtility.DisplayDialog("Graduation Setup",
            "All starter data created!\n\n" +
            "Check Assets/Prefabs/ and Assets/Data/ folders.\n\n" +
            "Next: Set up your 3 scenes (see instructions).",
            "OK");
    }

    // --- Helpers ---

    static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            AssetDatabase.CreateFolder(parent, name);
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static GameObject SavePrefab(GameObject obj, string path)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        return PrefabUtility.SaveAsPrefabAsset(obj, path);
    }
}
#endif
