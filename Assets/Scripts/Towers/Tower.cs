using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Configuration")]
    public TowerData data;
    public TowerSlot slot;

    [Header("Runtime")]
    public float currentRange;
    public float currentFireRate;
    public int currentDamage;

    protected Transform firePoint;
    protected float fireTimer;
    protected Enemy targetEnemy;

    public virtual void Initialize(TowerData towerData, TowerSlot towerSlot)
    {
        data = towerData;
        slot = towerSlot;

        // Apply base stats (skill tree buffs applied on top)
        currentRange = data.range;
        currentFireRate = data.fireRate;
        currentDamage = data.damage;

        ApplySkillTreeBuffs();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.WhiteSquare;
            if (data.sprite == null) sr.color = GetTowerColor(data.towerType);
            sr.sortingOrder = 4;
        }

        firePoint = transform.Find("FirePoint");
        if (firePoint == null)
            firePoint = transform;
    }

    public static Color GetTowerColor(TowerType type)
    {
        switch (type)
        {
            case TowerType.Rapid:    return new Color(0.2f, 0.8f, 0.9f); // cyan
            case TowerType.Balanced: return new Color(0.3f, 0.6f, 1.0f); // blue
            case TowerType.Sniper:   return new Color(0.8f, 0.3f, 0.8f); // purple
            case TowerType.Professor:return new Color(1.0f, 0.7f, 0.1f); // gold
            default:                 return Color.white;
        }
    }

    void ApplySkillTreeBuffs()
    {
        if (SkillTreeManager.Instance == null) return;
        BuffEffect buffs = SkillTreeManager.Instance.GetTotalBuffs();
        currentDamage = Mathf.RoundToInt(data.damage * buffs.damageMultiplier);
        currentRange = data.range * buffs.rangeMultiplier;
        currentFireRate = data.fireRate * buffs.fireRateMultiplier;
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        if (targetEnemy == null || !IsInRange(targetEnemy))
        {
            targetEnemy = FindClosestEnemy();
        }

        if (targetEnemy != null && fireTimer >= 1f / currentFireRate)
        {
            Shoot();
            fireTimer = 0f;
        }

        RotateTowardsTarget();
    }

    protected virtual void Shoot()
    {
        if (data.projectilePrefab == null || targetEnemy == null) return;

        GameObject projObj = Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(targetEnemy, currentDamage);
        }
    }

    Enemy FindClosestEnemy()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy closest = null;
        float closestDist = float.MaxValue;

        foreach (Enemy e in enemies)
        {
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist <= currentRange && dist < closestDist)
            {
                closestDist = dist;
                closest = e;
            }
        }
        return closest;
    }

    bool IsInRange(Enemy enemy)
    {
        if (enemy == null) return false;
        return Vector3.Distance(transform.position, enemy.transform.position) <= currentRange;
    }

    void RotateTowardsTarget()
    {
        if (targetEnemy == null) return;
        Vector3 dir = targetEnemy.transform.position - transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        float r = data != null ? data.range : currentRange;
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
