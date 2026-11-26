/*
 * FishercatNetShot
 * One thrown Fishercat net (Path 2, 2026-11-26), pooled by FishercatNetThrower (prefab Prefabs/Turrets/NetShot.prefab:
 * root with this, child `Net` (MeshFilter/Renderer, slot 0 = rope material) and child `Swirl` (the whirlpool, inactive)).
 *
 * - Flight: from the chest to the aim spot in an arc (arcHeight) over the stats' flightTime, spinning, opening from
 *   flightScale to the full radius.
 * - Landing: lies at the spot (rideHeight above the rats' feet, over their backs) and catches every rat the thrower
 *   CanCatch within the radius: Enemy.Stun(stunTime, or fatStunTime with force for fat rats) and spikeDamage (tier 3).
 *   Each rat is caught once per net.
 * - Tier 5 (persistTime > 0): the net stays that long on the spinning Swirl (wind streaks, sized to the pull's reach); rats the thrower can catch within radius x
 *   pullReach are pulled toward the middle (Enemy.Knockback steps at pullSpeed), and any that come under the net are
 *   caught.
 * - Its numbers are copied from the thrower's stats at launch (an upgrade bought mid-flight swaps the stats object).
 * - Without persistTime (tiers 1-4, user 2026-11-26) it catches as it lands and vanishes at once (fadeTime); with it, it
 *   stays persistTime (min lingerTime), then shrinks away over fadeTime and goes back to the pool.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatNetShot : MonoBehaviour
{
    [SerializeField] private MeshFilter net;
    [SerializeField] private MeshRenderer netRenderer;
    [SerializeField] private Transform swirl;
    [SerializeField, Min(0f)] private float arcHeight = 2.5f;
    [SerializeField, Range(0.05f, 1f)] private float flightScale = 0.25f;
    [SerializeField] private float flightSpin = 540f;
    [SerializeField] private float swirlSpin = 360f;
    [Tooltip("How far above the rats' feet the landed net lies.")]
    [SerializeField, Min(0f)] private float rideHeight = 0.3f;
    [SerializeField, Min(0f)] private float lingerTime = 0.6f;
    [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;
    [Tooltip("How fast a net without the whirlpool vanishes once it lands.")]
    [SerializeField, Min(0.01f)] private float vanishTime = 0.06f;

    private enum Phase { Flying, Down, Fading }

    private FishercatNetThrower owner;
    private ComponentPool<FishercatNetShot> pool;
    // The thrower's numbers copied at launch (an upgrade bought mid-flight replaces the thrower's stats object).
    private bool active;
    private float radius, flightTime, stunTime, fatStunTime, spikeDamage, persistTime, pullReach, pullSpeed;
    private Phase phase;
    private float timer;
    private float downTime;
    private Vector3 from;
    private Vector3 to;
    private readonly HashSet<Enemy> caught = new HashSet<Enemy>();
    private Material[] baseMaterials;

    /// <summary>Rats this net caught (testing).</summary>
    public int Caught => caught.Count;

    private void Awake()
    {
        if (netRenderer != null)
        {
            baseMaterials = netRenderer.sharedMaterials;
        }
    }

    public void Launch(FishercatNetThrower thrower, ComponentPool<FishercatNetShot> source, Vector3 start, Vector3 target, Mesh mesh, Material rope)
    {
        owner = thrower;
        pool = source;
        FishercatNetStats stats = thrower.Stats;
        radius = stats.Radius;
        flightTime = stats.FlightTime;
        stunTime = stats.StunTime;
        fatStunTime = stats.CatchesFat ? stats.FatStunTime : 0f;
        spikeDamage = stats.SpikeDamage;
        persistTime = stats.PersistTime;
        pullReach = stats.PullReach;
        pullSpeed = stats.PullSpeed;
        active = true;
        from = start;
        to = target + Vector3.up * rideHeight;
        caught.Clear();
        phase = Phase.Flying;
        timer = 0f;
        if (net != null && mesh != null)
        {
            net.sharedMesh = mesh;
        }
        if (netRenderer != null && baseMaterials != null && rope != null)
        {
            Material[] materials = (Material[])baseMaterials.Clone();
            materials[0] = rope;
            netRenderer.sharedMaterials = materials;
        }
        if (swirl != null)
        {
            swirl.gameObject.SetActive(false);
        }
        transform.position = from;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one * (radius * flightScale);
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }
        float dt = Time.deltaTime;
        timer += dt;
        switch (phase)
        {
            case Phase.Flying:
            {
                float u = Mathf.Clamp01(timer / flightTime);
                transform.position = Vector3.Lerp(from, to, u) + Vector3.up * (arcHeight * 4f * u * (1f - u));
                transform.rotation = Quaternion.Euler(0f, flightSpin * timer, 0f);
                transform.localScale = Vector3.one * (radius * Mathf.Lerp(flightScale, 1f, u * u));
                if (u >= 1f)
                {
                    Land();
                }
                break;
            }
            case Phase.Down:
            {
                if (persistTime > 0f && timer <= persistTime)
                {
                    Pull(dt);
                }
                if (swirl != null && swirl.gameObject.activeSelf)
                {
                    swirl.Rotate(0f, swirlSpin * dt, 0f, Space.World);
                    if (timer > persistTime)
                    {
                        swirl.gameObject.SetActive(false);
                    }
                }
                if (timer >= downTime)
                {
                    phase = Phase.Fading;
                    timer = 0f;
                }
                break;
            }
            case Phase.Fading:
            {
                float u = Mathf.Clamp01(timer / (persistTime > 0f ? fadeTime : vanishTime));
                transform.localScale = Vector3.one * (radius * (1f - u));
                if (u >= 1f)
                {
                    active = false;
                    pool.Release(this);
                }
                break;
            }
        }
    }

    private void Land()
    {
        phase = Phase.Down;
        timer = 0f;
        transform.position = to;
        transform.localScale = Vector3.one * radius;
        // Without the tier 5 whirlpool the net is gone as it lands (user, 2026-11-26): catch, then a quick vanish.
        downTime = persistTime > 0f ? Mathf.Max(lingerTime, persistTime) : 0f;
        if (persistTime > 0f && swirl != null)
        {
            swirl.gameObject.SetActive(true);
            swirl.localScale = Vector3.one * pullReach;
            swirl.position = new Vector3(to.x, to.y - rideHeight + 0.02f, to.z);
        }
        CatchUnder();
    }

    // Catches every catchable rat under the net that wasn't caught yet.
    private void CatchUnder()
    {
        float r2 = radius * radius;
        List<Enemy> enemies = Enemy.Active;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (caught.Contains(enemy) || !owner.CanCatch(enemy))
            {
                continue;
            }
            Vector3 p = enemy.transform.position;
            if (new Vector2(p.x - to.x, p.z - to.z).sqrMagnitude > r2)
            {
                continue;
            }
            bool fat = enemy.Stats != null && enemy.Stats.StunImmune;
            if (fat && fatStunTime <= 0f)
            {
                continue;
            }
            caught.Add(enemy);
            enemy.Stun(fat ? fatStunTime : stunTime, fat);
            if (spikeDamage > 0f)
            {
                enemy.TakeDamage(spikeDamage);
            }
        }
    }

    // Tier 5: pulls catchable rats near the net toward its middle; those that come under it are caught.
    private void Pull(float dt)
    {
        float reach = radius * pullReach;
        List<Enemy> enemies = Enemy.Active;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (!owner.CanCatch(enemy))
            {
                continue;
            }
            Vector3 p = enemy.transform.position;
            Vector2 toMiddle = new Vector2(to.x - p.x, to.z - p.z);
            float distance = toMiddle.magnitude;
            if (distance > reach || distance < 0.15f)
            {
                continue;
            }
            enemy.Knockback(toMiddle, Mathf.Min(distance, pullSpeed * dt * 2f), dt * 2f);
        }
        CatchUnder();
    }
}
