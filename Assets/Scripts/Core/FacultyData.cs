using UnityEngine;

[CreateAssetMenu(fileName = "NewFacultyData", menuName = "Graduation/Faculty Data")]
public class FacultyData : ScriptableObject
{
    public string facultyName = "EEE";
    public string fullName = "Electrical & Electronic Engineering";
    public Sprite buildingSprite;

    [Header("Courses (3-5 levels)")]
    public LevelData[] courses;

    [Header("Professor tower unlocked by clearing this building")]
    public TowerData professorTower;
}
