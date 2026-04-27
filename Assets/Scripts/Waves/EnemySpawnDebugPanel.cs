using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug enemy spawner panel.
///
/// Press <see cref="toggleKey"/> (default F2) during gameplay to open/close a
/// floating panel listing every available enemy type. Click an enemy button to
/// instantly spawn one at the active spawn point. Use the spawn-point dropdown
/// or the cycle button to change which spawn route is used.
///
/// Enemy list is gathered from (in priority order):
///   1. <see cref="MarathonMode.EnemyPool"/> if marathon is active
///   2. <see cref="EndlessMode.EnemyPool"/> if endless is active
///   3. Unique <c>EnemyData</c> referenced by <see cref="WaveSpawner.rounds"/>
///
/// Spawn calls go through <see cref="WaveSpawner.DebugSpawn"/>, which does not
/// affect round-end detection.
/// </summary>
public class EnemySpawnDebugPanel : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F2;

    private bool isOpen;
    private GameObject rootPanel;
    private Transform listParent;
    private Text spawnIndexLabel;
    private int activeSpawnIndex;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    void Toggle()
    {
        if (rootPanel == null) BuildPanel();
        isOpen = !isOpen;
        rootPanel.SetActive(isOpen);
        if (isOpen) RebuildList();
    }

    void BuildPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject c = new GameObject("EnemyDebugCanvas");
            canvas = c.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9001;
            c.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            c.AddComponent<GraphicRaycaster>();
        }

        rootPanel = new GameObject("EnemySpawnDebugPanel");
        rootPanel.transform.SetParent(canvas.transform, false);

        RectTransform rt = rootPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta = new Vector2(300f, 560f);
        Image bg = rootPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        // Header
        AddText(rootPanel.transform, "TITLE", "ENEMY / HERO DEBUG (F2)",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -8f), new Vector2(0f, 24f), 14, FontStyle.Bold);

        // Spawn-point row
        GameObject row = new GameObject("SpawnRow");
        row.transform.SetParent(rootPanel.transform, false);
        RectTransform rrt = row.AddComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 1f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.anchoredPosition = new Vector2(0f, -36f);
        rrt.sizeDelta = new Vector2(-16f, 28f);

        spawnIndexLabel = AddText(row.transform, "Label", "Spawn point: 0",
                                  new Vector2(0f, 0f), new Vector2(0.6f, 1f),
                                  Vector2.zero, Vector2.zero, 12, FontStyle.Normal);

        AddButton(row.transform, "Cycle", "Cycle ▶",
                  new Vector2(0.6f, 0f), new Vector2(1f, 1f),
                  Vector2.zero, Vector2.zero, CycleSpawnPoint);

        // Scroll area for enemy buttons
        GameObject scroll = new GameObject("ScrollArea");
        scroll.transform.SetParent(rootPanel.transform, false);
        RectTransform srt = scroll.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(8f, 36f);
        srt.offsetMax = new Vector2(-8f, -72f);

        VerticalLayoutGroup vlg = scroll.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childControlHeight     = true;
        vlg.childControlWidth      = true;

        ContentSizeFitter csf = scroll.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listParent = scroll.transform;

        // Refresh button
        GameObject refreshHost = new GameObject("RefreshRow");
        refreshHost.transform.SetParent(rootPanel.transform, false);
        RectTransform fr = refreshHost.AddComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 0f);
        fr.pivot = new Vector2(0.5f, 0f);
        fr.anchoredPosition = new Vector2(0f, 8f);
        fr.sizeDelta = new Vector2(-16f, 24f);

        AddButton(refreshHost.transform, "Refresh", "Refresh List",
                  new Vector2(0f, 0f), new Vector2(1f, 1f),
                  Vector2.zero, Vector2.zero, RebuildList);
    }

    void CycleSpawnPoint()
    {
        var ws = WaveSpawner.Instance;
        int total = ws != null ? ws.DebugSpawnPointCount : 1;
        if (total <= 0) total = 1;
        activeSpawnIndex = (activeSpawnIndex + 1) % total;
        if (spawnIndexLabel != null)
            spawnIndexLabel.text = $"Spawn point: {activeSpawnIndex}";
    }

    void RebuildList()
    {
        if (listParent == null) return;
        // Clear existing
        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        // ── Enemy section ──
        AddSectionHeader(listParent, "ENEMIES — click to spawn");
        var enemies = GatherEnemies();
        if (enemies.Count == 0)
        {
            AddText(listParent, "Empty", "(no enemies found)",
                    new Vector2(0f, 0f), new Vector2(1f, 1f),
                    Vector2.zero, Vector2.zero, 12, FontStyle.Italic);
        }
        else
        {
            foreach (var ed in enemies)
            {
                string label = string.IsNullOrEmpty(ed.enemyName) ? ed.name : ed.enemyName;
                EnemyData captured = ed;
                AddListButton(listParent, label, () => SpawnOne(captured),
                              new Color(0.18f, 0.45f, 0.18f, 0.95f));
            }
        }

        // ── Hero grant section ──
        AddSectionHeader(listParent, "HEROES — click to grant + unlock");
        var heroes = GatherGrantableHeroes();
        if (heroes.Count == 0)
        {
            AddText(listParent, "EmptyHeroes", "(no heroes found)",
                    new Vector2(0f, 0f), new Vector2(1f, 1f),
                    Vector2.zero, Vector2.zero, 12, FontStyle.Italic);
        }
        else
        {
            foreach (var td in heroes)
            {
                string label = string.IsNullOrEmpty(td.towerName) ? td.name : td.towerName;
                bool already = IsAlreadyAvailable(td);
                if (already) label = "[OWNED] " + label;
                TowerData captured = td;
                AddListButton(listParent, label, () => GrantHero(captured),
                              already ? new Color(0.30f, 0.30f, 0.30f, 0.95f)
                                      : new Color(0.40f, 0.28f, 0.10f, 0.95f));
            }
        }

        if (spawnIndexLabel != null)
            spawnIndexLabel.text = $"Spawn point: {activeSpawnIndex}";
    }

    static bool IsAlreadyAvailable(TowerData td)
    {
        var setup = FindFirstObjectByType<GameplayAutoSetup>();
        if (setup == null || setup.availableTowers == null) return false;
        foreach (var t in setup.availableTowers) if (t == td) return true;
        return false;
    }

    void GrantHero(TowerData td)
    {
        if (td == null) return;
        var setup = FindFirstObjectByType<GameplayAutoSetup>();
        if (setup == null) { Debug.LogWarning("[HeroGrant] No GameplayAutoSetup in scene."); return; }

        // Append to availableTowers if missing.
        var list = new List<TowerData>();
        if (setup.availableTowers != null) list.AddRange(setup.availableTowers);
        if (!list.Contains(td)) list.Add(td);
        setup.availableTowers = list.ToArray();

        // If this hero is gated by a FacultyData, force-unlock it for the run.
        if (GameManager.Instance != null && GameManager.Instance.allFaculties != null)
        {
            foreach (var f in GameManager.Instance.allFaculties)
            {
                if (f != null && f.professorTower == td)
                {
                    GameManager.Instance.DebugUnlockFaculty(f);
                    break;
                }
            }
        }

        setup.RebuildTowerShop();
        RebuildList();
        Debug.Log($"[HeroGrant] Granted hero '{td.towerName}'.");
    }

    static List<TowerData> GatherGrantableHeroes()
    {
        var seen = new HashSet<TowerData>();
        var list = new List<TowerData>();

        // Heroes already in the shop.
        var setup = FindFirstObjectByType<GameplayAutoSetup>();
        if (setup != null && setup.availableTowers != null)
        {
            foreach (var t in setup.availableTowers)
                if (t != null && t.isProfessorTower && seen.Add(t)) list.Add(t);
        }

        // Heroes registered as faculty professors (may be locked).
        if (GameManager.Instance != null && GameManager.Instance.allFaculties != null)
        {
            foreach (var f in GameManager.Instance.allFaculties)
            {
                if (f == null || f.professorTower == null) continue;
                if (seen.Add(f.professorTower)) list.Add(f.professorTower);
            }
        }

        return list;
    }

    static void AddSectionHeader(Transform parent, string text)
    {
        GameObject go = new GameObject("Hdr_" + text);
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 22; le.preferredHeight = 22;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.10f, 0.10f, 0.14f, 0.95f);

        GameObject txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        RectTransform trt = txt.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);
        Text t = txt.AddComponent<Text>();
        t.text = text;
        t.fontSize = 11;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(1f, 0.85f, 0.5f);
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void SpawnOne(EnemyData data)
    {
        var ws = WaveSpawner.Instance;
        if (ws == null) { Debug.LogWarning("[EnemyDebug] No WaveSpawner in scene."); return; }
        int total = ws.DebugSpawnPointCount;
        int idx = total > 0 ? Mathf.Clamp(activeSpawnIndex, 0, total - 1) : 0;
        ws.DebugSpawn(data, idx);
    }

    static List<EnemyData> GatherEnemies()
    {
        var seen = new HashSet<EnemyData>();
        var list = new List<EnemyData>();

        if (MarathonMode.IsActive && MarathonMode.EnemyPool != null)
        {
            foreach (var ed in MarathonMode.EnemyPool)
                if (ed != null && seen.Add(ed)) list.Add(ed);
        }

        if (EndlessMode.IsActive && EndlessMode.EnemyPool != null)
        {
            foreach (var ed in EndlessMode.EnemyPool)
                if (ed != null && seen.Add(ed)) list.Add(ed);
        }

        var ws = WaveSpawner.Instance;
        if (ws != null && ws.rounds != null)
        {
            foreach (var wave in ws.rounds)
            {
                if (wave == null || wave.enemyGroups == null) continue;
                foreach (var grp in wave.enemyGroups)
                    if (grp != null && grp.enemyType != null && seen.Add(grp.enemyType))
                        list.Add(grp.enemyType);
            }
        }

        return list;
    }

    // ── UI builders ──────────────────────────────────────────────────────────

    static Text AddText(Transform parent, string name, string content,
                        Vector2 anchorMin, Vector2 anchorMax,
                        Vector2 anchoredPos, Vector2 sizeDelta,
                        int fontSize, FontStyle style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;
        else { rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    static void AddButton(Transform parent, string name, string label,
                          Vector2 anchorMin, Vector2 anchorMax,
                          Vector2 anchoredPos, Vector2 sizeDelta,
                          UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;
        else { rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 0.95f);
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        RectTransform trt = txt.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        Text t = txt.AddComponent<Text>();
        t.text = label;
        t.fontSize = 12;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static void AddListButton(Transform parent, string label,
                              UnityEngine.Events.UnityAction onClick,
                              Color? color = null)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 26; le.preferredHeight = 26;
        Image img = go.AddComponent<Image>();
        img.color = color ?? new Color(0.18f, 0.45f, 0.18f, 0.95f);
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        RectTransform trt = txt.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);
        Text t = txt.AddComponent<Text>();
        t.text = label;
        t.fontSize = 12;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
