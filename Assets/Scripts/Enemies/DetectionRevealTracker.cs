using UnityEngine;

/// <summary>
/// Attach this component automatically to every Stealth enemy on Initialize().
/// Each fixed-update it checks whether any detection tower is within range.
/// If none is, the enemy is concealed.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class DetectionRevealTracker : MonoBehaviour
{
    private Enemy _enemy;

    void Awake() => _enemy = GetComponent<Enemy>();

    void FixedUpdate()
    {
        if (_enemy == null) return;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
        bool detected = false;

        foreach (Tower t in towers)
        {
            if (t.data == null || !t.data.hasDetection) continue;
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist <= t.currentRange) { detected = true; break; }
        }

        if (detected)
            _enemy.Reveal();
        else
            _enemy.Conceal();
    }
}
