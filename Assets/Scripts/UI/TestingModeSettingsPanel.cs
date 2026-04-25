using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal settings panel shown when the user clicks "TESTING MODE" on the
/// main menu. Lets the dev tweak round count, starting gold/lives, archetype
/// inclusion, and pick an optional saved custom map. Confirm hands the
/// settings off to <see cref="TestingModeLauncher"/>.
/// </summary>
public class TestingModeSettingsPanel : MonoBehaviour
{
    GameObject _root;
    Slider _roundCountSlider;  Text _roundCountValue;
    Slider _goldSlider;        Text _goldValue;
    Slider _livesSlider;       Text _livesValue;
    Toggle _archetypesToggle;
    Toggle _professorToggle;
    Dropdown _customMapDropdown;
    string[] _mapNames = new string[0];

    public static void Open()
    {
        if (FindAnyObjectByType<TestingModeSettingsPanel>() != null) return;
        var go = new GameObject("--- TestingModeSettingsPanel ---");
        go.AddComponent<TestingModeSettingsPanel>();
    }

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        // Find the existing canvas in the scene (MainMenu created one in MainMenuSetup)
        Canvas canvas = FindAnyObjectByType<Canvas>();
        Transform parent;
        if (canvas == null)
        {
            _root = new GameObject("SettingsCanvas");
            var c = _root.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 1500;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            _root.AddComponent<GraphicRaycaster>();
            parent = _root.transform;
        }
        else
        {
            parent = canvas.transform;
        }

