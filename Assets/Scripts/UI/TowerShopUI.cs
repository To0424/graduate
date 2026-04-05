using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerShopUI : MonoBehaviour
{
    [Header("Tower options")]
    public TowerShopItem[] shopItems;

    [Header("Prefab for tower (base — data determines type)")]
    public GameObject towerPrefab;

    [System.Serializable]
    public class TowerShopItem
    {
        public TowerData towerData;
        public Button button;
        public TextMeshProUGUI costText;
    }

    void Start()
    {
        for (int i = 0; i < shopItems.Length; i++)
        {
            int index = i;  // capture for closure
            TowerShopItem item = shopItems[i];

            if (item.costText != null)
                item.costText.text = $"{item.towerData.cost}g";

            if (item.button != null)
                item.button.onClick.AddListener(() => OnTowerSelected(index));
        }
    }

    void Update()
    {
        // Gray out buttons the player can't afford
        foreach (var item in shopItems)
        {
            if (item.button != null && CurrencyManager.Instance != null)
            {
                item.button.interactable = CurrencyManager.Instance.CanAfford(item.towerData.cost);
            }
        }
    }

    void OnTowerSelected(int index)
    {
        TowerShopItem item = shopItems[index];

        // Check if professor tower is locked
        if (item.towerData.isProfessorTower)
        {
            FacultyData faculty = FindFacultyForTower(item.towerData);
            if (faculty != null && !GameManager.Instance.IsProfessorTowerUnlocked(faculty))
            {
                Debug.Log($"Professor tower locked! Clear all {faculty.facultyName} courses first.");
                return;
            }
        }

        TowerPlacement.Instance?.StartPlacing(item.towerData, towerPrefab.GetComponent<Tower>());
    }

    FacultyData FindFacultyForTower(TowerData towerData)
    {
        if (GameManager.Instance == null) return null;
        foreach (FacultyData f in GameManager.Instance.allFaculties)
        {
            if (f.professorTower == towerData) return f;
        }
        return null;
    }
}
