/*
 * FishercatLooks
 * Shows the Fishercat's upgrade looks (2026-11-26, upgrade pass): on the prefab root, listens to Turret.Upgraded and
 * puts every listed part back to its prefab state, then applies each entry whose path has reached its tier.
 *
 * - Every list is filled in the inspector; paths are numbered 1-3 like the upgrade panel, tiers 1-5. Entries apply in
 *   list order, so a later entry wins over an earlier one on the same part (e.g. tier 5's shark gun over tier 3's gun).
 * - parts: show or hide an object (Path 1: tier 3 gun + case instead of the barrel, tier 4 cooler + big spear, tier 5
 *   aviators). meshes: swap a MeshFilter's mesh (tier 5 shark camo gun). scales: set a local scale (tier 5 bigger
 *   cooler). hats: the hat and band materials on the body renderer's hat slots (tier 2 red hat).
 * - Only looks: stats come from the tier assets (Turret), animation from FishercatAnimator (it reads the gun's state).
 * - (2026-11-26, Path 2) materials: swap one material slot of a renderer (tier 3 red net chest). poses: set a local
 *   position + rotation (the net chest moves to the dock's back-left corner while Path 1 tier 4's cooler is there).
 * - Hats (user, 2026-11-26, temp rule): when two paths each have a reached hat look, the path with the higher tier wins;
 *   at a tie the most recently upgraded path wins. Within one path the later entry wins.
 */

using System.Collections.Generic;
using UnityEngine;

