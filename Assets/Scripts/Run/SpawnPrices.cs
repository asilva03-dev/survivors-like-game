/*
 * SpawnPrices
 * The spawn director's price list (spawn director step 1, 2026-11-21; user's pick: a threat-budget director). One asset,
 * Assets/Run/SpawnPrices.asset. Every rat type the director can buy has a threat cost; a wave card's budget is spent on
 * them (WaveShopper).
 *
 * - RatType names what the director buys: the basic rat by armor (Bare / Helmet / Plated), Ranged, Bag, Pot, Athletic
 *   and Swarm (one whole pack of mice). ToSpawn maps it to EnemySpawner.Kind + armor layers.
 * - Per entry: cost (threat per rat, or per swarm), pack (how many come together when bought: small rats in packs, big
 *   ones single) and side: Either (follows the light/dark split), BaseOnly (athletic rats: never the dark's share) or
 *   DarkOnly (ranged rats: only the player's share in the dark).
 * - shadowMultiplier: a shadow version costs this x the cost (1.5).
 * - A new rat type joins the director by getting a RatType, a price entry here and a place on the wave cards' menus.
 * - All numbers are placeholders (user, 2026-11-21: "tweak it once most other mechanics are in").
 */

using System;
using UnityEngine;

public enum RatType { Bare, Helmet, Plated, Ranged, Bag, Pot, Athletic, Swarm }

public enum SpawnSide { Either, BaseOnly, DarkOnly }

[CreateAssetMenu(menuName = "TD/Spawn Prices", fileName = "SpawnPrices")]
public class SpawnPrices : ScriptableObject
{
    [Serializable]
    public class Price
    {
        public RatType type;
        [Tooltip("Threat cost of one (a whole pack of mice for Swarm).")]
        [Min(0.1f)] public float cost = 1f;
        [Tooltip("How many come together when bought (min, max).")]
        public Vector2Int pack = new Vector2Int(1, 1);
        [Tooltip("Either = follows the light/dark split; BaseOnly = never comes for the player; DarkOnly = only the player's share in the dark.")]
        public SpawnSide side = SpawnSide.Either;
    }

    [SerializeField] private Price[] prices = new Price[0];
    [Tooltip("A shadow version costs this many times as much.")]
    [SerializeField, Min(1f)] private float shadowMultiplier = 1.5f;

    public float ShadowMultiplier => shadowMultiplier;

    /// <summary>The price entry for a type, or null if it has none (the director can't buy it).</summary>
    public Price Get(RatType type)
    {
        if (prices == null)
        {
            return null;
        }
        for (int i = 0; i < prices.Length; i++)
        {
            if (prices[i] != null && prices[i].type == type)
            {
                return prices[i];
            }
        }
        return null;
    }

    /// <summary>Threat cost of one of `type` (x shadowMultiplier for a shadow). Infinity when it has no price.</summary>
    public float Cost(RatType type, bool shadow)
    {
        Price price = Get(type);
        if (price == null)
        {
            return float.PositiveInfinity;
        }
        return price.cost * (shadow ? shadowMultiplier : 1f);
    }

    /// <summary>The EnemySpawner kind and armor layers a RatType spawns as.</summary>
    public static void ToSpawn(RatType type, out EnemySpawner.Kind kind, out int armorLayers)
    {
        armorLayers = 0;
        switch (type)
        {
            case RatType.Helmet: kind = EnemySpawner.Kind.Basic; armorLayers = 1; break;
            case RatType.Plated: kind = EnemySpawner.Kind.Basic; armorLayers = 2; break;
            case RatType.Ranged: kind = EnemySpawner.Kind.Ranged; break;
            case RatType.Bag: kind = EnemySpawner.Kind.Bag; break;
            case RatType.Pot: kind = EnemySpawner.Kind.Pot; break;
            case RatType.Athletic: kind = EnemySpawner.Kind.Athletic; break;
            case RatType.Swarm: kind = EnemySpawner.Kind.Swarm; break;
            default: kind = EnemySpawner.Kind.Basic; break;
        }
    }
}
