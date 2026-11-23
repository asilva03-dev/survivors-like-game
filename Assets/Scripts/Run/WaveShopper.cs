/*
 * WaveShopper
 * The spawn director's shopping (spawn director step 1, 2026-11-21): turns one wave card (RunTimeline.Wave) into a list
 * of packs to release, using the SpawnPrices costs. Pure logic, no scene: the timeline inspector's Roll preview and (from
 * step 2) RunDirector both call Buy.
 *
 * - Budget: threatBudget rolled within +- budgetVariance.
 * - 1. Must-includes are bought first (count rats of each, shadow as set), even past the budget; they count toward the
 *   limits. 2. Shadows: menu items are bought as shadows until minShadowThreat of shadow threat is reached (only while
 *   affordable). 3. The rest: a weighted random menu item at a time, as a pack of a random size within its price's pack
 *   range (shrunk to what the budget and limits allow), a shadow with the card's shadowChance (if that's affordable),
 *   until nothing on the menu fits in what's left.
 * - Limits count rats (a Swarm counts as one). A type with no price can't be bought (skipped).
 * - The result is in buying order; the director shuffles it before releasing.
 */

using System.Collections.Generic;
using UnityEngine;

public static class WaveShopper
{
    public struct Pack
    {
        public RatType type;
        public bool shadow;
        public int count;
        public float cost;
    }

    public class Result
    {
        public readonly List<Pack> packs = new List<Pack>();
        public float budget;
        public float spent;
    }

    private const int MaxBuys = 1000;

    /// <summary>Spends one wave card's budget (see the header).</summary>
    public static Result Buy(RunTimeline.Wave card, SpawnPrices prices)
    {
        Result result = new Result();
        if (card == null || prices == null)
        {
            return result;
        }
        result.budget = Mathf.Max(0f, card.threatBudget * (1f + Random.Range(-card.budgetVariance, card.budgetVariance)));
        Dictionary<RatType, int> bought = new Dictionary<RatType, int>();

        if (card.mustInclude != null)
        {
            foreach (RunTimeline.MustInclude must in card.mustInclude)
            {
                if (must != null && prices.Get(must.type) != null)
                {
                    Add(result, bought, prices, must.type, must.shadow, must.count);
                }
            }
        }

        float shadowThreat = 0f;
        for (int guard = 0; guard < MaxBuys && shadowThreat < card.minShadowThreat; guard++)
        {
            RatType type;
            if (!PickAffordable(card, prices, bought, result.budget - result.spent, true, out type))
            {
                break;
            }
            int count = PackSize(card, prices, bought, type, true, result.budget - result.spent);
            shadowThreat += Add(result, bought, prices, type, true, count);
        }

        for (int guard = 0; guard < MaxBuys; guard++)
        {
            float left = result.budget - result.spent;
            RatType type;
            if (!PickAffordable(card, prices, bought, left, false, out type))
            {
                break;
            }
            bool shadow = card.shadowChance > 0f && Random.value < card.shadowChance && prices.Cost(type, true) <= left;
            int count = PackSize(card, prices, bought, type, shadow, left);
            Add(result, bought, prices, type, shadow, count);
        }
        return result;
    }

    private static float Add(Result result, Dictionary<RatType, int> bought, SpawnPrices prices, RatType type, bool shadow, int count)
    {
        float cost = prices.Cost(type, shadow) * count;
        result.packs.Add(new Pack { type = type, shadow = shadow, count = count, cost = cost });
        result.spent += cost;
        int had;
        bought.TryGetValue(type, out had);
        bought[type] = had + count;
        return cost;
    }

    // How many more of `type` the limits allow (int.MaxValue = no limit).
    private static int Room(RunTimeline.Wave card, Dictionary<RatType, int> bought, RatType type)
    {
        if (card.limits == null)
        {
            return int.MaxValue;
        }
        int room = int.MaxValue;
        foreach (RunTimeline.Limit limit in card.limits)
        {
            if (limit != null && limit.type == type)
            {
                int had;
                bought.TryGetValue(type, out had);
                room = Mathf.Min(room, limit.max - had);
            }
        }
        return room;
    }

    // A weighted random menu item that one of fits in `left` and the limits.
    private static bool PickAffordable(RunTimeline.Wave card, SpawnPrices prices, Dictionary<RatType, int> bought, float left,
        bool shadow, out RatType type)
    {
        type = RatType.Bare;
        if (card.menu == null)
        {
            return false;
        }
        float total = 0f;
        foreach (RunTimeline.MenuItem item in card.menu)
        {
            if (Fits(card, prices, bought, item, left, shadow))
            {
                total += item.weight;
            }
        }
        if (total <= 0f)
        {
            return false;
        }
        float roll = Random.value * total;
        foreach (RunTimeline.MenuItem item in card.menu)
        {
            if (!Fits(card, prices, bought, item, left, shadow))
            {
                continue;
            }
            type = item.type;
            roll -= item.weight;
            if (roll < 0f)
            {
                return true;
            }
        }
        return true;
    }

    private static bool Fits(RunTimeline.Wave card, SpawnPrices prices, Dictionary<RatType, int> bought,
        RunTimeline.MenuItem item, float left, bool shadow)
    {
        return item != null && item.weight > 0f && prices.Cost(item.type, shadow) <= left + 0.0001f
            && Room(card, bought, item.type) > 0;
    }

    private static int PackSize(RunTimeline.Wave card, SpawnPrices prices, Dictionary<RatType, int> bought, RatType type,
        bool shadow, float left)
    {
        SpawnPrices.Price price = prices.Get(type);
        int min = Mathf.Max(1, price.pack.x);
        int max = Mathf.Max(min, price.pack.y);
        int count = Random.Range(min, max + 1);
        int affordable = Mathf.FloorToInt((left + 0.0001f) / prices.Cost(type, shadow));
        return Mathf.Max(1, Mathf.Min(count, Mathf.Min(affordable, Room(card, bought, type))));
    }

    /// <summary>A one-line summary like "Bare x12, Helmet x4, Pot x1 (shadow)", for previews and the debug line.</summary>
    public static string Describe(Result result)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        List<string> order = new List<string>();
        foreach (Pack pack in result.packs)
        {
            string key = pack.type + (pack.shadow ? " (shadow)" : "");
            int had;
            if (!counts.TryGetValue(key, out had))
            {
                order.Add(key);
            }
            counts[key] = had + pack.count;
        }
        List<string> parts = new List<string>();
        foreach (string key in order)
        {
            parts.Add(key + " x" + counts[key]);
        }
        return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing";
    }
}
