using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight in-game map debug helper:
/// - Toggle on/off with F8
/// - P toggles edit mode: Slot / Path
/// - Slot mode: left add slot, right remove nearest empty slot
/// - Path mode: left add waypoint (before exit), right remove nearest waypoint
/// - C: copy all current slot coordinates to clipboard
/// - V: copy all current path coordinates to clipboard
/// - G: toggle grid snapping
/// - Tab: cycle active spawn route in Path mode
///
/// While active, this tool force-shows path visuals and disables BuildAndUpgradeUI
/// click routing so placement clicks are not hijacked by gameplay menus.
/// </summary>
public class InGameSlotDebugEditor : MonoBehaviour
{
    enum EditMode { Slot, Path }

    public static InGameSlotDebugEditor Instance { get; private set; }

    [Header("Hotkeys")]
    public KeyCode toggleKey = KeyCode.F8;
    public KeyCode toggleModeKey = KeyCode.P;
    public KeyCode toggleConnectModeKey = KeyCode.L;
    public KeyCode cycleSpawnKey = KeyCode.Tab;
    public KeyCode copySlotsKey = KeyCode.C;
    public KeyCode copyPathsKey = KeyCode.V;
    public KeyCode toggleSnapKey = KeyCode.G;

    [Header("Edit Settings")]
    public float removeRadius = 0.65f;
    public float removePathPointRadius = 0.75f;
    public float duplicateRadius = 0.35f;
    public bool snapToGrid = true;
    public float gridSize = 0.5f;

    private bool isActive;
    private EditMode editMode = EditMode.Slot;
    private bool manualConnectMode;
    private int connectFromIndex = -1;
    private int insertAfterIndex = -1;
    private int activeSpawnIndex;
    private Camera mainCam;
    private PathManager pathManager;
    private readonly List<List<Vector3>> editableSpawnPaths = new List<List<Vector3>>();
    private GameObject debugPathRoot;

    // Overlay UI
    private GameObject rootPanel;
    private Text infoText;
    private string statusLine = "Ready";
    private float nextPathRefresh;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        mainCam = Camera.main;
        pathManager = FindFirstObjectByType<PathManager>();
        BuildOverlay();
        SetDebugMode(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetDebugMode(!isActive);

        if (!isActive) return;

        if (mainCam == null) mainCam = Camera.main;
        if (pathManager == null) pathManager = FindFirstObjectByType<PathManager>();

        if (Input.GetKeyDown(toggleModeKey))
            ToggleEditMode();

        if (editMode == EditMode.Path && Input.GetKeyDown(cycleSpawnKey))
            CycleActiveSpawn();

        if (editMode == EditMode.Path && Input.GetKeyDown(toggleConnectModeKey))
            ToggleManualConnectMode();

        if (Time.unscaledTime >= nextPathRefresh)
        {
            nextPathRefresh = Time.unscaledTime + 0.25f;
            ForceShowPathVisuals();
        }

        if (Input.GetKeyDown(copySlotsKey))
            CopySlotsToClipboard();

        if (Input.GetKeyDown(copyPathsKey))
            CopyPathsToClipboard();

        if (Input.GetKeyDown(toggleSnapKey))
        {
            snapToGrid = !snapToGrid;
            statusLine = snapToGrid ? "Grid snap ON" : "Grid snap OFF";
        }

        Vector3 world = GetMouseWorld();
        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (!pointerOverUi)
        {
            if (editMode == EditMode.Slot)
            {
                if (Input.GetMouseButtonDown(0))
                    AddSlotAt(world);
                else if (Input.GetMouseButtonDown(1))
                    RemoveNearestSlot(world);
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                    HandlePathLeftClick(world);
                else if (Input.GetMouseButtonDown(1))
                    RemovePathPoint(world);
            }
        }

        RefreshOverlay(world, pointerOverUi);
    }

    void SetDebugMode(bool on)
    {
        isActive = on;

        if (rootPanel != null)
            rootPanel.SetActive(on);

        ToggleBuildMenus(!on);
        if (on)
        {
            TowerPlacement.Instance?.CancelPlacement();
            ForceShowPathVisuals();
            if (editMode == EditMode.Path)
            {
                EnsureEditablePathsLoaded();
                ApplyEditedPathsToRuntime();
                SetDebugPathVisualsVisible(true);
            }
            statusLine = "Debug mode ON";
        }
        else
        {
            SetDebugPathVisualsVisible(false);
            statusLine = "Debug mode OFF";
        }
    }

