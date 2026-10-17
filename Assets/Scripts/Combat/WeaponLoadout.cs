/*
 * WeaponLoadout / IAimedWeapon
 * Lives on the weapon owner (the player) and coordinates all of its aimed weapons so they work as a team.
 *
 * - Works on any Damageable category a weapon's stats allow (enemies, resources, ...).
 * - Resources (trees, ...) are always the lowest tier: an enemy anywhere in reach (a weapon's range or
 *   closeRange) beats a resource, even one inside closeRange. Resources only get the shared close-range
 *   targeting below when no enemy is in reach.
 * - Target priority (highest first):
 *     1. Anything inside closeRange of the owner (a character-wide range, not tied to any weapon)
 *        that every weapon in the loadout can target (enemies; resources only when no enemy is in reach)
 *     2. Bosses
 *     3. Elites
 *     4. Lowest current HP (absolute HP; among resources: lowest health percentage, see below)
 *     5. Closest to the owner
 *   The same order applies inside the close range, so a boss at the player's feet beats a minion, and
 *   an enemy always beats a tree standing next to it.
 * - Weapons whose stats set focusWeakestResource (the throwable; not guns, which spread over targets)
 *   add a rule for resources only: among resources in reach they pick the LOWEST HEALTH PERCENTAGE
 *   (not absolute HP), they may share a resource another weapon already took (guns spread out, so
 *   the weak tree is usually "taken" by a gun), and a weapon already on a resource switches to one
 *   that is lower by at least ResourceSwitchMargin (5% of max health) so it does not stick forever
 *   or flicker between equal trees. Enemy targeting and the shared close target are unchanged. Such
 *   weapons are left out of the side pairing below, so they never pull a gun off its target.
 * - Close-range targets are shared: while anything is inside closeRange, ALL weapons attack the same
 *   top-priority target in it (no splitting), regardless of each weapon's own range. This target is
 *   chosen once for the whole loadout and kept until it dies, leaves closeRange, or a higher tier
 *   target (boss or elite) enters closeRange.
 * - Weapons with avoidCloseRange (ThrowL/ThrowE) opt out of all of the above: they ignore the shared close
 *   target and the close-range tier bonus, skip targets inside closeRange while a target of the same kind is
 *   available outside it, and only take close targets when nothing else is in reach. If every weapon opts
 *   out, no shared close target is chosen.
 * - Weapons with preferClusters (ThrowL/ThrowE) first rank enemies by how many other enemies stand within
 *   clusterRadius of them (most first) within the same boss/elite rank, above distance; the close-range avoidance still filters
 *   targets first. When counts tie (all enemies alone) the normal order applies.
 * - Weapons with claimsTargets off (ThrowL/ThrowE) neither respect nor create claims: they ignore what guns have
 *   taken, and guns do not avoid the enemy they chose. Claims only exist between claiming weapons (guns).
 * - Weapons with useRing off (ThrowL/ThrowE) are left out of slot spacing and side pairing, so they never push
 *   another weapon's ring spot.
 * - Weapons with stickyTargets off (ThrowL/ThrowE) skip the sticky rule and re-pick every frame.
 * - Weapons with preferFarTargets (ThrowL/ThrowE) rank targets of the same tier by distance, FARTHEST first, instead
 *   of lowest HP then closest. Resources still use the weakest-percentage rule for focusWeakestResource weapons.
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
 *   fails only for guns when the loadout already holds maxGuns of them (2). Every other weapon type
 *   (ThrowL, AOE, ...) has no limit; aimed ones register themselves.
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
    bool CanTarget(TargetCategory category);
    bool FocusWeakestResource { get; }
    bool AvoidCloseRange { get; }
    bool PreferFarTargets { get; }
    bool StickyTargets { get; }
    bool UseRing { get; }
    bool ClaimsTargets { get; }
    bool PreferClusters { get; }
    float ClusterRadius { get; }
}

public class WeaponLoadout : MonoBehaviour
{
    private const int SpreadIterations = 4;
    private const int CloseTierBonus = 3;
    private const int ResourceTier = -1;
    private const float HealthTieEpsilon = 0.01f;
    private const float ResourceSwitchMargin = 0.05f;
    private const int MaxPairedWeapons = 5;
    private const float InvalidPairCost = 1000f;

    [SerializeField, Min(0f)] private float closeRange = 3f;
    [SerializeField, Min(0f)] private float pairingThreshold = 20f;
    [SerializeField, Min(1), UnityEngine.Serialization.FormerlySerializedAs("maxAimedWeapons")]
    private int maxGuns = 2;

    private readonly float[,] pairCost = new float[MaxPairedWeapons, MaxPairedWeapons];
    private readonly bool[] pairUsed = new bool[MaxPairedWeapons];
    private readonly int[] currentPairing = new int[MaxPairedWeapons];
    private readonly int[] bestPairing = new int[MaxPairedWeapons];
    private readonly Damageable[] pairResult = new Damageable[MaxPairedWeapons];
    private readonly List<int> pairWeapons = new List<int>(MaxPairedWeapons);
    private float bestPairCost;

    private readonly List<IAimedWeapon> weapons = new List<IAimedWeapon>();
    private readonly List<Damageable> targets = new List<Damageable>();
    private readonly List<float> angles = new List<float>();
    private readonly HashSet<Damageable> taken = new HashSet<Damageable>();
    private readonly Dictionary<IAimedWeapon, Damageable> lastTargets = new Dictionary<IAimedWeapon, Damageable>();

    private readonly Dictionary<Damageable, int> clusterCounts = new Dictionary<Damageable, int>();
    private readonly List<Damageable> clusterEnemies = new List<Damageable>();
    private bool clusterActive;
    private Damageable closeTarget;
    private int refreshedFrame = -1;

    public float CloseRange => closeRange;

    public bool TryAddWeapon(GameObject weaponPrefab)
    {
        if (weaponPrefab == null)
        {
            return false;
        }

        if (weaponPrefab.GetComponent<GunWeapon>() != null && CountGuns() >= maxGuns)
        {
            return false;
        }

        Instantiate(weaponPrefab, transform);
        return true;
    }

    private int CountGuns()
    {
        int count = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] is GunWeapon)
            {
                count++;
            }
        }

        return count;
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

    public Damageable GetTarget(IAimedWeapon weapon)
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

        clusterActive = false;
        bool enemyInReach = AnyEnemyInReach();
        Damageable closeTarget = SelectCloseTarget(enemyInReach);

        for (int i = 0; i < weapons.Count; i++)
        {
            IAimedWeapon weapon = weapons[i];
            Damageable target = closeTarget != null && !weapon.AvoidCloseRange ? closeTarget : SelectTarget(weapon);

            targets.Add(target);
            angles.Add(float.NaN);

            if (target != null && weapon.ClaimsTargets)
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
            Damageable target = targets[i];
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
            if (targets[i] != null && !weapons[i].FocusWeakestResource && weapons[i].UseRing)
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
                Damageable candidate = targets[pairWeapons[b]];

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

    private bool AnyEnemyInReach()
    {
        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (candidate.Category == TargetCategory.Resource)
            {
                continue;
            }

            for (int w = 0; w < weapons.Count; w++)
            {
                if (IsCandidate(weapons[w], candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool AnyWeaponUsesCloseTarget()
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (!weapons[i].AvoidCloseRange)
            {
                return true;
            }
        }

        return false;
    }

    private Damageable SelectCloseTarget(bool enemyInReach)
    {
        if (!AnyWeaponUsesCloseTarget())
        {
            closeTarget = null;
            return null;
        }

        Vector3 origin = transform.position;
        float sqrClose = closeRange * closeRange;
        Damageable best = null;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable enemy = Damageable.All[i];
            if (!AllWeaponsCanTarget(enemy.Category))
            {
                continue;
            }

            if (enemyInReach && enemy.Category == TargetCategory.Resource)
            {
                continue;
            }

            Vector3 offset = enemy.transform.position - origin;
            offset.y = 0f;

            if (offset.sqrMagnitude <= sqrClose && (best == null || IsBetter(enemy, best, origin)))
            {
                best = enemy;
            }
        }

        bool keepCurrent = closeTarget != null && closeTarget.isActiveAndEnabled && IsInCloseRange(closeTarget, origin)
            && !(enemyInReach && closeTarget.Category == TargetCategory.Resource)
            && (best == null || TierOf(best, origin) <= TierOf(closeTarget, origin));

        if (!keepCurrent)
        {
            closeTarget = best;
        }

        return closeTarget;
    }

    private bool AllWeaponsCanTarget(TargetCategory category)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (!weapons[i].AvoidCloseRange && !weapons[i].CanTarget(category))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsInCloseRange(Damageable enemy, Vector3 origin)
    {
        Vector3 offset = enemy.transform.position - origin;
        offset.y = 0f;
        return offset.sqrMagnitude <= closeRange * closeRange;
    }

    private Damageable SelectTarget(IAimedWeapon weapon)
    {
        Vector3 origin = weapon.Owner.position;

        Damageable previous;
        lastTargets.TryGetValue(weapon, out previous);
        bool focus = weapon.FocusWeakestResource;
        bool avoid = weapon.AvoidCloseRange;
        bool claims = weapon.ClaimsTargets;
        bool far = weapon.PreferFarTargets;

        bool outsideEnemy = false;
        bool outsideResource = false;
        bool anyEnemy = false;
        if (avoid)
        {
            for (int i = 0; i < Damageable.All.Count; i++)
            {
                Damageable candidate = Damageable.All[i];
                if (!IsCandidate(weapon, candidate))
                {
                    continue;
                }

                bool resource = candidate.Category == TargetCategory.Resource;
                anyEnemy |= !resource;
                if (!IsInCloseRange(candidate, origin))
                {
                    outsideEnemy |= !resource;
                    outsideResource |= resource;
                }
            }
        }

        clusterActive = weapon.PreferClusters;
        if (clusterActive)
        {
            ComputeClusters(weapon);
        }

        bool keepPrevious = weapon.StickyTargets && previous != null && previous.isActiveAndEnabled
            && IsCandidate(weapon, previous) && (!claims || !taken.Contains(previous) || IsSharedResource(focus, previous))
            && !(avoid && SkipForAvoid(previous, origin, outsideEnemy, outsideResource, anyEnemy));

        Damageable bestFree = null;
        Damageable bestAny = null;

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable enemy = Damageable.All[i];
            if (!IsCandidate(weapon, enemy)
                || (avoid && SkipForAvoid(enemy, origin, outsideEnemy, outsideResource, anyEnemy)))
            {
                continue;
            }

            if ((!claims || !taken.Contains(enemy) || IsSharedResource(focus, enemy))
                && (bestFree == null || IsBetter(enemy, bestFree, origin, focus, avoid, far)))
            {
                bestFree = enemy;
            }

            if (bestAny == null || IsBetter(enemy, bestAny, origin, focus, avoid, far))
            {
                bestAny = enemy;
            }
        }

        bool resourceFreeButEnemyTaken = bestFree != null && bestFree.Category == TargetCategory.Resource
            && bestAny != null && bestAny.Category != TargetCategory.Resource;
        if (resourceFreeButEnemyTaken)
        {
            // share the enemy another weapon already took instead of settling for a tree
            bestFree = null;
            keepPrevious = keepPrevious && previous.Category != TargetCategory.Resource;
        }

        if (keepPrevious && (bestFree == null || TierOf(bestFree, origin, avoid) <= TierOf(previous, origin, avoid))
            && !(focus && IsWeakerResource(bestFree, previous)))
        {
            return previous;
        }

        return bestFree != null ? bestFree : bestAny;
    }

    // Counts, for each enemy the weapon can reach, the other reachable enemies within its clusterRadius.
    private void ComputeClusters(IAimedWeapon weapon)
    {
        clusterCounts.Clear();
        clusterEnemies.Clear();

        for (int i = 0; i < Damageable.All.Count; i++)
        {
            Damageable candidate = Damageable.All[i];
            if (candidate.Category != TargetCategory.Resource && IsCandidate(weapon, candidate))
            {
                clusterEnemies.Add(candidate);
            }
        }

        float sqrRadius = weapon.ClusterRadius * weapon.ClusterRadius;
        for (int a = 0; a < clusterEnemies.Count; a++)
        {
            int count = 0;
            Vector3 position = clusterEnemies[a].transform.position;

            for (int b = 0; b < clusterEnemies.Count; b++)
            {
                if (a == b)
                {
                    continue;
                }

                Vector3 offset = clusterEnemies[b].transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= sqrRadius)
                {
                    count++;
                }
            }

            clusterCounts[clusterEnemies[a]] = count;
        }
    }

    // For close-range-avoiding weapons: a target inside closeRange is skipped while something of its kind
    // is available outside it (an enemy inside is also kept over resources outside, as enemies always win).
    private bool SkipForAvoid(Damageable target, Vector3 origin, bool outsideEnemy, bool outsideResource, bool anyEnemy)
    {
        if (!IsInCloseRange(target, origin))
        {
            return false;
        }

        return target.Category == TargetCategory.Resource ? !anyEnemy && outsideResource : outsideEnemy;
    }

    private bool IsCandidate(IAimedWeapon weapon, Damageable enemy)
    {
        if (!weapon.CanTarget(enemy.Category))
        {
            return false;
        }

        Vector3 offset = enemy.transform.position - weapon.Owner.position;
        offset.y = 0f;
        float sqr = offset.sqrMagnitude;
        return sqr <= weapon.Range * weapon.Range || sqr <= closeRange * closeRange;
    }

    private int TierOf(Damageable enemy, Vector3 origin, bool ignoreClose = false)
    {
        if (enemy.Category == TargetCategory.Resource)
        {
            return ResourceTier;
        }

        int tier = (int)enemy.Rank;
        return !ignoreClose && IsInCloseRange(enemy, origin) ? tier + CloseTierBonus : tier;
    }

    // True when both are resources and the candidate is lower in health percentage by the switch margin.
    private static bool IsWeakerResource(Damageable candidate, Damageable current)
    {
        return candidate != null && current != null
            && candidate.Category == TargetCategory.Resource
            && current.Category == TargetCategory.Resource
            && candidate.HealthNormalized + ResourceSwitchMargin < current.HealthNormalized;
    }

    // A resource-focusing weapon may take a resource another weapon already has.
    private static bool IsSharedResource(bool focus, Damageable target)
    {
        return focus && target.Category == TargetCategory.Resource;
    }

    private bool IsBetter(Damageable candidate, Damageable current, Vector3 origin, bool resourcesByPercent = false,
        bool ignoreClose = false, bool preferFar = false)
    {
        int candidateTier = TierOf(candidate, origin, ignoreClose);
        int currentTier = TierOf(current, origin, ignoreClose);
        if (candidateTier != currentTier)
        {
            return candidateTier > currentTier;
        }

        if (clusterActive && candidate.Category != TargetCategory.Resource && current.Category != TargetCategory.Resource)
        {
            int candidateCluster, currentCluster;
            clusterCounts.TryGetValue(candidate, out candidateCluster);
            clusterCounts.TryGetValue(current, out currentCluster);
            if (candidateCluster != currentCluster)
            {
                return candidateCluster > currentCluster;
            }
        }

        bool bothResources = resourcesByPercent && candidate.Category == TargetCategory.Resource
            && current.Category == TargetCategory.Resource;
        if (preferFar && !bothResources)
        {
            Vector3 farCandidate = candidate.transform.position - origin;
            Vector3 farCurrent = current.transform.position - origin;
            farCandidate.y = 0f;
            farCurrent.y = 0f;
            return farCandidate.sqrMagnitude > farCurrent.sqrMagnitude;
        }

        float healthDifference = bothResources
            ? candidate.HealthNormalized - current.HealthNormalized
            : candidate.Health - current.Health;
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
                    if (float.IsNaN(angles[a]) || float.IsNaN(angles[b]) || !weapons[a].UseRing || !weapons[b].UseRing)
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
