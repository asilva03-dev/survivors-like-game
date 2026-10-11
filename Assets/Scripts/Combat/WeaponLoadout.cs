/*
 * WeaponLoadout / IAimedWeapon
 * Lives on the weapon owner (the player) and coordinates all of its aimed weapons so they work as a team.
 *
 * - Target priority (highest first):
 *     1. Enemies inside closeRange of the owner (a character-wide range, not tied to any weapon)
 *     2. Bosses
 *     3. Elites
 *     4. Lowest current HP (absolute HP, not percentage)
 *     5. Closest to the owner
 *   The same order applies inside the close range, so a boss at the player's feet beats a minion.
 * - Close-range targets are shared: while any enemy is inside closeRange, ALL weapons attack the same
 *   top-priority enemy in it (no splitting), regardless of each weapon's own range. This target is
 *   chosen once for the whole loadout and kept until it dies, leaves closeRange, or a higher tier
 *   enemy (boss or elite) enters closeRange.
 * - Otherwise a weapon can only target enemies inside its own range.
 * - Sticky targets: a weapon keeps its current target while it is alive, still targetable and not
 *   claimed by an earlier weapon, and switches only when an enemy of a strictly higher tier (close
 *   range > boss > elite > normal) is available. Within a tier it does not re-rank, which keeps
 *   targeting from flickering as the player moves.
 * - Weapons are served in registration order, and each prefers enemies no earlier weapon has taken,
 *   so guns split targets when they can. If every candidate is taken (e.g. one enemy around), the
 *   weapon shares the best one, so all guns attack the same enemy.
 * - Pairing by side: after targets are chosen (the set of enemies never changes here), they are
 *   matched to weapons so total angular distance between each weapon's current ring spot and its
 *   target direction is smallest. A different pairing is used only if it is better by more than
 *   pairingThreshold degrees, so a near tie cannot flip back and forth. This keeps guns on their own
 *   side instead of crossing the ring when enemy order changes. Skipped for the shared close target.
 * - Slot spacing: each weapon's wanted ring spot is the direction to its target. Weapons whose spots
 *   are closer than the required spacing are pushed apart along the ring. The required spacing is the
 *   largest separationDistance of the pair, so bigger guns get more room.
 * - Pushing is symmetric and order-stable: when two spots coincide, the earlier registered weapon
 *   sits to the right (as seen from the owner looking at the target) and the later one to the left,
 *   so weapons do not swap sides or cross over.
 * - Weapons keep facing their real target from wherever they sit, so spread-out guns tilt inward.
 * - TryAddWeapon(prefab) spawns a weapon as a child of the owner mid-run (used by WeaponPickup) and
 *   fails only for aimed weapons (guns, later melee) when the loadout already holds
 *   maxAimedWeapons of them. Other weapon types (AOE, ...) are not capped yet and just run on their
 *   own; aimed weapons register themselves.
 * - Results are computed once per frame on first request, so script execution order does not matter.
 * - AimedWeapon registers itself automatically in OnEnable if a WeaponLoadout is on a parent.
 */

using System.Collections.Generic;
using UnityEngine;

public interface IAimedWeapon
{
    Transform Owner { get; }
    float Range { get; }
    float RingRadius { get; }
    float SeparationDistance { get; }
    Vector3 SlotDirection { get; }
}

public class WeaponLoadout : MonoBehaviour
{
    private const int SpreadIterations = 4;
    private const int CloseTierBonus = 3;
    private const float HealthTieEpsilon = 0.01f;
    private const int MaxPairedWeapons = 5;
    private const float InvalidPairCost = 1000f;

    [SerializeField, Min(0f)] private float closeRange = 3f;
    [SerializeField, Min(0f)] private float pairingThreshold = 20f;
    [SerializeField, Min(1)] private int maxAimedWeapons = 2;

    private readonly float[,] pairCost = new float[MaxPairedWeapons, MaxPairedWeapons];
    private readonly bool[] pairUsed = new bool[MaxPairedWeapons];
    private readonly int[] currentPairing = new int[MaxPairedWeapons];
    private readonly int[] bestPairing = new int[MaxPairedWeapons];
    private readonly Enemy[] pairResult = new Enemy[MaxPairedWeapons];
    private readonly List<int> pairWeapons = new List<int>(MaxPairedWeapons);
    private float bestPairCost;

    private readonly List<IAimedWeapon> weapons = new List<IAimedWeapon>();
    private readonly List<Enemy> targets = new List<Enemy>();
    private readonly List<float> angles = new List<float>();
    private readonly HashSet<Enemy> taken = new HashSet<Enemy>();
    private readonly Dictionary<IAimedWeapon, Enemy> lastTargets = new Dictionary<IAimedWeapon, Enemy>();

    private Enemy closeTarget;
    private int refreshedFrame = -1;