    void ToggleEditMode()
    {
        editMode = editMode == EditMode.Slot ? EditMode.Path : EditMode.Slot;
        connectFromIndex = -1;
        insertAfterIndex = -1;
        manualConnectMode = false;
        if (editMode == EditMode.Path)
        {
            EnsureEditablePathsLoaded();
            ApplyEditedPathsToRuntime();
            SetDebugPathVisualsVisible(true);
            statusLine = $"Mode: Path (spawn {activeSpawnIndex + 1})";
        }
        else
        {
            SetDebugPathVisualsVisible(false);
            statusLine = "Mode: Slot";
        }
    }

    void CycleActiveSpawn()
    {
        if (!EnsureEditablePathsLoaded()) return;
        if (editableSpawnPaths.Count == 0) return;

        activeSpawnIndex = (activeSpawnIndex + 1) % editableSpawnPaths.Count;
        connectFromIndex = -1;
        insertAfterIndex = -1;
        ApplyEditedPathsToRuntime();
        statusLine = $"Editing spawn route {activeSpawnIndex + 1}/{editableSpawnPaths.Count}";
    }

    void ToggleManualConnectMode()
    {
        manualConnectMode = !manualConnectMode;
        connectFromIndex = -1;
        if (manualConnectMode)
            statusLine = "Path connect ON: click FROM waypoint, then click TO waypoint.";
        else
            statusLine = "Path connect OFF: click waypoint to set insert anchor, then click empty space to add point.";
    }

    void ToggleBuildMenus(bool enabled)
    {
        BuildAndUpgradeUI[] menus = FindObjectsByType<BuildAndUpgradeUI>(FindObjectsSortMode.None);
        foreach (BuildAndUpgradeUI ui in menus)
            ui.enabled = enabled;
    }

