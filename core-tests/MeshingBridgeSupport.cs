using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;

namespace Ghumante.Core.Tests
{
    /// <summary>Synthetic bridge scenes (tiles with terrain, roads and structure records) and an OBJ dump with
    /// terrain, water and road context for the visual self-check (tools/mesh-preview).</summary>
    internal static class BridgeScenes
    {
        public const double Lift = 0.26;

        /// <summary>A tile whose heights follow f(local x, local z).</summary>
        public static TileData Tile(int tx, int ty, Func<double, double, double> f)
        {
            var id = new TileId(10, tx, ty);
            return MeshingChecks.SyntheticTile(id, (x, z) => f(x - id.X0, z - id.Z0));
        }

        public static RoadRecord Road(ulong id, RoadClass c, Surface s, double widthM, RoadFlags flags, sbyte layer, params double[] localMetres)
        {
            var pts = new int[localMetres.Length];
            for (int k = 0; k < pts.Length; k++) pts[k] = (int)Math.Round(localMetres[k] * 100.0);
            return new RoadRecord
            {
                OsmWayId = id, RoadClass = c, Surface = s, Flags = flags, Layer = layer, WidthCm = (ulong)Math.Round(widthM * 100.0), Points = pts,
                Access = Travel.Foot | Travel.Bicycle | Travel.Motorbike | Travel.Car | Travel.Jeep | Travel.Bus,
            };
        }

        /// <summary>Fill the structure records: the given ones by road index, Absent for the rest.</summary>
        public static void Structures(TileData t, params (int road, RoadStructureRecord rec)[] recs)
        {
            t.RoadStructures.Clear();
            for (int i = 0; i < t.Roads.Count; i++) t.RoadStructures.Add(RoadStructureRecord.Absent);
            foreach (var (road, rec) in recs) t.RoadStructures[road] = rec;
        }

        public static RoadStructureRecord Elevated(RoadStructureKind kind, RoadStructureFlags flags, sbyte layer, float[] deckY)
        {
            return new RoadStructureRecord
            {
                Kind = kind, Flags = flags | RoadStructureFlags.DeckFromTags, Layer = layer, RailingHeightM = 1.1f, DeckY = deckY,
            };
        }

        /// <summary>A river bridge: a 120 m secondary road bridge over a 6 m deep channel, with approaches and
        /// a structure record whose deck crests gently over the river.</summary>
        public static TileData RiverBridge()
        {
            TileData t = Tile(517, 159, (x, z) => 1285 - 6 * Math.Exp(-Math.Pow((x - 500) / 26.0, 2)));
            t.Roads.Add(Road(101, RoadClass.Secondary, Surface.Asphalt, 0, RoadFlags.None, 0, 360, 500, 440, 500));
            t.Roads.Add(Road(102, RoadClass.Secondary, Surface.Asphalt, 0, RoadFlags.Bridge, 1, 440, 500, 470, 500, 500, 500, 530, 500, 560, 500));
            t.Roads.Add(Road(103, RoadClass.Secondary, Surface.Asphalt, 0, RoadFlags.None, 0, 560, 500, 640, 500));
            float e = (float)(1285 - 6 * Math.Exp(-Math.Pow(60 / 26.0, 2)) + Lift);
            Structures(t, (1, Elevated(RoadStructureKind.Bridge, RoadStructureFlags.CarAccessible | RoadStructureFlags.WaterCrossing, 1,
                                       new[] { e, e + 0.5f, e + 0.8f, e + 0.5f, e })));
            return t;
        }

        /// <summary>A long multi-span bridge: 320 m of primary road over a wide braided river bed.</summary>
        public static TileData LongBridge()
        {
            Func<double, double, double> f = (x, z) =>
            {
                double d = Math.Abs(x - 500) / 150.0;
                return 1280 - 7 * (d >= 1 ? 0 : 1 - d * d * (3 - 2 * d)) + 0.6 * Math.Sin(z / 37.0);
            };
            TileData t = Tile(520, 160, f);
            t.Roads.Add(Road(201, RoadClass.Primary, Surface.Asphalt, 9, RoadFlags.None, 0, 260, 520, 340, 520));
            var pts = new List<double>();
            for (int i = 0; i <= 8; i++)
            {
                pts.Add(340 + 40 * i);
                pts.Add(520 + 6 * Math.Sin(i * 0.45));
            }
            t.Roads.Add(Road(202, RoadClass.Primary, Surface.Asphalt, 9, RoadFlags.Bridge, 1, pts.ToArray()));
            t.Roads.Add(Road(203, RoadClass.Primary, Surface.Asphalt, 9, RoadFlags.None, 0, 660, pts[pts.Count - 1], 740, pts[pts.Count - 1]));
            var y = new float[9];
            for (int i = 0; i <= 8; i++)
            {
                double f0 = (float)(f(340, 520) + Lift), f1 = f(660, 520) + Lift, s = i / 8.0;
                y[i] = (float)(f0 + (f1 - f0) * s + 1.6 * Math.Sin(Math.PI * s));
            }
            Structures(t, (1, Elevated(RoadStructureKind.Bridge, RoadStructureFlags.CarAccessible | RoadStructureFlags.WaterCrossing, 1, y)));
            return t;
        }

