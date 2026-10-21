/*
 * PlayerHealth
 * The player's Damageable (category Player): what contact damage and ranged-enemy bullets hurt.
 *
 * - After a hit that deals damage the player is invulnerable for invulnerabilityDuration seconds (the
 *   timer starts on the hit). Hits during that time are ignored completely, and enemy projectiles pass
 *   through the player instead of being used up.
 * - Healing (negative damage) is never blocked and does not start the timer.
 * - The player does not die yet (CanDie is false): health can reach 0 and nothing happens until lives exist.
 * - Instance is the active player health, for systems that need it without a scene search (contact damage).
 * - PlayerHitFeedback reads Damaged for the red flash and jitter; this class only owns health and the timer.
 */

using UnityEngine;

public class PlayerHealth : Damageable
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.8f;

    private float invulnerableUntil;

    public override bool IsInvulnerable => Time.time < invulnerableUntil;
    public float InvulnerabilityDuration => invulnerabilityDuration;
    protected override bool CanDie => false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Instance = this;
        invulnerableUntil = 0f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void TakeDamage(float amount)
    {
        if (amount > 0f)
        {
            if (IsInvulnerable)
            {
                return;
            }

            invulnerableUntil = Time.time + invulnerabilityDuration;
        }

        base.TakeDamage(amount);
    }
}
