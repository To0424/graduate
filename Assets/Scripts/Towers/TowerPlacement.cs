using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles the placement-ghost lifecycle for both regular towers and heroes
/// (which are now standalone towers placed onto empty slots, NOT augments).
///
/// While placing, the world is slowed via <see cref="InteractionTimeScale"/>
/// so the player can act precisely.  Right-click cancels.  Use
/// <see cref="PlaceAtSlot(TowerData, TowerSlot)"/> for instant placement
/// triggered by the radial build menu.
/// </summary>
public class TowerPlacement : MonoBehaviour
{
    public static TowerPlacement Instance { get; private set; }

    [Header("State")]
    public TowerData selectedTowerData;
    public Tower towerPrefab;
    public bool isPlacing = false;

    [Header("Preview")]
    public GameObject previewObject;

    /// <summary>Fired whenever a tower is built or evolved so menus can refresh.</summary>
    public static event Action OnTowerPlaced;

    private Camera mainCam;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start() { mainCam = Camera.main; }

    void Update()
    {
        if (!isPlacing) return;
        if (Input.GetMouseButtonDown(1)) { CancelPlacement(); return; }

        UpdatePreview();

        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            TryPlace();
    }

    // ── Drag-to-place flow (legacy + shop buttons) ────────────────────────────

    public void StartPlacing(TowerData data, Tower prefab)
    {
        selectedTowerData = data;
        towerPrefab       = prefab;
        if (!isPlacing) InteractionTimeScale.Begin();
        isPlacing         = true;

        SpawnPreview(data);
        AttachRangeIndicator(data.range, new Color(1f, 1f, 0.4f, 0.9f));
        if (data.heroSkill != null && data.heroSkill.radius > 0f)
            AttachRangeIndicator(data.heroSkill.radius, new Color(1f, 0.55f, 0.1f, 0.6f));

        HighlightEmptySlots(true);
    }

    void TryPlace()
    {
        Vector3 mousePos = ScreenToWorld();
        TowerSlot slot   = FindClosestEmptySlot(mousePos);
        if (slot == null) { Debug.Log("[TowerPlacement] No empty slot nearby."); return; }

        PlaceAtSlot(selectedTowerData, slot);
        CancelPlacement();
    }

    // ── Direct-place flow (used by the radial build menu) ─────────────────────

    /// <summary>Build the given tower directly onto a known slot. No mouse follow.</summary>
    public bool PlaceAtSlot(TowerData data, TowerSlot slot)
    {
        if (data == null || slot == null || slot.isOccupied) return false;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.CanAfford(data.cost))
        {
            Debug.Log($"[TowerPlacement] Not enough gold for {data.towerName}.");
            return false;
        }
        if (towerPrefab == null) towerPrefab = FindTowerPrefab();
        if (towerPrefab == null) { Debug.LogWarning("[TowerPlacement] No tower prefab available."); return false; }

        CurrencyManager.Instance.SpendGold(data.cost);
        slot.PlaceTower(towerPrefab, data);
        OnTowerPlaced?.Invoke();
        return true;
    }

    /// <summary>Provide a fallback prefab when called from the radial menu without a current selection.</summary>
    public void SetTowerPrefab(Tower prefab) { towerPrefab = prefab; }

    Tower FindTowerPrefab()
    {
        // GameplayAutoSetup or QuickTestBootstrap should have called SetTowerPrefab.
        return null;
    }

    // ── Cancel & helpers ──────────────────────────────────────────────────────

    public void CancelPlacement()
    {
        if (isPlacing) InteractionTimeScale.End();
        isPlacing = false;
        selectedTowerData = null;
        if (previewObject != null) Destroy(previewObject);
        HighlightEmptySlots(false);
    }

    void SpawnPreview(TowerData data)
    {
        if (previewObject != null) Destroy(previewObject);

        previewObject = new GameObject("TowerPreview");
        SpriteRenderer sr = previewObject.AddComponent<SpriteRenderer>();
        sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.WhiteSquare;
        Color c   = data.sprite != null ? Color.white : Tower.GetTowerColor(data.towerType);
        c.a       = 0.5f;
        sr.color  = c;
        sr.sortingOrder = 100;
    }

    void UpdatePreview()
    {
        if (previewObject == null) return;
        Vector3 pos     = ScreenToWorld();
        TowerSlot snap  = FindClosestEmptySlot(pos);
        if (snap != null) pos = snap.transform.position;
        previewObject.transform.position = pos;
    }

    void AttachRangeIndicator(float radius, Color color)
    {
        if (previewObject == null || radius <= 0f) return;

        GameObject ring = new GameObject("RangeIndicator");
        ring.transform.SetParent(previewObject.transform, false);
        ring.transform.localPosition = Vector3.zero;

        LineRenderer lr  = ring.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop          = true;
        lr.widthMultiplier = 0.06f;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = color;
        lr.endColor      = color;
        lr.sortingOrder  = 99;

        const int segments = 48;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    Vector3 ScreenToWorld()
    {
        Vector3 p = mainCam.ScreenToWorldPoint(Input.mousePosition);
        p.z = 0;
        return p;
    }

    TowerSlot FindClosestEmptySlot(Vector3 pos)
    {
        const float maxDist = 1.0f;
        TowerSlot closest = null;
        float closestDist = float.MaxValue;
        foreach (TowerSlot slot in FindObjectsByType<TowerSlot>(FindObjectsSortMode.None))
        {
            if (slot.isOccupied) continue;
            float d = Vector3.Distance(pos, slot.transform.position);
            if (d < closestDist && d < maxDist) { closestDist = d; closest = slot; }
        }
        return closest;
    }

    void HighlightEmptySlots(bool on)
    {
        foreach (TowerSlot slot in FindObjectsByType<TowerSlot>(FindObjectsSortMode.None))
            if (!slot.isOccupied) slot.Highlight(on);
    }
}
