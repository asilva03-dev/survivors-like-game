/*
 * WoodDrop
 * A pooled collectable wood chunk dropped when a tree breaks.
 *
 * - Launch(): arcs from the tree out to a landing spot (flightDuration, arcHeight), then hovers and
 *   spins in place the same way WeaponPickup does (spinSpeed about world Y, bobHeight / bobSpeed
 *   around hoverHeight above the ground).
 * - It does nothing about the player by itself. ResourceCollector (on the player) calls Attract()
 *   once the drop is inside the collection radius; the drop then flies at the player, speeding up
 *   (collectSpeed + collectAcceleration) and shrinking to nothing as it closes in. Arriving fires
 *   the static Collected event (ResourceInventory adds the wood) and returns the drop to its pool.
 * - A TrailRenderer (white, tapering) only emits while the drop is being collected; its look is set
 *   on the prefab.
 * - Active lists every enabled drop so the collector never searches the scene. Only drops that have
 *   landed (IsCollectable) can be attracted.
 * - Pooled: Launch() fully resets it, so a recycled drop starts clean.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public class WoodDrop : MonoBehaviour
{
    public static readonly List<WoodDrop> Active = new List<WoodDrop>();
    public static event Action<int> Collected;

    private enum State { Flying, Hovering, Collecting }

    [Header("Hover (matches WeaponPickup)")]
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField, Min(0f)] private float bobHeight = 0.2f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;
    [SerializeField, Min(0f)] private float hoverHeight = 1f;

    [Header("Burst")]
    [SerializeField, Min(0.05f)] private float flightDuration = 0.5f;
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;

    [Header("Collecting")]
    [SerializeField, Min(0.1f)] private float collectSpeed = 8f;
    [SerializeField, Min(0f)] private float collectAcceleration = 20f;
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.3f;

    [Header("Visuals")]
    [SerializeField] private TrailRenderer trail;

    private ComponentPool<WoodDrop> pool;
    private Transform target;
    private State state;
    private Vector3 startPosition;
    private Vector3 landPosition;
    private float elapsed;
    private float bobPhase;
    private float collectSpeedNow;
    private float collectStartDistance;
    private int amount;

    public bool IsCollectable => state == State.Hovering;
    public float HoverHeight => hoverHeight;

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    public void Launch(ComponentPool<WoodDrop> owningPool, Vector3 from, Vector3 land, int woodAmount)
    {
        pool = owningPool;
        amount = woodAmount;
        startPosition = from;
        landPosition = land;
        elapsed = 0f;
        bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        state = State.Flying;
        target = null;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        transform.position = from;

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    public void Attract(Transform player)
    {
        if (state != State.Hovering)
        {
            return;
        }

        target = player;
        state = State.Collecting;
        collectSpeedNow = collectSpeed;
        collectStartDistance = Mathf.Max(Vector3.Distance(transform.position, player.position), arriveDistance + 0.01f);

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        switch (state)
        {
            case State.Flying:
                Fly();
                break;
            case State.Hovering:
                transform.position = landPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight);
                break;
            case State.Collecting:
                Collect();
                break;
        }
    }

    private void Fly()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);
        Vector3 position = Vector3.Lerp(startPosition, landPosition, t);
        position.y += arcHeight * 4f * t * (1f - t);
        transform.position = position;

        if (t >= 1f)
        {
            state = State.Hovering;
        }
    }

    private void Collect()
    {
        collectSpeedNow += collectAcceleration * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target.position, collectSpeedNow * Time.deltaTime);

        float distance = Vector3.Distance(transform.position, target.position);
        float k = Mathf.Clamp01((distance - arriveDistance) / (collectStartDistance - arriveDistance));
        transform.localScale = Vector3.one * k;

        if (distance <= arriveDistance)
        {
            Collected?.Invoke(amount);
            pool.Release(this);
        }
    }
}
