using UnityEngine;

public enum TowerType
{
    Rapid,
    Balanced,
    Sniper,
    Professor
}

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Graduation/Tower Data")]
public class TowerData : ScriptableObject
{
    public string towerName;
    public TowerType towerType = TowerType.Balanced;
    public int cost = 50;
    public float range = 3f;
    public float fireRate = 1f;  // shots per second
    public int damage = 25;
    public Sprite sprite;
    public GameObject projectilePrefab;

    [Header("Professor Tower")]
    public bool isProfessorTower = false;
    public string requiredFaculty;  // faculty that must be cleared to unlock
}
