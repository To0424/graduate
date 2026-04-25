using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained, runtime-built map creator screen. Devs/players reach it
/// from the Main Menu's "MAP CREATOR" button. The whole UI + camera + input
/// handlers are constructed in code so no scene/prefab work is required.
///
/// Tools (radio buttons in the side panel):
///   - Spawn        : click in the world to drop a red spawn marker. The new
///                    spawn becomes the "active" spawn — subsequent waypoints
///                    you place attach to its path.
///   - Path         : click to append a waypoint to the active spawn's path.
///   - Home / Exit  : click to set the shared exit position (green marker).
///   - Tower Slot   : click to drop a buildable slot (blue square).
///   - Erase        : click any marker to delete it (and any waypoints that
///                    belong to a deleted spawn).
///
/// Each spawn owns its own polyline (drawn with LineRenderer, plus a final
/// dashed segment to the exit so devs can see where enemies will go).
///
/// Hit Save → opens a name prompt and writes JSON via <see cref="CustomMapStore"/>.
/// </summary>
public class MapCreator : MonoBehaviour
{
    enum Tool { Spawn, Path, Exit, TowerSlot, Erase }

    class SpawnEntry
    {
        public GameObject     marker;
        public List<Vector3>  waypoints = new List<Vector3>();
        public List<GameObject> wpMarkers = new List<GameObject>();
        public LineRenderer   line;
    }

    Tool _tool = Tool.Spawn;
    readonly List<SpawnEntry> _spawns      = new List<SpawnEntry>();
    SpawnEntry                _activeSpawn;
    GameObject                _exitMarker;
    Vector3                   _exitPos;
    bool                      _exitSet;
    readonly List<GameObject> _towerSlots  = new List<GameObject>();
    readonly List<Vector3>    _towerSlotPositions = new List<Vector3>();

    GameObject _root;
    Camera     _cam;
    Text       _statusText;
    GameObject _saveDialog;
    InputField _saveNameInput;

    /// <summary>Canvases we hid when launching so we can restore them on close.</summary>
    readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    /// <summary>If we changed the main camera's clear settings, snapshot to restore.</summary>
    Color _restoreCamColor;
    CameraClearFlags _restoreCamFlags;
    bool  _camSnapshotted;

    public static void LaunchOverlay()
    {
        var go = new GameObject("--- MapCreator ---");
        go.AddComponent<MapCreator>();
    }

