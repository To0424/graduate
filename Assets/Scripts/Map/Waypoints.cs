using UnityEngine;

public class Waypoints : MonoBehaviour
{
    [Header("Auto-collected from children")]
    public Transform[] points;

    [Header("Multiple spawn points for harder levels")]
    public Transform[] spawnPoints;
    public Transform exitPoint;

    void Awake()
    {
        // Only auto-collect if points weren't already set by PathManager
        if (points == null || points.Length == 0)
        {
            points = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                points[i] = transform.GetChild(i);
            }
        }

        if (points.Length > 0)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                spawnPoints = new Transform[] { points[0] };
            if (exitPoint == null)
                exitPoint = points[points.Length - 1];
        }
    }

    public float GetPathLength()
    {
        float length = 0f;
        for (int i = 0; i < points.Length - 1; i++)
        {
            length += Vector3.Distance(points[i].position, points[i + 1].position);
        }
        return length;
    }

    public Vector3 GetSpawnPosition(int spawnIndex = 0)
    {
        if (spawnPoints != null && spawnIndex < spawnPoints.Length)
            return spawnPoints[spawnIndex].position;
        return points[0].position;
    }

    public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 1;

    void OnDrawGizmos()
    {
        // Draw the path in the editor
        if (points == null || points.Length < 2) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < points.Length - 1; i++)
        {
            if (points[i] != null && points[i + 1] != null)
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
        }

        // Draw spawn points as green spheres
        Gizmos.color = Color.cyan;
        if (spawnPoints != null)
        {
            foreach (var sp in spawnPoints)
            {
                if (sp != null) Gizmos.DrawWireSphere(sp.position, 0.3f);
            }
        }

        // Draw exit as red sphere
        Gizmos.color = Color.red;
        if (exitPoint != null)
            Gizmos.DrawWireSphere(exitPoint.position, 0.3f);
    }
}
