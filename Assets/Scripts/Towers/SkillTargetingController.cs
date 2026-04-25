using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles the "pick a ground location" flow used by ground-targeted hero
/// skills. Slows time while active, draws a circular reticle that snaps to
/// the cursor, fires <see cref="OnTargetSelected"/> on left-click, and is
/// cancelled by right-click / Escape.
///
/// Singleton; created lazily via <see cref="EnsureExists"/>.
/// </summary>
public class SkillTargetingController : MonoBehaviour
{
    public static SkillTargetingController Instance { get; private set; }

    public bool IsTargeting => _targeting;

    /// <summary>World-space position of the reticle (i.e. cursor) while targeting.</summary>
    public Vector3 ReticlePosition
    {
        get
        {
            if (_reticle != null) return _reticle.transform.position;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return Vector3.zero;
            Vector3 p = _cam.ScreenToWorldPoint(Input.mousePosition); p.z = 0; return p;
        }
    }

    /// <summary>Programmatically commit at the reticle position. Used by the
    /// drag-from-hero-card and click-arm-then-click-again flows.</summary>
    public void CommitAtReticle()
    {
        if (!_targeting) return;
        Confirm(ReticlePosition);
    }

    private bool _targeting;
    private Camera _cam;
    private GameObject _reticle;
    private Action<Vector3> _onSelected;
    private Action _onCancelled;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("SkillTargetingController");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SkillTargetingController>();
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        _cam = Camera.main;
    }

    /// <summary>Begin targeting. The reticle radius is purely visual.</summary>
    public void BeginTargeting(float reticleRadius, Color color,
                               Action<Vector3> onSelected, Action onCancelled = null)
    {
        if (_targeting) Cancel();
        _targeting   = true;
        _onSelected  = onSelected;
        _onCancelled = onCancelled;
        InteractionTimeScale.Begin();

        if (_cam == null) _cam = Camera.main;
        BuildReticle(reticleRadius, color);
    }

    void BuildReticle(float radius, Color color)
    {
        _reticle = new GameObject("AOEReticle");
        var sr = _reticle.AddComponent<SpriteRenderer>();
        sr.sprite       = RuntimeSprite.Circle;
        sr.color        = new Color(color.r, color.g, color.b, 0.35f);
        sr.sortingOrder = 100;
        _reticle.transform.localScale = Vector3.one * (radius * 2f);

        // Outline
        var outline = new GameObject("Outline");
        outline.transform.SetParent(_reticle.transform, false);
        var lr = outline.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;
        lr.loop            = true;
        lr.widthMultiplier = 0.04f;          // (in local space — visually larger because of parent scale)
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.startColor      = color;
        lr.endColor        = color;
        lr.sortingOrder    = 101;

        const int segments = 48;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)segments) * Mathf.PI * 2f;
            // Outline radius 0.5 in local (sphere sprite is unit-diameter), parent scale handles size
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * 0.5f, Mathf.Sin(t) * 0.5f, 0f));
        }
    }

    void Update()
    {
        if (!_targeting) return;

        Vector3 mouse = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0;
        if (_reticle != null) _reticle.transform.position = mouse;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Confirm(mouse);
        }
    }

    void Confirm(Vector3 worldPos)
    {
        var cb = _onSelected;
        Teardown();
        cb?.Invoke(worldPos);
    }

    public void Cancel()
    {
        var cb = _onCancelled;
        Teardown();
        cb?.Invoke();
    }

    void Teardown()
    {
        if (!_targeting) return;
        _targeting = false;
        _onSelected = null;
        _onCancelled = null;
        if (_reticle != null) Destroy(_reticle);
        _reticle = null;
        InteractionTimeScale.End();
    }
}
