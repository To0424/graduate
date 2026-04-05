using UnityEngine;
using UnityEngine.EventSystems;

public class TowerPlacement : MonoBehaviour
{
    public static TowerPlacement Instance { get; private set; }

    [Header("State")]
    public TowerData selectedTowerData;
    public Tower towerPrefab;
    public bool isPlacing = false;

    [Header("Preview")]
    public GameObject previewObject;

    private Camera mainCam;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (!isPlacing) return;

        // Right click to cancel
        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        UpdatePreview();

        // Left click to place
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            TryPlace();
        }
    }

    public void StartPlacing(TowerData data, Tower prefab)
    {
        selectedTowerData = data;
        towerPrefab = prefab;
        isPlacing = true;

        if (previewObject != null)
            Destroy(previewObject);

        previewObject = new GameObject("TowerPreview");
        SpriteRenderer sr = previewObject.AddComponent<SpriteRenderer>();
        sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.WhiteSquare;
        Color previewColor = data.sprite != null ? Color.white : Tower.GetTowerColor(data.towerType);
        previewColor.a = 0.5f;
        sr.color = previewColor;
        sr.sortingOrder = 100;

        HighlightSlots(true);
    }

    public void CancelPlacement()
    {
        isPlacing = false;
        selectedTowerData = null;
        towerPrefab = null;
        if (previewObject != null)
            Destroy(previewObject);
        HighlightSlots(false);
    }

    void UpdatePreview()
    {
        if (previewObject == null) return;
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        previewObject.transform.position = mousePos;
    }

    void TryPlace()
    {
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        // Find the closest tower slot to the click
        TowerSlot closestSlot = FindClosestSlot(mousePos);
        if (closestSlot == null)
        {
            Debug.Log($"[TowerPlacement] No slot near {mousePos}");
            return;
        }

        if (closestSlot.isOccupied)
        {
            Debug.Log("Slot already occupied!");
            return;
        }

        if (!CurrencyManager.Instance.CanAfford(selectedTowerData.cost))
        {
            Debug.Log("Not enough gold!");
            return;
        }

        // Place the tower
        CurrencyManager.Instance.SpendGold(selectedTowerData.cost);
        closestSlot.PlaceTower(towerPrefab, selectedTowerData);
        Debug.Log($"[TowerPlacement] Placed {selectedTowerData.towerName} at {closestSlot.transform.position}");

        CancelPlacement();
    }

    TowerSlot FindClosestSlot(Vector3 pos)
    {
        float maxClickDist = 1.0f;
        TowerSlot closest = null;
        float closestDist = float.MaxValue;

        TowerSlot[] slots = FindObjectsByType<TowerSlot>(FindObjectsSortMode.None);
        foreach (TowerSlot slot in slots)
        {
            float dist = Vector3.Distance(pos, slot.transform.position);
            if (dist < closestDist && dist < maxClickDist)
            {
                closestDist = dist;
                closest = slot;
            }
        }
        return closest;
    }

    void HighlightSlots(bool on)
    {
        TowerSlot[] slots = FindObjectsByType<TowerSlot>(FindObjectsSortMode.None);
        foreach (TowerSlot slot in slots)
        {
            if (!slot.isOccupied)
                slot.Highlight(on);
        }
    }
}
