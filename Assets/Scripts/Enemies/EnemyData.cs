using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Graduation/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "ELEC1001 Bug";
    public float moveSpeed = 2f;
    public int maxHealth = 100;
    public int goldReward = 10;

    [Range(1, 4)]
    public int courseTier = 1;

    public Sprite sprite;
}