        /// <summary>A flyover: a trunk road ramps up at 5% over a secondary road with 7.3 m from road to deck.</summary>
        public static TileData Flyover(out int lowerRoad, out float lowY)
        {
            TileData t = Tile(518, 160, (x, z) => 1300.0);
            var xs = new double[] { 150, 200, 250, 300, 350, 420, 470, 530, 580, 650, 700, 750, 800, 850 };
            var pts = new double[xs.Length * 2];
            var y = new float[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                pts[2 * i] = xs[i];
                pts[2 * i + 1] = 500;
                y[i] = (float)(1300 + Lift + Math.Min(7.3, 0.05 * Math.Max(0, Math.Min(xs[i] - 150, 850 - xs[i]))));
            }
            t.Roads.Add(Road(301, RoadClass.Trunk, Surface.Asphalt, 10, RoadFlags.Bridge, 1, pts));
            t.Roads.Add(Road(302, RoadClass.Secondary, Surface.Asphalt, 10, RoadFlags.None, 0, 500, 120, 500, 880));
            t.Roads.Add(Road(303, RoadClass.Trunk, Surface.Asphalt, 10, RoadFlags.None, 0, 60, 500, 150, 500));
            t.Roads.Add(Road(304, RoadClass.Trunk, Surface.Asphalt, 10, RoadFlags.None, 0, 850, 500, 940, 500));
            Structures(t, (0, Elevated(RoadStructureKind.Flyover, RoadStructureFlags.CarAccessible, 1, y)));
            lowerRoad = 1;
            lowY = (float)(1300 + Lift);
            return t;
        }

        /// <summary>A pedestrian overbridge over a dual trunk road, deck 6.5 m above the road, stairs at both ends.</summary>
        public static TileData FootOverbridge()
        {
            TileData t = Tile(519, 160, (x, z) => 1300.0);
            t.Roads.Add(Road(406, RoadClass.Footway, Surface.Concrete, 2.5, RoadFlags.Bridge, 1, 500, 478, 500, 489, 500, 500, 500, 511, 500, 522));
            t.Roads.Add(Road(402, RoadClass.Trunk, Surface.Asphalt, 14, RoadFlags.None, 0, 300, 500, 700, 500));
            float d = (float)(1300 + Lift + 6.5);
            Structures(t, (0, Elevated(RoadStructureKind.Flyover, RoadStructureFlags.FootOverbridge, 1, new[] { d, d, d, d, d })));
            return t;
        }

        /// <summary>An underpass: a trunk road dipping 7 m below the ground for 120 m (Kalanki style) under a cross
        /// street, with a lowered deck profile.</summary>
        public static TileData Underpass()
        {
            TileData t = Tile(514, 160, (x, z) => 1310.0);
            var xs = new double[] { 200, 260, 320, 380, 440, 500, 560, 620, 680, 740, 800 };
            var pts = new double[xs.Length * 2];
            var y = new float[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                pts[2 * i] = xs[i];
                pts[2 * i + 1] = 500;
                y[i] = (float)(1310 + Lift - Math.Min(7.0, 0.06 * Math.Max(0, Math.Min(xs[i] - 200, 800 - xs[i]))));
            }
            t.Roads.Add(Road(601, RoadClass.Trunk, Surface.Asphalt, 10, RoadFlags.Tunnel, -1, pts));
            t.Roads[0].Flags = RoadFlags.None;
            t.Roads.Add(Road(602, RoadClass.Primary, Surface.Asphalt, 9, RoadFlags.None, 0, 470, 300, 470, 700));
            Structures(t, (0, new RoadStructureRecord
            {
                Kind = RoadStructureKind.Underpass, Flags = RoadStructureFlags.CarAccessible, Layer = -1, ClearanceM = 5.5f, DeckY = y,
            }));
            return t;
        }

