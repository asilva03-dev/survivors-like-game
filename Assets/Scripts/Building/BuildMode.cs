/*
 * BuildMode
 * The building state (slice 6). Tab (toggleKey) opens and closes it, but it only opens while the player is inside the
 * base light; pressing it in the dark shows a short red "Build only in the light" message instead. Leaving the light
 * or a preset draft opening closes it. Esc does nothing here (kept for a future pause menu).
 *
 * - While open it blocks the axe and the right-click dash every frame (PlayerInputGate); the player still walks,
 *   enemies keep coming and nothing pauses.
 * - categories: the build bar's tabs, each a name and a list of defenses (TurretDefinitions) shown as slots.
 * - Selected: the defense picked from the bar (its ghost, BuildGhost). Select() refuses a defense the player can't
 *   afford. Right click unselects (the bar slides back up); closing build mode unselects too.
 * - SelectedTurret (step 6): left-clicking a built turret while no defense is selected (and not over UI) selects it:
 *   UpgradePanelUI slides in and the turret shows its range ring. Turrets have no colliders, so the pick finds the
 *   turret whose upright body (ground to pickHeight) passes within pickRadius of the cursor ray. Right click closes
 *   it; picking a defense from the bar or closing build mode closes it too.
 * - Static IsActive / Instance; events OpenChanged(bool), SelectionChanged. BuildStripHighlight and TurretRadii show
 *   themselves while it is open; BuildModeUI shows the message and the bar.
 * - Test hooks: Open() / Close() / Toggle() (Open returns false with the reason), Select(), Deselect(), CanAfford(),
 *   SelectTurret(), DeselectTurret(), SellSelected(), TurretAtCursor(screenPoint).
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildMode : MonoBehaviour
{
    public const string DarkMessage = "Build only in the light";

    [Serializable]
    public class Category
    {
        public string name = "Turrets";
        public List<TurretDefinition> defenses = new List<TurretDefinition>();
    }

    [SerializeField] private Key toggleKey = Key.Tab;
    [SerializeField] private BuildModeUI ui;
    [SerializeField] private List<Category> categories = new List<Category>();
    [Tooltip("How close (units) the cursor ray must pass to a turret's body to select it.")]
    [SerializeField, Min(0.1f)] private float pickRadius = 0.9f;
    [SerializeField, Min(0.1f)] private float pickHeight = 2.4f;

    private PresetDraft draft;
    private bool selectedTurretWasSet;
    private ResourceInventory inventory;

    public static BuildMode Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.IsOpen;

    public event Action<bool> OpenChanged;
    public event Action SelectionChanged;
    public event Action TurretSelectionChanged;

    public bool IsOpen { get; private set; }
    public TurretDefinition Selected { get; private set; }
    public Turret SelectedTurret { get; private set; }
    public IReadOnlyList<Category> Categories => categories;
    public ResourceInventory Inventory => inventory != null ? inventory : (inventory = FindAnyObjectByType<ResourceInventory>());

    private void Awake()
    {
        Instance = this;
        if (ui == null)
        {
            ui = GetComponent<BuildModeUI>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        draft = FindAnyObjectByType<PresetDraft>();
    }

    private void OnEnable()
    {
        if (BaseLight.Instance != null)
        {
            BaseLight.Instance.PlayerInsideChanged += OnPlayerInsideChanged;
        }
    }

    private void OnDisable()
    {
        if (BaseLight.Instance != null)
        {
            BaseLight.Instance.PlayerInsideChanged -= OnPlayerInsideChanged;
        }
        Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
        {
            Toggle();
        }

        if (!IsOpen)
        {
            return;
        }

        if (DraftOpen() || (BaseLight.Instance != null && !BaseLight.Instance.IsPlayerInside))
        {
            Close();
            return;
        }

        if (SelectedTurret == null && selectedTurretWasSet)
        {
            DeselectTurret(); // the turret was removed
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            if (Selected != null)
            {
                Deselect();
            }
            else if (SelectedTurret != null)
            {
                DeselectTurret();
            }
        }
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Selected == null && !PointerOverUI())
        {
            Turret picked = TurretAtCursor(mouse.position.ReadValue());
            if (picked != null)
            {
                SelectTurret(picked);
            }
        }

        PlayerInputGate.BlockAttacks();
        PlayerInputGate.BlockDash();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open(out _);
        }
    }

    public bool Open(out string reason)
    {
        reason = null;
        if (IsOpen)
        {
            return true;
        }

        if (DraftOpen())
        {
            reason = "a preset draft is open";
            return false;
        }

        if (BaseLight.Instance != null && !BaseLight.Instance.IsPlayerInside)
        {
            reason = DarkMessage;
            if (ui != null)
            {
                ui.ShowError(DarkMessage);
            }
            return false;
        }

        IsOpen = true;
        OpenChanged?.Invoke(true);
        return true;
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        Deselect();
        DeselectTurret();
        IsOpen = false;
        OpenChanged?.Invoke(false);
    }

    public bool CanAfford(TurretDefinition defense)
    {
        return defense != null && Inventory != null && Inventory.Wood >= defense.WoodCost;
    }

    // Picks a defense from the bar (the bar slides down; the ghost follows in step 4). Unaffordable = refused.
    public bool Select(TurretDefinition defense)
    {
        if (!IsOpen || defense == null || !CanAfford(defense))
        {
            return false;
        }

        DeselectTurret();
        Selected = defense;
        SelectionChanged?.Invoke();
        return true;
    }

    public void SelectTurret(Turret turret)
    {
        if (!IsOpen || turret == null || turret == SelectedTurret)
        {
            return;
        }

        SetRangeVisible(SelectedTurret, false);
        SelectedTurret = turret;
        selectedTurretWasSet = true;
        SetRangeVisible(turret, true);
        TurretSelectionChanged?.Invoke();
    }

    // Sells the turret whose panel is open; returns the refund (0 if none).
    public int SellSelected()
    {
        Turret turret = SelectedTurret;
        if (turret == null)
        {
            return 0;
        }

        DeselectTurret();
        return turret.Sell(Inventory);
    }

    public void DeselectTurret()
    {
        if (!selectedTurretWasSet)
        {
            return;
        }

        SetRangeVisible(SelectedTurret, false);
        SelectedTurret = null;
        selectedTurretWasSet = false;
        TurretSelectionChanged?.Invoke();
    }

    // The turret whose body (ground up to pickHeight) the cursor ray passes closest to, within pickRadius.
    public Turret TurretAtCursor(Vector2 screenPoint)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return null;
        }

        Ray ray = cam.ScreenPointToRay(screenPoint);
        Turret best = null;
        float bestDistance = pickRadius;
        for (int i = 0; i < Turret.Active.Count; i++)
        {
            Turret turret = Turret.Active[i];
            for (float h = 0f; h <= pickHeight + 0.001f; h += pickHeight / 6f)
            {
                Vector3 point = turret.Center + Vector3.up * h;
                float distance = Vector3.Cross(ray.direction, point - ray.origin).magnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = turret;
                }
            }
        }
        return best;
    }

    private static void SetRangeVisible(Turret turret, bool visible)
    {
        if (turret == null)
        {
            return;
        }

        TurretRadii radii = turret.GetComponent<TurretRadii>();
        if (radii != null)
        {
            radii.RangeVisible = visible;
        }
    }

    private static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public void Deselect()
    {
        if (Selected == null)
        {
            return;
        }

        Selected = null;
        SelectionChanged?.Invoke();
    }

    private void OnPlayerInsideChanged(bool inside)
    {
        if (!inside)
        {
            Close();
        }
    }

    private bool DraftOpen()
    {
        return draft != null && draft.IsOpen;
    }
}
