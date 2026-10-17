/*
 * HitFlash
 * On a Damageable: when it takes damage its model brightens toward white and fades back.
 *
 * - Tints each material's _BaseColor toward white through per-material MaterialPropertyBlocks (the
 *   materials themselves are never changed, so nothing is created and pooled enemies cannot come back
 *   tinted: the blocks are cleared when the flash ends or the object is disabled).
 * - intensity is how far toward white at the start of the flash (0 = none, 1 = pure white); the tint then
 *   fades out linearly over duration seconds. A hit during a flash restarts it.
 * - Renderers are the ones under the Enemy's Visual child, or under this object when it has no Enemy / Visual,
 *   so the health bar canvas is never tinted. Collected once in Awake. Materials without _BaseColor are skipped.
 * - Listens to Damageable.Damaged and ignores other targets and zero damage.
 */

using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class HitFlash : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField, Range(0f, 1f)] private float intensity = 0.45f;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;

    private Damageable damageable;
    private Renderer[] renderers;
    private Color[][] baseColors;
    private MaterialPropertyBlock block;
    private float remaining;
    private bool flashing;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        block = new MaterialPropertyBlock();

        Enemy enemy = damageable as Enemy;
        Transform root = enemy != null && enemy.Visual != null ? enemy.Visual : transform;
        renderers = root.GetComponentsInChildren<Renderer>(true);

        baseColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            baseColors[i] = new Color[materials.Length];
            for (int m = 0; m < materials.Length; m++)
            {
                baseColors[i][m] = materials[m] != null && materials[m].HasProperty(BaseColorId)
                    ? materials[m].GetColor(BaseColorId)
                    : Color.clear;
            }
        }
    }

    private void OnEnable()
    {
        Damageable.Damaged += HandleDamaged;
        remaining = 0f;
    }

    private void OnDisable()
    {
        Damageable.Damaged -= HandleDamaged;
        Restore();
    }

    private void HandleDamaged(Damageable target, float amount)
    {
        if (target != damageable || amount <= 0f)
        {
            return;
        }

        remaining = duration;
        flashing = true;
        Apply();
    }

    private void Update()
    {
        if (!flashing)
        {
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            Restore();
            return;
        }

        Apply();
    }

    private void Apply()
    {
        float amount = intensity * Mathf.Clamp01(remaining / duration);

        for (int i = 0; i < renderers.Length; i++)
        {
            for (int m = 0; m < baseColors[i].Length; m++)
            {
                if (baseColors[i][m].a <= 0f)
                {
                    continue;
                }

                Color tinted = Color.Lerp(baseColors[i][m], Color.white, amount);
                tinted.a = baseColors[i][m].a;
                renderers[i].GetPropertyBlock(block, m);
                block.SetColor(BaseColorId, tinted);
                renderers[i].SetPropertyBlock(block, m);
            }
        }
    }

    private void Restore()
    {
        if (!flashing)
        {
            return;
        }

        flashing = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int m = 0; m < baseColors[i].Length; m++)
            {
                renderers[i].SetPropertyBlock(null, m);
            }
        }
    }
}
