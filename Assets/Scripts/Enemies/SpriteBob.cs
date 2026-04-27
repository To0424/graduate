using UnityEngine;

/// <summary>
/// Adds a simple sinusoidal bob to a sprite (vertical by default). Attach to
/// any GameObject — useful for giving static sprites a tiny bit of life.
/// Operates on the local position so it composes cleanly with parent movement
/// (e.g. an enemy walking along a path will still bob up and down on top of it).
/// </summary>
public class SpriteBob : MonoBehaviour
{
    [Tooltip("Peak vertical offset in world units.")]
    public float amplitude = 0.08f;
    [Tooltip("Bobs per second.")]
    public float frequency = 2f;
    [Tooltip("Random phase offset so multiple instances don't bob in lockstep.")]
    public bool randomizePhase = true;

    private float _phase;
    private float _lastOffset;

    void OnEnable()
    {
        _phase      = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        _lastOffset = 0f;
    }

    void LateUpdate()
    {
        // Run in LateUpdate so this composes on top of MoveAlongPath (which
        // runs in Update). Subtract the previous frame's bob offset before
        // applying the new one so we don't fight other systems writing to Y.
        float y = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + _phase) * amplitude;
        var p = transform.localPosition;
        p.y = p.y - _lastOffset + y;
        transform.localPosition = p;
        _lastOffset = y;
    }
}
