/*
 * PlayerWading
 * On the Player (2026-11-10, user): the player walks through trees instead of bumping into them (tree colliders are
 * triggers), but wading through one slows walking and blocks starting a dash or a jump; the trees rustle as the player
 * passes.
 *
 * - Wading = the player's centre is within wadeRadius (flat) of any live tree (TreeSpawner.LiveTrees, a plain distance
 *   loop each frame). While wading: PlayerController.WadeSpeedMultiplier = speedMultiplier and PlayerController.Wading
 *   is on (no new dash or jump; one already going carries on).
 * - Rustle: a tree rustles (ResourceTree.Rustle, away from the player) when the player enters it, and again every
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

    private readonly List<ResourceTree> touching = new List<ResourceTree>();
    private readonly List<float> lastRustle = new List<float>();
    private readonly List<ResourceTree> nowTouching = new List<ResourceTree>();
    private readonly List<float> nowRustle = new List<float>();

    private PlayerController controller;
    private Rigidbody body;
    private TreeSpawner trees;

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
            if (trees == null)
            {
                SetWading(false);
                return;
            }
        }

        Vector3 position = transform.position;
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        bool moving = velocity.sqrMagnitude > movingSpeed * movingSpeed;
        float sqrRadius = wadeRadius * wadeRadius;
        float now = Time.time;

        nowTouching.Clear();
        nowRustle.Clear();
        IReadOnlyList<ResourceTree> live = trees.LiveTrees;
        for (int i = 0; i < live.Count; i++)
        {
            ResourceTree tree = live[i];
            Vector3 offset = tree.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrRadius)
            {
                continue;
            }

            int before = touching.IndexOf(tree);
            float last = before >= 0 ? lastRustle[before] : float.NegativeInfinity;
            bool entered = before < 0;
            if (entered || (moving && now - last >= rustleInterval))
            {
                // Tip away from the player, or along the movement when standing on the trunk.
                tree.Rustle(offset.sqrMagnitude > 0.0001f ? offset : velocity);
                last = now;
            }
            nowTouching.Add(tree);
            nowRustle.Add(last);
        }

        touching.Clear();
        touching.AddRange(nowTouching);
        lastRustle.Clear();
        lastRustle.AddRange(nowRustle);
        SetWading(touching.Count > 0);
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
