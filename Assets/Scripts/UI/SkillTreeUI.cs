using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built full-screen Skill Tree overlay UI.
/// Call Build(canvasParent) once after the canvas is created (hidden by default).
/// Use Open() / Close() to show or hide it.
/// </summary>
public class SkillTreeUI : MonoBehaviour
{
    // ── Public API ────────────────────────────────────────────────────────────

    public void Build(Transform canvasParent)
    {
        _panel = MakePanel(canvasParent, "SkillTreeOverlay", new Color(0.04f, 0.07f, 0.14f, 0.97f));
        Stretch(_panel);
        _panel.SetActive(false);

        BuildHeader();
        BuildColumns();

        SkillPointManager.OnSkillPointsChanged += OnSkillPointsChanged;
        SkillTreeManager.OnNodeUnlocked        += OnNodeUnlocked;

        if (SkillPointManager.Instance != null)
            SetSPLabel(SkillPointManager.Instance.SkillPoints);
    }

    public void Open()
    {
        if (_panel == null) return;
        // The columns may not have been built yet (the SkillTreeManager / data
        // wasn't ready when Build() ran). Retry now.
        if (_cards.Count == 0) BuildColumns();
        _panel.SetActive(true);
        // Make sure we render on top of any sibling UI added after Build().
        _panel.transform.SetAsLastSibling();
        RefreshAll();
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // ── Private state ─────────────────────────────────────────────────────────

    private GameObject         _panel;
    private TextMeshProUGUI    _spLabel;

    private struct CardRefs
    {
        public Image           bg;
        public TextMeshProUGUI nameLabel;
        public Button          unlockBtn;
        public TextMeshProUGUI unlockBtnLabel;
    }

    private readonly Dictionary<string, CardRefs> _cards = new Dictionary<string, CardRefs>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        SkillPointManager.OnSkillPointsChanged -= OnSkillPointsChanged;
        SkillTreeManager.OnNodeUnlocked        -= OnNodeUnlocked;
    }

    void OnSkillPointsChanged(int sp)
    {
        SetSPLabel(sp);
        if (_panel != null && _panel.activeSelf)
            RefreshAll();
    }

    void OnNodeUnlocked(string _) => RefreshAll();

    // ── Header ────────────────────────────────────────────────────────────────

    void BuildHeader()
    {
        var hdr = MakePanel(_panel.transform, "Header", new Color(0.07f, 0.09f, 0.17f, 1f));
        var rt  = hdr.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.93f);
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Anchored(MakeTMP(hdr.transform, "Title", "★  Skill Tree  ★", 36, Color.white),
                 new Vector2(0.5f, 0.5f), new Vector2(440, 50));

        var spObj = MakeTMP(hdr.transform, "SP", "Skill Points: 0", 25, new Color(0.4f, 0.92f, 1f));
        Anchored(spObj, new Vector2(0.2f, 0.5f), new Vector2(290, 40));
        _spLabel = spObj.GetComponent<TextMeshProUGUI>();

