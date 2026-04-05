using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on an empty GameObject in the Gameplay scene.
/// Auto-creates all gameplay managers, HUD, tower shop, and game-over panels.
/// Assign prefabs and tower data in the Inspector.
/// </summary>
public class GameplayAutoSetup : MonoBehaviour
{
    [Header("Prefabs (create these in Unity)")]
    public GameObject enemyPrefab;
    public GameObject towerPrefab;

    [Header("Path Patterns (assign PathPatternData assets)")]
    public PathPatternData[] easyPatterns;
    public PathPatternData[] mediumPatterns;
    public PathPatternData[] hardPatterns;
    public PathPatternData[] expertPatterns;

    [Header("Tower Shop (assign TowerData assets for your buyable towers)")]
    public TowerData[] availableTowers;

    // Runtime references
    private PathManager pathManager;
    private WaveSpawner waveSpawner;
    private CurrencyManager currencyManager;
    private LivesManager livesManager;
    private TowerPlacement towerPlacement;

    // HUD references
    private TextMeshProUGUI goldText;
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI enemiesLeftText;
    private TextMeshProUGUI timerText;
    private Button startRoundButton;
    private Button pauseButton;

    // Cached delegates for cleanup
    private System.Action<int> onGoldChanged;
    private System.Action<int> onLivesChanged;
    private System.Action<int> onRoundStart;
    private System.Action<int> onEnemyCountChanged;

    // Round timer
    private float roundTimer;
    private bool roundTimerActive;

    // Pause
    private GameObject pausePanel;
    private TextMeshProUGUI pauseBtnLabel;

    // Game over panels
    private GameObject levelWonPanel;
    private GameObject levelLostPanel;
    private GameObject graduationPanel;
    private TextMeshProUGUI creditsEarnedText;
    private TextMeshProUGUI spEarnedText;

    void Start()
    {
        CreateManagers();
        CreateCamera();
        CreateEventSystem();
        Canvas canvas = CreateCanvas();
        CreateHUD(canvas.transform);
        CreateTowerShop(canvas.transform);
        CreateGameOverUI(canvas.transform);
        SetupLevel();
    }

    // ========== MANAGERS ==========

    void CreateManagers()
    {
        // PathManager
        GameObject pmObj = new GameObject("PathManager");
        pathManager = pmObj.AddComponent<PathManager>();
        pathManager.easyPatterns = easyPatterns;
        pathManager.mediumPatterns = mediumPatterns;
        pathManager.hardPatterns = hardPatterns;
        pathManager.expertPatterns = expertPatterns;

        // WaveSpawner
        GameObject wsObj = new GameObject("WaveSpawner");
        waveSpawner = wsObj.AddComponent<WaveSpawner>();
        waveSpawner.enemyPrefab = enemyPrefab;

        // CurrencyManager (per-level, not persistent)
        GameObject cmObj = new GameObject("CurrencyManager");
        currencyManager = cmObj.AddComponent<CurrencyManager>();

        // LivesManager (per-level)
        GameObject lmObj = new GameObject("LivesManager");
        livesManager = lmObj.AddComponent<LivesManager>();

        // TowerPlacement
        GameObject tpObj = new GameObject("TowerPlacement");
        towerPlacement = tpObj.AddComponent<TowerPlacement>();
    }

