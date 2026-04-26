using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drop this on any scene. It ensures all persistent managers exist (GameManager, CreditManager, etc.)
/// even if you play directly from that scene in the editor (skipping MainMenu).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Only needed in the FIRST scene that loads, or for editor testing")]
    public FacultyData[] allFaculties;

    void Awake()
    {
        // If GameManager already exists (from a previous scene), skip
        if (GameManager.Instance != null) return;

        // Create persistent managers
        GameObject go = new GameObject("--- Persistent Managers ---");
        DontDestroyOnLoad(go);

        // GameManager
        GameManager gm = go.AddComponent<GameManager>();
        gm.allFaculties = allFaculties;

        // CreditManager
        go.AddComponent<CreditManager>();
    }
}