    void BuildOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject c = new GameObject("SlotDebugCanvas");
            canvas = c.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;
            c.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            c.AddComponent<GraphicRaycaster>();
        }

        rootPanel = new GameObject("SlotDebugPanel");
        rootPanel.transform.SetParent(canvas.transform, false);

        RectTransform rt = rootPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(18f, -18f);
        rt.sizeDelta = new Vector2(640f, 250f);

        Image bg = rootPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject txt = new GameObject("Info");
        txt.transform.SetParent(rootPanel.transform, false);
        RectTransform tr = txt.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 10f);
        tr.offsetMax = new Vector2(-12f, -10f);

        infoText = txt.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        infoText.fontSize = 18;
        infoText.alignment = TextAnchor.UpperLeft;
        infoText.color = Color.white;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void RefreshOverlay(Vector3 world, bool pointerOverUi)
    {
        if (infoText == null) return;

        int slotCount = GetAllSlots().Count;
        string worldText = FormatVec(world);
        string block = pointerOverUi ? "YES" : "NO";
        string snapText = snapToGrid ? $"ON ({gridSize:0.##})" : "OFF";
        string modeText = editMode == EditMode.Slot ? "SLOT" : "PATH";
        string spawnText = editableSpawnPaths.Count > 0
            ? $"{Mathf.Clamp(activeSpawnIndex + 1, 1, editableSpawnPaths.Count)}/{editableSpawnPaths.Count}"
            : "n/a";
        string connectText = manualConnectMode ? "ON" : "OFF";

        infoText.text =
            "MAP DEBUG (F8 to close)\n" +
            "P: toggle mode (SLOT/PATH)   |   Tab: next spawn route (PATH mode)\n" +
            "L: toggle path connect mode (PATH only)\n" +
            "SLOT: Left add slot, Right remove slot   |   C copy slot coords\n" +
            "PATH (connect OFF): click waypoint to set anchor, click empty to add after it\n" +
            "PATH (connect ON): click FROM waypoint then TO waypoint to reconnect order\n" +
            "PATH: Right remove waypoint   |   V copy path coords\n" +
            "G: toggle grid snap\n" +
            $"Mouse world: {worldText}   UI blocked: {block}\n" +
            $"Mode: {modeText}   Active route: {spawnText}   Connect: {connectText}   Grid: {snapText}   Slots: {slotCount}\n" +
            $"Status: {statusLine}";
    }

    Vector3 GetMouseWorld()
    {
        if (mainCam == null) return Vector3.zero;

        Vector3 w = mainCam.ScreenToWorldPoint(Input.mousePosition);
        w.z = 0f;

        if (!snapToGrid) return w;

        float g = Mathf.Max(0.01f, gridSize);
        w.x = Mathf.Round(w.x / g) * g;
        w.y = Mathf.Round(w.y / g) * g;
        return w;
    }

    bool EnsureEditablePathsLoaded()
    {
        if (pathManager == null || pathManager.currentWaypoints == null)
        {
            statusLine = "Path data not found.";
            return false;
        }

        if (editableSpawnPaths.Count > 0) return true;

        Waypoints wp = pathManager.currentWaypoints;
        int spawnCount = Mathf.Max(1, wp.SpawnPointCount);
        for (int s = 0; s < spawnCount; s++)
        {
            Transform[] chain = wp.GetPathFor(s);
            var points = new List<Vector3>();
            if (chain != null)
            {
                foreach (Transform t in chain)
                {
                    if (t != null) points.Add(t.position);
                }
            }

            if (points.Count < 2)
            {
                Vector3 start = wp.GetSpawnPosition(Mathf.Clamp(s, 0, spawnCount - 1));
                Vector3 end = wp.exitPoint != null ? wp.exitPoint.position : start + Vector3.right * 6f;
                points.Clear();
                points.Add(start);
                points.Add(end);
            }

            editableSpawnPaths.Add(points);
        }

        activeSpawnIndex = Mathf.Clamp(activeSpawnIndex, 0, editableSpawnPaths.Count - 1);
        return true;
    }

    void HandlePathLeftClick(Vector3 pos)
    {
        if (!EnsureEditablePathsLoaded()) return;

        List<Vector3> path = editableSpawnPaths[activeSpawnIndex];
        int nearest = FindNearestWaypointIndex(path, pos, removePathPointRadius, includeEndpoints: true);

        if (manualConnectMode)
        {
            if (nearest < 0)
            {
                statusLine = "Connect mode: click on a waypoint.";
                return;
            }

            HandleConnectPick(path, nearest);
            ApplyEditedPathsToRuntime();
            return;
        }

        // Non-connect path edit mode:
        // - click existing waypoint to set insertion anchor
        // - click empty world to add a new point after anchor
        if (nearest >= 0)
        {
            insertAfterIndex = nearest;
            statusLine = $"Insert anchor set to WP{nearest}. Click empty space to add next point.";
            return;
        }

        AddPathPoint(pos);
    }

    void HandleConnectPick(List<Vector3> path, int pickedIndex)
    {
        if (connectFromIndex < 0)
        {
            if (pickedIndex >= path.Count - 1)
            {
                statusLine = "Cannot start connection from route end.";
                return;
            }
            connectFromIndex = pickedIndex;
            statusLine = $"Connect FROM WP{connectFromIndex}. Now click TO waypoint.";
            return;
        }

        if (pickedIndex == connectFromIndex)
        {
            statusLine = "Pick a different TO waypoint.";
            return;
        }

        if (pickedIndex == 0 || pickedIndex == path.Count - 1)
        {
            statusLine = "Start/end points are fixed. Choose an internal TO waypoint.";
            connectFromIndex = -1;
            return;
        }

        ConnectWaypointIndices(path, connectFromIndex, pickedIndex);
        statusLine = $"Connected WP{connectFromIndex} -> WP{pickedIndex}.";
        connectFromIndex = -1;
    }

    void ConnectWaypointIndices(List<Vector3> path, int from, int to)
    {
        if (path == null || path.Count < 3) return;
        if (from < 0 || from >= path.Count) return;
        if (to <= 0 || to >= path.Count - 1) return;
        if (from == to) return;

        Vector3 node = path[to];
        path.RemoveAt(to);
        if (to < from) from--;

        int insertIndex = Mathf.Clamp(from + 1, 1, Mathf.Max(1, path.Count - 1));
        path.Insert(insertIndex, node);

        // Continue adding from the latest connected node.
        insertAfterIndex = insertIndex;
    }

    int FindNearestWaypointIndex(List<Vector3> path, Vector3 pos, float radius, bool includeEndpoints)
    {
        if (path == null || path.Count == 0) return -1;

        int start = includeEndpoints ? 0 : 1;
        int endInclusive = includeEndpoints ? path.Count - 1 : path.Count - 2;
        if (start > endInclusive) return -1;

        int nearest = -1;
        float best = radius;
        for (int i = start; i <= endInclusive; i++)
        {
            float d = Vector2.Distance(path[i], pos);
            if (d <= best)
            {
                best = d;
                nearest = i;
            }
        }
        return nearest;
    }

    void AddPathPoint(Vector3 pos)
    {
        if (!EnsureEditablePathsLoaded()) return;

        List<Vector3> path = editableSpawnPaths[activeSpawnIndex];
        foreach (Vector3 p in path)
        {
            if (Vector2.Distance(p, pos) <= duplicateRadius)
            {
                statusLine = "Waypoint too close to existing point.";
                return;
            }
        }

        // Insert right after the selected anchor if present; otherwise before route end.
        int insertIndex;
        if (insertAfterIndex >= 0)
        {
            int anchor = Mathf.Clamp(insertAfterIndex, 0, Mathf.Max(0, path.Count - 2));
            insertIndex = anchor + 1;
        }
        else
        {
            insertIndex = Mathf.Max(1, path.Count - 1);
        }

        path.Insert(insertIndex, pos);
        insertAfterIndex = insertIndex;

        ApplyEditedPathsToRuntime();
        statusLine = $"Added waypoint {ToVector3Literal(pos)} on route {activeSpawnIndex + 1} after WP{Mathf.Max(0, insertIndex - 1)}";
        Debug.Log($"[MapDebug] Added waypoint at {ToVector3Literal(pos)} on route {activeSpawnIndex + 1}");
    }

    void RemovePathPoint(Vector3 pos)
    {
        if (!EnsureEditablePathsLoaded()) return;

        List<Vector3> path = editableSpawnPaths[activeSpawnIndex];
        if (path.Count <= 2)
        {
            statusLine = "Route has only start/end; nothing to remove.";
            return;
        }

        int bestIndex = -1;
        float bestDist = removePathPointRadius;
        for (int i = 1; i < path.Count - 1; i++)
        {
            float d = Vector2.Distance(path[i], pos);
            if (d <= bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            statusLine = "No waypoint near cursor to remove.";
            return;
        }

        Vector3 removed = path[bestIndex];
        path.RemoveAt(bestIndex);
        if (insertAfterIndex >= path.Count)
            insertAfterIndex = path.Count - 1;
        if (connectFromIndex == bestIndex)
            connectFromIndex = -1;

        ApplyEditedPathsToRuntime();
        statusLine = $"Removed waypoint {ToVector3Literal(removed)} on route {activeSpawnIndex + 1}";
        Debug.Log($"[MapDebug] Removed waypoint at {ToVector3Literal(removed)} on route {activeSpawnIndex + 1}");
    }

    void ApplyEditedPathsToRuntime()
    {
        if (pathManager == null || pathManager.currentWaypoints == null) return;
        if (editableSpawnPaths.Count == 0) return;

        Waypoints wp = pathManager.currentWaypoints;
        if (debugPathRoot != null) Destroy(debugPathRoot);
        debugPathRoot = new GameObject("DebugEditedPaths");

        wp.perSpawnPaths = new Transform[editableSpawnPaths.Count][];

        for (int s = 0; s < editableSpawnPaths.Count; s++)
        {
            List<Vector3> path = editableSpawnPaths[s];
            Transform[] chain = new Transform[path.Count];

            for (int i = 0; i < path.Count; i++)
            {
                GameObject p = new GameObject($"DbgSpawn{s}_WP{i}");
                p.transform.SetParent(debugPathRoot.transform);
                p.transform.position = path[i];
                p.transform.localScale = Vector3.one * ((i == 0 || i == path.Count - 1) ? 0.36f : 0.28f);

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = RuntimeSprite.Circle;
                sr.sortingOrder = 8;

                if (i == 0) sr.color = new Color(1f, 0.3f, 0.3f, 1f);                    // route start
                else if (i == path.Count - 1) sr.color = new Color(0.35f, 1f, 0.35f, 1f); // route end
                else sr.color = s == activeSpawnIndex
                    ? new Color(1f, 0.95f, 0.35f, 0.95f)
                    : new Color(0.8f, 0.8f, 0.8f, 0.8f);

                chain[i] = p.transform;
            }

            wp.perSpawnPaths[s] = chain;

            // Keep the legacy single-chain route synced to the first spawn.
            if (s == 0) wp.points = chain;

            CreatePathLineVisual(debugPathRoot.transform, chain, s == activeSpawnIndex);
        }

        SetDebugPathVisualsVisible(isActive && editMode == EditMode.Path);
    }

    void CreatePathLineVisual(Transform parent, Transform[] chain, bool selected)
    {
        if (chain == null || chain.Length < 2) return;

        GameObject lineGo = new GameObject(selected ? "DebugPathLineActive" : "DebugPathLine");
        lineGo.transform.SetParent(parent);

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = true;
        lr.positionCount = chain.Length;
        lr.startWidth = lr.endWidth = selected ? 0.22f : 0.14f;
        lr.startColor = lr.endColor = selected
            ? new Color(1f, 0.8f, 0.25f, 0.95f)
            : new Color(0.75f, 0.75f, 0.75f, 0.7f);
        lr.sortingOrder = 7;

        for (int i = 0; i < chain.Length; i++)
        {
            Vector3 p = chain[i] != null ? chain[i].position : Vector3.zero;
            lr.SetPosition(i, p);
        }
    }

    void SetDebugPathVisualsVisible(bool visible)
    {
        if (debugPathRoot == null) return;

        SpriteRenderer[] sprites = debugPathRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in sprites) sr.enabled = visible;

        LineRenderer[] lines = debugPathRoot.GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lr in lines) lr.enabled = visible;
    }

    void AddSlotAt(Vector3 pos)
    {
        if (pathManager == null)
        {
            statusLine = "PathManager not found.";
            return;
        }

        TowerSlot near = FindNearestSlot(pos, duplicateRadius, includeOccupied: true);
        if (near != null)
        {
            statusLine = "Slot already exists nearby.";
            return;
        }

        GameObject slotsParent = GameObject.Find("TowerSlots");
        if (slotsParent == null) slotsParent = new GameObject("TowerSlots");

        GameObject slotObj = new GameObject($"Slot{slotsParent.transform.childCount}");
        slotObj.transform.position = pos;
        slotObj.transform.SetParent(slotsParent.transform);

        TowerSlot slot = slotObj.AddComponent<TowerSlot>();

        SpriteRenderer sr = slotObj.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.WhiteSquare;
        sr.color = new Color(0.35f, 0.95f, 1f, 0.92f);
        sr.sortingOrder = 4;
        slotObj.transform.localScale = Vector3.one * 0.7f;

        BoxCollider2D col = slotObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;

        AddSlotToPathManager(slot);

        string literal = ToVector3Literal(pos);
        statusLine = $"Added {literal}";
        Debug.Log($"[MapDebug] Added slot at {literal}");
    }

    void RemoveNearestSlot(Vector3 pos)
    {
        TowerSlot slot = FindNearestSlot(pos, removeRadius, includeOccupied: false);
        if (slot == null)
        {
            statusLine = "No removable empty slot nearby.";
            return;
        }

        RemoveSlotFromPathManager(slot);
        string literal = ToVector3Literal(slot.transform.position);
        Destroy(slot.gameObject);

        statusLine = $"Removed {literal}";
        Debug.Log($"[MapDebug] Removed slot at {literal}");
    }

    void AddSlotToPathManager(TowerSlot slot)
    {
        if (pathManager == null || slot == null) return;

        TowerSlot[] oldArr = pathManager.currentTowerSlots ?? new TowerSlot[0];
        TowerSlot[] next = new TowerSlot[oldArr.Length + 1];
        for (int i = 0; i < oldArr.Length; i++) next[i] = oldArr[i];
        next[oldArr.Length] = slot;
        pathManager.currentTowerSlots = next;
    }

    void RemoveSlotFromPathManager(TowerSlot target)
    {
        if (pathManager == null || target == null) return;

        var keep = new List<TowerSlot>();
        TowerSlot[] arr = pathManager.currentTowerSlots ?? new TowerSlot[0];
        foreach (TowerSlot slot in arr)
        {
            if (slot == null) continue;
            if (slot == target) continue;
            keep.Add(slot);
        }
        pathManager.currentTowerSlots = keep.ToArray();
    }

    TowerSlot FindNearestSlot(Vector3 pos, float radius, bool includeOccupied)
    {
        TowerSlot nearest = null;
        float best = radius;

        List<TowerSlot> slots = GetAllSlots();
        foreach (TowerSlot slot in slots)
        {
            if (slot == null) continue;
            if (!includeOccupied && slot.isOccupied) continue;

            float d = Vector2.Distance(pos, slot.transform.position);
            if (d <= best)
            {
                best = d;
                nearest = slot;
            }
        }

        return nearest;
    }

    List<TowerSlot> GetAllSlots()
    {
        var list = new List<TowerSlot>();

        if (pathManager != null && pathManager.currentTowerSlots != null)
        {
            foreach (TowerSlot slot in pathManager.currentTowerSlots)
            {
                if (slot != null) list.Add(slot);
            }
            if (list.Count > 0) return list;
        }

        TowerSlot[] found = FindObjectsByType<TowerSlot>(FindObjectsSortMode.None);
        foreach (TowerSlot slot in found)
            if (slot != null) list.Add(slot);

        return list;
    }

    void CopySlotsToClipboard()
    {
        List<TowerSlot> slots = GetAllSlots();
        if (slots.Count == 0)
        {
            statusLine = "No slots to copy.";
            return;
        }

        slots.Sort((a, b) =>
        {
            int byX = a.transform.position.x.CompareTo(b.transform.position.x);
            if (byX != 0) return byX;
            return a.transform.position.y.CompareTo(b.transform.position.y);
        });

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("new Vector3[]");
        sb.AppendLine("{");
        foreach (TowerSlot slot in slots)
            sb.AppendLine("    " + ToVector3Literal(slot.transform.position) + ",");
        sb.Append("};");

        GUIUtility.systemCopyBuffer = sb.ToString();
        statusLine = $"Copied {slots.Count} slot coords to clipboard.";
        Debug.Log("[MapDebug] Copied slot coordinates to clipboard.\n" + sb);
    }

    void CopyPathsToClipboard()
    {
        if (!EnsureEditablePathsLoaded()) return;
        if (editableSpawnPaths.Count == 0)
        {
            statusLine = "No paths to copy.";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("new PathPatternData.SpawnWaypoints[]");
        sb.AppendLine("{");
        for (int s = 0; s < editableSpawnPaths.Count; s++)
        {
            sb.AppendLine("    new PathPatternData.SpawnWaypoints");
            sb.AppendLine("    {");
            sb.AppendLine("        positions = new Vector3[]");
            sb.AppendLine("        {");
            foreach (Vector3 p in editableSpawnPaths[s])
                sb.AppendLine("            " + ToVector3Literal(p) + ",");
            sb.AppendLine("        }");
            sb.AppendLine("    },");
        }
        sb.AppendLine("};");

        GUIUtility.systemCopyBuffer = sb.ToString();
        statusLine = $"Copied {editableSpawnPaths.Count} path route(s) to clipboard.";
        Debug.Log("[MapDebug] Copied path coordinates to clipboard.\n" + sb);
    }

    void ForceShowPathVisuals()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;
            string n = sr.gameObject.name;

            bool isPathVisual =
                n.StartsWith("PathLine") ||
                n.StartsWith("Waypoint") ||
                n.StartsWith("SpawnPoint") ||
                n.StartsWith("Spawn") && n.Contains("_WP") ||
                n == "Exit";

            if (!isPathVisual) continue;

            sr.enabled = true;
            if (n.StartsWith("PathLine"))
                sr.color = new Color(1f, 0.85f, 0.45f, 1f);
        }
    }

    static string FormatVec(Vector3 v)
    {
        return $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
    }

    static string ToVector3Literal(Vector3 v)
    {
        string x = v.x.ToString("0.###", CultureInfo.InvariantCulture);
        string y = v.y.ToString("0.###", CultureInfo.InvariantCulture);
        return $"new Vector3({x}f, {y}f, 0f)";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (isActive) ToggleBuildMenus(true);
    }
}
