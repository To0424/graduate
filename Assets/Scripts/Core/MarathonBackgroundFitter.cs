using UnityEngine;

/// <summary>
/// Stretches a SpriteRenderer to fill the main camera's visible bounds at all
/// times. Re-fits every frame so it tracks zoom / aspect changes. The fit
/// includes generous overscan so panning never exposes the empty world edges.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MarathonBackgroundFitter : MonoBehaviour
{
    [Tooltip("Extra factor beyond the camera bounds so panning never shows empty edges.")]
    public float overscan = 2.5f;

    SpriteRenderer _sr;
    Camera         _cam;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _cam = Camera.main;
    }

    void LateUpdate()
    {
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
}
