using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The OBJ format of <see cref="ObjDump"/> and the preview dumps every package renders with
    /// tools/mesh-preview/render.py (see its README). The dump tests write only when <c>GHUMANTE_PREVIEW_DIR</c> is
    /// set; otherwise they pass without touching the disk:
    /// <list type="bullet">
    /// <item><c>scene/&lt;tile&gt;/</c>: one level-10 tile of the kathmandu_core sample as the player sees it up close
    /// (terrain, roads, road decals, areas, heroes, B0 detail buildings within <c>GHUMANTE_PREVIEW_RADIUS</c>
    /// (default 150 m) of a street-level eye point and B1 buildings beyond, as separate OBJs, with the eye point
    /// stored in each file and a <c>render.sh</c>). <c>GHUMANTE_PREVIEW_TILE=tx,ty</c> picks another level-10
    /// tile.</item>
    /// <item><c>vehicles/</c>, <c>characters/</c>, <c>heroes/</c>: every catalogue vehicle (LOD0 with wheels), sample
    /// characters and every stage-one hero (LOD0).</item>
    /// </list>
    /// </summary>
    public class PreviewDumps
    {
        private static MeshData Triangle(bool uv)
        {
            var m = new MeshData();
            uint c = MeshColor.FromHex(0xFF8000);
            if (uv)
            {
                m.AddVertex(0f, 0f, 0f, 0f, 0f, -1f, c, (float)MaterialChannel.Brick, 0.5f);
                m.AddVertex(0f, 1f, 0f, 0f, 0f, -1f, c, (float)MaterialChannel.Brick, 1f);
                m.AddVertex(1f, 0f, 0f, 0f, 0f, -1f, c, (float)MaterialChannel.Brick, 1f);
            }
            else
            {
                m.AddVertex(0f, 0f, 2f, 0f, 1f, 0f, c);
                m.AddVertex(0f, 0f, 3f, 0f, 1f, 0f, c);
                m.AddVertex(1f, 0f, 2f, 0f, 1f, 0f, c);
            }
            m.AddTriangle(0, 1, 2);
            return m;
        }

        [Test]
        public void ObjFlipsZAndWindingAndWritesUvs()
        {
            string obj = ObjDump.Format(new[] { "meshpreview: views=1" }, new ObjPart("wall", Triangle(true)), new ObjPart("floor", Triangle(false)));
            string[] lines = obj.Split('\n');
            Assert.That(obj, Does.Contain("# meshpreview: views=1\n"));
            Assert.That(obj, Does.Contain("\nv 0 0 -2 1 0.502 0\n"), "z negated, colour 0..1");
            Assert.That(obj, Does.Contain("\nvn 0 0 1\n"), "normal z negated");
            Assert.That(obj, Does.Contain("\nvt 2 0.5\n"), "u = channel, v = AO");
            Assert.That(obj, Does.Contain("\nvt 0 1\n"), "a part without UV0 reads as Plain, open");
            Assert.That(obj, Does.Contain("\no wall\nf 1/1/1 3/3/3 2/2/2\n"), "winding reversed");
            Assert.That(obj, Does.Contain("\no floor\nf 4/4/4 6/6/6 5/5/5\n"), "second part offset");
            int v = 0, vn = 0, vt = 0, f = 0;
            foreach (string l in lines)
            {
                if (l.StartsWith("v ", StringComparison.Ordinal)) v++;
                else if (l.StartsWith("vn ", StringComparison.Ordinal)) vn++;
                else if (l.StartsWith("vt ", StringComparison.Ordinal)) vt++;
                else if (l.StartsWith("f ", StringComparison.Ordinal)) f++;
            }
            Assert.That(new[] { v, vn, vt, f }, Is.EqualTo(new[] { 6, 6, 6, 2 }));

            // OBJ front faces are counter-clockwise: the reversed triangle's cross product follows the flipped normal.
            string plain = ObjDump.Format(null, new ObjPart("floor", Triangle(false)));
            Assert.That(plain, Does.Not.Contain("vt "));
            Assert.That(plain, Does.Contain("f 1//1 3//3 2//2"));
        }

        [Test]
        public void WritesNothingUnlessThePreviewDirIsSet()
        {
            if (ObjDump.Enabled) Assert.Pass(ObjDump.EnvVar + " is set: dumps are on");
            Assert.That(ObjDump.Write(Triangle(true), "never.obj"), Is.Null);
        }

        [Test]
        public void AppendMovesAndKeepsUvs()
        {
            var dst = new MeshData();
            ObjDump.Append(dst, Triangle(false), 1f, 2f, 3f);
            ObjDump.Append(dst, Triangle(true), 0f, 0f, 0f);
            Assert.That(dst.VertexCount, Is.EqualTo(6));
            Assert.That(dst.TriangleCount, Is.EqualTo(2));
            Assert.That(dst.Positions[2], Is.EqualTo(5f));
            Assert.That(dst.HasUv0, Is.True);
            Assert.That(dst.Uv0[3 * 2], Is.EqualTo((float)MaterialChannel.Brick));
            Assert.That(dst.Indices[3], Is.EqualTo(3));
        }

        /// <summary>One level-10 tile as the player sees it near the camera (the meshers TileBuild runs), one OBJ per
        /// layer plus a street-level eye point (directive in each file) and render.sh with the exact commands.</summary>
        [Test]
        public void DumpSampleTileScene()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the scene");
            TileId id = PickTile();
            TileData src = StreamingSampleRegion.Tile(id);
            Assert.That(src, Is.Not.Null, "tile " + id + " is not in the sample pack");
            Assert.That(src.HasDetail, Is.True, "tile " + id + " has no detail");
            string name = "scene/tile_" + id.Tx + "_" + id.Ty;

            var terrain = new MeshData();
            TerrainMesher.Build(src, id, new TerrainOptions { Step = 1 }, terrain);
            TileHeightSampler sampler = TileHeightSampler.ForArea(src, id, 1);
            var roadOptions = new RoadOptions();
            var roads = new MeshData();
            RoadMesher.Build(src, sampler, roadOptions, roads);
            var decals = new MeshData();
            MarkingMesher.Build(src, sampler, roadOptions, decals);
            float ex, ey, ez, lx, ly, lz;
            StreetEye(src, sampler, out ex, out ey, out ez, out lx, out ly, out lz);
            // Near the eye the B0 detail band (what the game streams around the camera), the B1 band beyond.
            float near = NearRadius();
            float nx0 = Math.Min(ex, lx) - near, nz0 = Math.Min(ez, lz) - near, nx1 = Math.Max(ex, lx) + near, nz1 = Math.Max(ez, lz) + near;
            var band = new MeshData();
            BuildingMesher.Build(src, sampler, new BuildingOptions { Band = BuildingBand.B0KitLite }, band);
            MeshData buildings = ObjDump.Crop(band, nx0, nz0, nx1, nz1, true);
            band.Clear();
            BuildingMesher.Build(src, sampler, new BuildingOptions { Band = BuildingBand.B1Styled }, band);
            MeshData far = ObjDump.Crop(band, nx0, nz0, nx1, nz1, false);
            var areas = new MeshData();
            AreaMesher.Build(src, sampler, new AreaOptions(), areas);
            var heroes = new MeshData();
            foreach (HeritageRecord r in Curated().Heritage)
            {
                double hx, hz;
                int hb;
                if (!HeroBuilder.TryLocate(r, HeroBuilder.Merge(r), src, out hx, out hz, out hb)) continue;
                HeroBuilder.Build(r, src, 0, heroes, null);
            }

            CultureInfo c = CultureInfo.InvariantCulture;
            // Directive points in OBJ coordinates (Unity z negated).
            string eye = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez);
            string look = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", lx, ly, -lz);
            var directives = new[]
            {
                "tile " + id + " of kathmandu_core; positions are metres from the tile's south-west corner, absolute height",
                "street-level eye (Unity x,y,z): " + string.Format(c, "{0:0.##},{1:0.##},{2:0.##} looking at {3:0.##},{4:0.##},{5:0.##}", ex, ey, ez, lx, ly, lz),
                "meshpreview: grid=0 sun=160,62 eye=" + eye + " look=" + look,
            };
            var layers = new (string, MeshData)[]
            {
                ("terrain", terrain), ("roads", roads), ("decals", decals), ("areas", areas), ("buildings", buildings), ("buildings_b1", far), ("heroes", heroes),
            };
            var files = new List<string>();
            foreach (var (layer, mesh) in layers)
            {
                if (mesh.TriangleCount == 0) continue;
                ObjDump.Write(mesh, name + "/" + layer + ".obj", directives);
                files.Add(layer + ".obj");
            }
            string dir = Path.Combine(ObjDump.Dir, name);
            var sh = new StringBuilder();
            sh.Append("#!/bin/sh\n# Render this scene (tools/mesh-preview/README.md). Run from the repository root.\n");
            sh.Append("D=").Append(dir).Append('\n');
            string all = string.Join(" ", files.ConvertAll(f => "$D/" + f));
            sh.Append("# street level, from the eye point stored in the OBJ files:\n");
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(all).Append(" -o $D/street.png --size 1200 --height 700 --fog 350\n");
            sh.Append("# overview of the whole tile:\n");
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(all).Append(" -o $D/overview.png --views bird --no-eye --size 1200\n");
            File.WriteAllText(Path.Combine(dir, "render.sh"), sh.ToString());
            TestContext.WriteLine("scene " + id + ": terrain " + terrain.TriangleCount + ", roads " + roads.TriangleCount + ", decals " + decals.TriangleCount +
                                  ", buildings " + buildings.TriangleCount + " (B0 near) + " + far.TriangleCount + " (B1), areas " + areas.TriangleCount + ", heroes " + heroes.TriangleCount + " triangles -> " + dir);
        }

        /// <summary>Every catalogue vehicle at LOD0 with its wheels, sample characters and every stage-one hero.</summary>
        [Test]
        public void DumpSampleObjects()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the samples");
            var wheel = new MeshData();
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                var m = new MeshData();
                VehicleMesher.Build(v, 0, VehicleLod.Lod0, m, 1234u);
                foreach (WheelSocket s in VehicleMesher.Wheels(e))
                {
                    wheel.Clear();
                    VehicleMesher.BuildWheel(s.Radius, s.Width, VehicleLod.Lod0, wheel);
                    ObjDump.Append(m, wheel, s.X, s.Y, s.Z);
                }
                ObjDump.Write(m, "vehicles/" + Safe(e.AssetId + "_" + e.Variant) + ".obj", new[] { "meshpreview: views=4" });
            }

            var people = new (string, CharacterRecipe, HeadwearMode, int)[]
            {
                ("player_default", new CharacterRecipe { Skin = 6, Hair = 1 }, HeadwearMode.Outfit, 0),
                ("player_helmet", new CharacterRecipe { Skin = 6, Hair = 8 }, HeadwearMode.Helmet, 0),
                ("daura_topi", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 3u), HeadwearMode.Outfit, 0),
                ("sari", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u), HeadwearMode.Outfit, 0),
                ("porter", CharacterRecipe.ForPedestrian(PedArchetype.Porter, 1, 5u), HeadwearMode.Outfit, 0),
                ("monk", CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 6u), HeadwearMode.Outfit, 0),
                ("tourist", CharacterRecipe.ForPedestrian(PedArchetype.Tourist, 3, 7u), HeadwearMode.Outfit, 0),
            };
            foreach (var (n, recipe, mode, lod) in people)
            {
                var m = new MeshData();
                HumanoidMesher.Build(recipe, lod, m, null, mode);
                ObjDump.Write(m, "characters/" + n + ".obj", new[] { "meshpreview: views=4" });
            }

            CuratedDb db = Curated();
            var tiles = new List<TileData>();
            foreach (TileId t in StreamingSampleRegion.TilesAt(10)) tiles.Add(StreamingSampleRegion.Tile(t));
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                HeritageRecord rec;
                if (!db.TryGetHeritage(recipe.Id, out rec)) rec = HeroCatalog.ToRecord(recipe);
                HeroRecipe merged = HeroBuilder.Merge(rec);
                TileData tile = null;
                double x, z;
                int b;
                foreach (TileData t in tiles)
                    if (HeroBuilder.TryLocate(rec, merged, t, out x, out z, out b))
                    {
                        tile = t;
                        break;
                    }
                if (tile == null)
                {
                    double gx, gz;
                    WorldFrame.LonLatToGame(recipe.Lon, recipe.Lat, out gx, out gz);
                    double size = TileId.SizeAt(10);
                    tile = MeshingChecks.SyntheticTile(new TileId(10, (int)Math.Floor(gx / size), (int)Math.Floor(gz / size)), (px, pz) => 1330);
                }
                var m = new MeshData();
                if (HeroBuilder.Build(rec, tile, 0, m, null) > 0) ObjDump.Write(m, "heroes/" + Safe(recipe.Id) + ".obj", new[] { "meshpreview: views=4" });
            }
        }

        private static CuratedDb _db;

        private static CuratedDb Curated()
        {
            return _db ?? (_db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.curated.ghcd"))));
        }

        private static float NearRadius()
        {
            string s = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_RADIUS");
            return string.IsNullOrWhiteSpace(s) ? 150f : float.Parse(s.Trim(), CultureInfo.InvariantCulture);
        }

        private static TileId PickTile()
        {
            string s = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_TILE");
            if (!string.IsNullOrWhiteSpace(s))
            {
                string[] p = s.Split(',');
                return new TileId(10, int.Parse(p[0].Trim(), CultureInfo.InvariantCulture), int.Parse(p[1].Trim(), CultureInfo.InvariantCulture));
            }
            return StreamingSampleRegion.DensestLeaf();
        }

        /// <summary>A chase-camera eye over a street near the tile centre (the widest drivable road there): 6 m behind
        /// and 2.6 m above a point on it, looking 25 m ahead (tile-local Unity coordinates).</summary>
        private static void StreetEye(TileData t, TileHeightSampler h, out float ex, out float ey, out float ez, out float lx, out float ly, out float lz)
        {
            double size = t.Tile.Size, cx = size * 0.5, cz = size * 0.5;
            double best = double.MaxValue;
            double px = cx, pz = cz, dx = 1, dz = 0;
            foreach (RoadRecord r in t.Roads)
            {
                if (r.RoadClass >= RoadClass.Track || r.RoadClass == RoadClass.Unknown || r.PointCount < 2) continue;
                for (int i = 0; i + 1 < r.PointCount; i++)
                {
                    double ax = r.Points[2 * i] * 0.01, az = r.Points[2 * i + 1] * 0.01, bx = r.Points[2 * i + 2] * 0.01, bz = r.Points[2 * i + 3] * 0.01;
                    double sx = bx - ax, sz = bz - az, len = Math.Sqrt(sx * sx + sz * sz);
                    if (len < 30) continue;
                    double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
                    double d = Math.Sqrt((mx - cx) * (mx - cx) + (mz - cz) * (mz - cz)) - (r.RoadClass <= RoadClass.Tertiary ? 60 : 0);
                    if (d >= best) continue;
                    best = d;
                    px = ax + sx * 0.3;
                    pz = az + sz * 0.3;
                    dx = sx / len;
                    dz = sz / len;
                }
            }
            float g;
            double ox = t.Tile.X0, oz = t.Tile.Z0;
            ex = (float)(px - dx * 6);
            ez = (float)(pz - dz * 6);
            ey = (h.TryHeight(ox + ex, oz + ez, out g) ? g : 0f) + 2.6f;
            lx = (float)(px + dx * 25);
            lz = (float)(pz + dz * 25);
            ly = (h.TryHeight(ox + lx, oz + lz, out g) ? g : 0f) + 1.2f;
        }

        private static string Safe(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? char.ToLowerInvariant(ch) : '_');
            return sb.ToString();
        }
    }
}
