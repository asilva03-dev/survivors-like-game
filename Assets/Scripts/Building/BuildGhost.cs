/*
 * BuildGhost
 * The placement ghost of build mode (slice 6 step 4), on the BuildMode object.
 *
 * - While a defense is selected from the bar, a see-through copy of its model (DefensePreview.ModelCopy, no scripts)
 *   follows the mouse, snapped to the centre of the grid tile under the cursor (ground-plane ray at y 0). Its gun
 *   faces the way the turret would (Turret.FindRestDirection), and its white range ring is shown.
 * - Green (validColor) when TurretPlacer.CanBuild allows the tile, red (invalidColor) otherwise; the hint box shows
 *   the controls and the reason (BuildModeUI.ShowHint).
 * - Left click builds through TurretPlacer.TryBuild (spends the wood). The same defense stays selected so more can be
 *   placed; right click unselects (BuildMode). A refused click also shows the reason as a red shaking message.
 * - Ghost material: a tinted instance of `material` (GridDebug.mat, transparent, unlit) on every renderer.
 * - Test hooks: SetTestPoint(world or null) overrides the cursor, Confirm() acts like a left click; Visible, Valid,
 *   Reason, Cell, GhostPosition.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildGhost : MonoBehaviour
{
    public const string Controls = "Left click: build      Right click: cancel";

    [SerializeField] private BuildModeUI ui;
    [SerializeField] private Material material;
    [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.45f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.45f);
    [SerializeField, Min(0f)] private float lift = 0.02f;

    [Header("Range ring")]
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField, Min(0.005f)] private float ringWidth = 0.06f;
    [SerializeField, Range(16, 256)] private int ringSegments = 96;

    private BuildMode buildMode;
    private Camera cam;
    private Transform root;
    private GameObject model;
    private Transform gun;
    private Material ghostMaterial;
    private LineRenderer ring;
    private TurretDefinition shownDefense;
    private float shownRange = -1f;
    private int selectedFrame = -10;
    private Vector3? testPoint;

    public bool Visible => root != null && root.gameObject.activeSelf;
    public bool Valid { get; private set; }
    public string Reason { get; private set; }
    public Vector2Int Cell { get; private set; }
    public Vector3 GhostPosition => root != null ? root.position : Vector3.zero;

    private void Awake()
    {
        buildMode = GetComponent<BuildMode>();
        if (ui == null)
        {
            ui = GetComponent<BuildModeUI>();
        }

        root = new GameObject("BuildGhost").transform;
        ghostMaterial = new Material(material != null ? material : new Material(Shader.Find("Sprites/Default")));

        GameObject ringObject = new GameObject("RangeRing");
        ringObject.transform.SetParent(root, false);
        ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringObject.transform.localPosition = new Vector3(0f, 0.03f, 0f); // above the strip highlight
        ring = ringObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = ringMaterial != null ? ringMaterial : new Material(Shader.Find("Sprites/Default"));
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        root.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (buildMode != null)
        {
            buildMode.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnDisable()
    {
        if (buildMode != null)
        {
            buildMode.SelectionChanged -= OnSelectionChanged;
        }
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root.gameObject);
        }
        Destroy(ghostMaterial);
    }

    private void Update()
    {
        TurretDefinition defense = buildMode != null && buildMode.IsOpen ? buildMode.Selected : null;
        Vector3 point;
        if (defense == null || !TryGetPoint(out point))
        {
            Hide();
            return;
        }

        if (defense != shownDefense)
        {
            Rebuild(defense);
        }

        BuildGrid grid = BuildGrid.Instance;
        Cell = grid != null ? grid.WorldToCell(point) : Vector2Int.zero;
        Vector3 position = grid != null ? grid.CellToWorld(Cell) : new Vector3(point.x, 0f, point.z);
        root.position = position + Vector3.up * lift;
        root.gameObject.SetActive(true);

        string reason = null;
        Valid = TurretPlacer.Instance != null && TurretPlacer.Instance.CanBuild(defense, Cell, out reason);
        Reason = Valid ? null : reason;
        ghostMaterial.color = Valid ? validColor : invalidColor;
        if (gun != null)
        {
            gun.rotation = Quaternion.LookRotation(grid != null && grid.GetState(Cell) == CellState.BuildStrip
                ? Turret.FindRestDirection(Cell)
                : Vector3.forward);
        }
        UpdateRing(defense);

        if (ui != null)
        {
            ui.ShowHint(Controls, Valid ? "Ready to build (" + defense.WoodCost + " wood)" : Reason, Valid);
        }

        Mouse mouse = Mouse.current;
        if (testPoint == null && mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
        {
            Confirm();
        }
    }

    // Acts like a left click on the current tile. The click that picked the defense never also builds.
    public bool Confirm()
    {
        if (!Visible || Time.frameCount - selectedFrame <= 1 || buildMode == null || buildMode.Selected == null)
        {
            return false;
        }

        string reason;
        if (TurretPlacer.Instance != null && TurretPlacer.Instance.TryBuild(buildMode.Selected, Cell, out reason))
        {
            return true;
        }

        if (ui != null && TurretPlacer.Instance != null)
        {
            TurretPlacer.Instance.CanBuild(buildMode.Selected, Cell, out reason);
            ui.ShowError(reason ?? TurretPlacer.NotEnoughWood);
        }
        return false;
    }

    public void SetTestPoint(Vector3? world)
    {
        testPoint = world;
    }

    private void OnSelectionChanged()
    {
        selectedFrame = Time.frameCount;
    }

    private void Hide()
    {
        if (root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(false);
        }
        if (ui != null)
        {
            ui.HideHint();
        }
    }

    private bool TryGetPoint(out Vector3 point)
    {
        if (testPoint.HasValue)
        {
            point = testPoint.Value;
            return true;
        }

        point = Vector3.zero;
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return false;
            }
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        float distance;
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance))
        {
            return false;
        }

        point = ray.GetPoint(distance);
        return true;
    }

    private void Rebuild(TurretDefinition defense)
    {
        if (model != null)
        {
            Destroy(model);
        }

        shownDefense = defense;
        shownRange = -1f;
        model = DefensePreview.ModelCopy(defense.Prefab.gameObject, root);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        gun = FindChild(model.transform, "Gun");

        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = ghostMaterial;
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void UpdateRing(TurretDefinition defense)
    {
        TurretGun turretGun = defense.Prefab.GetComponentInChildren<TurretGun>(true);
        float range = turretGun != null ? turretGun.Range : 0f;
        ring.enabled = range > 0f;
        ring.startWidth = ringWidth;
        ring.endWidth = ringWidth;
        ring.startColor = ringColor;
        ring.endColor = ringColor;
        if (Mathf.Approximately(range, shownRange))
        {
            return;
        }

        shownRange = range;
        ring.positionCount = ringSegments;
        for (int i = 0; i < ringSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / ringSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * range);
        }
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }
        return null;
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
