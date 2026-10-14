/*
 * LingerPuddle
 * Pooled ground circle left by a thrown weapon (ThrowLWeapon). For its lifetime it damages every target
 * inside its radius on a fixed interval, then fades out over the last part of its life and returns to
 * the pool.
 *
 * - Radius, damage interval and color are read live from the ThrowLStats asset each frame; only the
 *   lifetime is fixed when the puddle starts.
 * - The first damage tick lands immediately (the explosion), then every lingerDamageInterval.
 * - Each tick deals stats.DamageFor(category) once per target inside, for categories the stats allow.
 * - Drawn as a flat disc (AreaWeapon's mesh) with a transparent tinted material; alpha = opacity.
 */

using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LingerPuddle : MonoBehaviour
{
    private const float FadeFraction = 0.25f;

    [SerializeField] private Material material;
    [SerializeField, Min(0f)] private float groundHeight = 0.04f;

    private ComponentPool<LingerPuddle> pool;
    private ThrowLStats stats;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private float lifetime;
    private float age;
    private float tickTimer;

    private void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = AreaWeapon.GetDiscMesh();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        block = new MaterialPropertyBlock();
    }

    public void Begin(ComponentPool<LingerPuddle> owningPool, ThrowLStats puddleStats, Vector3 center)
    {
        pool = owningPool;
        stats = puddleStats;
        lifetime = puddleStats.Lifetime;
        age = 0f;
        tickTimer = 0f;
        transform.SetPositionAndRotation(new Vector3(center.x, groundHeight, center.z), Quaternion.identity);
        Apply(1f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            pool.Release(this);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer += Mathf.Max(stats.LingerDamageInterval, 0.01f);
            DamageInside();
        }

        float fadeStart = lifetime * (1f - FadeFraction);
        float fade = age > fadeStart ? 1f - (age - fadeStart) / (lifetime - fadeStart) : 1f;
        Apply(fade);
    }

    private void Apply(float fade)
    {
        transform.localScale = new Vector3(stats.Radius, 1f, stats.Radius);

        Color color = stats.LingerColor;
        color.a *= fade;
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(block);
    }

    private void DamageInside()
    {
        Vector3 center = transform.position;
        float sqrRadius = stats.Radius * stats.Radius;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (!stats.CanTarget(candidate.Category))
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRadius)
            {
                continue;
            }

            float amount = stats.DamageFor(candidate.Category);
            if (amount > 0f)
            {
                candidate.TakeDamage(amount);
            }
        }
    }
}
