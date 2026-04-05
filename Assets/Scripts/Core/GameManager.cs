using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public enum GameState
{
    MainMenu,
    Overworld,
    Playing,
    Paused,
    LevelWon,
    LevelLost,
    Graduated
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State")]
    public GameState currentState = GameState.MainMenu;

    [Header("Faculty Data")]
    public FacultyData[] allFaculties;

    [Header("Current Level")]
    public FacultyData currentFaculty;
    public int currentCourseIndex;

    // Track completed courses: "EEE_0", "FBE_2", etc.
    private HashSet<string> completedCourses = new HashSet<string>();

    public static event Action<GameState> OnGameStateChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        LoadProgress();
    }

    void OnEnable()
    {
        WaveSpawner.OnAllRoundsComplete += HandleLevelWon;
        LivesManager.OnAllLivesLost += HandleLevelLost;
        CreditManager.OnGraduated += HandleGraduated;
    }

    void OnDisable()
    {
        WaveSpawner.OnAllRoundsComplete -= HandleLevelWon;
        LivesManager.OnAllLivesLost -= HandleLevelLost;
        CreditManager.OnGraduated -= HandleGraduated;
    }

    public void SetState(GameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(newState);

        if (newState == GameState.Paused)
            Time.timeScale = 0f;
        else
            Time.timeScale = 1f;
    }

    public void StartLevel(FacultyData faculty, int courseIndex)
    {
        currentFaculty = faculty;
        currentCourseIndex = courseIndex;
        SceneManager.LoadScene("Gameplay");
        SetState(GameState.Playing);
    }

    public void GoToOverworld()
    {
        SetState(GameState.Overworld);
        SceneManager.LoadScene("Overworld");
    }

    public void GoToMainMenu()
    {
        SetState(GameState.MainMenu);
        SceneManager.LoadScene("MainMenu");
    }

    void HandleLevelWon()
    {
        SetState(GameState.LevelWon);

        LevelData level = GetCurrentLevel();
        if (level != null)
        {
            string key = GetCourseKey(currentFaculty, currentCourseIndex);
            bool firstClear = !completedCourses.Contains(key);

            if (firstClear)
            {
                CreditManager.Instance.AddCredits(level.creditsReward);
                SkillPointManager.Instance.AddSkillPoints(level.skillPointsReward);
                completedCourses.Add(key);
            }

            SaveProgress();
        }
    }

    void HandleLevelLost()
    {
        SetState(GameState.LevelLost);
    }

    void HandleGraduated()
    {
        SetState(GameState.Graduated);
    }

    public void RetryLevel()
    {
        StartLevel(currentFaculty, currentCourseIndex);
    }

    public LevelData GetCurrentLevel()
    {
        if (currentFaculty == null) return null;
        if (currentCourseIndex < 0 || currentCourseIndex >= currentFaculty.courses.Length) return null;
        return currentFaculty.courses[currentCourseIndex];
    }

    // --- Progression queries ---

    public bool IsCourseCompleted(FacultyData faculty, int courseIndex)
    {
        return completedCourses.Contains(GetCourseKey(faculty, courseIndex));
    }

    public bool IsFacultyCleared(FacultyData faculty)
    {
        for (int i = 0; i < faculty.courses.Length; i++)
        {
            if (!IsCourseCompleted(faculty, i)) return false;
        }
        return true;
    }

    public bool IsProfessorTowerUnlocked(FacultyData faculty)
    {
        return IsFacultyCleared(faculty);
    }

    string GetCourseKey(FacultyData faculty, int index)
    {
        return $"{faculty.facultyName}_{index}";
    }

    // --- Save/Load ---

    public void ResetAllProgress()
    {
        completedCourses.Clear();
        PlayerPrefs.DeleteKey("CompletedCourses");

        if (CreditManager.Instance != null)
            CreditManager.Instance.ResetCredits();
        if (SkillPointManager.Instance != null)
            SkillPointManager.Instance.ResetSkillPoints();
        if (SkillTreeManager.Instance != null)
            SkillTreeManager.Instance.ResetTree();

        PlayerPrefs.Save();
        Debug.Log("[GameManager] All progress reset!");
    }

    void SaveProgress()
    {
        string data = string.Join(",", completedCourses);
        PlayerPrefs.SetString("CompletedCourses", data);
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        string data = PlayerPrefs.GetString("CompletedCourses", "");
        completedCourses.Clear();
        if (!string.IsNullOrEmpty(data))
        {
            foreach (string c in data.Split(','))
            {
                if (!string.IsNullOrEmpty(c))
                    completedCourses.Add(c);
            }
        }

        CreditManager.Instance?.Load();
        SkillPointManager.Instance?.Load();
    }
}
