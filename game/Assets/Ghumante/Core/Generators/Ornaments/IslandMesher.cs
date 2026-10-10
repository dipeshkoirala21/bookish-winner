using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// The island itself and everything on it except the centrepiece (docs/research/w2/ref_ornaments.md §8): the
    /// painted kerb (yellow-black or black-white bands), the cobble apron of rings, the domed lawn (or stone paving),
    /// flower beds (marigolds, roses, white flowers), clipped hedges and shrubs, trees, radial paths, the island
    /// railing, solar street lights, the keep-left sign and the traffic police post.
    /// </summary>
    internal static class IslandMesher
    {
        /// <summary>Band length of the painted kerb at LOD 0 (Kathmandu practice: about 0.6 m).</summary>
        public const double KerbBandM = 0.6;

        /// <summary>Width of the kerb top inside the island edge.</summary>
        public const double KerbTopM = 0.3;

        // ------------------------------------------------------------------ outline

        /// <summary>Fill c.Xs/Zs/Nx/Nz with <paramref name="n"/> points of the island outline offset inward by
        /// <paramref name="inset"/> (CCW seen from above), tile-local. Returns the perimeter of the inset outline.</summary>
        public static double Outline(OrnCtx c, int n, double inset)
        {
            Grow(c, n + 1);
            RoundaboutSite s = c.Site;
            double r = Math.Max(0.5, s.RadiusM - inset);
            for (int k = 0; k < n; k++)
            {
                // Compass angle decreasing = counter-clockwise seen from above.
                double a = -2 * Math.PI * k / n;
                double sx = Math.Sin(a), cz = Math.Cos(a);
                c.Xs[k] = s.X + r * sx;
                c.Zs[k] = s.Z + r * cz;
                c.Nx[k] = sx;
                c.Nz[k] = cz;
            }
            return 2 * Math.PI * r;
        }

        private static void Grow(OrnCtx c, int n)
        {
            if (c.Xs.Length >= n) return;
            int cap = Math.Max(n, c.Xs.Length * 2);
            c.Xs = new double[cap];
            c.Zs = new double[cap];
            c.Nx = new double[cap];
            c.Nz = new double[cap];
        }

        /// <summary>Radius inside the kerb (and apron) where planting may start.</summary>
        public static double InnerRadius(in RoundaboutSite s)
        {
            return Math.Max(0.3, s.RadiusM - Math.Max(0, s.ApronM) - KerbTopM);
        }

        // ------------------------------------------------------------------ kerb, apron, lawn

        /// <summary>The kerb ring with its painted bands, the apron, and the lawn (or paving) inside.</summary>
        public static void Base(OrnCtx c)
        {
            MeshData m = c.M;
            RoundaboutSite s = c.Site;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double band = (s.RadiusM > 14 ? 1.4 : s.RadiusM > 10 ? 1.0 : KerbBandM) * (c.Lod == 0 ? 1 : 3);
            double perim = Outline(c, 8, 0);
            int n = (int)Math.Round(perim / band);
            if (c.Lod == 2) n = Math.Max(16, Math.Min(32, (int)(perim / 5)));
            else if (c.Lod >= 3) n = Math.Max(10, Math.Min(16, (int)(perim / 6)));
            n = Math.Max(c.Lod >= 3 ? 10 : 16, n + (n & 1));
            if (n > 240) n = 240;
            Outline(c, n, 0);
            double apron = Math.Max(0, Math.Min(s.ApronM, 0.4 * s.RadiusM));
            bool painted = c.Design.Kerb != KerbPaint.Stone && c.Lod < 2;
            uint a = c.Design.Kerb == KerbPaint.BlackWhite ? OrnamentPalette.PaintWhite : OrnamentPalette.PaintYellow;
            uint bcol = OrnamentPalette.PaintBlack;
            uint plain = c.Design.Kerb == KerbPaint.Stone ? OrnamentPalette.KerbStone : MeshColor.Lerp(a, bcol, 0.45f);
            float k = c.KerbH + (apron > 0 ? 0.05f : 0f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                uint col = painted ? ((i & 1) == 0 ? a : bcol) : plain;
                MaterialChannel ch = painted ? MaterialChannel.Paint : MaterialChannel.Stone;
                float ra = c.RoadY(c.Xs[i], c.Zs[i]), rb = c.RoadY(c.Xs[j], c.Zs[j]);
                bool round = c.Lod == 0 && s.RadiusM <= 10;
                if (c.Lod >= 2)
                {
                    // Far away: the kerb face and its top only (the apron reads as part of the road).
                    KerbStrip(m, c, i, j, 0, -0.1, 0, k, ra, rb, col, ch);
                    if (c.Lod == 2) KerbStrip(m, c, i, j, 0, k, apron + KerbTopM + 0.1, k, ra, rb, OrnamentPalette.KerbStone, MaterialChannel.Stone);
                    continue;
                }
                if (apron > 0)
                {
                    // Low mountable outer kerb, cobble apron, then the island kerb with a rounded nose.
                    KerbStrip(m, c, i, j, 0, -0.1, 0, round ? 0.08 : 0.11, ra, rb, col, ch);
                    if (round) KerbStrip(m, c, i, j, 0, 0.08, 0.06, 0.11, ra, rb, col, ch);
                    KerbStrip(m, c, i, j, round ? 0.06 : 0, 0.11, apron - 0.2, 0.11, ra, rb, OrnamentPalette.Cobble, MaterialChannel.Flagstone);
                    KerbStrip(m, c, i, j, apron - 0.2, 0.11, apron - 0.2, round ? k - 0.05 : k, ra, rb, col, ch);
                    if (round) KerbStrip(m, c, i, j, apron - 0.2, k - 0.05, apron - 0.14, k, ra, rb, col, ch);
                    KerbStrip(m, c, i, j, round ? apron - 0.14 : apron - 0.2, k, apron + KerbTopM + 0.1, k, ra, rb, OrnamentPalette.KerbStone, MaterialChannel.Stone);
                }
                else
                {
                    KerbStrip(m, c, i, j, 0, -0.1, 0, round ? k - 0.06 : k, ra, rb, col, ch);
                    if (round) KerbStrip(m, c, i, j, 0, k - 0.06, 0.07, k, ra, rb, col, ch);
                    KerbStrip(m, c, i, j, round ? 0.07 : 0, k, KerbTopM + 0.1, k, ra, rb, OrnamentPalette.KerbStone, MaterialChannel.Stone);
                }
            }
            c.Ao(v0, i0, c.RoadY(s.X, s.Z) - 0.1, 0.3f);
            Lawn(c, apron);
        }

        /// <summary>One quad of the kerb profile between outline points i and j: from (d0 inward, h0 above the road) to
        /// (d1, h1).</summary>
        private static void KerbStrip(MeshData m, OrnCtx c, int i, int j, double d0, double h0, double d1, double h1, float ra, float rb,
                                      uint col, MaterialChannel ch)
        {
            // Profile edge normal in (outward, up): edge (−(d1 − d0), h1 − h0) rotated.
            double eo = -(d1 - d0), eu = h1 - h0;
            double no = eu, nu = -eo;
            double l = Math.Sqrt(no * no + nu * nu);
            if (l < 1e-9) return;
            no /= l;
            nu /= l;
            int v = m.VertexCount;
            Vtx(m, c.Xs[i] - c.Nx[i] * d0, ra + h0, c.Zs[i] - c.Nz[i] * d0, c.Nx[i] * no, nu, c.Nz[i] * no, col, ch);
            Vtx(m, c.Xs[j] - c.Nx[j] * d0, rb + h0, c.Zs[j] - c.Nz[j] * d0, c.Nx[j] * no, nu, c.Nz[j] * no, col, ch);
            Vtx(m, c.Xs[j] - c.Nx[j] * d1, rb + h1, c.Zs[j] - c.Nz[j] * d1, c.Nx[j] * no, nu, c.Nz[j] * no, col, ch);
            Vtx(m, c.Xs[i] - c.Nx[i] * d1, ra + h1, c.Zs[i] - c.Nz[i] * d1, c.Nx[i] * no, nu, c.Nz[i] * no, col, ch);
            OrnamentKit.Tri(m, v, v + 1, v + 2);
            OrnamentKit.Tri(m, v, v + 2, v + 3);
        }

        private static void Vtx(MeshData m, double x, double y, double z, double nx, double ny, double nz, uint col, MaterialChannel ch)
        {
            OrnamentKit.Norm(ref nx, ref ny, ref nz);
            m.AddVertex((float)x, (float)y, (float)z, (float)nx, (float)ny, (float)nz, col, (float)ch, 1f);
        }

        /// <summary>The domed lawn (or stone paving) filling the island inside the kerb top: concentric rings of the
        /// inset outline, heights from <see cref="OrnCtx.TopY"/>.</summary>
        private static void Lawn(OrnCtx c, double apron)
        {
            MeshData m = c.M;
            RoundaboutSite s = c.Site;
            bool paved = c.Design.Garden == GardenStyle.Paved;
            int ns = c.Lod >= 3 ? 10 : c.Lod == 2 ? 16 : s.RadiusM > 14 ? (c.Lod == 0 ? 48 : 24) : (c.Lod == 0 ? 32 : 16);
            int rings = c.Lod >= 1 ? 1 : s.RadiusM > 14 ? 4 : 3;
            double inset = c.Lod >= 3 ? 0.05 : apron + KerbTopM;
            Outline(c, ns, inset);
            uint col = paved ? OrnamentPalette.PathStone : OrnamentPalette.Lawn;
            MaterialChannel ch = paved ? MaterialChannel.Flagstone : MaterialChannel.Grass;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            for (int r = 0; r <= rings; r++)
            {
                double f = 1.0 - (double)r / rings;
                f = f * (2 - f) * 0.5 + 0.5 * f; // rings denser near the edge
                if (r == rings)
                {
                    float yc = c.TopY(s.X, s.Z) - 0.004f;
                    m.AddVertex((float)s.X, yc, (float)s.Z, 0, 1, 0, col, (float)ch, 1f);
                    break;
                }
                for (int k = 0; k < ns; k++)
                {
                    double x = s.X + (c.Xs[k] - s.X) * f, z = s.Z + (c.Zs[k] - s.Z) * f;
                    float y = c.TopY(x, z) - 0.004f;
                    m.AddVertex((float)x, y, (float)z, 0, 1, 0, col, (float)ch, 1f);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                int a0 = v0 + r * ns, b0 = v0 + (r + 1) * ns;
                for (int k = 0; k < ns; k++)
                {
                    int k1 = (k + 1) % ns;
                    if (r == rings - 1)
                    {
                        OrnamentKit.Tri(m, a0 + k, a0 + k1, b0);
                        continue;
                    }
                    OrnamentKit.Tri(m, a0 + k, a0 + k1, b0 + k1);
                    OrnamentKit.Tri(m, a0 + k, b0 + k1, b0 + k);
                }
            }
            ShapeNoise.RecomputeNormals(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0);
            if (!paved) ShapeColor.JitterByPosition(m, v0, m.VertexCount - v0, 0.06f, c.Seed, 2.5);
        }

        // ------------------------------------------------------------------ planting

        /// <summary>Flower beds, hedges, shrubs and trees between the centrepiece footprint (radius
        /// <paramref name="rc"/>) and the kerb.</summary>
        public static void Garden(OrnCtx c, double rc)
        {
            if (c.Lod >= 3) return;
            RoundaboutSite s = c.Site;
            double ri = InnerRadius(s);
            double span = ri - rc;
            if (span < 0.8) return;
            MeshData m = c.M;
            uint seed = c.Seed;
            int segs = Math.Max(16, Math.Min(c.Lod == 0 ? 56 : 40, (int)(2 * Math.PI * ri / (c.Lod == 0 ? 1.0 : 2.4))));
            int v0 = m.VertexCount, i0 = m.IndexCount;
            if (c.Lod >= 1)
            {
                FlatGarden(c, rc, ri, span);
                c.Ao(v0, i0, c.CentreTopY - 0.2, 0.3f);
                return;
            }
            switch (c.Design.Garden)
            {
                case GardenStyle.Marigold:
                {
                    double r = rc + 0.45 * span, w = Math.Min(1.1, 0.3 * span);
                    RingMound(c, r, 0, 360, segs, 0, 0, w, 0.38, OrnamentPalette.MarigoldOrange, OrnamentPalette.MarigoldYellow, 0.06, seed, 0.35, true);
                    Blooms(c, r, w, 0.42, (int)(2 * Math.PI * r / 0.9), OrnamentPalette.MarigoldOrange, OrnamentPalette.MarigoldYellow, seed);
                    if (span > 2.6) Hedge(c, ri - 0.55, 0.5, 0.55, segs);
                    break;
                }
                case GardenStyle.Roses:
                {
                    double r = rc + 0.5 * span;
                    SoilRing(c, r, Math.Min(1.0, 0.3 * span), segs);
                    int bushes = Math.Min(14, (int)(2 * Math.PI * r / 1.6));
                    for (int b = 0; b < bushes; b++)
                    {
                        double ang = 2 * Math.PI * (b + 0.5) / bushes;
                        double x = s.X + r * Math.Sin(ang), z = s.Z + r * Math.Cos(ang);
                        if (c.Clearance(x, z) < 0.45) continue;
                        RoseBush(c, x, c.TopY(x, z), z, seed + (uint)b);
                    }
                    if (span > 2.6) Hedge(c, ri - 0.55, 0.5, 0.6, segs);
                    break;
                }
                case GardenStyle.Mixed:
                {
                    // Sweeping marigold border near the kerb, an inner wavy bed, round clipped shrubs, palms.
                    double r = ri - 0.9;
                    RingMound(c, r, 0, 360, segs, 0.04, 7, 0.9, 0.36, OrnamentPalette.MarigoldYellow, OrnamentPalette.MarigoldOrange, 0.06, seed, 0.4, true);
                    Blooms(c, r, 0.9, 0.4, (int)(2 * Math.PI * r / 1.1), OrnamentPalette.MarigoldYellow, OrnamentPalette.MarigoldOrange, seed);
                    if (span > 5)
                        RingMound(c, rc + 0.35 * span, 20, 300, segs / 2, 0.12, 3, 0.8, 0.32, OrnamentPalette.MarigoldOrange, OrnamentPalette.MarigoldYellow, 0.06,
                                  seed + 7, 0.5, false);
                    Shrubs(c, rc + 0.5, ri - 1.8, seed);
                    break;
                }
                case GardenStyle.White:
                {
                    // The "Garden of Hope" plan: white flower beds in arcs alternating with marigold arcs, a hedge.
                    double r = rc + 0.5 * span;
                    int arcs = Math.Max(4, Math.Min(8, (int)(2 * Math.PI * r / 14)));
                    double sweep = 360.0 / arcs;
                    int arcSegs = Math.Max(4, segs / arcs);
                    for (int q = 0; q < arcs; q++)
                    {
                        bool white = (q & 1) == 0;
                        RingMound(c, r, sweep * q + 8, sweep - 16, arcSegs, 0, 0, white ? 1.3 : 1.0, 0.34, white ? OrnamentPalette.FlowerWhite : OrnamentPalette.MarigoldYellow,
                                  white ? OrnamentPalette.LeafGreen : OrnamentPalette.MarigoldOrange, 0.05, seed + (uint)q, white ? 0.25 : 0.35, true);
                    }
                    Hedge(c, ri - 0.45, 0.5, 0.55, segs);
                    break;
                }
                case GardenStyle.Lawn:
                    Shrubs(c, rc + 0.4, ri - 0.8, seed);
                    break;
                case GardenStyle.Paved:
                {
                    // Potted plants round a statue's paving (New Road); a chautari's paving stays bare.
                    if (c.Design.Centre == Centrepiece.ShadeTree) break;
                    int pots = Math.Max(3, (int)(2 * Math.PI * (ri - 0.7) / 3.2));
                    if (c.Lod >= 2) pots = Math.Min(pots, 4);
                    for (int p = 0; p < pots; p++)
                    {
                        double ang = 2 * Math.PI * (p + 0.5) / pots;
                        double x = s.X + (ri - 0.7) * Math.Sin(ang), z = s.Z + (ri - 0.7) * Math.Cos(ang);
                        if (c.Clearance(x, z) < 0.45) continue;
                        OrnamentKit.Pot(c, x, c.TopY(x, z), z, 0.3, seed + (uint)p);
                    }
                    break;
                }
            }
            c.Ao(v0, i0, c.CentreTopY - 0.2, 0.4f);
        }

        /// <summary>Planting seen from 60 m and beyond: the beds become low coloured rings (lathe annuli), a few
        /// shrubs stay as soft balls.</summary>
        private static void FlatGarden(OrnCtx c, double rc, double ri, double span)
        {
            RoundaboutSite s = c.Site;
            int radial = c.Lod == 1 ? Math.Max(16, Math.Min(32, (int)(ri * 2))) : 12;
            uint seed = c.Seed;
            switch (c.Design.Garden)
            {
                case GardenStyle.Marigold:
                    FlatBed(c, rc + 0.45 * span, Math.Min(1.1, 0.3 * span), 0.35, OrnamentPalette.MarigoldOrange, radial);
                    if (span > 2.6 && c.Lod == 1) FlatBed(c, ri - 0.55, 0.5, 0.55, OrnamentPalette.Hedge, radial);
                    break;
                case GardenStyle.Roses:
                    FlatBed(c, rc + 0.5 * span, Math.Min(1.0, 0.3 * span), 0.45, OrnamentPalette.Hedge, radial);
                    if (span > 2.6 && c.Lod == 1) FlatBed(c, ri - 0.55, 0.5, 0.6, OrnamentPalette.Hedge, radial);
                    break;
                case GardenStyle.Mixed:
                    FlatBed(c, ri - 0.9, 0.9, 0.36, OrnamentPalette.MarigoldYellow, radial);
                    if (span > 5 && c.Lod == 1) FlatBed(c, rc + 0.35 * span, 0.8, 0.32, OrnamentPalette.MarigoldOrange, radial);
                    Shrubs(c, rc + 0.5, ri - 1.8, seed);
                    break;
                case GardenStyle.White:
                    FlatBed(c, rc + 0.5 * span, 1.3, 0.34, OrnamentPalette.FlowerWhite, radial);
                    if (c.Lod == 1) FlatBed(c, ri - 0.45, 0.5, 0.55, OrnamentPalette.Hedge, radial);
                    break;
                case GardenStyle.Lawn:
                    Shrubs(c, rc + 0.4, ri - 0.8, seed);
                    break;
                case GardenStyle.Paved:
                {
                    int pots = c.Lod == 1 && c.Design.Centre != Centrepiece.ShadeTree ? 4 : 0;
                    for (int p = 0; p < pots; p++)
                    {
                        double ang = 2 * Math.PI * (p + 0.5) / pots;
                        double x = s.X + (ri - 0.7) * Math.Sin(ang), z = s.Z + (ri - 0.7) * Math.Cos(ang);
                        OrnamentKit.Pot(c, x, c.TopY(x, z), z, 0.3, seed + (uint)p);
                    }
                    break;
                }
            }
        }

        /// <summary>A low ring bed of radius r (centreline), width w and height h: a lathe annulus on the lawn.</summary>
        private static void FlatBed(OrnCtx c, double r, double w, double h, uint col, int radial)
        {
            RoundaboutSite s = c.Site;
            float y = c.TopY(s.X + r, s.Z) - 0.02f;
            double ro = r + 0.5 * w, rin = Math.Max(0, r - 0.5 * w);
            Profile2 p = c.Q.Clear(false);
            p.Add(ro, 0, true).Add(ro - 0.15 * w, h, true).Add(rin + 0.15 * w, h, true).Add(rin, 0, true);
            Shapes.Lathe(c.M, Affine3.Translation(s.X, y, s.Z), OrnamentKit.B(col, MaterialChannel.Foliage), p, radial, c.L);
        }

        private static void Blooms(OrnCtx c, double r, double width, double h, int count, uint a, uint b, uint seed)
        {
            if (c.Lod > 0) return;
            RoundaboutSite s = c.Site;
            count = Math.Min(count, 32);
            for (int i = 0; i < count; i++)
            {
                double ang = 2 * Math.PI * (i + OrnamentSeed.Unit(seed, 3 * i)) / count;
                double rr = r + (OrnamentSeed.Unit(seed, 3 * i + 1) - 0.5) * 0.6 * width;
                double x = s.X + rr * Math.Sin(ang), z = s.Z + rr * Math.Cos(ang);
                // Only where the bed itself is drawn (RingMound leaves gaps round furniture and the centrepiece).
                if (c.Clearance(s.X + r * Math.Sin(ang), s.Z + r * Math.Cos(ang)) < 0.5 * width + 0.05) continue;
                OrnamentKit.Bloom(c, x, c.TopY(x, z) + h, z, 0.1, OrnamentSeed.Unit(seed, 3 * i + 2) < 0.5 ? a : b);
            }
        }

        private static void Hedge(OrnCtx c, double r, double width, double height, int segs)
        {
            RingMound(c, r, 0, 360, segs, 0, 0, width, height, OrnamentPalette.Hedge, OrnamentPalette.HedgeLight, 0.025, c.Seed ^ 0x77u, 0.5, false);
            if (c.Stats != null) c.Stats.Shrubs++;
        }

        private static void SoilRing(OrnCtx c, double r, double w, int segs)
        {
            RingMound(c, r, 0, 360, segs, 0, 0, w, 0.08, OrnamentPalette.Soil, OrnamentPalette.Soil, 0, c.Seed, 1, true, -0.02, MaterialChannel.Dirt);
        }

        /// <summary>
        /// A bed or hedge mound along the circle (or arc from <paramref name="startDeg"/> over <paramref name="sweepDeg"/>)
        /// of radius r round the island centre, with an optional wobble: drawn as the arcs that stay clear of every
        /// footprint taken (a gap round the police post, a lamp, a tree, the sign), or as one closed loop when nothing
        /// stands in the way.
        /// </summary>
        private static void RingMound(OrnCtx c, double r, double startDeg, double sweepDeg, int segs, double wobble, int lobes, double width, double height,
                                      uint a, uint b, double bump, uint seed, double cell, bool smooth, double lift = 0, MaterialChannel ch = MaterialChannel.Foliage)
        {
            RoundaboutSite s = c.Site;
            bool full = sweepDeg >= 359.9;
            int n = Math.Max(full ? 8 : 2, Math.Min(c.Blocked.Length - 1, 2 * segs));
            double clear = 0.5 * width + 0.05;
            int samples = full ? n : n + 1, blocked = 0;
            for (int k = 0; k < samples; k++)
            {
                double deg = startDeg + sweepDeg * k / n, ang = deg * Math.PI / 180.0;
                double rr = r * (1 + wobble * Math.Sin(lobes * ang));
                bool hit = c.Clearance(s.X + rr * Math.Sin(ang), s.Z + rr * Math.Cos(ang)) < clear;
                c.Blocked[k] = hit;
                if (hit) blocked++;
            }
            if (blocked == 0)
            {
                OrnamentKit.Mound(c, OrnamentKit.Ring(c, c.Path, s.X, s.Z, r, lift, startDeg, sweepDeg, segs, wobble, lobes), full, width, height, a, b, ch, bump,
                                  seed, cell, smooth);
                return;
            }
            if (blocked == samples) return;
            // Runs of free samples; a full ring starts its walk at a blocked sample so no run wraps round.
            int origin = 0;
            if (full)
                while (!c.Blocked[origin]) origin++;
            int run = -1;
            for (int q = 0; q <= samples; q++)
            {
                int k = full ? (origin + q) % samples : q;
                bool free = q < samples && !c.Blocked[k] && !(full && q == 0);
                if (free && run < 0) run = q;
                if (free || run < 0) continue;
                int len = q - run;
                if (len >= 2)
                {
                    double d0 = startDeg + sweepDeg * (full ? origin + run : run) / n, d1 = d0 + sweepDeg * (len - 1) / n;
                    int arcSegs = Math.Max(1, (int)Math.Round((double)segs * (len - 1) / n));
                    OrnamentKit.Mound(c, OrnamentKit.Ring(c, c.Path, s.X, s.Z, r, lift, d0, d1 - d0, arcSegs, wobble, lobes), false, width, height, a, b, ch,
                                      bump, seed + (uint)run, cell, smooth);
                }
                run = -1;
            }
        }

        private static void RoseBush(OrnCtx c, double x, double y, double z, uint seed)
        {
            OrnamentKit.Clump(c, x, y + 0.32, z, 0.32, 0.3, 0.32, OrnamentPalette.Hedge, MaterialChannel.Foliage, 8, 0.25, seed);
            if (c.Lod == 0)
            {
                uint col = (seed & 1) == 0 ? OrnamentPalette.RoseRed : OrnamentPalette.RosePink;
                for (int k = 0; k < 2; k++)
                {
                    double a = 2.1 * k + (seed % 5);
                    OrnamentKit.Bloom(c, x + 0.2 * Math.Cos(a), y + 0.55 + 0.05 * k, z + 0.2 * Math.Sin(a), 0.08, col);
                }
            }
            if (c.Stats != null) c.Stats.Shrubs++;
        }

        /// <summary>Round clipped shrubs (golden and dark green), purple bougainvillea, palms and small conifers
        /// scattered over the lawn between r0 and r1, deterministic. Every crown stays over the island (at most over its
        /// kerb top), never over the road: an item whose crown would reach past the edge moves in, or is left out.</summary>
        private static void Shrubs(OrnCtx c, double r0, double r1, uint seed)
        {
            if (r1 <= r0 + 0.3) return;
            RoundaboutSite s = c.Site;
            double edge = InnerRadius(s) + 0.5 * KerbTopM;
            double area = Math.PI * (r1 * r1 - r0 * r0);
            int n = Math.Min(c.Lod == 0 ? 9 : c.Lod == 1 ? 5 : 3, (int)(area / 16));
            int palms = 0;
            for (int i = 0; i < n; i++)
            {
                double ang = 2 * Math.PI * (i + 0.6 * OrnamentSeed.Unit(seed, 11 * i)) / Math.Max(1, n);
                double rr = Math.Sqrt(r0 * r0 + (r1 * r1 - r0 * r0) * OrnamentSeed.Unit(seed, 11 * i + 1));
                float kind = OrnamentSeed.Unit(seed, 11 * i + 2);
                // Crown reach from the stem: clipped balls up to 0.9 m, a palm's lean and fronds 2.3 m, a conifer 0.9 m.
                bool palm = kind >= 0.82 && kind < 0.92 && c.Lod == 0 && palms < 2;
                double reach = c.Lod >= 2 ? 0.6 : kind < 0.82 ? 0.9 : palm ? 2.3 : 0.9;
                rr = Math.Min(rr, edge - reach);
                if (rr < r0 || rr < 0) continue;
                double x = s.X + rr * Math.Sin(ang), z = s.Z + rr * Math.Cos(ang);
                // Not in the police post, a lamp, a tree or the sign.
                if (c.Clearance(x, z) < Math.Min(reach, 0.9) + 0.1) continue;
                // Nothing tall in front of or behind a centrepiece: palms and conifers there become clipped shrubs, so
                // the statue, the gate or the mandala stays in view from the arms it faces.
                if (kind >= 0.82 && HasCentrepiece(c) && InSightline(c, x, z))
                {
                    kind = 0.3f;
                    palm = false;
                }
                float y = c.TopY(x, z);
                if (c.Lod >= 2)
                {
                    Shapes.Sphere(c.M, Affine3.Translation(x, y + 0.5, z) * Affine3.Scaling(1, 0.8, 1), OrnamentKit.B(kind < 0.5 ? OrnamentPalette.ShrubGolden : OrnamentPalette.Hedge, MaterialChannel.Foliage), 0.6, 5, c.L);
                    continue;
                }
                if (kind < 0.42)
                {
                    double r = 0.45 + 0.35 * OrnamentSeed.Unit(seed, 11 * i + 3);
                    OrnamentKit.Clump(c, x, y + 0.85 * r, z, r, 0.85 * r, r, OrnamentPalette.ShrubGolden, MaterialChannel.Foliage, 8, 0.08, seed + (uint)i);
                }
                else if (kind < 0.7)
                {
                    double r = 0.5 + 0.4 * OrnamentSeed.Unit(seed, 11 * i + 3);
                    OrnamentKit.Clump(c, x, y + 0.8 * r, z, r, 0.8 * r, r, OrnamentPalette.Hedge, MaterialChannel.Foliage, 8, 0.08, seed + (uint)i);
                }
                else if (kind < 0.82)
                {
                    OrnamentKit.Clump(c, x, y + 0.5, z, 0.6, 0.5, 0.55, OrnamentPalette.Bougainvillea, MaterialChannel.Foliage, 8, 0.3, seed + (uint)i);
                }
                else if (palm)
                {
                    Palm(c, x, y, z, 2.6 + 1.4 * OrnamentSeed.Unit(seed, 11 * i + 4), seed + (uint)i);
                    palms++;
                }
                else
                {
                    Conifer(c, x, y, z, 2.8 + 2.0 * OrnamentSeed.Unit(seed, 11 * i + 4), seed + (uint)i);
                }
                if (c.Stats != null) c.Stats.Shrubs++;
            }
        }

        private static bool HasCentrepiece(OrnCtx c)
        {
            Centrepiece k = c.Design.Centre;
            return k != Centrepiece.Garden && k != Centrepiece.PolicePodium;
        }

        /// <summary>True when (x, z) lies within 40 degrees of the centrepiece's facing axis, in front or behind.</summary>
        private static bool InSightline(OrnCtx c, double x, double z)
        {
            double b = Math.Atan2(x - c.CX, z - c.CZ) * 180.0 / Math.PI - c.FacingDeg;
            b = Math.Abs(b % 180.0);
            if (b > 90) b = 180 - b;
            return b < 40;
        }

        /// <summary>A raised round planter at tile-local (x, z): a brick ring wall with a coping, soil, a mound of
        /// marigolds and a clipped shrub in the middle (generic island centres, Maitighar's round feature bed).
        /// Returns its footprint radius.</summary>
        public static double RaisedBed(OrnCtx c, double x, double z, double r)
        {
            MeshData m = c.M;
            float y = c.TopY(x, z);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            if (c.Lod >= 2)
            {
                Shapes.Cylinder(m, Affine3.Translation(x, y - 0.1, z), OrnamentKit.B(OrnamentPalette.BrickPlanter, MaterialChannel.Brick), r + 0.25, 0.45, 8, 0, 0, false, false, c.L);
                Shapes.Dome(m, Affine3.Translation(x, y + 0.33, z), OrnamentKit.B(OrnamentPalette.MarigoldOrange, MaterialChannel.Foliage), r + 0.2, 0.5, 8, false, c.L);
                return r + 0.4;
            }
            Shapes.Cylinder(m, Affine3.Translation(x, y - 0.1, z), OrnamentKit.B(OrnamentPalette.BrickPlanter, MaterialChannel.Brick), r + 0.25, 0.45, 28, 0.04, 1, false, true, c.L);
            Shapes.Dome(m, Affine3.Translation(x, y + 0.33, z), OrnamentKit.B(OrnamentPalette.Soil, MaterialChannel.Dirt), r, 0.12, 20, false, c.L);
            Path3 ring = c.Path.Clear();
            int n = c.Lod == 0 ? 28 : 14;
            for (int k = 0; k < n; k++)
            {
                double a = 2 * Math.PI * k / n, px = x + 0.72 * r * Math.Sin(a), pz = z + 0.72 * r * Math.Cos(a);
                ring.Add(px, y + 0.35, pz);
            }
            OrnamentKit.Mound(c, ring, true, 0.5 * r, 0.3, OrnamentPalette.MarigoldOrange, OrnamentPalette.MarigoldYellow, MaterialChannel.Foliage, 0.05, c.Seed + 3, 0.25);
            OrnamentKit.Clump(c, x, y + 0.35 + 0.55 * r, z, 0.45 * r, 0.55 * r, 0.45 * r, OrnamentPalette.Hedge, MaterialChannel.Foliage, 12, 0.12, c.Seed + 5);
            c.Ao(v0, i0, y - 0.1, 0.4f);
            if (c.Stats != null) c.Stats.Shrubs++;
            return r + 0.4;
        }

        // ------------------------------------------------------------------ trees

        /// <summary>A ring of <paramref name="count"/> trees at radius r round the island centre: columnar Ashoka-like
        /// conifers alternating with round-crowned shade trees; a tree whose spot is taken (centrepiece, furniture) or
        /// on a road corridor moves along the ring a little or is left out.</summary>
        public static void TreeRing(OrnCtx c, double r, int count)
        {
            if (count <= 0 || c.Lod >= 3) return;
            MeshData m = c.M;
            if (c.Lod == 2) count = Math.Min(count, 4);
            double inner = InnerRadius(c.Site), edge = inner + 0.5 * KerbTopM;
            for (int i = 0; i < count; i++)
            {
                double prefer = 360.0 * (i + 0.5) / count + c.FacingDeg;
                if (HasCentrepiece(c))
                {
                    // Keep the centrepiece in view from the arms it faces: no tree within 40 degrees of its axis.
                    double rel = ((prefer - c.FacingDeg) % 180.0 + 180.0) % 180.0;
                    if (rel < 45) prefer += 45 - rel;
                    else if (rel > 135) prefer -= rel - 135;
                }
                // The crown stays over the island (a shade tree's lobes reach 2.4 m from the trunk, a conifer 1.4 m): its
                // lowest leaves hang below the 4.5 m overhead clearance.
                double rt = Math.Min(r, edge - ((i & 1) == 0 ? 1.4 : 2.4));
                double x, z, deg;
                if (rt < 1.0 || !c.FindSpot(prefer, rt, 0.5, 1.2, inner, 6, 0.4 * 360.0 / count, out x, out z, out deg))
                {
                    c.Skip();
                    continue;
                }
                c.Take(OrnamentFootprint.Disc(FootprintKind.Tree, x, z, 0.5));
                float y = c.TopY(x, z);
                int v0 = m.VertexCount, i0 = m.IndexCount;
                if ((i & 1) == 0) Conifer(c, x, y, z, 6 + 2 * OrnamentSeed.Unit(c.Seed, 40 + i), c.Seed + (uint)i);
                else ShadeTree(c, x, y, z, 5.5 + 2 * OrnamentSeed.Unit(c.Seed, 40 + i), c.Seed + (uint)i);
                c.Ao(v0, i0, y, 0.3f);
                if (c.Stats != null) c.Stats.Trees++;
            }
        }

        /// <summary>A columnar evergreen (Ashoka / cypress idiom common on Kathmandu islands).</summary>
        public static void Conifer(OrnCtx c, double x, double y, double z, double h, uint seed)
        {
            MeshData m = c.M;
            if (c.Lod >= 1)
            {
                Profile2 lp = c.Q.Clear(false);
                double lr = 0.17 * h;
                lp.Add(0, 0, true).Add(lr, 0.2 * h).Add(0.85 * lr, 0.55 * h).Add(0.3 * lr, 0.9 * h).Add(0, h, true);
                Shapes.Lathe(m, Affine3.Translation(x, y + 0.3, z), OrnamentKit.B(OrnamentPalette.Conifer, MaterialChannel.Foliage), lp, c.Lod == 1 ? 6 : 4, c.L);
                return;
            }
            Shapes.Cylinder(m, Affine3.Translation(x, y, z), OrnamentKit.B(OrnamentPalette.Bark, MaterialChannel.Bark), 0.12, 0.6, 6, 0, 0, false, true, c.L);
            Profile2 p = c.Q.Clear(false);
            double r = 0.17 * h;
            p.Add(0, 0, true).Add(0.75 * r, 0.02 * h).Add(r, 0.2 * h).Add(0.9 * r, 0.5 * h).Add(0.55 * r, 0.8 * h).Add(0.15 * r, 0.97 * h)
             .Add(0, h, true);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Shapes.Lathe(m, Affine3.Translation(x, y + 0.4, z), OrnamentKit.B(OrnamentPalette.Conifer, MaterialChannel.Foliage), p, 9, c.L);
            if (c.Lod < 2)
            {
                ShapeNoise.Displace(m, v0, m.VertexCount - v0, 0.07 * r, 1.8, seed, 2);
                ShapeNoise.RecomputeNormals(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0);
            }
            ShapeColor.VerticalShade(m, v0, m.VertexCount - v0, y, y + h, 0.75f, 1.15f);
        }

        /// <summary>A round-crowned shade tree: trunk, two branches and three canopy lobes.</summary>
        public static void ShadeTree(OrnCtx c, double x, double y, double z, double h, uint seed)
        {
            MeshData m = c.M;
            ShapeBrush bark = OrnamentKit.B(OrnamentPalette.Bark, MaterialChannel.Bark);
            if (c.Lod >= 1)
            {
                Shapes.Cylinder(m, Affine3.Translation(x, y, z), bark, 0.18, 0.6 * h, 4, 0, 0, false, false, c.L);
                Shapes.Sphere(m, Affine3.Translation(x, y + 0.72 * h, z) * Affine3.Scaling(1, 0.78, 1), OrnamentKit.B(OrnamentPalette.TreeCanopy, MaterialChannel.Foliage), 0.3 * h, c.Lod == 1 ? 8 : 5, c.L);
                return;
            }
            Shapes.Frustum(m, Affine3.Translation(x, y, z), bark, 0.2, 0.13, 0.55 * h, 8, 0, 0, false, false, c.L);
            if (c.Lod < 2)
            {
                Shapes.Bar(m, Affine3.Identity, bark, x, y + 0.45 * h, z, x + 0.12 * h, y + 0.68 * h, z + 0.05 * h, 0.07, 5, c.L);
                Shapes.Bar(m, Affine3.Identity, bark, x, y + 0.45 * h, z, x - 0.1 * h, y + 0.7 * h, z - 0.06 * h, 0.07, 5, c.L);
            }
            double cr = 0.3 * h;
            OrnamentKit.Clump(c, x, y + 0.72 * h, z, cr, 0.75 * cr, cr, OrnamentPalette.TreeCanopy, MaterialChannel.Foliage, 10, 0.18, seed);
            if (c.Lod < 2)
            {
                OrnamentKit.Clump(c, x + 0.42 * cr, y + 0.62 * h, z + 0.2 * cr, 0.65 * cr, 0.55 * cr, 0.65 * cr, OrnamentPalette.LeafGreen, MaterialChannel.Foliage, 8, 0.2, seed + 1);
                OrnamentKit.Clump(c, x - 0.45 * cr, y + 0.64 * h, z - 0.15 * cr, 0.6 * cr, 0.5 * cr, 0.6 * cr, OrnamentPalette.TreeCanopy, MaterialChannel.Foliage, 8, 0.2, seed + 2);
            }
        }

        /// <summary>A small palm: a gently curved trunk and drooping fronds.</summary>
        public static void Palm(OrnCtx c, double x, double y, double z, double h, uint seed)
        {
            MeshData m = c.M;
            Path3 p = c.Path2.Clear();
            double lean = 0.12 * h;
            double la = OrnamentSeed.Unit(seed, 1) * 2 * Math.PI;
            for (int k = 0; k <= 4; k++)
            {
                double t = k / 4.0;
                p.Add(x + lean * t * t * Math.Cos(la), y + h * t, z + lean * t * t * Math.Sin(la));
            }
            Shapes.Tube(m, Affine3.Identity, OrnamentKit.B(OrnamentPalette.Bark, MaterialChannel.Bark), p, 0.13, 6, true, false, c.L, 0.09);
            double tx = p.X[4], ty = p.Y[4], tz = p.Z[4];
            int fronds = c.Lod == 0 ? 7 : 5;
            Profile2 sec = c.Q.Clear(true).Add(-0.22, 0).Add(0, 0.04).Add(0.22, 0).Add(0, -0.03);
            for (int f = 0; f < fronds; f++)
            {
                double a = 2 * Math.PI * f / fronds + la;
                Path3 fp = c.Path2.Clear();
                double len = 1.5 + 0.3 * OrnamentSeed.Unit(seed, f + 3);
                for (int k = 0; k <= 4; k++)
                {
                    double t = k / 4.0;
                    fp.Add(tx + len * t * Math.Cos(a), ty + 0.35 * len * t - 0.75 * len * t * t, tz + len * t * Math.Sin(a));
                }
                Shapes.Sweep(m, Affine3.Identity, OrnamentKit.B(OrnamentPalette.Palm, MaterialChannel.Foliage), fp, sec, false, true,
                             SweepFrames.ParallelTransport, 1, 0.15);
            }
        }

        // ------------------------------------------------------------------ paths, railing, lamps, sign, police

        /// <summary>Stone paths from the kerb (at the facing bearing and every 360/count degrees from it) straight towards
        /// the centrepiece, ending at its footprint (radial for a centred one, converging on an offset mandala).</summary>
        public static void Paths(OrnCtx c, int count, double width)
        {
            if (count <= 0 || c.Lod >= 2) return;
            MeshData m = c.M;
            RoundaboutSite s = c.Site;
            double ri = InnerRadius(s);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            for (int p = 0; p < count; p++)
            {
                double ang = (c.FacingDeg + 360.0 * p / count) * Math.PI / 180.0;
                double ex = s.X + (ri + 0.05) * Math.Sin(ang), ez = s.Z + (ri + 0.05) * Math.Cos(ang);
                double dx = c.CX - ex, dz = c.CZ - ez, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1) continue;
                dx /= len;
                dz /= len;
                // Walk in from the kerb until the centrepiece's footprint (or the centre).
                double run = 0;
                while (run + 0.25 < len && c.CentreClearance(ex + (run + 0.25) * dx, ez + (run + 0.25) * dz) > 0.1) run += 0.25;
                if (run < 1) continue;
                double px = dz, pz = -dx;
                int steps = Math.Max(2, (int)(run / 1.5));
                int vb = m.VertexCount;
                for (int k = 0; k <= steps; k++)
                {
                    double t = run * (1.0 - (double)k / steps);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x = ex + t * dx + side * 0.5 * width * px, z = ez + t * dz + side * 0.5 * width * pz;
                        m.AddVertex((float)x, c.TopY(x, z) + 0.025f, (float)z, 0, 1, 0, OrnamentPalette.PathStone, (float)MaterialChannel.Flagstone, 1f);
                    }
                }
                for (int k = 0; k < steps; k++)
                {
                    int a = vb + 2 * k;
                    OrnamentKit.Tri(m, a, a + 1, a + 3);
                    OrnamentKit.Tri(m, a, a + 3, a + 2);
                }
            }
            ShapeNoise.RecomputeNormals(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0, false);
        }

        /// <summary>The island railing just inside the kerb top.</summary>
        public static void Railing(OrnCtx c)
        {
            RailingStyle style = c.Design.Railing;
            if (style == RailingStyle.None || c.Lod >= 2) return;
            MeshData m = c.M;
            RoundaboutSite s = c.Site;
            double inset = Math.Max(0, s.ApronM) + KerbTopM + 0.12;
            double perim = Outline(c, 8, inset);
            double spacing = style == RailingStyle.TealPosts ? 4.5 : style == RailingStyle.BlackIron ? 2.8 : 2.6;
            if (s.RadiusM > 14) spacing *= 1.4;
            if (c.Lod == 1) spacing *= 4;
            int n = Math.Max(8, (int)Math.Round(perim / spacing));
            if (n > 400) n = 400;
            Outline(c, n, inset);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double postH = style == RailingStyle.TealPosts ? 1.55 : style == RailingStyle.BlackIron ? 1.15 : 0.8;
            uint postCol = style == RailingStyle.TealPosts ? OrnamentPalette.TealPost : style == RailingStyle.BlackIron ? OrnamentPalette.IronBlack : OrnamentPalette.PaintWhite;
            MaterialChannel ch = style == RailingStyle.BlackIron ? MaterialChannel.Metal : MaterialChannel.Paint;
            ShapeBrush pb = OrnamentKit.B(postCol, ch);
            float baseLift = 0f;
            if (style == RailingStyle.BlackIron)
            {
                // Low cream plinth wall under the iron railing.
                Path3 w = c.Path.Clear();
                for (int k = 0; k < n; k++) w.Add(c.Xs[k], c.TopY(c.Xs[k], c.Zs[k]) - 0.05, c.Zs[k]);
                Profile2 sec = c.Q.Clear(true).Add(-0.14, 0, true).Add(0.14, 0, true).Add(0.12, 0.42, true).Add(-0.12, 0.42, true);
                Shapes.Sweep(m, Affine3.Identity, OrnamentKit.B(OrnamentPalette.WallCream, MaterialChannel.Plaster), w, sec, true, true, SweepFrames.Upright);
                baseLift = 0.37f;
            }
            for (int k = 0; k < n; k++)
            {
                double x = c.Xs[k], z = c.Zs[k];
                float y = c.TopY(x, z) + baseLift;
                Affine3 f = Affine3.Translation(x, y, z);
                if (style == RailingStyle.TealPosts)
                {
                    OrnamentKit.Box(m, f * Affine3.Translation(0, 0.5 * postH, 0), pb, 0.16, postH, 0.16);
                    Shapes.Cone(m, f * Affine3.Translation(0, postH, 0) * Affine3.Yaw(45), pb, 0.13, 0.22, 4, 0, 0, false, c.L);
                }
                else
                {
                    Shapes.Cylinder(m, f, pb, style == RailingStyle.BlackIron ? 0.035 : 0.03, postH, 4, 0, 0, false, false, c.L);
                    if (style == RailingStyle.BlackIron) Shapes.Cone(m, f * Affine3.Translation(0, postH, 0), pb, 0.05, 0.12, 4, 0, 0, false, c.L);
                    else Shapes.Sphere(m, f * Affine3.Translation(0, postH + 0.03, 0), pb, 0.05, 4, c.L);
                }
                if (c.Stats != null) c.Stats.RailingPosts++;
            }
            // Rails: closed loops.
            double[] heights = style == RailingStyle.TealPosts ? s_tealRails : style == RailingStyle.BlackIron ? s_ironRails : s_whiteRails;
            Profile2 rs = c.Q.Clear(true);
            rs.SetRect(style == RailingStyle.TealPosts ? 0.07 : 0.04, style == RailingStyle.TealPosts ? 0.05 : 0.035);
            foreach (double hh in heights)
            {
                if (c.Lod == 1 && hh < 0.9) continue;
                Path3 rp = c.Path.Clear();
                for (int k = 0; k < n; k++) rp.Add(c.Xs[k], c.TopY(c.Xs[k], c.Zs[k]) + baseLift + hh * postH, c.Zs[k]);
                Shapes.Sweep(m, Affine3.Identity, pb, rp, rs, true, true, SweepFrames.Upright);
            }
            if (c.Lod == 0)
            {
                if (style == RailingStyle.WhiteArches && c.Design.IsHero)
                {
                    // Arched bays: a rail rising in an arch between posts.
                    Path3 ap = c.Path.Clear();
                    const int per = 2;
                    for (int k = 0; k < n; k++)
                    {
                        int j = (k + 1) % n;
                        for (int q = 0; q < per; q++)
                        {
                            double t = (double)q / per;
                            double x = c.Xs[k] + (c.Xs[j] - c.Xs[k]) * t, z = c.Zs[k] + (c.Zs[j] - c.Zs[k]) * t;
                            ap.Add(x, c.TopY(x, z) + postH * (0.45 + 0.4 * Math.Sin(Math.PI * t)), z);
                        }
                    }
                    Profile2 tsec = c.Q.Clear(true).SetCircle(0.018, 3);
                    Shapes.Sweep(m, Affine3.Identity, pb, ap, tsec, true, true, SweepFrames.ParallelTransport);
                }
                else if (style == RailingStyle.BlackIron)
                {
                    // Pickets between the posts.
                    for (int k = 0; k < n; k++)
                    {
                        int j = (k + 1) % n;
                        int pk = c.Design.IsHero ? 5 : 1;
                        for (int q = 1; q < pk; q++)
                        {
                            double t = (double)q / pk;
                            double x = c.Xs[k] + (c.Xs[j] - c.Xs[k]) * t, z = c.Zs[k] + (c.Zs[j] - c.Zs[k]) * t;
                            float y = c.TopY(x, z) + baseLift;
                            Shapes.Bar(m, Affine3.Identity, pb, x, y, z, x, y + 0.9 * postH, z, 0.014, 3, c.L);
                        }
                    }
                }
            }
            c.Ao(v0, i0, c.CentreTopY - 0.3, 0.3f);
        }

        private static readonly double[] s_tealRails = { 0.35, 0.78 };
        private static readonly double[] s_ironRails = { 0.08, 0.92 };
        private static readonly double[] s_whiteRails = { 1.0 };

        /// <summary>Solar street lights evenly round the island near the kerb, arms pointing out over the ring road
        /// (above the 4.5 m overhead clearance); each moves along the ring to a free spot or is left out.</summary>
        public static void Lamps(OrnCtx c, int count, double height)
        {
            if (count <= 0 || c.Lod >= 3) return;
            RoundaboutSite s = c.Site;
            double inner = InnerRadius(s);
            double r = inner - 0.7;
            if (r < 1.5) r = Math.Max(0.6, inner * 0.6);
            for (int i = 0; i < count; i++)
            {
                double x, z, deg;
                if (!c.FindSpot(c.FacingDeg + 360.0 * (i + 0.5) / count, r, 0.3, 0.3, inner, 5, 0.45 * 360.0 / count, out x, out z, out deg))
                {
                    c.Skip();
                    continue;
                }
                c.Take(OrnamentFootprint.Disc(FootprintKind.Lamp, x, z, 0.3));
                OrnamentKit.SolarLamp(c, x, z, deg, height);
            }
        }

        /// <summary>Plan radius of a police post: the drum on its step, the compact drum without the step, or the
        /// podium under its umbrella (the canopy at 2 m reaches 1.15 m).</summary>
        public static double PostRadius(PoliceStyle style, bool compact)
        {
            return style == PoliceStyle.Umbrella ? 1.15 : compact ? 0.74 : 0.95;
        }

        /// <summary>
        /// The traffic police post on the island near the kerb, a little off the main arm so it does not hide the
        /// centrepiece, at the nearest bearing where it stands clear of everything already placed (statue platform,
        /// basin, mandala) and of the road corridors; the compact drum without its step where the full one does not
        /// fit. Left out (counted in <see cref="OrnamentStats.Skipped"/>) when the island has no room at all.
        /// </summary>
        public static void Police(OrnCtx c, PoliceStyle style)
        {
            if (style == PoliceStyle.None || c.Lod >= 3) return;
            RoundaboutSite s = c.Site;
            double inner = InnerRadius(s);
            double prefer = s.MainArmDeg + (inner > 3 ? 55 : 0);
            for (int pass = 0; pass < 2; pass++)
            {
                bool compact = pass == 1;
                if (compact && style == PoliceStyle.Umbrella) break;
                double pr = PostRadius(style, compact);
                double r = Math.Max(0, inner - pr - 0.05);
                double x, z, deg;
                if (!c.FindSpot(prefer, r, pr, 0.15, inner, 10, 180, out x, out z, out deg)) continue;
                c.Take(OrnamentFootprint.Disc(FootprintKind.PolicePost, x, z, pr));
                PolicePost(c, x, c.TopY(x, z), z, deg, style, compact);
                return;
            }
            c.Skip();
        }

        /// <summary>A traffic police post at (x, y, z) facing <paramref name="deg"/> (<paramref name="compact"/>: the
        /// drum without its step, for tight islands).</summary>
        public static void PolicePost(OrnCtx c, double x, double y, double z, double deg, PoliceStyle style, bool compact = false)
        {
            MeshData m = c.M;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 f = Affine3.Translation(x, y, z) * Affine3.Yaw(deg);
            ShapeBrush white = OrnamentKit.B(OrnamentPalette.PodiumWhite, MaterialChannel.Plaster);
            ShapeBrush blue = OrnamentKit.B(OrnamentPalette.PoliceBlue, MaterialChannel.Paint);
            ShapeBrush steel = OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal);
            if (c.Lod >= 2)
            {
                Shapes.Cylinder(m, f, white, 0.75, style == PoliceStyle.Umbrella ? 0.4 : 1.03, 6, 0, 0, false, true, c.L);
                if (style == PoliceStyle.Umbrella)
                {
                    Shapes.Cone(m, f * Affine3.Translation(0, 2.0, 0), OrnamentKit.B(OrnamentPalette.UmbrellaRed, MaterialChannel.Fabric), 1.15, 0.45, 6, 0, 0, false, c.L);
                    return;
                }
                OrnamentKit.Box(m, f * Affine3.Translation(0, 2.2, 0), steel, 0.1, 2.4, 0.1);
                OrnamentKit.Box(m, f * Affine3.Translation(0, 3.3, 0), blue, 1.25, 0.9, 0.3);
                return;
            }
            if (style == PoliceStyle.Umbrella)
            {
                Shapes.Cylinder(m, f, white, 0.7, 0.38, 16, 0.05, 2, false, true, c.L);
                Shapes.Cylinder(m, f * Affine3.Translation(0, 0.22, 0), blue, 0.71, 0.08, 16, 0, 0, false, false, c.L);
                Shapes.Cylinder(m, f, steel, 0.035, 2.45, 6, 0, 0, false, true, c.L);
                // Umbrella: eight panels alternating red and white.
                for (int k = 0; k < 8; k++)
                {
                    uint col = (k & 1) == 0 ? OrnamentPalette.UmbrellaRed : OrnamentPalette.FlagWhite;
                    Shapes.Lathe(m, f * Affine3.Translation(0, 2.0, 0), OrnamentKit.B(col, MaterialChannel.Fabric),
                                 c.Q.Clear(false).Add(1.15, 0, true).Add(0.6, 0.32).Add(0, 0.45, true), 16, c.L, 45 * k, 45, false);
                }
                return;
            }
            // Drum: step, white drum with a blue band, top rail, central post with the box sign, roof and solar panel.
            int pr = c.Lod == 0 ? 14 : 10;
            double rim = c.Lod == 0 ? 0.03 : 0;
            if (!compact) Shapes.Cylinder(m, f, white, 0.95, 0.18, pr, rim, 1, false, true, c.L);
            Shapes.Cylinder(m, f * Affine3.Translation(0, compact ? 0 : 0.18, 0), white, 0.72, compact ? 1.03 : 0.85, pr, rim, 1, false, true, c.L);
            Shapes.Cylinder(m, f * Affine3.Translation(0, 0.68, 0), blue, 0.735, 0.14, pr, 0, 0, false, false, c.L);
            if (c.Lod == 0)
            {
                Shapes.Torus(m, f * Affine3.Translation(0, 1.85, 0), steel, 0.68, 0.025, 14, 3, c.L);
                for (int k = 0; k < 5; k++)
                {
                    double a = Math.PI * 2 * k / 5;
                    Shapes.Bar(m, f, steel, 0.68 * Math.Sin(a), 1.03, 0.68 * Math.Cos(a), 0.68 * Math.Sin(a), 1.85, 0.68 * Math.Cos(a), 0.02, 4, c.L);
                }
            }
            Shapes.Cylinder(m, f * Affine3.Translation(0, 1.0, 0), steel, 0.06, 2.6, 8, 0, 0, false, true, c.L);
            if (c.Lod == 0) Shapes.RoundedBox(m, f * Affine3.Translation(0, 3.25, 0), blue, 1.25, 0.9, 0.3, 0.05, 1, c.L);
            else OrnamentKit.Box(m, f * Affine3.Translation(0, 3.25, 0), blue, 1.25, 0.9, 0.3);
            if (c.Lod < 2)
            {
                // A white band and a maroon stripe round the box (traffic police colours, no text).
                OrnamentKit.Box(m, f * Affine3.Translation(0, 3.25, 0), white, 1.27, 0.22, 0.32);
                OrnamentKit.Box(m, f * Affine3.Translation(0, 3.62, 0), OrnamentKit.B(OrnamentPalette.PoliceSign, MaterialChannel.Paint), 1.27, 0.1, 0.32);
            }
            OrnamentKit.Box(m, f * Affine3.Translation(0, 3.76, 0), white, 1.5, 0.08, 0.6);
            OrnamentKit.Box(m, f * Affine3.Translation(0, 3.95, -0.05) * Affine3.RotationX(-0.4), OrnamentKit.B(OrnamentPalette.SolarPanel, MaterialChannel.Glass), 0.9, 0.04, 0.55);
            c.Ao(v0, i0, y);
        }

        /// <summary>A keep-left sign (blue disc, white arrow, no text) near the kerb facing the main arm, at a free
        /// spot (or left out).</summary>
        public static void KeepLeftSign(OrnCtx c)
        {
            if (c.Lod >= 1) return;
            RoundaboutSite s = c.Site;
            double inner = InnerRadius(s);
            double r = inner - 0.45;
            double x, z, deg;
            if (r < 0.3 || !c.FindSpot(s.MainArmDeg - (r > 3 ? 18 : 0), r, 0.42, 0.15, inner, 8, 64, out x, out z, out deg))
            {
                c.Skip();
                return;
            }
            c.Take(OrnamentFootprint.Disc(FootprintKind.Sign, x, z, 0.42));
            float y = c.TopY(x, z);
            MeshData m = c.M;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 f = Affine3.Translation(x, y, z) * Affine3.Yaw(deg);
            ShapeBrush steel = OrnamentKit.B(OrnamentPalette.Steel, MaterialChannel.Metal);
            Shapes.Cylinder(m, f, steel, 0.035, 1.9, 6, 0, 0, false, true, c.L);
            Affine3 disc = f * Affine3.Translation(0, 1.75, 0.05) * Affine3.RotationX(Math.PI / 2);
            Shapes.Cylinder(m, disc, OrnamentKit.B(OrnamentPalette.FlagWhite, MaterialChannel.Paint), 0.4, 0.025, 20, 0.008, 1, true, true, c.L);
            Shapes.Cylinder(m, disc * Affine3.Translation(0, 0.012, 0), OrnamentKit.B(OrnamentPalette.SignBlue, MaterialChannel.Paint), 0.36, 0.016, 20, 0, 0, false, true, c.L);
            // The arrow, pointing down to the left (keep left), on the front face.
            Affine3 af = f * Affine3.Translation(0, 1.75, 0.095) * Affine3.RotationZ(Math.PI * 0.75);
            OrnamentKit.FlatPoly(m, af, OrnamentPalette.FlagWhite, MaterialChannel.Paint, s_shaftX, s_shaftY, 4, 0.004, false);
            OrnamentKit.FlatPoly(m, af, OrnamentPalette.FlagWhite, MaterialChannel.Paint, s_headX, s_headY, 3, 0.004, false);
            c.Ao(v0, i0, y);
        }

        private static readonly double[] s_shaftX = { -0.06, 0.06, 0.06, -0.06 };
        private static readonly double[] s_shaftY = { -0.22, -0.22, 0.05, 0.05 };
        private static readonly double[] s_headX = { 0.16, 0, -0.16 };
        private static readonly double[] s_headY = { 0.04, 0.23, 0.04 };
    }
}
