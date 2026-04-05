using UnityEngine;

[CreateAssetMenu(fileName = "NewPathPattern", menuName = "Graduation/Path Pattern")]
public class PathPatternData : ScriptableObject
{
    public string patternName;

    [Header("Difficulty: 1=easy (1xxx courses), 4=hard (4xxx courses)")]
    [Range(1, 4)]
    public int difficultyTier = 1;

    [Header("Path waypoints in order")]
    public Vector3[] waypointPositions;

    [Header("Spawn points (entry). Easy=1, Hard=up to 3)")]
    public Vector3[] spawnPointPositions;

    [Header("Exit point")]
    public Vector3 exitPosition;

    [Header("Tower slot positions beside the path")]
    public Vector3[] towerSlotPositions;
}
