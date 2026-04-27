using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// In-game build & upgrade interface. One MonoBehaviour creates and manages:
///   • a click router that distinguishes empty slots from built towers
///   • a radial build menu (opens on empty-slot click)
///   • a tower upgrade panel (opens on built-tower click)
///
/// Clicking a different slot/tower while a menu is open switches to that
/// target's menu. Right-click, Escape, or clicking empty space closes the
/// active menu. Time slows automatically while either menu is open.
/// </summary>
public class BuildAndUpgradeUI : MonoBehaviour
{
    [Header("Configuration (set by GameplayAutoSetup)")]
    public TowerData[] availableOptions;
    public Tower towerPrefab;

    private Camera _cam;
    private Canvas _canvas;
    private GameObject _radialRoot;
    private GameObject _upgradeRoot;
    private GameObject _tooltipRoot;
    private TowerSlot _activeSlot;
    private Tower _activeTower;
    private bool _menuOpen;
    private GameObject _rangeRing;

    private TextMeshProUGUI _upgradeTitle;
    private TextMeshProUGUI _upgradeStats;
    private Button   _path1Btn,   _path2Btn,   _sellBtn;
    private TextMeshProUGUI _path1Lbl, _path2Lbl;
    private Button   _cap1Btn,    _cap2Btn;
    private TextMeshProUGUI _cap1Lbl, _cap2Lbl;
    private TextMeshProUGUI _tooltipTitle;
    private TextMeshProUGUI _tooltipBody;
    private readonly List<TowerData> _radialOptions = new List<TowerData>();
    private readonly List<Image> _radialSectorImages = new List<Image>();

    private const float PaletteDiameter = 310f;
    private const float PaletteRadius = PaletteDiameter * 0.5f;
    private const float PaletteCancelRadius = 44f;

    public void Build(Canvas canvas, TowerData[] options, Tower prefab)
    {
        _canvas           = canvas;
        availableOptions  = options;
        towerPrefab       = prefab;
        _cam              = Camera.main;
        BuildRadialRoot();
        BuildTooltipRoot();
        BuildUpgradeRoot();
        TowerPlacement.OnTowerPlaced += OnTowerPlaced;
    }

    void OnDestroy()
    {
        TowerPlacement.OnTowerPlaced -= OnTowerPlaced;
        if (_menuOpen) InteractionTimeScale.End();
    }

    void OnTowerPlaced() { CloseMenus(); }