    public float CloseRange => closeRange;

    public bool TryAddWeapon(GameObject weaponPrefab)
    {
        if (weaponPrefab == null)
        {
            return false;
        }

        bool isAimed = weaponPrefab.GetComponent<IAimedWeapon>() != null;
        if (isAimed && weapons.Count >= maxAimedWeapons)
        {
            return false;
        }

        Instantiate(weaponPrefab, transform);
        return true;
    }

    public void Register(IAimedWeapon weapon)
    {
        if (!weapons.Contains(weapon))
        {
            weapons.Add(weapon);
            refreshedFrame = -1;
        }
    }

    public void Unregister(IAimedWeapon weapon)
    {
        weapons.Remove(weapon);
        lastTargets.Remove(weapon);
        refreshedFrame = -1;
    }

    public Enemy GetTarget(IAimedWeapon weapon)
    {
        Refresh();
        int index = weapons.IndexOf(weapon);
        return index >= 0 ? targets[index] : null;
    }

    public bool TryGetSlotDirection(IAimedWeapon weapon, out Vector3 direction)
    {
        Refresh();
        int index = weapons.IndexOf(weapon);

        if (index < 0 || float.IsNaN(angles[index]))
        {
            direction = Vector3.zero;
            return false;
        }

        float radians = angles[index] * Mathf.Deg2Rad;
        direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        return true;
    }

    private static float ArcToAngle(float distance, float radius)
    {
        float ratio = Mathf.Clamp01(distance / (2f * Mathf.Max(radius, 0.01f)));
        return 2f * Mathf.Asin(ratio) * Mathf.Rad2Deg;
    }

    private void Refresh()
    {
        if (refreshedFrame == Time.frameCount)
        {
            return;
        }

        refreshedFrame = Time.frameCount;
        taken.Clear();
        targets.Clear();
        angles.Clear();

        Enemy closeTarget = SelectCloseTarget();

        for (int i = 0; i < weapons.Count; i++)
        {
            IAimedWeapon weapon = weapons[i];
            Enemy target = closeTarget != null ? closeTarget : SelectTarget(weapon);

            targets.Add(target);
            angles.Add(float.NaN);

            if (target != null)
            {
                taken.Add(target);
            }
        }

        if (closeTarget == null)
        {
            PairByAngle();
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            IAimedWeapon weapon = weapons[i];
            Enemy target = targets[i];
            lastTargets[weapon] = target;

            if (target == null)
            {
                continue;
            }

            Vector3 offset = target.transform.position - weapon.Owner.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.0001f)
            {
                angles[i] = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            }
        }