    void CreateCamera()
    {
        if (Camera.main == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6;
            cam.backgroundColor = new Color(0.2f, 0.25f, 0.2f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.AddComponent<AudioListener>();
        }
    }

    void CreateEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    Canvas CreateCanvas()
    {
        GameObject canvasObj = new GameObject("GameplayCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ========== HUD ==========

    void CreateHUD(Transform parent)
    {
        // Top bar background
        GameObject topBar = CreatePanel(parent, "TopBar", new Color(0, 0, 0, 0.6f));
        RectTransform tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0, 0.92f);
        tbRect.anchorMax = new Vector2(1, 1);
        tbRect.sizeDelta = Vector2.zero;
        tbRect.offsetMin = Vector2.zero;
        tbRect.offsetMax = Vector2.zero;

        // Gold text
        GameObject goldObj = CreateText(topBar.transform, "GoldText", "Gold: 100", 28, Color.yellow);
        SetAnchored(goldObj, new Vector2(0.1f, 0.5f), new Vector2(200, 50));
        goldText = goldObj.GetComponent<TextMeshProUGUI>();

        // Lives text
        GameObject livesObj = CreateText(topBar.transform, "LivesText", "Lives: 20", 28, new Color(1f, 0.4f, 0.4f));
        SetAnchored(livesObj, new Vector2(0.3f, 0.5f), new Vector2(200, 50));
        livesText = livesObj.GetComponent<TextMeshProUGUI>();

        // Round text
        GameObject roundObj = CreateText(topBar.transform, "RoundText", "Round: 0/0", 28, Color.white);
        SetAnchored(roundObj, new Vector2(0.48f, 0.5f), new Vector2(200, 50));
        roundText = roundObj.GetComponent<TextMeshProUGUI>();

        // Round timer text (beside round)
        GameObject timerObj = CreateText(topBar.transform, "TimerText", "Time: 0s", 22, new Color(0.9f, 0.9f, 0.9f));
        SetAnchored(timerObj, new Vector2(0.62f, 0.5f), new Vector2(150, 50));
        timerText = timerObj.GetComponent<TextMeshProUGUI>();

        // Enemies left text
        GameObject enemiesObj = CreateText(topBar.transform, "EnemiesLeftText", "Enemies: 0", 22, new Color(1f, 0.6f, 0.3f));
        SetAnchored(enemiesObj, new Vector2(0.8f, 0.5f), new Vector2(200, 50));
        enemiesLeftText = enemiesObj.GetComponent<TextMeshProUGUI>();

        // Start Round button (bottom right)
        GameObject startBtn = CreateButton(parent, "StartRoundBtn", "START ROUND", new Color(0.2f, 0.5f, 0.8f));
        SetAnchored(startBtn, new Vector2(0.85f, 0.06f), new Vector2(250, 60));
        startRoundButton = startBtn.GetComponent<Button>();
        startRoundButton.onClick.AddListener(() => WaveSpawner.Instance?.StartNextRound());

        // Pause button (top right)
        GameObject pBtn = CreateButton(parent, "PauseBtn", "| |", new Color(0.4f, 0.4f, 0.4f));
        SetAnchored(pBtn, new Vector2(0.95f, 0.96f), new Vector2(60, 40));
        pauseButton = pBtn.GetComponent<Button>();
        pauseBtnLabel = pBtn.GetComponentInChildren<TextMeshProUGUI>();
        pauseButton.onClick.AddListener(TogglePause);

        // Pause overlay panel (hidden by default)
        CreatePausePanel(parent);

        // Subscribe to events
        SubscribeHUD();
    }

    void CreatePausePanel(Transform parent)
    {
        pausePanel = CreatePanel(parent, "PausePanel", new Color(0, 0, 0, 0.7f));
        StretchFull(pausePanel);
        pausePanel.SetActive(false);

        CreateText(pausePanel.transform, "PauseTitle", "PAUSED", 56, Color.white);
        SetAnchored(pausePanel.transform.Find("PauseTitle").gameObject, new Vector2(0.5f, 0.6f), new Vector2(400, 70));

        GameObject resumeBtn = CreateButton(pausePanel.transform, "ResumeBtn", "Resume", new Color(0.2f, 0.6f, 0.3f));
        SetAnchored(resumeBtn, new Vector2(0.5f, 0.45f), new Vector2(250, 60));
        resumeBtn.GetComponent<Button>().onClick.AddListener(TogglePause);

        GameObject quitBtn = CreateButton(pausePanel.transform, "QuitToMapBtn", "Quit to Map", new Color(0.6f, 0.2f, 0.2f));
        SetAnchored(quitBtn, new Vector2(0.5f, 0.32f), new Vector2(250, 60));
        quitBtn.GetComponent<Button>().onClick.AddListener(() => {
            Time.timeScale = 1f;
            GameManager.Instance?.GoToOverworld();
        });
    }

    void TogglePause()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentState == GameState.Paused)
        {
            GameManager.Instance.SetState(GameState.Playing);
            pausePanel.SetActive(false);
            if (pauseBtnLabel) pauseBtnLabel.text = "| |";
        }
        else
        {
            GameManager.Instance.SetState(GameState.Paused);
            pausePanel.SetActive(true);
            if (pauseBtnLabel) pauseBtnLabel.text = "> >";
        }
    }