        /// <summary>A bridge cut by the border between two tiles (west and east), with consistent deck heights from a
        /// world-x function when <paramref name="records"/>, else flags only (derived decks).</summary>
        public static void Seam(bool records, out TileData west, out TileData east)
        {
            Func<double, double, double> world = (x, z) => 1290 - 5 * Math.Exp(-Math.Pow((x - 600 * 1024.0 - 1010) / 30.0, 2));
            west = Tile(600, 300, (x, z) => world(x + 600 * 1024.0, z + 300 * 1024.0));
            east = Tile(601, 300, (x, z) => world(x + 601 * 1024.0, z + 300 * 1024.0));
            // Original vertices at local-west x = 960, 1000 (inside west) and 1060, 1100 (inside east); cut at 1024.
            Func<double, float> deck = wx => (float)(world(600 * 1024.0 + 960, 0) + Lift + 0.9 * Math.Sin(Math.PI * (wx - 960) / 140.0));
            west.Roads.Add(Road(501, RoadClass.Tertiary, Surface.Asphalt, 7, RoadFlags.Bridge | RoadFlags.HasNextCtx, 1, 960, 512, 1000, 515, 1024, 516.2, 1060, 518));
            east.Roads.Add(Road(501, RoadClass.Tertiary, Surface.Asphalt, 7, RoadFlags.Bridge | RoadFlags.HasPrevCtx, 1, 1000 - 1024, 515, 0, 516.2, 60 - 24, 518,
                                1100 - 1024, 520));
            west.Roads.Add(Road(502, RoadClass.Tertiary, Surface.Asphalt, 7, RoadFlags.None, 0, 900, 512, 960, 512));
            east.Roads.Add(Road(503, RoadClass.Tertiary, Surface.Asphalt, 7, RoadFlags.None, 0, 76, 520, 140, 520));
            if (!records) return;
            Structures(west, (0, Elevated(RoadStructureKind.Bridge, RoadStructureFlags.CarAccessible | RoadStructureFlags.WaterCrossing, 1,
                                          new[] { deck(960), deck(1000), deck(1024), deck(1060) })));
            Structures(east, (0, Elevated(RoadStructureKind.Bridge, RoadStructureFlags.CarAccessible | RoadStructureFlags.WaterCrossing, 1,
                                          new[] { deck(1000), deck(1024), deck(1060), deck(1100) })));
        }