        SpreadAngles();
    }

    private void PairByAngle()
    {
        pairWeapons.Clear();
        for (int i = 0; i < weapons.Count; i++)
        {
            if (targets[i] != null)
            {
                pairWeapons.Add(i);
            }
        }

        int count = pairWeapons.Count;
        if (count < 2 || count > MaxPairedWeapons)
        {
            return;
        }

        for (int a = 0; a < count; a++)
        {
            IAimedWeapon weapon = weapons[pairWeapons[a]];
            Vector3 slot = weapon.SlotDirection;
            float slotYaw = Mathf.Atan2(slot.x, slot.z) * Mathf.Rad2Deg;

            for (int b = 0; b < count; b++)
            {
                Enemy candidate = targets[pairWeapons[b]];

                if (!IsCandidate(weapon, candidate))
                {
                    pairCost[a, b] = InvalidPairCost;
                    continue;
                }

                Vector3 offset = candidate.transform.position - weapon.Owner.position;
                float targetYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                pairCost[a, b] = Mathf.Abs(Mathf.DeltaAngle(slotYaw, targetYaw));
            }
        }

        float identityCost = 0f;
        for (int a = 0; a < count; a++)
        {
            identityCost += pairCost[a, a];
        }

        bestPairCost = identityCost;
        for (int a = 0; a < count; a++)
        {
            pairUsed[a] = false;
            bestPairing[a] = a;
        }

        SearchPairings(0, count, 0f);

        if (bestPairCost + pairingThreshold >= identityCost)
        {
            return;
        }

        for (int a = 0; a < count; a++)
        {
            pairResult[a] = targets[pairWeapons[bestPairing[a]]];
        }

        for (int a = 0; a < count; a++)
        {
            targets[pairWeapons[a]] = pairResult[a];
        }
    }

    private void SearchPairings(int weaponIndex, int count, float costSoFar)
    {
        if (costSoFar >= bestPairCost)
        {
            return;
        }

        if (weaponIndex == count)
        {
            bestPairCost = costSoFar;
            for (int a = 0; a < count; a++)
            {
                bestPairing[a] = currentPairing[a];
            }

            return;
        }

        for (int target = 0; target < count; target++)
        {
            if (pairUsed[target])
            {
                continue;
            }

            pairUsed[target] = true;
            currentPairing[weaponIndex] = target;
            SearchPairings(weaponIndex + 1, count, costSoFar + pairCost[weaponIndex, target]);
            pairUsed[target] = false;
        }
    }

    private Enemy SelectCloseTarget()
    {
        Vector3 origin = transform.position;
        float sqrClose = closeRange * closeRange;
        Enemy best = null;

        for (int i = 0; i < Enemy.Active.Count; i++)
        {
            Enemy enemy = Enemy.Active[i];
            Vector3 offset = enemy.transform.position - origin;
            offset.y = 0f;

            if (offset.sqrMagnitude <= sqrClose && (best == null || IsBetter(enemy, best, origin)))
            {
                best = enemy;
            }
        }

        bool keepCurrent = closeTarget != null && closeTarget.isActiveAndEnabled && IsInCloseRange(closeTarget, origin)
            && (best == null || TierOf(best, origin) <= TierOf(closeTarget, origin));

        if (!keepCurrent)
        {
            closeTarget = best;
        }

        return closeTarget;
    }

    private bool IsInCloseRange(Enemy enemy, Vector3 origin)
    {
        Vector3 offset = enemy.transform.position - origin;
        offset.y = 0f;
        return offset.sqrMagnitude <= closeRange * closeRange;
    }

    private Enemy SelectTarget(IAimedWeapon weapon)
    {
        Vector3 origin = weapon.Owner.position;

        Enemy previous;
        lastTargets.TryGetValue(weapon, out previous);
        bool keepPrevious = previous != null && previous.isActiveAndEnabled
            && IsCandidate(weapon, previous) && !taken.Contains(previous);

        Enemy bestFree = null;
        Enemy bestAny = null;

        for (int i = 0; i < Enemy.Active.Count; i++)
        {
            Enemy enemy = Enemy.Active[i];
            if (!IsCandidate(weapon, enemy))
            {
                continue;
            }

            if (!taken.Contains(enemy) && (bestFree == null || IsBetter(enemy, bestFree, origin)))
            {
                bestFree = enemy;
            }

            if (bestAny == null || IsBetter(enemy, bestAny, origin))
            {
                bestAny = enemy;
            }
        }

        if (keepPrevious && (bestFree == null || TierOf(bestFree, origin) <= TierOf(previous, origin)))
        {
            return previous;
        }

        return bestFree != null ? bestFree : bestAny;
    }

    private bool IsCandidate(IAimedWeapon weapon, Enemy enemy)
    {
        Vector3 offset = enemy.transform.position - weapon.Owner.position;
        offset.y = 0f;
        float sqr = offset.sqrMagnitude;
        return sqr <= weapon.Range * weapon.Range || sqr <= closeRange * closeRange;
    }

    private int TierOf(Enemy enemy, Vector3 origin)
    {
        Vector3 offset = enemy.transform.position - origin;
        offset.y = 0f;
        int tier = (int)enemy.Rank;
        return offset.sqrMagnitude <= closeRange * closeRange ? tier + CloseTierBonus : tier;
    }

    private bool IsBetter(Enemy candidate, Enemy current, Vector3 origin)
    {
        int candidateTier = TierOf(candidate, origin);
        int currentTier = TierOf(current, origin);
        if (candidateTier != currentTier)
        {
            return candidateTier > currentTier;
        }

        float healthDifference = candidate.Health - current.Health;
        if (Mathf.Abs(healthDifference) > HealthTieEpsilon)
        {
            return healthDifference < 0f;
        }

        Vector3 candidateOffset = candidate.transform.position - origin;
        Vector3 currentOffset = current.transform.position - origin;
        candidateOffset.y = 0f;
        currentOffset.y = 0f;
        return candidateOffset.sqrMagnitude < currentOffset.sqrMagnitude;
    }

    private void SpreadAngles()
    {
        for (int iteration = 0; iteration < SpreadIterations; iteration++)
        {
            for (int a = 0; a < weapons.Count; a++)
            {
                for (int b = a + 1; b < weapons.Count; b++)
                {
                    if (float.IsNaN(angles[a]) || float.IsNaN(angles[b]))
                    {
                        continue;
                    }

                    float spacing = Mathf.Max(weapons[a].SeparationDistance, weapons[b].SeparationDistance);
                    float minAngle = ArcToAngle(spacing, weapons[a].RingRadius);
                    float difference = Mathf.DeltaAngle(angles[a], angles[b]);

                    if (Mathf.Abs(difference) >= minAngle)
                    {
                        continue;
                    }

                    float push = (minAngle - Mathf.Abs(difference)) * 0.5f;
                    float direction = difference > 0.001f ? 1f : -1f;

                    angles[b] += direction * push;
                    angles[a] -= direction * push;
                }
            }
        }
    }
}
