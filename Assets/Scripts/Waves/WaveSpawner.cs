using UnityEngine;
using System;
using System.Collections;

public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    [Header("Configuration")]
    public WaveData[] rounds;
    public GameObject enemyPrefab;

    [Header("Endless mode")]
    /// <summary>When true, <see cref="OnAllRoundsComplete"/> is never raised
    /// and waves are pulled from <see cref="nextWaveProvider"/> indefinitely.</summary>
    public bool isEndless = false;
    /// <summary>Optional callback invoked when a new round begins; should
    /// return the wave to spawn for that 0-based round index. Used by
    /// <see cref="EndlessMode"/>.</summary>
    public System.Func<int, WaveData> nextWaveProvider;

    [Header("Runtime")]
    public int currentRound = 0;
    public int enemiesAlive = 0;
    public bool isSpawning = false;

    /// <summary>Total enemies still expected to be dealt with this round —
    /// includes ones still to spawn AND ones currently on the map. Drops to 0
    /// when the round ends. This is what the HUD's "Enemies" counter shows.</summary>
    public int enemiesRemainingThisRound = 0;

    private Waypoints waypoints;

    public static event Action<int> OnRoundStart;       // round index
    public static event Action<int> OnRoundComplete;    // round index
    public static event Action OnAllRoundsComplete;
    public static event Action<int> OnEnemyCountChanged; // enemies remaining this round

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        Enemy.OnEnemyDeath += HandleEnemyRemoved;
        Enemy.OnEnemyReachedExit += HandleEnemyRemoved;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDeath -= HandleEnemyRemoved;
        Enemy.OnEnemyReachedExit -= HandleEnemyRemoved;
    }

    public void Setup(WaveData[] waveData, Waypoints wp)
    {
        rounds = waveData;
        waypoints = wp;
        currentRound = 0;
        enemiesAlive = 0;
        enemiesRemainingThisRound = 0;
    }

    public void StartNextRound()
    {
        if (isSpawning || enemiesAlive > 0)
        {
            Debug.Log("[WaveSpawner] Round still in progress, ignoring StartNextRound.");
            return;
        }

        WaveData wave = ResolveWaveFor(currentRound);
        if (wave == null)
        {
            OnAllRoundsComplete?.Invoke();
            return;
        }

        StartCoroutine(SpawnRound(wave));
    }

    WaveData ResolveWaveFor(int round)
    {
        if (isEndless && nextWaveProvider != null)
            return nextWaveProvider(round);
        if (rounds != null && round < rounds.Length)
            return rounds[round];
        return null;
    }

    static int CountEnemiesInWave(WaveData wave)
    {
        if (wave == null || wave.enemyGroups == null) return 0;
        int sum = 0;
        foreach (EnemyGroup g in wave.enemyGroups) sum += g.count;
        return sum;
    }

    IEnumerator SpawnRound(WaveData wave)
    {
        isSpawning = true;
        enemiesRemainingThisRound = CountEnemiesInWave(wave);
        OnEnemyCountChanged?.Invoke(enemiesRemainingThisRound);
        OnRoundStart?.Invoke(currentRound);
        Debug.Log($"[WaveSpawner] Starting round {currentRound + 1}/{rounds.Length} ({enemiesRemainingThisRound} enemies)");

        foreach (EnemyGroup group in wave.enemyGroups)
        {
            for (int i = 0; i < group.count; i++)
            {
                SpawnEnemy(group.enemyType, group.spawnPointIndex);
                yield return new WaitForSeconds(group.spawnInterval);
            }
            yield return new WaitForSeconds(wave.delayBetweenGroups);
        }

        isSpawning = false;
        Debug.Log($"[WaveSpawner] Finished spawning. Enemies alive: {enemiesAlive}");

        // Check if all enemies already died during spawning
        if (enemiesAlive <= 0)
        {
            OnRoundComplete?.Invoke(currentRound);
            currentRound++;
            Debug.Log($"[WaveSpawner] Round complete (all died during spawn). Next round: {currentRound}");
            if (!isEndless && currentRound >= rounds.Length)
                OnAllRoundsComplete?.Invoke();
        }
    }

    void SpawnEnemy(EnemyData enemyData, int spawnPointIndex)
    {
        Vector3 spawnPos = waypoints.GetSpawnPosition(spawnPointIndex);
        GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        Enemy enemy = obj.GetComponent<Enemy>();
        enemy.Initialize(enemyData, waypoints.GetPathFor(spawnPointIndex));
        enemiesAlive++;
        // "Remaining" only ticks down on death/exit, not on spawn — we already
        // counted the whole wave at the start of the round.
    }

    /// <summary>Debug helper: spawn one enemy of the given type at the given
    /// spawn point without affecting the round's enemy count or HUD.
    /// Used by the in-game enemy spawner debug panel.</summary>
    public void DebugSpawn(EnemyData enemyData, int spawnPointIndex)
    {
        if (enemyData == null || enemyPrefab == null || waypoints == null) return;
        Vector3 spawnPos = waypoints.GetSpawnPosition(spawnPointIndex);
        GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        Enemy enemy = obj.GetComponent<Enemy>();
        enemy.Initialize(enemyData, waypoints.GetPathFor(spawnPointIndex));
        // Note: deliberately not touching enemiesAlive / enemiesRemainingThisRound
        // so debug spawns don't break round-end detection.
    }

    /// <summary>Number of available spawn points on the current map.</summary>
    public int DebugSpawnPointCount => waypoints != null ? waypoints.SpawnPointCount : 0;

    /// <summary>Spawn split-enemies in place when a Splitter dies. Each child
    /// inherits the parent's path and starts at the parent's current waypoint
    /// index so it doesn't snap back to the spawn. Counts toward the
    /// remaining-enemies HUD as additional work for the round.</summary>
    public void SpawnSplit(EnemyData childData, Vector3 position, Transform[] path, int waypointIndex, int count)
    {
        if (childData == null || enemyPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            // Slight scatter so the children don't perfectly overlap.
            Vector3 jitter = new Vector3((i - (count - 1) * 0.5f) * 0.35f, 0f, 0f);
            GameObject obj = Instantiate(enemyPrefab, position + jitter, Quaternion.identity);
            Enemy child = obj.GetComponent<Enemy>();
            child.Initialize(childData, path);
            child.transform.position = position + jitter;
            child.SetWaypointIndex(Mathf.Max(0, waypointIndex));
            enemiesAlive++;
            enemiesRemainingThisRound++;
        }
        OnEnemyCountChanged?.Invoke(enemiesRemainingThisRound);
    }

    void HandleEnemyRemoved(Enemy enemy)
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
        enemiesRemainingThisRound = Mathf.Max(0, enemiesRemainingThisRound - 1);
        OnEnemyCountChanged?.Invoke(enemiesRemainingThisRound);
        if (enemiesAlive <= 0 && !isSpawning)
        {
            OnRoundComplete?.Invoke(currentRound);
            currentRound++;
            if (!isEndless && currentRound >= rounds.Length)
            {
                OnAllRoundsComplete?.Invoke();
            }
        }
    }
}
