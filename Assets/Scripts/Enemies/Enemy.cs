using UnityEngine;
using System;

public class Enemy : MonoBehaviour
{
    [Header("Runtime Data")]
    public EnemyData data;
    public int currentHealth;
    public float moveSpeed;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    // HP bar
    private SpriteRenderer hpBarBg;
    private SpriteRenderer hpBarFill;
    private static readonly float HP_BAR_WIDTH = 0.6f;
    private static readonly float HP_BAR_HEIGHT = 0.08f;
    private static readonly float HP_BAR_OFFSET_Y = 0.45f;

    public static event Action<Enemy> OnEnemyDeath;
    public static event Action<Enemy> OnEnemyReachedExit;

    public void Initialize(EnemyData enemyData, Transform[] path)
    {
        data = enemyData;
        currentHealth = data.maxHealth;
        moveSpeed = data.moveSpeed;
        waypoints = path;
        currentWaypointIndex = 0;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = data.sprite != null ? data.sprite : RuntimeSprite.Circle;
            if (data.sprite == null) sr.color = Color.red;
        }

        CreateHPBar();

        if (waypoints.Length > 0)
            transform.position = waypoints[0].position;
    }

    void CreateHPBar()
    {
        // Background (dark)
        GameObject bgObj = new GameObject("HPBarBg");
        bgObj.transform.SetParent(transform);
        bgObj.transform.localPosition = new Vector3(0, HP_BAR_OFFSET_Y, 0);
        bgObj.transform.localRotation = Quaternion.identity;
        hpBarBg = bgObj.AddComponent<SpriteRenderer>();
        hpBarBg.sprite = RuntimeSprite.WhiteSquare;
        hpBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        hpBarBg.sortingOrder = 9;
        bgObj.transform.localScale = new Vector3(HP_BAR_WIDTH, HP_BAR_HEIGHT, 1f);

        // Fill (green → red based on HP)
        GameObject fillObj = new GameObject("HPBarFill");
        fillObj.transform.SetParent(transform);
        fillObj.transform.localPosition = new Vector3(0, HP_BAR_OFFSET_Y, 0);
        fillObj.transform.localRotation = Quaternion.identity;
        hpBarFill = fillObj.AddComponent<SpriteRenderer>();
        hpBarFill.sprite = RuntimeSprite.WhiteSquare;
        hpBarFill.color = Color.green;
        hpBarFill.sortingOrder = 10;
        fillObj.transform.localScale = new Vector3(HP_BAR_WIDTH, HP_BAR_HEIGHT, 1f);
    }

    void UpdateHPBar()
    {
        if (hpBarFill == null) return;
        float ratio = (float)currentHealth / data.maxHealth;
        ratio = Mathf.Clamp01(ratio);

        // Scale the fill bar
        hpBarFill.transform.localScale = new Vector3(HP_BAR_WIDTH * ratio, HP_BAR_HEIGHT, 1f);
        // Offset so it shrinks from right
        float offset = (HP_BAR_WIDTH * (1f - ratio)) * -0.5f;
        hpBarFill.transform.localPosition = new Vector3(offset, HP_BAR_OFFSET_Y, 0);

        // Color: green → yellow → red
        hpBarFill.color = Color.Lerp(Color.red, Color.green, ratio);

        // Keep HP bar rotation fixed (don't rotate with enemy)
        if (hpBarBg != null) hpBarBg.transform.rotation = Quaternion.identity;
        hpBarFill.transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        MoveAlongPath();
        UpdateHPBar();
    }

    void MoveAlongPath()
    {
        if (currentWaypointIndex >= waypoints.Length)
        {
            ReachExit();
            return;
        }

        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            currentWaypointIndex++;
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        CurrencyManager.Instance?.AddGold(data.goldReward);
        OnEnemyDeath?.Invoke(this);
        Destroy(gameObject);
    }

    void ReachExit()
    {
        LivesManager.Instance?.LoseLife(1);
        OnEnemyReachedExit?.Invoke(this);
        Destroy(gameObject);
    }

    public float GetDistanceToExit()
    {
        float dist = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        for (int i = currentWaypointIndex; i < waypoints.Length - 1; i++)
        {
            dist += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
        }
        return dist;
    }
}