        // -------------------------------------------------------------------------------------------------------
        // OBJ dump with context
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Write <paramref name="m"/> (tile-local, absolute Y) as an OBJ for tools/mesh-preview, mapped like
        /// core-tests/Support/ObjDump (x, y, −z; the mirror reverses the winding, so faces are written reversed), with vertex colours
        /// darkened by the baked AO. With a tile, adds terrain (cells below <paramref name="waterY"/> drawn as water)
        /// and road strips around the bounds (margin m) as context. Coordinates are recentred on the bounds.
        /// </summary>
        public static void WriteObj(string path, MeshData m, TileData t, double margin, double waterY, BridgeLayout layout)
        {
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            double cx = 0.5 * (minX + maxX), cz = 0.5 * (minZ + maxZ), cy = minY;
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append("# Ghumante bridge preview\n# meshpreview: views=front,34,top\n");
            int vbase = 1;
            Action<string> group = name => sb.Append("o ").Append(name).Append('\n');
            Action<double, double, double, double, double, double, double, double, double> vert = (x, y, z, r, g, b, nx, ny, nz) =>
            {
                sb.AppendFormat(inv, "v {0:F3} {1:F3} {2:F3} {3:F3} {4:F3} {5:F3}\n", x - cx, y - cy, -(z - cz), r, g, b);
                sb.AppendFormat(inv, "vn {0:F4} {1:F4} {2:F4}\n", nx, ny, -nz);
            };
            if (t != null)
            {
                var h = new TileHeightSampler(t);
                group("terrain");
                double step = 2.0;
                int nx = (int)Math.Ceiling((maxX - minX + 2 * margin) / step), nz = (int)Math.Ceiling((maxZ - minZ + 2 * margin) / step);
                double x0 = minX - margin, z0 = minZ - margin;
                for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    double ax = x0 + i * step, az = z0 + j * step;
                    double[] qx = { ax, ax + step, ax + step, ax }, qz = { az, az, az + step, az + step };
                    float[] qy = new float[4];
                    bool water = false;
                    for (int k = 0; k < 4; k++)
                    {
                        float hh;
                        if (!h.TryHeightClamped(t.Tile.X0 + qx[k], t.Tile.Z0 + qz[k], out hh)) hh = (float)cy;
                        if (hh < waterY) water = true;
                        qy[k] = hh;
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        double y = water ? Math.Max(qy[k], waterY) : qy[k];
                        double r = water ? 0.32 : 0.47, g = water ? 0.55 : 0.62, b = water ? 0.72 : 0.33;
                        if (!water && (i + j) % 2 == 0)
                        {
                            r *= 0.96;
                            g *= 0.96;
                        }
                        vert(qx[k], y, qz[k], r, g, b, 0, 1, 0);
                    }
                    sb.AppendFormat(inv, "f {0}//{0} {1}//{1} {2}//{2}\nf {0}//{0} {2}//{2} {3}//{3}\n", vbase, vbase + 1, vbase + 2, vbase + 3);
                    vbase += 4;
                }
                // Road strips: draped roads at ground + lift, elevated ones at their deck.
                group("roads");
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    RoadStructureRecord s = layout != null ? layout.Structures[ri] : t.RoadStructureOf(ri);
                    double half = 0.5 * Math.Max(RoadStyle.WidthM(r) * (BridgeStyle.IsFoot(r.RoadClass) ? 1.0 : 1.25), 2.0);
                    int[] p = r.Points;
                    int n = p.Length / 2;
                    int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? n - 2 : n - 1;
                    for (int k = first; k < last; k++)
                    {
                        double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                        double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
                        if (l < 1e-6) continue;
                        int sub = Math.Max(1, (int)Math.Ceiling(l / 2.0));
                        for (int q = 0; q < sub; q++)
                        {
                            double f0 = (double)q / sub, f1 = (double)(q + 1) / sub;
                            double ux = -dz / l * half, uz = dx / l * half;
                            double px0 = ax + dx * f0, pz0 = az + dz * f0, px1 = ax + dx * f1, pz1 = az + dz * f1;
                            double y0, y1;
                            if (s.DeckY != null && s.DeckY.Length == n)
                            {
                                y0 = s.DeckY[k] + (s.DeckY[k + 1] - s.DeckY[k]) * f0 + 0.005;
                                y1 = s.DeckY[k] + (s.DeckY[k + 1] - s.DeckY[k]) * f1 + 0.005;
                            }
                            else
                            {
                                float g0, g1;
                                h.TryHeightClamped(t.Tile.X0 + px0, t.Tile.Z0 + pz0, out g0);
                                h.TryHeightClamped(t.Tile.X0 + px1, t.Tile.Z0 + pz1, out g1);
                                y0 = g0 + Lift;
                                y1 = g1 + Lift;
                            }
                            vert(px0 - ux, y0, pz0 - uz, 0.33, 0.34, 0.37, 0, 1, 0);
                            vert(px1 - ux, y1, pz1 - uz, 0.33, 0.34, 0.37, 0, 1, 0);
                            vert(px1 + ux, y1, pz1 + uz, 0.33, 0.34, 0.37, 0, 1, 0);
                            vert(px0 + ux, y0, pz0 + uz, 0.33, 0.34, 0.37, 0, 1, 0);
                            sb.AppendFormat(inv, "f {0}//{0} {1}//{1} {2}//{2}\nf {0}//{0} {2}//{2} {3}//{3}\n", vbase, vbase + 3, vbase + 2, vbase + 1);
                            sb.AppendFormat(inv, "f {0}//{0} {1}//{1} {2}//{2}\nf {0}//{0} {2}//{2} {3}//{3}\n", vbase, vbase + 1, vbase + 2, vbase + 3);
                            vbase += 4;
                        }
                    }
                }
            }
            group("bridge");
            for (int v = 0; v < m.VertexCount; v++)
            {
                double ao = m.HasUv0 ? m.Uv0[2 * v + 1] : 1.0;
                double shade = 0.5 + 0.5 * ao;
                vert(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], m.Colors[4 * v] / 255.0 * shade, m.Colors[4 * v + 1] / 255.0 * shade,
                     m.Colors[4 * v + 2] / 255.0 * shade, m.Normals[3 * v], m.Normals[3 * v + 1], m.Normals[3 * v + 2]);
            }
            for (int i = 0; i < m.IndexCount; i += 3)
                sb.AppendFormat(inv, "f {0}//{0} {1}//{1} {2}//{2}\n", vbase + m.Indices[i], vbase + m.Indices[i + 2], vbase + m.Indices[i + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, sb.ToString());
        }
    }
}