        var closeObj = MakeButton(hdr.transform, "CloseBtn", "✕  Close", new Color(0.62f, 0.14f, 0.14f));
        Anchored(closeObj, new Vector2(0.91f, 0.5f), new Vector2(140, 44));
        closeObj.GetComponent<Button>().onClick.AddListener(Close);
    }

    // ── Columns ───────────────────────────────────────────────────────────────

    static readonly Color[] k_Accents =
    {
        new Color(0.18f, 0.62f, 0.34f),   // Social       – emerald
        new Color(0.18f, 0.42f, 0.78f),   // Internship   – blue
        new Color(0.82f, 0.48f, 0.12f),   // PartTimeWork – amber
        new Color(0.58f, 0.22f, 0.72f),   // Certs        – violet
    };

    static readonly string[] k_Labels =
        { "Social", "Internship", "Part-time Work", "Certifications" };

    void BuildColumns()
    {
        // Make sure a SkillTreeManager exists — if we were placed in a scene
        // (e.g. Overworld) where no bootstrap created one, spin one up now so
        // its placeholder tree is available.
        if (SkillTreeManager.Instance == null)
        {
            var stmGO = new GameObject("SkillTreeManager (auto)");
            stmGO.AddComponent<SkillTreeManager>();
        }

        var data = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.skillTreeData : null;
        if (data == null || data.nodes == null || data.nodes.Length == 0)
        {
            Debug.LogWarning("[SkillTreeUI] No SkillTreeData available; columns will be empty until Open() is called again with data.");
            return;
        }

        var sections = (SkillSection[])System.Enum.GetValues(typeof(SkillSection));
        float cw  = 1f / sections.Length;
        float gap = 0.004f;

        for (int i = 0; i < sections.Length; i++)
        {
            SkillSection sec    = sections[i];
            Color        accent = k_Accents[i];

            Color colBg = (i % 2 == 0)
                ? new Color(0.10f, 0.12f, 0.20f, 0.55f)
                : new Color(0.08f, 0.10f, 0.17f, 0.55f);

            var col = MakePanel(_panel.transform, "Col_" + sec, colBg);
            var cr  = col.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(i * cw + gap, 0f);
            cr.anchorMax = new Vector2((i + 1) * cw - gap, 0.92f);
            cr.offsetMin = cr.offsetMax = Vector2.zero;

            // Section header strip
            var strip = MakePanel(col.transform, "Strip", accent);
            var sr    = strip.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 0.94f);
            sr.anchorMax = Vector2.one;
            sr.offsetMin = sr.offsetMax = Vector2.zero;
            Anchored(MakeTMP(strip.transform, "StripLbl", k_Labels[i], 22, Color.white),
                     new Vector2(0.5f, 0.5f), new Vector2(220, 36));

            // Scroll view (below the strip)
            var scroll  = MakeScrollView(col.transform, new Vector2(0f, 0f), new Vector2(1f, 0.93f));
            var content = scroll.GetComponent<ScrollRect>().content;

            // Cards sorted by prerequisite depth (roots first)
            var nodes = data.nodes
                .Where(n => n.section == sec)
                .OrderBy(n => PrereqDepth(n, data.nodes))
                .ToArray();

            foreach (var node in nodes)
                AddCard(content, node, accent);
        }
    }

    // ── Node card ─────────────────────────────────────────────────────────────

    void AddCard(Transform parent, SkillNode node, Color accent)
    {
        var card = MakePanel(parent, "Card_" + node.nodeName, new Color(0.13f, 0.16f, 0.23f, 1f));
        card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(12, 12, 8, 8);
        vlg.spacing                = 4f;
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Name (bold)
        var nameTmp = AddTextRow(card.transform, node.nodeName, 18, Color.white, 26f, bold: true);

        // Description
        if (!string.IsNullOrEmpty(node.description))
        {
            var d = AddTextRow(card.transform, node.description, 13, new Color(0.76f, 0.76f, 0.76f), 0f);
            d.enableWordWrapping = true;
        }

        // Buff summary
        AddTextRow(card.transform, DescribeBuff(node.buff), 13, new Color(0.35f, 0.88f, 0.58f), 22f);

        // Prerequisites
        if (node.prerequisiteNodeNames != null && node.prerequisiteNodeNames.Length > 0)
            AddTextRow(card.transform,
                       "Req: " + string.Join(", ", node.prerequisiteNodeNames),
                       12, new Color(0.86f, 0.78f, 0.32f), 20f);

        // Cost
        AddTextRow(card.transform, "Cost: " + node.cost + " SP", 13, new Color(0.9f, 0.85f, 0.3f), 22f);

        AddSpacer(card.transform, 4f);

        // Unlock button
        var btnObj  = AddActionButton(card.transform, "Unlock  (" + node.cost + " SP)", accent);
        var btnComp = btnObj.GetComponent<Button>();
        string capturedName = node.nodeName;
        btnComp.onClick.AddListener(() => TryUnlock(capturedName));

        _cards[node.nodeName] = new CardRefs
        {
            bg             = card.GetComponent<Image>(),
            nameLabel      = nameTmp,
            unlockBtn      = btnComp,
            unlockBtnLabel = btnObj.GetComponentInChildren<TextMeshProUGUI>(),
        };
    }

    void TryUnlock(string nodeName)
    {
        if (SkillTreeManager.Instance != null)
            SkillTreeManager.Instance.UnlockNode(nodeName);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void SetSPLabel(int sp)
    {
        if (_spLabel != null)
            _spLabel.text = "Skill Points: " + sp;
    }

    void RefreshAll()
    {
        var stm = SkillTreeManager.Instance;
        if (stm == null || stm.skillTreeData == null) return;

        if (SkillPointManager.Instance != null)
            SetSPLabel(SkillPointManager.Instance.SkillPoints);

        foreach (var node in stm.skillTreeData.nodes)
        {
            if (!_cards.TryGetValue(node.nodeName, out var r)) continue;

            bool unlocked  = stm.IsNodeUnlocked(node.nodeName);
            bool canUnlock = stm.CanUnlockNode(node);

            if (r.bg != null)
                r.bg.color = unlocked
                    ? new Color(0.10f, 0.28f, 0.18f, 1f)   // green tint = unlocked
                    : new Color(0.13f, 0.16f, 0.23f, 1f);  // default

            if (r.nameLabel != null)
                r.nameLabel.color = unlocked ? new Color(0.38f, 0.98f, 0.60f) : Color.white;

            if (r.unlockBtn != null)
            {
                r.unlockBtn.interactable = canUnlock;
                if (r.unlockBtnLabel != null)
                    r.unlockBtnLabel.text = unlocked
                        ? "✓  Unlocked"
                        : "Unlock  (" + node.cost + " SP)";
            }
        }
    }

    // ── Layout row helpers ────────────────────────────────────────────────────

    static TextMeshProUGUI AddTextRow(Transform parent, string text, int size,
                                      Color color, float fixedHeight, bool bold = false)
    {
        var obj = new GameObject("Row");
        obj.transform.SetParent(parent, false);

        var le = obj.AddComponent<LayoutElement>();
        if (fixedHeight > 0f)
        {
            le.preferredHeight = fixedHeight;
            le.minHeight       = fixedHeight;
        }
        else
        {
            obj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Left;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    static void AddSpacer(Transform parent, float height)
    {
        var sp = new GameObject("Spacer");
        sp.transform.SetParent(parent, false);
        var le = sp.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = height;
    }

    static GameObject AddActionButton(Transform parent, string label, Color bg)
    {
        var btnObj = new GameObject("UnlockBtn");
        btnObj.transform.SetParent(parent, false);
        var le = btnObj.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 34f;

        var img = btnObj.AddComponent<Image>(); img.color = bg;
        var btn = btnObj.AddComponent<Button>(); btn.targetGraphic = img;

        ColorBlock cb    = btn.colors;
        cb.disabledColor = new Color(0.28f, 0.28f, 0.28f, 0.65f);
        btn.colors       = cb;

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(btnObj.transform, false);
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 14; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return btnObj;
    }

    // ── Scroll view builder ───────────────────────────────────────────────────

    static GameObject MakeScrollView(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var sv   = new GameObject("ScrollView");
        sv.transform.SetParent(parent, false);
        var svRT = sv.AddComponent<RectTransform>();
        svRT.anchorMin = anchorMin; svRT.anchorMax = anchorMax;
        svRT.offsetMin = svRT.offsetMax = Vector2.zero;

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false;

        // Viewport
        var vp   = new GameObject("Viewport");
        vp.transform.SetParent(sv.transform, false);
        var vpRT = vp.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        vp.AddComponent<Image>().color = Color.clear;
        vp.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var cnt  = new GameObject("Content");
        cnt.transform.SetParent(vp.transform, false);
        var cRT  = cnt.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot     = new Vector2(0.5f, 1f);
        cRT.sizeDelta = Vector2.zero;

        var vlg = cnt.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 8f;
        vlg.padding                = new RectOffset(6, 6, 8, 8);
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        cnt.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRT;
        sr.content  = cRT;

        return sv;
    }

    // ── Generic UI primitives ─────────────────────────────────────────────────

    static GameObject MakePanel(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        obj.AddComponent<Image>().color = color;
        return obj;
    }

    static GameObject MakeTMP(Transform parent, string name, string text, int size, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color bg)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<RectTransform>();
        var img = btnObj.AddComponent<Image>(); img.color = bg;
        var btn = btnObj.AddComponent<Button>(); btn.targetGraphic = img;

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(btnObj.transform, false);
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 22; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return btnObj;
    }

    static void Stretch(GameObject obj)
    {
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Anchored(GameObject obj, Vector2 anchor, Vector2 size)
    {
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    static int PrereqDepth(SkillNode node, SkillNode[] all)
    {
        if (node.prerequisiteNodeNames == null || node.prerequisiteNodeNames.Length == 0) return 0;
        int max = 0;
        foreach (string p in node.prerequisiteNodeNames)
        {
            SkillNode pNode = all.FirstOrDefault(n => n.nodeName == p);
            if (pNode != null) max = Mathf.Max(max, PrereqDepth(pNode, all) + 1);
        }
        return max;
    }

    static string DescribeBuff(BuffEffect b)
    {
        if (b == null) return "—";
        var parts = new List<string>();
        if (b.damageMultiplier   != 1f) parts.Add("+" + Mathf.RoundToInt((b.damageMultiplier   - 1f) * 100) + "% Damage");
        if (b.rangeMultiplier    != 1f) parts.Add("+" + Mathf.RoundToInt((b.rangeMultiplier    - 1f) * 100) + "% Range");
        if (b.fireRateMultiplier != 1f) parts.Add("+" + Mathf.RoundToInt((b.fireRateMultiplier - 1f) * 100) + "% Fire Rate");
        if (b.bonusStartGold > 0)      parts.Add("+" + b.bonusStartGold + " Gold");
        if (b.bonusLives > 0)          parts.Add("+" + b.bonusLives + " Lives");
        if (b.multiTarget)             parts.Add("Multi-target");
        return parts.Count > 0 ? string.Join("  ·  ", parts) : "—";
    }
}
