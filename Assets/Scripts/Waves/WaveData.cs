using UnityEngine;

[System.Serializable]
public class EnemyGroup
{
    public EnemyData enemyType;
    public int count = 5;
    public float spawnInterval = 1f;
    [Tooltip("Which spawn point index to use (0 = default). For multi-spawn path patterns.")]
    public int spawnPointIndex = 0;
}

[CreateAssetMenu(fileName = "NewWaveData", menuName = "Graduation/Wave Data")]
public class WaveData : ScriptableObject
{
    public string waveName = "Round 1";
    public EnemyGroup[] enemyGroups;
    public float delayBetweenGroups = 2f;
}
