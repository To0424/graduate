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

    public void Initialize(Enemy targetEnemy, int dmg, DamageType type = DamageType.Normal,
                           float splashR = 0f, float splashFrac = 0.6f,
                           float slowMul = 1f, float slowDur = 0f)
    {
        target         = targetEnemy;
        damage         = dmg;
        damageType     = type;
        splashRadius   = splashR;
        splashFraction = splashFrac;
        slowMultiplier = slowMul;
        slowDuration   = slowDur;

        // Ensure visible
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
        {
            sr.sprite = RuntimeSprite.Circle;
            if (slowMul < 1f && slowDur > 0f)        sr.color = new Color(0.55f, 0.85f, 1f); // ice
            else if (splashR > 0f)                    sr.color = new Color(1f, 0.5f, 0.1f); // cannon
            else if (type == DamageType.Pierce)       sr.color = new Color(1f, 0.5f, 0f);
            else                                      sr.color = Color.yellow;
        }
    }

    void Update()
    {
        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime) { Destroy(gameObject); return; }
        if (target == null)          { Destroy(gameObject); return; }

        Vector3 direction = (target.transform.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
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
