using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boots the Gameplay scene into "test everything" mode straight from the
/// Main Menu, optionally with custom round / spawn settings supplied by
/// <see cref="TestingModeSettingsPanel"/>.
/// </summary>
public static class TestingModeLauncher
{
    public class Overrides
    {
        public int  startingGold      = 200;
        public int  startingLives     = 20;
        public int  roundCount        = 3;
        public bool includeArchetypes = true;
        public bool includeProfessor  = true;
        /// Optional: name of a saved custom map to use instead of the default test path.
        public string customMapName;
    }

    public static bool TestingModeRequested { get; private set; }
    public static Overrides PendingOverrides { get; private set; }

    private static bool _hooked;

    public static void Launch(Overrides overrides = null)
    {
        TestingModeRequested = true;
        PendingOverrides     = overrides ?? new Overrides();

        // Wipe any current-level state so the bootstrap is the source of truth.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentFaculty     = null;
            GameManager.Instance.currentCourseIndex = 0;
        }

        if (!_hooked)
        {
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        SceneManager.LoadScene("Gameplay");
    }

    /// <summary>Called once by QuickTestBootstrap.Awake to clear the flag.</summary>
    public static void ConsumeRequest()
    {
        TestingModeRequested = false;
    }

    public static void ApplyOverridesTo(QuickTestBootstrap bootstrap)
    {
        if (PendingOverrides == null || bootstrap == null) return;
        bootstrap.startingGold      = PendingOverrides.startingGold;
        bootstrap.startingLives     = PendingOverrides.startingLives;
        bootstrap.roundCount        = Mathf.Clamp(PendingOverrides.roundCount, 1, 5);
        bootstrap.includeArchetypes = PendingOverrides.includeArchetypes;
        bootstrap.includeProfessor  = PendingOverrides.includeProfessor;
        bootstrap.customMapName     = PendingOverrides.customMapName;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay") return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _hooked = false;

        // Awake of any in-scene QuickTestBootstrap has already run by now.
        // If one exists already, it will have consumed the request and applied
        // the overrides — nothing more to do.
        if (Object.FindAnyObjectByType<QuickTestBootstrap>() != null) return;

        // Otherwise create one so the bootstrap still happens before Start fires.
        GameObject go = new GameObject("--- TestingMode QuickTest ---");
        go.AddComponent<QuickTestBootstrap>();
    }
}
