/*
 * Enemy
 * Temporary placeholder enemy: stationary, has health, dies by disabling itself.
 *
 * - Active holds every enabled enemy so weapons can find targets without scene searches.
 * - Damaged (enemy, amount) is raised on every hit, before death is handled, so killing blows are
 *   reported too. DamageNumberSpawner listens to it.
 * - Died is raised just before the object is disabled; EnemySpawner listens to it and returns the
 *   enemy to its pool.
 * - Health resets in OnEnable, so re-enabling an enemy gives it a fresh life.
 * - AimPoint is what weapons shoot at (the object's center).
 * - Rank (Normal / Elite / Boss) and Health (current HP) feed weapon target priority in
 *   WeaponLoadout. Rank is just a stub field for now; every enemy is Normal until real elites and
 *   bosses exist.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyRank { Normal, Elite, Boss }

public class Enemy : MonoBehaviour, IDamageable
{
    public static readonly List<Enemy> Active = new List<Enemy>();
    public static event Action<Enemy> Died;
    public static event Action<Enemy, float> Damaged;

    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private EnemyRank rank = EnemyRank.Normal;

    private float health;

    public Vector3 AimPoint => transform.position;
    public float Health => health;
    public EnemyRank Rank => rank;
    public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

    private void OnEnable()
    {
        health = maxHealth;
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        Damaged?.Invoke(this, amount);

        if (health > 0f)
        {
            return;
        }

        Died?.Invoke(this);
        gameObject.SetActive(false);
    }

    public static Enemy FindNearest(Vector3 from, float range)
    {
        Enemy nearest = null;
        float best = range * range;

        for (int i = 0; i < Active.Count; i++)
        {
            Vector3 offset = Active[i].transform.position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                nearest = Active[i];
            }
        }

        return nearest;
    }
}
