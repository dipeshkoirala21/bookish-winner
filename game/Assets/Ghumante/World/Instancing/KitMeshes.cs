using System;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// The procedural meshes of the instanced dressing (W2_DESIGN 5.4-5.8): trees and plants from the nature kit
    /// (Core <see cref="FloraMesher"/>: every species at LOD0 and LOD1, the shape families' far volume and impostor),
    /// the chautari platform, street props, people as rigid parts, cows and dogs. Everything is built in code (no
    /// hand-made assets). Frame: origin on the ground, +Y up, +Z forward (north at yaw 0), +X right. Trees and plants
    /// are unit-sized (height 1, crown diameter 1) and scaled per instance. Vertex alpha is the per-instance tint mask
    /// of the <c>_INSTANCE_TINT</c> shaders: 255 on crowns, clothes and coats, 0 on trunks, skin, hair and fixed parts;
    /// the kit's UV0 carries the material channel and baked AO. Deterministic; engine-free.
    /// </summary>
    public static class KitMeshes
    {
        /// <summary>Tree LODs: species LOD0 and LOD1, family volume, family impostor (<see cref="FloraMesher.Lods"/>).</summary>
        public const int TreeLods = FloraMesher.Lods;

        /// <summary>Triangle caps per tree LOD (the nature kit's <see cref="FloraMesher.Budget"/>: 1 600 / 240 / 112 / 8).</summary>
        public static readonly int[] TreeBudget = FloraMesher.Budget;

        private const uint Tinted = 0xFFFFFFFF;
        private const uint Stone = 0x9A948A00;

        // ---------------------------------------------------------------------------------------------------------
        // Primitives

        /// <summary>An axis-aligned box [x0, x1] × [y0, y1] × [z0, z1].</summary>
        public static int Box(MeshData m, double x0, double x1, double y0, double y1, double z0, double z1, uint c, BoxFaces faces = BoxFaces.All)
        {
            // KitFrame with U = +X has W = −Z, so w spans [−z1, −z0].
            var f = new KitFrame(0, 0, 0, 1, 0);
            return MeshKit.Box(m, f, x0, x1, y0, y1, -z1, -z0, c, faces);
        }

        /// <summary>A box centred on (cx, cz) with half sizes, from y0 to y1.</summary>
        public static int BoxC(MeshData m, double cx, double cz, double hx, double hz, double y0, double y1, uint c)
        {
            return Box(m, cx - hx, cx + hx, y0, y1, cz - hz, cz + hz, c);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Trees and plants (the nature kit: Core FloraMesher)

        /// <summary>
        /// A unit tree of a shape family (height 1, crown diameter 1): LOD 0 and 1 are the family's typical species
        /// (round: broadleaf, cone: chir pine, umbrella: pipal, column: silky oak, fountain: bamboo, low: shrub) in
        /// October, LOD 2 the family volume and 3 its impostor (grey foliage for the instance tint). The renderer draws
        /// each species' own LOD0 and LOD1 (<see cref="Plant"/>); this is the per-family stand-in.
        /// </summary>
        public static int Tree(TreeShape shape, int lod, MeshData m)
        {
            if (lod >= 2 && shape != TreeShape.Low) return FloraMesher.Family(shape, lod, m);
            return FloraMesher.Build(Typical(shape), Math.Min(lod, 1), 10, m);
        }

        /// <summary>The species a shape family stands for in <see cref="Tree"/>.</summary>
        public static TreeSpecies Typical(TreeShape shape)
        {
            switch (shape)
            {
                case TreeShape.Cone: return TreeSpecies.ChirPine;
                case TreeShape.Umbrella: return TreeSpecies.Pipal;
                case TreeShape.Column: return TreeSpecies.SilkyOak;
                case TreeShape.Fountain: return TreeSpecies.Bamboo;
                case TreeShape.Low: return TreeSpecies.Shrub;
                default: return TreeSpecies.Broadleaf;
            }
        }

        /// <summary>A species' unit model (tree or plant) at LOD 0 or 1 in a month (bloom, flush and harvest are
        /// baked into the near LODs).</summary>
        public static int Plant(TreeSpecies s, int lod, int month, MeshData m)
        {
            return FloraMesher.Build(s, lod, month, m);
        }

        /// <summary>The chautari platform (W2_DESIGN 5.8; the nature kit's <see cref="FloraMesher.Chautari"/>): coursed
        /// stone walls, a slab top, the porter ledge on the south side with a step and a shrine stone; unit width and
        /// height, scaled per instance (width about half the crown, 0.45-0.9 m high). <paramref name="lod"/> 0 is the
        /// detailed model, 1 the simple one.</summary>
        public static int Chautari(MeshData m, int lod = 0)
        {
            return FloraMesher.Chautari(lod, m);
        }

        /// <summary>Triangles of <see cref="Tree"/> for a family and LOD.</summary>
        public static int TreeTris(TreeShape s, int lod)
        {
            var m = new MeshData(256, 768);
            return Tree(s, lod, m);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Street props (built at their default size in metres; instances scale by HeightM / DefaultHeight)

        /// <summary>Default height of a prop mesh (metres).</summary>
        public static float DefaultHeight(StreetPropKind k)
        {
            switch (k)
            {
                case StreetPropKind.StreetLamp: return 10f;
                case StreetPropKind.BusStop: return 2.6f;
                case StreetPropKind.BusShelter: return 2.7f;
                case StreetPropKind.TrafficSignal: return 4.5f;
                case StreetPropKind.Gate: return 3.2f;
                case StreetPropKind.StorageTank: return 2.6f;
                case StreetPropKind.Bench: return 0.85f;
                case StreetPropKind.WaterTap: return 1.0f;
                case StreetPropKind.Windsock: return 6f;
                case StreetPropKind.Helipad: return 0.05f;
                case StreetPropKind.TaxiStand: return 2.4f;
                case StreetPropKind.AerowayGate: return 2.4f;
                case StreetPropKind.ParkingPosition: return 0.05f;
                case StreetPropKind.Mast: return 25f;
                case StreetPropKind.Artwork: return 2.5f;
                case StreetPropKind.PowerTower: return 30f;
                case StreetPropKind.PowerPole: return 9f;
                case StreetPropKind.Well: return 0.8f;
                case StreetPropKind.SolarPanel: return 1.6f;
                case StreetPropKind.Chimney: return 18f;
                default: return 1f;
            }
        }

        /// <summary>Number of street prop kinds with a mesh.</summary>
        public const int PropKinds = 20;

        /// <summary>A street prop at its default size. Colours are fixed (alpha 0) except tanks (tinted).</summary>
        public static int Prop(StreetPropKind k, MeshData m)
        {
            const uint pole = 0xA8A49C00, dark = 0x2E344000, white = 0xF2F0E800, red = 0xD93A2B00, yellow = 0xF2C23000;
            const uint blue = 0x2F7DE100, green = 0x2FA84F00, wood = 0x6B412900, roof = 0x3D7CC900, lamp = 0xFFE8A000;
            int t0 = m.TriangleCount;
            switch (k)
            {
                case StreetPropKind.StreetLamp:
                    MeshKit.Frustum(m, 0, 0, 0.12, 0, 0.07, 10, 6, false, pole);
                    MeshKit.Bar(m, 0, 9.6, 0, 0, 10.0, 1.6, 0.08, pole);
                    BoxC(m, 0, 1.7, 0.18, 0.35, 9.75, 9.95, dark);
                    BoxC(m, 0, 1.7, 0.14, 0.3, 9.7, 9.75, lamp);
                    break;
                case StreetPropKind.BusStop:
                    MeshKit.Cylinder(m, 0, 0, 0.05, 0, 2.2, 6, false, pole);
                    BoxC(m, 0, 0, 0.35, 0.03, 2.0, 2.6, blue);
                    BoxC(m, 0, 0.031, 0.25, 0.005, 2.15, 2.45, white);
                    break;
                case StreetPropKind.BusShelter:
                    for (int i = 0; i < 2; i++) BoxC(m, i == 0 ? -1.6 : 1.6, -0.6, 0.05, 0.05, 0, 2.5, pole);
                    BoxC(m, 0, -0.65, 1.7, 0.04, 0.5, 2.3, 0x9DC3D100); // back panel
                    BoxC(m, 0, 0, 1.8, 0.8, 2.5, 2.7, roof);
                    BoxC(m, 0, -0.4, 1.4, 0.2, 0.42, 0.48, wood); // bench
                    break;
                case StreetPropKind.TrafficSignal:
                    MeshKit.Cylinder(m, 0, 0, 0.08, 0, 3.6, 6, false, pole);
                    BoxC(m, 0, 0.05, 0.18, 0.15, 3.6, 4.5, dark);
                    BoxC(m, 0, 0.21, 0.09, 0.01, 4.25, 4.4, red);
                    BoxC(m, 0, 0.21, 0.09, 0.01, 4.0, 4.15, yellow);
                    BoxC(m, 0, 0.21, 0.09, 0.01, 3.75, 3.9, green);
                    break;
                case StreetPropKind.Gate:
                    BoxC(m, -1.8, 0, 0.25, 0.25, 0, 3.2, white);
                    BoxC(m, 1.8, 0, 0.25, 0.25, 0, 3.2, white);
                    BoxC(m, 0, 0, 2.1, 0.3, 2.8, 3.2, red);
                    break;
                case StreetPropKind.StorageTank:
                    for (int i = 0; i < 4; i++) BoxC(m, i % 2 == 0 ? -0.45 : 0.45, i < 2 ? -0.45 : 0.45, 0.04, 0.04, 0, 1.2, dark);
                    BoxC(m, 0, 0, 0.6, 0.6, 1.2, 1.28, dark);
                    MeshKit.Cylinder(m, 0, 0, 0.55, 1.28, 2.45, 10, false, Tinted);
                    MeshKit.Dome(m, 0, 0, 0.55, 2.45, 0.15, 10, 1, Tinted);
                    break;
                case StreetPropKind.Bench:
                    BoxC(m, 0, 0, 0.9, 0.22, 0.42, 0.5, wood);
                    BoxC(m, -0.75, 0, 0.05, 0.2, 0, 0.42, dark);
                    BoxC(m, 0.75, 0, 0.05, 0.2, 0, 0.42, dark);
                    BoxC(m, 0, -0.2, 0.9, 0.03, 0.5, 0.85, wood);
                    break;
                case StreetPropKind.WaterTap:
                    BoxC(m, 0, 0, 0.2, 0.2, 0, 0.9, Stone);
                    MeshKit.Bar(m, 0, 0.8, 0, 0, 0.8, 0.3, 0.05, 0x8C949C00);
                    break;
                case StreetPropKind.Windsock:
                    MeshKit.Cylinder(m, 0, 0, 0.06, 0, 6, 6, false, white);
                    MeshKit.Frustum(m, 0, 0.6, 0.35, 5.6, 0.2, 5.9, 8, false, 0xF5821F00);
                    break;
                case StreetPropKind.Helipad:
                    MeshKit.Cylinder(m, 0, 0, 7.5, 0, 0.05, 16, true, 0x5A5D6200);
                    BoxC(m, -1.2, 0, 0.3, 2.0, 0.05, 0.06, white);
                    BoxC(m, 1.2, 0, 0.3, 2.0, 0.05, 0.06, white);
                    BoxC(m, 0, 0, 1.0, 0.3, 0.05, 0.06, white);
                    break;
                case StreetPropKind.TaxiStand:
                case StreetPropKind.AerowayGate:
                    MeshKit.Cylinder(m, 0, 0, 0.05, 0, 2.0, 6, false, pole);
                    BoxC(m, 0, 0, 0.4, 0.03, 2.0, 2.4, k == StreetPropKind.TaxiStand ? 0xFFEB3B00 : yellow);
                    break;
                case StreetPropKind.ParkingPosition:
                    BoxC(m, 0, 0, 0.15, 6.0, 0.02, 0.05, yellow);
                    BoxC(m, 0, 6.0, 1.5, 0.15, 0.02, 0.05, yellow);
                    break;
                case StreetPropKind.Mast:
                    MeshKit.Frustum(m, 0, 0, 0.6, 0, 0.15, 25, 4, false, 0xC9433A00);
                    break;
                case StreetPropKind.Artwork:
                    BoxC(m, 0, 0, 0.6, 0.6, 0, 1.0, Stone);
                    MeshKit.Blob(m, 0, 1.75, 0, 0.45, 0.75, 0.45, 0x8E8A8000);
                    break;
                case StreetPropKind.PowerTower:
                    MeshKit.Frustum(m, 0, 0, 3.5, 0, 0.8, 30, 4, false, 0x9EA3A800);
                    BoxC(m, 0, 0, 6, 0.3, 24, 24.5, 0x9EA3A800);
                    BoxC(m, 0, 0, 4.5, 0.3, 27, 27.5, 0x9EA3A800);
                    break;
                case StreetPropKind.PowerPole:
                    MeshKit.Frustum(m, 0, 0, 0.16, 0, 0.1, 9, 4, false, pole);
                    BoxC(m, 0, 0, 0.8, 0.06, 8.2, 8.32, pole);
                    break;
                case StreetPropKind.Well:
                    MeshKit.Cylinder(m, 0, 0, 0.8, 0, 0.8, 10, false, Stone);
                    MeshKit.Cylinder(m, 0, 0, 0.62, 0.79, 0.8, 10, true, 0x2B3B4A00);
                    break;
                case StreetPropKind.SolarPanel:
                    BoxC(m, 0, 0.6, 0.05, 0.05, 0, 1.2, dark);
                    BoxC(m, 0, -0.6, 0.05, 0.05, 0, 0.6, dark);
                    MeshKit.Quad(m, -1, 0.6, -0.75, 1, 0.6, -0.75, 1, 1.4, 0.75, -1, 1.4, 0.75, 0, 1, -1, 0x1E3A6B00);
                    break;
                case StreetPropKind.Chimney:
                    MeshKit.Frustum(m, 0, 0, 1.4, 0, 0.9, 18, 8, true, 0xA0523A00);
                    break;
            }
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // People (rigid parts)

        /// <summary>Parts of a person: torso and head (one mesh), and limbs pivoting at the hip and shoulder.</summary>
        public enum PersonPart : byte
        {
            Body = 0,
            LegLeft = 1,
            LegRight = 2,
            ArmLeft = 3,
            ArmRight = 4,
        }

        public const float HipY = 0.82f, ShoulderY = 1.32f, ShoulderX = 0.21f, HipX = 0.09f;

        /// <summary>
        /// One part of a 1.62 m cartoon person (W2_DESIGN 5.4 crowds; the player avatar is Track D's). The body carries
        /// clothes (tinted), skin and hair; legs (trousers, tinted darker by the instance) and arms are separate meshes
        /// pivoting at <see cref="HipY"/> / <see cref="ShoulderY"/> so the near crowd can swing them. LOD 2 is the whole
        /// figure as a 5-box block-out for the far crowd. <paramref name="headgear"/>: 0 none, 1 topi, 2 police cap, 3
        /// monk (shaved head, robe over the shoulder).
        /// </summary>
        public static int Person(PersonPart part, int lod, int headgear, MeshData m)
        {
            const uint skin = 0xC68B5E00, hair = 0x1E1A1700, shoe = 0x2A242000;
            int t0 = m.TriangleCount;
            int sides = lod == 0 ? 8 : 6;
            if (lod >= 2)
            {
                // Block-out: legs, torso, head (whole figure, no separate limbs).
                BoxC(m, 0, 0, 0.16, 0.1, 0.0, HipY, Tinted);
                BoxC(m, 0, 0, 0.21, 0.12, HipY, ShoulderY + 0.04, Tinted);
                BoxC(m, 0, 0, 0.11, 0.11, ShoulderY + 0.04, 1.62, headgear == 3 ? skin : hair);
                return m.TriangleCount - t0;
            }
            switch (part)
            {
                case PersonPart.Body:
                    MeshKit.Frustum(m, 0, 0, 0.17, HipY - 0.04, 0.2, ShoulderY + 0.02, sides, true, Tinted);
                    MeshKit.Cylinder(m, 0, 0, 0.05, ShoulderY + 0.02, ShoulderY + 0.1, 5, false, skin);
                    // Head: a chunky cartoon head (0.26 m), hair cap on top.
                    MeshKit.Frustum(m, 0, 0, 0.12, ShoulderY + 0.08, 0.13, ShoulderY + 0.22, sides, false, skin);
                    MeshKit.Dome(m, 0, 0, 0.13, ShoulderY + 0.22, 0.13, sides, lod == 0 ? 3 : 2, headgear == 3 ? skin : hair);
                    if (lod == 0)
                    {
                        // Eyes (forward = +Z).
                        BoxC(m, -0.045, 0.125, 0.018, 0.008, ShoulderY + 0.17, ShoulderY + 0.21, 0x1A1A1A00);
                        BoxC(m, 0.045, 0.125, 0.018, 0.008, ShoulderY + 0.17, ShoulderY + 0.21, 0x1A1A1A00);
                    }
                    if (headgear == 1) MeshKit.Frustum(m, 0, 0, 0.135, ShoulderY + 0.3, 0.12, ShoulderY + 0.4, sides, true, 0x8E3B5A00);
                    else if (headgear == 2)
                    {
                        MeshKit.Cylinder(m, 0, 0, 0.14, ShoulderY + 0.3, ShoulderY + 0.38, sides, true, 0xF2F0E800);
                        BoxC(m, 0, 0.12, 0.1, 0.06, ShoulderY + 0.3, ShoulderY + 0.32, 0x1A1A1A00);
                    }
                    else if (headgear == 3) MeshKit.Bar(m, -0.17, ShoulderY - 0.1, 0.05, 0.12, HipY + 0.1, -0.05, 0.08, 0x7A1F1F00);
                    break;
                case PersonPart.LegLeft:
                case PersonPart.LegRight:
                {
                    double x = part == PersonPart.LegLeft ? -HipX : HipX;
                    // Pivot at the hip: the leg hangs down from y = 0 to −HipY (the instance matrix lifts it to the hip).
                    MeshKit.Frustum(m, x, 0, 0.075, -HipY + 0.06, 0.085, 0, sides, true, Tinted);
                    BoxC(m, x, 0.04, 0.05, 0.11, -HipY, -HipY + 0.07, shoe);
                    break;
                }
                default:
                {
                    double x = part == PersonPart.ArmLeft ? -ShoulderX : ShoulderX;
                    // Pivot at the shoulder: the arm hangs to −0.58 m; the hand is skin.
                    MeshKit.Frustum(m, x, 0, 0.045, -0.5, 0.055, 0, sides, false, Tinted);
                    MeshKit.Blob(m, x, -0.54, 0, 0.05, 0.06, 0.05, skin);
                    break;
                }
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Carry props of the crowd (<c>PedPose.CarryProp</c>, W2_DESIGN 5.4): 1 doko, 2 sack, 3 gas cylinder,
        /// 4 baby, 5 umbrella.</summary>
        public const int CarryProps = 6;

        /// <summary>
        /// A carry prop in the person's body frame (moves and bows with the torso; forward = +Z, so the back is −Z): the
        /// doko is a conical bamboo basket on the back with its namlo strap to the forehead, the sack a hessian bundle, the
        /// gas cylinder a red LPG bottle on the back, the baby a shawl bundle with a little head, the umbrella a black
        /// canopy held over the head. Fixed colours (vertex alpha 0: never tinted). <paramref name="kind"/> 0 or out of
        /// range builds nothing.
        /// </summary>
        public static int Carry(int kind, int lod, MeshData m)
        {
            const uint bamboo = 0xB08A4E00, strap = 0x6B4A2E00, hessian = 0xC9B48A00, gas = 0xC0392B00, valve = 0x8A8A8A00;
            const uint shawl = 0xD84A7A00, skin = 0xC68B5E00, canopy = 0x22222200, pole = 0x5A4A3A00;
            int t0 = m.TriangleCount;
            int sides = lod == 0 ? 10 : 6;
            switch (kind)
            {
                case 1:
                    MeshKit.Frustum(m, 0, -0.32, 0.13, HipY + 0.02, 0.27, ShoulderY + 0.2, sides, true, bamboo);
                    if (lod == 0) MeshKit.Frustum(m, 0, -0.32, 0.275, ShoulderY + 0.14, 0.285, ShoulderY + 0.2, sides, false, strap);
                    MeshKit.Bar(m, 0, ShoulderY + 0.16, -0.12, 0, ShoulderY + 0.3, 0.1, 0.025, strap); // namlo over the head
                    break;
                case 2:
                    MeshKit.Blob(m, 0, ShoulderY - 0.12, -0.28, 0.2, 0.26, 0.15, hessian);
                    break;
                case 3:
                    MeshKit.Cylinder(m, 0, -0.3, 0.15, HipY + 0.05, ShoulderY + 0.05, sides, false, gas);
                    MeshKit.Dome(m, 0, -0.3, 0.15, ShoulderY + 0.05, 0.08, sides, 2, gas);
                    MeshKit.Cylinder(m, 0, -0.3, 0.035, ShoulderY + 0.12, ShoulderY + 0.18, 5, true, valve);
                    break;
                case 4:
                    MeshKit.Blob(m, 0, ShoulderY - 0.18, -0.22, 0.17, 0.22, 0.13, shawl);
                    MeshKit.Blob(m, 0, ShoulderY + 0.08, -0.24, 0.08, 0.08, 0.08, skin);
                    break;
                case 5:
                    MeshKit.Bar(m, ShoulderX + 0.02, HipY + 0.3, 0.18, 0.08, 2.05, 0.06, 0.012, pole);
                    MeshKit.Frustum(m, 0.08, 0.06, 0.55, 1.98, 0.04, 2.22, sides, true, canopy);
                    break;
            }
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Animals

        /// <summary>A humped zebu cow (W2_DESIGN 5.5), standing or lying (chewing), LOD 0 or 1 (2: block-out).
        /// The coat is tinted per instance; horns, hooves and muzzle are fixed.</summary>
        public static int Cow(bool lying, int lod, MeshData m)
        {
            const uint horn = 0xD8CDB200, hoof = 0x2B262200, muzzle = 0x3A302A00;
            int t0 = m.TriangleCount;
            double body = lying ? 0.55 : 0.95;
            if (lod >= 2)
            {
                BoxC(m, 0, 0, 0.32, 0.8, body - 0.35, body + 0.3, Tinted);
                BoxC(m, 0, 0.95, 0.14, 0.2, body, body + 0.35, Tinted);
                if (!lying) BoxC(m, 0, 0, 0.25, 0.65, 0, body - 0.35, hoof);
                return m.TriangleCount - t0;
            }
            MeshKit.Blob(m, 0, body, 0, 0.36, 0.34, 0.85, Tinted);
            if (lod == 0) MeshKit.Blob(m, 0, body + 0.05, 0, 0.33, 0.3, 0.7, Tinted);
            MeshKit.Blob(m, 0, body + 0.3, 0.45, 0.18, 0.2, 0.2, Tinted); // hump
            MeshKit.Blob(m, 0, body + 0.2, 1.0, 0.15, 0.2, 0.25, Tinted); // head
            MeshKit.Blob(m, 0, body + 0.08, 1.22, 0.1, 0.09, 0.08, muzzle);
            MeshKit.Bar(m, -0.08, body + 0.38, 1.0, -0.2, body + 0.55, 0.95, 0.04, horn);
            MeshKit.Bar(m, 0.08, body + 0.38, 1.0, 0.2, body + 0.55, 0.95, 0.04, horn);
            if (!lying)
                for (int i = 0; i < 4; i++)
                {
                    double x = i % 2 == 0 ? -0.18 : 0.18, z = i < 2 ? -0.55 : 0.55;
                    MeshKit.Bar(m, x, 0.05, z, x, body - 0.2, z, 0.11, Tinted);
                    BoxC(m, x, z, 0.06, 0.06, 0, 0.08, hoof);
                }
            else
            {
                BoxC(m, -0.3, 0.6, 0.08, 0.25, 0.1, 0.22, Tinted); // folded legs
                BoxC(m, 0.3, -0.4, 0.08, 0.25, 0.1, 0.22, Tinted);
            }
            MeshKit.Bar(m, 0, body + 0.1, -0.85, 0, body - 0.45, -0.95, 0.04, Tinted); // tail
            return m.TriangleCount - t0;
        }

        /// <summary>A street dog (W2_DESIGN 5.5), standing or curled asleep, LOD 0 or 1 (2: block-out). Coat tinted.</summary>
        public static int Dog(bool asleep, int lod, MeshData m)
        {
            const uint nose = 0x1A1A1A00;
            int t0 = m.TriangleCount;
            if (asleep)
            {
                MeshKit.Blob(m, 0, 0.16, 0, 0.3, 0.16, 0.32, Tinted);
                MeshKit.Blob(m, 0.12, 0.2, 0.22, 0.11, 0.1, 0.12, Tinted);
                return m.TriangleCount - t0;
            }
            if (lod >= 2)
            {
                BoxC(m, 0, 0, 0.12, 0.32, 0.28, 0.5, Tinted);
                BoxC(m, 0, 0.38, 0.08, 0.1, 0.42, 0.6, Tinted);
                return m.TriangleCount - t0;
            }
            MeshKit.Blob(m, 0, 0.45, 0, 0.14, 0.13, 0.33, Tinted);
            MeshKit.Blob(m, 0, 0.6, 0.36, 0.1, 0.1, 0.13, Tinted);
            MeshKit.Blob(m, 0, 0.57, 0.5, 0.03, 0.03, 0.03, nose);
            MeshKit.Tri(m, -0.07, 0.66, 0.34, -0.03, 0.66, 0.36, -0.06, 0.76, 0.33, 0, 0, 1, Tinted); // ears
            MeshKit.Tri(m, 0.07, 0.66, 0.34, 0.03, 0.66, 0.36, 0.06, 0.76, 0.33, 0, 0, 1, Tinted);
            for (int i = 0; i < 4; i++)
            {
                double x = i % 2 == 0 ? -0.07 : 0.07, z = i < 2 ? -0.22 : 0.22;
                MeshKit.Bar(m, x, 0.0, z, x, 0.4, z, 0.05, Tinted);
            }
            MeshKit.Bar(m, 0, 0.5, -0.3, 0, 0.62, -0.48, 0.035, Tinted);
            return m.TriangleCount - t0;
        }

        /// <summary>A light stud (airport and aircraft lights): an octahedron of unit size.</summary>
        public static int LightStud(MeshData m)
        {
            return MeshKit.Blob(m, 0, 0, 0, 0.5, 0.5, 0.5, 0xFFFFFFFF);
        }
    }
}
