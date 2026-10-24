using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// One sector of the Boudhanath kora (a kora <see cref="BoudhaPart"/>, <see cref="BoudhaSite.Sectors"/>), built as a
    /// hero of its own: on its inner ring the stretch of the compound wall (<see cref="BoudhaWall"/>), the north entrance
    /// (<see cref="BoudhaNorth"/>) or the south pavilion, and here the paved ground and the compound's furniture; on its
    /// outer ring the houses (<see cref="BoudhaRing"/>) and the gompas (<see cref="BoudhaGompas"/>) round the kora
    /// (boudha.md 2.3, 3.2, 4):
    /// <list type="bullet">
    /// <item>the kora plaza: the grey stone-flag inner path 3.6 m wide along the wall, a darker stone kerb line, then red
    /// brick out to the house fronts and into the lanes, a raised slab (its top at <see cref="PaveTop"/>, over the
    /// kora's pedestrian ways) with walkable colliders;</item>
    /// <item>the compound ground between the wall and the lowest terrace, flagged in grey stone;</item>
    /// <item>the lamp houses with their ochre-yellow sloping roofs over tables of brass butter lamps, the potted shrub
    /// beds of the compound corners, the white juniper kilns, the benches on the brick zone facing the stupa.</item>
    /// </list>
    /// </summary>
    internal static class BoudhaQuarter
    {
        /// <summary>Top of the kora paving above the kora level: 2 cm over the ribbon of the pedestrian ways the road
        /// mesher draws there (0.28 m).</summary>
        public const double PaveTop = 0.30;

        /// <summary>Top of the compound's flagged ground above the kora level.</summary>
        public const double CompoundTop = 0.06;

        /// <summary>Width of the stone-flag inner path along the wall (m).</summary>
        public const double InnerPathM = 3.6;

        /// <summary>Farthest the plaza paving reaches from the stupa axis where no house front stops it (m).</summary>
        public const double MaxPlazaR = 79.0;

        /// <summary>How far the paving runs into a lane mouth past the neighbouring house fronts (m).</summary>
        public const double LaneMouthM = 4.0;

        // Paving scratch (one sector's rays at 1.5° at most, and the median window), one set per building thread.
        [ThreadStatic] private static double[] _front, _clamped, _window;

        /// <summary>One kora sector at one LOD; returns the prayer-wheel niches its stretch of the wall draws.</summary>
        public static int Build(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            BoudhaSector sec = BoudhaSite.SectorOf(part);
            int niches = 0;
            if (sec.HasInner)
            {
                // The wall (its own bake), then the paving baked from its top, then the compound.
                niches = BoudhaWall.Build(m, lod >= 3 ? null : c, f, part, lod);
                int v0 = m.VertexCount, i0 = m.IndexCount;
                Paving(m, lod >= 3 ? null : c, tile, f, part, lod);
                SacredDraw.Bake(m, v0, i0, f.Level + PaveTop);
                v0 = m.VertexCount;
                i0 = m.IndexCount;
                if (part == BoudhaPart.NorthGate) BoudhaNorth.Build(m, lod >= 3 ? null : c, f, lod);
                if (lod <= 2) Compound(m, c, f, part, lod);
                SacredDraw.Bake(m, v0, i0, f.Level);
            }
            if (sec.HasOuter)
            {
                int v0 = m.VertexCount, i0 = m.IndexCount;
                BoudhaRing.Build(m, lod >= 3 ? null : c, tile, f, part, lod);
                BoudhaGompas.Build(m, lod >= 3 ? null : c, tile, f, part, lod);
                SacredDraw.Bake(m, v0, i0, f.Level);
            }
            return lod >= 3 ? 0 : niches;
        }

        // ------------------------------------------------------------------------------------------------ paving

        /// <summary>The sector's inner bearing range (degrees from the site's north axis, start &lt; end, may run past
        /// 360); false when it has no inner ring.</summary>
        private static bool Range(BoudhaPart part, out double a0, out double a1)
        {
            BoudhaSector sec = BoudhaSite.SectorOf(part);
            a0 = sec.In0;
            a1 = sec.In1;
            if (!sec.HasInner) return false;
            if (a1 <= a0) a1 += 360.0;
            return true;
        }

        /// <summary>Distance from the stupa axis along bearing <paramref name="ang"/> (radians) to the first building
        /// footprint of the tile beyond <paramref name="from"/> (the house fronts), capped at <paramref name="cap"/>.</summary>
        private static double FrontAt(TileData tile, in BoudhaFrame f, double ang, double from, double cap)
        {
            double du = Math.Sin(ang), dv = Math.Cos(ang), best = cap;
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0, cs = Math.Cos(a), sn = Math.Sin(a);
            ulong t1 = 2 * BoudhaSite.T1Way;
            for (int i = 0; i < tile.Buildings.Count; i++)
            {
                BuildingRecord b = tile.Buildings[i];
                if (b.OsmRef == t1 || (b.Flags & BuildingFlags.Part) != 0) continue;
                int[] r = b.Rings[0];
                int n = r.Length / 2;
                double fx = r[0] / 100.0 - f.AX, fz = r[1] / 100.0 - f.AZ;
                if (fx * fx + fz * fz > (cap + 60) * (cap + 60)) continue;
                for (int p = 0, q = n - 1; p < n; q = p++)
                {
                    double x0 = r[2 * q] / 100.0 - f.AX, z0 = r[2 * q + 1] / 100.0 - f.AZ, x1 = r[2 * p] / 100.0 - f.AX, z1 = r[2 * p + 1] / 100.0 - f.AZ;
                    double u0 = x0 * cs - z0 * sn, v0 = x0 * sn + z0 * cs, u1 = x1 * cs - z1 * sn, v1 = x1 * sn + z1 * cs;
                    double ex = u1 - u0, ey = v1 - v0, den = du * ey - dv * ex;
                    if (Math.Abs(den) < 1e-12) continue;
                    double t = (u0 * ey - v0 * ex) / den, s = (u0 * dv - v0 * du) / den;
                    if (t > from && t < best && s >= 0 && s <= 1) best = t;
                }
            }
            return best;
        }

        /// <summary>Distance from the stupa axis along bearing <paramref name="ang"/> to the lowest terrace's outline.</summary>
        private static double TerraceAt(double ang)
        {
            double[] pu = BoudhaKit.U2, pv = BoudhaKit.V2;
            int n = BoudhaStupa.Terrace(0, pu, pv);
            double du = Math.Sin(ang), dv = Math.Cos(ang), best = 0;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ex = pu[i] - pu[j], ey = pv[i] - pv[j], den = du * ey - dv * ex;
                if (Math.Abs(den) < 1e-12) continue;
                double t = (pu[j] * ey - pv[j] * ex) / den, s = (pu[j] * dv - pv[j] * du) / den;
                if (t > best && s >= 0 && s <= 1) best = t;
            }
            return best;
        }

        /// <summary>The kora paving of the quarter and the compound's flagged ground, ray by ray from the stupa axis.</summary>
        private static void Paving(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            if (c != null && lod > 0)
            {
                // The walkable slabs follow LOD0's rays whatever LOD draws the paving: the streamer takes a hero's colliders
                // from the first LOD it builds (TileBuild.BuildHeroes: LOD1 on Low, which never draws LOD0).
                Paving(new MeshData(), c, tile, f, part, 0);
                c = null;
            }
            Affine3 k = f.Site;
            SacredDrawnRoads roads = SacredDrawnRoads.For(tile);
            double a0, a1;
            if (!Range(part, out a0, out a1)) return;
            double stepDeg = lod == 0 ? 1.5 : lod == 1 ? 3.0 : lod == 2 ? 7.5 : 15.0;
            int steps = Math.Max(1, (int)Math.Round((a1 - a0) / stepDeg));
            double y = PaveTop;
            ShapeBrush flag = BoudhaKit.B(BoudhaKit.StoneFlag, MaterialChannel.Flagstone), kerb = BoudhaKit.B(BoudhaKit.StoneKerb, MaterialChannel.Stone);
            ShapeBrush ground = BoudhaKit.B(MeshColor.FromHex(0x9A968E), MaterialChannel.Flagstone);
            // The house fronts ray by ray; a ray that runs up a lane mouth (far past its neighbours) stops a few metres
            // in, so the paving's edge does not spike down the lanes.
            if (_front == null)
            {
                _front = new double[64];
                _clamped = new double[64];
                _window = new double[9];
            }
            double[] front = _front;
            for (int s = 0; s <= steps; s++)
            {
                double ang = (a0 + (a1 - a0) * s / steps) * Math.PI / 180.0;
                front[s] = FrontAt(tile, f, ang, BoudhaWall.RadiusAt(f, ang) + 2.0, MaxPlazaR);
            }
            double[] win = _window;
            for (int s = 0; s <= steps; s++)
            {
                int n = 0;
                for (int q = Math.Max(0, s - 4); q <= Math.Min(steps, s + 4); q++) win[n++] = front[q];
                Array.Sort(win, 0, n);
                _clamped[s] = Math.Min(front[s], win[n / 2] + LaneMouthM);
            }
            double pw = 0, po = 0, pt = 0, pc = 0, pcin = 0, pu0 = 0, pv0 = 0;
            for (int s = 0; s <= steps; s++)
            {
                double deg = a0 + (a1 - a0) * s / steps, ang = deg * Math.PI / 180.0, du = Math.Sin(ang), dv = Math.Cos(ang);
                double wall = BoudhaWall.RadiusAt(f, ang), inner = wall + BoudhaWall.HalfThick + 0.04, kerbR = inner + InnerPathM;
                double outer = Math.Max(kerbR + 0.6, _clamped[s] + 0.3);
                double terrace = TerraceAt(ang), cin = wall - BoudhaWall.HalfThick - 0.04;
                if (s > 0)
                {
                    // Inner path (flags), kerb line, brick out to the fronts: quads between the previous ray and this one.
                    Strip(m, k, pu0, pv0, du, dv, pw, po, inner, kerbR, y, flag, s, 0);
                    Strip(m, k, pu0, pv0, du, dv, po, po + 0.25, kerbR, kerbR + 0.25, y + 0.005, kerb, s, 1);
                    Strip(m, k, pu0, pv0, du, dv, po + 0.25, pt, kerbR + 0.25, outer, y, BoudhaKit.B(BoudhaKit.PlazaBrick, MaterialChannel.Brick), s, 2);
                    // The slab's outer edge face down to the ground.
                    Skirt(m, k, pu0, pv0, du, dv, pt, outer, y, BoudhaKit.B(BoudhaKit.StoneKerb, MaterialChannel.Stone, 0.8f));
                    // The compound's flagged ground from the terrace foot to the wall's inner face.
                    if (cin > terrace + 0.3 && pcin > pc + 0.3) Strip(m, k, pu0, pv0, du, dv, pc, pcin, terrace, cin, CompoundTop, ground, s, 3);
                    if (c != null && s % 2 == 0)
                    {
                        // Walkable slab colliders, one band per two rays, broken where the road mesher draws the kora
                        // ways (their ribbon, 2 cm under the paving, is the ground there; no platform stands on a drawn
                        // road, decision 1).
                        double midDeg = a0 + (a1 - a0) * (s - 1) / steps, ma = midDeg * Math.PI / 180.0;
                        double r0 = inner, r1 = Math.Min(outer, pt);
                        double tang = 0.5 * (r0 + r1) * Math.Tan(stepDeg * Math.PI / 180.0) + 0.3;
                        double bearing = (BoudhaSite.AxisBearingDeg + midDeg) * Math.PI / 180.0;
                        double start = double.NaN;
                        for (double r = r0; ; r += 0.25)
                        {
                            bool end = r >= r1;
                            bool free = !end && OffRoad(roads, f, ma, r, tang);
                            if (free && double.IsNaN(start)) start = r;
                            if ((!free || end) && !double.IsNaN(start))
                            {
                                double e = Math.Min(r, r1), rm = 0.5 * (start + e), half = 0.5 * (e - start);
                                if (half > 0.1)
                                {
                                    double x, z;
                                    f.ToTile(rm * Math.Sin(ma), rm * Math.Cos(ma), out x, out z);
                                    // Box U axis across the ray (tile x, z): the ray's bearing turned 90°.
                                    c.AddBox(x, z, f.Level - 0.2, f.Level + y, tang, half, Math.Cos(bearing), -Math.Sin(bearing), GenColliderFlags.Walkable, GenColliders.Stone);
                                }
                                start = double.NaN;
                            }
                            if (end) break;
                        }
                    }
                }
                pu0 = du;
                pv0 = dv;
                pw = inner;
                po = kerbR;
                pt = outer;
                pc = terrace;
                pcin = cin;
            }
        }

        /// <summary>True when a slab sample at radius r on the ray at site angle <paramref name="ang"/> (and
        /// <paramref name="tang"/> either side across it) keeps clear of the drawn roads.</summary>
        private static bool OffRoad(SacredDrawnRoads roads, in BoudhaFrame f, double ang, double r, double tang)
        {
            double su = Math.Sin(ang), sv = Math.Cos(ang), tu = sv, tv = -su;
            for (int q = -1; q <= 1; q++)
            {
                double u = r * su + q * tang * tu, v = r * sv + q * tang * tv, x, z;
                f.ToTile(u, v, out x, out z);
                if (roads.SignedDistance(x, z) < 0.3) return false;
            }
            return true;
        }

        /// <summary>True when a solid of radius <paramref name="radius"/> at site (u, v) keeps clear of the drawn roads, so
        /// it may carry a collider (objects standing on the kora walk the road mesher draws are left walk-through).</summary>
        internal static bool Clear(TileData tile, in BoudhaFrame f, double u, double v, double radius)
        {
            if (tile == null) return true;
            double x, z;
            f.ToTile(u, v, out x, out z);
            return SacredDrawnRoads.For(tile).SignedDistance(x, z) >= radius + 0.1;
        }

        /// <summary>True when the rectangle of half sizes (hw across, hd deep) at site (u, v) facing <paramref name="a"/>
        /// keeps clear of the drawn roads (its corners and edge midpoints).</summary>
        private static bool PlanClear(in BoudhaFrame f, double u, double v, double a, double hw, double hd)
        {
            if (f.Tile == null) return true;
            double fu = Math.Sin(a), fv = Math.Cos(a), xu = fv, xv = -fu;
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue;
                if (!Clear(f.Tile, f, u + xu * hw * i + fu * hd * j, v + xv * hw * i + fv * hd * j, 0.0)) return false;
            }
            return true;
        }

        /// <summary>A flat quad between two rays (directions 0 and 1) from radius a to b on each.</summary>
        private static void Strip(MeshData m, in Affine3 k, double du0, double dv0, double du1, double dv1, double a0, double b0, double a1, double b1, double y,
                                  in ShapeBrush b, int s, int band)
        {
            if (b0 <= a0 + 0.01 || b1 <= a1 + 0.01) return;
            // A faint per-quad tone change (the brick channel's texture draws the courses; stronger steps read as rays).
            float tone = 0.975f + 0.025f * (((s * 7 + band * 3) % 5) / 4f);
            ShapeBrush bb = new ShapeBrush(MeshColor.Scale(b.Color, tone), b.Channel, b.Ao);
            SacredDraw.Quad(m, k, du0 * a0, y, dv0 * a0, du0 * b0, y, dv0 * b0, du1 * b1, y, dv1 * b1, du1 * a1, y, dv1 * a1, 0, 1, 0, bb);
        }

        /// <summary>The vertical outer face of the paving slab between two rays.</summary>
        private static void Skirt(MeshData m, in Affine3 k, double du0, double dv0, double du1, double dv1, double r0, double r1, double y, in ShapeBrush b)
        {
            double nu = du0 + du1, nv = dv0 + dv1, l = Math.Sqrt(nu * nu + nv * nv);
            SacredDraw.Quad(m, k, du0 * r0, y, dv0 * r0, du1 * r1, y, dv1 * r1, du1 * r1, -0.2, dv1 * r1, du0 * r0, -0.2, dv0 * r0, nu / l, 0, nv / l, b);
        }

        // ------------------------------------------------------------------------------------------------ compound

        /// <summary>True when the site-local point stands on the sector's inner ring.</summary>
        private static bool Mine(BoudhaPart part, double u, double v)
        {
            return BoudhaSite.InnerAt(u, v) == part;
        }

        /// <summary>The compound's shrub beds: site (u, v) of the middle, the length along the wall and the depth across it
        /// (c_aerial/c04).</summary>
        private static readonly double[] Beds =
        {
            -24.5, 33.0, 8.5, 3.0, -34.0, 24.5, 8.0, 3.0, // NW corner
            22.5, 35.0, 7.0, 3.0, 35.5, 19.0, 7.0, 2.8, // NE corner (the lamp houses between)
            23.0, -36.0, 8.0, 3.0, 34.5, -27.0, 7.5, 3.0, // SE corner
            -23.0, -36.0, 8.0, 3.0, -34.5, -27.0, 7.5, 3.0, // SW corner
            -42.5, 9.0, 8.0, 1.8, 42.5, 4.0, 8.0, 1.8, // the strips along the west and east walls
        };

        /// <summary>Lamp houses, kilns, the shrub beds and the benches of a sector (each by where it stands).</summary>
        private static void Compound(MeshData m, GenColliders c, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            Affine3 k = f.Site;
            // NE: two lamp houses along the wall (around/03 left).
            if (Mine(part, 33.5, 25.0)) LampHouse(m, c, f, 33.5, 25.0, -40, 4.2, 3.0, lod);
            if (Mine(part, 28.0, 31.5)) LampHouse(m, c, f, 28.0, 31.5, -40, 3.6, 2.6, lod);
            // The planted beds of the compound corners (c_aerial/c04: dense green beds of potted shrubs in the NW, NE, SE
            // and SW corners between the lowest terrace and the wall, and narrow strips along the east and west walls).
            for (int i = 0; i < Beds.Length; i += 4)
                if (Mine(part, Beds[i], Beds[i + 1])) ShrubBed(m, f, Beds[i], Beds[i + 1], Beds[i + 2], Beds[i + 3], lod, i);
            if (Mine(part, 44.0, -14.0))
            {
                BoudhaWall.Kiln(m, BoudhaKit.Facing(k, 44.0, CompoundTop, -14.0, 1, 0), 1.3, 2.0, lod);
                if (c != null) Cylinder(c, f, 44.0, -14.0, 2.0, 0.7);
            }
            // By the south stair to T1 (corner/04): two lamp houses.
            if (Mine(part, 8.5, -43.5)) LampHouse(m, c, f, 8.5, -43.5, 180, 4.4, 3.0, lod);
            if (Mine(part, 13.5, -42.5)) LampHouse(m, c, f, 13.5, -42.5, 180, 3.4, 2.4, lod);
            if (Mine(part, -44.5, 3.0))
            {
                BoudhaWall.Kiln(m, BoudhaKit.Facing(k, -44.5, CompoundTop, 3.0, -1, 0), 1.3, 2.0, lod);
                if (c != null) Cylinder(c, f, -44.5, 3.0, 2.0, 0.7);
            }
            // Benches on the brick zone facing the stupa (OSM n5162080318-20, r 55).
            for (int i = 0; i < 3; i++)
            {
                double ang = (262 + 8 * i) * Math.PI / 180.0, r = BoudhaWall.RadiusAt(f, ang) + BoudhaWall.HalfThick + InnerPathM + 2.0;
                double u = r * Math.Sin(ang), v = r * Math.Cos(ang);
                if (Mine(part, u, v)) Bench(m, c, f, u, v, ang, lod);
            }
            // The NE compound house east of the north gate.
            if (Mine(part, BoudhaNorth.NeHouseU, BoudhaNorth.NeHouseV)) BoudhaNorth.NeHouse(m, c, f, lod);
        }

        private static void Cylinder(GenColliders c, in BoudhaFrame f, double u, double v, double h, double r)
        {
            double x, z;
            f.ToTile(u, v, out x, out z);
            c.AddCylinder(x, z, f.Level - 0.2, f.Level + h, r);
        }

        /// <summary>
        /// A lamp house (sang-khang for butter lamps; around/03, corner/04): an open timber shed with an ochre-yellow
        /// sloping roof (pent roof, high side toward the wall) over stepped tables of brass butter lamps, at site (u, v),
        /// its open front facing bearing <paramref name="faceDeg"/> (site degrees), <paramref name="w"/> × <paramref name="d"/>.
        /// </summary>
        internal static void LampHouse(MeshData m, GenColliders c, in BoudhaFrame f, double u, double v, double faceDeg, double w, double d, int lod)
        {
            Affine3 k = f.Site;
            double a = faceDeg * Math.PI / 180.0;
            // Moved toward the stupa, 10 cm at a time, until its plan clears the drawn kora (the wall it stands against
            // is fitted back off the kora in the north-east; BoudhaWall.Fit).
            for (int it = 0; it < 40 && !PlanClear(f, u, v, a, 0.5 * w + 0.4, 0.5 * d + 0.4); it++)
            {
                double r = Math.Sqrt(u * u + v * v);
                u *= (r - 0.1) / r;
                v *= (r - 0.1) / r;
            }
            Affine3 lf = BoudhaKit.Facing(k, u, CompoundTop, v, Math.Sin(a), Math.Cos(a));
            ShapeBrush roof = BoudhaKit.B(MeshColor.FromHex(0xD9B13B), MaterialChannel.Metal), post = BoudhaKit.B(MeshColor.FromHex(0x6B4A2E), MaterialChannel.Wood);
            ShapeBrush back = BoudhaKit.B(BoudhaKit.LimeWhite, MaterialChannel.Plaster), floor = BoudhaKit.B(MeshColor.FromHex(0x8E8A82), MaterialChannel.Stone);
            double hw = 0.5 * w, hd = 0.5 * d, hFront = 2.3, hBack = 3.0;
            SacredDraw.Box(m, lf, -hw, hw, 0, 0.18, -hd, hd, floor, BoxFaces.All & ~BoxFaces.Bottom);
            // Back and side walls (low), posts at the front.
            SacredDraw.Box(m, lf, -hw, hw, 0.18, hBack - 0.1, -hd, -hd + 0.2, back, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            for (int s = -1; s <= 1 && lod <= 1; s += 2)
            {
                SacredDraw.Box(m, lf, s * hw - 0.1, s * hw + 0.1, 0.18, 1.1, -hd, hd, back, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                Shapes.Cylinder(m, SacredDraw.At(lf, s * (hw - 0.12), 0.18, hd - 0.12), post, 0.08, hFront - 0.18, lod == 0 ? 8 : 5, 0, 0, false, false);
            }
            // The pent roof (overhanging 0.4 m), its fascia and the yellow ridge flashing.
            double e = 0.4;
            SacredDraw.Quad(m, lf, -hw - e, hFront - 0.1, hd + e, hw + e, hFront - 0.1, hd + e, hw + e, hBack, -hd - 0.1, -hw - e, hBack, -hd - 0.1, 0, 0.94, 0.33, roof);
            SacredDraw.Quad(m, lf, -hw - e, hFront - 0.12, hd + e, hw + e, hFront - 0.12, hd + e, hw + e, hBack - 0.02, -hd - 0.1, -hw - e, hBack - 0.02, -hd - 0.1, 0, -0.94, -0.33,
                            Looks.Ao(roof, 0.7f));
            if (lod <= 1)
            {
                SacredDraw.Box(m, lf, -hw - e, hw + e, hFront - 0.3, hFront - 0.1, hd + e - 0.06, hd + e, BoudhaKit.B(MeshColor.FromHex(0xC8282E), MaterialChannel.Paint), BoxFaces.Front | BoxFaces.Bottom);
                // Stepped lamp tables with rows of brass butter lamps.
                ShapeBrush table = BoudhaKit.B(MeshColor.FromHex(0x2A2622), MaterialChannel.Metal), brass = BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal);
                for (int t = 0; t < 3; t++)
                {
                    double z0 = -hd + 0.35 + t * 0.45, y = 0.18 + 0.55 + t * 0.22;
                    SacredDraw.Box(m, lf, -hw + 0.3, hw - 0.3, 0.18, y, z0, z0 + 0.42, table, BoxFaces.All & ~BoxFaces.Bottom);
                    if (lod == 0)
                    {
                        int lamps = (int)((w - 0.7) / 0.42);
                        for (int q = 0; q < lamps; q++)
                        {
                            double x = -hw + 0.45 + q * 0.42;
                            Shapes.Frustum(m, SacredDraw.At(lf, x, y, z0 + 0.21), brass, 0.04, 0.07, 0.08, 4, 0, 0, false, true);
                        }
                    }
                }
            }
            if (c != null)
            {
                double x0, y0, z0, x1, y1, z1;
                lf.Point(-hw, 0, -hd, out x0, out y0, out z0);
                lf.Point(hw, 0, -hd, out x1, out y1, out z1);
                c.AddWall(x0, z0, x1, z1, f.Level, f.Level + hBack, 0.15);
            }
        }

        /// <summary>
        /// A planted bed of the compound (c_aerial/c04, around/03): a low red-brick kerb round dark soil packed with clay
        /// pots of dark green shrubs 0.6-0.9 m tall (#2F5A2A), in two rows, its long side <paramref name="w"/> along the
        /// wall (turned to face out from the stupa) and <paramref name="d"/> across it.
        /// </summary>
        private static void ShrubBed(MeshData m, in BoudhaFrame f, double u, double v, double w, double d, int lod, int seed)
        {
            Affine3 k = f.Site;
            double r = Math.Max(1e-6, Math.Sqrt(u * u + v * v));
            Affine3 g = BoudhaKit.Facing(k, u, CompoundTop, v, u / r, v / r);
            ShapeBrush brick = BoudhaKit.B(MeshColor.FromHex(0x9E4A32), MaterialChannel.Brick), soil = BoudhaKit.B(MeshColor.FromHex(0x4A3A2C), MaterialChannel.Dirt);
            ShapeBrush shrub = BoudhaKit.B(MeshColor.FromHex(0x2F5A2A), MaterialChannel.Foliage), shrubLit = BoudhaKit.B(MeshColor.FromHex(0x3E6E34), MaterialChannel.Foliage);
            SacredDraw.Box(m, g, -0.5 * w, 0.5 * w, 0, 0.3, -0.5 * d, 0.5 * d, brick, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Panel(m, SacredDraw.Basis(g, 0, 0.31, 0, 1, 0, 0, 0, 0, -1), -0.5 * w + 0.12, -0.5 * d + 0.12, 0.5 * w - 0.12, 0.5 * d - 0.12, 0, soil);
            if (lod >= 2)
            {
                // Far: the mass of foliage as one low green slab over the soil.
                SacredDraw.Box(m, g, -0.5 * w + 0.15, 0.5 * w - 0.15, 0.31, 0.95, -0.5 * d + 0.15, 0.5 * d - 0.15, shrub, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            ShapeBrush clay = BoudhaKit.B(BoudhaKit.Terracotta, MaterialChannel.Dirt);
            double pitch = lod == 0 ? 0.85 : 1.7;
            int cols = Math.Max(1, (int)((w - 0.3) / pitch)), rows = d >= 2.0 ? 2 : 1;
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    int h = (seed * 31 + i * 7 + j * 13) % 5;
                    double x = -0.5 * w + 0.15 + (i + 0.5) * (w - 0.3) / cols + (j == 1 ? 0.25 * pitch : 0), z = rows == 1 ? 0 : (j == 0 ? -0.25 : 0.25) * d;
                    double tall = 0.3 + 0.1 * h, rad = 0.3 + 0.03 * h;
                    Affine3 p = SacredDraw.At(g, x, 0.3, z);
                    if (lod == 0) Shapes.Frustum(m, p, clay, 0.16, 0.21, 0.28, 4, 0, 0, false, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(p, 0, 0.28 + 0.5 * tall, 0), h % 2 == 0 ? shrub : shrubLit, rad, 0.5 * tall + 0.08, rad, lod == 0 ? 5 : 4, false);
                }
        }

        /// <summary>A kora bench (wooden slats on iron legs, a backrest) facing the stupa at site (u, v).</summary>
        private static void Bench(MeshData m, GenColliders c, in BoudhaFrame f, double u, double v, double ang, int lod)
        {
            Affine3 k = f.Site;
            Affine3 bf = BoudhaKit.Facing(k, u, PaveTop, v, -Math.Sin(ang), -Math.Cos(ang));
            ShapeBrush wood = BoudhaKit.B(MeshColor.FromHex(0x7A5234), MaterialChannel.Wood), iron = BoudhaKit.B(MeshColor.FromHex(0x2B2B2E), MaterialChannel.Metal);
            for (int s = -1; s <= 1; s += 2)
            {
                SacredDraw.Box(m, bf, s * 0.75 - 0.03, s * 0.75 + 0.03, 0, 0.45, -0.25, 0.25, iron, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, bf, s * 0.75 - 0.03, s * 0.75 + 0.03, 0.45, 0.9, -0.25, -0.2, iron, BoxFaces.All & ~BoxFaces.Bottom);
            }
            int slats = lod == 0 ? 3 : 1;
            for (int q = 0; q < slats; q++)
            {
                double z0 = -0.24 + q * 0.5 / slats;
                SacredDraw.Box(m, bf, -0.9, 0.9, 0.42, 0.47, z0, z0 + 0.5 / slats - 0.03, wood, BoxFaces.All & ~BoxFaces.Bottom);
            }
            SacredDraw.Box(m, bf, -0.9, 0.9, 0.6, 0.85, -0.25, -0.2, wood, BoxFaces.All);
            // Solid only off the walk the road mesher draws (the benches stand on the kora street).
            if (c != null && Clear(f.Tile, f, u, v, 0.95))
            {
                double x0, y0, z0, x1, y1, z1;
                bf.Point(-0.9, 0, 0, out x0, out y0, out z0);
                bf.Point(0.9, 0, 0, out x1, out y1, out z1);
                c.AddWall(x0, z0, x1, z1, f.Level + PaveTop, f.Level + PaveTop + 0.5, 0.25);
            }
        }
    }
}