public class FishercatLooks : MonoBehaviour
{
    [System.Serializable]
    public class PartLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public GameObject part;
        public bool active = true;
    }

    [System.Serializable]
    public class MeshLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public MeshFilter target;
        public Mesh mesh;
    }

    [System.Serializable]
    public class ScaleLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public Transform target;
        public Vector3 scale = Vector3.one;
    }

    [System.Serializable]
    public class HatLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public Material hat;
        public Material band;
    }

    [System.Serializable]
    public class MaterialLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public Renderer target;
        public int slot;
        public Material material;
    }

    [System.Serializable]
    public class PoseLook
    {
        [Range(1, 3)] public int path = 1;
        [Range(1, 5)] public int tier = 1;
        public Transform target;
        public Vector3 position;
        public Vector3 rotation;
    }

    [SerializeField] private Turret turret;
    [Tooltip("The cat's body renderer; its hat and band material slots change with the hat looks.")]
    [SerializeField] private MeshRenderer body;
    [SerializeField] private int hatSlot = 3;
    [SerializeField] private int bandSlot = 4;
    [SerializeField] private List<PartLook> parts = new List<PartLook>();
    [SerializeField] private List<MeshLook> meshes = new List<MeshLook>();
    [SerializeField] private List<ScaleLook> scales = new List<ScaleLook>();
    [SerializeField] private List<HatLook> hats = new List<HatLook>();
    [SerializeField] private List<MaterialLook> materialLooks = new List<MaterialLook>();
    [SerializeField] private List<PoseLook> poses = new List<PoseLook>();

    private readonly Dictionary<GameObject, bool> baseActive = new Dictionary<GameObject, bool>();
    private readonly Dictionary<MeshFilter, Mesh> baseMesh = new Dictionary<MeshFilter, Mesh>();
    private readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();
    private Material[] baseBodyMaterials;
    private readonly Dictionary<Renderer, Material[]> baseRendererMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Transform, Pose> basePose = new Dictionary<Transform, Pose>();
    private int[] lastTiers = new int[3];
    private int lastUpgradedPath = -1;

    private void Awake()
    {
        foreach (PartLook look in parts)
        {
            if (look.part != null && !baseActive.ContainsKey(look.part))
            {
                baseActive[look.part] = look.part.activeSelf;
            }
        }
        foreach (MeshLook look in meshes)
        {
            if (look.target != null && !baseMesh.ContainsKey(look.target))
            {
                baseMesh[look.target] = look.target.sharedMesh;
            }
        }
        foreach (ScaleLook look in scales)
        {
            if (look.target != null && !baseScale.ContainsKey(look.target))
            {
                baseScale[look.target] = look.target.localScale;
            }
        }
        if (body != null)
        {
            baseBodyMaterials = body.sharedMaterials;
        }
        foreach (MaterialLook look in materialLooks)
        {
            if (look.target != null && !baseRendererMaterials.ContainsKey(look.target))
            {
                baseRendererMaterials[look.target] = look.target.sharedMaterials;
            }
        }
        foreach (PoseLook look in poses)
        {
            if (look.target != null && !basePose.ContainsKey(look.target))
            {
                basePose[look.target] = new Pose(look.target.localPosition, look.target.localRotation);
            }
        }
    }

    private void OnEnable()
    {
        if (turret != null)
        {
            turret.Upgraded += Apply;
        }
        Apply();
    }

    private void OnDisable()
    {
        if (turret != null)
        {
            turret.Upgraded -= Apply;
        }
    }

    // The path (1-3) whose hat shows: among paths with a reached hat look, the highest tier; a tie goes to the path
    // upgraded most recently. 0 = none.
    private int HatPath()
    {
        int best = 0;
        int bestTier = 0;
        foreach (HatLook look in hats)
        {
            if (!Reached(look.path, look.tier))
            {
                continue;
            }
            int tier = turret.Tier(look.path - 1);
            if (best == 0 || tier > bestTier || (tier == bestTier && look.path - 1 == lastUpgradedPath))
            {
                best = look.path;
                bestTier = tier;
            }
        }
        return best;
    }

    private bool Reached(int path, int tier)
    {
        return turret != null && turret.Tier(path - 1) >= tier;
    }

    /// <summary>Puts every listed part back to the prefab's look, then applies each reached tier's look in list order.</summary>
    public void Apply()
    {
        // Which path was just upgraded (for the hat tie rule).
        for (int p = 0; p < lastTiers.Length; p++)
        {
            int now = turret != null ? turret.Tier(p) : 0;
            if (now > lastTiers[p])
            {
                lastUpgradedPath = p;
            }
            lastTiers[p] = now;
        }
        foreach (KeyValuePair<Renderer, Material[]> pair in baseRendererMaterials)
        {
            pair.Key.sharedMaterials = pair.Value;
        }
        foreach (KeyValuePair<Transform, Pose> pair in basePose)
        {
            pair.Key.localPosition = pair.Value.position;
            pair.Key.localRotation = pair.Value.rotation;
        }
        foreach (KeyValuePair<GameObject, bool> pair in baseActive)
        {
            pair.Key.SetActive(pair.Value);
        }
        foreach (KeyValuePair<MeshFilter, Mesh> pair in baseMesh)
        {
            pair.Key.sharedMesh = pair.Value;
        }
        foreach (KeyValuePair<Transform, Vector3> pair in baseScale)
        {
            pair.Key.localScale = pair.Value;
        }
        Material[] materials = baseBodyMaterials != null ? (Material[])baseBodyMaterials.Clone() : null;

        foreach (PartLook look in parts)
        {
            if (look.part != null && Reached(look.path, look.tier))
            {
                look.part.SetActive(look.active);
            }
        }
        foreach (MeshLook look in meshes)
        {
            if (look.target != null && Reached(look.path, look.tier))
            {
                look.target.sharedMesh = look.mesh;
            }
        }
        foreach (ScaleLook look in scales)
        {
            if (look.target != null && Reached(look.path, look.tier))
            {
                look.target.localScale = look.scale;
            }
        }
        foreach (MaterialLook look in materialLooks)
        {
            if (look.target != null && look.material != null && Reached(look.path, look.tier))
            {
                Material[] slots = look.target.sharedMaterials;
                if (look.slot >= 0 && look.slot < slots.Length)
                {
                    slots[look.slot] = look.material;
                    look.target.sharedMaterials = slots;
                }
            }
        }
        foreach (PoseLook look in poses)
        {
            if (look.target != null && Reached(look.path, look.tier))
            {
                look.target.localPosition = look.position;
                look.target.localRotation = Quaternion.Euler(look.rotation);
            }
        }
        if (materials != null)
        {
            int hatPath = HatPath();
            foreach (HatLook look in hats)
            {
                if (look.path != hatPath || !Reached(look.path, look.tier))
                {
                    continue;
                }
                if (look.hat != null && hatSlot < materials.Length)
                {
                    materials[hatSlot] = look.hat;
                }
                if (look.band != null && bandSlot < materials.Length)
                {
                    materials[bandSlot] = look.band;
                }
            }
            body.sharedMaterials = materials;
        }
    }
}
