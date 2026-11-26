/*
 * FishercatModelBuilder
 * Procedural low-poly model of the Fishercat tower (tower 3, 2026-11-24, user's sketch from behind): a cat in a blue
 * fisherman (bucket) hat sitting on a small broken-off piece of dock above a patch of water, a barrel of spears beside it
 * and an open cooler behind it. Same faceted style and helpers as TrapperModelBuilder.
 *
 * - Footprint 2x2 (corner tile (0, 0) + (1, 0), (0, -1), (1, -1)): every part is in the tower root's space (root = the
 *   corner tile's centre), centred on the footprint's middle (0.5, -0.5). The tower's front is +Z (at rotation 0 the game
 *   camera sees it from behind, like the sketch).
 * - Water (flat, wavy-edged patch over the 4 tiles; ripples animated by FishercatWater), the dock (plank deck at DeckTop 1.5 = fence level on
 *   four log posts, one taller mooring post, uneven plank ends) and the cooler (red body, white lid flipped open, ice and a
 *   fish tail inside; since 2026-11-25 blue ice packs (IcePack submesh) and loose ice cubes around the fish) are static
 *   (BuildProps). The barrel (staves, dark bands, three spears standing in it) is its own mesh
 *   (BuildBarrel, pivot at its bottom centre, placed at BarrelSpot) so the throw animation can rattle it when the cat
 *   rummages for a spear (2026-11-24).
 * - The cat (BuildCat, earsOut or ears under the hat) is the part that turns; paws are separate (BuildHand), like the
 *   Trapper. The spear (BuildSpear) points along +Z, its middle at the origin, tip at +SpearLength/2.
 * - Units: world units; the cat is built at the Trapper's size (r 0.38) and scaled on the prefab.
 * - Upgrade parts (2026-11-26, BuildUpgradeParts): the cooler left the props (base tower has none; upgrades add it, lid its
 *   own mesh so it can shut), the chunky blue spear gun with a shark-mouth muzzle + its black shark-camo variant, the open spear case (replaces the barrel) and aviators.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FishercatModelBuilder
{
    public const float DeckTop = 1.5f;
    public static readonly Vector3 Centre = new Vector3(0.5f, 0f, -0.5f);
    public static readonly Vector3 CatSpot = new Vector3(0.3f, DeckTop, -0.15f);
    public const float SpearLength = 1.5f;
    /// <summary>How far the water reaches from the footprint's middle (user: further than the 2x2 tiles).</summary>
    public const float WaterRadius = 1.2f;

    // Cat submeshes.
    public const int Fur = 0, FurAccent = 1, Dark = 2, Hat = 3, HatBand = 4, CatSubmeshes = 5;
    // Prop submeshes.
    public const int Plank = 0, PlankDark = 1, Post = 2, BarrelWood = 3, PropDark = 4, CoolerBody = 5, CoolerWhite = 6,
        SpearWood = 7, Metal = 8, Fish = 9, Ice = 10, IcePack = 11, PropSubmeshes = 12;
    // Water submeshes.
    public const int Water = 0, Ripple = 1, WaterSubmeshes = 2;
    // Spear submeshes.
    public const int SpearShaft = 0, SpearHead = 1, SpearCollar = 2, SpearSubmeshes = 3;

    public const string BodyPath = "Assets/Models/FishercatBody.asset";
    public const string HandPath = "Assets/Models/FishercatHand.asset";
    public const string PropsPath = "Assets/Models/FishercatProps.asset";
    public const string WaterPath = "Assets/Models/FishercatWater.asset";
    public const string SpearPath = "Assets/Models/FishercatSpear.asset";
    public const string BarrelPath = "Assets/Models/FishercatBarrel.asset";
    public static readonly Vector3 BarrelSpot = new Vector3(1.08f, DeckTop, -0.22f);
    public const float BarrelHeight = 0.65f;

    // Upgrade parts (Path 1, 2026-11-26). The cooler left the props mesh: the base tower has none.
    public const string CoolerPath = "Assets/Models/FishercatCooler.asset";
    public const string CoolerLidPath = "Assets/Models/FishercatCoolerLid.asset";
    public const string SpearGunPath = "Assets/Models/FishercatSpearGun.asset";
    public const string SharkGunPath = "Assets/Models/FishercatSharkGun.asset";
    public const string SpearCasePath = "Assets/Models/FishercatSpearCase.asset";
    public const string AviatorsPath = "Assets/Models/FishercatAviators.asset";
    public static readonly Vector3 CoolerSpot = new Vector3(0.5f, DeckTop, -0.93f);
    public const float CoolerYaw = -8f;
    public static readonly Vector3 CoolerSize = new Vector3(0.8f, 0.45f, 0.5f);
    /// <summary>The lid's hinge in the cooler's space; open = Euler(-105, 0, 0).</summary>
    public static readonly Vector3 CoolerHinge = new Vector3(0f, 0.47f, -0.25f);
    public static readonly Vector3 CaseSize = new Vector3(0.46f, 0.6f, 0.36f);
    // Spear gun submeshes (materials FishercatGunMouth, FishercatGunBlue, FishercatGunBlack, CoolerWhite, FishercatGunShark).
    public const int GunMouth = 0, GunBody = 1, GunDark = 2, GunWhite = 3, GunShark = 4, GunSubmeshes = 5;
    // Aviator submeshes (TurretDark lenses, TrapMetal frame).
    public const int AviatorLens = 0, AviatorFrame = 1, AviatorSubmeshes = 2;

    // Path 2 (nets, 2026-11-26).
    public const string NetPath = "Assets/Models/FishercatNet.asset";
    public const string SpikedNetPath = "Assets/Models/FishercatNetSpiked.asset";
    public const string NetBundlePath = "Assets/Models/FishercatNetBundle.asset";
    public const string NetChestPath = "Assets/Models/FishercatNetChest.asset";
    public const string NetChestBandsPath = "Assets/Models/FishercatNetChestBands.asset";
    public const string WhirlpoolPath = "Assets/Models/FishercatWhirlpool.asset";
    /// <summary>The net is built with radius 1 (scaled per tier); its middle rises NetDome above the rim.</summary>
    public const float NetDome = 0.22f;
    public static readonly Vector3 ChestSize = new Vector3(0.56f, 0.34f, 0.4f);
    // Net submeshes (rope, lead weights, spikes) and chest submeshes (wood, dark trim, rope, weights, metal).
    public const int NetRope = 0, NetWeight = 1, NetSpike = 2, NetSubmeshes = 3;
    public const int ChestWood = 0, ChestDark = 1, ChestRope = 2, ChestWeight = 3, ChestMetal = 4, ChestSubmeshes = 5;

    // Path 3 (water, 2026-11-26).
    public const string PoleStandPath = "Assets/Models/FishercatPoleStand.asset";
    public const string PoleStandFancyPath = "Assets/Models/FishercatPoleStandFancy.asset";
    public const string PoleStandIndustrialPath = "Assets/Models/FishercatPoleStandIndustrial.asset";
    public const string PoolPath = "Assets/Models/FishercatPool.asset";
    public const string PoleRodPath = "Assets/Models/FishercatPoleRod.asset";
    public const string PoleRodFancyPath = "Assets/Models/FishercatPoleRodFancy.asset";
    public const string PoleRodIndustrialPath = "Assets/Models/FishercatPoleRodIndustrial.asset";
    /// <summary>The rod's butt in the stand's space (the rod pivots in the cat's paws around here).</summary>
    public static readonly Vector3 PoleButt = new Vector3(0f, 0.42f, -0.5f);
    public const string BobberPath = "Assets/Models/FishercatBobber.asset";
    public const string SharkPath = "Assets/Models/FishercatShark.asset";
    public const string BigSharkPath = "Assets/Models/FishercatBigShark.asset";
    public const string BigSharkJawPath = "Assets/Models/FishercatBigSharkJaw.asset";
    /// <summary>The great white's mouth corner (its lower jaw's hinge), in the head's space (puddle radius 1).</summary>
    public static readonly Vector3 BigSharkHinge = new Vector3(-0.15f, 0.55f, 0f);
    /// <summary>Where the pole's line leaves the rod tip, in the stand's space (the stand yaws to face its pool).</summary>
    public static readonly Vector3 PoleTip = new Vector3(0f, 1.75f, 1.45f);
    // Pole stand submeshes (wood, dark, metal, accent = gold / hazard yellow, cork, black, red, white).
    public const int PoleWood = 0, PoleDark = 1, PoleMetal = 2, PoleAccent = 3, PoleCork = 4, PoleBlack = 5, PoleRed = 6, PoleWhite = 7, PoleSubmeshes = 8;
    // Pool submeshes (water, foam ring). Shark submeshes (top, belly, dark, teeth, mouth).
    public const int PoolWater = 0, PoolFoam = 1, PoolSubmeshes = 2;
    public const int SharkTop = 0, SharkBelly = 1, SharkDark = 2, SharkTeeth = 3, SharkMouth = 4, SharkSubmeshes = 5;

    /// <summary>Writes every Fishercat mesh in place (user's pick 2026-11-24: B, cream cat, ears under the hat).</summary>
    [MenuItem("Tools/Models/Turret/Build Fishercat")]
    public static void BuildFishercat()
    {
        Save(BuildCat(false), BodyPath);
        Save(BuildHand(), HandPath);
        Save(BuildProps(), PropsPath);
        Save(BuildWater(), WaterPath);
        Save(BuildSpear(), SpearPath);
        Save(BuildBarrel(), BarrelPath);
        BuildUpgradeParts();
    }

    /// <summary>Writes the upgrade-only meshes (cooler + lid, spear gun, shark gun, spear case, aviators).</summary>
    [MenuItem("Tools/Models/Turret/Build Fishercat Upgrade Parts")]
    public static void BuildUpgradeParts()
    {
        Save(BuildCooler(), CoolerPath);
        Save(BuildCoolerLid(), CoolerLidPath);
        Save(BuildSpearGun(false), SpearGunPath);
        Save(BuildSpearGun(true), SharkGunPath);
        Save(BuildSpearCase(), SpearCasePath);
        Save(BuildAviators(), AviatorsPath);
        Save(BuildNet(false), NetPath);
        Save(BuildNet(true), SpikedNetPath);
        Save(BuildNetBundle(), NetBundlePath);
        Save(BuildNetChest(), NetChestPath);
        Save(BuildNetChestBands(), NetChestBandsPath);
        Save(BuildWhirlpool(), WhirlpoolPath);
        // Since the Path 3 logistics the stand meshes hold the stand only; the rods are their own meshes (picked up by the cat).
        Save(BuildPoleStand(0, true, false), PoleStandPath);
        Save(BuildPoleStand(1, true, false), PoleStandFancyPath);
        Save(BuildPoleStand(2, true, false), PoleStandIndustrialPath);
        Save(BuildPoleStand(0, false, true), PoleRodPath);
        Save(BuildPoleStand(1, false, true), PoleRodFancyPath);
        Save(BuildPoleStand(2, false, true), PoleRodIndustrialPath);
        Save(BuildPool(), PoolPath);
        Save(BuildBobber(), BobberPath);
        Save(BuildShark(false), SharkPath);
        Save(BuildBigSharkHead(false), BigSharkPath);
        Save(BuildBigSharkHead(true), BigSharkJawPath);
    }

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<int>[] Triangles = new List<int>[16];

    // ---- The cat.

    /// <summary>The cat in its fisherman hat, feet at the origin, facing +Z (no paws: BuildHand).</summary>
    public static Mesh BuildCat(bool earsOut)
    {
        Begin();
        float r = 0.38f;
        Vector3 c = new Vector3(0f, r * 0.98f, 0f);
        Ball(Fur, c, new Vector3(r, r * 0.95f, r * 0.9f), 10, true);
        for (int side = -1; side <= 1; side += 2)
        {
            if (earsOut)
            {
                // Ears poking up through the hat's sides.
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * -24f);
                Vector3 ear = c + new Vector3(side * r * 0.56f, r * 0.62f, -r * 0.05f);
                Frustum(Fur, ear, tilt, r * 0.26f, 0.005f, r * 0.62f, 4);
                Frustum(FurAccent, ear + new Vector3(side * -0.01f, r * 0.12f, r * 0.11f), tilt, r * 0.14f, 0.005f, r * 0.42f, 4);
            }
            // Calm eyes, cheek puffs, whiskers.
            Box(Dark, c + new Vector3(side * r * 0.3f, r * 0.18f, r * 0.84f), new Vector3(r * 0.13f, r * 0.13f, r * 0.08f), Quaternion.identity);
            Ball(FurAccent, c + new Vector3(side * r * 0.14f, -r * 0.08f, r * 0.82f), new Vector3(r * 0.17f, r * 0.13f, r * 0.12f), 6, true);
            for (int w = 0; w < 2; w++)
            {
                Box(Dark, c + new Vector3(side * r * 0.62f, -r * (0.04f + w * 0.12f), r * 0.68f), new Vector3(r * 0.28f, r * 0.025f, r * 0.03f),
                    Quaternion.Euler(0f, side * -30f, side * (w == 0 ? 6f : -6f)));
            }
        }
        Box(Dark, c + new Vector3(0f, r * 0.02f, r * 0.93f), new Vector3(r * 0.1f, r * 0.07f, r * 0.05f), Quaternion.identity);
        // Short tail curled round the side.
        Segment(Fur, new Vector3(-r * 0.7f, r * 0.12f, -r * 0.35f), new Vector3(-r * 0.95f, r * 0.1f, r * 0.2f), r * 0.2f, r * 0.15f);

        // Blue bucket hat: sloped brim, crown, darker band.
        Frustum(Hat, c + new Vector3(0f, r * 0.48f, 0f), Quaternion.Euler(-6f, 0f, 0f), new Vector2(r * 1.08f, r * 1.02f), new Vector2(r * 0.74f, r * 0.7f), r * 0.16f, 12);
        Frustum(HatBand, c + new Vector3(0f, r * 0.62f, -r * 0.02f), Quaternion.identity, new Vector2(r * 0.75f, r * 0.71f), new Vector2(r * 0.72f, r * 0.68f), r * 0.11f, 12);
        Frustum(Hat, c + new Vector3(0f, r * 0.72f, -r * 0.02f), Quaternion.identity, new Vector2(r * 0.72f, r * 0.68f), new Vector2(r * 0.56f, r * 0.52f), r * 0.32f, 12);
        return Finish("FishercatBody", CatSubmeshes);
    }

    /// <summary>One round paw (world units, centred; not scaled with the cat).</summary>
    public static Mesh BuildHand()
    {
        Begin();
        Ball(FurAccent, Vector3.zero, new Vector3(0.11f, 0.09f, 0.12f), 8, true);
        return Finish("FishercatHand", CatSubmeshes);
    }

    // ---- The spear (user's diagram: long thin shaft, a collar near the back, a pointed head with one barb sticking up).

    /// <summary>One spear along +Z, middle at the origin, the barb pointing up (+Y).</summary>
    public static Mesh BuildSpear()
    {
        Begin();
        Spear(SpearShaft, SpearHead, SpearCollar, new Vector3(0f, 0f, -SpearLength * 0.5f), Vector3.forward, Vector3.up, SpearLength);
        return Finish("FishercatSpear", SpearSubmeshes);
    }

    private static void Spear(int wood, int metal, int collar, Vector3 butt, Vector3 dir, Vector3 up, float length)
    {
        dir = dir.normalized;
        up = Vector3.ProjectOnPlane(up, dir).normalized;
        Vector3 headBase = butt + dir * (length * 0.82f);
        Segment(wood, butt, headBase, 0.04f, 0.045f);
        Segment(collar, butt + dir * (length * 0.3f), butt + dir * (length * 0.36f), 0.065f, 0.065f);
        // Head: a socket, then a four-sided point.
        Frustum(metal, headBase, Quaternion.FromToRotation(Vector3.up, dir), 0.035f, 0.05f, length * 0.04f, 4);
        Frustum(metal, headBase + dir * (length * 0.04f), Quaternion.FromToRotation(Vector3.up, dir), 0.055f, 0.004f, length * 0.14f, 4);
        // The barb: a thin swept-back fin rising from the head's base.
        Vector3 a = headBase + dir * (length * 0.02f);
        Vector3 b = headBase + dir * (length * 0.08f);
        Vector3 tip = headBase + dir * (length * 0.035f) + up * (length * 0.1f);
        Fin(metal, a, b, tip, Vector3.Cross(dir, up) * 0.012f);
    }

    // A thin triangular plate (two faces and edges) between a, b and tip, `side` = half its thickness.
    private static void Fin(int material, Vector3 a, Vector3 b, Vector3 tip, Vector3 side)
    {
        Vector3 mid = (a + b + tip) / 3f;
        AddPoly(material, new[] { a + side, b + side, tip + side }, mid);
        AddPoly(material, new[] { a - side, b - side, tip - side }, mid);
        AddPoly(material, new[] { a + side, a - side, tip - side, tip + side }, mid);
        AddPoly(material, new[] { b + side, b - side, tip - side, tip + side }, mid);
    }

    // ---- Water.

    /// <summary>A flat wavy-edged patch of water over the 2x2 footprint (root space); ripples: FishercatWater.</summary>
    public static Mesh BuildWater()
    {
        Begin();
        int n = 28;
        Vector3[] ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float wobble = 0.06f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 5f + 2.1f);
            float rx = WaterRadius + wobble;
            float rz = WaterRadius + wobble;
            ring[i] = Centre + new Vector3(Mathf.Cos(a) * rx, 0.025f, Mathf.Sin(a) * rz);
        }
        Vector3 centre = Centre + new Vector3(0f, 0.025f, 0f);
        for (int i = 0; i < n; i++)
        {
            AddPolyUp(Water, new[] { centre, ring[(i + 1) % n], ring[i] });
        }
        // The ripple rings are animated since 2026-11-25 (Turrets/FishercatWater.cs, same five spots); the Ripple submesh
        // stays empty.
        return Finish("FishercatWater", WaterSubmeshes);
    }

    // ---- Dock, barrel, cooler.

    /// <summary>The static props in the tower root's space: dock section and open cooler (the barrel: BuildBarrel).</summary>
    public static Mesh BuildProps()
    {
        Begin();
        Dock();
        // The cooler is its own mesh since 2026-11-26 (BuildCooler / BuildCoolerLid): the base tower has none, upgrades add it.
        return Finish("FishercatProps", PropSubmeshes);
    }

    /// <summary>The open cooler without its lid, bottom centre at the origin, unrotated (prop submeshes); placed at CoolerSpot.</summary>
    public static Mesh BuildCooler()
    {
        Begin();
        Cooler(Vector3.zero, 0f);
        return Finish("FishercatCooler", PropSubmeshes);
    }

    /// <summary>The cooler lid, pivot on its hinge (CoolerHinge in the cooler's space), closed at rotation 0, lying along +Z.</summary>
    public static Mesh BuildCoolerLid()
    {
        Begin();
        Box(CoolerWhite, new Vector3(0f, 0.025f, CoolerSize.z * 0.5f), new Vector3(CoolerSize.x + 0.02f, 0.05f, CoolerSize.z + 0.02f), Quaternion.identity);
        return Finish("FishercatCoolerLid", PropSubmeshes);
    }

    /// <summary>
    /// Path 1 tier 3+ (user's sketch, 2026-11-26; made chunkier the same day): a beefy spear gun held at the hip, origin at
    /// the grip, muzzle along +Z. Blue body (tall stock, big receiver, a stepped front section), black top rail with a sight,
    /// black grip and trigger guard; the muzzle is an open shark mouth (upper and lower jaws hinged open, pink throat; teeth only
    /// on the shark camo), plus wider parts for texture (cheek rest, chamber bulge, collar, foregrip). The loaded spears are separate children. shark = the tier 5 camo (user's second sketch): the body
    /// mainly black, an eye on each side and a row of shark teeth painted along the sides of the front section.
    /// </summary>
    public static Mesh BuildSpearGun(bool shark)
    {
        Begin();
        int body = shark ? GunShark : GunBody;
        // Stock (tall, angled), its butt plate, grip and trigger guard.
        Box(body, new Vector3(0f, -0.02f, -0.42f), new Vector3(0.14f, 0.26f, 0.36f), Quaternion.Euler(5f, 0f, 0f));
        Box(GunDark, new Vector3(0f, -0.04f, -0.61f), new Vector3(0.15f, 0.27f, 0.035f), Quaternion.Euler(5f, 0f, 0f));
        Box(GunDark, new Vector3(0f, -0.15f, -0.08f), new Vector3(0.1f, 0.2f, 0.11f), Quaternion.Euler(-18f, 0f, 0f));
        Box(GunDark, new Vector3(0f, -0.11f, 0.07f), new Vector3(0.04f, 0.07f, 0.16f), Quaternion.identity);
        // Receiver, the stepped front section, the black top rail with a sight.
        Box(body, new Vector3(0f, 0.05f, 0.08f), new Vector3(0.18f, 0.24f, 0.62f), Quaternion.identity);
        Box(body, new Vector3(0f, 0.04f, 0.55f), new Vector3(0.17f, 0.2f, 0.32f), Quaternion.identity);
        Box(GunDark, new Vector3(0f, 0.2f, 0.1f), new Vector3(0.11f, 0.07f, 0.44f), Quaternion.identity);
        Box(GunDark, new Vector3(0f, 0.25f, 0.24f), new Vector3(0.04f, 0.05f, 0.06f), Quaternion.identity);
        // Wider parts for texture (user, 2026-11-26): a cheek rest on the stock, a chamber bulge on the receiver, a black
        // collar where the front section steps down, and a black foregrip block under the front for the other paw.
        Box(body, new Vector3(0f, 0.1f, -0.4f), new Vector3(0.18f, 0.08f, 0.22f), Quaternion.Euler(5f, 0f, 0f));
        Box(body, new Vector3(0f, 0.03f, -0.02f), new Vector3(0.23f, 0.17f, 0.2f), Quaternion.identity);
        Box(GunDark, new Vector3(0f, 0.045f, 0.385f), new Vector3(0.21f, 0.24f, 0.06f), Quaternion.identity);
        Box(GunDark, new Vector3(0f, -0.09f, 0.54f), new Vector3(0.12f, 0.07f, 0.18f), Quaternion.identity);
        // The open shark mouth: throat, jaws hinged at the front section's face, teeth on both.
        Box(GunMouth, new Vector3(0f, 0.04f, 0.705f), new Vector3(0.14f, 0.12f, 0.03f), Quaternion.identity);
        for (int j = 0; j < 2; j++)
        {
            bool upper = j == 0;
            Vector3 hinge = new Vector3(0f, upper ? 0.105f : -0.025f, 0.69f);
            Quaternion rot = Quaternion.Euler(upper ? -20f : 18f, 0f, 0f);
            float len = upper ? 0.24f : 0.22f;
            Box(body, hinge + rot * new Vector3(0f, 0f, len * 0.5f), new Vector3(0.17f, 0.07f, len), rot);
            Box(GunMouth, hinge + rot * new Vector3(0f, upper ? -0.03f : 0.03f, len * 0.45f), new Vector3(0.12f, 0.012f, len * 0.8f), rot);
            if (!shark)
            {
                continue; // the blue gun's mouth has no teeth (user, 2026-11-26)
            }
            float face = upper ? -0.035f : 0.035f;
            float point = upper ? -0.05f : 0.05f;
            for (int i = 0; i < 4; i++)
            {
                float x = -0.06f + i * 0.04f;
                float z = len - 0.03f;
                Fin(GunWhite, hinge + rot * new Vector3(x - 0.016f, face, z), hinge + rot * new Vector3(x + 0.016f, face, z),
                    hinge + rot * new Vector3(x, face + point, z + 0.005f), rot * new Vector3(0f, 0f, 0.008f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    float z = 0.08f + i * 0.065f;
                    float x = s * 0.07f;
                    Fin(GunWhite, hinge + rot * new Vector3(x, face, z - 0.016f), hinge + rot * new Vector3(x, face, z + 0.016f),
                        hinge + rot * new Vector3(x, face + point * 0.9f, z), rot * new Vector3(0.008f, 0f, 0f));
                }
            }
        }
        if (shark)
        {
            // An eye on each side, and a grin of shark teeth painted along the front section's sides.
            for (int s = -1; s <= 1; s += 2)
            {
                Box(GunWhite, new Vector3(s * 0.091f, 0.11f, 0.36f), new Vector3(0.01f, 0.05f, 0.055f), Quaternion.identity);
                Box(GunDark, new Vector3(s * 0.095f, 0.11f, 0.365f), new Vector3(0.01f, 0.03f, 0.03f), Quaternion.identity);
                Vector3 paint = new Vector3(0.004f, 0f, 0f);
                float x = s * 0.089f;
                for (int i = 0; i < 5; i++)
                {
                    float z = 0.44f + i * 0.06f;
                    Fin(GunWhite, new Vector3(x, 0.095f, z - 0.025f), new Vector3(x, 0.095f, z + 0.025f), new Vector3(x, 0.04f, z), paint);
                    float z2 = z + 0.03f;
                    if (i < 4)
                    {
                        Fin(GunWhite, new Vector3(x, -0.015f, z2 - 0.025f), new Vector3(x, -0.015f, z2 + 0.025f), new Vector3(x, 0.035f, z2), paint);
                    }
                }
            }
        }
        return Finish(shark ? "FishercatSharkGun" : "FishercatSpearGun", GunSubmeshes);
    }

    /// <summary>
    /// Path 1 tier 3+ (user's sketch): the open spear case replacing the barrel, bottom centre at the origin (prop submeshes):
    /// an open-topped wooden box with a dark band round its middle and two bundles of three spears tied together.
    /// </summary>
    public static Mesh BuildSpearCase()
    {
        Begin();
        Vector3 size = CaseSize;
        float t = 0.035f;
        Box(BarrelWood, new Vector3(0f, t * 0.5f, 0f), new Vector3(size.x, t, size.z), Quaternion.identity);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(BarrelWood, new Vector3(s * (size.x - t) * 0.5f, size.y * 0.5f, 0f), new Vector3(t, size.y, size.z), Quaternion.identity);
            Box(BarrelWood, new Vector3(0f, size.y * 0.5f, s * (size.z - t) * 0.5f), new Vector3(size.x - 2f * t, size.y, t), Quaternion.identity);
            for (int u = -1; u <= 1; u += 2)
            {
                Box(PlankDark, new Vector3(s * (size.x * 0.5f - 0.01f), size.y * 0.5f, u * (size.z * 0.5f - 0.01f)), new Vector3(0.05f, size.y + 0.01f, 0.05f), Quaternion.identity);
            }
        }
        Box(PropDark, new Vector3(0f, size.y * 0.52f, 0f), new Vector3(size.x + 0.025f, 0.06f, size.z + 0.025f), Quaternion.identity);
        Box(PropDark, new Vector3(0f, size.y - 0.02f, 0f), new Vector3(size.x + 0.02f, 0.035f, size.z + 0.02f), Quaternion.identity);
        // Two bundles of three, each tied round the middle.
        for (int b = 0; b < 2; b++)
        {
            Vector3 foot = new Vector3(b == 0 ? -0.1f : 0.1f, 0.05f, b == 0 ? 0.03f : -0.03f);
            Vector3 lean = new Vector3(b == 0 ? -0.08f : 0.08f, 1f, b == 0 ? 0.04f : -0.03f).normalized;
            Vector3[] offs = { new Vector3(-0.035f, 0f, 0.02f), new Vector3(0.035f, 0f, 0.02f), new Vector3(0f, 0f, -0.04f) };
            for (int i = 0; i < 3; i++)
            {
                Spear(SpearWood, Metal, PropDark, foot + offs[i], lean, Vector3.forward, SpearLength * 0.95f);
            }
            Frustum(PropDark, foot + lean * (size.y + 0.12f), Quaternion.FromToRotation(Vector3.up, lean), 0.07f, 0.07f, 0.05f, 8);
        }
        return Finish("FishercatSpearCase", PropSubmeshes);
    }

    /// <summary>Path 1 tier 5: aviator sunglasses in the cat body's space (built at the body's size, prefab scale like the body).</summary>
    public static Mesh BuildAviators()
    {
        Begin();
        float r = 0.38f;
        Vector3 c = new Vector3(0f, r * 0.98f, 0f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 lens = c + new Vector3(side * r * 0.3f, r * 0.15f, r * 0.9f);
            Ball(AviatorLens, lens, new Vector3(r * 0.21f, r * 0.17f, r * 0.04f), 8, true);
            Box(AviatorFrame, lens + new Vector3(0f, r * 0.17f, 0f), new Vector3(r * 0.44f, r * 0.035f, r * 0.04f), Quaternion.identity);
            Segment(AviatorFrame, c + new Vector3(side * r * 0.5f, r * 0.31f, r * 0.84f), c + new Vector3(side * r * 0.86f, r * 0.3f, r * 0.15f), r * 0.035f, r * 0.035f);
        }
        Box(AviatorFrame, c + new Vector3(0f, r * 0.27f, r * 0.93f), new Vector3(r * 0.2f, r * 0.03f, r * 0.03f), Quaternion.identity);
        return Finish("FishercatAviators", AviatorSubmeshes);
    }

    /// <summary>The barrel of spears, bottom centre at the origin (prop submeshes); placed at BarrelSpot.</summary>
    public static Mesh BuildBarrel()
    {
        Begin();
        Barrel(Vector3.zero);
        return Finish("FishercatBarrel", PropSubmeshes);
    }

    private static void Dock()
    {
        float minX = -0.2f, maxX = 1.22f, minZ = -1.2f, maxZ = 0.3f;
        float thick = 0.07f;
        // Planks run along X; their east ends are uneven (a broken-off piece of a pier).
        int count = 7;
        float width = (maxZ - minZ) / count;
        float[] endJitter = { 0.04f, -0.12f, 0.02f, 0.1f, -0.06f, 0.05f, -0.16f };
        for (int i = 0; i < count; i++)
        {
            float z = minZ + width * (i + 0.5f);
            float x1 = maxX + endJitter[i];
            Box(i % 3 == 1 ? PlankDark : Plank, new Vector3((minX + x1) * 0.5f, DeckTop - thick * 0.5f, z),
                new Vector3(x1 - minX, thick, width - 0.025f), Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 0.6f : -0.6f)));
        }
        // Beams under the planks, along Z.
        for (int b = 0; b < 2; b++)
        {
            float x = b == 0 ? -0.05f : 1.05f;
            Box(Post, new Vector3(x, DeckTop - thick - 0.06f, (minZ + maxZ) * 0.5f), new Vector3(0.12f, 0.12f, maxZ - minZ - 0.05f), Quaternion.identity);
        }
        // Four log posts into the water; the north-east one stands up as a mooring post with a rope band.
        Vector3[] posts = { new Vector3(-0.08f, 0f, 0.2f), new Vector3(1.08f, 0f, 0.2f), new Vector3(-0.08f, 0f, -1.08f), new Vector3(1.08f, 0f, -1.08f) };
        for (int i = 0; i < posts.Length; i++)
        {
            float top = i == 1 ? DeckTop + 0.35f : DeckTop - thick;
            Frustum(Post, posts[i] + Vector3.down * 0.05f, Quaternion.identity, 0.095f, 0.085f, top + 0.05f, 7);
            if (i == 1)
            {
                Frustum(PropDark, posts[i] + Vector3.up * (DeckTop + 0.12f), Quaternion.identity, 0.105f, 0.105f, 0.08f, 7);
                Frustum(PlankDark, posts[i] + Vector3.up * (top - 0.01f), Quaternion.identity, 0.09f, 0.07f, 0.04f, 7);
            }
        }
    }

    private static void Barrel(Vector3 bottom)
    {
        // Bigger barrel (user): it may hang a little over the dock's edge.
        float h = BarrelHeight;
        Frustum(BarrelWood, bottom, Quaternion.identity, 0.25f, 0.3f, h * 0.5f, 12);
        Frustum(BarrelWood, bottom + Vector3.up * (h * 0.5f), Quaternion.identity, 0.3f, 0.26f, h * 0.5f, 12);
        Frustum(PropDark, bottom + Vector3.up * 0.08f, Quaternion.identity, 0.275f, 0.282f, 0.05f, 12);
        Frustum(PropDark, bottom + Vector3.up * (h - 0.13f), Quaternion.identity, 0.285f, 0.275f, 0.05f, 12);
        Frustum(PropDark, bottom + Vector3.up * (h - 0.02f), Quaternion.identity, 0.235f, 0.235f, 0.015f, 12);
        // Three spears standing in it, leaning a little apart.
        Vector3[] lean = { new Vector3(-0.1f, 1f, 0.06f), new Vector3(0.02f, 1f, -0.08f), new Vector3(0.12f, 1f, 0.05f) };
        Vector3[] foot = { new Vector3(-0.1f, 0.12f, 0.05f), new Vector3(0.01f, 0.12f, -0.08f), new Vector3(0.11f, 0.12f, 0.05f) };
        for (int i = 0; i < 3; i++)
        {
            Spear(SpearWood, Metal, PropDark, bottom + foot[i], lean[i], Vector3.forward, SpearLength * 0.95f);
        }
    }

    private static void Cooler(Vector3 bottom, float yaw)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 size = new Vector3(0.8f, 0.45f, 0.5f);
        Box(CoolerBody, bottom + rot * new Vector3(0f, size.y * 0.5f, 0f), size, rot);
        // White rim, ice inside, a fish tail sticking out.
        Box(CoolerWhite, bottom + rot * new Vector3(0f, size.y - 0.02f, 0f), new Vector3(size.x + 0.02f, 0.05f, size.z + 0.02f), rot);
        Box(Ice, bottom + rot * new Vector3(0f, size.y - 0.015f, 0f), new Vector3(size.x - 0.08f, 0.05f, size.z - 0.08f), rot);
        Ball(Fish, bottom + rot * new Vector3(0.1f, size.y + 0.02f, 0.03f), new Vector3(0.2f, 0.06f, 0.08f), 6, true);
        Vector3 tailBase = bottom + rot * new Vector3(0.28f, size.y + 0.03f, 0.03f);
        Fin(Fish, tailBase, tailBase + rot * new Vector3(0.15f, 0.07f, 0f), tailBase + rot * new Vector3(0.12f, 0.18f, 0f), rot * new Vector3(0f, 0f, 0.012f));
        // Blue ice packs packed around the fish, half sunk in the ice, plus a few loose ice cubes (user, 2026-11-25).
        Vector3[] packAt = { new Vector3(-0.24f, 0f, 0.12f), new Vector3(-0.22f, 0f, -0.11f), new Vector3(0.01f, 0f, -0.13f),
            new Vector3(0.27f, 0f, -0.12f), new Vector3(-0.04f, 0f, 0.15f) };
        Vector3[] packTilt = { new Vector3(4f, 15f, 8f), new Vector3(-10f, -10f, 0f), new Vector3(0f, 20f, -6f),
            new Vector3(8f, -25f, 10f), new Vector3(-6f, 70f, 4f) };
        for (int i = 0; i < packAt.Length; i++)
        {
            Box(IcePack, bottom + rot * (packAt[i] + new Vector3(0f, size.y + 0.025f, 0f)), new Vector3(0.18f, 0.065f, 0.12f), rot * Quaternion.Euler(packTilt[i]));
        }
        Vector3[] cubeAt = { new Vector3(-0.32f, 0f, 0.0f), new Vector3(0.14f, 0f, -0.17f), new Vector3(0.33f, 0f, 0.14f) };
        for (int i = 0; i < cubeAt.Length; i++)
        {
            Box(Ice, bottom + rot * (cubeAt[i] + new Vector3(0f, size.y + 0.03f, 0f)), new Vector3(0.06f, 0.06f, 0.06f), rot * Quaternion.Euler(20f * i, 35f * i, 10f));
        }
        // Handles.
        for (int s = -1; s <= 1; s += 2)
        {
            Box(PropDark, bottom + rot * new Vector3(s * (size.x * 0.5f + 0.015f), size.y * 0.7f, 0f), new Vector3(0.035f, 0.05f, 0.2f), rot);
        }
        // The lid (hinged at the back edge) is its own mesh since 2026-11-26: BuildCoolerLid, at CoolerHinge.
    }

    // ---- Path 2: nets (2026-11-26, user's spec: a small net that catches a rat or two, larger nets, spiked, metal,
    // and a net that stays on the floor and pulls rats in with a whirlpool).

    // A point on the net's dome at radius r (0..1) and angle a (radians); the rim sits at y 0.
    private static Vector3 NetPoint(float r, float a)
    {
        return new Vector3(Mathf.Cos(a) * r, NetDome * (1f - r * r), Mathf.Sin(a) * r);
    }

    /// <summary>
    /// The thrown net, laid open: radius 1 (the tower scales it per tier), centre at the origin, rim on y 0, dome up.
    /// Rope spokes and rings over a dome, lead weights round the rim, a knot in the middle. spiked = tier 3+: short spikes
    /// up and down at every knot of the outer rings.
    /// </summary>
    public static Mesh BuildNet(bool spiked)
    {
        Begin();
        int spokes = 12;
        float[] rings = { 0.3f, 0.55f, 0.8f, 1f };
        float rope = 0.035f;
        for (int s = 0; s < spokes; s++)
        {
            float a = s * Mathf.PI * 2f / spokes;
            for (int k = 0; k < 6; k++)
            {
                Segment(NetRope, NetPoint(k / 6f * 0.98f + 0.02f, a), NetPoint((k + 1) / 6f, a), rope, rope);
            }
        }
        foreach (float r in rings)
        {
            int n = Mathf.Max(spokes, Mathf.RoundToInt(spokes * 2 * r));
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                // A little sag between knots.
                Vector3 p0 = NetPoint(r, a0), p1 = NetPoint(r, a1);
                Vector3 mid = NetPoint(r, (a0 + a1) * 0.5f) + Vector3.down * 0.015f;
                Segment(NetRope, p0, mid, rope, rope);
                Segment(NetRope, mid, p1, rope, rope);
            }
        }
        Ball(NetWeight, NetPoint(0f, 0f) + Vector3.up * 0.02f, new Vector3(0.07f, 0.05f, 0.07f), 6, true);
        for (int s = 0; s < spokes; s++)
        {
            float a = s * Mathf.PI * 2f / spokes;
            Ball(NetWeight, NetPoint(1f, a), new Vector3(0.055f, 0.05f, 0.055f), 6, true);
            if (spiked)
            {
                foreach (float r in new[] { 0.55f, 0.8f })
                {
                    Vector3 knot = NetPoint(r, a + (r > 0.6f ? Mathf.PI / spokes : 0f));
                    Frustum(NetSpike, knot, Quaternion.identity, 0.035f, 0.002f, 0.14f, 4);
                    Frustum(NetSpike, knot, Quaternion.Euler(180f, 0f, 0f), 0.035f, 0.002f, 0.12f, 4);
                }
            }
        }
        return Finish(spiked ? "FishercatNetSpiked" : "FishercatNet", NetSubmeshes);
    }

    /// <summary>The bundled net the cat holds before a throw: a lumpy rope ball with bands round it and two weights hanging.</summary>
    public static Mesh BuildNetBundle()
    {
        Begin();
        Ball(NetRope, Vector3.zero, new Vector3(0.15f, 0.12f, 0.14f), 8, true);
        Quaternion[] bands = { Quaternion.identity, Quaternion.Euler(70f, 20f, 0f), Quaternion.Euler(-40f, 80f, 30f), Quaternion.Euler(20f, -60f, 75f) };
        foreach (Quaternion q in bands)
        {
            Frustum(NetWeight, q * new Vector3(0f, -0.015f, 0f), q, new Vector2(0.158f, 0.148f), new Vector2(0.158f, 0.148f), 0.03f, 10);
        }
        Segment(NetRope, new Vector3(0.08f, -0.09f, 0.05f), new Vector3(0.12f, -0.22f, 0.08f), 0.03f, 0.025f);
        Ball(NetWeight, new Vector3(0.12f, -0.24f, 0.08f), new Vector3(0.04f, 0.04f, 0.04f), 6, true);
        Segment(NetRope, new Vector3(-0.07f, -0.1f, -0.04f), new Vector3(-0.09f, -0.18f, -0.1f), 0.03f, 0.025f);
        Ball(NetWeight, new Vector3(-0.09f, -0.2f, -0.1f), new Vector3(0.04f, 0.04f, 0.04f), 6, true);
        return Finish("FishercatNetBundle", NetSubmeshes);
    }

    /// <summary>
    /// The chest of nets (tier 1+, where the cooler stood): a wooden chest, bottom centre at the origin, front +Z, its lid
    /// flipped open against the back, dark trim, net bundles heaped inside and a length of net with weights hanging over
    /// the front edge. Tier 3 paints it red (material swap on ChestWood).
    /// </summary>
    public static Mesh BuildNetChest()
    {
        Begin();
        Vector3 size = ChestSize;
        float t = 0.035f;
        Box(ChestWood, new Vector3(0f, t * 0.5f, 0f), new Vector3(size.x, t, size.z), Quaternion.identity);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(ChestWood, new Vector3(s * (size.x - t) * 0.5f, size.y * 0.5f, 0f), new Vector3(t, size.y, size.z), Quaternion.identity);
            Box(ChestWood, new Vector3(0f, size.y * 0.5f, s * (size.z - t) * 0.5f), new Vector3(size.x - 2f * t, size.y, t), Quaternion.identity);
        }
        // Trim: a dark band at the foot and round the top rim, plank lines on the front.
        Box(ChestDark, new Vector3(0f, 0.03f, 0f), new Vector3(size.x + 0.02f, 0.05f, size.z + 0.02f), Quaternion.identity);
        Box(ChestDark, new Vector3(0f, size.y - 0.02f, 0f), new Vector3(size.x + 0.02f, 0.04f, size.z + 0.02f), Quaternion.identity);
        for (int i = 1; i < 3; i++)
        {
            Box(ChestDark, new Vector3(0f, size.y * i / 3f, size.z * 0.5f + 0.003f), new Vector3(size.x - 0.04f, 0.012f, 0.01f), Quaternion.identity);
        }
        // Lid: hinged at the back, flipped open past upright.
        Vector3 hinge = new Vector3(0f, size.y, -size.z * 0.5f);
        Quaternion lid = Quaternion.Euler(-110f, 0f, 0f);
        Box(ChestWood, hinge + lid * new Vector3(0f, 0.03f, size.z * 0.5f), new Vector3(size.x + 0.02f, 0.06f, size.z + 0.02f), lid);
        Box(ChestDark, hinge + lid * new Vector3(0f, 0.065f, size.z * 0.5f), new Vector3(size.x - 0.1f, 0.012f, 0.05f), lid);
        // Nets heaped inside, poking over the rim.
        Ball(ChestRope, new Vector3(-0.12f, size.y - 0.02f, 0.02f), new Vector3(0.17f, 0.1f, 0.15f), 8, true);
        Ball(ChestRope, new Vector3(0.12f, size.y - 0.03f, -0.04f), new Vector3(0.15f, 0.09f, 0.13f), 8, true);
        Ball(ChestWeight, new Vector3(0.02f, size.y + 0.05f, 0.05f), new Vector3(0.04f, 0.04f, 0.04f), 6, true);
        // A length of net hanging over the front edge: a few strands down the front with cross ropes and weights.
        float frontZ = size.z * 0.5f + 0.02f;
        for (int k = 0; k < 4; k++)
        {
            float x = -0.15f + k * 0.1f;
            Vector3 top = new Vector3(x, size.y + 0.01f, frontZ - 0.04f);
            Vector3 lip = new Vector3(x, size.y - 0.01f, frontZ + 0.005f);
            Vector3 low = new Vector3(x + (k % 2 == 0 ? 0.02f : -0.015f), size.y * 0.35f - k * 0.02f, frontZ + 0.012f);
            Segment(ChestRope, top, lip, 0.025f, 0.025f);
            Segment(ChestRope, lip, low, 0.025f, 0.022f);
            Ball(ChestWeight, low + Vector3.down * 0.02f, new Vector3(0.032f, 0.032f, 0.032f), 6, true);
        }
        for (int c = 0; c < 2; c++)
        {
            float y = size.y * (0.78f - c * 0.25f);
            Segment(ChestRope, new Vector3(-0.16f, y, frontZ + 0.008f), new Vector3(0.16f, y - 0.02f, frontZ + 0.01f), 0.02f, 0.02f);
        }
        return Finish("FishercatNetChest", ChestSubmeshes);
    }

    /// <summary>Tier 5's reinforcement on the net chest (same space): metal corner brackets, two bands round it, rivets, a hasp.</summary>
    public static Mesh BuildNetChestBands()
    {
        Begin();
        Vector3 size = ChestSize;
        float o = 0.012f;
        for (int s = -1; s <= 1; s += 2)
        {
            for (int u = -1; u <= 1; u += 2)
            {
                Vector3 corner = new Vector3(s * (size.x * 0.5f + o * 0.5f), size.y * 0.5f, u * (size.z * 0.5f + o * 0.5f));
                Box(ChestMetal, corner, new Vector3(0.07f, size.y + 0.02f, 0.07f), Quaternion.identity);
            }
            float x = s * size.x * 0.3f;
            Box(ChestMetal, new Vector3(x, size.y * 0.5f, 0f), new Vector3(0.05f, size.y + 0.015f, size.z + 2f * o), Quaternion.identity);
            for (int r = 0; r < 3; r++)
            {
                Ball(ChestDark, new Vector3(x, size.y * (0.2f + r * 0.3f), size.z * 0.5f + o + 0.004f), new Vector3(0.012f, 0.012f, 0.01f), 6, true);
            }
        }
        Box(ChestMetal, new Vector3(0f, size.y * 0.8f, size.z * 0.5f + o + 0.004f), new Vector3(0.07f, 0.09f, 0.015f), Quaternion.identity);
        Box(ChestDark, new Vector3(0f, size.y * 0.76f, size.z * 0.5f + o + 0.014f), new Vector3(0.03f, 0.03f, 0.012f), Quaternion.identity);
        return Finish("FishercatNetChestBands", ChestSubmeshes);
    }

    /// <summary>
    /// Tier 5's whirlpool under a landed net, as wind (user, 2026-11-26: "less colorful ... just wind looking effects"):
    /// radius 1 (scaled to the pull's reach), thin white streaks curling inward at different radii and heights, tapered and
    /// fading out at both ends (vertex alpha, for a transparent Sprites/Default material); it spins in play. One submesh.
    /// </summary>
    public static Mesh BuildWhirlpool()
    {
        List<Vector3> verts = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> tris = new List<int>();
        float[] radius = { 1f, 0.92f, 0.82f, 0.72f, 0.62f, 0.52f, 0.42f, 0.33f, 0.24f };
        float[] start = { 0f, 2.4f, 4.4f, 1.1f, 3.3f, 5.4f, 0.6f, 2.9f, 4.8f };
        float[] span = { 2.2f, 1.7f, 2.0f, 1.6f, 1.9f, 1.4f, 1.7f, 1.3f, 1.5f };
        float[] height = { 0.05f, 0.11f, 0.07f, 0.14f, 0.09f, 0.16f, 0.12f, 0.18f, 0.14f };
        int steps = 20;
        for (int s = 0; s < radius.Length; s++)
        {
            float width = Mathf.Lerp(0.05f, 0.03f, (float)s / radius.Length);
            int first = verts.Count;
            for (int k = 0; k <= steps; k++)
            {
                float u = (float)k / steps;
                float a = start[s] + u * span[s];
                float r = radius[s] * Mathf.Lerp(1f, 0.82f, u);
                // Clamped: sin(pi) is a tiny negative, and Pow of it is NaN (NaN vertex alpha made bloom blow up, 2026-11-26).
                float bulge = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                float w = width * bulge;
                float y = height[s] + 0.03f * Mathf.Sin(u * Mathf.PI * 2f + s);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * (r - w * 0.5f) + Vector3.up * y);
                verts.Add(dir * (r + w * 0.5f) + Vector3.up * y);
                Color c = new Color(1f, 1f, 1f, Mathf.Pow(bulge, 1.5f));
                colors.Add(c);
                colors.Add(c);
                if (k > 0)
                {
                    int i0 = first + (k - 1) * 2;
                    tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1);
                    tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0 + 3);
                }
            }
        }
        Mesh mesh = new Mesh { name = "FishercatWhirlpool" };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        List<Vector3> normals = new List<Vector3>();
        for (int i = 0; i < verts.Count; i++)
        {
            normals.Add(Vector3.up);
        }
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---- Path 3: water (2026-11-26, user's spec: a fishing pole on a stand casts a line onto the path where a pool of water
    // slows rats; later a small shark, a fancier pole, a bigger green pool, an industrial pole and a big shark).

    /// <summary>
    /// The fishing pole propped on a stand (where the cooler stood), base centre at the origin; the rod points +Z, rising to
    /// its tip at PoleTip (the line hangs from there). look 0 = tier 1 (wooden stand, cork handle, plain rod, small reel),
    /// 1 = tier 3 "more expensive" (dark polished stand with gold trim, black carbon rod with gold guides and a chrome reel),
    /// 2 = tier 5 "industrial" (steel base plate with bolts, a steel arm, thick steel rod, a big boxed motor reel, hazard stripes).
    /// </summary>
    public static Mesh BuildPoleStand(int look)
    {
        return BuildPoleStand(look, true, true);
    }

    /// <summary>
    /// Split for the Path 3 logistics (2026-11-26: the cat picks the rod up): stand = the stand only, rod = the rod and its
    /// reel only, both in the same stand space (the rod rests on the stand at identity).
    /// </summary>
    public static Mesh BuildPoleStand(int look, bool stand, bool rod)
    {
        Begin();
        bool fancy = look == 1, industrial = look == 2;
        Vector3 butt = new Vector3(0f, 0.42f, -0.5f);
        Vector3 tip = PoleTip;
        Vector3 dir = (tip - butt).normalized;
        float length = Vector3.Distance(butt, tip);
        Vector3 rest = butt + dir * (length * 0.3f);
        // Stand.
        if (!stand)
        {
        }
        else if (!industrial)
        {
            int wood = fancy ? PoleDark : PoleWood;
            Box(wood, new Vector3(0f, 0.03f, -0.1f), new Vector3(0.34f, 0.06f, 0.5f), Quaternion.identity);
            Segment(wood, new Vector3(0f, 0.05f, rest.z), new Vector3(0f, rest.y - 0.04f, rest.z), 0.07f, 0.06f);
            for (int s = -1; s <= 1; s += 2)
            {
                Segment(wood, new Vector3(0f, rest.y - 0.06f, rest.z), new Vector3(s * 0.07f, rest.y + 0.06f, rest.z), 0.035f, 0.03f);
            }
            Segment(wood, new Vector3(0f, 0.05f, butt.z + 0.02f), new Vector3(0f, butt.y - 0.03f, butt.z + 0.02f), 0.05f, 0.05f);
            if (fancy)
            {
                Box(PoleAccent, new Vector3(0f, 0.065f, -0.1f), new Vector3(0.36f, 0.012f, 0.52f), Quaternion.identity);
                Frustum(PoleAccent, new Vector3(0f, rest.y - 0.07f, rest.z), Quaternion.identity, 0.045f, 0.045f, 0.03f, 8);
            }
        }
        else
        {
            Box(PoleMetal, new Vector3(0f, 0.03f, -0.08f), new Vector3(0.5f, 0.06f, 0.62f), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int u = -1; u <= 1; u += 2)
                {
                    Frustum(PoleDark, new Vector3(s * 0.2f, 0.06f, -0.08f + u * 0.25f), Quaternion.identity, 0.025f, 0.025f, 0.02f, 6);
                }
            }
            // Hazard stripes along the plate's front edge.
            for (int k = 0; k < 5; k++)
            {
                Box(k % 2 == 0 ? PoleAccent : PoleBlack, new Vector3(-0.2f + k * 0.1f, 0.062f, 0.2f), new Vector3(0.1f, 0.01f, 0.06f), Quaternion.Euler(0f, 30f, 0f));
            }
            Box(PoleMetal, new Vector3(0f, rest.y * 0.5f, rest.z), new Vector3(0.1f, rest.y, 0.1f), Quaternion.identity);
            Box(PoleAccent, new Vector3(0f, rest.y * 0.45f, rest.z), new Vector3(0.105f, 0.08f, 0.105f), Quaternion.identity);
            Box(PoleDark, new Vector3(0f, rest.y - 0.02f, rest.z), new Vector3(0.2f, 0.06f, 0.12f), Quaternion.identity);
            Segment(PoleMetal, new Vector3(0f, 0.06f, butt.z + 0.04f), new Vector3(0f, butt.y - 0.03f, butt.z + 0.04f), 0.08f, 0.08f);
        }
        // Rod: handle, reel, blank, guides, tip.
        if (rod)
        {
        float handle = length * 0.2f;
        Segment(industrial ? PoleBlack : fancy ? PoleBlack : PoleCork, butt, butt + dir * handle, 0.055f, 0.05f);
        if (fancy)
        {
            Frustum(PoleAccent, butt + dir * handle, Quaternion.FromToRotation(Vector3.up, dir), 0.032f, 0.032f, 0.025f, 8);
        }
        int blank = industrial ? PoleMetal : fancy ? PoleBlack : PoleWood;
        Segment(blank, butt + dir * handle, tip, industrial ? 0.05f : 0.035f, industrial ? 0.03f : 0.012f);
        int guides = 4;
        for (int g = 1; g <= guides; g++)
        {
            Vector3 at = butt + dir * (handle + (length - handle) * g / (guides + 0.5f));
            float size = Mathf.Lerp(0.05f, 0.025f, (float)g / guides);
            Box(fancy ? PoleAccent : PoleMetal, at + Vector3.down * size * 0.6f, new Vector3(0.012f, size, 0.012f), Quaternion.LookRotation(dir));
        }
        Ball(fancy ? PoleAccent : PoleMetal, tip, new Vector3(0.02f, 0.02f, 0.02f), 6, true);
        // Reel under the rod, just ahead of the handle.
        Vector3 reelAt = butt + dir * (handle * 0.7f) + Vector3.down * (industrial ? 0.16f : 0.1f);
        if (!industrial)
        {
            Quaternion side = Quaternion.Euler(0f, 0f, 90f);
            Frustum(fancy ? PoleMetal : PoleDark, reelAt + Vector3.left * 0.04f, side, 0.07f, 0.07f, 0.08f, 10);
            Frustum(fancy ? PoleAccent : PoleMetal, reelAt + Vector3.right * 0.045f, side, 0.02f, 0.02f, 0.04f, 6);
            Segment(fancy ? PoleAccent : PoleMetal, reelAt + new Vector3(0.09f, 0f, 0f), reelAt + new Vector3(0.09f, -0.07f, 0.02f), 0.015f, 0.015f);
            Ball(fancy ? PoleWhite : PoleRed, reelAt + new Vector3(0.1f, -0.08f, 0.02f), new Vector3(0.02f, 0.02f, 0.02f), 6, true);
        }
        else
        {
            Box(PoleAccent, reelAt, new Vector3(0.22f, 0.16f, 0.2f), Quaternion.LookRotation(dir));
            Frustum(PoleBlack, reelAt + Vector3.left * 0.06f, Quaternion.Euler(0f, 0f, 90f), 0.085f, 0.085f, 0.12f, 12);
            Box(PoleDark, reelAt + Vector3.up * 0.09f, new Vector3(0.08f, 0.03f, 0.12f), Quaternion.LookRotation(dir));
            Box(PoleRed, reelAt + new Vector3(0.115f, 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.03f), Quaternion.identity);
        }
        }
        string name = (rod && !stand ? "FishercatPoleRod" : "FishercatPoleStand") + (industrial ? "Industrial" : fancy ? "Fancy" : "");
        return Finish(name, PoleSubmeshes);
    }

    /// <summary>The pool the line makes on the path: a wavy patch of water, radius 1 (scaled per tier), on y 0.02, with a
    /// lighter foam ring at its edge.</summary>
    public static Mesh BuildPool()
    {
        Begin();
        int n = 28;
        Vector3[] inner = new Vector3[n];
        Vector3[] outer = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float wobble = 0.05f * Mathf.Sin(a * 3f + 0.4f) + 0.035f * Mathf.Sin(a * 5f + 1.7f);
            inner[i] = new Vector3(Mathf.Cos(a) * (0.9f + wobble), 0.02f, Mathf.Sin(a) * (0.9f + wobble));
            outer[i] = new Vector3(Mathf.Cos(a) * (1f + wobble), 0.021f, Mathf.Sin(a) * (1f + wobble));
        }
        Vector3 centre = new Vector3(0f, 0.02f, 0f);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddPolyUp(PoolWater, new[] { centre, inner[j], inner[i] });
            AddPolyUp(PoolFoam, new[] { inner[i], inner[j], outer[j], outer[i] });
        }
        return Finish("FishercatPool", PoolSubmeshes);
    }

    /// <summary>The float on the line in the pool: red top, white bottom, a little stem; bottom at the origin.</summary>
    public static Mesh BuildBobber()
    {
        Begin();
        Ball(PoleWhite, new Vector3(0f, 0.06f, 0f), new Vector3(0.07f, 0.06f, 0.07f), 8, true);
        Ball(PoleRed, new Vector3(0f, 0.09f, 0f), new Vector3(0.068f, 0.045f, 0.068f), 8, false);
        Segment(PoleRed, new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.2f, 0f), 0.015f, 0.012f);
        return Finish("FishercatBobber", PoleSubmeshes);
    }

    /// <summary>
    /// A shark leaping out of the pool, nose +Z, middle at the origin. big = false: tier 3's small shark (about 1 long, grey
    /// top and white belly, a dorsal fin, two tail fins, side fins, small eyes and a toothy grin). big = true: an older
    /// whole-body big shark (no longer saved; tier 5 uses BuildBigSharkHead since the user's sketch, 2026-11-26).
    /// </summary>
    public static Mesh BuildShark(bool big)
    {
        Begin();
        float s = big ? 2.4f : 1f;
        Vector3 body = new Vector3(0.2f, 0.19f, 0.5f) * s * (big ? 1.12f : 1f);
        Ball(SharkTop, Vector3.zero, body, 10, true);
        Ball(SharkBelly, new Vector3(0f, -body.y * 0.35f, body.z * 0.05f), new Vector3(body.x * 0.85f, body.y * 0.65f, body.z * 0.85f), 10, true);
        // Snout and tail stock.
        Frustum(SharkTop, new Vector3(0f, body.y * 0.05f, body.z * 0.85f), Quaternion.Euler(90f, 0f, 0f), new Vector2(body.x * 0.7f, body.y * 0.55f), new Vector2(0.02f * s, 0.02f * s), body.z * 0.35f, 8);
        Frustum(SharkTop, new Vector3(0f, 0f, -body.z * 0.8f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(body.x * 0.55f, body.y * 0.55f), new Vector2(body.x * 0.18f, body.y * 0.25f), body.z * 0.45f, 8);
        Vector3 side = new Vector3(0.012f * s, 0f, 0f);
        // Dorsal fin, tail fins (crescent on the big one), side fins.
        Fin(SharkTop, new Vector3(0f, body.y * 0.9f, body.z * 0.15f), new Vector3(0f, body.y * 0.85f, -body.z * 0.25f), new Vector3(0f, body.y * (big ? 2.1f : 1.9f), -body.z * 0.32f), side);
        Vector3 tailRoot = new Vector3(0f, 0f, -body.z * 1.22f);
        Fin(SharkTop, tailRoot + new Vector3(0f, 0.02f * s, 0.05f * s), tailRoot + new Vector3(0f, -0.02f * s, -0.04f * s), tailRoot + new Vector3(0f, (big ? 0.42f : 0.3f) * s, -0.2f * s), side);
        Fin(SharkTop, tailRoot + new Vector3(0f, 0f, 0.05f * s), tailRoot + new Vector3(0f, -0.02f * s, -0.04f * s), tailRoot + new Vector3(0f, (big ? -0.34f : -0.18f) * s, -0.14f * s), side);
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 root = new Vector3(k * body.x * 0.8f, -body.y * 0.3f, body.z * 0.25f);
            Fin(SharkTop, root, root + new Vector3(0f, 0f, -0.15f * s), root + new Vector3(k * 0.28f * s, -0.12f * s, -0.18f * s), new Vector3(0f, 0.01f * s, 0f));
            // Eyes.
            Box(SharkDark, new Vector3(k * body.x * 0.78f, body.y * 0.25f, body.z * 0.72f), new Vector3(0.01f * s, 0.035f * s, 0.035f * s), Quaternion.identity);
        }
        if (!big)
        {
            // A toothy grin along the underside of the snout.
            Box(SharkMouth, new Vector3(0f, -body.y * 0.42f, body.z * 0.75f), new Vector3(body.x * 1.1f, 0.03f, 0.16f), Quaternion.Euler(-12f, 0f, 0f));
            for (int i = 0; i < 6; i++)
            {
                float x = (-0.5f + i / 5f) * body.x * 0.95f;
                Vector3 at = new Vector3(x, -body.y * 0.4f, body.z * 0.8f + Mathf.Abs(x) * -0.3f);
                Fin(SharkTeeth, at + new Vector3(-0.012f, 0f, 0f), at + new Vector3(0.012f, 0f, 0f), at + new Vector3(0f, -0.035f, 0.005f), new Vector3(0f, 0f, 0.005f));
            }
        }
        else
        {
            // Jaws wide open: a dark mouth, an upper and lower jaw edge, rows of teeth, gill slits and scars.
            Vector3 mouth = new Vector3(0f, -body.y * 0.25f, body.z * 0.88f);
            Box(SharkMouth, mouth, new Vector3(body.x * 1.2f, body.y * 0.55f, 0.3f * s * 0.2f), Quaternion.Euler(-20f, 0f, 0f));
            Box(SharkBelly, mouth + new Vector3(0f, -body.y * 0.35f, -0.05f * s), new Vector3(body.x * 1.15f, 0.05f * s, 0.32f * s * 0.5f), Quaternion.Euler(25f, 0f, 0f));
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? body.y * 0.24f : -body.y * 0.24f;
                float point = row == 0 ? -0.07f * s : 0.06f * s;
                for (int i = 0; i < 9; i++)
                {
                    float x = (-0.5f + i / 8f) * body.x * 1.05f;
                    Vector3 at = mouth + new Vector3(x, y, 0.035f * s - Mathf.Abs(x) * 0.4f);
                    Fin(SharkTeeth, at + new Vector3(-0.014f * s, 0f, 0f), at + new Vector3(0.014f * s, 0f, 0f), at + new Vector3(0f, point, 0.004f * s), new Vector3(0f, 0f, 0.004f * s));
                }
            }
            for (int k = -1; k <= 1; k += 2)
            {
                for (int g = 0; g < 4; g++)
                {
                    Box(SharkDark, new Vector3(k * body.x * 0.93f, -body.y * 0.05f, body.z * (0.42f - g * 0.07f)), new Vector3(0.01f * s, body.y * 0.5f, 0.012f * s), Quaternion.Euler(-10f, 0f, 0f));
                }
                Box(SharkBelly, new Vector3(k * body.x * 0.9f, body.y * 0.5f, -body.z * 0.1f), new Vector3(0.01f * s, 0.012f * s, 0.18f * s), Quaternion.Euler(20f * k, 0f, 0f));
            }
        }
        return Finish(big ? "FishercatBigShark" : "FishercatShark", SharkSubmeshes);
    }

    /// <summary>
    /// Tier 5's big shark as the user drew it (2026-11-26): only its head bursting straight up out of the puddle, jaws wide
    /// open to the sky, filling the puddle; reworked the same day to a rounder, chunkier, more shark-like head (user: "more
    /// like a realistic shark, but in a blocky game sort of way"). Built for a puddle of radius 1 (scaled with the pool),
    /// water line at y 0: a big rounded upper head tapering to a blunt snout (leaning a little away from the mouth), a
    /// thick rounded lower jaw with a white underside leaning out, a pink mouth and gums between, rows of triangular teeth
    /// along both jaws, round dark eyes and gill slits on the sides.
    /// </summary>
    public static Mesh BuildBigSharkHead()
    {
        return BuildBigSharkHead(false);
    }

    /// <summary>
    /// The great white split for the bite (2026-11-26, user: "make the shark actually close its mouth"): jaw = false is the
    /// head (body, upper jaw and teeth, eyes, gills, mouth floor); jaw = true is the lower jaw with its teeth, built from the
    /// mouth's corner (BigSharkHinge) and moved so the hinge is at its origin: rotating it about +Z by a negative angle
    /// swings it up against the upper teeth.
    /// </summary>
    public static Mesh BuildBigSharkHead(bool jaw)
    {
        Begin();
        // A great white (user, 2026-11-26: "still too malnourished ... like a great white ... fill the puddle almost entirely
        // if not extend past it"): a huge, wide head (about 2.2 across, wider than the puddle) with a heavy conical snout.
        // Slimmed 2026-11-26 (user: too wide, clipping): sideways 1.15 -> 0.85, radii about 12% less.
        Vector2 headShape = new Vector2(0.78f, 0.85f);
        // Raised 0.4 and set on a solid body (user's sketch, 2026-11-26): the jaws meet above the water, not at it.
        const float rise = 0.4f;
        Vector3 upA = new Vector3(0.4f, rise - 0.25f, 0f), upB = new Vector3(0.9f, rise + 2.05f, 0f);
        float[] upR = { 0.82f, 0.86f, 0.84f, 0.78f, 0.67f, 0.53f, 0.37f, 0.21f, 0.07f };
        Vector3 upDir = (upB - upA).normalized;
        Vector3 upIn = Vector3.Cross(Vector3.forward, upDir);
        if (jaw)
        {
            // Lower jaw: massive and rounded, white below, grey on top, growing out of the mouth's corner.
            Vector3 lowA = BigSharkHinge + new Vector3(-0.12f, -0.05f, 0f), lowB = new Vector3(-1.5f, rise + 0.85f, 0f);
            float[] lowR = { 0.42f, 0.62f, 0.62f, 0.53f, 0.39f, 0.21f };
            Lathe(SharkBelly, lowA, lowB, lowR, new Vector2(0.72f, 0.8f), 12);
            Lathe(SharkTop, lowA + new Vector3(0.09f, 0.05f, 0f), lowB + new Vector3(0.05f, 0.04f, 0f), new[] { 0.36f, 0.55f, 0.53f, 0.44f, 0.32f, 0.16f }, new Vector2(0.62f, 0.76f), 12);
            Vector3 lowDir = (lowB - lowA).normalized;
            Vector3 lowIn = -Vector3.Cross(Vector3.forward, lowDir);
            Teeth(lowA, lowDir, lowIn, lowR, Vector3.Distance(lowA, lowB), 0.25f, 0.86f, 6, 0.19f);
            for (int i = 0; i < Vertices.Count; i++)
            {
                Vertices[i] -= BigSharkHinge;
            }
            return Finish("FishercatBigSharkJaw", SharkSubmeshes);
        }
        Lathe(SharkTop, new Vector3(0f, -0.25f, 0f), new Vector3(0f, rise + 0.3f, 0f), new[] { 1f, 1.02f, 1f, 0.94f }, new Vector2(1f, 0.72f), 12);
        Lathe(SharkBelly, new Vector3(-0.18f, -0.25f, 0f), new Vector3(-0.22f, rise + 0.28f, 0f), new[] { 0.86f, 0.88f, 0.85f, 0.78f }, new Vector2(0.95f, 0.7f), 12);
        Lathe(SharkTop, upA, upB, upR, headShape, 12);
        // Mouth (user, 2026-11-26: no tongue / no red): only a flat dark floor between the jaws.
        Ball(SharkDark, new Vector3(-0.1f, rise + 0.1f, 0f), new Vector3(0.5f, 0.06f, 0.5f), 12, true);
        // Teeth: big triangular rows curving across the upper jaw (the lower jaw's are on the jaw mesh).
        Teeth(upA, upDir, upIn, upR, Vector3.Distance(upA, upB), 0.2f, 0.84f, 9, 0.22f);
        // Small black eyes high on the sides, long gill slits low and toward the back.
        float upLength = Vector3.Distance(upA, upB);
        for (int side = -1; side <= 1; side += 2)
        {
            float uEye = 1.45f / upLength;
            Vector3 eyeAxis = upA + upDir * 1.45f;
            float eyeZ = SampleRadius(upR, uEye) * headShape.y * 0.93f;
            Ball(SharkDark, eyeAxis + new Vector3(0.12f, 0f, side * eyeZ), new Vector3(0.09f, 0.1f, 0.06f), 8, true);
            Ball(SharkBelly, eyeAxis + new Vector3(0.15f, 0.04f, side * (eyeZ + 0.03f)), new Vector3(0.022f, 0.022f, 0.015f), 6, true);
            for (int g = 0; g < 5; g++)
            {
                float u = (0.25f + g * 0.12f) / upLength;
                Vector3 at = upA + upDir * (0.25f + g * 0.12f) + new Vector3(0.35f, 0f, side * SampleRadius(upR, u) * headShape.y * 0.9f);
                Box(SharkDark, at, new Vector3(0.03f, 0.38f, 0.04f), Quaternion.Euler(0f, side * 30f, -12f));
            }
        }
        return Finish("FishercatBigShark", SharkSubmeshes);
    }

    // Rings of `sides` points around the axis a->b with the given radii (evenly along it), the cross-section an ellipse
    // (shape.x across the mouth's opening, shape.y sideways), joined into a faceted tube, capped at the far end.
    private static void Lathe(int material, Vector3 a, Vector3 b, float[] radii, Vector2 shape, int sides)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 across = Vector3.Cross(Vector3.forward, axis).normalized;
        Vector3[][] rings = new Vector3[radii.Length][];
        for (int r = 0; r < radii.Length; r++)
        {
            Vector3 c = Vector3.Lerp(a, b, (float)r / (radii.Length - 1));
            rings[r] = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float t = (i + 0.5f) * Mathf.PI * 2f / sides;
                rings[r][i] = c + across * (Mathf.Cos(t) * radii[r] * shape.x) + Vector3.forward * (Mathf.Sin(t) * radii[r] * shape.y);
            }
        }
        for (int r = 0; r < radii.Length - 1; r++)
        {
            Vector3 mid = Vector3.Lerp(a, b, (r + 0.5f) / (radii.Length - 1));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddPoly(material, new[] { rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i] }, mid);
            }
        }
        AddPoly(material, rings[radii.Length - 1], Vector3.Lerp(a, b, 0.9f));
    }

    // Teeth along a jaw: `count` rows from `from` to `to` (shares of its length), each row three teeth curving across the
    // jaw's inner side, sitting on the surface (radius from the jaw's profile) and pointing inward.
    private static void Teeth(Vector3 a, Vector3 axis, Vector3 inward, float[] radii, float length, float from, float to, int count, float size)
    {
        for (int k = 0; k < count; k++)
        {
            float u = Mathf.Lerp(from, to, (float)k / (count - 1));
            float radius = SampleRadius(radii, u) * 0.82f;
            float s = size * Mathf.Lerp(1f, 0.55f, u);
            Vector3 c = a + axis * (u * length);
            for (int zi = -1; zi <= 1; zi++)
            {
                float angle = zi * 0.55f;
                Vector3 normal = (inward * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)).normalized;
                Vector3 at = c + normal * radius;
                Vector3 along = axis * (s * 0.45f);
                Fin(SharkTeeth, at - along, at + along, at + normal * s + axis * (s * 0.15f), Vector3.Cross(axis, normal).normalized * 0.012f);
            }
        }
    }

    private static float SampleRadius(float[] radii, float u)
    {
        float x = Mathf.Clamp01(u) * (radii.Length - 1);
        int i = Mathf.Min(radii.Length - 2, Mathf.FloorToInt(x));
        return Mathf.Lerp(radii[i], radii[i + 1], x - i);
    }

    // A six-sided block between an axis-aligned base rectangle and a (smaller, shifted) top rectangle (half sizes in x, z).
    private static void Prism(int material, Vector3 baseCentre, Vector2 baseHalf, Vector3 topCentre, Vector2 topHalf)
    {
        Vector3[] b =
        {
            baseCentre + new Vector3(-baseHalf.x, 0f, -baseHalf.y), baseCentre + new Vector3(baseHalf.x, 0f, -baseHalf.y),
            baseCentre + new Vector3(baseHalf.x, 0f, baseHalf.y), baseCentre + new Vector3(-baseHalf.x, 0f, baseHalf.y)
        };
        Vector3[] t =
        {
            topCentre + new Vector3(-topHalf.x, 0f, -topHalf.y), topCentre + new Vector3(topHalf.x, 0f, -topHalf.y),
            topCentre + new Vector3(topHalf.x, 0f, topHalf.y), topCentre + new Vector3(-topHalf.x, 0f, topHalf.y)
        };
        Vector3 mid = (baseCentre + topCentre) * 0.5f;
        AddPoly(material, b, mid);
        AddPoly(material, t, mid);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            AddPoly(material, new[] { b[i], b[j], t[j], t[i] }, mid);
        }
    }

    // ---- Helpers (same shapes as TrapperModelBuilder).

    private static void Begin()
    {
        Vertices.Clear();
        Normals.Clear();
        for (int i = 0; i < Triangles.Length; i++)
        {
            if (Triangles[i] == null)
            {
                Triangles[i] = new List<int>();
            }
            Triangles[i].Clear();
        }
    }

    private static Mesh Finish(string name, int submeshes)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.subMeshCount = submeshes;
        for (int i = 0; i < submeshes; i++)
        {
            mesh.SetTriangles(Triangles[i], i);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    public static void Save(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetColors(mesh.colors);
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                existing.SetTriangles(mesh.GetTriangles(i), i);
            }
            existing.RecalculateBounds();
            existing.UploadMeshData(false);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("FishercatModelBuilder: wrote " + path);
    }

    private static void Box(int material, Vector3 centre, Vector3 size, Quaternion rotation)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = new Vector3(
                (i == 0 || i == 3 || i == 4 || i == 7) ? -h.x : h.x,
                (i == 0 || i == 1 || i == 4 || i == 5) ? -h.y : h.y,
                i < 4 ? -h.z : h.z);
            c[i] = centre + rotation * local;
        }
        int[][] faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 },
            new[] { 4, 5, 1, 0 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }
        };
        foreach (int[] f in faces)
        {
            AddPoly(material, new[] { c[f[0]], c[f[1]], c[f[2]], c[f[3]] }, centre);
        }
    }

    private static void Frustum(int material, Vector3 c, Quaternion rot, float r0, float r1, float h, int n)
    {
        Frustum(material, c, rot, new Vector2(r0, r0), new Vector2(r1, r1), h, n);
    }

    private static void Frustum(int material, Vector3 c, Quaternion rot, Vector2 r0, Vector2 r1, float h, int n)
    {
        Vector3[] bottom = new Vector3[n];
        Vector3[] top = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / n;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            bottom[i] = c + rot * new Vector3(cs * r0.x, 0f, sn * r0.y);
            top[i] = c + rot * new Vector3(cs * r1.x, h, sn * r1.y);
        }
        Vector3 centre = c + rot * new Vector3(0f, h * 0.5f, 0f);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            AddPoly(material, new[] { bottom[i], bottom[j], top[j], top[i] }, centre);
        }
        if (r1.x > 0.01f)
        {
            AddPoly(material, top, centre);
        }
        if (r0.x > 0.01f)
        {
            AddPoly(material, bottom, centre);
        }
    }

    private static void Ball(int material, Vector3 centre, Vector3 radii, int sides, bool whole)
    {
        float[] t = whole ? new[] { -1f, -0.7f, -0.25f, 0.25f, 0.7f, 1f } : new[] { -0.25f, 0.25f, 0.7f, 1f };
        for (int i = 0; i < t.Length - 1; i++)
        {
            float k0 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i] * t[i]));
            float k1 = Mathf.Sqrt(Mathf.Max(0f, 1f - t[i + 1] * t[i + 1]));
            Vector3 bottom = centre + new Vector3(0f, t[i] * radii.y, 0f);
            Frustum(material, bottom, Quaternion.identity, new Vector2(radii.x * k0, radii.z * k0),
                new Vector2(radii.x * k1, radii.z * k1), (t[i + 1] - t[i]) * radii.y, sides);
        }
    }

    private static void Segment(int material, Vector3 a, Vector3 b, float startSize, float endSize)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, axis);
        right = right.sqrMagnitude < 0.001f ? Vector3.right : right.normalized;
        Vector3 up = Vector3.Cross(axis, right).normalized;
        float s0 = startSize * 0.5f, s1 = endSize * 0.5f;
        Vector3[] c =
        {
            a - right * s0 - up * s0, a + right * s0 - up * s0, a + right * s0 + up * s0, a - right * s0 + up * s0,
            b - right * s1 - up * s1, b + right * s1 - up * s1, b + right * s1 + up * s1, b - right * s1 + up * s1
        };
        Vector3 mid = (a + b) * 0.5f;
        int[][] faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 },
            new[] { 4, 5, 1, 0 }, new[] { 1, 5, 6, 2 }, new[] { 4, 0, 3, 7 }
        };
        foreach (int[] f in faces)
        {
            AddPoly(material, new[] { c[f[0]], c[f[1]], c[f[2]], c[f[3]] }, mid);
        }
    }

    // A flat polygon facing up (water).
    private static void AddPolyUp(int material, Vector3[] p)
    {
        AddPoly(material, p, p[0] + Vector3.down);
    }

    private static void AddPoly(int material, Vector3[] p, Vector3 inside)
    {
        Vector3 normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (normal.sqrMagnitude < 1e-12f && p.Length > 3)
        {
            normal = Vector3.Cross(p[2] - p[0], p[3] - p[0]);
        }
        Vector3 faceCentre = Vector3.zero;
        foreach (Vector3 v in p)
        {
            faceCentre += v;
        }
        faceCentre /= p.Length;
        bool flip = Vector3.Dot(normal, faceCentre - inside) < 0f;
        normal = (flip ? -normal : normal).normalized;
        int start = Vertices.Count;
        foreach (Vector3 v in p)
        {
            Vertices.Add(v);
            Normals.Add(normal);
        }
        List<int> tris = Triangles[material];
        for (int k = 1; k < p.Length - 1; k++)
        {
            tris.Add(start);
            tris.Add(start + (flip ? k + 1 : k));
            tris.Add(start + (flip ? k : k + 1));
        }
    }
}
