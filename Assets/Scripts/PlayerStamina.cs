/*
 * PlayerStamina
 * Holds the player's stamina pool and handles regeneration.
 *
 * - Other scripts spend stamina with TrySpend (instant cost) or Drain (continuous cost).
 * - Any spend or drain restarts the regen delay, so stamina only recovers after
 *   the player has stopped using it for regenDelay seconds.
 * - All values are public tuning knobs in the inspector: maxStamina, regenRate
 *   (units per second) and regenDelay (seconds).
 * - RegenMultiplier scales regenRate (set every frame by PlayerController: faster inside the base light).
 */

using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenRate = 20f;
    [SerializeField] private float regenDelay = 1.5f;

    private float delayTimer;

    public float Current { get; private set; }
    public float Max => maxStamina;
    public float Normalized => maxStamina > 0f ? Current / maxStamina : 0f;
    public float RegenMultiplier { get; set; } = 1f;

    private void Awake()
    {
        Current = maxStamina;
    }

    private void Update()
    {
        if (delayTimer > 0f)
        {
            delayTimer -= Time.deltaTime;
            return;
        }

        Current = Mathf.Min(maxStamina, Current + regenRate * RegenMultiplier * Time.deltaTime);
    }

    public bool TrySpend(float amount)
    {
        if (Current < amount)
        {
            return false;
        }

        Current -= amount;
        delayTimer = regenDelay;
        return true;
    }

    public void Drain(float amount)
    {
        Current = Mathf.Max(0f, Current - amount);
        delayTimer = regenDelay;
    }
}
