using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Graduation/Level Data")]
public class LevelData : ScriptableObject
{
    public string courseCode = "ELEC1001";

    [Range(1, 4)]
    public int courseTier = 1;

    public int creditsReward = 6;
    public int skillPointsReward = 1;
    public int startingGold = 100;
    public int startingLives = 20;

    [Header("Rounds (3-5 per level)")]
    public WaveData[] rounds;

    [Header("Visuals")]
    public Sprite classroomBackground;

    [Header("Path difficulty tier (which pool of patterns to pick from)")]
    public int pathDifficultyTier = 1;
}
