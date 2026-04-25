using UnityEngine;

/// <summary>
/// A buildable plot. Stores the placed Tower and exposes hover/highlight
/// helpers used during placement and the radial build menu.
/// </summary>
public class TowerSlot : MonoBehaviour
{
    public bool isOccupied = false;
    public Tower currentTower;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    public bool PlaceTower(Tower towerPrefab, TowerData data)
    {
        if (isOccupied) return false;

        Tower tower = Instantiate(towerPrefab, transform.position, Quaternion.identity);
        tower.Initialize(data, this);
        currentTower = tower;
        isOccupied = true;

        if (data != null && data.unique) DeployedUniqueRegistry.MarkDeployed(data);

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        return true;
    }

    public void RemoveTower()
    {
        if (currentTower != null)
        {
            if (currentTower.data != null && currentTower.data.unique)
                DeployedUniqueRegistry.Unmark(currentTower.data);
            Destroy(currentTower.gameObject);
            currentTower = null;
        }
        isOccupied = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>Highlight during placement / mouse-over (yellow = available).</summary>
    public void Highlight(bool on)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = on ? Color.yellow : originalColor;
    }
}
