using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Adds RTS-style pan + zoom to an orthographic 2D camera.
/// Pan: hold middle-mouse OR right-mouse and drag.
/// Zoom: scroll wheel.
/// Optional WASD/arrow-keys also pan (configurable).
/// Designed to coexist with the gameplay's left-click tower placement.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Zoom")]
    public float minOrthoSize = 4f;
    public float maxOrthoSize = 18f;
    public float zoomStep     = 1.2f;

    [Header("Pan")]
    public bool  enableKeyboardPan = true;
    public float keyboardPanSpeed  = 12f;

    [Header("Bounds (set to 0,0 to disable)")]
    public Vector2 worldMin = Vector2.zero;
    public Vector2 worldMax = Vector2.zero;

    Camera _cam;
    Vector3 _dragWorldOrigin;
    bool    _dragging;

    void Awake() { _cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        HandleZoom();
        HandlePan();
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        if (IsPointerOverUI()) return;

        // Zoom toward the cursor: remember world-space cursor before & after.
        Vector3 before = _cam.ScreenToWorldPoint(Input.mousePosition);
        _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * zoomStep,
                                            minOrthoSize, maxOrthoSize);
        Vector3 after = _cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 shift = before - after;
        shift.z = 0f;
        transform.position += shift;
        ClampToBounds();
    }

    void HandlePan()
    {
        // Begin drag on middle or right mouse, but only if not on UI.
        if ((Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1)) && !IsPointerOverUI())
        {
            _dragWorldOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
            _dragging = true;
        }
        if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
            _dragging = false;

        if (_dragging && (Input.GetMouseButton(2) || Input.GetMouseButton(1)))
        {
            Vector3 cur = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 diff = _dragWorldOrigin - cur;
            diff.z = 0f;
            transform.position += diff;
            // Don't refresh _dragWorldOrigin: the camera moved by `diff`, so
            // the next ScreenToWorldPoint with the same screen pos would
            // already give us the original world point back — perfect for
            // continuous panning.
            ClampToBounds();
        }

        if (enableKeyboardPan)
        {
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
            if (h != 0f || v != 0f)
            {
                transform.position += new Vector3(h, v, 0f) * keyboardPanSpeed * Time.unscaledDeltaTime;
                ClampToBounds();
            }
        }
    }

    void ClampToBounds()
    {
        if (worldMin == Vector2.zero && worldMax == Vector2.zero) return;
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, worldMin.x, worldMax.x);
        p.y = Mathf.Clamp(p.y, worldMin.y, worldMax.y);
        transform.position = p;
    }

    static bool IsPointerOverUI()
    {
        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }
}
