using UnityEngine;

/// <summary>
/// Sets up the gameplay scene when it loads.
/// Attach to an empty GameObject in the Gameplay scene.
/// </summary>
public class LevelSetup : MonoBehaviour
{
    [Header("References (in scene)")]
    public PathManager pathManager;
    public WaveSpawner waveSpawner;
    public SpriteRenderer backgroundRenderer;

    void Start()
    {
        LevelData level = GameManager.Instance?.GetCurrentLevel();
        if (level == null)
        {
            Debug.LogWarning("No current level set in GameManager. Using defaults.");
            return;
        }

        // Set up the path
        pathManager.LoadPathForTier(level.pathDifficultyTier);

        // Set up wave spawner
        waveSpawner.Setup(level.rounds, pathManager.currentWaypoints);

        // Set starting economy
        CurrencyManager.Instance.SetStartingGold(level.startingGold);
        LivesManager.Instance.SetStartingLives(level.startingLives);

        // Set background
        if (backgroundRenderer != null && level.classroomBackground != null)
            backgroundRenderer.sprite = level.classroomBackground;

        // Auto-start first round (or wait for player button)
        // waveSpawner.StartNextRound();
    }
}
