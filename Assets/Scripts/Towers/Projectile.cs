using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float maxLifetime = 5f;

    private Enemy target;
    private int damage;
    private DamageType damageType;
    private float lifetime;

    // Splash + slow payloads (0 / 1f means inert).
    private float splashRadius = 0f;
    private float splashFraction = 0.6f;
    private float slowMultiplier = 1f;
    private float slowDuration = 0f;

    // ── Arc trajectory state ─────────────────────────────────────────────
    // When arcHeight > 0 the projectile lobs in a parabola from the
    // launch point to the target's CURRENT position (re-snapped each frame
    // so it still tracks moving enemies, but with vertical arc offset).
    private float arcHeight = 0f;
    private Vector3 arcStart;
    private float arcTotalDistance;
    private float arcTraveled;

    public void Initialize(Enemy targetEnemy, int dmg, DamageType type = DamageType.Normal,
                           float splashR = 0f, float splashFrac = 0.6f,
                           float slowMul = 1f, float slowDur = 0f,
                           float arcH = 0f, Sprite spriteOverride = null)
    {
        target         = targetEnemy;
        damage         = dmg;
        damageType     = type;
        splashRadius   = splashR;
        splashFraction = splashFrac;
        slowMultiplier = slowMul;
        slowDuration   = slowDur;
        arcHeight      = arcH;

        // Initialise arc state from the launch position.
        if (arcHeight > 0f && target != null)
        {
            arcStart = transform.position;
            arcTotalDistance = Vector2.Distance(arcStart, target.transform.position);
            arcTraveled = 0f;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Sprite priority: explicit per-tower override > prefab sprite > fallback circle.
            if (spriteOverride != null) sr.sprite = spriteOverride;
            else if (sr.sprite == null)
            {
                sr.sprite = RuntimeSprite.Circle;
                if (slowMul < 1f && slowDur > 0f)        sr.color = new Color(0.55f, 0.85f, 1f); // ice
                else if (splashR > 0f)                    sr.color = new Color(1f, 0.5f, 0.1f); // cannon
                else if (type == DamageType.Pierce)       sr.color = new Color(1f, 0.5f, 0f);
                else                                      sr.color = Color.yellow;
            }
        }
    }

    void Update()
    {
        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime) { Destroy(gameObject); return; }
        if (target == null)          { Destroy(gameObject); return; }

        if (arcHeight > 0f)
        {
            UpdateArc();
        }
        else
        {
            Vector3 direction = (target.transform.position - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void UpdateArc()
    {
        // Re-target each frame so a moving enemy is still hit. Distance
        // budget is recomputed against the current target so the arc stays
        // proportional even if the enemy walks away mid-flight.
        Vector3 targetPos = target.transform.position;
        arcTraveled += speed * Time.deltaTime;
        // Total distance can grow when the target retreats; clamp t to (0,1].
        arcTotalDistance = Mathf.Max(arcTotalDistance,
                                     arcTraveled + Vector2.Distance(transform.position, targetPos));
        float t = Mathf.Clamp01(arcTraveled / Mathf.Max(0.0001f, arcTotalDistance));

        // Linear ground motion + parabolic vertical offset (4*h*t*(1-t)).
        Vector3 ground = Vector3.Lerp(arcStart, targetPos, t);
        float yOffset = 4f * arcHeight * t * (1f - t);
        Vector3 newPos = ground + new Vector3(0f, yOffset, 0f);

        // Rotate so the sprite points along the tangent of the arc.
        Vector3 delta = newPos - transform.position;
        if (delta.sqrMagnitude > 0.000001f)
        {
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        transform.position = newPos;

        // Detonate on landing — covers fast-moving targets that the trigger
        // collider might overshoot when the arc snaps to t=1.
        if (t >= 1f) DetonateOnTarget();
    }

    void DetonateOnTarget()
    {
        if (target == null) { Destroy(gameObject); return; }
        ApplyHit(target, damage);
        if (splashRadius > 0f)
        {
            int splashDamage = Mathf.Max(1, Mathf.RoundToInt(damage * splashFraction));
            Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (Enemy e in all)
            {
                if (e == null || e == target) continue;
                if (Vector3.Distance(e.transform.position, transform.position) > splashRadius) continue;
                ApplyHit(e, splashDamage);
            }
        }
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null && enemy == target)
        {
            ApplyHit(enemy, damage);

            if (splashRadius > 0f)
            {
                int splashDamage = Mathf.Max(1, Mathf.RoundToInt(damage * splashFraction));
                Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
                foreach (Enemy e in all)
                {
                    if (e == null || e == enemy) continue;
                    if (Vector3.Distance(e.transform.position, transform.position) > splashRadius) continue;
                    ApplyHit(e, splashDamage);
                }
            }

            Destroy(gameObject);
        }
    }

    void ApplyHit(Enemy e, int dmg)
    {
        e.TakeDamage(dmg, damageType);
        if (slowMultiplier < 1f && slowDuration > 0f)
            e.ApplySlow(slowMultiplier, slowDuration);
    }
}