    void Update()
    {
        if (HandleRadialPaletteInput()) return;

        if (_tooltipRoot != null && _tooltipRoot.activeSelf)
            MoveTooltip(Input.mousePosition);

        // Universal close inputs
        if (_menuOpen && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CloseMenus();
            return;
        }

        // Don't capture clicks while the placement / targeting system is active
        if (TowerPlacement.Instance != null && TowerPlacement.Instance.isPlacing) return;
        if (SkillTargetingController.Instance != null && SkillTargetingController.Instance.IsTargeting) return;

        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Resolve what was clicked in the world
        Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0;
        Collider2D hit = Physics2D.OverlapPoint(world);

        TowerSlot slot  = hit != null ? hit.GetComponent<TowerSlot>() : null;
        Tower     tower = hit != null ? hit.GetComponent<Tower>()     : null;
        if (tower == null && slot != null && slot.currentTower != null) tower = slot.currentTower;

        // Re-route: clicking on a different valid target switches menus seamlessly.
        if (tower != null)
        {
            if (tower != _activeTower) OpenUpgradeMenu(tower);
        }
        else if (slot != null && !slot.isOccupied)
        {
            if (slot != _activeSlot) OpenRadialMenu(slot);
        }
        else if (_menuOpen)
        {
            // Clicked empty world space → close.
            CloseMenus();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Radial build menu
    // ────────────────────────────────────────────────────────────────────────

    void BuildRadialRoot()
    {
        _radialRoot = new GameObject("RadialBuildMenu");
        _radialRoot.transform.SetParent(_canvas.transform, false);
        var rt = _radialRoot.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(540, 540);
        _radialRoot.SetActive(false);
    }

    void OpenRadialMenu(TowerSlot slot)
    {
        // If the upgrade panel was open for a previous selection, drop it.
        if (_activeTower != null) HideUpgradePanel();
        if (_activeSlot != null && _activeSlot != slot) _activeSlot.Highlight(false);

        _activeSlot  = slot;
        _activeTower = null;
        BuildRadialButtons(slot);

        Vector3 screen = _cam.WorldToScreenPoint(slot.transform.position);
        _radialRoot.GetComponent<RectTransform>().position = screen;
        _radialRoot.SetActive(true);

        slot.Highlight(true);
        SetMenuOpen(true);
    }

    void BuildRadialButtons(TowerSlot slot)
    {
        for (int i = _radialRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(_radialRoot.transform.GetChild(i).gameObject);
        _radialOptions.Clear();
        _radialSectorImages.Clear();

        // Filter: regular towers only (no heroes), and skip deployed uniques.
        var visible = new System.Collections.Generic.List<TowerData>();
        if (availableOptions != null)
        {
            foreach (TowerData td in availableOptions)
            {
                if (td == null) continue;
                if (td.isProfessorTower) continue;
                if (td.unique && DeployedUniqueRegistry.IsDeployed(td)) continue;
                visible.Add(td);
            }
        }

        if (visible.Count == 0)
        {
            GameObject none = MakeText(_radialRoot.transform, "NoOptions", "No regular towers", 20, new Color(0.90f, 0.93f, 0.98f));
            RectTransform nrt = none.GetComponent<RectTransform>();
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(0f, 64f);
            nrt.sizeDelta = new Vector2(260f, 56f);

            GameObject cancelOnly = MakeButton(_radialRoot.transform, "Cancel", "X", new Color(0.28f, 0.28f, 0.33f, 1f));
            RectTransform cot = cancelOnly.GetComponent<RectTransform>();
            cot.anchorMin = cot.anchorMax = new Vector2(0.5f, 0.5f);
            cot.anchoredPosition = Vector2.zero;
            cot.sizeDelta = new Vector2(68f, 68f);
            TextMeshProUGUI cOnlyLabel = cancelOnly.GetComponentInChildren<TextMeshProUGUI>();
            if (cOnlyLabel != null)
            {
                cOnlyLabel.fontSize = 24;
                cOnlyLabel.fontStyle = FontStyles.Bold;
            }
            cancelOnly.GetComponent<Button>().onClick.AddListener(CloseMenus);
            return;
        }

        const float iconRadius = 100f;
        const float iconSize = 58f;

        // Slightly transparent large background circle.
        GameObject backdrop = new GameObject("PaletteBackdrop");
        backdrop.transform.SetParent(_radialRoot.transform, false);
        RectTransform backRT = backdrop.AddComponent<RectTransform>();
        backRT.anchorMin = backRT.anchorMax = new Vector2(0.5f, 0.5f);
        backRT.anchoredPosition = Vector2.zero;
        backRT.sizeDelta = new Vector2(PaletteDiameter, PaletteDiameter);
        Image backImg = backdrop.AddComponent<Image>();
        backImg.sprite = RuntimeSprite.Circle != null ? RuntimeSprite.Circle : RuntimeSprite.WhiteSquare;
        backImg.color = new Color(0.08f, 0.12f, 0.18f, 0.55f);
        backImg.raycastTarget = false;

        int sectorCount = visible.Count;
        for (int i = 0; i < sectorCount; i++)
        {
            _radialOptions.Add(visible[i]);
            Image sectorImage = AddRadialBuildOption(visible[i], i, sectorCount, iconRadius, iconSize);
            _radialSectorImages.Add(sectorImage);
        }

        SetSectorHighlight(-1);

        // Center cancel button
        GameObject cancel = MakeButton(_radialRoot.transform, "Cancel", "X", new Color(0.30f, 0.30f, 0.35f, 1f));
        RectTransform crt = cancel.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(72f, 72f);
        TextMeshProUGUI cLbl = cancel.GetComponentInChildren<TextMeshProUGUI>();
        if (cLbl != null)
        {
            cLbl.fontSize = 26;
            cLbl.fontStyle = FontStyles.Bold;
        }
        cancel.GetComponent<Button>().onClick.AddListener(CloseMenus);
    }

    Image AddRadialBuildOption(TowerData td, int index, int total, float iconRadius, float iconSize)
    {
        bool affordable = CurrencyManager.Instance != null &&
                          CurrencyManager.Instance.CanAfford(td.cost);

        Color baseCol = Tower.GetTowerColor(td.towerType) * 0.8f;
        baseCol.a = affordable ? 0.36f : 0.24f;

        GameObject btn = new GameObject($"R_{td.towerName}_{index}");
        btn.transform.SetParent(_radialRoot.transform, false);
        RectTransform brt = btn.AddComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(PaletteDiameter, PaletteDiameter);
        float wedgeRotation = -360f * index / Mathf.Max(1f, total);
        brt.localEulerAngles = new Vector3(0f, 0f, wedgeRotation);

        Image bg = btn.AddComponent<Image>();
        bg.sprite = RuntimeSprite.Circle != null ? RuntimeSprite.Circle : RuntimeSprite.WhiteSquare;
        bg.type = Image.Type.Filled;
        bg.fillMethod = Image.FillMethod.Radial360;
        bg.fillAmount = 1f / Mathf.Max(1, total);
        bg.fillOrigin = (int)Image.Origin360.Top;
        bg.color = baseCol;
        bg.raycastTarget = false;

        // Place icon at the center of the local wedge; parent rotation moves it to the proper sector.
        float sectorAngle = (Mathf.PI * 2f) / Mathf.Max(1f, total);
        float localCenter = sectorAngle * 0.5f; // clockwise from top
        Vector2 iconPos = new Vector2(Mathf.Sin(localCenter) * iconRadius, Mathf.Cos(localCenter) * iconRadius);

        // Icon inside each equal sector.
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(btn.transform, false);
        RectTransform iconRT = iconObj.AddComponent<RectTransform>();
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = iconPos;
        iconRT.sizeDelta = new Vector2(iconSize, iconSize);
        iconRT.localEulerAngles = new Vector3(0f, 0f, -wedgeRotation);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = td.sprite != null ? td.sprite : RuntimeSprite.WhiteSquare;
        icon.preserveAspect = true;
        icon.color = affordable ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.95f);
        icon.raycastTarget = false;

        // Small cost text under icon.
        GameObject costObj = MakeText(btn.transform, "Cost", $"{td.cost}g", 13, new Color(0.96f, 0.98f, 1f, affordable ? 1f : 0.75f));
        RectTransform costRT = costObj.GetComponent<RectTransform>();
        costRT.anchorMin = costRT.anchorMax = new Vector2(0.5f, 0.5f);
        costRT.anchoredPosition = iconPos + new Vector2(0f, -36f);
        costRT.sizeDelta = new Vector2(70f, 22f);
        costRT.localEulerAngles = new Vector3(0f, 0f, -wedgeRotation);
        TextMeshProUGUI costTmp = costObj.GetComponent<TextMeshProUGUI>();
        if (costTmp != null)
        {
            costTmp.fontStyle = FontStyles.Bold;
            costTmp.alignment = TextAlignmentOptions.Center;
        }
        GameObject costBg = new GameObject("CostBG");
        costBg.transform.SetParent(costObj.transform, false);
        costBg.transform.SetSiblingIndex(0);
        RectTransform costBgRT = costBg.AddComponent<RectTransform>();
        costBgRT.anchorMin = Vector2.zero;
        costBgRT.anchorMax = Vector2.one;
        costBgRT.offsetMin = Vector2.zero;
        costBgRT.offsetMax = Vector2.zero;
        Image costBgImg = costBg.AddComponent<Image>();
        costBgImg.color = new Color(0f, 0f, 0f, 0.35f);
        costBgImg.raycastTarget = false;

        Debug.Log($"Tower icon test: {td.towerName} sprite={(td.sprite != null ? td.sprite.name : "NULL")}");

        return bg;
    }

    bool HandleRadialPaletteInput()
    {
        if (_radialRoot == null || !_radialRoot.activeSelf) return false;
        if (_activeSlot == null || _radialOptions.Count == 0) return false;

        RectTransform radialRT = _radialRoot.GetComponent<RectTransform>();
        if (radialRT == null) return false;

        Camera eventCam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(radialRT, Input.mousePosition, eventCam, out local))
            return false;

        float dist = local.magnitude;
        int hovered = -1;
        if (dist >= PaletteCancelRadius && dist <= PaletteRadius)
            hovered = GetSectorIndex(local, _radialOptions.Count);

        SetSectorHighlight(hovered);

        if (hovered >= 0 && hovered < _radialOptions.Count)
            ShowTooltip(_radialOptions[hovered], Input.mousePosition);
        else
            HideTooltip();

        if (!Input.GetMouseButtonDown(0)) return false;

        if (hovered >= 0 && hovered < _radialOptions.Count)
        {
            TowerData chosen = _radialOptions[hovered];
            if (TowerPlacement.Instance != null)
            {
                TowerPlacement.Instance.SetTowerPrefab(towerPrefab);
                TowerPlacement.Instance.PlaceAtSlot(chosen, _activeSlot);
            }
            CloseMenus();
            return true;
        }

        if (dist <= PaletteCancelRadius || dist > PaletteRadius)
        {
            CloseMenus();
            return true;
        }

        return true;
    }

    int GetSectorIndex(Vector2 localPoint, int sectorCount)
    {
        if (sectorCount <= 0) return -1;
        // Angle measured clockwise from top to match wedge construction.
        float angle = Mathf.Atan2(localPoint.x, localPoint.y);
        if (angle < 0f) angle += Mathf.PI * 2f;
        int idx = Mathf.FloorToInt((angle / (Mathf.PI * 2f)) * sectorCount);
        return Mathf.Clamp(idx, 0, sectorCount - 1);
    }

    void SetSectorHighlight(int hovered)
    {
        int n = Mathf.Min(_radialOptions.Count, _radialSectorImages.Count);
        for (int i = 0; i < n; i++)
        {
            Image sector = _radialSectorImages[i];
            if (sector == null) continue;

            bool affordable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(_radialOptions[i].cost);
            Color c = Tower.GetTowerColor(_radialOptions[i].towerType) * 0.8f;
            if (i == hovered) c.a = affordable ? 0.62f : 0.46f;
            else c.a = affordable ? 0.36f : 0.24f;
            sector.color = c;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Upgrade panel
    // ────────────────────────────────────────────────────────────────────────

    void BuildUpgradeRoot()
    {
        _upgradeRoot = MakePanel(_canvas.transform, "TowerUpgradePanel",
                                 new Color(0.04f, 0.06f, 0.12f, 0.95f));
        var rt = _upgradeRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.13f, 0.10f);
        rt.anchorMax = new Vector2(0.46f, 0.55f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _upgradeRoot.SetActive(false);

        var titleObj = MakeText(_upgradeRoot.transform, "Title", "Tower", 26, Color.white);
        var titleRT  = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.88f); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;
        _upgradeTitle = titleObj.GetComponent<TextMeshProUGUI>();

        var statsObj = MakeText(_upgradeRoot.transform, "Stats", "", 14, new Color(0.8f, 0.85f, 0.95f));
        var statsRT  = statsObj.GetComponent<RectTransform>();
        statsRT.anchorMin = new Vector2(0.05f, 0.70f); statsRT.anchorMax = new Vector2(0.95f, 0.88f);
        statsRT.offsetMin = statsRT.offsetMax = Vector2.zero;
        _upgradeStats = statsObj.GetComponent<TextMeshProUGUI>();
        _upgradeStats.alignment = TextAlignmentOptions.TopLeft;

        // Path 1 button
        GameObject p1 = MakeButton(_upgradeRoot.transform, "Path1", "—", new Color(0.18f, 0.42f, 0.78f));
        var p1RT = p1.GetComponent<RectTransform>();
        p1RT.anchorMin = new Vector2(0.05f, 0.48f); p1RT.anchorMax = new Vector2(0.48f, 0.68f);
        p1RT.offsetMin = p1RT.offsetMax = Vector2.zero;
        _path1Btn = p1.GetComponent<Button>();
        _path1Lbl = p1.GetComponentInChildren<TextMeshProUGUI>();
        _path1Btn.onClick.AddListener(() => BuyUpgrade(1));

        // Path 2 button
        GameObject p2 = MakeButton(_upgradeRoot.transform, "Path2", "—", new Color(0.78f, 0.42f, 0.18f));
        var p2RT = p2.GetComponent<RectTransform>();
        p2RT.anchorMin = new Vector2(0.52f, 0.48f); p2RT.anchorMax = new Vector2(0.95f, 0.68f);
        p2RT.offsetMin = p2RT.offsetMax = Vector2.zero;
        _path2Btn = p2.GetComponent<Button>();
        _path2Lbl = p2.GetComponentInChildren<TextMeshProUGUI>();
        _path2Btn.onClick.AddListener(() => BuyUpgrade(2));

        // Capstone 1 button (purple/blue — path 1 finisher)
        GameObject c1 = MakeButton(_upgradeRoot.transform, "Cap1", "—", new Color(0.32f, 0.18f, 0.55f));
        var c1RT = c1.GetComponent<RectTransform>();
        c1RT.anchorMin = new Vector2(0.05f, 0.27f); c1RT.anchorMax = new Vector2(0.48f, 0.46f);
        c1RT.offsetMin = c1RT.offsetMax = Vector2.zero;
        _cap1Btn = c1.GetComponent<Button>();
        _cap1Lbl = c1.GetComponentInChildren<TextMeshProUGUI>();
        _cap1Btn.onClick.AddListener(() => BuyCapstone(1));

        // Capstone 2 button (gold/red — path 2 finisher)
        GameObject c2 = MakeButton(_upgradeRoot.transform, "Cap2", "—", new Color(0.55f, 0.32f, 0.10f));
        var c2RT = c2.GetComponent<RectTransform>();
        c2RT.anchorMin = new Vector2(0.52f, 0.27f); c2RT.anchorMax = new Vector2(0.95f, 0.46f);
        c2RT.offsetMin = c2RT.offsetMax = Vector2.zero;
        _cap2Btn = c2.GetComponent<Button>();
        _cap2Lbl = c2.GetComponentInChildren<TextMeshProUGUI>();
        _cap2Btn.onClick.AddListener(() => BuyCapstone(2));

        // Sell button
        GameObject sell = MakeButton(_upgradeRoot.transform, "Sell", "Sell", new Color(0.55f, 0.18f, 0.18f));
        var sellRT = sell.GetComponent<RectTransform>();
        sellRT.anchorMin = new Vector2(0.05f, 0.05f); sellRT.anchorMax = new Vector2(0.48f, 0.23f);
        sellRT.offsetMin = sellRT.offsetMax = Vector2.zero;
        _sellBtn = sell.GetComponent<Button>();
        _sellBtn.onClick.AddListener(SellTower);

        // Close button
        GameObject close = MakeButton(_upgradeRoot.transform, "Close", "Close", new Color(0.3f, 0.3f, 0.35f));
        var clRT = close.GetComponent<RectTransform>();
        clRT.anchorMin = new Vector2(0.52f, 0.05f); clRT.anchorMax = new Vector2(0.95f, 0.23f);
        clRT.offsetMin = clRT.offsetMax = Vector2.zero;
        close.GetComponent<Button>().onClick.AddListener(CloseMenus);
    }

    void OpenUpgradeMenu(Tower tower)
    {
        // Drop any active radial first
        if (_activeSlot != null) { _activeSlot.Highlight(false); _activeSlot = null; }
        HideRadial();
        DestroyRangeRing();

        _activeTower = tower;
        _upgradeRoot.SetActive(true);
        BuildRangeRing(tower);
        RefreshUpgradePanel();
        SetMenuOpen(true);
    }

    void RefreshUpgradePanel()
    {
        if (_activeTower == null || _activeTower.data == null) { CloseMenus(); return; }
        TowerData d = _activeTower.data;

        int totalTier = _activeTower.path1Tier + _activeTower.path2Tier;
        string capStatus = _activeTower.capstoneTier == 0
            ? $"Capstone: {totalTier}/{Mathf.Max(1, d.capstoneRequiredTotalTiers)}"
            : $"Capstone: {(_activeTower.capstoneTier == 1 ? d.capstonePath1?.upgradeName : d.capstonePath2?.upgradeName)}";

        _upgradeTitle.text = $"{d.towerName}";
        _upgradeStats.text =
            $"Damage: {_activeTower.currentDamage}   Range: {_activeTower.currentRange:F1}   Rate: {_activeTower.currentFireRate:F2}/s\n" +
            $"Path 1: {_activeTower.path1Tier}/{(d.path1Upgrades?.Length ?? 0)}    " +
            $"Path 2: {_activeTower.path2Tier}/{(d.path2Upgrades?.Length ?? 0)}\n" +
            capStatus;

        UpdatePathButton(1, d.path1Upgrades, _activeTower.path1Tier, _path1Btn, _path1Lbl);
        UpdatePathButton(2, d.path2Upgrades, _activeTower.path2Tier, _path2Btn, _path2Lbl);
        UpdateCapstoneButton(1, d.capstonePath1, _cap1Btn, _cap1Lbl);
        UpdateCapstoneButton(2, d.capstonePath2, _cap2Btn, _cap2Lbl);
    }

    void UpdateCapstoneButton(int idx, TowerUpgrade cap, Button btn, TextMeshProUGUI label)
    {
        if (cap == null)
        {
            label.text = "—";
            btn.interactable = false;
            return;
        }
        if (_activeTower.capstoneTier != 0)
        {
            // A capstone has been chosen — lock both buttons.
            label.text = (_activeTower.capstoneTier == idx)
                ? $"★ {cap.upgradeName}\n(active)"
                : $"{cap.upgradeName}\n(other path chosen)";
            btn.interactable = false;
            return;
        }
        if (!_activeTower.CanBuyCapstone())
        {
            label.text = $"{cap.upgradeName}\n(needs {_activeTower.data.capstoneRequiredTotalTiers} upgrades)";
            btn.interactable = false;
            return;
        }
        label.text = $"★ {cap.upgradeName}\n{cap.description}\n<b>{cap.cost}g</b>";
        btn.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(cap.cost);
    }

    void BuyCapstone(int pathIndex)
    {
        if (_activeTower == null) return;
        if (_activeTower.TryBuyCapstone(pathIndex))
        {
            BuildRangeRing(_activeTower);
            RefreshUpgradePanel();
        }
    }

    void UpdatePathButton(int idx, TowerUpgrade[] path, int tier, Button btn, TextMeshProUGUI label)
    {
        if (path == null || tier >= path.Length)
        {
            label.text = $"Path {idx}\n(maxed)";
            btn.interactable = false;
            return;
        }
        TowerUpgrade up = path[tier];
        label.text = $"Path {idx} \u2192 {up.upgradeName}\n{up.description}\n<b>{up.cost}g</b>";
        btn.interactable = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(up.cost);
    }

    void BuyUpgrade(int pathIndex)
    {
        if (_activeTower == null) return;
        if (_activeTower.TryBuyUpgrade(pathIndex))
        {
            // Range may have changed — rebuild the ring so it reflects the new size.
            BuildRangeRing(_activeTower);
            RefreshUpgradePanel();
        }
    }

    void SellTower()
    {
        if (_activeTower == null || _activeTower.slot == null) { CloseMenus(); return; }
        int refund = Mathf.RoundToInt((_activeTower.data?.cost ?? 0) * 0.5f);
        CurrencyManager.Instance?.AddGold(refund);
        _activeTower.slot.RemoveTower();
        CloseMenus();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Shared
    // ────────────────────────────────────────────────────────────────────────

    void HideRadial()
    {
        if (_radialRoot != null) _radialRoot.SetActive(false);
        HideTooltip();
    }

    void HideUpgradePanel()
    {
        if (_upgradeRoot != null) _upgradeRoot.SetActive(false);
    }

    void CloseMenus()
    {
        HideRadial();
        HideUpgradePanel();
        HideTooltip();
        _radialOptions.Clear();
        _radialSectorImages.Clear();
        DestroyRangeRing();
        if (_activeSlot != null) _activeSlot.Highlight(false);
        _activeSlot = null;
        _activeTower = null;
        SetMenuOpen(false);
    }

    // ── Range ring ────────────────────────────────────────────────────────────

    void BuildRangeRing(Tower tower)
    {
        DestroyRangeRing();
        if (tower == null) return;
        float radius = tower.currentRange;
        if (radius <= 0f) return;

        _rangeRing = new GameObject("TowerRangeRing");
        _rangeRing.transform.SetParent(tower.transform, false);
        _rangeRing.transform.localPosition = Vector3.zero;

        LineRenderer lr   = _rangeRing.AddComponent<LineRenderer>();
        lr.useWorldSpace  = false;
        lr.loop           = true;
        lr.widthMultiplier = 0.06f;
        lr.material       = new Material(Shader.Find("Sprites/Default"));
        Color c           = new Color(0.4f, 0.95f, 1f, 0.85f);
        lr.startColor     = c;
        lr.endColor       = c;
        lr.sortingOrder   = 99;

        const int segments = 48;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    void DestroyRangeRing()
    {
        if (_rangeRing != null) Destroy(_rangeRing);
        _rangeRing = null;
    }

    void SetMenuOpen(bool open)
    {
        if (open == _menuOpen) return;
        _menuOpen = open;
        if (open) InteractionTimeScale.Begin();
        else      InteractionTimeScale.End();
    }

    void BuildTooltipRoot()
    {
        _tooltipRoot = MakePanel(_canvas.transform, "BuildOptionTooltip", new Color(0.06f, 0.08f, 0.13f, 0.96f));
        RectTransform ttRT = _tooltipRoot.GetComponent<RectTransform>();
        ttRT.pivot = new Vector2(0f, 1f);
        ttRT.sizeDelta = new Vector2(300f, 140f);
        _tooltipRoot.SetActive(false);

        GameObject titleObj = MakeText(_tooltipRoot.transform, "Title", "", 18, Color.white);
        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -10f);
        titleRT.sizeDelta = new Vector2(-18f, 30f);
        _tooltipTitle = titleObj.GetComponent<TextMeshProUGUI>();
        _tooltipTitle.alignment = TextAlignmentOptions.TopLeft;
        _tooltipTitle.fontStyle = FontStyles.Bold;

        GameObject bodyObj = MakeText(_tooltipRoot.transform, "Body", "", 14, new Color(0.86f, 0.90f, 0.98f));
        RectTransform bodyRT = bodyObj.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 0f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.offsetMin = new Vector2(10f, 10f);
        bodyRT.offsetMax = new Vector2(-10f, -42f);
        _tooltipBody = bodyObj.GetComponent<TextMeshProUGUI>();
        _tooltipBody.alignment = TextAlignmentOptions.TopLeft;
        _tooltipBody.enableWordWrapping = true;
        _tooltipBody.overflowMode = TextOverflowModes.Overflow;
    }

    void ShowTooltip(TowerData td, Vector2 screenPos)
    {
        if (_tooltipRoot == null || td == null) return;

        _tooltipTitle.text = $"{td.towerName}  ({td.cost}g)";
        _tooltipBody.text =
            $"Damage: {td.damage}\n" +
            $"Range: {td.range:F1}\n" +
            $"Rate: {td.fireRate:F2}/s\n" +
            $"Type: {td.towerType}";

        MoveTooltip(screenPos);
        _tooltipRoot.SetActive(true);
    }

    void MoveTooltip(Vector2 screenPos)
    {
        if (_tooltipRoot == null || !_tooltipRoot.activeSelf) return;
        if (_canvas == null) return;

        RectTransform canvasRT = _canvas.transform as RectTransform;
        RectTransform ttRT = _tooltipRoot.GetComponent<RectTransform>();
        if (canvasRT == null || ttRT == null) return;

        Camera eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam;
        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, eventCam, out localPos)) return;

        Vector2 anchored = localPos + new Vector2(20f, -18f);
        Vector2 half = canvasRT.rect.size * 0.5f;
        Vector2 size = ttRT.sizeDelta;

        float minX = -half.x;
        float maxX = half.x - size.x;
        float maxY = half.y;
        float minY = -half.y + size.y;
        anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
        anchored.y = Mathf.Clamp(anchored.y, minY, maxY);

        ttRT.anchoredPosition = anchored;
    }

    void HideTooltip()
    {
        if (_tooltipRoot != null) _tooltipRoot.SetActive(false);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────
    static GameObject MakePanel(Transform parent, string name, Color c)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var img = obj.AddComponent<Image>();
        img.color = c;
        return obj;
    }

    static GameObject MakeText(Transform parent, string name, string text, int size, Color c)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = c;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var img = obj.AddComponent<Image>();
        img.color = color;
        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject lbl = MakeText(obj.transform, "Label", label, 16, Color.white);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        return obj;
    }

    static void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
    {
        if (trigger == null || callback == null) return;
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
}
