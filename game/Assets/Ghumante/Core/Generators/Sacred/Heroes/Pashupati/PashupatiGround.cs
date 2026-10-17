using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// The Pashupatinath ground (pashupati-temple.md 2.1): the terrain model is a surface model that reads the roofs round
    /// the courtyard (it climbs 3-5 m over the west buildings), where the real courtyard is one paved level and the west
    /// forecourt and gate passage stand three steps above it. <see cref="Apply"/> flattens a decoded tile's height grid
    /// to those levels inside their zones (site rectangles), cuts the ground standing above them down to the level in a
    /// band round them (<see cref="CutM"/>, for the streamer's decimated terrain) and clamps the terrain beyond to a cone
    /// of slopes back to the original, so the terrain mesh, the sampler, the ground query and every placer read the same
    /// floor the site is built on. The east edge stops at the riverside blocks (a = +28), whose walls drop to the ghat
    /// walk (the river front package's ground) and is steep there; the west forecourt eases into the Deopatan lanes at
    /// 1:4. Idempotent, any tile level; the streamer calls it on every decoded tile before meshing
    /// (<see cref="HeroBuilder.PrepareGround"/>). Also draws the courtyard and forecourt paving.
    /// </summary>
    public static class PashupatiGround
    {
        /// <summary>One level zone: site rectangle, height above the courtyard, the blend slope (rise over run) of the
        /// terrain round it on each side (−a, +a, −b, +b) and the width of the band on each side where the terrain is cut
        /// down to the level (never filled) before the slope starts.</summary>
        private struct Zone
        {
            public double A0, A1, B0, B1, Rise, SlopeA0, SlopeA1, SlopeB0, SlopeB1, CutA0, CutA1, CutB0, CutB1;
        }

        /// <summary>
        /// Width of the cut bands (m). The streamer draws a level-10 tile's terrain decimated (every second height
        /// sample on the Mid and High tiers, every fourth on Low: <see cref="Ghumante.Core.Streaming.StreamingConfig.TerrainStep"/>),
        /// so a 16 m terrain cell that reaches over the paving from the rising ground round the courtyard would push
        /// through it. The terrain is cut to the level for one 16 m cell's diagonal round the paved zones (cuts only
        /// lower the ground: east of the courtyard, toward the river, it already falls away).
        /// </summary>
        public const double CutM = 23.0;

        /// <summary>The level zones: the courtyard with the riverside blocks and the south court, and the forecourt.</summary>
        private static readonly Zone[] Zones =
        {
            new Zone
            {
                A0 = -36.7, A1 = 28.0, B0 = -24.0, B1 = 31.0, Rise = 0, SlopeA0 = 0.5, SlopeA1 = 1.2, SlopeB0 = 0.5, SlopeB1 = 0.6, CutA0 = CutM,
                CutA1 = CutM, CutB0 = CutM, CutB1 = CutM,
            },
            new Zone
            {
                A0 = -30.0, A1 = 22.0, B0 = -36.0, B1 = -24.0, Rise = 0, SlopeA0 = 0.5, SlopeA1 = 1.2, SlopeB0 = 0.5, SlopeB1 = 0.5, CutA0 = CutM,
                CutA1 = 0, CutB0 = CutM, CutB1 = 0,
            },
            new Zone
            {
                A0 = -78.0, A1 = -36.7, B0 = -20.0, B1 = 22.0, Rise = PashupatiSite.ForecourtRiseM, SlopeA0 = 0.25, SlopeA1 = 0.5, SlopeB0 = 0.3, SlopeB1 = 0.4,
                CutA0 = CutM, CutA1 = 0, CutB0 = CutM, CutB1 = CutM,
            },
        };

        /// <summary>Farthest a zone's blend reaches past its cut band (m).</summary>
        public const double BlendReachM = 24.0;

        /// <summary>The level (metres above the courtyard) a site point is flattened to, NaN outside every zone.</summary>
        public static double LevelAt(double a, double b)
        {
            for (int i = 0; i < Zones.Length; i++)
            {
                Zone z = Zones[i];
                if (a >= z.A0 && a <= z.A1 && b >= z.B0 && b <= z.B1) return z.Rise;
            }
            return double.NaN;
        }

        /// <summary>Flatten the site in a decoded tile's height grid (see the class summary); returns the samples changed.
        /// A tile the site does not reach is left alone.</summary>
        public static int Apply(TileData t)
        {
            return Flatten(t, true);
        }

        /// <summary>
        /// The tile as the streamer prepares it, without touching <paramref name="t"/>: the tile itself when it is
        /// already prepared (or the site does not reach it, or it has no heights), else a copy sharing every record but
        /// with its own flattened height grid. The site's models read their ground from this so a build on a raw tile
        /// (tests, tools) sits on the same floor as in the streamed world and never alters a tile someone else holds.
        /// </summary>
        public static TileData Prepared(TileData t)
        {
            if (Flatten(t, false) == 0) return t;
            var copy = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = (ushort[])t.HeightsQ.Clone() };
            Flatten(copy, true);
            return copy;
        }

        /// <summary>The samples <see cref="Apply"/> changes; written only when <paramref name="write"/>.</summary>
        private static int Flatten(TileData t, bool write)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2 || t.HeightsQ.Length < t.HeightsN * t.HeightsN) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            PashupatiFrame f = PashupatiFrame.Plain(t, 0, null);
            // The site's tile-local bounding box with the blend reach.
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < Zones.Length; i++)
            {
                for (int q = 0; q < 4; q++)
                {
                    double a = (q & 1) == 0 ? Zones[i].A0 - BlendReachM - CutM : Zones[i].A1 + BlendReachM + CutM;
                    double b = (q & 2) == 0 ? Zones[i].B0 - BlendReachM - CutM : Zones[i].B1 + BlendReachM + CutM;
                    double x, z;
                    f.ToTile(a, b, out x, out z);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }
            }
            if (maxX < 0 || maxZ < 0 || minX > size || minZ > size) return 0;
            int i0 = Math.Max(0, (int)Math.Floor(minX / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling(maxX / cell));
            int j0 = Math.Max(0, (int)Math.Floor(minZ / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling(maxZ / cell));
            int changed = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double a, b;
                    f.FromTile(i * cell, j * cell, out a, out b);
                    int k = j * n + i;
                    ushort q = t.HeightsQ[k];
                    double h = Ght.Dequantize(q);
                    // Inside a zone: its level exactly. Outside: clamp to each zone's cone, the nearest zone last.
                    double inside = LevelAt(a, b);
                    if (!double.IsNaN(inside))
                    {
                        h = f.Level + inside;
                    }
                    else
                    {
                        int nearest = -1;
                        double best = double.MaxValue;
                        for (int z = 0; z < Zones.Length; z++)
                        {
                            double d = Distance(Zones[z], a, b);
                            if (d < best)
                            {
                                best = d;
                                nearest = z;
                            }
                        }
                        if (best > BlendReachM + CutM) continue;
                        for (int z = 0; z < Zones.Length; z++)
                        {
                            if (z == nearest) continue;
                            h = Clamp(Zones[z], f.Level, a, b, h);
                        }
                        h = Clamp(Zones[nearest], f.Level, a, b, h);
                    }
                    ushort nq = Ght.Quantize(h);
                    if (nq == q) continue;
                    if (write) t.HeightsQ[k] = nq;
                    changed++;
                }
            }
            return changed;
        }

        private static double Distance(in Zone z, double a, double b)
        {
            double da = a < z.A0 ? z.A0 - a : a > z.A1 ? a - z.A1 : 0, db = b < z.B0 ? z.B0 - b : b > z.B1 ? b - z.B1 : 0;
            return Math.Sqrt(da * da + db * db);
        }

        /// <summary>Clamp height h to within the zone's cone of slopes: each side's slope times the distance past it
        /// (above the level, past its cut band).</summary>
        private static double Clamp(in Zone z, double level, double a, double b, double h)
        {
            double da = a < z.A0 ? z.A0 - a : a > z.A1 ? a - z.A1 : 0, db = b < z.B0 ? z.B0 - b : b > z.B1 ? b - z.B1 : 0;
            double ca = a < z.A0 ? z.CutA0 : a > z.A1 ? z.CutA1 : 0, cb = b < z.B0 ? z.CutB0 : b > z.B1 ? z.CutB1 : 0;
            if (da > BlendReachM + ca || db > BlendReachM + cb) return h;
            double sa = a < z.A0 ? z.SlopeA0 : z.SlopeA1, sb = b < z.B0 ? z.SlopeB0 : z.SlopeB1;
            double below = Math.Sqrt(Sq(da * sa) + Sq(db * sb)), above = Math.Sqrt(Sq(Math.Max(0, da - ca) * sa) + Sq(Math.Max(0, db - cb) * sb));
            double l = level + z.Rise;
            return Math.Max(l - below, Math.Min(l + above, h));
        }

        private static double Sq(double x)
        {
            return x * x;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Paving
        // ---------------------------------------------------------------------------------------------------------

        private static readonly ShapeBrush Flags = new ShapeBrush(MeshColor.FromHex(0x9F998E), MaterialChannel.Flagstone);
        private static readonly ShapeBrush FlagsLight = new ShapeBrush(MeshColor.FromHex(0xC9C1B2), MaterialChannel.Flagstone);
        private static readonly ShapeBrush Vermilion = new ShapeBrush(MeshColor.FromHex(0xB8432C), MaterialChannel.Dirt);
        private static readonly ShapeBrush Petals = new ShapeBrush(MeshColor.FromHex(0xE89A1C), MaterialChannel.Fabric);
        private static readonly ShapeBrush Runner = new ShapeBrush(MeshColor.FromHex(0xA8402F), MaterialChannel.Fabric);
        private static readonly ShapeBrush RunnerEdge = new ShapeBrush(MeshColor.FromHex(0xE07A2A), MaterialChannel.Fabric);

        /// <summary>The courtyard's grey stone flags (pashupati-temple.md 2.1, PT-17), worn and stained with vermilion and
        /// marigold petals at the doors and round the Nandi; a floor 2 cm over the flattened terrain.</summary>
        internal static void CourtyardFloor(ref PashupatiFrame f, MeshData m)
        {
            if (f.Lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double cellM = f.Lod == 0 ? 6.0 : f.Lod == 1 ? 12.0 : 30.0;
            // Main court, the strip under the riverside blocks south of the east stair and the north-east corner by it.
            Paving(ref f, m, -33.4, 24.2, -24.0, 30.0, 0.02, cellM, Flags, 11u);
            Paving(ref f, m, 24.2, 27.8, -24.0, 4.6, 0.02, cellM, Flags, 12u);
            Paving(ref f, m, 24.2, 27.8, 10.2, 30.0, 0.02, cellM, Flags, 13u);
            Paving(ref f, m, -28.0, 20.0, -34.0, -24.0, 0.02, cellM, Flags, 14u);
            if (f.Lod == 0)
            {
                // Vermilion powder and marigold petals at the four doors and round the Nandi's pedestal.
                for (int s = 0; s < 4; s++)
                {
                    double da = s == 0 ? -1 : s == 2 ? 1 : 0, db = s == 1 ? 1 : s == 3 ? -1 : 0;
                    double ca = da * 11.4, cb = db * 11.6;
                    Stain(ref f, m, ca, cb, 1.6, 0.9, da, db, Vermilion, (uint)s);
                    Stain(ref f, m, ca + 0.4 * db, cb + 0.4 * da, 0.8, 0.5, da, db, Petals, (uint)s + 7);
                }
                Stain(ref f, m, -15.5, 1.9, 1.8, 0.6, 1, 0, Vermilion, 31u);
                Stain(ref f, m, -12.6, -0.6, 0.9, 0.7, 1, 0, Petals, 32u);
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>The west forecourt (PT-05): light grey flags three steps above the courtyard and the red runner with its
        /// orange border to the gate.</summary>
        internal static void Forecourt(ref PashupatiFrame f, MeshData m)
        {
            if (f.Lod >= 3) return;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double y = PashupatiSite.ForecourtRiseM + 0.02, cellM = f.Lod == 0 ? 6.0 : f.Lod == 1 ? 14.0 : 40.0;
            Paving(ref f, m, -76.0, -40.1, -19.0, 21.0, y, cellM, FlagsLight, 21u);
            if (f.Lod <= 1)
            {
                // The runner: 1.4 m of red with an orange edge either side, from the square to the gate.
                const double B = PashupatiGates.WestB;
                Strip(ref f, m, -66.0, -40.1, B - 0.7, B + 0.7, y + 0.012, Runner);
                Strip(ref f, m, -66.0, -40.1, B - 0.82, B - 0.7, y + 0.014, RunnerEdge);
                Strip(ref f, m, -66.0, -40.1, B + 0.7, B + 0.82, y + 0.014, RunnerEdge);
            }
            SacredDraw.Bake(m, v0, i0, f.Level + PashupatiSite.ForecourtRiseM);
        }

        /// <summary>A flat paved rectangle (site a0..a1, b0..b1 at height y) in cells of about <paramref name="cellM"/>,
        /// each cell's tint jittered (worn slabs).</summary>
        internal static void Paving(ref PashupatiFrame f, MeshData m, double a0, double a1, double b0, double b1, double y, double cellM, in ShapeBrush b,
                                    uint seed)
        {
            int na = Math.Max(1, (int)Math.Round((a1 - a0) / cellM)), nb = Math.Max(1, (int)Math.Round((b1 - b0) / cellM));
            // Draped cells, where the terrain's grid rises through the level (round the zone edges, mostly under the
            // buildings, and the strip at the West Gate's courtyard face where the forecourt's step eases down).
            double sub = Math.Max(2.0, 0.25 * cellM);
            for (int i = 0; i < na; i++)
            {
                for (int j = 0; j < nb; j++)
                {
                    double qa0 = a0 + (a1 - a0) * i / na, qa1 = a0 + (a1 - a0) * (i + 1) / na;
                    double qb0 = b0 + (b1 - b0) * j / nb, qb1 = b0 + (b1 - b0) * (j + 1) / nb;
                    uint h = ShapeNoise.Hash(i, j, 3, seed);
                    float k = 0.92f + 0.12f * (h % 1000) / 1000f;
                    ShapeBrush c = b.WithColor(MeshColor.Scale(b.Color, k));
                    int ka = 1, kb = 1;
                    if (Rises(ref f, qa0, qa1, qb0, qb1, y))
                    {
                        ka = Math.Max(1, (int)Math.Ceiling((qa1 - qa0) / sub));
                        kb = Math.Max(1, (int)Math.Ceiling((qb1 - qb0) / sub));
                    }
                    for (int u = 0; u < ka; u++)
                    {
                        for (int w = 0; w < kb; w++)
                        {
                            double sa0 = qa0 + (qa1 - qa0) * u / ka, sa1 = qa0 + (qa1 - qa0) * (u + 1) / ka;
                            double sb0 = qb0 + (qb1 - qb0) * w / kb, sb1 = qb0 + (qb1 - qb0) * (w + 1) / kb;
                            SacredDraw.Quad(m, f.Site, sa0, Drape(ref f, sa0, sb0, y), sb0, sa1, Drape(ref f, sa1, sb0, y), sb0, sa1, Drape(ref f, sa1, sb1, y), sb1,
                                            sa0, Drape(ref f, sa0, sb1, y), sb1, 0, 1, 0, c);
                        }
                    }
                }
            }
        }

        /// <summary>The paving height at site (a, b): <paramref name="y"/>, or 2 cm over the terrain where it rises above
        /// (on any tier's decimation: <see cref="PashupatiFrame.TopGroundAt"/>).</summary>
        private static double Drape(ref PashupatiFrame f, double a, double b, double y)
        {
            return f.Terrain == null ? y : Math.Max(y, f.TopGroundAt(a, b, y) + 0.02);
        }

        /// <summary>Whether the terrain rises through a level cell anywhere (1 m samples).</summary>
        private static bool Rises(ref PashupatiFrame f, double a0, double a1, double b0, double b1, double y)
        {
            if (f.Terrain == null) return false;
            int sa = Math.Max(1, (int)Math.Ceiling(a1 - a0)), sb = Math.Max(1, (int)Math.Ceiling(b1 - b0));
            for (int i = 0; i <= sa; i++)
                for (int j = 0; j <= sb; j++)
                    if (f.TopGroundAt(a0 + (a1 - a0) * i / sa, b0 + (b1 - b0) * j / sb, y) + 0.02 > y + 0.003) return true;
            return false;
        }


        /// <summary>A flat strip on the floor.</summary>
        internal static void Strip(ref PashupatiFrame f, MeshData m, double a0, double a1, double b0, double b1, double y, in ShapeBrush b)
        {
            SacredDraw.Quad(m, f.Site, a0, y, b0, a1, y, b0, a1, y, b1, a0, y, b1, 0, 1, 0, b);
        }

        /// <summary>An irregular flat stain (a jittered octagon) centred on site (a, b), long along (da, db).</summary>
        private static void Stain(ref PashupatiFrame f, MeshData m, double a, double b, double len, double wid, double da, double db, in ShapeBrush br, uint seed)
        {
            const int N = 8;
            int c = SacredDraw.V(m, f.Site, a, 0.035, b, 0, 1, 0, br);
            int first = m.VertexCount;
            for (int i = 0; i < N; i++)
            {
                double t = 2 * Math.PI * i / N, k = 0.75 + 0.5 * (0.5 + 0.5 * ShapeNoise.Unit(i, 1, 5, seed));
                double u = 0.5 * len * Math.Cos(t) * k, w = 0.5 * wid * Math.Sin(t) * k;
                SacredDraw.V(m, f.Site, a + u * da - w * db, 0.03, b + u * db + w * da, 0, 1, 0, br);
            }
            for (int i = 0; i < N; i++) SacredDraw.Tri(m, c, first + i, first + (i + 1) % N);
        }
    }
}