    void SubscribeHUD()
    {
        onGoldChanged = (g) => { if (goldText) goldText.text = $"Gold: {g}"; };
        onLivesChanged = (l) => { if (livesText) livesText.text = $"Lives: {l}"; };
        onRoundStart = (r) =>
        {
            if (roundText == null) return;
            int total = WaveSpawner.Instance != null ? WaveSpawner.Instance.rounds.Length : 0;
            roundText.text = $"Round: {r + 1}/{total}";
            roundTimer = 0f;
            roundTimerActive = true;
        };
        onEnemyCountChanged = (count) =>
        {
            if (enemiesLeftText) enemiesLeftText.text = $"Enemies: {count}";
            if (count <= 0) roundTimerActive = false;
        };

        CurrencyManager.OnGoldChanged += onGoldChanged;
        LivesManager.OnLivesChanged += onLivesChanged;
        WaveSpawner.OnRoundStart += onRoundStart;
        WaveSpawner.OnEnemyCountChanged += onEnemyCountChanged;
    }

    void Update()
    {
        if (roundTimerActive && timerText != null)
        {
            roundTimer += Time.deltaTime;
            timerText.text = $"Time: {Mathf.FloorToInt(roundTimer)}s";
        }
    }

    // ========== TOWER SHOP ==========

