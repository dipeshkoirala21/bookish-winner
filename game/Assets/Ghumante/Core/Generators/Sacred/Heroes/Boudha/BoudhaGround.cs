using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// The Boudha plaza ground (boudha.md 2.3). The terrain model is a surface model (Copernicus) that includes the stupa
    /// and the ring of houses: it rises 5-8 m under the stupa and wanders 3 m round the kora, where the real plaza is one
    /// flat paved level. <see cref="KoraLevel"/> is that level (the 10th percentile of the terrain on the kora ring,
    /// r 50-60 m, so it also holds after flattening); <see cref="Apply"/> flattens a decoded tile's height grid to it
    /// within <see cref="BoudhaSite.PlazaRadiusM"/> of the T1 centroid and clamps the terrain beyond to a 1:4 cone of
    /// slopes back to the original over <see cref="BoudhaSite.PlazaBlendM"/> (leaving the drawn roads beyond the plaza on
    /// their own ground), so the terrain mesh, the sampler, the ground query and every placer agree on one flat plaza.
    /// Idempotent. The streamer calls it on every decoded tile
    /// before meshing (<see cref="HeroBuilder.PrepareGround"/> in TileBuild.Execute).
    /// </summary>
    public static class BoudhaGround
    {
        [ThreadStatic] private static float[] _samples;

        /// <summary>The steepest slope (rise over run) between the plaza level and the original terrain beyond it.</summary>
        public const double BlendSlope = 0.25;

        /// <summary>Inner and outer radius of the kora ring the level is read from (m from the T1 centroid).</summary>
        public const double RingInnerM = 50.0, RingOuterM = 60.0;

        /// <summary>The kora level (absolute metres, on the height grid's 0.15 m quantum) round tile-local (ox, oz).</summary>
        public static double KoraLevel(TileData t, double ox, double oz)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            int i0 = Math.Max(0, (int)Math.Floor((ox - RingOuterM) / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling((ox + RingOuterM) / cell));
            int j0 = Math.Max(0, (int)Math.Floor((oz - RingOuterM) / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling((oz + RingOuterM) / cell));
            int cap = (i1 - i0 + 1) * (j1 - j0 + 1);
            if (cap <= 0) return Ght.Dequantize(t.HeightsQ[0]);
            if (_samples == null || _samples.Length < cap) _samples = new float[Math.Max(cap, 1024)];
            int c = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double dx = i * cell - ox, dz = j * cell - oz, r = Math.Sqrt(dx * dx + dz * dz);
                    if (r < RingInnerM || r > RingOuterM) continue;
                    _samples[c++] = Ght.Dequantize(t.HeightsQ[j * n + i]);
                }
            }
            if (c == 0)
            {
                int ci = Math.Max(0, Math.Min(n - 1, (int)Math.Round(ox / cell))), cj = Math.Max(0, Math.Min(n - 1, (int)Math.Round(oz / cell)));
                return Ght.Dequantize(t.HeightsQ[cj * n + ci]);
            }
            Array.Sort(_samples, 0, c);
            return _samples[Math.Min(c - 1, (int)(0.1 * c))];
        }

        /// <summary>The T1 centroid in a tile (tile-local metres), from its footprint or the curated lon/lat; false when the
        /// plaza does not reach the tile.</summary>
        public static bool TryCentre(TileData t, out double ox, out double oz)
        {
            ox = oz = 0;
            if (t == null) return false;
            ulong t1 = 2 * BoudhaSite.T1Way;
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                if (t.Buildings[i].OsmRef != t1) continue;
                BoudhaSite.Centroid(t.Buildings[i].Rings[0], out ox, out oz);
                return true;
            }
            double gx, gz;
            WorldFrame.LonLatToGame(BoudhaSite.Lon, BoudhaSite.Lat, out gx, out gz);
            ox = gx - t.Tile.X0;
            oz = gz - t.Tile.Z0;
            double reach = BoudhaSite.PlazaRadiusM + BoudhaSite.PlazaBlendM, size = t.Tile.Size;
            return ox > -reach && oz > -reach && ox < size + reach && oz < size + reach;
        }

        /// <summary>Flatten the plaza in a decoded tile's height grid (see the class summary); returns the samples changed.
        /// A tile the plaza does not reach is left alone.</summary>
        public static int Apply(TileData t)
        {
            if (t == null || t.HeightsQ == null || t.HeightsN < 2) return 0;
            double ox, oz;
            if (!TryCentre(t, out ox, out oz)) return 0;
            int n = t.HeightsN;
            double size = t.Tile.Size, cell = size / (n - 1);
            // Only the tile (at any level) that holds the site: the plaza lies 176 m or more from every edge of its
            // level-10 tile, so no neighbour is reached by the blend.
            if (ox < 0 || oz < 0 || ox >= size || oz >= size) return 0;
            double r0 = BoudhaSite.PlazaRadiusM, r1 = r0 + BoudhaSite.PlazaBlendM;
            int i0 = Math.Max(0, (int)Math.Floor((ox - r1) / cell)), i1 = Math.Min(n - 1, (int)Math.Ceiling((ox + r1) / cell));
            int j0 = Math.Max(0, (int)Math.Floor((oz - r1) / cell)), j1 = Math.Min(n - 1, (int)Math.Ceiling((oz + r1) / cell));
            if (i0 > i1 || j0 > j1) return 0;
            double level = KoraLevel(t, ox, oz);
            ushort lq = Ght.Quantize(level);
            // Beyond the plaza the drawn roads keep their own ground: Boudha Main Road runs through the blend south of the
            // main gate, and a cone across a carriageway would lift the ground under one edge and leave the other on the
            // original terrain (the ribbon then floats at that edge). Cells within a cell and a metre of a drawn
            // carriageway are left as they are (the drawn-road index works on the roads alone, not the heights).
            SacredDrawnRoads roads = t.Roads.Count > 0 ? SacredDrawnRoads.For(t) : null;
            double keep = Math.Min(SacredDrawnRoads.FarM - 0.5, Math.Max(2.0, cell + 1.0));
            int changed = 0;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double dx = i * cell - ox, dz = j * cell - oz, r = Math.Sqrt(dx * dx + dz * dz);
                    if (r >= r1) continue;
                    int k = j * n + i;
                    ushort q = t.HeightsQ[k];
                    ushort nq;
                    if (r <= r0)
                    {
                        nq = lq;
                    }
                    else
                    {
                        if (roads != null && roads.SignedDistance(i * cell, j * cell) < keep) continue;
                        // A cone of slopes (1:4) round the plaza: the terrain is clamped to within (r − r0) / 4 of the level,
                        // so a second pass changes nothing.
                        double band = (r - r0) * BlendSlope, h = Ght.Dequantize(q), l = Ght.Dequantize(lq);
                        nq = Ght.Quantize(Math.Max(l - band, Math.Min(l + band, h)));
                    }
                    if (nq == q) continue;
                    t.HeightsQ[k] = nq;
                    changed++;
                }
            }
            return changed;
        }
    }
}
