using UnityEngine;
using UnityEngine.UI;
using TMPro;
// Marathon mode hook is in this same global namespace.

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

    // Collapsible side shops
    private GameObject towerShopPanel;
    private GameObject heroShopPanel;
    private Button towerShopToggleButton;
    private Button heroShopToggleButton;
    private TextMeshProUGUI towerShopToggleLabel;
    private TextMeshProUGUI heroShopToggleLabel;

    // Marathon buff history
    private GameObject buffHistoryPanel;
    private Button buffHistoryToggleButton;
    private TextMeshProUGUI buffHistoryToggleLabel;
    private TextMeshProUGUI buffHistoryBody;

    void Start()
    {
        DeployedUniqueRegistry.Reset();
        CreateManagers();
        EnsureTowerPrefabIsClickable();
        CreateCamera();
        CreateEventSystem();
        Canvas canvas = CreateCanvas();
        CreateHUD(canvas.transform);
        EnsureStarterAndUnlockedProfessors();
        CreateBuildAndUpgradeUI(canvas);
        CreateTowerShop(canvas.transform);
        CreateGameOverUI(canvas.transform);
        CreateHeroSkillUI(canvas.transform);
        CreateRoundCompleteBanner(canvas);
        EnsureInGameSlotDebugEditor();
        BringControlButtonsToFront();
        SetupLevel();
    }

    void EnsureInGameSlotDebugEditor()
    {
        if (FindFirstObjectByType<InGameSlotDebugEditor>() != null) return;
        GameObject go = new GameObject("InGameSlotDebugEditor");
        go.AddComponent<InGameSlotDebugEditor>();
    }

    void CreateRoundCompleteBanner(Canvas canvas)
    {
        GameObject go = new GameObject("RoundCompleteBannerHost");
        var banner = go.AddComponent<RoundCompleteBanner>();
        banner.Build(canvas);
    }

    void EnsureTowerPrefabIsClickable()
    {
        if (towerPrefab == null) return;
        if (towerPrefab.GetComponent<Collider2D>() == null)
        {
            // Adding to a prefab GameObject in memory only affects this play session,
            // which is what we want for runtime-built test scenes.
            CircleCollider2D col = towerPrefab.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.45f;
        }
    }

    void CreateBuildAndUpgradeUI(Canvas canvas)
    {
        TowerPlacement.Instance?.SetTowerPrefab(towerPrefab != null ? towerPrefab.GetComponent<Tower>() : null);

        GameObject bu = new GameObject("BuildAndUpgradeUI");
        BuildAndUpgradeUI ui = bu.AddComponent<BuildAndUpgradeUI>();
        Tower tp = towerPrefab != null ? towerPrefab.GetComponent<Tower>() : null;
        ui.Build(canvas, availableTowers, tp);
    }

    /// <summary>
    /// Restores the always-on left vertical sidebar of regular (non-professor)
    /// towers, used to drag-place during the prep phase between rounds.
    /// </summary>
    void CreateNormalTowerSidebar(Transform parent)
    {
        if (availableTowers == null || availableTowers.Length == 0) return;

        var regulars = new System.Collections.Generic.List<TowerData>();
        foreach (TowerData td in availableTowers)
        {
            if (td == null) continue;
            if (td.isProfessorTower) continue;
            regulars.Add(td);
        }
        if (regulars.Count == 0) return;

        BuildShopPanel(parent, "TowerShopPanel", "TOWERS",
                       new Vector2(0f, 0f), new Vector2(0.10f, 0.85f),
                       regulars, isProfessor: false);
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

        // Add (or refresh) pan + zoom controls. Endless / Marathon want a
        // wider initial zoom so the player can see the full campus-shaped map.
        Camera mainCam = Camera.main;
        if (mainCam.GetComponent<CameraController>() == null)
            mainCam.gameObject.AddComponent<CameraController>();
        if (EndlessMode.IsActive)
        {
            mainCam.orthographicSize = 12f;
            CameraController cc = mainCam.GetComponent<CameraController>();
            cc.maxOrthoSize = 22f;
            cc.minOrthoSize = 5f;
        }
        else if (MarathonMode.IsActive)
        {
            mainCam.orthographicSize = 9f;
            CameraController cc = mainCam.GetComponent<CameraController>();
            cc.maxOrthoSize = 16f;
            cc.minOrthoSize = 4f;
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
        SetAnchored(goldObj, new Vector2(0.11f, 0.5f), new Vector2(220, 50));
        goldText = goldObj.GetComponent<TextMeshProUGUI>();

        // Lives text
        GameObject livesObj = CreateText(topBar.transform, "LivesText", "Lives: 20", 28, new Color(1f, 0.4f, 0.4f));
        SetAnchored(livesObj, new Vector2(0.28f, 0.5f), new Vector2(220, 50));
        livesText = livesObj.GetComponent<TextMeshProUGUI>();

        // Round text
        string roundLabel = MarathonMode.IsActive
            ? $"Round: 0/{MarathonMode.TOTAL_WAVES}"
            : (EndlessMode.IsActive ? "Round: 0 (Endless)" : "Round: 0/0");
        GameObject roundObj = CreateText(topBar.transform, "RoundText", roundLabel, 28, Color.white);
        SetAnchored(roundObj, new Vector2(0.45f, 0.5f), new Vector2(220, 50));
        roundText = roundObj.GetComponent<TextMeshProUGUI>();

        // Round timer text (beside round)
        GameObject timerObj = CreateText(topBar.transform, "TimerText", "Time: 0s", 22, new Color(0.9f, 0.9f, 0.9f));
        SetAnchored(timerObj, new Vector2(0.60f, 0.5f), new Vector2(170, 50));
        timerText = timerObj.GetComponent<TextMeshProUGUI>();

        // Enemies left text
        GameObject enemiesObj = CreateText(topBar.transform, "EnemiesLeftText", "Enemies: 0", 22, new Color(1f, 0.6f, 0.3f));
        SetAnchored(enemiesObj, new Vector2(0.74f, 0.5f), new Vector2(220, 50));
        enemiesLeftText = enemiesObj.GetComponent<TextMeshProUGUI>();

        // Start Round button (bottom right)
        GameObject startBtn = CreateButton(parent, "StartRoundBtn", "START ROUND", new Color(0.2f, 0.5f, 0.8f));
        SetAnchored(startBtn, new Vector2(0.85f, 0.06f), new Vector2(250, 60));
        startRoundButton = startBtn.GetComponent<Button>();
        startRoundButton.onClick.AddListener(() => WaveSpawner.Instance?.StartNextRound());

        // Pause button (top-right nav)
        GameObject pBtn = CreateButton(topBar.transform, "PauseBtn", "| |", new Color(0.4f, 0.4f, 0.4f));
        SetAnchored(pBtn, new Vector2(0.965f, 0.5f), new Vector2(62, 36));
        pauseButton = pBtn.GetComponent<Button>();
        pauseBtnLabel = pBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (pauseBtnLabel) pauseBtnLabel.fontSize = 20;
        pauseButton.onClick.AddListener(TogglePause);

        // Left-corner circular controls for hero + skill (+ buffs in marathon).
        GameObject heroBtn = CreateCircularButton(parent, "HeroShopToggleBtn", "H+", new Color(0.55f, 0.38f, 0.05f));
        SetAnchored(heroBtn, new Vector2(0.05f, 0.12f), new Vector2(72, 72));
        heroShopToggleButton = heroBtn.GetComponent<Button>();
        heroShopToggleLabel = heroBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (heroShopToggleLabel) heroShopToggleLabel.fontSize = 24;
        heroShopToggleButton.onClick.AddListener(ToggleHeroShopPanel);

        // Buff history toggle for marathon runs.
        if (MarathonMode.IsActive)
        {
            GameObject buffBtn = CreateCircularButton(parent, "BuffHistoryBtn", "B+", new Color(0.18f, 0.5f, 0.45f));
            SetAnchored(buffBtn, new Vector2(0.05f, 0.21f), new Vector2(72, 72));
            buffHistoryToggleButton = buffBtn.GetComponent<Button>();
            buffHistoryToggleLabel = buffBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (buffHistoryToggleLabel) buffHistoryToggleLabel.fontSize = 24;
            buffHistoryToggleButton.onClick.AddListener(ToggleBuffHistoryPanel);
            CreateBuffHistoryPanel(parent);
        }

        // Speed toggle button in nav bar (cycles 1x → 2x → 3x)
        GameObject spdBtn = CreateButton(topBar.transform, "SpeedBtn", "1x", new Color(0.18f, 0.45f, 0.25f));
        SetAnchored(spdBtn, new Vector2(0.90f, 0.5f), new Vector2(82, 36));
        Button spdBtnComp = spdBtn.GetComponent<Button>();
        TextMeshProUGUI spdLabel = spdBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (spdLabel) spdLabel.fontSize = 17;
        if (GameSpeedController.Instance == null)
        {
            var gsHost = new GameObject("--- GameSpeed ---");
            gsHost.AddComponent<GameSpeedController>();
        }
        spdBtnComp.onClick.AddListener(() =>
        {
            GameSpeedController.Instance?.Cycle();
            if (spdLabel != null)
            {
                float m = GameSpeedController.CurrentMultiplier;
                if      (Mathf.Approximately(m, 1f)) spdLabel.text = "1x";
                else if (Mathf.Approximately(m, 2f)) spdLabel.text = "2x";
                else                                  spdLabel.text = "3x";
            }
        });

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

        string quitLabel = MarathonMode.IsActive ? "Quit to Menu" : "Quit to Map";
        GameObject quitBtn = CreateButton(pausePanel.transform, "QuitToMapBtn", quitLabel, new Color(0.6f, 0.2f, 0.2f));
        SetAnchored(quitBtn, new Vector2(0.5f, 0.32f), new Vector2(250, 60));
        quitBtn.GetComponent<Button>().onClick.AddListener(QuitFromGameplay);
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

    void ToggleTowerShopPanel()
    {
        if (towerShopPanel == null) return;
        bool show = !towerShopPanel.activeSelf;
        towerShopPanel.SetActive(show);
        if (show && heroShopPanel != null) heroShopPanel.SetActive(false);
        SyncNavButtonLabels();
        BringControlButtonsToFront();
    }

    void ToggleHeroShopPanel()
    {
        if (heroShopPanel == null) return;
        bool show = !heroShopPanel.activeSelf;
        heroShopPanel.SetActive(show);
        if (show && towerShopPanel != null) towerShopPanel.SetActive(false);
        SyncNavButtonLabels();
        BringControlButtonsToFront();
    }

    void ToggleBuffHistoryPanel()
    {
        if (buffHistoryPanel == null) return;

        bool show = !buffHistoryPanel.activeSelf;
        if (show) RefreshBuffHistoryPanel();
        buffHistoryPanel.SetActive(show);
        SyncNavButtonLabels();
        BringControlButtonsToFront();
    }

    void CreateBuffHistoryPanel(Transform parent)
    {
        buffHistoryPanel = CreatePanel(parent, "BuffHistoryPanel", new Color(0.04f, 0.09f, 0.12f, 1f));
        RectTransform rt = buffHistoryPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.10f, 0.14f);
        rt.anchorMax = new Vector2(0.48f, 0.82f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        GameObject header = CreatePanel(buffHistoryPanel.transform, "HeaderBar", new Color(0.13f, 0.38f, 0.40f, 1f));
        RectTransform hr = header.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 0.88f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.offsetMin = Vector2.zero;
        hr.offsetMax = Vector2.zero;

        GameObject title = CreateText(buffHistoryPanel.transform, "BuffHistoryTitle", "Buff Picks", 36, new Color(1f, 0.96f, 0.82f));
        RectTransform tr = title.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0.88f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        GameObject bodyFrame = CreatePanel(buffHistoryPanel.transform, "BuffHistoryBodyFrame", new Color(0.07f, 0.15f, 0.20f, 1f));
        RectTransform fr = bodyFrame.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0.04f, 0.05f);
        fr.anchorMax = new Vector2(0.96f, 0.85f);
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;

        GameObject body = CreateText(bodyFrame.transform, "BuffHistoryBody", "No buffs chosen yet.", 24, new Color(0.92f, 0.95f, 1f));
        RectTransform br = body.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.05f, 0.05f);
        br.anchorMax = new Vector2(0.95f, 0.95f);
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;

        buffHistoryBody = body.GetComponent<TextMeshProUGUI>();
        if (buffHistoryBody != null)
        {
            buffHistoryBody.alignment = TextAlignmentOptions.TopLeft;
            buffHistoryBody.enableWordWrapping = true;
            buffHistoryBody.overflowMode = TextOverflowModes.Overflow;
            buffHistoryBody.lineSpacing = 8f;
        }

        buffHistoryPanel.SetActive(false);
    }

    void RefreshBuffHistoryPanel()
    {
        if (buffHistoryBody == null) return;

        var picks = MarathonMode.PickedBuffHistory;
        if (picks == null || picks.Count == 0)
        {
            buffHistoryBody.text = "No buffs chosen yet.";
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < picks.Count; i++)
            sb.AppendLine($"{i + 1}. {picks[i]}");

        buffHistoryBody.text = sb.ToString();
    }

    void SyncNavButtonLabels()
    {
        if (towerShopToggleLabel != null)
            towerShopToggleLabel.text = (towerShopPanel != null && towerShopPanel.activeSelf) ? "T-" : "T+";
        if (heroShopToggleLabel != null)
            heroShopToggleLabel.text = (heroShopPanel != null && heroShopPanel.activeSelf) ? "H-" : "H+";
        if (buffHistoryToggleLabel != null)
            buffHistoryToggleLabel.text = (buffHistoryPanel != null && buffHistoryPanel.activeSelf) ? "B-" : "B+";
    }

    void BringControlButtonsToFront()
    {
        // Keep the compact control row always clickable above any opened panel.
        if (towerShopToggleButton != null) towerShopToggleButton.transform.SetAsLastSibling();
        if (heroShopToggleButton != null) heroShopToggleButton.transform.SetAsLastSibling();
        if (buffHistoryToggleButton != null) buffHistoryToggleButton.transform.SetAsLastSibling();
    }

    void SubscribeHUD()
    {
        onGoldChanged = (g) => { if (goldText) goldText.text = $"Gold: {g}"; };
        onLivesChanged = (l) => { if (livesText) livesText.text = $"Lives: {l}"; };
        onRoundStart = (r) =>
        {
            if (roundText == null) return;
            if (MarathonMode.IsActive)
            {
                int shown = Mathf.Clamp(r + 1, 0, MarathonMode.TOTAL_WAVES);
                roundText.text = $"Round: {shown}/{MarathonMode.TOTAL_WAVES}";
            }
            else if (EndlessMode.IsActive)
            {
                roundText.text = $"Round: {r + 1} (Endless)";
            }
            else
            {
                int total = WaveSpawner.Instance != null ? WaveSpawner.Instance.rounds.Length : 0;
                roundText.text = $"Round: {r + 1}/{total}";
            }
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

        // Keep control labels in sync if panels are closed by other UI controls.
        SyncNavButtonLabels();
    }

    // ========== TOWER SHOP ==========

    /// <summary>
    /// Make sure the shop always offers at least one hero the player can use
    /// from level one (the "Self" starter), and any faculty professor towers
    /// the player has already unlocked by clearing that faculty.
    /// </summary>
    void EnsureStarterAndUnlockedProfessors()
    {
        var list = new System.Collections.Generic.List<TowerData>();
        if (availableTowers != null) list.AddRange(availableTowers);

        // 1. Starter "Self" hero — always available
        bool hasStarter = list.Exists(t => t != null && t.isProfessorTower && t.towerName == "Self (Player)");
        if (!hasStarter)
            list.Add(BuildStarterPlayerHero());

        // 1b. "Self 2" — also granted from the start (ground-targeted DoT pool).
        bool hasSelf2 = list.Exists(t => t != null && t.isProfessorTower && t.towerName == "Self 2");
        if (!hasSelf2)
            list.Add(BuildStarterPlayerHero2());

        // 2. Unlocked faculty professors (only those configured with a hero skill)
        if (GameManager.Instance != null && GameManager.Instance.allFaculties != null)
        {
            foreach (FacultyData f in GameManager.Instance.allFaculties)
            {
                if (f == null || f.professorTower == null) continue;
                if (f.professorTower.heroSkill == null) continue;       // skip pre-hero-refactor profs
                if (!GameManager.Instance.IsProfessorTowerUnlocked(f)) continue;
                if (!list.Contains(f.professorTower))
                    list.Add(f.professorTower);
            }
        }

        availableTowers = list.ToArray();
    }

    private static TowerData _cachedStarterHero;

    /// <summary>Creates (and caches) a free in-memory starter hero representing the player.</summary>
    static TowerData BuildStarterPlayerHero()
    {
        if (_cachedStarterHero != null) return _cachedStarterHero;

        HeroSkillData skill = ScriptableObject.CreateInstance<HeroSkillData>();
        skill.skillName        = "Pep Talk";
        skill.description      = "Briefly empowers towers in a wide circle (+50% damage).";
        skill.cooldown         = 18f;
        skill.effect           = HeroSkillEffect.EmpowerAllies;
        skill.empowerMultiplier = 1.5f;
        skill.empowerDuration  = 6f;
        // Wider than a typical sniper tower so the Self hero can buff a small cluster.
        skill.radius           = 5f;

        TowerData td = ScriptableObject.CreateInstance<TowerData>();
        td.towerName        = "Self (Player)";
        td.towerType        = TowerType.Professor;
        td.cost             = 0;          // free starter
        td.range            = 0f;
        td.fireRate         = 0f;
        td.damage           = 0;
        td.isProfessorTower = true;
        td.heroSkill        = skill;
        td.unique           = true;       // only one Self per level

        _cachedStarterHero = td;
        return td;
    }

    private static TowerData _cachedStarterHero2;

    /// <summary>Creates (and caches) the second free starter hero — a
    /// ground-targeted DoT pool. Granted to the player from level one.</summary>
    static TowerData BuildStarterPlayerHero2()
    {
        if (_cachedStarterHero2 != null) return _cachedStarterHero2;

        HeroSkillData skill = ScriptableObject.CreateInstance<HeroSkillData>();
        skill.skillName        = "Study Group";
        skill.description      = "Drops a focused study area dealing damage over time.";
        skill.cooldown         = 20f;
        skill.effect           = HeroSkillEffect.GroundTargetedAOE;
        skill.radius           = 2.5f;
        skill.aoeDamagePerTick = 15;
        skill.aoeTickInterval  = 0.4f;
        skill.aoeDuration      = 3f;
        skill.aoeDamageType    = DamageType.Pierce;

        TowerData td = ScriptableObject.CreateInstance<TowerData>();
        td.towerName        = "Self 2";
        td.towerType        = TowerType.Professor;
        td.cost             = 0;
        td.range            = 0f;
        td.fireRate         = 0f;
        td.damage           = 0;
        td.isProfessorTower = true;
        td.heroSkill        = skill;
        td.unique           = true;

        _cachedStarterHero2 = td;
        return td;
    }

    void CreateTowerShop(Transform parent)
    {
        // Always make sure the player has a starter hero ("Self") plus any
        // professors unlocked via cleared faculties — even if the inspector
        // list only contains regular towers.
        EnsureStarterAndUnlockedProfessors();

        if (availableTowers == null || availableTowers.Length == 0) return;

        // Split towers into regular and professor lists
        var regularTowers  = new System.Collections.Generic.List<TowerData>();
        var professorTowers = new System.Collections.Generic.List<TowerData>();
        foreach (TowerData td in availableTowers)
        {
            if (td == null) continue;
            if (td.isProfessorTower) professorTowers.Add(td);
            else                    regularTowers.Add(td);
        }

        towerShopPanel = null;
        heroShopPanel = null;

        // Regular tower shop (left) — collapsed by default.
        if (regularTowers.Count > 0)
        {
            towerShopPanel = BuildShopPanel(parent, "TowerShopPanel", "TOWERS",
                                            new Vector2(0f, 0f), new Vector2(0.12f, 0.9f),
                                            regularTowers, isProfessor: false);
            towerShopPanel.SetActive(false);
        }

        // Hero / professor shop (right) — collapsed by default.
        if (professorTowers.Count > 0)
        {
            heroShopPanel = BuildShopPanel(parent, "ProfessorShopPanel", "HEROES",
                                           new Vector2(0.86f, 0.10f), new Vector2(0.995f, 0.87f),
                                           professorTowers, isProfessor: true);
            heroShopPanel.SetActive(false);
        }

        SyncNavButtonLabels();
        BringControlButtonsToFront();
    }

    /// <summary>Destroy and recreate the side shop panels using the current
    /// <see cref="availableTowers"/>. Called when marathon buffs unlock new
    /// towers mid-run so the side shop reflects them immediately.</summary>
    public void RebuildTowerShop()
    {
        bool towerWasOpen = towerShopPanel != null && towerShopPanel.activeSelf;
        bool heroWasOpen = heroShopPanel != null && heroShopPanel.activeSelf;

        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        Transform parent = canvas.transform;
        var existingTowers = parent.Find("TowerShopPanel");
        if (existingTowers != null) Destroy(existingTowers.gameObject);
        var existingHeroes = parent.Find("ProfessorShopPanel");
        if (existingHeroes != null) Destroy(existingHeroes.gameObject);

        towerShopPanel = null;
        heroShopPanel = null;
        CreateTowerShop(parent);

        if (towerShopPanel != null) towerShopPanel.SetActive(towerWasOpen);
        if (heroShopPanel != null) heroShopPanel.SetActive(heroWasOpen);
        SyncNavButtonLabels();
        BringControlButtonsToFront();
    }

    GameObject BuildShopPanel(Transform parent, string panelName, string title,
                              Vector2 anchorMin, Vector2 anchorMax,
                              System.Collections.Generic.List<TowerData> towers,
                              bool isProfessor)
    {
        Color panelBg = isProfessor
            ? new Color(0.12f, 0.08f, 0.02f, 1f)   // solid warm dark-gold hero panel
            : new Color(0f, 0f, 0f, 0.82f);

        GameObject shopPanel = CreatePanel(parent, panelName, panelBg);
        RectTransform spRect = shopPanel.GetComponent<RectTransform>();
        spRect.anchorMin = anchorMin;
        spRect.anchorMax = anchorMax;
        spRect.sizeDelta = Vector2.zero;
        spRect.offsetMin = Vector2.zero;
        spRect.offsetMax = Vector2.zero;

        // Solid header bar for clearer visual hierarchy.
        Color headerBg = isProfessor
            ? new Color(0.70f, 0.49f, 0.10f, 1f)
            : new Color(0.20f, 0.30f, 0.45f, 1f);
        GameObject headerBar = CreatePanel(shopPanel.transform, "HeaderBar", headerBg);
        RectTransform hbRect = headerBar.GetComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0f, 0.89f);
        hbRect.anchorMax = new Vector2(1f, 1f);
        hbRect.offsetMin = Vector2.zero;
        hbRect.offsetMax = Vector2.zero;

        // Title
        Color titleColor = isProfessor ? new Color(0.10f, 0.06f, 0.01f) : Color.white;
        int titleSize = isProfessor ? 30 : 22;
        GameObject shopTitle = CreateText(shopPanel.transform, "ShopTitle", title, titleSize, titleColor);
        RectTransform stRect = shopTitle.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0f, 0.90f);
        stRect.anchorMax = new Vector2(1f, 1f);
        stRect.sizeDelta = Vector2.zero;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // Subtitle for professors
        if (isProfessor)
        {
            GameObject sub = CreateText(shopPanel.transform, "SubTitle", "Select hero and place on built tower", 18, new Color(0.96f, 0.93f, 0.82f));
            RectTransform subRT = sub.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.03f, 0.82f);
            subRT.anchorMax = new Vector2(0.97f, 0.89f);
            subRT.sizeDelta = Vector2.zero;
            subRT.offsetMin = Vector2.zero;
            subRT.offsetMax = Vector2.zero;
        }

        // Only regular tower shop keeps an explicit close button.
        // Hero shop is controlled by the left-circle toggle now.
        if (!isProfessor)
        {
            GameObject closeBtn = CreateButton(shopPanel.transform, "CloseBtn", "X", new Color(0.2f, 0.2f, 0.2f, 0.9f));
            SetAnchored(closeBtn, new Vector2(0.92f, 0.955f), new Vector2(38, 30));
            TextMeshProUGUI closeLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (closeLabel != null) closeLabel.fontSize = 18;
            closeBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                shopPanel.SetActive(false);
                SyncNavButtonLabels();
                BringControlButtonsToFront();
            });
        }

        // Tower buttons. Cap per-button height so a 1-hero list doesn't
        // produce a giant button that fills the entire panel.
        float startY     = isProfessor ? 0.80f : 0.88f;
        float totalSpace = startY;
        float maxStep    = isProfessor ? 0.19f : 0.18f;  // taller hero buttons for larger text
        float yStep      = Mathf.Min(maxStep, totalSpace / Mathf.Max(towers.Count, 1));

        for (int i = 0; i < towers.Count; i++)
        {
            TowerData td = towers[i];
            bool prof    = isProfessor;

            Color btnColor = prof
                ? new Color(0.30f, 0.20f, 0.05f)
                : Tower.GetTowerColor(td.towerType) * 0.7f;
            btnColor.a = 1f;

            string label = $"{td.towerName}\n{td.cost}g";
            if (prof && td.heroSkill != null)
                label += $"\n{td.heroSkill.skillName}";

            float yPos = startY - (i * yStep);

            GameObject btn = CreateButton(shopPanel.transform, $"Btn_{td.towerName}", label, btnColor);
            RectTransform btnRect = btn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.05f, yPos - yStep + 0.02f);
            btnRect.anchorMax = new Vector2(0.95f, yPos);
            btnRect.sizeDelta = Vector2.zero;
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpLabel)
            {
                tmpLabel.fontSize = prof ? 20 : 16;
                tmpLabel.fontStyle = FontStyles.Bold;
            }

            btn.GetComponent<Button>().onClick.AddListener(() => OnTowerSelected(td, prof));
        }

        return shopPanel;
    }

    void OnTowerSelected(TowerData data, bool isProfessor)
    {
        // Faculty-lock check (applies to both professors and regular unlockable towers)
        if (data.isProfessorTower)
        {
            FacultyData faculty = FindFacultyForTower(data);
            if (faculty != null && GameManager.Instance != null && !GameManager.Instance.IsProfessorTowerUnlocked(faculty))
            {
                Debug.Log($"Professor locked! Clear all {faculty.facultyName} courses first.");
                return;
            }
        }

        if (isProfessor)
            TowerPlacement.Instance?.StartPlacing(data, towerPrefab.GetComponent<Tower>());
        else
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

        string continueLabel = MarathonMode.IsActive ? "Main Menu" : "Continue";
        GameObject contBtn = CreateButton(levelWonPanel.transform, "ContinueBtn", continueLabel, new Color(0.2f, 0.6f, 0.3f));
        SetAnchored(contBtn, new Vector2(0.5f, 0.35f), new Vector2(250, 60));
        contBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (MarathonMode.IsActive) GameManager.Instance?.GoToMainMenu();
            else                       GameManager.Instance?.GoToOverworld();
        });

        // --- Level Lost Panel ---
        levelLostPanel = CreatePanel(parent, "LevelLostPanel", new Color(0, 0, 0, 0.85f));
        StretchFull(levelLostPanel);
        levelLostPanel.SetActive(false);

        CreateText(levelLostPanel.transform, "LostTitle", "LEVEL FAILED", 48, new Color(1f, 0.3f, 0.3f));
        SetAnchored(levelLostPanel.transform.Find("LostTitle").gameObject, new Vector2(0.5f, 0.6f), new Vector2(600, 60));

        string retryLabel = MarathonMode.IsActive ? "Restart Marathon" : "Retry";
        GameObject retryBtn = CreateButton(levelLostPanel.transform, "RetryBtn", retryLabel, new Color(0.6f, 0.4f, 0.1f));
        SetAnchored(retryBtn, new Vector2(0.4f, 0.4f), new Vector2(200, 60));
        retryBtn.GetComponent<Button>().onClick.AddListener(RetryCurrentMode);

        string quitLabel = MarathonMode.IsActive ? "Main Menu" : "Back to Map";
        GameObject quitBtn = CreateButton(levelLostPanel.transform, "QuitBtn", quitLabel, new Color(0.4f, 0.4f, 0.4f));
        SetAnchored(quitBtn, new Vector2(0.6f, 0.4f), new Vector2(200, 60));
        quitBtn.GetComponent<Button>().onClick.AddListener(QuitFromGameplay);

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
                    }
                    else
                    {
                        creditsEarnedText.text = $"+{level.creditsReward} Credits";
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

    void RetryCurrentMode()
    {
        Time.timeScale = 1f;
        if (MarathonMode.IsActive)
        {
            MarathonMode.Launch();
            return;
        }

        if (EndlessMode.IsActive)
        {
            EndlessMode.Launch();
            return;
        }

        GameManager.Instance?.RetryLevel();
    }

    void QuitFromGameplay()
    {
        Time.timeScale = 1f;
        if (MarathonMode.IsActive || EndlessMode.IsActive)
            GameManager.Instance?.GoToMainMenu();
        else
            GameManager.Instance?.GoToOverworld();
    }

    // ========== HERO SKILL UI ==========

    void CreateHeroSkillUI(Transform parent)
    {
        GameObject heroUIObj = new GameObject("HeroSkillUI");
        HeroSkillUI heroSkillUI = heroUIObj.AddComponent<HeroSkillUI>();
        heroSkillUI.Build(parent);
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
    FitCameraToPlayableBoundsAndClampZoom();
        waveSpawner.Setup(level.rounds, pathManager.currentWaypoints);
        currencyManager.SetStartingGold(level.startingGold);
        livesManager.SetStartingLives(level.startingLives);

        // Initial round label before the first wave starts.
        if (roundText != null)
        {
            if (MarathonMode.IsActive)
                roundText.text = $"Round: 0/{MarathonMode.TOTAL_WAVES}";
            else if (EndlessMode.IsActive)
                roundText.text = "Round: 0 (Endless)";
            else
            {
                int total = level.rounds != null ? level.rounds.Length : 0;
                roundText.text = $"Round: 0/{total}";
            }
        }

        // Hand off endless-mode wave generation to WaveSpawner if active.
        if (EndlessMode.IsActive)
        {
            waveSpawner.isEndless = true;
            waveSpawner.nextWaveProvider = EndlessMode.GenerateWave;
        }
        else if (MarathonMode.IsActive)
        {
            // Marathon uses the same provider hook but the generator returns
            // null after wave 40, which causes WaveSpawner to raise
            // OnAllRoundsComplete and end the run cleanly.
            waveSpawner.isEndless = true;
            waveSpawner.nextWaveProvider = round =>
                round >= MarathonMode.TOTAL_WAVES ? null : MarathonMode.GenerateWave(round);
        }

        // Apply classroom background if assigned
        if (level.classroomBackground != null)
        {
            GameObject bg = new GameObject("LevelBackground");
            SpriteRenderer bgSR = bg.AddComponent<SpriteRenderer>();
            bgSR.sprite = level.classroomBackground;
            bgSR.sortingOrder = -10;
            bg.transform.position = new Vector3(0f, 0f, 5f);

            // Keep background in world-space and fit to the playable map area
            // so zooming out doesn't make it look like a tiny UI image.
            MarathonBackgroundFitter fitter = bg.AddComponent<MarathonBackgroundFitter>();
            fitter.followCamera = false;
            fitter.overscan = 1.3f;
        }
    }

    void FitCameraToPlayableBoundsAndClampZoom()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        CameraController cc = cam.GetComponent<CameraController>();
        if (cc == null) return;

        Bounds bounds;
        if (!TryGetPlayableBounds(out bounds)) return;

        // Small margin so edge slots stay visible without allowing huge empty areas.
        bounds.Expand(new Vector3(1.2f, 1.2f, 0f));

        cc.worldMin = new Vector2(bounds.min.x, bounds.min.y);
        cc.worldMax = new Vector2(bounds.max.x, bounds.max.y);

        // Limit max zoom-out to roughly the map extent.
        float fitByHeight = bounds.size.y * 0.5f;
        float fitByWidth = (bounds.size.x * 0.5f) / Mathf.Max(0.01f, cam.aspect);
        float fitOrtho = Mathf.Max(fitByHeight, fitByWidth);
        float desiredMaxOrtho = fitOrtho;

        cc.maxOrthoSize = Mathf.Max(cc.minOrthoSize + 0.5f, desiredMaxOrtho);
        if (cam.orthographicSize > cc.maxOrthoSize)
            cam.orthographicSize = cc.maxOrthoSize;

        // Start centered on the current playable region.
        Vector3 p = cam.transform.position;
        p.x = bounds.center.x;
        p.y = bounds.center.y;
        cam.transform.position = p;
    }

    bool TryGetPlayableBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        if (pathManager == null || pathManager.currentWaypoints == null)
            return false;

        if (pathManager.currentWaypoints.points != null)
        {
            foreach (Transform t in pathManager.currentWaypoints.points)
                Encapsulate(t, ref initialized, ref bounds);
        }

        if (pathManager.currentWaypoints.spawnPoints != null)
        {
            foreach (Transform t in pathManager.currentWaypoints.spawnPoints)
                Encapsulate(t, ref initialized, ref bounds);
        }

        Encapsulate(pathManager.currentWaypoints.exitPoint, ref initialized, ref bounds);

        if (pathManager.currentWaypoints.perSpawnPaths != null)
        {
            foreach (Transform[] chain in pathManager.currentWaypoints.perSpawnPaths)
            {
                if (chain == null) continue;
                foreach (Transform t in chain)
                    Encapsulate(t, ref initialized, ref bounds);
            }
        }

        if (pathManager.currentTowerSlots != null)
        {
            foreach (TowerSlot slot in pathManager.currentTowerSlots)
                Encapsulate(slot != null ? slot.transform : null, ref initialized, ref bounds);
        }

        return initialized;
    }

    static void Encapsulate(Transform t, ref bool initialized, ref Bounds bounds)
    {
        if (t == null) return;

        if (!initialized)
        {
            bounds = new Bounds(t.position, Vector3.zero);
            initialized = true;
        }
        else
        {
            bounds.Encapsulate(t.position);
        }
    }

    void OnDestroy()
    {
        GameManager.OnGameStateChanged -= HandleGameState;
        if (onGoldChanged != null) CurrencyManager.OnGoldChanged -= onGoldChanged;
        if (onLivesChanged != null) LivesManager.OnLivesChanged -= onLivesChanged;
        if (onRoundStart != null) WaveSpawner.OnRoundStart -= onRoundStart;
        if (onEnemyCountChanged != null) WaveSpawner.OnEnemyCountChanged -= onEnemyCountChanged;

        // Endless run is always tied to the gameplay scene's lifetime.
        if (EndlessMode.IsActive) EndlessMode.EndRun();
        if (MarathonMode.IsActive) MarathonMode.EndRun();
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

    GameObject CreateCircularButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject btnObj = CreateButton(parent, name, label, bgColor);
        Image img = btnObj.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = RuntimeSprite.Circle;
            img.type = Image.Type.Sliced;
            img.preserveAspect = true;
        }
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
