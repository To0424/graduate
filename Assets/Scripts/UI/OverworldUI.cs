using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OverworldUI : MonoBehaviour
{
    [Header("Faculty Buttons")]
    public FacultyButton[] facultyButtons;

    [Header("Level Select Panel")]
    public GameObject levelSelectPanel;
    public Transform levelButtonContainer;
    public GameObject levelButtonPrefab;

    [Header("Info")]
    public TextMeshProUGUI totalCreditsText;
    public TextMeshProUGUI skillPointsText;
    public Button skillTreeButton;

    private FacultyData selectedFaculty;
    private SkillTreeUI _skillTreeUI;

    [System.Serializable]
    public class FacultyButton
    {
        public FacultyData faculty;
        public Button button;
        public Image buildingImage;
        public GameObject clearedBadge;
    }

    void Start()
    {
        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);

        for (int i = 0; i < facultyButtons.Length; i++)
        {
            int index = i;
            FacultyButton fb = facultyButtons[i];
            if (fb.button != null)
                fb.button.onClick.AddListener(() => OnFacultyClicked(index));

            if (fb.buildingImage != null && fb.faculty.buildingSprite != null)
                fb.buildingImage.sprite = fb.faculty.buildingSprite;

            // Show cleared badge
            if (fb.clearedBadge != null)
                fb.clearedBadge.SetActive(GameManager.Instance.IsFacultyCleared(fb.faculty));
        }

        if (skillTreeButton != null)
            skillTreeButton.onClick.AddListener(OpenSkillTree);

        UpdateInfoDisplay();
    }

    /// <summary>Builds (lazily) and opens the runtime Skill Tree overlay.</summary>
    void OpenSkillTree()
    {
        if (_skillTreeUI == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) { Debug.LogWarning("[OverworldUI] No Canvas found for SkillTreeUI."); return; }
            _skillTreeUI = gameObject.AddComponent<SkillTreeUI>();
            _skillTreeUI.Build(canvas.transform);
        }
        _skillTreeUI.Open();
    }

    void OnFacultyClicked(int index)
    {
        selectedFaculty = facultyButtons[index].faculty;
        ShowLevelSelect(selectedFaculty);
    }

    void ShowLevelSelect(FacultyData faculty)
    {
        if (levelSelectPanel == null) return;
        levelSelectPanel.SetActive(true);

        // Clear old buttons
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);

        // Create a button per course
        for (int i = 0; i < faculty.courses.Length; i++)
        {
            int courseIndex = i;
            LevelData course = faculty.courses[i];

            GameObject btnObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI label = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            bool completed = GameManager.Instance.IsCourseCompleted(faculty, i);
            if (label != null)
                label.text = completed ? $"{course.courseCode} ✓" : course.courseCode;

            btn.onClick.AddListener(() =>
            {
                GameManager.Instance.StartLevel(faculty, courseIndex);
            });
        }
    }

    void UpdateInfoDisplay()
    {
        if (totalCreditsText != null && CreditManager.Instance != null)
            totalCreditsText.text = $"Credits: {CreditManager.Instance.TotalCredits}/240";
        if (skillPointsText != null && SkillPointManager.Instance != null)
            skillPointsText.text = $"Skill Points: {SkillPointManager.Instance.SkillPoints}";
    }
}
