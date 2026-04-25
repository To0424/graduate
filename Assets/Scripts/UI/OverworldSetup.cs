using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on an empty GameObject in the Overworld scene.
/// Auto-creates the HKU campus map UI with faculty building buttons and level selection.
/// Assign FacultyData assets in the Inspector.
/// </summary>
public class OverworldSetup : MonoBehaviour
{
    [Header("Assign your faculty data assets here")]
    public FacultyData[] faculties;

    // Runtime references
    private Canvas canvas;
    private GameObject levelSelectPanel;
    private Transform levelButtonContainer;
    private TextMeshProUGUI creditsText;
    private TextMeshProUGUI skillPointsText;
    private FacultyData selectedFaculty;
    private SkillTreeUI skillTreeUI;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        // EventSystem (needed for UI clicks)
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas
        GameObject canvasObj = new GameObject("OverworldCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Build the Skill Tree overlay (hidden until the button is clicked)
        skillTreeUI = gameObject.AddComponent<SkillTreeUI>();
        skillTreeUI.Build(canvasObj.transform);

        // Background
        GameObject bg = CreatePanel(canvasObj.transform, "Background", new Color(0.12f, 0.18f, 0.12f, 1f));
        StretchFull(bg);

        // Title
        GameObject title = CreateText(canvasObj.transform, "Title", "HKU Campus", 48, Color.white);
        SetAnchored(title, new Vector2(0.5f, 0.92f), new Vector2(600, 60));

        // Credits display
        GameObject creditsObj = CreateText(canvasObj.transform, "Credits", "Credits: 0/240", 28, Color.yellow);
        SetAnchored(creditsObj, new Vector2(0.15f, 0.92f), new Vector2(300, 40));
        creditsText = creditsObj.GetComponent<TextMeshProUGUI>();

        // Skill points display
        GameObject spObj = CreateText(canvasObj.transform, "SkillPoints", "Skill Points: 0", 28, Color.cyan);
        SetAnchored(spObj, new Vector2(0.85f, 0.92f), new Vector2(300, 40));
        skillPointsText = spObj.GetComponent<TextMeshProUGUI>();

        // Skill Tree button
        GameObject stBtn = CreateButton(canvasObj.transform, "SkillTreeBtn", "Skill Tree", new Color(0.4f, 0.2f, 0.6f));
        SetAnchored(stBtn, new Vector2(0.85f, 0.85f), new Vector2(200, 50));
        stBtn.GetComponent<Button>().onClick.AddListener(() => skillTreeUI.Open());

        // Back to Menu button
        GameObject backBtn = CreateButton(canvasObj.transform, "BackBtn", "< Main Menu", new Color(0.4f, 0.4f, 0.4f));
        SetAnchored(backBtn, new Vector2(0.1f, 0.85f), new Vector2(200, 50));
        backBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance?.GoToMainMenu());

        // Faculty building buttons — arranged in a grid
        CreateFacultyButtons(canvasObj.transform);

        // Level select panel (hidden by default)
        CreateLevelSelectPanel(canvasObj.transform);

        // Update displays
        UpdateInfo();
    }

    void CreateFacultyButtons(Transform parent)
    {
        if (faculties == null || faculties.Length == 0) return;

        // Layout faculty buttons in a horizontal row in the center
        float startX = 0.5f - (faculties.Length - 1) * 0.12f / 2f;

        for (int i = 0; i < faculties.Length; i++)
        {
            int index = i;
            FacultyData f = faculties[i];

            bool cleared = GameManager.Instance != null && GameManager.Instance.IsFacultyCleared(f);
            Color btnColor = cleared ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.3f, 0.4f, 0.6f);

            GameObject btn = CreateButton(parent, $"Faculty_{f.facultyName}", f.facultyName, btnColor);
            float xPos = startX + i * 0.12f;
            SetAnchored(btn, new Vector2(xPos, 0.55f), new Vector2(180, 180));

            // Add course count text under the button
            string info = $"{f.fullName}\n{f.courses.Length} courses";
            if (cleared) info += "\n★ CLEARED";
            GameObject infoText = CreateText(parent, $"FacultyInfo_{i}", info, 16, Color.white);
            SetAnchored(infoText, new Vector2(xPos, 0.40f), new Vector2(180, 60));

            btn.GetComponent<Button>().onClick.AddListener(() => OnFacultyClicked(f));
        }
    }

    void CreateLevelSelectPanel(Transform parent)
    {
        // Dark overlay panel
        levelSelectPanel = CreatePanel(parent, "LevelSelectPanel", new Color(0, 0, 0, 0.85f));
        StretchFull(levelSelectPanel);
        levelSelectPanel.SetActive(false);

        // Panel title
        GameObject panelTitle = CreateText(levelSelectPanel.transform, "PanelTitle", "Select Course", 40, Color.white);
        SetAnchored(panelTitle, new Vector2(0.5f, 0.85f), new Vector2(400, 50));

        // Close button
        GameObject closeBtn = CreateButton(levelSelectPanel.transform, "CloseBtn", "X", new Color(0.7f, 0.2f, 0.2f));
        SetAnchored(closeBtn, new Vector2(0.9f, 0.9f), new Vector2(60, 60));
        closeBtn.GetComponent<Button>().onClick.AddListener(() => levelSelectPanel.SetActive(false));

        // Container for course buttons
        GameObject container = new GameObject("LevelButtonContainer");
        container.transform.SetParent(levelSelectPanel.transform, false);
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.2f, 0.2f);
        containerRect.anchorMax = new Vector2(0.8f, 0.75f);
        containerRect.sizeDelta = Vector2.zero;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        levelButtonContainer = container.transform;
    }

    void OnFacultyClicked(FacultyData faculty)
    {
        selectedFaculty = faculty;
        levelSelectPanel.SetActive(true);

        // Update panel title
        TextMeshProUGUI titleText = levelSelectPanel.transform.Find("PanelTitle")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null) titleText.text = faculty.fullName;

        // Clear old course buttons
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);

        // Create course buttons
        for (int i = 0; i < faculty.courses.Length; i++)
        {
            int courseIndex = i;
            LevelData course = faculty.courses[i];
            bool completed = GameManager.Instance != null && GameManager.Instance.IsCourseCompleted(faculty, i);

            Color color = completed ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.3f, 0.5f, 0.7f);
            string label = completed
                ? $"{course.courseCode}  ✓  ({course.creditsReward} credits)"
                : $"{course.courseCode}  ({course.creditsReward} credits)";

            GameObject btn = CreateButton(levelButtonContainer, $"Course_{course.courseCode}", label, color);

            // Force size via LayoutElement
            UnityEngine.UI.LayoutElement le = btn.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = 70;
            le.minHeight = 70;

            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameManager.Instance?.StartLevel(faculty, courseIndex);
            });
        }
    }

    void UpdateInfo()
    {
        if (creditsText != null && CreditManager.Instance != null)
            creditsText.text = $"Credits: {CreditManager.Instance.TotalCredits}/240";
        if (skillPointsText != null && SkillPointManager.Instance != null)
            skillPointsText.text = $"Skill Points: {SkillPointManager.Instance.SkillPoints}";
    }

    // --- UI Helpers ---

    GameObject CreateText(Transform parent, string name, string text, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    GameObject CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<RectTransform>();
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }

    GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        Image img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }

    void SetAnchored(GameObject obj, Vector2 anchor, Vector2 size)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    void StretchFull(GameObject obj)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
