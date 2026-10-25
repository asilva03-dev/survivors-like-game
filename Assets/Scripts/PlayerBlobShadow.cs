/*
 * PlayerBlobShadow
 * On the Player: a soft dark circle on the surface under the player (the ground or a wall top) that shrinks and fades
 * the higher the player's feet are above it, so jumps read clearly. Visual only.
 *
 * - Builds its own root-level object `PlayerShadow` on Awake (a flat quad with a generated radial-falloff texture and
 *   a Sprites/Default material, no shadows), so the hop/jitter/facing animations never move it.
 * - Each LateUpdate a ray goes down from the player's centre onto surfaceMask; hits on the player itself and on
 *   Damageables (trees, enemies, the base) are skipped. The circle sits lift above the hit point.
 * - Height = feet (PlayerController.FeetHeight, or the root minus feetOffset) above that surface: at 0 the circle is
 *   `size` wide at `maxAlpha`; at fadeHeight and above it is size * minScale wide at minAlpha. No surface = hidden.
 * - size is wider than the cat on purpose: the camera looks down at 68 deg, so the cat hides a smaller circle.
 *   Changing softness in Play rebuilds the texture (OnValidate).
 */

using UnityEngine;

public class PlayerBlobShadow : MonoBehaviour
{
    private const int TextureSize = 64;
    private const float RayLength = 20f;

    [Tooltip("Width on the ground. Wider than the cat (~1.7) so a ring shows around it from the tilted camera.")]
    [SerializeField, Min(0.1f)] private float size = 2.1f;
    [SerializeField, Range(0.05f, 1f)] private float minScale = 0.5f;
    [Tooltip("Feet height above the surface at which the circle is smallest and faintest.")]
    [SerializeField, Min(0.1f)] private float fadeHeight = 1.8f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.55f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;
    [SerializeField] private Color color = Color.black;
    [Tooltip("0 = hard edge, 1 = fades from the centre.")]
    [SerializeField, Range(0f, 1f)] private float softness = 0.35f;
    [SerializeField, Min(0f)] private float lift = 0.03f;
    [SerializeField] private LayerMask surfaceMask = (1 << 0) | (1 << 9);
    [Tooltip("Used when there is no PlayerController: distance from the root down to the feet.")]
    [SerializeField, Min(0f)] private float feetOffset = 1f;

    private readonly RaycastHit[] hits = new RaycastHit[8];
    private Transform shadow;
    private MeshRenderer shadowRenderer;
    private MaterialPropertyBlock block;
    private PlayerController controller;
    private Rigidbody body;

    public float SurfaceHeight { get; private set; }
    public float CurrentScale { get; private set; }
    public float CurrentAlpha { get; private set; }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
        block = new MaterialPropertyBlock();
        BuildShadow();
    }

    // Softness edits in Play rebuild the circle texture.
    private void OnValidate()
    {
        if (Application.isPlaying && shadowRenderer != null)
        {
            Texture old = shadowRenderer.sharedMaterial.mainTexture;
            shadowRenderer.sharedMaterial.mainTexture = BuildTexture();
            if (old != null)
            {
                Destroy(old);
            }
        }
    }

    private void OnDestroy()
    {
        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }
    }

    private void OnDisable()
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        float surface;
        if (!FindSurface(out surface))
        {
            shadowRenderer.enabled = false;
            return;
        }

        float feet = controller != null ? controller.FeetHeight : transform.position.y - feetOffset;
        float t = Mathf.Clamp01((feet - surface) / fadeHeight);
        CurrentScale = size * Mathf.Lerp(1f, minScale, t);
        CurrentAlpha = Mathf.Lerp(maxAlpha, minAlpha, t);
        SurfaceHeight = surface;

        Vector3 position = transform.position;
        shadow.position = new Vector3(position.x, surface + lift, position.z);
        shadow.localScale = new Vector3(CurrentScale, CurrentScale, 1f);
        Color tint = color;
        tint.a = CurrentAlpha;
        block.SetColor("_Color", tint);
        shadowRenderer.SetPropertyBlock(block);
        shadowRenderer.enabled = true;
    }

    // Highest surface below the player's centre, ignoring the player and anything with a Damageable.
    private bool FindSurface(out float surface)
    {
        surface = 0f;
        int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, hits, RayLength, surfaceMask,
            QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i].collider;
            if ((body != null && hit.attachedRigidbody == body) || hit.transform.IsChildOf(transform)
                || hit.GetComponentInParent<Damageable>() != null)
            {
                continue;
            }
            if (!found || hits[i].point.y > surface)
            {
                surface = hits[i].point.y;
                found = true;
            }
        }
        return found;
    }

    private void BuildShadow()
    {
        GameObject go = new GameObject("PlayerShadow");
        shadow = go.transform;
        shadow.rotation = Quaternion.Euler(90f, 0f, 0f);

        Mesh mesh = new Mesh { name = "PlayerShadowQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        shadowRenderer = go.AddComponent<MeshRenderer>();
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        Material material = new Material(Shader.Find("Sprites/Default")) { name = "PlayerShadow" };
        material.mainTexture = BuildTexture();
        shadowRenderer.sharedMaterial = material;
    }

    // White circle whose alpha falls off toward the edge (softness sets where the fall-off starts).
    private Texture2D BuildTexture()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "PlayerShadowCircle",
            wrapMode = TextureWrapMode.Clamp
        };
        float inner = 1f - softness;
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = (x + 0.5f) / TextureSize * 2f - 1f;
                float dy = (y + 0.5f) / TextureSize * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = r >= 1f ? 0f : r <= inner ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (r - inner) / Mathf.Max(softness, 0.0001f));
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
