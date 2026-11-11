/*
 * PlayerWading
 * On the Player (2026-11-10, user): the player walks through trees instead of bumping into them (tree colliders are
 * triggers), but wading through one slows walking and blocks starting a dash or a jump; the trees rustle as the player
 * passes. Decoration bushes (GroundDecorations, step 3) work the same way.
 *
 * - Wading = the player's centre is within wadeRadius (flat) of any live tree (TreeSpawner.LiveTrees) or within a bush's
 *   own WadeRadius (GroundDecorations.LiveBushes); plain distance loops each frame. While wading:
 *   PlayerController.WadeSpeedMultiplier = speedMultiplier and PlayerController.Wading is on (no new dash or jump; one
 *   already going carries on).
 * - Rustle: a tree or bush rustles (IWadeable.Rustle, away from the player) when the player enters it, and again every
 *   rustleInterval seconds while the player keeps moving inside it.
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerWading : MonoBehaviour
{
    [Tooltip("Flat distance from a tree's centre at which the player is wading through it (the canopy is ~1.2 wide).")]
    [SerializeField, Min(0.1f)] private float wadeRadius = 1.2f;
    [Tooltip("Walking speed multiplier while wading.")]
    [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.7f;
    [Tooltip("Seconds between rustles of a tree while the player keeps moving inside it.")]
    [SerializeField, Min(0.05f)] private float rustleInterval = 0.4f;
    [Tooltip("The player counts as moving above this flat speed.")]
    [SerializeField, Min(0f)] private float movingSpeed = 0.5f;

    private readonly List<IWadeable> touching = new List<IWadeable>();
    private readonly List<float> lastRustle = new List<float>();
    private readonly List<IWadeable> nowTouching = new List<IWadeable>();
    private readonly List<float> nowRustle = new List<float>();

    private PlayerController controller;
    private Rigidbody body;
    private TreeSpawner trees;
    private GroundDecorations decorations;
    private Vector3 position;
    private Vector3 velocity;
    private bool moving;
    private float now;

    public bool IsWading { get; private set; }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody>();
    }

    private void OnDisable()
    {
        SetWading(false);
        touching.Clear();
        lastRustle.Clear();
    }

    private void Update()
    {
        if (trees == null)
        {
            trees = FindAnyObjectByType<TreeSpawner>();
        }
        if (decorations == null)
        {
            decorations = FindAnyObjectByType<GroundDecorations>();
        }

        position = transform.position;
        velocity = body != null ? body.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        moving = velocity.sqrMagnitude > movingSpeed * movingSpeed;
        now = Time.time;

        nowTouching.Clear();
        nowRustle.Clear();
        if (trees != null)
        {
            IReadOnlyList<ResourceTree> live = trees.LiveTrees;
            for (int i = 0; i < live.Count; i++)
            {
                Check(live[i], live[i].transform.position, wadeRadius);
            }
        }
        if (decorations != null)
        {
            IReadOnlyList<DecorBush> bushes = decorations.LiveBushes;
            for (int i = 0; i < bushes.Count; i++)
            {
                Check(bushes[i], bushes[i].transform.position, bushes[i].WadeRadius);
            }
        }

        touching.Clear();
        touching.AddRange(nowTouching);
        lastRustle.Clear();
        lastRustle.AddRange(nowRustle);
        SetWading(touching.Count > 0);
    }

    // Within radius: counts as wading, rustles on entry and every rustleInterval while moving.
    private void Check(IWadeable thing, Vector3 centre, float radius)
    {
        Vector3 offset = centre - position;
        offset.y = 0f;
        if (offset.sqrMagnitude > radius * radius)
        {
            return;
        }
        int before = touching.IndexOf(thing);
        float last = before >= 0 ? lastRustle[before] : float.NegativeInfinity;
        if (before < 0 || (moving && now - last >= rustleInterval))
        {
            // Tip away from the player, or along the movement when standing on its centre.
            thing.Rustle(offset.sqrMagnitude > 0.0001f ? offset : velocity);
            last = now;
        }
        nowTouching.Add(thing);
        nowRustle.Add(last);
    }

    private void SetWading(bool wading)
    {
        IsWading = wading;
        if (controller != null)
        {
            controller.Wading = wading;
            controller.WadeSpeedMultiplier = wading ? speedMultiplier : 1f;
        }
    }
}