    void CreateTowerShop(Transform parent)
    {
        if (availableTowers == null || availableTowers.Length == 0) return;

        // Shop panel (left side)
        GameObject shopPanel = CreatePanel(parent, "TowerShopPanel", new Color(0, 0, 0, 0.5f));
        RectTransform spRect = shopPanel.GetComponent<RectTransform>();
        spRect.anchorMin = new Vector2(0, 0);
        spRect.anchorMax = new Vector2(0.12f, 0.9f);
        spRect.sizeDelta = Vector2.zero;
        spRect.offsetMin = Vector2.zero;
        spRect.offsetMax = Vector2.zero;

        // Title
        GameObject shopTitle = CreateText(shopPanel.transform, "ShopTitle", "TOWERS", 24, Color.white);
        RectTransform stRect = shopTitle.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0, 0.92f);
        stRect.anchorMax = new Vector2(1, 1);
        stRect.sizeDelta = Vector2.zero;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // Tower buttons
        float yStep = 0.85f / Mathf.Max(availableTowers.Length, 1);
        for (int i = 0; i < availableTowers.Length; i++)
        {
            int index = i;
            TowerData td = availableTowers[i];
            if (td == null) continue;

            Color btnColor = Tower.GetTowerColor(td.towerType) * 0.7f;
            btnColor.a = 1f;
            string label = $"{td.towerName}\n{td.cost}g";

            float yPos = 0.88f - (i * yStep);

            // Create button relative to the shop panel
            GameObject btn = CreateButton(shopPanel.transform, $"TowerBtn_{td.towerName}", label, btnColor);
            RectTransform btnRect = btn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.05f, yPos - yStep + 0.02f);
            btnRect.anchorMax = new Vector2(0.95f, yPos);
            btnRect.sizeDelta = Vector2.zero;
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            // Font size adjustment
            TextMeshProUGUI tmpLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpLabel) tmpLabel.fontSize = 18;

            btn.GetComponent<Button>().onClick.AddListener(() => OnTowerSelected(td));
        }
    }

    void OnTowerSelected(TowerData data)
    {
        if (data.isProfessorTower)
        {
            FacultyData faculty = FindFacultyForTower(data);
            if (faculty != null && GameManager.Instance != null && !GameManager.Instance.IsProfessorTowerUnlocked(faculty))
            {
                Debug.Log($"Professor tower locked! Clear all {faculty.facultyName} courses first.");
                return;
            }
        }

        TowerPlacement.Instance?.StartPlacing(data, towerPrefab.GetComponent<Tower>());
    }

    FacultyData FindFacultyForTower(TowerData towerData)
    {
        if (GameManager.Instance == null) return null;
        foreach (FacultyData f in GameManager.Instance.allFaculties)
        {
            if (f.professorTower == towerData) return f;
        }
        return null;
    }

    // ========== GAME OVER UI ==========

    void CreateGameOverUI(Transform parent)
    {
        // --- Level Won Panel ---
        levelWonPanel = CreatePanel(parent, "LevelWonPanel", new Color(0, 0, 0, 0.85f));
        StretchFull(levelWonPanel);
        levelWonPanel.SetActive(false);

        CreateText(levelWonPanel.transform, "WonTitle", "LEVEL COMPLETE!", 48, Color.green);
        SetAnchored(levelWonPanel.transform.Find("WonTitle").gameObject, new Vector2(0.5f, 0.65f), new Vector2(600, 60));

        GameObject ceObj = CreateText(levelWonPanel.transform, "CreditsEarned", "+0 Credits", 32, Color.yellow);
        SetAnchored(ceObj, new Vector2(0.5f, 0.55f), new Vector2(400, 40));
        creditsEarnedText = ceObj.GetComponent<TextMeshProUGUI>();

        GameObject spObj = CreateText(levelWonPanel.transform, "SPEarned", "+0 Skill Points", 28, Color.cyan);
        SetAnchored(spObj, new Vector2(0.5f, 0.48f), new Vector2(400, 40));
        spEarnedText = spObj.GetComponent<TextMeshProUGUI>();

        GameObject contBtn = CreateButton(levelWonPanel.transform, "ContinueBtn", "Continue", new Color(0.2f, 0.6f, 0.3f));
        SetAnchored(contBtn, new Vector2(0.5f, 0.35f), new Vector2(250, 60));
        contBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance?.GoToOverworld());

        // --- Level Lost Panel ---
        levelLostPanel = CreatePanel(parent, "LevelLostPanel", new Color(0, 0, 0, 0.85f));
        StretchFull(levelLostPanel);
        levelLostPanel.SetActive(false);

        CreateText(levelLostPanel.transform, "LostTitle", "LEVEL FAILED", 48, new Color(1f, 0.3f, 0.3f));
        SetAnchored(levelLostPanel.transform.Find("LostTitle").gameObject, new Vector2(0.5f, 0.6f), new Vector2(600, 60));

        GameObject retryBtn = CreateButton(levelLostPanel.transform, "RetryBtn", "Retry", new Color(0.6f, 0.4f, 0.1f));
        SetAnchored(retryBtn, new Vector2(0.4f, 0.4f), new Vector2(200, 60));
        retryBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance?.RetryLevel());

        GameObject quitBtn = CreateButton(levelLostPanel.transform, "QuitBtn", "Back to Map", new Color(0.4f, 0.4f, 0.4f));
        SetAnchored(quitBtn, new Vector2(0.6f, 0.4f), new Vector2(200, 60));
        quitBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance?.GoToOverworld());

        // --- Graduation Panel ---
        graduationPanel = CreatePanel(parent, "GraduationPanel", new Color(0, 0, 0, 0.9f));
        StretchFull(graduationPanel);
        graduationPanel.SetActive(false);

        CreateText(graduationPanel.transform, "GradTitle", "CONGRATULATIONS!", 56, Color.yellow);
        SetAnchored(graduationPanel.transform.Find("GradTitle").gameObject, new Vector2(0.5f, 0.65f), new Vector2(700, 70));

        CreateText(graduationPanel.transform, "GradMsg", "You have graduated from HKU!", 36, Color.white);
        SetAnchored(graduationPanel.transform.Find("GradMsg").gameObject, new Vector2(0.5f, 0.5f), new Vector2(700, 50));

        GameObject menuBtn = CreateButton(graduationPanel.transform, "MenuBtn", "Main Menu", new Color(0.3f, 0.4f, 0.6f));
        SetAnchored(menuBtn, new Vector2(0.5f, 0.35f), new Vector2(250, 60));
        menuBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance?.GoToMainMenu());

        // Subscribe to game state changes
        GameManager.OnGameStateChanged += HandleGameState;
    }

    void HandleGameState(GameState state)
    {
        levelWonPanel.SetActive(false);
        levelLostPanel.SetActive(false);
        graduationPanel.SetActive(false);

        switch (state)
        {
            case GameState.LevelWon:
                levelWonPanel.SetActive(true);
                LevelData level = GameManager.Instance?.GetCurrentLevel();
                if (level != null)
                {
                    bool isReplay = GameManager.Instance != null &&
                        GameManager.Instance.IsCourseCompleted(
                            GameManager.Instance.currentFaculty,
                            GameManager.Instance.currentCourseIndex);
                    if (isReplay)
                    {
                        creditsEarnedText.text = "Already Completed";
                        spEarnedText.text = "(No additional rewards)";
                    }
                    else
                    {
                        creditsEarnedText.text = $"+{level.creditsReward} Credits";
                        spEarnedText.text = $"+{level.skillPointsReward} Skill Points";
                    }
                }
                break;
            case GameState.LevelLost:
                levelLostPanel.SetActive(true);
                break;
            case GameState.Graduated:
                graduationPanel.SetActive(true);
                break;
        }
    }

    // ========== LEVEL SETUP ==========

    void SetupLevel()
    {
        LevelData level = GameManager.Instance?.GetCurrentLevel();
        if (level == null)
        {
            Debug.LogWarning("No level data found. Assign faculties in GameBootstrap and start from MainMenu.");
            // Set some defaults so the scene doesn't crash
            currencyManager.SetStartingGold(200);
            livesManager.SetStartingLives(20);
            return;
        }

        pathManager.LoadPathForTier(level.pathDifficultyTier);
        waveSpawner.Setup(level.rounds, pathManager.currentWaypoints);
        currencyManager.SetStartingGold(level.startingGold);
        livesManager.SetStartingLives(level.startingLives);

        // Apply classroom background if assigned
        if (level.classroomBackground != null)
        {
            GameObject bg = new GameObject("LevelBackground");
            SpriteRenderer bgSR = bg.AddComponent<SpriteRenderer>();
            bgSR.sprite = level.classroomBackground;
            bgSR.sortingOrder = -10;
            // Scale to fill camera view
            Camera cam = Camera.main;
            if (cam != null)
            {
                float camHeight = cam.orthographicSize * 2f;
                float camWidth = camHeight * cam.aspect;
                float spriteW = bgSR.sprite.bounds.size.x;
                float spriteH = bgSR.sprite.bounds.size.y;
                bg.transform.localScale = new Vector3(camWidth / spriteW, camHeight / spriteH, 1f);
            }
        }
    }

    void OnDestroy()
    {
        GameManager.OnGameStateChanged -= HandleGameState;
        if (onGoldChanged != null) CurrencyManager.OnGoldChanged -= onGoldChanged;
        if (onLivesChanged != null) LivesManager.OnLivesChanged -= onLivesChanged;
        if (onRoundStart != null) WaveSpawner.OnRoundStart -= onRoundStart;
        if (onEnemyCountChanged != null) WaveSpawner.OnEnemyCountChanged -= onEnemyCountChanged;
    }

    // ========== UI HELPERS ==========

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
