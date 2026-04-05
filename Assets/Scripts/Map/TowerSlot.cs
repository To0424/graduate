using UnityEngine;

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
        return true;
    }

    public void RemoveTower()
    {
        if (currentTower != null)
        {
            Destroy(currentTower.gameObject);
            currentTower = null;
        }
        isOccupied = false;
    }

    public void Highlight(bool on)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = on ? Color.yellow : originalColor;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.blue;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.6f);
    }
}