    void Start()
    {
        // Hide every other canvas in the scene so the Main Menu UI doesn't sit
        // on top of (or behind) our editor. We restore them on Close().
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c == null || !c.gameObject.activeSelf) continue;
            // Skip our own canvas (we haven't built it yet, but be defensive).
            if (c.gameObject.name == "MapCreatorCanvas") continue;
            c.gameObject.SetActive(false);
            _hiddenCanvases.Add(c);
        }

        // Make sure there is a camera + EventSystem
        _cam = Camera.main;
        if (_cam == null)
        {
            var camGO = new GameObject("MapCreatorCamera");
            _cam = camGO.AddComponent<Camera>();
            _cam.orthographic     = true;
            _cam.orthographicSize = 6;
            _cam.backgroundColor  = new Color(0.08f, 0.10f, 0.13f);
            _cam.clearFlags       = CameraClearFlags.SolidColor;
            camGO.tag = "MainCamera";
        }
        else
        {
            // Snapshot the camera's clear settings so we can restore them if
            // the user closes the map creator and goes back to the main menu.
            _camSnapshotted   = true;
            _restoreCamColor  = _cam.backgroundColor;
            _restoreCamFlags  = _cam.clearFlags;
            _cam.clearFlags       = CameraClearFlags.SolidColor;
            _cam.backgroundColor  = new Color(0.08f, 0.10f, 0.13f);
            // Pull camera back to a sensible default for the editor view.
            var camPos = _cam.transform.position;
            _cam.transform.position = new Vector3(0, 0, camPos.z != 0 ? camPos.z : -10f);
        }
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        BuildUI();
        SetTool(Tool.Spawn);
    }

    void Update()
    {
        // Left-click in world (not over UI) → run current tool.
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0;
            HandleClick(world);
        }
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    bool IsPointerOverUI()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    // ── UI construction ─────────────────────────────────────────────────────

    void BuildUI()
    {
        _root = new GameObject("MapCreatorCanvas");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        // Side panel
        var side = MakePanel(_root.transform, "SidePanel", new Color(0.12f, 0.14f, 0.20f, 0.95f));
        var sideRT = side.GetComponent<RectTransform>();
        sideRT.anchorMin = new Vector2(0, 0);
        sideRT.anchorMax = new Vector2(0, 1);
        sideRT.pivot     = new Vector2(0, 0.5f);
        sideRT.sizeDelta = new Vector2(280, 0);
        sideRT.anchoredPosition = Vector2.zero;

        MakeLabel(side.transform, "Title", "MAP CREATOR", 26, FontStyle.Bold,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -50), new Vector2(0, 60));

        MakeLabel(side.transform, "Instr",
            "Tools — pick one then click on the map. ESC to close.",
            14, FontStyle.Normal,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -90), new Vector2(-20, 50));

        // Tool buttons
        float y = -160;
        AddToolButton(side.transform, "Spawn (red)",   Tool.Spawn,    ref y);
        AddToolButton(side.transform, "Path waypoint", Tool.Path,     ref y);
        AddToolButton(side.transform, "Home / Exit",   Tool.Exit,     ref y);
        AddToolButton(side.transform, "Tower slot",    Tool.TowerSlot,ref y);
        AddToolButton(side.transform, "Erase",         Tool.Erase,    ref y);

        y -= 30;
        AddSimpleButton(side.transform, "Clear all", new Color(0.6f, 0.2f, 0.2f), ref y, ClearAll);
        AddSimpleButton(side.transform, "Save",      new Color(0.2f, 0.5f, 0.2f), ref y, OpenSaveDialog);
        AddSimpleButton(side.transform, "Close",     new Color(0.3f, 0.3f, 0.3f), ref y, Close);

        // Status bar
        _statusText = MakeLabel(side.transform, "Status", "",
            14, FontStyle.Italic,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 30), new Vector2(-20, 80));
        _statusText.color = new Color(0.9f, 0.9f, 0.6f);
        _statusText.alignment = TextAnchor.LowerCenter;

        BuildSaveDialog();
    }

    void BuildSaveDialog()
    {
        _saveDialog = MakePanel(_root.transform, "SaveDialog", new Color(0.05f, 0.07f, 0.10f, 0.95f));
        var rt = _saveDialog.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520, 240);
        rt.anchoredPosition = Vector2.zero;

        MakeLabel(_saveDialog.transform, "Title", "Save map as…", 22, FontStyle.Bold,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -40), new Vector2(0, 40));

        var inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(_saveDialog.transform, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = inputRT.anchorMax = new Vector2(0.5f, 0.5f);
        inputRT.sizeDelta = new Vector2(420, 50);
        inputRT.anchoredPosition = new Vector2(0, 10);
        var bg = inputGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.18f, 0.22f);
        _saveNameInput = inputGO.AddComponent<InputField>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 5); textRT.offsetMax = new Vector2(-10, -5);
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        _saveNameInput.textComponent = text;

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(inputGO.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 5); phRT.offsetMax = new Vector2(-10, -5);
        var ph = phGO.AddComponent<Text>();
        ph.font = text.font; ph.fontSize = 18;
        ph.color = new Color(0.6f, 0.6f, 0.6f);
        ph.text = "MyAwesomeMap";
        ph.alignment = TextAnchor.MiddleLeft;
        _saveNameInput.placeholder = ph;

        // Buttons
        var okBtn = MakeButton(_saveDialog.transform, "OkBtn", "Save",
            new Color(0.2f, 0.5f, 0.2f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(80, -70), new Vector2(140, 50));
        okBtn.onClick.AddListener(DoSave);

        var cancelBtn = MakeButton(_saveDialog.transform, "CancelBtn", "Cancel",
            new Color(0.4f, 0.4f, 0.4f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-80, -70), new Vector2(140, 50));
        cancelBtn.onClick.AddListener(() => _saveDialog.SetActive(false));

        _saveDialog.SetActive(false);
    }

    void OpenSaveDialog()
    {
        if (_spawns.Count == 0 || !_exitSet)
        {
            SetStatus("Need at least 1 spawn and a Home before saving.");
            return;
        }
        _saveNameInput.text = "";
        _saveDialog.SetActive(true);
        _saveDialog.transform.SetAsLastSibling();
    }

    void DoSave()
    {
        string name = _saveNameInput.text.Trim();
        if (string.IsNullOrEmpty(name)) { SetStatus("Enter a name."); return; }

        var data = new CustomMapStore.Data
        {
            mapName        = name,
            difficultyTier = 1,
            exitPosition   = _exitPos,
        };
        foreach (var s in _spawns)
        {
            data.spawns.Add(new CustomMapStore.SerializableSpawn
            {
                spawn     = s.marker.transform.position,
                waypoints = s.waypoints.ToArray(),
            });
        }
        data.towerSlots.AddRange(_towerSlotPositions);

        CustomMapStore.Save(data);
        SetStatus($"Saved '{name}'.");
        _saveDialog.SetActive(false);
    }

    void Close()
    {
        Destroy(_root);
        // Tear down all world markers we created
        foreach (var s in _spawns)
        {
            if (s.marker != null) Destroy(s.marker);
            if (s.line != null && s.line.gameObject != null) Destroy(s.line.gameObject);
            foreach (var w in s.wpMarkers) if (w != null) Destroy(w);
        }
        if (_exitMarker != null) Destroy(_exitMarker);
        foreach (var t in _towerSlots) if (t != null) Destroy(t);

        // Restore the canvases we hid on launch.
        foreach (var c in _hiddenCanvases) if (c != null) c.gameObject.SetActive(true);
        _hiddenCanvases.Clear();

        // Restore camera clear settings if we changed them.
        if (_camSnapshotted && _cam != null)
        {
            _cam.backgroundColor = _restoreCamColor;
            _cam.clearFlags      = _restoreCamFlags;
        }

        Destroy(gameObject);
    }

    // ── World tool actions ──────────────────────────────────────────────────

    void HandleClick(Vector3 world)
    {
        switch (_tool)
        {
            case Tool.Spawn:     CreateSpawn(world); break;
            case Tool.Path:      AddWaypoint(world); break;
            case Tool.Exit:      SetExit(world); break;
            case Tool.TowerSlot: AddTowerSlot(world); break;
            case Tool.Erase:     EraseAt(world); break;
        }
        RefreshAllLines();
    }

    void CreateSpawn(Vector3 pos)
    {
        var s = new SpawnEntry { marker = MakeWorldMarker("Spawn", pos, Color.red, 0.5f) };
        var lineGO = new GameObject("SpawnLine");
        lineGO.transform.position = Vector3.zero;
        s.line = lineGO.AddComponent<LineRenderer>();
        s.line.material = new Material(Shader.Find("Sprites/Default"));
        s.line.startColor = s.line.endColor = new Color(1f, 0.7f, 0.3f, 0.85f);
        s.line.startWidth = s.line.endWidth = 0.12f;
        s.line.useWorldSpace = true;
        _spawns.Add(s);
        _activeSpawn = s;
        SetStatus($"New spawn placed. Now use 'Path waypoint' to draw its route. ({_spawns.Count} spawn(s))");
    }

    void AddWaypoint(Vector3 pos)
    {
        if (_activeSpawn == null)
        {
            SetStatus("Place a Spawn first, then add waypoints.");
            return;
        }
        _activeSpawn.waypoints.Add(pos);
        _activeSpawn.wpMarkers.Add(MakeWorldMarker("WP", pos, new Color(1f, 0.85f, 0.4f), 0.25f));
    }

    void SetExit(Vector3 pos)
    {
        if (_exitMarker != null) Destroy(_exitMarker);
        _exitMarker = MakeWorldMarker("Exit", pos, Color.green, 0.5f);
        _exitPos = pos;
        _exitSet = true;
        SetStatus("Home / Exit set.");
    }

    void AddTowerSlot(Vector3 pos)
    {
        _towerSlotPositions.Add(pos);
        _towerSlots.Add(MakeWorldMarker("Slot", pos, new Color(0.3f, 0.5f, 0.9f, 0.7f), 0.6f, square: true));
    }

    void EraseAt(Vector3 pos)
    {
        const float radius = 0.4f;
        // Try spawns
        for (int i = _spawns.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(_spawns[i].marker.transform.position, pos) < radius)
            {
                Destroy(_spawns[i].marker);
                if (_spawns[i].line != null) Destroy(_spawns[i].line.gameObject);
                foreach (var w in _spawns[i].wpMarkers) if (w != null) Destroy(w);
                if (_activeSpawn == _spawns[i]) _activeSpawn = null;
                _spawns.RemoveAt(i);
                return;
            }
            // waypoints belonging to this spawn
            for (int j = _spawns[i].waypoints.Count - 1; j >= 0; j--)
            {
                if (Vector3.Distance(_spawns[i].waypoints[j], pos) < radius)
                {
                    Destroy(_spawns[i].wpMarkers[j]);
                    _spawns[i].wpMarkers.RemoveAt(j);
                    _spawns[i].waypoints.RemoveAt(j);
                    return;
                }
            }
        }
        // Tower slots
        for (int i = _towerSlots.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(_towerSlotPositions[i], pos) < radius)
            {
                Destroy(_towerSlots[i]);
                _towerSlots.RemoveAt(i);
                _towerSlotPositions.RemoveAt(i);
                return;
            }
        }
        // Exit
        if (_exitMarker != null && Vector3.Distance(_exitPos, pos) < radius)
        {
            Destroy(_exitMarker);
            _exitMarker = null;
            _exitSet    = false;
        }
    }

    void RefreshAllLines()
    {
        foreach (var s in _spawns)
        {
            int extra = _exitSet ? 1 : 0;
            s.line.positionCount = 1 + s.waypoints.Count + extra;
            s.line.SetPosition(0, s.marker.transform.position);
            for (int i = 0; i < s.waypoints.Count; i++)
                s.line.SetPosition(1 + i, s.waypoints[i]);
            if (_exitSet)
                s.line.SetPosition(1 + s.waypoints.Count, _exitPos);
        }
    }

    void ClearAll()
    {
        foreach (var s in _spawns)
        {
            if (s.marker != null) Destroy(s.marker);
            if (s.line != null) Destroy(s.line.gameObject);
            foreach (var w in s.wpMarkers) if (w != null) Destroy(w);
        }
        _spawns.Clear();
        _activeSpawn = null;
        if (_exitMarker != null) Destroy(_exitMarker);
        _exitMarker = null; _exitSet = false;
        foreach (var t in _towerSlots) if (t != null) Destroy(t);
        _towerSlots.Clear();
        _towerSlotPositions.Clear();
        SetStatus("Cleared.");
    }

    GameObject MakeWorldMarker(string name, Vector3 pos, Color c, float scale, bool square = false)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square ? RuntimeSprite.WhiteSquare : RuntimeSprite.Circle;
        sr.color  = c;
        sr.sortingOrder = 5;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    // ── UI helpers ──────────────────────────────────────────────────────────

    GameObject MakePanel(Transform parent, string name, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = c;
        return go;
    }

    Text MakeLabel(Transform parent, string name, string content, int size, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.fontStyle = style; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.text = content;
        return t;
    }

    Button MakeButton(Transform parent, string name, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var t = lblGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        t.text = label;
        return btn;
    }

    void AddToolButton(Transform parent, string label, Tool tool, ref float y)
    {
        var btn = MakeButton(parent, "Tool_" + tool, label, new Color(0.25f, 0.30f, 0.40f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(-30, 44));
        btn.onClick.AddListener(() => SetTool(tool));
        y -= 52;
    }

    void AddSimpleButton(Transform parent, string label, Color color, ref float y, System.Action onClick)
    {
        var btn = MakeButton(parent, "Btn_" + label, label, color,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(-30, 44));
        btn.onClick.AddListener(() => onClick());
        y -= 52;
    }

    void SetTool(Tool tool)
    {
        _tool = tool;
        SetStatus($"Tool: {tool}");
    }

    void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
    }
}
