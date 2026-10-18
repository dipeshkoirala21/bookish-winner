using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>The facade family of a house round the kora (boudha.md 3.7) or the gompa a footprint carries.</summary>
    internal enum HouseLook : byte
    {
        /// <summary>Painted RCC house: coloured plaster, white Rana-style window pediments, balconies with railings.</summary>
        Painted = 0,

        /// <summary>Exposed red brick Newar-revival front: dark carved wooden windows, tiled cornice bands.</summary>
        Brick = 1,

        /// <summary>Ochre plaster with green louvred shutters and white stucco pediments under a brown tiled hip roof
        /// (the Guru Lhakhang's east neighbour, bell/09).</summary>
        Ochre = 2,

        /// <summary>Guru Lhakhang (Mahaguru Gompa, "Tamang Gompa"; <see cref="BoudhaGompas"/>).</summary>
        GuruLhakhang = 3,

        /// <summary>Jamchen Lhakhang (Sakya Maitreya Lhakhang; <see cref="BoudhaGompas"/>).</summary>
        Jamchen = 4,

        /// <summary>Old Dhabzang gompa (<see cref="BoudhaGompas"/>).</summary>
        Dhabzang = 5,
    }

    /// <summary>Extras of a ring house.</summary>
    [Flags]
    internal enum HouseFlags : byte
    {
        None = 0,

        /// <summary>A rooftop café facing the stupa: colourful umbrellas over tables.</summary>
        Cafe = 1,

        /// <summary>A pitched clay-tile hip roof (red, with dormers) instead of a flat roof.</summary>
        Pitched = 2,

        /// <summary>Balconies with railings on the upper storeys of the front.</summary>
        Balcony = 4,

        /// <summary>A black water tank on a steel stand and a solar water heater on the roof.</summary>
        Tanks = 8,

        /// <summary>A stair room (mumty) on the roof.</summary>
        Mumty = 16,

        /// <summary>A rooftop pole with a vertical five-colour banner.</summary>
        FlagPole = 32,

        /// <summary>A street house (the lane to the main gate and Boudha Main Road): every free face of 3 m or more is a
        /// shop front, not only the faces toward the stupa.</summary>
        Street = 64,

        /// <summary>Newar-revival tile pent eaves (jhingati) along the fronts at every floor line above the first storey
        /// and at the roof line (the brick houses either side of the main gate).</summary>
        Eaves = 128,
    }

    /// <summary>One building round the kora that the site rebuilds: OSM way, the quarter that builds it, storeys (the
    /// pipeline's levels, checked on the photos), facade family, wall colour, awning colour and extras.</summary>
    internal struct RingHouse
    {
        public ulong Way;
        public BoudhaPart Part;
        public byte Storeys;
        public HouseLook Look;
        public uint Wall;
        public byte Awning;
        public HouseFlags Flags;

        public RingHouse(ulong way, BoudhaPart part, byte storeys, HouseLook look, uint wall, byte awning, HouseFlags flags)
        {
            Way = way;
            Part = part;
            Storeys = storeys;
            Look = look;
            Wall = wall;
            Awning = awning;
            Flags = flags;
        }
    }

    /// <summary>
    /// S7, the ring of houses round the kora (boudha.md 3.7): every front-row building on its OSM footprint as a 3-6
    /// storey kora house of one of the facade families of the photos (painted RCC in ochre, brick red, salmon, cream or
    /// pale green with white Rana-style pediments; exposed red brick with dark carved windows; ochre with green shutters),
    /// the ground floor opened into shops with rolled-up shutters, hanging goods, signboards and striped awnings on every
    /// face toward the kora, a string course, cornice and parapet, balconies, and on the roofs the cafés with umbrellas,
    /// water tanks, solar heaters, stair rooms, banner poles or a red clay-tile hip roof with dormers (gate2/00, bell/09,
    /// commons_late/c03, corner/04). Faces against a neighbour stay plain. Thin walls along every outline edge are the
    /// colliders. Gompa footprints are built by <see cref="BoudhaGompas"/>.
    /// </summary>
    internal static class BoudhaRing
    {
        /// <summary>Section of a house (m above the kora): plinth, the shop storey, each upper storey, the parapet and
        /// the hip roof.</summary>
        public const double Plinth = 0.15, ShopH = 3.4, FloorH = 3.0, ParapetH = 1.0, RoofRise = 2.0, Eave = 0.45;

        /// <summary>Rooftop extras above the roof slab (m).</summary>
        public const double MumtyH = 2.6, CafeH = 2.55, TankH = 2.2, PoleH = 4.5;

        private const uint Ochre = 0xE8C35A, BrickRed = 0xB5442E, Salmon = 0xE39A7A, Cream = 0xF1E6CC, PaleGreen = 0xBFD8B0, White = 0xF2EFE8;
        private const uint Orange = 0xE58A3A, ExposedBrick = 0xA64B32, OchreDeep = 0xE3B84E, Pink = 0xE7A9A0;

        private const HouseFlags C = HouseFlags.Cafe, P = HouseFlags.Pitched, B = HouseFlags.Balcony, T = HouseFlags.Tanks, M = HouseFlags.Mumty,
                                 F = HouseFlags.FlagPole, St = HouseFlags.Street, Ev = HouseFlags.Eaves;

        private const BoudhaPart N = BoudhaPart.North, E = BoudhaPart.East, S = BoudhaPart.South, W = BoudhaPart.West;

        /// <summary>The front row clockwise from north (boudha.md 3.7, OSM; the quarter by the footprint's centroid).</summary>
        public static readonly RingHouse[] Houses =
        {
            new RingHouse(520755992, N, 3, HouseLook.GuruLhakhang, OchreDeep, 0, HouseFlags.None),
            new RingHouse(520756005, N, 3, HouseLook.Ochre, OchreDeep, 1, P),
            new RingHouse(517234573, N, 3, HouseLook.Painted, Cream, 2, C | T),
            new RingHouse(911869006, N, 5, HouseLook.Brick, ExposedBrick, 3, B | T | M),
            new RingHouse(911869007, N, 3, HouseLook.Painted, Salmon, 0, C),
            new RingHouse(911869008, N, 3, HouseLook.Painted, PaleGreen, 4, T | F),
            new RingHouse(209312611, N, 3, HouseLook.Dhabzang, White, 0, HouseFlags.None),
            new RingHouse(912264293, N, 4, HouseLook.Brick, ExposedBrick, 1, C | M),
            new RingHouse(912264294, E, 5, HouseLook.Painted, Ochre, 2, T),
            new RingHouse(912264291, E, 3, HouseLook.Painted, White, 3, F),
            new RingHouse(912264292, E, 2, HouseLook.Painted, Cream, 0, HouseFlags.None),
            new RingHouse(912264454, E, 4, HouseLook.Painted, BrickRed, 4, B | T),
            new RingHouse(912264453, E, 6, HouseLook.Brick, ExposedBrick, 1, C | M),
            new RingHouse(912264591, E, 4, HouseLook.Painted, Ochre, 2, B | P),
            new RingHouse(912264590, E, 4, HouseLook.Painted, Salmon, 3, C | T),
            new RingHouse(912264589, E, 6, HouseLook.Painted, Cream, 0, B | M | F),
            new RingHouse(912264741, E, 4, HouseLook.Brick, ExposedBrick, 4, T),
            new RingHouse(912269948, E, 5, HouseLook.Painted, PaleGreen, 1, C),
            new RingHouse(912269941, E, 6, HouseLook.Painted, Orange, 2, B | T),
            new RingHouse(211905448, E, 4, HouseLook.Painted, Cream, 0, C | F),
            new RingHouse(912269936, E, 3, HouseLook.Brick, ExposedBrick, 4, M),
            new RingHouse(912270352, E, 5, HouseLook.Painted, Ochre, 1, C | T),
            new RingHouse(912270787, S, 4, HouseLook.Painted, Pink, 2, B | M),
            new RingHouse(912276969, S, 4, HouseLook.Painted, BrickRed, 3, P | B),
            new RingHouse(912276521, S, 4, HouseLook.Painted, Salmon, 0, C | T),
            new RingHouse(912276520, S, 6, HouseLook.Brick, ExposedBrick, 4, P | St),
            // The lane to the main gate (Commons "Boudhanath entrance gate and its premises", 2019-2022): exposed brick
            // with tile pent eaves west of the gate, the red-brick front with white window frames beyond it.
            new RingHouse(911869018, S, 4, HouseLook.Brick, ExposedBrick, 1, P | St | Ev),
            new RingHouse(517160028, S, 1, HouseLook.Painted, White, 2, HouseFlags.None),
            new RingHouse(911869019, S, 5, HouseLook.Painted, BrickRed, 3, P | St | Ev),
            new RingHouse(912278963, S, 4, HouseLook.Painted, Ochre, 0, C),
            new RingHouse(912278964, S, 5, HouseLook.Painted, Cream, 4, T | M),
            new RingHouse(912278965, S, 5, HouseLook.Brick, ExposedBrick, 1, C | F),
            new RingHouse(912280177, W, 6, HouseLook.Brick, ExposedBrick, 2, B | T | Ev),
            new RingHouse(912280176, W, 5, HouseLook.Painted, OchreDeep, 3, C),
            new RingHouse(911869025, W, 5, HouseLook.Brick, ExposedBrick, 0, C | M),
            new RingHouse(911869026, W, 5, HouseLook.Painted, Ochre, 4, B | T),
            new RingHouse(911869027, W, 5, HouseLook.Painted, Cream, 1, C | F),
            new RingHouse(911869028, W, 4, HouseLook.Painted, BrickRed, 2, P | B),
            new RingHouse(209530023, W, 2, HouseLook.Jamchen, OchreDeep, 0, HouseFlags.None),
            new RingHouse(911869029, W, 3, HouseLook.Brick, ExposedBrick, 3, T),
            new RingHouse(912280174, W, 4, HouseLook.Brick, ExposedBrick, 4, C | M | Ev),
            new RingHouse(912280388, W, 4, HouseLook.Painted, Salmon, 0, B | T),
            new RingHouse(209312596, W, 3, HouseLook.Painted, Ochre, 1, C),
            new RingHouse(517236636, W, 3, HouseLook.Brick, ExposedBrick, 2, T | F),
            new RingHouse(517236635, W, 3, HouseLook.Painted, BrickRed, 3, B | M),
            new RingHouse(912261800, W, 4, HouseLook.Painted, BrickRed, 4, C),
            new RingHouse(912261799, N, 5, HouseLook.Painted, Ochre, 0, B | T | M),
            new RingHouse(912262363, N, 4, HouseLook.Brick, ExposedBrick, 1, C),
            new RingHouse(912262534, N, 4, HouseLook.Painted, Salmon, 2, B | F),
            // East of the main gate on Boudha Main Road (the ATM front), built with the gate.
            new RingHouse(912276519, BoudhaPart.Gate, 5, HouseLook.Brick, ExposedBrick, 2, St | Ev | T),
        };

        /// <summary>Awning stripe colours (alternating with white), by <see cref="RingHouse.Awning"/>.</summary>
        private static readonly uint[] AwningColours = { 0xE8B530, 0x2F8F4E, 0xC8282E, 0x2E5DA8, 0xE07A2A };

        /// <summary>Signboard colours.</summary>
        private static readonly uint[] SignColours = { 0xC8282E, 0x2E5DA8, 0xF2C230, 0x2F7A3E, 0xF4F2EC, 0x7A1F1F };

        /// <summary>Goods hanging in the shop openings (thangkas, scarves, prayer flags, singing bowls).</summary>
        private static readonly uint[] GoodsColours = { 0xC8282E, 0xE8B530, 0x2E5DA8, 0x9E2430, 0xC9A050, 0x2F8F4E, 0xE07A2A };

        /// <summary>Umbrella colours of the rooftop cafés.</summary>
        private static readonly uint[] UmbrellaColours = { 0xC8282E, 0x2F8F4E, 0x2E5DA8, 0xE8B530, 0xF4F2EC };

        /// <summary>Top of a house body (the roof slab) above the kora.</summary>
        public static double BodyTop(int storeys)
        {
            return Plinth + ShopH + Math.Max(0, storeys - 1) * FloorH;
        }

        /// <summary>The highest point of a house (hip roof ridge, café umbrellas, stair room, banner pole or parapet).</summary>
        public static double HouseTop(in RingHouse h)
        {
            double b = BodyTop(h.Storeys);
            if ((h.Flags & HouseFlags.Pitched) != 0) return b + RoofRise + 0.1;
            double t = ParapetH + 0.06;
            if ((h.Flags & HouseFlags.Cafe) != 0) t = Math.Max(t, CafeH);
            if ((h.Flags & HouseFlags.Mumty) != 0) t = Math.Max(t, MumtyH + 0.12);
            if ((h.Flags & HouseFlags.Tanks) != 0) t = Math.Max(t, TankH);
            if ((h.Flags & HouseFlags.FlagPole) != 0) t = Math.Max(t, PoleH + 0.2);
            return b + t;
        }

        /// <summary>The tallest structure of a kora quarter above the kora: its houses, its gompas and the flagpoles on
        /// the compound wall.</summary>
        public static double TopOf(BoudhaPart p)
        {
            double t = BoudhaWall.PoleTop + BoudhaWall.PoleTipH;
            for (int i = 0; i < Houses.Length; i++)
            {
                if (Houses[i].Part != p) continue;
                double h;
                switch (Houses[i].Look)
                {
                    case HouseLook.GuruLhakhang: h = BoudhaGompas.GuruTop; break;
                    case HouseLook.Jamchen: h = BoudhaGompas.JamchenTop; break;
                    case HouseLook.Dhabzang: h = BoudhaGompas.DhabzangTop; break;
                    default: h = HouseTop(Houses[i]); break;
                }
                t = Math.Max(t, h);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------ scratch

        [ThreadStatic] private static double[] _hu, _hv, _ou, _ov;
        [ThreadStatic] private static byte[] _kind;

        private const byte KindPlain = 0, KindFront = 1, KindSide = 2;

        /// <summary>The footprint of OSM way <paramref name="way"/> in site-local (u, v), counter-clockwise, without the
        /// closing point, into per-thread scratch; 0 when the tile does not hold it.</summary>
        internal static int Footprint(TileData tile, in BoudhaFrame f, ulong way, out double[] pu, out double[] pv)
        {
            if (_hu == null)
            {
                _hu = new double[128];
                _hv = new double[128];
                _ou = new double[128];
                _ov = new double[128];
                _kind = new byte[128];
            }
            pu = _hu;
            pv = _hv;
            ulong osm = 2 * way;
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                if (tile.Buildings[i].OsmRef != osm) continue;
                int[] ring = tile.Buildings[i].Rings[0];
                int n = Math.Min(ring.Length / 2, 127);
                int k = 0;
                for (int j = 0; j < n; j++)
                {
                    double u, v;
                    f.ToSite(ring[2 * j] / 100.0, ring[2 * j + 1] / 100.0, out u, out v);
                    if (k > 0 && Math.Abs(u - pu[k - 1]) < 0.05 && Math.Abs(v - pv[k - 1]) < 0.05) continue;
                    pu[k] = u;
                    pv[k] = v;
                    k++;
                }
                while (k >= 3 && Math.Abs(pu[0] - pu[k - 1]) < 0.05 && Math.Abs(pv[0] - pv[k - 1]) < 0.05) k--;
                if (k < 3) return 0;
                BoudhaKit.Ccw(pu, pv, k);
                double a0 = Math.Abs(BoudhaKit.Area(pu, pv, k));
                OffRoads(tile, f, pu, pv, k);
                // A house the drawn road leaves no room for is not built (w912264741, a 17 m² sliver at the east lane
                // mouth: its 5 m deep plan lies 3.4 m into the drawn kora).
                double a1 = Math.Abs(BoudhaKit.Area(pu, pv, k));
                if (a1 < Math.Max(MinFootprintM2, MinKeptShare * a0)) return 0;
                return k;
            }
            return 0;
        }

        /// <summary>Clear distance (m) a house outline (its collider walls are 0.2 m half thick) keeps from the edge of a
        /// carriageway as the road mesher draws it.</summary>
        public const double RoadClearM = 0.3;

        /// <summary>A footprint cut back off the roads to less than this area (m²) or share of its OSM plan is dropped.</summary>
        public const double MinFootprintM2 = 8.0, MinKeptShare = 0.35;

        [ThreadStatic] private static double[] _need;

        /// <summary>
        /// Cuts the faces of a footprint (site-local, counter-clockwise) back off the drawn roads: the kora street and the
        /// lanes are drawn wider than the gaps between the OSM outlines in places (up to 3.8 m into the east row), and
        /// nothing solid may stand on them (rideability decision 1). Every corner closer than <see cref="RoadClearM"/> to
        /// a drawn edge slides back along its side wall (the neighbouring corner farther from the road; toward the
        /// footprint's middle when both neighbours stand in the road) until it clears, so the front moves back along the
        /// party walls; a face whose middle still reaches the road (a straight front along the curved kora) takes its
        /// corners back by the overlap. Faces away from the roads keep the OSM line.
        /// </summary>
        private static void OffRoads(TileData tile, in BoudhaFrame f, double[] pu, double[] pv, int n)
        {
            if (tile == null) return;
            SacredDrawnRoads roads = SacredDrawnRoads.For(tile);
            if (_need == null || _need.Length < pu.Length) _need = new double[pu.Length];
            double[] need = _need;
            for (int it = 0; it < 6; it++)
            {
                double cu = 0, cv = 0;
                for (int i = 0; i < n; i++)
                {
                    need[i] = RoadClearM;
                    cu += pu[i] / n;
                    cv += pv[i] / n;
                }
                // Faces reaching the road between their corners raise the clearance their corners need.
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double eu = pu[j] - pu[i], ev = pv[j] - pv[i], l = Math.Sqrt(eu * eu + ev * ev);
                    double di = Drawn(roads, f, pu[i], pv[i]), dj = Drawn(roads, f, pu[j], pv[j]);
                    // Only once both corners clear (a face running into the road from a corner in it moves with the corner).
                    if (di < RoadClearM - 1e-3 || dj < RoadClearM - 1e-3) continue;
                    int q = Math.Max(2, (int)Math.Ceiling(l / 0.25));
                    double worst = 0;
                    for (int s = 1; s < q; s++)
                    {
                        double t = (double)s / q;
                        worst = Math.Max(worst, RoadClearM - Drawn(roads, f, pu[i] + eu * t, pv[i] + ev * t));
                    }
                    if (worst <= 1e-3) continue;
                    need[i] = Math.Max(need[i], di + worst + 0.02);
                    need[j] = Math.Max(need[j], dj + worst + 0.02);
                }
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    double d0 = Drawn(roads, f, pu[i], pv[i]);
                    if (d0 >= need[i] - 1e-3) continue;
                    any = true;
                    int p = (i + n - 1) % n, q = (i + 1) % n;
                    double dp = Drawn(roads, f, pu[p], pv[p]), dq = Drawn(roads, f, pu[q], pv[q]);
                    double tu, tv;
                    if (Math.Max(dp, dq) >= need[i])
                    {
                        int o = dp >= dq ? p : q;
                        tu = pu[o];
                        tv = pv[o];
                    }
                    else
                    {
                        tu = cu;
                        tv = cv;
                    }
                    // The first point toward the target that clears (bisection; at most 85 % of the way).
                    double lo = 0, hi = 0.85;
                    if (Drawn(roads, f, pu[i] + (tu - pu[i]) * hi, pv[i] + (tv - pv[i]) * hi) >= need[i])
                    {
                        for (int k = 0; k < 14; k++)
                        {
                            double mid = 0.5 * (lo + hi);
                            if (Drawn(roads, f, pu[i] + (tu - pu[i]) * mid, pv[i] + (tv - pv[i]) * mid) >= need[i]) hi = mid;
                            else lo = mid;
                        }
                    }
                    pu[i] += (tu - pu[i]) * hi;
                    pv[i] += (tv - pv[i]) * hi;
                }
                if (!any) return;
            }
        }

        private static double Drawn(SacredDrawnRoads roads, in BoudhaFrame f, double u, double v)
        {
            double x, z;
            f.ToTile(u, v, out x, out z);
            return roads.SignedDistance(x, z);
        }

        /// <summary>True when the site-local point lies inside another building of the tile (the party-wall test).</summary>
        internal static bool InsideAnyBuilding(TileData tile, in BoudhaFrame f, ulong self, double u, double v)
        {
            double x, z;
            f.ToTile(u, v, out x, out z);
            int xc = (int)Math.Round(x * 100), zc = (int)Math.Round(z * 100);
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                BuildingRecord b = tile.Buildings[i];
                if (b.OsmRef == self || (b.Flags & BuildingFlags.Part) != 0) continue;
                int[] r = b.Rings[0];
                int n = r.Length / 2;
                // Cheap reject on the first vertex (no footprint near the kora is wider than 200 m).
                if (Math.Abs(r[0] - xc) > 20000 || Math.Abs(r[1] - zc) > 20000) continue;
                bool inside = false;
                for (int a = 0, c = n - 1; a < n; c = a++)
                {
                    int ax = r[2 * a], az = r[2 * a + 1], cx = r[2 * c], cz = r[2 * c + 1];
                    if ((az > zc) != (cz > zc) && xc < (double)(cx - ax) * (zc - az) / (cz - az) + ax) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ build

        /// <summary>Every house of a quarter (not its gompas) at one LOD.</summary>
        public static void Build(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            for (int i = 0; i < Houses.Length; i++)
            {
                RingHouse h = Houses[i];
                if (h.Part != part || h.Look >= HouseLook.GuruLhakhang) continue;
                double[] pu, pv;
                int n = Footprint(tile, f, h.Way, out pu, out pv);
                if (n < 3) continue;
                House(m, c, tile, f, h, pu, pv, n, lod, i);
            }
        }

        /// <summary>One house on its footprint (site-local, counter-clockwise).</summary>
        internal static void House(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, in RingHouse h, double[] pu, double[] pv, int n, int lod,
                                   int seed)
        {
            Affine3 k = f.Site;
            bool brick = h.Look == HouseLook.Brick;
            ShapeBrush wall = BoudhaKit.B(MeshColor.FromHex(h.Wall), brick ? MaterialChannel.Brick : MaterialChannel.Plaster);
            ShapeBrush trim = brick ? BoudhaKit.B(MeshColor.FromHex(0x5A3422), MaterialChannel.Wood) : BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Plaster);
            ShapeBrush foot = BoudhaKit.B(MeshColor.Scale(MeshColor.FromHex(h.Wall), 0.72f), MaterialChannel.Plaster);
            double top = BodyTop(h.Storeys);
            bool pitched = (h.Flags & HouseFlags.Pitched) != 0;
            bool hip = false;
            double rcu = 0, rcv = 0, rau = 0, rav = 0, rha = 0, rhb = 0;
            if (pitched)
            {
                double area = Math.Abs(BoudhaKit.Area(pu, pv, n));
                hip = OrientedRect(pu, pv, n, out rcu, out rcv, out rau, out rav, out rha, out rhb) && area >= 0.82 * 4 * rha * rhb;
            }
            ClassifyEdges(tile, f, h.Way, pu, pv, n, (h.Flags & HouseFlags.Street) != 0);

            // Body: base course, the wall, a string course over the shops (and at every floor on brick fronts), the
            // cornice and, on flat roofs, the parapet with its coping.
            Mould mo = SacredDraw.M;
            if (lod >= 3)
            {
                // The far ring (rooftop and aerial views keep the quarters the hero slice steps down here): the plain
                // body to the roof, or to the parapet's top under a flat roof.
                mo.Add(0, -0.3, wall).Add(0, hip ? top : top + 0.6);
            }
            else if (lod == 2)
            {
                // The wall, the cornice ledge and (flat roofs) the parapet face and its coping; the roof slab sits just
                // under the coping so no inner face is needed.
                mo.Add(0, -0.3, wall).Add(0, top).Add(0.15, top, trim);
                if (hip) mo.Add(0.15, top + 0.25);
                else mo.Add(0.15, top + ParapetH).Add(-0.1, top + ParapetH);
            }
            else if (lod == 1)
            {
                // Base course, a flush string course over the shops, the cornice ledge and the parapet with its coping.
                double sc = Plinth + ShopH;
                mo.Add(0, -0.3, foot).Add(0, Plinth + 0.3, wall).Add(0, sc - 0.1, trim).Add(0, sc + 0.1, wall).Add(0, top - 0.25).Add(0.15, top - 0.25, trim)
                  .Add(0.15, top + 0.02);
                if (!hip) mo.Add(0.04, top + 0.02, wall).Add(0.04, top + ParapetH).Add(-0.12, top + ParapetH, trim);
            }
            else
            {
                double sc = Plinth + ShopH;
                mo.Add(0.05, -0.3, foot).Add(0.05, Plinth + 0.25).Add(0, Plinth + 0.3, wall).Add(0, sc - 0.12).Add(0.1, sc - 0.1, trim).Add(0.1, sc + 0.08)
                  .Add(0, sc + 0.1, wall);
                if (lod == 0 && brick)
                {
                    for (int s = 2; s < h.Storeys; s++)
                    {
                        double y = sc + (s - 1) * FloorH;
                        mo.Add(0, y - 0.06).Add(0.07, y - 0.05, trim).Add(0.07, y + 0.05).Add(0, y + 0.06, wall);
                    }
                }
                mo.Add(0, top - 0.3).Add(0.12, top - 0.28, trim).Add(0.2, top - 0.12).Add(0.2, top + 0.02);
                if (!hip)
                    mo.Add(0.05, top + 0.04, wall).Add(0.05, top + ParapetH - 0.08).Add(0.1, top + ParapetH - 0.06, trim).Add(0.1, top + ParapetH + 0.06)
                      .Add(-0.17, top + ParapetH + 0.06).Add(-0.17, top + ParapetH - 0.06).Add(-0.12, top + ParapetH - 0.08, wall).Add(-0.12, top + 0.02);
            }
            SacredDraw.Ring(m, k, pu, pv, n, mo);

            // Roof.
            if (hip)
            {
                HipRoof(m, k, rcu, rcv, rau, rav, rha, rhb, top + 0.02, lod);
            }
            else
            {
                double[] ou = _ou, ov = _ov;
                BoudhaKit.Offset(pu, pv, n, lod >= 3 ? 0 : lod >= 1 ? -0.1 : -0.12, ou, ov);
                ShapeBrush slab = BoudhaKit.B(MeshColor.FromHex(0xA9A49A), MaterialChannel.Concrete);
                // LOD1/2 lift the slab to just under the coping (their parapets have no inner face).
                double y = lod >= 3 ? top + 0.6 : lod >= 1 ? top + ParapetH - 0.12 : top + 0.02;
                Shapes.BevelExtrude(m, SacredDraw.At(k, 0, y - 0.06, 0), slab, ou, ov, n, 0.06, 0, 1, BevelStyle.Chamfer, true, false, 0, 30);
                if (lod <= 1) RoofExtras(m, k, h, pu, pv, n, lod == 0 ? top : y, lod, seed);
            }

            // Facades toward the kora and the lanes (far off the kora fronts only).
            {
                for (int i = 0; i < n; i++)
                {
                    if (_kind[i] == KindPlain || (lod >= 3 && _kind[i] != KindFront)) continue;
                    int j = (i + 1) % n;
                    double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                    if (l < 2.2) continue;
                    double nu = dv / l, nv = -du / l;
                    Affine3 e = BoudhaKit.Facing(k, 0.5 * (pu[i] + pu[j]), 0, 0.5 * (pv[i] + pv[j]), nu, nv);
                    Facade(m, e, h, l, _kind[i] == KindFront, lod, seed * 7 + i);
                }
            }

            // Colliders: thin walls along the outline.
            if (c != null)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double x0, z0, x1, z1;
                    f.ToTile(pu[i], pv[i], out x0, out z0);
                    f.ToTile(pu[j], pv[j], out x1, out z1);
                    c.AddWall(x0, z0, x1, z1, f.Level - 0.3, f.Level + top + 0.5, 0.2);
                }
            }
        }

        /// <summary>Mark every outline edge front, side or plain (see <see cref="House"/>) into the edge scratch; a
        /// <paramref name="street"/> house fronts every free face of 3 m or more.</summary>
        private static void ClassifyEdges(TileData tile, in BoudhaFrame f, ulong way, double[] pu, double[] pv, int n, bool street)
        {
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                _kind[i] = KindPlain;
                if (l < 1e-6) continue;
                double nu = dv / l, nv = -du / l, mu = 0.5 * (pu[i] + pu[j]), mv = 0.5 * (pv[i] + pv[j]), r = Math.Sqrt(mu * mu + mv * mv);
                double toward = -(nu * mu + nv * mv) / Math.Max(1e-6, r);
                // Against a neighbour: points just outside the edge (at its quarter points) lie inside another building.
                int blocked = 0;
                for (int q = 1; q <= 3; q++)
                {
                    double t = q / 4.0, su = pu[i] + du * t + nu * 0.7, sv = pv[i] + dv * t + nv * 0.7;
                    if (InsideAnyBuilding(tile, f, 2 * way, su, sv)) blocked++;
                }
                if (blocked >= 2) continue;
                if (toward > 0.35 || (street && l >= 3.0)) _kind[i] = KindFront;
                else if (toward > -0.6 && l >= 3.0) _kind[i] = KindSide;
            }
        }

        // ------------------------------------------------------------------------------------------------ facade

        /// <summary>
        /// One face of a house in frame <paramref name="e"/> (origin at the middle of the wall's foot, x along the wall,
        /// z out of it): shops on the ground floor (faces toward the kora), a window per bay on every upper storey and
        /// the balconies.
        /// </summary>
        private static void Facade(MeshData m, in Affine3 e, in RingHouse h, double len, bool front, int lod, int seed)
        {
            double half = 0.5 * len - 0.35;
            if (half < 0.6) return;
            int bays = Math.Max(1, (int)Math.Round(2 * half / 3.1));
            double bw = 2 * half / bays;
            if (lod >= 2)
            {
                // Fronts only: a dark band per storey and the coloured awning band over the shops (LOD2 and the far ring).
                if (!front) return;
                ShapeBrush glass = BoudhaKit.B(MeshColor.FromHex(h.Look == HouseLook.Brick ? 0x2E2420u : 0x3A342Eu), MaterialChannel.Glass, 0.8f);
                for (int s = 1; s < h.Storeys; s++)
                {
                    double y = Plinth + ShopH + (s - 1) * FloorH + 0.85;
                    SacredDraw.Panel(m, e, -half + 0.3, y, half - 0.3, y + 1.4, 0.02, glass);
                }
                if (front)
                {
                    SacredDraw.Panel(m, e, -half, Plinth, half, Plinth + 2.4, 0.02, BoudhaKit.B(MeshColor.FromHex(0x231E1A), MaterialChannel.Plain, 0.55f));
                    SacredDraw.Quad(m, e, -half, Plinth + 2.75, 0.02, half, Plinth + 2.75, 0.02, half, Plinth + 2.4, 0.9, -half, Plinth + 2.4, 0.9, 0, 0.93, 0.36,
                                    BoudhaKit.B(MeshColor.FromHex(AwningColours[h.Awning % AwningColours.Length]), MaterialChannel.Fabric));
                }
                return;
            }
            for (int b = 0; b < bays; b++)
            {
                double xc = -half + (b + 0.5) * bw;
                int hs = Hash(seed, b);
                if (front) Shop(m, e, h, xc, bw, lod, hs);
                else if (h.Storeys <= 1) Window(m, e, h, xc, Plinth + 1.0, lod, hs);
                for (int s = 1; s < h.Storeys; s++)
                {
                    double y = Plinth + ShopH + (s - 1) * FloorH + 0.85;
                    Window(m, e, h, xc, y, lod, hs + s);
                }
            }
            if ((h.Flags & HouseFlags.Balcony) != 0 && front && h.Storeys >= 3)
            {
                for (int s = 1; s < h.Storeys; s += 2) Balcony(m, e, h, half, Plinth + ShopH + (s - 1) * FloorH, lod);
            }
            if ((h.Flags & HouseFlags.Eaves) != 0 && lod <= 1)
            {
                // Tile pent eaves at the floor lines from the second floor up (above the 4.5 m overhead clearance of the
                // lane and the road) and under the parapet.
                for (int s = 2; s < h.Storeys; s++) TileEave(m, e, 0.5 * len, Plinth + ShopH + (s - 1) * FloorH - 0.05, lod);
                if ((h.Flags & HouseFlags.Pitched) == 0) TileEave(m, e, 0.5 * len, BodyTop(h.Storeys) - 0.32, lod);
            }
        }

        /// <summary>A Newar tile pent eave (jhingati) along the wall of frame <paramref name="e"/>, its top edge on the wall
        /// at <paramref name="y"/>: the red-brown tiled slope 0.65 m out and down, the dark timber soffit and (near) the
        /// tile courses and the carved dentil band under it.</summary>
        private static void TileEave(MeshData m, in Affine3 e, double half, double y, int lod)
        {
            ShapeBrush tile = BoudhaKit.B(MeshColor.FromHex(0x8E3B2A), MaterialChannel.RoofTile), wood = BoudhaKit.B(MeshColor.FromHex(0x3A2418), MaterialChannel.Wood, 0.7f);
            double d = 0.65, drop = 0.3, x0 = -half - 0.05, x1 = half + 0.05;
            SacredDraw.Quad(m, e, x0, y, 0, x1, y, 0, x1, y - drop, d, x0, y - drop, d, 0, 0.91, 0.42, tile);
            SacredDraw.Quad(m, e, x0, y - drop - 0.06, d, x1, y - drop - 0.06, d, x1, y - 0.12, 0, x0, y - 0.12, 0, 0, -1, 0.2, wood);
            SacredDraw.Quad(m, e, x0, y - drop - 0.06, d, x1, y - drop - 0.06, d, x1, y - drop, d, x0, y - drop, d, 0, 0, 1, wood);
            if (lod > 0) return;
            // Tile courses: three darker ribs down the slope, and the dentils under the soffit.
            ShapeBrush rib = BoudhaKit.B(MeshColor.FromHex(0x6E2C20), MaterialChannel.RoofTile);
            for (int q = 1; q <= 3; q++)
            {
                double t = q / 4.0, yy = y - drop * t + 0.012, z = d * t;
                SacredDraw.Quad(m, e, x0, yy - 0.025, z + 0.02, x1, yy - 0.025, z + 0.02, x1, yy, z - 0.03, x0, yy, z - 0.03, 0, 0.91, 0.42, rib);
            }
            int dent = Math.Max(2, (int)((x1 - x0) / 0.6));
            for (int q = 0; q < dent; q++)
            {
                double x = x0 + (q + 0.5) * (x1 - x0) / dent;
                SacredDraw.Box(m, e, x - 0.06, x + 0.06, y - 0.32, y - 0.2, 0, 0.12, wood, BoxFaces.Front | BoxFaces.Bottom);
            }
        }

        private static int Hash(int a, int b)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                x ^= x >> 13;
                x *= 0x5bd1e995;
                x ^= x >> 15;
                return (int)(x & 0x7fffffff);
            }
        }

        /// <summary>A shop bay: the opening with its rolled-up shutter, goods hanging inside and on a low shelf, a
        /// signboard and the striped awning with its scalloped valance.</summary>
        private static void Shop(MeshData m, in Affine3 e, in RingHouse h, double xc, double bw, int lod, int hs)
        {
            double ow = Math.Min(2.6, bw - 0.5), x0 = xc - 0.5 * ow, x1 = xc + 0.5 * ow, y0 = Plinth, y1 = Plinth + 2.45;
            ShapeBrush dark = BoudhaKit.B(MeshColor.FromHex(0x231E1A), MaterialChannel.Plain, 0.5f);
            if (lod >= 1)
            {
                SacredDraw.Panel(m, e, x0, y0, x1, y1, 0.015, dark);
            }
            else
            {
                double d = 0.35;
                SacredDraw.Panel(m, e, x0, y0, x1, y1, -d, dark);
                ShapeBrush reveal = BoudhaKit.B(MeshColor.Scale(MeshColor.FromHex(h.Wall), 0.6f), MaterialChannel.Plaster, 0.7f);
                SacredDraw.Quad(m, e, x0, y1, -d, x1, y1, -d, x1, y1, 0, x0, y1, 0, 0, -1, 0, reveal);
                SacredDraw.Quad(m, e, x0, y0, -d, x0, y0, 0, x0, y1, 0, x0, y1, -d, 1, 0, 0, reveal);
                SacredDraw.Quad(m, e, x1, y0, -d, x1, y0, 0, x1, y1, 0, x1, y1, -d, -1, 0, 0, reveal);
                SacredDraw.Box(m, e, x0, x1, y1 - 0.22, y1, -0.12, 0.0, BoudhaKit.B(MeshColor.FromHex(0x8C9096), MaterialChannel.Metal), BoxFaces.Front | BoxFaces.Bottom);
                int items = Math.Max(2, (int)(ow / 0.7));
                for (int q = 0; q < items; q++)
                {
                    double gx = x0 + 0.15 + (ow - 0.3) * (q + 0.5) / items, gw = 0.42, gh = 0.6 + 0.25 * ((hs >> q) & 1);
                    uint col = GoodsColours[(hs + q * 3) % GoodsColours.Length];
                    SacredDraw.Panel(m, e, gx - 0.5 * gw, y1 - 0.3 - gh, gx + 0.5 * gw, y1 - 0.3, -0.2, BoudhaKit.B(MeshColor.FromHex(col), MaterialChannel.Fabric));
                }
                // A low counter of brass bowls and statues.
                SacredDraw.Box(m, e, x0 + 0.1, x1 - 0.1, y0, y0 + 0.75, -0.3, -0.05, BoudhaKit.B(MeshColor.FromHex(0x6B4A2E), MaterialChannel.Wood, 0.8f),
                               BoxFaces.Front | BoxFaces.Top);
                SacredDraw.Panel(m, e, x0 + 0.2, y0 + 0.75, x1 - 0.2, y0 + 0.95, -0.15, BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal));
            }
            // Signboard.
            uint sign = SignColours[hs % SignColours.Length];
            double sy0 = y1 + 0.45, sy1 = y1 + 0.9;
            if (lod == 0)
            {
                SacredDraw.Box(m, e, x0 + 0.05, x1 - 0.05, sy0, sy1, 0.0, 0.07, BoudhaKit.B(MeshColor.FromHex(sign), MaterialChannel.Paint),
                               BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                uint text = sign == 0xF4F2EC || sign == 0xF2C230 ? 0x7A1F1Fu : 0xF4F2ECu;
                SacredDraw.Panel(m, e, x0 + 0.3, 0.5 * (sy0 + sy1) - 0.07, x1 - 0.3, 0.5 * (sy0 + sy1) + 0.07, 0.075, BoudhaKit.B(MeshColor.FromHex(text), MaterialChannel.Paint));
            }
            else
            {
                SacredDraw.Panel(m, e, x0 + 0.05, sy0, x1 - 0.05, sy1, 0.03, BoudhaKit.B(MeshColor.FromHex(sign), MaterialChannel.Paint));
            }
            // Striped awning, drawn on both sides so it shows from under it too.
            uint ac = AwningColours[h.Awning % AwningColours.Length];
            double ax0 = xc - 0.5 * bw + 0.1, ax1 = xc + 0.5 * bw - 0.1, ya = y1 + 0.32, yb = y1 - 0.05, za = 0.02, zb = 0.95;
            int stripes = lod == 0 ? Math.Max(4, (int)((ax1 - ax0) / 0.4)) : 1;
            ShapeBrush under = Looks.Ao(BoudhaKit.B(MeshColor.FromHex(ac), MaterialChannel.Fabric), 0.75f);
            SacredDraw.Quad(m, e, ax0, ya - 0.01, za, ax1, ya - 0.01, za, ax1, yb - 0.01, zb, ax0, yb - 0.01, zb, 0, -0.93, -0.36, under);
            for (int q = 0; q < stripes; q++)
            {
                double sa = ax0 + (ax1 - ax0) * q / stripes, sb = ax0 + (ax1 - ax0) * (q + 1) / stripes;
                ShapeBrush sbr = BoudhaKit.B(lod > 0 || q % 2 == 0 ? MeshColor.FromHex(ac) : MeshColor.FromHex(0xF4F2EC), MaterialChannel.Fabric);
                SacredDraw.Quad(m, e, sa, ya, za, sb, ya, za, sb, yb, zb, sa, yb, zb, 0, 0.93, 0.36, sbr);
                if (lod == 0) SacredDraw.Quad(m, e, sa, yb, zb, sb, yb, zb, sb, yb - 0.18, zb + 0.01, sa, yb - 0.18, zb + 0.01, 0, 0, 1, sbr);
            }
        }

        /// <summary>An upper-storey window of the house's family with its sill at <paramref name="y"/>.</summary>
        private static void Window(MeshData m, in Affine3 e, in RingHouse h, double xc, double y, int lod, int hs)
        {
            double w = 1.1, hh = 1.4, x0 = xc - 0.5 * w, x1 = xc + 0.5 * w, y0 = y, y1 = y + hh;
            ShapeBrush glass = BoudhaKit.B(MeshColor.FromHex(0x2C333B), MaterialChannel.Glass, 0.85f);
            switch (h.Look)
            {
                case HouseLook.Brick:
                {
                    // The Newar-revival fronts by the gate and on the kora (gate/Commons 2019-2022) show dark carved
                    // lattice in the openings, not glass: a deep brown carved panel inside the frame.
                    ShapeBrush wood = BoudhaKit.B(MeshColor.FromHex(0x3A2418), MaterialChannel.WoodCarved);
                    SacredDraw.Panel(m, e, x0, y0, x1, y1, 0.01, BoudhaKit.B(MeshColor.FromHex(0x241812), MaterialChannel.WoodCarved, 0.85f));
                    if (lod == 0)
                    {
                        // Carved frame whose lintel and sill run out past it (tiki jhya style), and two muntins.
                        SacredDraw.Box(m, e, x0 - 0.12, x1 + 0.12, y0 - 0.1, y0, 0, 0.12, wood, BoxFaces.Front | BoxFaces.Top);
                        SacredDraw.Box(m, e, x0 - 0.35, x1 + 0.35, y1, y1 + 0.14, 0, 0.12, wood, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                        SacredDraw.Panel(m, e, x0 - 0.1, y0, x0, y1, 0.08, wood);
                        SacredDraw.Panel(m, e, x1, y0, x1 + 0.1, y1, 0.08, wood);
                        SacredDraw.Panel(m, e, xc - 0.03, y0, xc + 0.03, y1, 0.04, wood);
                        SacredDraw.Panel(m, e, x0, y0 + 0.62 * hh, x1, y0 + 0.62 * hh + 0.06, 0.04, wood);
                    }
                    break;
                }
                case HouseLook.Ochre:
                {
                    ShapeBrush white = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Plaster), green = BoudhaKit.B(MeshColor.FromHex(0x2F7A4A), MaterialChannel.Wood);
                    SacredDraw.Panel(m, e, x0, y0, x1, y1, 0.01, green);
                    if (lod == 0)
                    {
                        SacredDraw.Box(m, e, x0 - 0.12, x1 + 0.12, y0 - 0.08, y0, 0, 0.1, white, BoxFaces.Front | BoxFaces.Top);
                        SacredDraw.Box(m, e, x0 - 0.1, x0, y0, y1, 0, 0.06, white, BoxFaces.Front);
                        SacredDraw.Box(m, e, x1, x1 + 0.1, y0, y1, 0, 0.06, white, BoxFaces.Front);
                        ShapeBrush slat = BoudhaKit.B(MeshColor.FromHex(0x23603A), MaterialChannel.Wood);
                        for (int q = 1; q < 7; q += 2) SacredDraw.Panel(m, e, x0, y0 + q * 0.2 - 0.03, x1, y0 + q * 0.2, 0.03, slat);
                        Pediment(m, e, xc, y1 + 0.02, w + 0.4, 0.38, white, (hs & 1) == 0);
                    }
                    break;
                }
                default:
                {
                    ShapeBrush white = BoudhaKit.B(MeshColor.FromHex(0xF6F3EC), MaterialChannel.Plaster);
                    SacredDraw.Panel(m, e, x0, y0, x1, y1, 0.01, glass);
                    if (lod == 0)
                    {
                        // Frame, sill, a muntin cross and the Rana pediment (triangular or segmental) or a flat hood.
                        SacredDraw.Box(m, e, x0 - 0.14, x1 + 0.14, y0 - 0.1, y0, 0, 0.12, white, BoxFaces.Front | BoxFaces.Top);
                        SacredDraw.Panel(m, e, x0 - 0.1, y0, x0, y1, 0.05, white);
                        SacredDraw.Panel(m, e, x1, y0, x1 + 0.1, y1, 0.05, white);
                        SacredDraw.Panel(m, e, xc - 0.025, y0, xc + 0.025, y1, 0.03, white);
                        SacredDraw.Panel(m, e, x0, y0 + 0.65 * hh, x1, y0 + 0.65 * hh + 0.05, 0.03, white);
                        if ((hs & 3) != 3) Pediment(m, e, xc, y1 + 0.02, w + 0.36, 0.34, white, (hs & 4) == 0);
                        else SacredDraw.Box(m, e, x0 - 0.2, x1 + 0.2, y1, y1 + 0.16, 0, 0.18, white, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                    }
                    break;
                }
            }
        }

        /// <summary>A white Rana-style pediment over a window (centre <paramref name="xc"/>, foot at <paramref name="y"/>
        /// on the wall of frame <paramref name="e"/>): triangular, or segmental (a low arc on short shoulders).</summary>
        internal static void Pediment(MeshData m, in Affine3 e, double xc, double y, double w, double hgt, in ShapeBrush b, bool triangular)
        {
            double[] x = BoudhaKit.U2, z = BoudhaKit.V2;
            int n = 0;
            // The outline in (across, −up): the frame below maps its local z to the wall's −y.
            x[n] = xc - 0.5 * w;
            z[n++] = 0;
            x[n] = xc + 0.5 * w;
            z[n++] = 0;
            if (triangular)
            {
                x[n] = xc;
                z[n++] = -hgt;
            }
            else
            {
                for (int i = 0; i <= 3; i++)
                {
                    double a = Math.PI * i / 3.0;
                    x[n] = xc + 0.5 * w * Math.Cos(a);
                    z[n++] = -(0.45 * hgt + 0.55 * hgt * Math.Sin(a));
                }
            }
            // Local x along the wall, local y out of the wall (extrusion), local z = x × y = down the wall.
            Affine3 pf = SacredDraw.Basis(e, 0, y, 0.0, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, pf, b, x, z, n, 0.12, 0, 0, BevelStyle.Chamfer, true, false, 0, 30);
        }

        /// <summary>A balcony along the front at floor height <paramref name="y"/>: a slab and its railing.</summary>
        private static void Balcony(MeshData m, in Affine3 e, in RingHouse h, double half, double y, int lod)
        {
            double x0 = -half + 0.3, x1 = half - 0.3, d = 0.9;
            ShapeBrush slab = BoudhaKit.B(MeshColor.FromHex(0xE9E4DA), MaterialChannel.Concrete);
            bool wooden = h.Look == HouseLook.Brick;
            ShapeBrush rail = BoudhaKit.B(wooden ? MeshColor.FromHex(0x3A2418) : MeshColor.FromHex(0x2B2B2E), wooden ? MaterialChannel.Wood : MaterialChannel.Metal);
            SacredDraw.Box(m, e, x0, x1, y - 0.14, y, 0, d, slab, lod == 0 ? BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right
                                                                       : BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
            if (lod >= 1)
            {
                SacredDraw.Quad(m, e, x0, y, d, x1, y, d, x1, y + 1.0, d, x0, y + 1.0, d, 0, 0, 1, rail);
                return;
            }
            Shapes.Bar(m, e, rail, x0, y + 1.0, d - 0.04, x1, y + 1.0, d - 0.04, 0.03, 4);
            Shapes.Bar(m, e, rail, x0, y + 0.5, d - 0.04, x1, y + 0.5, d - 0.04, 0.018, 4);
            int posts = Math.Max(2, (int)((x1 - x0) / 0.6));
            for (int q = 0; q <= posts; q++)
            {
                double x = x0 + (x1 - x0) * q / posts;
                SacredDraw.Box(m, e, x - 0.025, x + 0.025, y, y + 1.0, d - 0.06, d - 0.02, rail, BoxFaces.Front);
            }
        }

        // ------------------------------------------------------------------------------------------------ roofs

        /// <summary>A red clay-tile hip roof over the footprint's oriented rectangle (centre, long axis, half sizes) with
        /// tile courses, a ridge and white dormers on the slope that faces the stupa.</summary>
        private static void HipRoof(MeshData m, in Affine3 k, double cu, double cv, double au, double av, double ha, double hb, double y0, int lod)
        {
            ShapeBrush tile = BoudhaKit.B(MeshColor.FromHex(0xA04A34), MaterialChannel.RoofTile), ridge = BoudhaKit.B(MeshColor.FromHex(0x6E3226), MaterialChannel.RoofTile);
            ShapeBrush soffit = BoudhaKit.B(MeshColor.FromHex(0x5A3422), MaterialChannel.Wood, 0.7f);
            // Frame: x along the long axis, y up, z = x × y (site (−av, au)).
            Affine3 rf = SacredDraw.Basis(k, cu, y0, cv, au, 0, av, 0, 1, 0);
            double a = ha + Eave, b = hb + Eave, rise = RoofRise, rh = Math.Max(0, a - b);
            for (int s = -1; s <= 1; s += 2)
            {
                double l = Math.Sqrt(rise * rise + b * b);
                SacredDraw.Quad(m, rf, -a, 0, s * b, a, 0, s * b, rh, rise, 0, -rh, rise, 0, 0, b / l, s * rise / l, tile);
                double lx = Math.Sqrt(rise * rise + (a - rh) * (a - rh));
                SacredDraw.Tri(m, rf, s * a, 0, -b, s * a, 0, b, s * rh, rise, 0, s * rise / lx, (a - rh) / lx, 0, tile);
            }
            SacredDraw.Quad(m, rf, -a, -0.02, -b, a, -0.02, -b, a, -0.02, b, -a, -0.02, b, 0, -1, 0, soffit);
            if (lod <= 1 && rh > 0.1) Shapes.Bar(m, rf, ridge, -rh, rise + 0.02, 0, rh, rise + 0.02, 0, 0.09, 5);
            if (lod > 0) return;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int q = 1; q < 5; q++)
                {
                    double t = q / 5.0, xh = a + (rh - a) * t, y = rise * t + 0.02, z = s * b * (1 - t);
                    Shapes.Bar(m, rf, ridge, -xh, y, z, xh, y, z, 0.025, 3);
                }
            }
            // Dormers on the slope facing the stupa axis.
            double side = (av * cu - au * cv) >= 0 ? 1 : -1;
            int count = rh > 2.5 ? 2 : 1;
            for (int d = 0; d < count; d++)
            {
                double dx = count == 1 ? 0 : (d == 0 ? -0.5 : 0.5) * rh;
                Dormer(m, rf, dx, side, rise, b);
            }
        }

        /// <summary>A small white dormer with its window and a little gable roof on slope <paramref name="side"/> of the
        /// hip roof frame <paramref name="rf"/>.</summary>
        private static void Dormer(MeshData m, in Affine3 rf, double x, double side, double rise, double b)
        {
            double z = side * 0.45 * b, y = rise * 0.55;
            ShapeBrush white = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Plaster), tile = BoudhaKit.B(MeshColor.FromHex(0xA04A34), MaterialChannel.RoofTile);
            // A frame facing down the slope (out of the roof).
            Affine3 df = SacredDraw.Turn(rf, x, 0, z, side > 0 ? 0 : Math.PI);
            SacredDraw.Box(m, df, -0.5, 0.5, y - 0.35, y + 0.65, -0.9, 0.0, white, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Panel(m, df, -0.28, y - 0.15, 0.28, y + 0.45, 0.01, BoudhaKit.B(MeshColor.FromHex(0x2C333B), MaterialChannel.Glass, 0.85f));
            SacredDraw.Tri(m, df, -0.5, y + 0.65, 0.0, 0.5, y + 0.65, 0.0, 0, y + 1.0, 0.0, 0, 0, 1, white);
            SacredDraw.Quad(m, df, -0.62, y + 0.6, 0.12, 0, y + 1.05, 0.12, 0, y + 1.05, -1.0, -0.62, y + 0.6, -1.0, -0.6, 0.8, 0, tile);
            SacredDraw.Quad(m, df, 0.62, y + 0.6, 0.12, 0, y + 1.05, 0.12, 0, y + 1.05, -1.0, 0.62, y + 0.6, -1.0, 0.6, 0.8, 0, tile);
        }

        /// <summary>The minimum-area rectangle over a polygon's edge directions: centre, the long axis (unit) and the half
        /// sizes along it and across it.</summary>
        internal static bool OrientedRect(double[] pu, double[] pv, int n, out double cu, out double cv, out double au, out double av, out double ha, out double hb)
        {
            cu = cv = au = av = ha = hb = 0;
            double best = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double du = pu[j] - pu[i], dv = pv[j] - pv[i], l = Math.Sqrt(du * du + dv * dv);
                if (l < 0.5) continue;
                du /= l;
                dv /= l;
                double a0 = double.MaxValue, a1 = double.MinValue, b0 = double.MaxValue, b1 = double.MinValue;
                for (int q = 0; q < n; q++)
                {
                    double a = pu[q] * du + pv[q] * dv, bb = -pu[q] * dv + pv[q] * du;
                    a0 = Math.Min(a0, a);
                    a1 = Math.Max(a1, a);
                    b0 = Math.Min(b0, bb);
                    b1 = Math.Max(b1, bb);
                }
                double area = (a1 - a0) * (b1 - b0);
                if (area >= best) continue;
                best = area;
                double ma = 0.5 * (a0 + a1), mb = 0.5 * (b0 + b1);
                cu = ma * du - mb * dv;
                cv = ma * dv + mb * du;
                if (a1 - a0 >= b1 - b0)
                {
                    au = du;
                    av = dv;
                    ha = 0.5 * (a1 - a0);
                    hb = 0.5 * (b1 - b0);
                }
                else
                {
                    au = -dv;
                    av = du;
                    ha = 0.5 * (b1 - b0);
                    hb = 0.5 * (a1 - a0);
                }
            }
            return best < double.MaxValue && ha > 0.5 && hb > 0.5;
        }

        /// <summary>The roof of a flat-roofed house: café umbrellas over tables, a water tank and a solar heater, a stair
        /// room, a banner pole.</summary>
        private static void RoofExtras(MeshData m, in Affine3 k, in RingHouse h, double[] pu, double[] pv, int n, double top, int lod, int seed)
        {
            double cu = 0, cv = 0;
            for (int i = 0; i < n; i++)
            {
                cu += pu[i];
                cv += pv[i];
            }
            cu /= n;
            cv /= n;
            double r = Math.Max(1e-6, Math.Sqrt(cu * cu + cv * cv));
            // Toward the stupa (front), back, and sideways.
            double fu = -cu / r, fv = -cv / r, su = fv, sv = -fu;
            double rad = Math.Max(0.8, BoudhaKit.EdgeDistance(pu, pv, n, cu, cv)), ru, rv;
            int seg = lod == 0 ? 10 : 6;
            if ((h.Flags & HouseFlags.Cafe) != 0)
            {
                int count = rad > 3.2 ? 3 : 2;
                for (int q = 0; q < count; q++)
                {
                    double t = (q / (count - 1.0) - 0.5) * 2;
                    ru = cu + fu * rad * 0.45 + su * t * rad * 0.55;
                    rv = cv + fv * rad * 0.45 + sv * t * rad * 0.55;
                    uint col = UmbrellaColours[(seed + q) % UmbrellaColours.Length];
                    Shapes.Cylinder(m, SacredDraw.At(k, ru, top, rv), BoudhaKit.B(MeshColor.FromHex(0xD8D2C4), MaterialChannel.Metal), 0.03, CafeH - 0.45, 4, 0, 0, false, false);
                    Shapes.Cone(m, SacredDraw.At(k, ru, top + CafeH - 0.55, rv), BoudhaKit.B(MeshColor.FromHex(col), MaterialChannel.Fabric), 1.1, 0.55, seg, 0, 0, true);
                    if (lod == 0)
                        Shapes.Cylinder(m, SacredDraw.At(k, ru, top, rv), BoudhaKit.B(MeshColor.FromHex(0x6B4A2E), MaterialChannel.Wood), 0.4, 0.75, 8, 0, 0, false, true);
                }
            }
            if ((h.Flags & HouseFlags.Tanks) != 0)
            {
                ru = cu - fu * rad * 0.45 + su * rad * 0.3;
                rv = cv - fv * rad * 0.45 + sv * rad * 0.3;
                ShapeBrush black = BoudhaKit.B(MeshColor.FromHex(0x1E2024), MaterialChannel.Plain), steel = BoudhaKit.B(MeshColor.FromHex(0x8C9096), MaterialChannel.Metal);
                SacredDraw.Box(m, SacredDraw.At(k, ru, top, rv), -0.6, 0.6, 0, 0.85, -0.6, 0.6, steel, BoxFaces.Top | BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                Shapes.Cylinder(m, SacredDraw.At(k, ru, top + 0.85, rv), black, 0.55, TankH - 0.85 - 0.08, seg, 0.06, 1, false, true);
                if (lod == 0)
                {
                    double pu0 = cu - fu * rad * 0.4 - su * rad * 0.35, pv0 = cv - fv * rad * 0.4 - sv * rad * 0.35;
                    Affine3 sf = BoudhaKit.Facing(k, pu0, top, pv0, fu, fv);
                    SacredDraw.Quad(m, sf, -0.9, 0.2, -0.6, 0.9, 0.2, -0.6, 0.9, 1.1, 0.4, -0.9, 1.1, 0.4, 0, 0.74, -0.67, BoudhaKit.B(MeshColor.FromHex(0x1E2A4A), MaterialChannel.Glass));
                    Shapes.Bar(m, sf, BoudhaKit.B(MeshColor.FromHex(0xEDEDED), MaterialChannel.Metal), -0.95, 1.25, 0.5, 0.95, 1.25, 0.5, 0.18, 8);
                }
            }
            if ((h.Flags & HouseFlags.Mumty) != 0)
            {
                ru = cu - fu * rad * 0.4 - su * rad * 0.25;
                rv = cv - fv * rad * 0.4 - sv * rad * 0.25;
                double hw = Math.Min(1.2, 0.6 * rad);
                ShapeBrush w = BoudhaKit.B(MeshColor.FromHex(h.Wall), h.Look == HouseLook.Brick ? MaterialChannel.Brick : MaterialChannel.Plaster);
                Affine3 mf = BoudhaKit.Facing(k, ru, top, rv, fu, fv);
                SacredDraw.Box(m, mf, -hw, hw, 0, MumtyH, -hw, hw, w, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, mf, -hw - 0.15, hw + 0.15, MumtyH, MumtyH + 0.12, -hw - 0.15, hw + 0.15, BoudhaKit.B(MeshColor.FromHex(0xE9E4DA), MaterialChannel.Concrete), BoxFaces.All);
                SacredDraw.Panel(m, mf, -0.4, 0, 0.4, 2.0, hw + 0.01, BoudhaKit.B(MeshColor.FromHex(0x5A3A22), MaterialChannel.Wood));
            }
            if ((h.Flags & HouseFlags.FlagPole) != 0)
            {
                ru = cu + su * rad * 0.5;
                rv = cv + sv * rad * 0.5;
                Shapes.Cylinder(m, SacredDraw.At(k, ru, top, rv), BoudhaKit.B(MeshColor.FromHex(0x6B4A2E), MaterialChannel.Wood), 0.05, PoleH, 5, 0, 0, false, true);
                Shapes.Cone(m, SacredDraw.At(k, ru, top + PoleH, rv), BoudhaKit.Gilt, 0.08, 0.2, 5, 0, 0, true);
                Affine3 pf = BoudhaKit.Facing(k, ru, 0, rv, su, sv);
                for (int i = 0; i < 5; i++)
                {
                    ShapeBrush col = BoudhaKit.B(SacredPalette.Flags[i], MaterialChannel.Fabric);
                    double ya = top + PoleH - 0.3 - i * 0.55, yb = ya - 0.55;
                    for (int s = -1; s <= 1; s += 2) SacredDraw.Quad(m, pf, 0.05, yb, 0, 0.6, yb + 0.04, 0, 0.6, ya + 0.04, 0, 0.05, ya, 0, 0, 0, s, col);
                }
            }
        }
    }
}
