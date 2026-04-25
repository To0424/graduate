using UnityEngine;

public class PathManager : MonoBehaviour
{
    [Header("Path pattern pools by difficulty tier")]
    public PathPatternData[] easyPatterns;    // tier 1
    public PathPatternData[] mediumPatterns;  // tier 2
    public PathPatternData[] hardPatterns;    // tier 3
    public PathPatternData[] expertPatterns;  // tier 4

    [Header("Runtime references (set after loading)")]
    public Waypoints currentWaypoints;
    public TowerSlot[] currentTowerSlots;

    [Header("Prefabs")]
    public GameObject waypointMarkerPrefab;
    public GameObject towerSlotPrefab;

    public void LoadPathForTier(int tier)
    {
        PathPatternData[] pool = GetPoolForTier(tier);
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"No path patterns for tier {tier}!");
            return;
        }

        PathPatternData pattern = pool[Random.Range(0, pool.Length)];
        BuildPathFromPattern(pattern);
    }

    PathPatternData[] GetPoolForTier(int tier)
    {
        switch (tier)
        {
            case 1: return easyPatterns;
            case 2: return mediumPatterns;
            case 3: return hardPatterns;
            case 4: return expertPatterns;
            default: return easyPatterns;
        }
    }

    void BuildPathFromPattern(PathPatternData pattern)
    {
        // Clear existing
        ClearCurrentPath();

        // Create path parent
        GameObject pathObj = new GameObject("Path");
        Waypoints wp = pathObj.AddComponent<Waypoints>();

        // Create waypoint children
        Transform[] waypointTransforms = new Transform[pattern.waypointPositions.Length];
        for (int i = 0; i < pattern.waypointPositions.Length; i++)
        {
            GameObject point = new GameObject($"Waypoint{i}");
            point.transform.position = pattern.waypointPositions[i];
            point.transform.SetParent(pathObj.transform);
            waypointTransforms[i] = point.transform;

            // Visual: small circle at each waypoint
            SpriteRenderer dot = point.AddComponent<SpriteRenderer>();
            dot.sprite = RuntimeSprite.Circle;
            dot.color = new Color(0.55f, 1f, 0.55f, 0.95f);
            dot.sortingOrder = 1;
            point.transform.localScale = Vector3.one * 0.3f;
        }

        // Draw path lines between waypoints
        for (int i = 0; i < waypointTransforms.Length - 1; i++)
        {
            CreatePathLine(pathObj.transform, waypointTransforms[i].position, waypointTransforms[i + 1].position, i);
        }

        // Set spawn points
        Transform[] spawns = new Transform[pattern.spawnPointPositions.Length];
        for (int i = 0; i < pattern.spawnPointPositions.Length; i++)
        {
            GameObject sp = new GameObject($"SpawnPoint{i}");
            sp.transform.position = pattern.spawnPointPositions[i];
            sp.transform.SetParent(pathObj.transform);
            spawns[i] = sp.transform;

            // Visual: red marker for spawn (enemies come from here)
            SpriteRenderer sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprite.Circle;
            sr.color = Color.red;
            sr.sortingOrder = 2;
            sp.transform.localScale = Vector3.one * 0.5f;
        }
        wp.spawnPoints = spawns;

        // Set exit
        GameObject exitObj = new GameObject("Exit");
        exitObj.transform.position = pattern.exitPosition;
        exitObj.transform.SetParent(pathObj.transform);
        wp.exitPoint = exitObj.transform;

        // Visual: green marker for exit (the home base you defend)
        SpriteRenderer exitSR = exitObj.AddComponent<SpriteRenderer>();
        exitSR.sprite = RuntimeSprite.Circle;
        exitSR.color = Color.green;
        exitSR.sortingOrder = 2;
        exitObj.transform.localScale = Vector3.one * 0.5f;

        wp.points = waypointTransforms;
        currentWaypoints = wp;

        // Per-spawn paths (custom maps may define a different waypoint chain
        // for each spawn). When present, draw extra path lines so devs can
        // see them, and store them on the Waypoints component for the spawner.
        if (pattern.spawnWaypointPositions != null && pattern.spawnWaypointPositions.Length > 0)
        {
            wp.perSpawnPaths = new Transform[pattern.spawnWaypointPositions.Length][];
            for (int s = 0; s < pattern.spawnWaypointPositions.Length; s++)
            {
                var chain = pattern.spawnWaypointPositions[s];
                if (chain == null || chain.positions == null || chain.positions.Length == 0)
                {
                    wp.perSpawnPaths[s] = waypointTransforms;
                    continue;
                }
                Transform[] tArr = new Transform[chain.positions.Length];
                for (int j = 0; j < chain.positions.Length; j++)
                {
                    GameObject pt = new GameObject($"Spawn{s}_WP{j}");
                    pt.transform.position = chain.positions[j];
                    pt.transform.SetParent(pathObj.transform);
                    tArr[j] = pt.transform;
                }
                for (int j = 0; j < tArr.Length - 1; j++)
                    CreatePathLine(pathObj.transform, tArr[j].position, tArr[j + 1].position, 1000 + s * 100 + j);

                // Home marker at the END of every per-spawn chain (multi-home maps).
                Transform homeT = tArr[tArr.Length - 1];
                SpriteRenderer homeSR = homeT.gameObject.AddComponent<SpriteRenderer>();
                homeSR.sprite = RuntimeSprite.Circle;
                homeSR.color = Color.green;
                homeSR.sortingOrder = 2;
                homeT.localScale = Vector3.one * 0.7f;

                wp.perSpawnPaths[s] = tArr;
            }
        }

        // Create tower slots with visible squares
        currentTowerSlots = new TowerSlot[pattern.towerSlotPositions.Length];
        GameObject slotsParent = new GameObject("TowerSlots");
        for (int i = 0; i < pattern.towerSlotPositions.Length; i++)
        {
            GameObject slotObj = new GameObject($"Slot{i}");
            slotObj.transform.position = pattern.towerSlotPositions[i];
            slotObj.transform.SetParent(slotsParent.transform);

            // Add TowerSlot component
            TowerSlot slot = slotObj.AddComponent<TowerSlot>();

            // Visual: semi-transparent square
            SpriteRenderer sr = slotObj.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprite.WhiteSquare;
            sr.color = new Color(0.45f, 0.78f, 1f, 0.95f);
            sr.sortingOrder = 2;
            slotObj.transform.localScale = Vector3.one * 0.7f;

            // Click target for the radial build menu
            BoxCollider2D col = slotObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            currentTowerSlots[i] = slot;
        }
    }

    /// <summary>
    /// Creates a thin stretched sprite between two points to visualize the path.
    /// </summary>
    void CreatePathLine(Transform parent, Vector3 from, Vector3 to, int index)
    {
        GameObject line = new GameObject($"PathLine{index}");
        line.transform.SetParent(parent);
        SpriteRenderer sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.WhiteSquare;
        sr.color = new Color(1f, 0.85f, 0.45f, 1f);
        sr.sortingOrder = 0;

        Vector3 midpoint = (from + to) / 2f;
        line.transform.position = midpoint;

        float distance = Vector3.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        line.transform.rotation = Quaternion.Euler(0, 0, angle);
        line.transform.localScale = new Vector3(distance, 0.42f, 1f);
    }

    void ClearCurrentPath()
    {
        if (currentWaypoints != null)
            Destroy(currentWaypoints.gameObject);

        GameObject existingSlots = GameObject.Find("TowerSlots");
        if (existingSlots != null)
            Destroy(existingSlots);

        currentWaypoints = null;
        currentTowerSlots = null;
    }
}
