/*
 * PresetDraft
 * The pick-1-of-3 wall preset draft. Holds the run's two draft pools, deals cards, and hands the picked chunk to the
 * ChunkPlacer.
 *
 * - Pools: at Start every chunk in `chunks` adds WallChunk.CopiesInPool copies to the base pool (IsBaseChunk) or the
 *   normal pool. A draft deals up to offerCount cards from one pool without putting them back, so a chunk with several
 *   copies left can show up more than once. The picked copy is used up (placed or discarded); the others go back.
 *   Offers don't have to fit the current base. When a draft opens and its pool holds fewer than offerCount cards, that
 *   pool is refilled to its full starting contents first.
 * - The base draft opens on Start (baseDraftOnStart) unless the layout already holds a base chunk (test layout); the
 *   base chunk must be placed. Normal drafts open with OpenDraft() (the round state machine later) or draftKey (M, a
 *   debug key) once the base is placed, never while a draft or placement is already open.
 * - While a draft or placement is open, player mouse attacks are blocked (PlayerInputGate).
 * - Context menu "Find All Chunks" (editor) fills `chunks` with every WallChunk asset in the project.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PresetDraft : MonoBehaviour
{
    private enum State
    {
        Idle,
        Choosing,
        Placing
    }

    [SerializeField] private BaseLayout layout;
    [SerializeField] private PresetDraftUI ui;
    [SerializeField] private ChunkPlacer placer;
    [SerializeField] private List<WallChunk> chunks = new List<WallChunk>();
    [SerializeField, Min(1)] private int offerCount = 3;
    [SerializeField] private bool baseDraftOnStart = true;
    [Tooltip("Debug key that opens a normal draft (until rounds trigger it).")]
    [SerializeField] private Key draftKey = Key.M;

    private readonly List<WallChunk> basePool = new List<WallChunk>();
    private readonly List<WallChunk> mainPool = new List<WallChunk>();
    private readonly List<WallChunk> offered = new List<WallChunk>();
    private State state;
    private bool draftIsBase;

    public bool IsOpen => state != State.Idle;
    public IReadOnlyList<WallChunk> Offered => offered;
    public int BasePoolCount => basePool.Count;
    public int MainPoolCount => mainPool.Count;

    private void Start()
    {
        FillPool(true);
        FillPool(false);

        if (baseDraftOnStart && !layout.HasBaseChunk)
        {
            OpenDraft(true);
        }
    }

    private void Update()
    {
        if (state != State.Idle)
        {
            PlayerInputGate.BlockAttacks();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[draftKey].wasPressedThisFrame && layout.HasBaseChunk)
        {
            OpenDraft(false);
        }
    }

    /// <summary>Deals a draft from the base or the normal pool. False when one is already open or the pool is empty.</summary>
    public bool OpenDraft(bool baseDraft)
    {
        if (state != State.Idle)
        {
            return false;
        }
        List<WallChunk> pool = baseDraft ? basePool : mainPool;
        if (pool.Count < offerCount)
        {
            FillPool(baseDraft);
        }
        offered.Clear();
        for (int i = 0; i < offerCount && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            offered.Add(pool[index]);
            pool.RemoveAt(index);
        }
        if (offered.Count == 0)
        {
            Debug.LogWarning("PresetDraft: the " + (baseDraft ? "base" : "preset") + " pool is empty.", this);
            return false;
        }

        draftIsBase = baseDraft;
        state = State.Choosing;
        PlayerInputGate.BlockAttacks();
        ui.ShowCards(offered, baseDraft ? "Choose your base" : "Choose a preset", baseDraft ? null : layout, OnPicked);
        return true;
    }

    private void OnPicked(int index, int rotation)
    {
        if (state != State.Choosing || index < 0 || index >= offered.Count)
        {
            return;
        }
        WallChunk picked = offered[index];
        List<WallChunk> pool = draftIsBase ? basePool : mainPool;
        for (int i = 0; i < offered.Count; i++)
        {
            if (i != index)
            {
                pool.Add(offered[i]);
            }
        }
        offered.Clear();
        ui.HideCards();
        state = State.Placing;
        placer.Begin(picked, rotation, draftIsBase, OnPlacementDone);
    }

    // Resets a pool to every chunk of that kind times its CopiesInPool.
    private void FillPool(bool baseChunks)
    {
        List<WallChunk> pool = baseChunks ? basePool : mainPool;
        pool.Clear();
        foreach (WallChunk chunk in chunks)
        {
            if (chunk == null || chunk.IsBaseChunk != baseChunks)
            {
                continue;
            }
            for (int i = 0; i < chunk.CopiesInPool; i++)
            {
                pool.Add(chunk);
            }
        }
    }

    private void OnPlacementDone(bool placed)
    {
        state = State.Idle;
    }

#if UNITY_EDITOR
    [ContextMenu("Find All Chunks")]
    private void FindAllChunks()
    {
        UnityEditor.Undo.RecordObject(this, "Find All Chunks");
        chunks.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:WallChunk"))
        {
            chunks.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<WallChunk>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
