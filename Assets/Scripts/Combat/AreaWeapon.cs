/*
 * AreaWeapon
 * AOE weapon: a filled zone on the ground around the owner that damages targets inside it every
 * 1 / attacksPerSecond seconds.
 *
 * - All numbers, the fill color and the target cap come from an AreaStats asset.
 * - Every target in a category the stats allow (enemies, resources, ...) within radius takes that
 *   category's damage on each attack, or only the nearest maxTargets when the cap is 1 or more
 *   (-1 = all). It attacks on a fixed interval whether or not anyone is inside.
 * - The zone is drawn as a flat disc mesh tinted by the stats color (alpha = opacity) and follows
 *   the owner. It is part of this weapon's object, so it only appears once the weapon is spawned
 *   (i.e. picked up).
 * - Needs a MeshFilter, MeshRenderer and a transparent zone material (Sprites/Default tints by color).
 * - Radius is read every frame, so editing the stats asset in Play mode resizes the zone live.
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AreaWeapon : WeaponBase<AreaStats>
{
    private const int Segments = 64;
    private const float FlashBoost = 0.3f;

    private static Mesh discMesh;

    [SerializeField] private Material zoneMaterial;
    [SerializeField, Min(0f)] private float groundHeight = 0.04f;

    private readonly List<Damageable> hits = new List<Damageable>();
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private float flashTimer;

    protected override void Awake()
    {
        base.Awake();

        GetComponent<MeshFilter>().sharedMesh = GetDiscMesh();

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = zoneMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        block = new MaterialPropertyBlock();
    }

    protected override void Tick()
    {
        flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);

        if (IsReady)
        {
            Attack();
            StartCooldown();
        }
    }

    private void LateUpdate()
    {
        Vector3 center = Owner.position;
        transform.SetPositionAndRotation(new Vector3(center.x, groundHeight, center.z), Quaternion.identity);
        transform.localScale = new Vector3(Stats.Radius, 1f, Stats.Radius);

        Color color = Stats.Color;
        if (flashTimer > 0f && Stats.FlashDuration > 0f)
        {
            color.a = Mathf.Min(1f, color.a + FlashBoost * flashTimer / Stats.FlashDuration);
        }

        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(block);
    }

    private void Attack()
    {
        Vector3 center = Owner.position;
        float sqrRadius = Stats.Radius * Stats.Radius;
        hits.Clear();

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!Stats.CanTarget(candidate.Category))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= sqrRadius)
            {
                hits.Add(candidate);
            }
        }

        int limit = Stats.MaxTargets;
        if (limit >= 0 && hits.Count > limit)
        {
            KeepNearest(hits, limit, center);
        }

        for (int i = 0; i < hits.Count; i++)
        {
            float amount = Stats.DamageFor(hits[i].Category);
            if (amount > 0f)
            {
                hits[i].TakeDamage(amount);
            }
        }

        flashTimer = Stats.FlashDuration;
    }

    private static void KeepNearest(List<Damageable> list, int count, Vector3 center)
    {
        for (int k = 0; k < count; k++)
        {
            int best = k;
            float bestSqr = FlatSqrDistance(list[k], center);

            for (int i = k + 1; i < list.Count; i++)
            {
                float sqr = FlatSqrDistance(list[i], center);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            (list[k], list[best]) = (list[best], list[k]);
        }

        list.RemoveRange(count, list.Count - count);
    }

    private static float FlatSqrDistance(Damageable target, Vector3 center)
    {
        Vector3 offset = target.transform.position - center;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private static Mesh GetDiscMesh()
    {
        if (discMesh != null)
        {
            return discMesh;
        }

        Vector3[] vertices = new Vector3[Segments + 1];
        Vector3[] normals = new Vector3[Segments + 1];
        int[] triangles = new int[Segments * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            normals[i + 1] = Vector3.up;

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % Segments + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        discMesh = new Mesh { name = "AreaDisc", vertices = vertices, normals = normals, triangles = triangles };
        discMesh.RecalculateBounds();
        return discMesh;
    }
}
