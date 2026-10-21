/*
 * PlayerHitFeedback
 * On the Player: when PlayerHealth takes damage the player model flashes red and jitters.
 *
 * - model is the child holding the player's renderers (the root carries the Rigidbody, so the jitter moves
 *   the child, never the root). Its local position is restored when the jitter ends.
 * - Flash: each material's _BaseColor is tinted toward flashColor through per-material MaterialPropertyBlocks
 *   (same approach as HitFlash; materials are never changed) and fades out linearly over flashDuration.
 *   flashIntensity 1 = pure flashColor at the start.
 * - Jitter: random flat (XZ) offset of up to jitterAmplitude each frame, shrinking to zero over jitterDuration.
 * - Only hits that deal damage trigger it, so hits ignored during invulnerability do nothing.
 */

using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerHitFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Transform model;
    [SerializeField] private Color flashColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField, Range(0f, 1f)] private float flashIntensity = 0.85f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float jitterDuration = 0.2f;
    [SerializeField, Min(0f)] private float jitterAmplitude = 0.12f;

    private PlayerHealth health;
    private Renderer[] renderers;
    private Color[][] baseColors;
    private MaterialPropertyBlock block;
    private Vector3 modelRestPosition;
    private float flashRemaining;
    private float jitterRemaining;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        block = new MaterialPropertyBlock();

        Transform root = model != null ? model : transform;
        modelRestPosition = model != null ? model.localPosition : Vector3.zero;
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
    }

    private void OnDisable()
    {
        Damageable.Damaged -= HandleDamaged;
        flashRemaining = 0f;
        jitterRemaining = 0f;
        ClearFlash();
        ResetModel();
    }

    private void HandleDamaged(Damageable target, float amount)
    {
        if (target != health || amount <= 0f)
        {
            return;
        }

        flashRemaining = flashDuration;
        jitterRemaining = jitterDuration;
    }

    private void Update()
    {
        if (flashRemaining > 0f)
        {
            flashRemaining -= Time.deltaTime;
            if (flashRemaining > 0f)
            {
                ApplyFlash(flashIntensity * Mathf.Clamp01(flashRemaining / flashDuration));
            }
            else
            {
                ClearFlash();
            }
        }

        if (jitterRemaining > 0f)
        {
            jitterRemaining -= Time.deltaTime;
            if (jitterRemaining > 0f)
            {
                Vector2 random = Random.insideUnitCircle * (jitterAmplitude * Mathf.Clamp01(jitterRemaining / jitterDuration));
                if (model != null)
                {
                    model.localPosition = modelRestPosition + new Vector3(random.x, 0f, random.y);
                }
            }
            else
            {
                ResetModel();
            }
        }
    }

    private void ResetModel()
    {
        if (model != null)
        {
            model.localPosition = modelRestPosition;
        }
    }

    private void ApplyFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int m = 0; m < baseColors[i].Length; m++)
            {
                if (baseColors[i][m].a <= 0f)
                {
                    continue;
                }

                Color tinted = Color.Lerp(baseColors[i][m], flashColor, amount);
                tinted.a = baseColors[i][m].a;
                renderers[i].GetPropertyBlock(block, m);
                block.SetColor(BaseColorId, tinted);
                renderers[i].SetPropertyBlock(block, m);
            }
        }
    }

    private void ClearFlash()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int m = 0; m < baseColors[i].Length; m++)
            {
                renderers[i].SetPropertyBlock(null, m);
            }
        }
    }
}
