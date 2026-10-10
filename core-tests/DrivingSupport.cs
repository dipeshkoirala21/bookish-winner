using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>An analytic ground for vehicle tests: a plane (height = H0 + GradeX·x + GradeZ·z) with a surface
    /// per position, optional bounds (no ground outside: a wall) and an optional east-west road along Z = RoadZ.</summary>
    internal sealed class PlaneGround : IGroundQuery, IRoadQuery
    {
        public double H0 = 1300.0, GradeX = 0.0, GradeZ = 0.0;
        public SurfaceGroup Surface = SurfaceGroup.Paved;
        public Func<double, double, SurfaceGroup> SurfaceAt;
        public Func<double, double, double> HeightAt;
        public double MinX = double.NegativeInfinity, MaxX = double.PositiveInfinity;
        public double MinZ = double.NegativeInfinity, MaxZ = double.PositiveInfinity;
        public bool HasRoad;
        public double RoadZ;
        public float RoadHalfWidth = 3f;
        public int Samples;

        public bool TrySample(double x, double z, out GroundSample s)
        {
            Samples++;
            s = default(GroundSample);
            if (x < MinX || x > MaxX || z < MinZ || z > MaxZ) return false;
            double h = HeightAt != null ? HeightAt(x, z) : H0 + GradeX * x + GradeZ * z;
            double gx = GradeX, gz = GradeZ;
            if (HeightAt != null)
            {
                const double e = 0.05;
                gx = (HeightAt(x + e, z) - HeightAt(x - e, z)) / (2 * e);
                gz = (HeightAt(x, z + e) - HeightAt(x, z - e)) / (2 * e);
            }
            double inv = 1.0 / Math.Sqrt(gx * gx + 1 + gz * gz);
            s.Nx = (float)(-gx * inv);
            s.Ny = (float)inv;
            s.Nz = (float)(-gz * inv);
            s.Height = (float)h;
            s.TerrainHeight = (float)h;
            s.Surface = SurfaceAt != null ? SurfaceAt(x, z) : Surface;
            if (HasRoad && Math.Abs(z - RoadZ) <= RoadHalfWidth + RoadSpatialIndex.OnRoadMarginM)
            {
                s.OnRoad = true;
                s.RoadClass = RoadClass.Residential;
                s.RoadSurface = Data.Surface.Asphalt;
                s.Surface = SurfaceGroup.Paved;
                s.RoadDirX = 1f;
                s.RoadDirZ = 0f;
                s.RoadOffsetM = (float)(RoadZ - z);
                s.RoadHalfWidthM = RoadHalfWidth;
                s.Height += 0.25f;
            }
            return true;
        }

        public bool TryNearestRoad(double x, double z, double maxDistM, out RoadHit hit)
        {
            hit = default(RoadHit);
            if (!HasRoad) return false;
            double d = Math.Abs(z - RoadZ);
            if (d - RoadHalfWidth > maxDistM) return false;
            hit.X = x;
            hit.Z = RoadZ;
            hit.DirX = 1f;
            hit.DirZ = 0f;
            hit.DistanceM = (float)d;
            hit.HalfWidthM = RoadHalfWidth;
            hit.EdgeDistanceM = (float)(d - RoadHalfWidth);
            hit.OnRoad = hit.EdgeDistanceM <= RoadSpatialIndex.OnRoadMarginM;
            return true;
        }
    }

    /// <summary>Synthetic tiles and the committed sample region (shared/sample-regions/kathmandu_core).</summary>
    internal static class DrivingData
    {
        public static TileData Tile(int level, int tx, int ty, Func<int, int, double> heightAt, Biome biome = Biome.UrbanDense, int n = 129)
        {
            var t = new TileData { Tile = new TileId(level, tx, ty), Flags = Ght.FlagHasDetail, HeightsN = n };
            t.HeightsQ = new ushort[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                t.HeightsQ[j * n + i] = Ght.Quantize(heightAt(i, j));
            t.BiomesN = 65;
            t.Biomes = new Biome[65 * 65];
            for (int k = 0; k < t.Biomes.Length; k++) t.Biomes[k] = biome;
            return t;
        }

        public static TileData Flat(int level, int tx, int ty, double h, Biome biome = Biome.UrbanDense)
        {
            return Tile(level, tx, ty, (i, j) => h, biome);
        }

        /// <summary>A road from local-metre points (cm-rounded), flags and width (0 = class default).</summary>
        public static RoadRecord Road(RoadClass cls, Surface surface, double widthM, RoadFlags flags, params double[] localMetres)
        {
            var pts = new int[localMetres.Length];
            for (int k = 0; k < pts.Length; k++) pts[k] = (int)Math.Round(localMetres[k] * 100.0);
            return new RoadRecord
            {
                RoadClass = cls, Surface = surface, Flags = flags, WidthCm = (ulong)Math.Round(widthM * 100.0), Points = pts,
            };
        }

        // ---- the real sample region ----

        private static Dictionary<TileId, TileData> _tiles;

        public static string SamplePath(string suffix)
        {
            return Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core" + suffix);
        }

        /// <summary>Every tile of the sample pack, decoded once per test run.</summary>
        public static Dictionary<TileId, TileData> SampleTiles()
        {
            if (_tiles != null) return _tiles;
            string p = SamplePath(".ghpk");
            if (!File.Exists(p)) Assert.Fail("sample region missing: " + p);
            var tiles = new Dictionary<TileId, TileData>();
            using (var pack = new PackReader(File.ReadAllBytes(p)))
            {
                foreach (PackEntry e in pack.Entries) tiles[e.Tile] = pack.ReadTile(e.Tile);
            }
            _tiles = tiles;
            return tiles;
        }

        /// <summary>The middle of the longest segment of Thamel Marg in its leaf tile (game metres): on the street and away
        /// from junction nodes, where a wider crossing street is the nearest W2 surface.</summary>
        public static void ThamelMarg(out double x, out double z)
        {
            double tx, tz;
            Geo.WorldFrame.LonLatToGame(85.31172094019205, 27.716693189023914, out tx, out tz);
            TileData t = SampleTiles()[TileId.At(10, tx, tz)];
            double best = -1;
            x = tx;
            z = tz;
            foreach (RoadRecord r in t.Roads)
            {
                NameRecord n = t.Name(r.NameRef);
                if (n == null || n.Default != "Thamel Marg") continue;
                int first, last;
                RoadSpatialIndex.RenderedRange(r, out first, out last);
                for (int k = first; k < last; k++)
                {
                    double dx = (r.Points[2 * k + 2] - r.Points[2 * k]) / 100.0, dz = (r.Points[2 * k + 3] - r.Points[2 * k + 1]) / 100.0;
                    double len = Math.Sqrt(dx * dx + dz * dz);
                    if (len <= best) continue;
                    best = len;
                    x = t.Tile.X0 + (r.Points[2 * k] + r.Points[2 * k + 2]) / 200.0;
                    z = t.Tile.Z0 + (r.Points[2 * k + 1] + r.Points[2 * k + 3]) / 200.0;
                }
            }
            if (best < 0) Assert.Fail("Thamel Marg not in the sample pack");
        }

        /// <summary>A ground query holding every tile of the sample pack (all levels, step 1), with the tiles' solids
        /// (building footprints, props, railings) unless <paramref name="solids"/> is false.</summary>
        public static TileGroundQuery SampleGround(bool solids = true)
        {
            var g = new TileGroundQuery { TileSolidsEnabled = solids };
            foreach (TileData t in SampleTiles().Values) g.Add(t);
            return g;
        }
    }
}
