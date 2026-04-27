using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Endless-mode controller. Lives entirely in static state because we want it
/// to survive scene loads (the launch is requested from the main menu and
/// consumed by the gameplay-scene bootstrap).
///
/// Wave generation is procedural: a tunable mix of archetypes that ramps
/// difficulty (HP, count, spawn-point variety) round-by-round. Bosses appear
/// every 5 rounds.
/// </summary>
public static class EndlessMode
{
    /// <summary>Set by <see cref="Launch"/>; the gameplay bootstrap clears it.</summary>
    public static bool LaunchRequested;

    /// <summary>True while the player is inside an active endless run.</summary>
    public static bool IsActive;

    /// <summary>Last round index requested via <see cref="GenerateWave"/> (0-based).</summary>
    public static int CurrentRound;

    /// <summary>Enemy templates used to build waves (basic, fast, tank, shielded, stealth, boss).</summary>
    public static EnemyData[] EnemyPool;

    /// <summary>Number of spawn points the active endless map exposes.</summary>
    public static int SpawnPointCount = 1;

    public static event Action<int> OnRoundChanged;

    static bool _hooked;

    public static void Launch()
    {
        LaunchRequested = true;
        IsActive = false;
        CurrentRound = 0;

        // Make sure a QuickTestBootstrap exists in the gameplay scene to
        // consume the request — mirroring TestingModeLauncher's flow.
        if (!_hooked)
        {
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        SceneManager.LoadScene("Gameplay");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay") return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _hooked = false;

        // If a QuickTestBootstrap was already in the scene its Awake has
        // already consumed the flag; nothing to do.
        if (UnityEngine.Object.FindAnyObjectByType<QuickTestBootstrap>() != null) return;

        var go = new GameObject("--- Endless QuickTest ---");
        go.AddComponent<QuickTestBootstrap>();
    }

    public static void ConsumeRequest()
    {
        LaunchRequested = false;
    }

    public static void EndRun()
    {
        IsActive = false;
        EnemyPool = null;
    }

    /// <summary>
    /// Builds a fresh <see cref="WaveData"/> for round <paramref name="round"/>
    /// (0-based). Returns null if <see cref="EnemyPool"/> hasn't been set.
    /// Called by <see cref="WaveSpawner"/> via its <c>nextWaveProvider</c>.
    /// </summary>
    public static WaveData GenerateWave(int round)
    {
        if (EnemyPool == null || EnemyPool.Length == 0) return null;

        CurrentRound = round;
        OnRoundChanged?.Invoke(round);

        // Stat scaling: gentle HP ramp + bigger enemy counts later.
        float hpMul = 1f + round * 0.12f;
        int   goldBonus = round / 2;

        var groups = new List<EnemyGroup>();
        int sp = Mathf.Max(1, SpawnPointCount);

        // Basic swarm — split across all spawn points so the player has to
        // defend every home.
        EnemyData basic = EnemyPool[0];
        int basicPerSpawn = 4 + round;
        float basicInterval = Mathf.Max(0.35f, 1.1f - round * 0.04f);
        EnemyData scaledBasic = ScaledClone(basic, hpMul, goldBonus);
        for (int i = 0; i < sp; i++)
            groups.Add(new EnemyGroup { enemyType = scaledBasic, count = basicPerSpawn,
                                        spawnInterval = basicInterval, spawnPointIndex = i });

        // Fast bug — appears from round 2.
        if (round >= 1 && EnemyPool.Length > 1)
        {
            EnemyData fast = ScaledClone(EnemyPool[1], hpMul, goldBonus);
            groups.Add(new EnemyGroup { enemyType = fast, count = 3 + round,
                                        spawnInterval = 0.55f,
                                        spawnPointIndex = round % sp });
        }

        // Tank — appears from round 3.
        if (round >= 2 && EnemyPool.Length > 2)
        {
            EnemyData tank = ScaledClone(EnemyPool[2], hpMul * 1.1f, goldBonus * 2);
            groups.Add(new EnemyGroup { enemyType = tank, count = 1 + round / 3,
                                        spawnInterval = 1.4f,
                                        spawnPointIndex = (round + 1) % sp });
        }

        // Shielded — appears from round 4.
        if (round >= 3 && EnemyPool.Length > 3)
        {
            EnemyData shielded = ScaledClone(EnemyPool[3], hpMul, goldBonus);
            groups.Add(new EnemyGroup { enemyType = shielded, count = 2 + round / 2,
                                        spawnInterval = 0.9f,
                                        spawnPointIndex = (round + 2) % sp });
        }

        // Stealth — appears from round 5.
        if (round >= 4 && EnemyPool.Length > 4)
        {
            EnemyData stealth = ScaledClone(EnemyPool[4], hpMul, goldBonus);
            groups.Add(new EnemyGroup { enemyType = stealth, count = 2 + round / 3,
                                        spawnInterval = 0.85f,
                                        spawnPointIndex = round % sp });
        }

        // Boss every 5 rounds.
        if (round > 0 && round % 5 == 0 && EnemyPool.Length > 5)
        {
            EnemyData boss = ScaledClone(EnemyPool[5], hpMul * 1.25f, goldBonus * 4);
            int bossCount = 1 + Mathf.Min(3, round / 10);
            groups.Add(new EnemyGroup { enemyType = boss, count = bossCount,
                                        spawnInterval = 2.5f,
                                        spawnPointIndex = round % sp });
        }

        var wave = ScriptableObject.CreateInstance<WaveData>();
        wave.waveName = $"Endless Round {round + 1}";
        wave.enemyGroups = groups.ToArray();
        wave.delayBetweenGroups = 0.7f;
        return wave;
    }

    static EnemyData ScaledClone(EnemyData src, float hpMul, int goldBonus)
    {
        var c = ScriptableObject.CreateInstance<EnemyData>();
        c.enemyName    = src.enemyName;
        c.moveSpeed    = src.moveSpeed;
        c.maxHealth    = Mathf.Max(1, Mathf.RoundToInt(src.maxHealth * hpMul));
        c.goldReward   = src.goldReward + goldBonus;
        c.courseTier   = src.courseTier;
        c.sprite       = src.sprite;
        c.animatorController = src.animatorController;
        c.flipWithDirection  = src.flipWithDirection;
        c.artFacesRight      = src.artFacesRight;
        c.visualScale        = src.visualScale;
        c.deathAnimatorOverride = src.deathAnimatorOverride;
        c.deathSpriteOverride   = src.deathSpriteOverride;
        c.deathFxDuration       = src.deathFxDuration;
        c.deathFxScale          = src.deathFxScale;
        c.archetype    = src.archetype;
        c.shieldHealth = Mathf.Max(0, Mathf.RoundToInt(src.shieldHealth * hpMul));
        c.bossScale    = src.bossScale;
        return c;
    }
}
