using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float maxLifetime = 5f;

    private Enemy target;
    private int damage;
    private float lifetime;

    public void Initialize(Enemy targetEnemy, int dmg)
    {
        target = targetEnemy;
        damage = dmg;

        // Ensure visible
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
        {
            sr.sprite = RuntimeSprite.Circle;
            sr.color = Color.yellow;
        }
    }

    void Update()
    {
        lifetime += Time.deltaTime;
        if (lifetime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direction = (target.transform.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // Rotate to face target
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null && enemy == target)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
