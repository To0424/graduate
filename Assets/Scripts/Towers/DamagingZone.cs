using UnityEngine;

/// <summary>
/// Spawns a circular damage-over-time zone at a world position. Damage is
/// applied to every Enemy inside <see cref="radius"/> on every <see cref="tickInterval"/>
/// second tick, for a total of <see cref="duration"/> seconds. Visualised as a
/// translucent disc with a coloured outline.
/// </summary>
public class DamagingZone : MonoBehaviour
{
    public int        damagePerTick = 12;
    public float      tickInterval  = 0.4f;
    public float      duration      = 3f;
    public float      radius        = 1.5f;
    public DamageType damageType    = DamageType.Pierce;
    public Color      tint          = new Color(1f, 0.45f, 0.1f, 0.4f);

    private float _life;
    private float _tickTimer;

    public static DamagingZone Spawn(Vector3 pos, HeroSkillData skill)
    {
        GameObject go = new GameObject("DamagingZone");
        go.transform.position = pos;
        DamagingZone z = go.AddComponent<DamagingZone>();
        z.damagePerTick = skill.aoeDamagePerTick;
        z.tickInterval  = Mathf.Max(0.05f, skill.aoeTickInterval);
        z.duration      = skill.aoeDuration;
        z.radius        = Mathf.Max(0.1f, skill.radius);
        z.damageType    = skill.aoeDamageType;
        z.BuildVisual();
        return z;
    }

    void BuildVisual()
    {
        // Filled disc
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = RuntimeSprite.Circle;
        sr.color        = tint;
        sr.sortingOrder = 1;
        transform.localScale = Vector3.one * (radius * 2f);

        // Outline
        var outline = new GameObject("Outline");
        outline.transform.SetParent(transform, false);
        var lr = outline.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;
        lr.loop            = true;
        lr.widthMultiplier = 0.05f;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        Color edge = tint; edge.a = 1f;
        lr.startColor = edge;
        lr.endColor   = edge;
        lr.sortingOrder = 2;
        const int seg = 48;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float t = (i / (float)seg) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * 0.5f, Mathf.Sin(t) * 0.5f, 0f));
        }
    }

    void Update()
    {
        _life += Time.deltaTime;
        _tickTimer -= Time.deltaTime;
        if (_tickTimer <= 0f)
        {
            _tickTimer = tickInterval;
            ApplyTick();
        }
        if (_life >= duration) Destroy(gameObject);
    }

    void ApplyTick()
    {
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Vector3 worldPos = transform.position;
        foreach (Enemy e in all)
        {
            if (e == null) continue;
            if (Vector3.Distance(worldPos, e.transform.position) <= radius)
                e.TakeDamage(damagePerTick, damageType);
        }
    }
}