        // Dim background
        var dim = new GameObject("Dim");
        dim.transform.SetParent(parent, false);
        var dimRT = dim.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero; dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = dimRT.offsetMax = Vector2.zero;
        var dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.6f);
        dim.AddComponent<Button>(); // eats clicks behind the panel

        // Panel
        var panel = new GameObject("SettingsPanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(680, 720);
        rt.anchoredPosition = Vector2.zero;
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.13f, 0.18f, 0.97f);

        // Track the dim+panel together for cleanup
        if (_root == null) _root = panel; // close() destroys this; dim survives, fix below
        // Make Close also destroy dim
        _disposables = new GameObject[] { dim, panel };

        // Title
        MakeLabel(panel.transform, "Title", "Testing Mode — Round Settings", 28, FontStyle.Bold,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -50), new Vector2(640, 50));

        float y = -120;
        const float rowH = 70;

        _roundCountSlider = MakeSlider(panel.transform, "Number of rounds", 1, 5, 3, ref y, rowH, out _roundCountValue, true);
        _goldSlider       = MakeSlider(panel.transform, "Starting gold",   50, 1000, 200, ref y, rowH, out _goldValue, true);
        _livesSlider      = MakeSlider(panel.transform, "Starting lives",   1, 50, 20, ref y, rowH, out _livesValue, true);

        _archetypesToggle = MakeToggle(panel.transform, "Include archetype enemies", true, ref y, rowH);
        _professorToggle  = MakeToggle(panel.transform, "Include the Professor (boss)",  true, ref y, rowH);

        // Custom map dropdown
        _mapNames = CustomMapStore.ListMapNames();
        _customMapDropdown = MakeDropdown(panel.transform, "Custom map (optional)", ref y, rowH);

        // Buttons
        var startBtn = MakeButton(panel.transform, "StartBtn", "START", new Color(0.2f, 0.55f, 0.25f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(120, 60), new Vector2(220, 60));
        startBtn.onClick.AddListener(Confirm);

        var cancelBtn = MakeButton(panel.transform, "CancelBtn", "Cancel", new Color(0.4f, 0.4f, 0.4f),
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-120, 60), new Vector2(220, 60));
        cancelBtn.onClick.AddListener(Close);

        panel.transform.SetAsLastSibling();
    }

    GameObject[] _disposables;

    void Confirm()
    {
        var o = new TestingModeLauncher.Overrides
        {
            roundCount        = Mathf.RoundToInt(_roundCountSlider.value),
            startingGold      = Mathf.RoundToInt(_goldSlider.value),
            startingLives     = Mathf.RoundToInt(_livesSlider.value),
            includeArchetypes = _archetypesToggle.isOn,
            includeProfessor  = _professorToggle.isOn,
            customMapName     = (_customMapDropdown != null && _customMapDropdown.value > 0
                                   && _customMapDropdown.value - 1 < _mapNames.Length)
                                   ? _mapNames[_customMapDropdown.value - 1]
                                   : null
        };
        Close();
        TestingModeLauncher.Launch(o);
    }

    void Close()
    {
        if (_disposables != null)
            foreach (var go in _disposables) if (go != null) Destroy(go);
        Destroy(gameObject);
    }

    // ── UI helpers ──────────────────────────────────────────────────────────

    Text MakeLabel(Transform parent, string name, string content, int size, FontStyle style,
        Vector2 amin, Vector2 amax, Vector2 piv, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.fontStyle = style; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = content;
        return t;
    }

    Slider MakeSlider(Transform parent, string label, float min, float max, float def,
        ref float y, float rowH, out Text valueText, bool wholeNumbers)
    {
        // Label
        MakeLabel(parent, label + "_lbl", label, 18, FontStyle.Normal,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(40, y), new Vector2(300, 30));

        // Slider
        var sgo = new GameObject(label + "_slider");
        sgo.transform.SetParent(parent, false);
        var srt = sgo.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0, 1);
        srt.anchoredPosition = new Vector2(40, y - 30); srt.sizeDelta = new Vector2(450, 20);

        var bg = new GameObject("Background");
        bg.transform.SetParent(sgo.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sgo.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.25f); faRT.anchorMax = new Vector2(1, 0.75f);
        faRT.offsetMin = new Vector2(5, 0);     faRT.offsetMax = new Vector2(-15, 0);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f);

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sgo.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10, 0); haRT.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hRT = handle.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(20, 30);
        handle.AddComponent<Image>().color = Color.white;

        var slider = sgo.AddComponent<Slider>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect      = fillRT;
        slider.handleRect    = hRT;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = min;
        slider.maxValue      = max;
        slider.wholeNumbers  = wholeNumbers;
        slider.value         = def;

        // Value text
        valueText = MakeLabel(parent, label + "_val", def.ToString("0"), 18, FontStyle.Bold,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(510, y - 25), new Vector2(120, 30));
        var vt = valueText;
        slider.onValueChanged.AddListener(v => vt.text = v.ToString("0"));

        y -= rowH;
        return slider;
    }

    Toggle MakeToggle(Transform parent, string label, bool def, ref float y, float rowH)
    {
        var go = new GameObject(label + "_toggle");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(40, y); rt.sizeDelta = new Vector2(560, 40);

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(0, 0.5f); bgRT.pivot = new Vector2(0, 0.5f);
        bgRT.sizeDelta = new Vector2(28, 28); bgRT.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        var check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        var cRT = check.AddComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = new Vector2(4, 4); cRT.offsetMax = new Vector2(-4, -4);
        check.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f);

        MakeLabel(go.transform, "Label", label, 18, FontStyle.Normal,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
            new Vector2(40, 0), new Vector2(500, 30));

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bg.GetComponent<Image>();
        toggle.graphic       = check.GetComponent<Image>();
        toggle.isOn          = def;

        y -= rowH;
        return toggle;
    }

    Dropdown MakeDropdown(Transform parent, string label, ref float y, float rowH)
    {
        MakeLabel(parent, label + "_lbl", label, 18, FontStyle.Normal,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(40, y), new Vector2(400, 30));

        var go = new GameObject(label + "_dropdown");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(40, y - 30); rt.sizeDelta = new Vector2(560, 36);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var lRT = labelGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = new Vector2(10, 2); lRT.offsetMax = new Vector2(-25, -2);
        var lblText = labelGO.AddComponent<Text>();
        lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lblText.fontSize = 16; lblText.color = Color.white;
        lblText.alignment = TextAnchor.MiddleLeft;

        // Template (required by Dropdown component)
        var template = new GameObject("Template");
        template.transform.SetParent(go.transform, false);
        var tRT = template.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 0); tRT.anchorMax = new Vector2(1, 0); tRT.pivot = new Vector2(0.5f, 1);
        tRT.anchoredPosition = Vector2.zero; tRT.sizeDelta = new Vector2(0, 150);
        template.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
        template.AddComponent<ScrollRect>();

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(template.transform, false);
        var vRT = viewport.AddComponent<RectTransform>();
        vRT.anchorMin = Vector2.zero; vRT.anchorMax = Vector2.one;
        vRT.offsetMin = vRT.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRTd = content.AddComponent<RectTransform>();
        cRTd.anchorMin = new Vector2(0, 1); cRTd.anchorMax = new Vector2(1, 1); cRTd.pivot = new Vector2(0.5f, 1);
        cRTd.sizeDelta = new Vector2(0, 28);

        var item = new GameObject("Item");
        item.transform.SetParent(content.transform, false);
        var iRT = item.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0, 0.5f); iRT.anchorMax = new Vector2(1, 0.5f); iRT.pivot = new Vector2(0.5f, 0.5f);
        iRT.sizeDelta = new Vector2(0, 28);
        item.AddComponent<Toggle>();

        var itemBG = new GameObject("Item Background");
        itemBG.transform.SetParent(item.transform, false);
        var ibgRT = itemBG.AddComponent<RectTransform>();
        ibgRT.anchorMin = Vector2.zero; ibgRT.anchorMax = Vector2.one;
        ibgRT.offsetMin = ibgRT.offsetMax = Vector2.zero;
        itemBG.AddComponent<Image>().color = new Color(0.25f, 0.3f, 0.4f);

        var itemLabel = new GameObject("Item Label");
        itemLabel.transform.SetParent(item.transform, false);
        var ilRT = itemLabel.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero; ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(10, 1); ilRT.offsetMax = new Vector2(-10, -1);
        var itText = itemLabel.AddComponent<Text>();
        itText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itText.fontSize = 14; itText.color = Color.white; itText.alignment = TextAnchor.MiddleLeft;

        template.SetActive(false);

        var dd = go.AddComponent<Dropdown>();
        dd.targetGraphic = img;
        dd.captionText   = lblText;
        dd.template      = tRT;
        dd.itemText      = itText;
        var sr = template.GetComponent<ScrollRect>();
        sr.viewport      = vRT;
        sr.content       = cRTd;

        // Populate
        var opts = new System.Collections.Generic.List<Dropdown.OptionData>();
        opts.Add(new Dropdown.OptionData("(Default test path)"));
        foreach (var n in _mapNames) opts.Add(new Dropdown.OptionData(n));
        dd.options = opts;
        dd.value = 0;
        dd.RefreshShownValue();

        y -= rowH + 10;
        return dd;
    }

    Button MakeButton(Transform parent, string name, string label, Color color,
        Vector2 amin, Vector2 amax, Vector2 piv, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
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
        t.fontSize = 22; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        t.text = label;
        return btn;
    }
}
