using UnityEngine;

/// <summary>
/// Fits a SpriteRenderer to the marathon map area once so the background lives
/// in world-space with the path. This keeps zoom/pan behavior consistent:
/// background and path scale together instead of drifting apart.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MarathonBackgroundFitter : MonoBehaviour
{
    [Tooltip("Extra multiplier on map bounds. 1.30 means 30% margin around the map.")]
    public float overscan = 1.30f;

    [Tooltip("If true, re-fit every frame to camera bounds (old behavior).")]
    public bool followCamera = false;

    SpriteRenderer _sr;
    Camera         _cam;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _cam = Camera.main;
    }

    void Start()
    {
        // Default behavior: fit once to world/map bounds so path and background
        // remain aligned while zooming and panning.
        if (!followCamera)
            FitToMapBoundsOrCamera();
    }

    void LateUpdate()
    {
        if (!followCamera) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _sr == null || _sr.sprite == null) return;

        float h = _cam.orthographicSize * 2f * overscan;
        float w = h * _cam.aspect;
        float spW = _sr.sprite.bounds.size.x;
        float spH = _sr.sprite.bounds.size.y;
        if (spW <= 0f || spH <= 0f) return;

        // Use the larger ratio so the image always covers (no gaps); excess is cropped.
        float scale = Mathf.Max(w / spW, h / spH);
        transform.localScale = new Vector3(scale, scale, 1f);

        // Stay centred on the camera so panning never exposes voids.
        Vector3 cp = _cam.transform.position;
        transform.position = new Vector3(cp.x, cp.y, transform.position.z);
    }

    void FitToMapBoundsOrCamera()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _sr == null || _sr.sprite == null) return;

        Bounds mapBounds;
        bool hasMapBounds = TryGetMapBounds(out mapBounds);

        float targetW;
        float targetH;
        Vector3 center;

        if (hasMapBounds)
        {
            targetW = Mathf.Max(1f, mapBounds.size.x * overscan);
            targetH = Mathf.Max(1f, mapBounds.size.y * overscan);
            center = mapBounds.center;
        }
        else
        {
            // Fallback: fit to current camera view once.
            targetH = _cam.orthographicSize * 2f * overscan;
            targetW = targetH * _cam.aspect;
            center = _cam.transform.position;
        }

        float spW = _sr.sprite.bounds.size.x;
        float spH = _sr.sprite.bounds.size.y;
        if (spW <= 0f || spH <= 0f) return;

        float scale = Mathf.Max(targetW / spW, targetH / spH);
        transform.localScale = new Vector3(scale, scale, 1f);
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }

    bool TryGetMapBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        PathManager pm = FindFirstObjectByType<PathManager>();
        if (pm == null) return false;

        // Main waypoint path
        if (pm.currentWaypoints != null && pm.currentWaypoints.points != null)
        {
            foreach (Transform t in pm.currentWaypoints.points)
                Encapsulate(t, ref initialized, ref bounds);
        }

        // Spawn markers
        if (pm.currentWaypoints != null && pm.currentWaypoints.spawnPoints != null)
        {
            foreach (Transform t in pm.currentWaypoints.spawnPoints)
                Encapsulate(t, ref initialized, ref bounds);
        }

        // Exit marker
        if (pm.currentWaypoints != null && pm.currentWaypoints.exitPoint != null)
            Encapsulate(pm.currentWaypoints.exitPoint, ref initialized, ref bounds);

        // Multi-spawn custom routes
        if (pm.currentWaypoints != null && pm.currentWaypoints.perSpawnPaths != null)
        {
            foreach (Transform[] chain in pm.currentWaypoints.perSpawnPaths)
            {
                if (chain == null) continue;
                foreach (Transform t in chain)
                    Encapsulate(t, ref initialized, ref bounds);
            }
        }

        // Tower slots
        if (pm.currentTowerSlots != null)
        {
            foreach (TowerSlot slot in pm.currentTowerSlots)
                Encapsulate(slot != null ? slot.transform : null, ref initialized, ref bounds);
        }

        return initialized;
    }

    static void Encapsulate(Transform t, ref bool initialized, ref Bounds bounds)
    {
        if (t == null) return;
        if (!initialized)
        {
            bounds = new Bounds(t.position, Vector3.zero);
            initialized = true;
        }
        else
        {
            bounds.Encapsulate(t.position);
        }
    }
}
