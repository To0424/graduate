using UnityEngine;
using System;
using System.Collections;

public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    [Header("Configuration")]
    public WaveData[] rounds;
    public GameObject enemyPrefab;

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

        if (currentRound >= rounds.Length)
        {
            OnAllRoundsComplete?.Invoke();
            return;
        }

        StartCoroutine(SpawnRound(rounds[currentRound]));
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
            if (currentRound >= rounds.Length)
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

    void HandleEnemyRemoved(Enemy enemy)
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
        enemiesRemainingThisRound = Mathf.Max(0, enemiesRemainingThisRound - 1);
        OnEnemyCountChanged?.Invoke(enemiesRemainingThisRound);
        if (enemiesAlive <= 0 && !isSpawning)
        {
            OnRoundComplete?.Invoke(currentRound);
            currentRound++;
            if (currentRound >= rounds.Length)
            {
                OnAllRoundsComplete?.Invoke();
            }
        }
    }
}
