using UnityEngine;

/// <summary>
/// Spawns a short-lived "puff of smoke" GameObject when an enemy dies so the
/// removal feels less abrupt. Three layers, in order of preference:
///
///   1. <see cref="EnemyData.deathAnimatorOverride"/> — drag a unique
///      Animator Controller in the inspector for boss-specific death VFX.
///   2. Resources/Animators/EnemyDeath.controller — global default shared by
///      every enemy. Drop your "minecraft puff" controller here once and all
///      enemies use it automatically (no per-enemy wiring needed).
///   3. Procedural fallback — a white circle that scales up while fading
///      out. Looks like a quick poof, no asset required. Used until you
///      author the global controller.
///
/// The GameObject self-destroys after <see cref="EnemyData.deathFxDuration"/>
/// seconds.
/// </summary>
public static class EnemyDeathFX
{
    /// <summary>Cached lookup so we only hit Resources once per session.</summary>
    static RuntimeAnimatorController _defaultController;
    static bool _defaultLookupDone;

    public static void Spawn(EnemyData data, Vector3 position, Vector3 worldScale, bool flipX = false)
    {
        if (data == null) return;

        var go = new GameObject("EnemyDeathFX");
        go.transform.position = position;
        // Compose the enemy's runtime scale (which already includes
        // visualScale / bossScale) with the FX-specific multiplier.
        float fxScale = data.deathFxScale > 0f ? data.deathFxScale : 1f;
        Vector3 baseScale = worldScale.sqrMagnitude > 0f ? worldScale : Vector3.one;
        go.transform.localScale = baseScale * fxScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 6; // above enemies, below UI
        sr.flipX = flipX;    // preserve the enemy's facing direction
        sr.sprite = data.deathSpriteOverride != null
                  ? data.deathSpriteOverride
                  : (data.sprite != null ? data.sprite : RuntimeSprite.Circle);

        // Layer 1: per-enemy override animator.
        RuntimeAnimatorController controller = data.deathAnimatorOverride;

        // Layer 2: global default animator from Resources.
        if (controller == null)
        {
            if (!_defaultLookupDone)
            {
                _defaultController = Resources.Load<RuntimeAnimatorController>("Animators/EnemyDeath");
                _defaultLookupDone = true;
            }
            controller = _defaultController;
        }

        if (controller != null)
        {
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.updateMode = AnimatorUpdateMode.Normal;
        }
        else
        {
            // Layer 3: procedural smoke puff. Independent of any asset.
            sr.color = new Color(0.92f, 0.92f, 0.92f, 0.85f);
            go.AddComponent<EnemyDeathFXProcedural>().Begin(data.deathFxDuration);
        }

        Object.Destroy(go, Mathf.Max(0.05f, data.deathFxDuration));
    }
}

/// <summary>Procedural fallback: scales up while fading alpha to zero.</summary>
public class EnemyDeathFXProcedural : MonoBehaviour
{
    SpriteRenderer _sr;
    Vector3 _startScale;
    float _t;
    float _duration = 0.55f;

    public void Begin(float duration)
    {
        _duration = Mathf.Max(0.05f, duration);
        _sr = GetComponent<SpriteRenderer>();
        _startScale = transform.localScale;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / _duration);
        // Scale from 1.0x to 1.6x with a slight ease-out.
        float s = 1f + 0.6f * (1f - (1f - k) * (1f - k));
        transform.localScale = _startScale * s;
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = Mathf.Lerp(0.85f, 0f, k);
            _sr.color = c;
        }
    }
}
