using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using Ghumante.Core.Save;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class TileReaderTests
    {
        internal static void AssertTileMatches(TileData td, JsonElement x)
        {
            Assert.That(td.Tile, Is.EqualTo(new TileId(x.GetProperty("level").GetInt32(), x.GetProperty("tx").GetInt32(), x.GetProperty("ty").GetInt32())));
            Assert.That(td.Tile.Key, Is.EqualTo(x.GetProperty("key").GetUInt64()));
            Assert.That(td.Version, Is.EqualTo(x.GetProperty("version").GetInt32()));
            Assert.That(td.Flags, Is.EqualTo(x.GetProperty("flags").GetInt32()));
            Assert.That(td.HasDetail, Is.EqualTo(x.GetProperty("has_detail").GetBoolean()));
            Assert.That(td.DataVersion, Is.EqualTo(x.GetProperty("data_version").GetUInt32()));
            Assert.That(td.PayloadCrc32, Is.EqualTo(x.GetProperty("payload_crc32").GetUInt32()));

            var chunks = x.GetProperty("chunks").EnumerateArray().ToArray();
            Assert.That(td.Chunks.Length, Is.EqualTo(chunks.Length));
            for (int i = 0; i < chunks.Length; i++)
            {
                Assert.That(td.Chunks[i].FourCCString, Is.EqualTo(chunks[i].GetProperty("fourcc").GetString()));
                Assert.That(td.Chunks[i].Codec, Is.EqualTo(chunks[i].GetProperty("codec").GetInt32()));
                Assert.That(td.Chunks[i].Offset, Is.EqualTo(chunks[i].GetProperty("offset").GetUInt32()));
                Assert.That(td.Chunks[i].StoredSize, Is.EqualTo(chunks[i].GetProperty("stored_size").GetUInt32()));
            }

            if (x.GetProperty("heights_q").ValueKind == JsonValueKind.Null) Assert.That(td.HeightsQ, Is.Null);
            else
            {
                Assert.That(td.HeightsN, Is.EqualTo(x.GetProperty("heights_n").GetInt32()));
                Assert.That(td.HeightsQ.Select(v => (int)v).ToArray(), Is.EqualTo(GoldenFiles.Ints(x.GetProperty("heights_q"))));
                float[] hm = td.HeightsMetres();
                var expected = x.GetProperty("heights_m").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                Assert.That(hm, Is.EqualTo(expected)); // bit-exact float32
                Assert.That(td.HeightAt(td.HeightsN - 1, 1), Is.EqualTo(expected[(td.HeightsN - 1) * td.HeightsN + 1]));
            }
            if (x.GetProperty("biomes").ValueKind == JsonValueKind.Null) Assert.That(td.Biomes, Is.Null);
            else
            {
                Assert.That(td.BiomesN, Is.EqualTo(x.GetProperty("biomes_n").GetInt32()));
                Assert.That(td.Biomes.Select(v => (int)v).ToArray(), Is.EqualTo(GoldenFiles.Ints(x.GetProperty("biomes"))));
            }

            var names = x.GetProperty("names").EnumerateArray().ToArray();
            Assert.That(td.Names.Count, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; i++) GoldenFiles.AssertName(names[i], td.Names[i], "name " + i);

            var roads = x.GetProperty("roads").EnumerateArray().ToArray();
            Assert.That(td.Roads.Count, Is.EqualTo(roads.Length));
            for (int i = 0; i < roads.Length; i++)
            {
                JsonElement e = roads[i];
                RoadRecord r = td.Roads[i];
                Assert.That(r.OsmWayId, Is.EqualTo(e.GetProperty("osm_way_id").GetUInt64()));
                Assert.That((int)r.RoadClass, Is.EqualTo(e.GetProperty("road_class").GetInt32()));
                Assert.That((int)r.Surface, Is.EqualTo(e.GetProperty("surface").GetInt32()));
                Assert.That((int)r.SurfaceSource, Is.EqualTo(e.GetProperty("surface_source").GetInt32()));
                Assert.That((int)r.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That((int)r.Lanes, Is.EqualTo(e.GetProperty("lanes").GetInt32()));
                Assert.That((int)r.SacScale, Is.EqualTo(e.GetProperty("sac_scale").GetInt32()));
                Assert.That((int)r.TrailVisibility, Is.EqualTo(e.GetProperty("trail_visibility").GetInt32()));
                Assert.That((int)r.Layer, Is.EqualTo(e.GetProperty("layer").GetInt32()));
                Assert.That(r.WidthCm, Is.EqualTo(e.GetProperty("width_cm").GetUInt64()));
                Assert.That((int)r.Access, Is.EqualTo(e.GetProperty("access").GetInt32()));
                Assert.That(r.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                Assert.That(r.RefRef, Is.EqualTo(e.GetProperty("ref_ref").GetInt32()));
                Assert.That(r.Points, Is.EqualTo(GoldenFiles.Ints(e.GetProperty("points"))));
            }

            var lines = x.GetProperty("lines").EnumerateArray().ToArray();
            Assert.That(td.Lines.Count, Is.EqualTo(lines.Length));
            for (int i = 0; i < lines.Length; i++)
            {
                JsonElement e = lines[i];
                LineRecord l = td.Lines[i];
                Assert.That(l.OsmWayId, Is.EqualTo(e.GetProperty("osm_way_id").GetUInt64()));
                Assert.That((int)l.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That((int)l.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(l.WidthCm, Is.EqualTo(e.GetProperty("width_cm").GetUInt64()));
                Assert.That(l.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                Assert.That(l.Points, Is.EqualTo(GoldenFiles.Ints(e.GetProperty("points"))));
            }

            var bldgs = x.GetProperty("buildings").EnumerateArray().ToArray();
            Assert.That(td.Buildings.Count, Is.EqualTo(bldgs.Length));
            for (int i = 0; i < bldgs.Length; i++)
            {
                JsonElement e = bldgs[i];
                BuildingRecord b = td.Buildings[i];
                Assert.That(b.OsmRef, Is.EqualTo(e.GetProperty("osm_ref").GetUInt64()));
                Assert.That((int)b.Archetype, Is.EqualTo(e.GetProperty("archetype").GetInt32()));
                Assert.That((int)b.Use, Is.EqualTo(e.GetProperty("use").GetInt32()));
                Assert.That((int)b.Levels, Is.EqualTo(e.GetProperty("levels").GetInt32()));
                Assert.That((int)b.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(b.HeightCm, Is.EqualTo(e.GetProperty("height_cm").GetUInt64()));
                Assert.That(b.MinHeightCm, Is.EqualTo(e.GetProperty("min_height_cm").GetUInt64()));
                Assert.That((int)b.RoofShape, Is.EqualTo(e.GetProperty("roof_shape").GetInt32()));
                Assert.That((int)b.RoofMaterial, Is.EqualTo(e.GetProperty("roof_material").GetInt32()));
                Assert.That((int)b.WallMaterial, Is.EqualTo(e.GetProperty("wall_material").GetInt32()));
                Assert.That(b.Seed, Is.EqualTo(e.GetProperty("seed").GetUInt32()));
                Assert.That(b.Seed, Is.EqualTo(Hashes.BuildingSeed(b.OsmRef)));
                Assert.That(b.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                var rings = e.GetProperty("rings").EnumerateArray().ToArray();
                Assert.That(b.Rings.Length, Is.EqualTo(rings.Length));
                for (int k = 0; k < rings.Length; k++) Assert.That(b.Rings[k], Is.EqualTo(GoldenFiles.Ints(rings[k])));
            }

            var areas = x.GetProperty("areas").EnumerateArray().ToArray();
            Assert.That(td.Areas.Count, Is.EqualTo(areas.Length));
            for (int i = 0; i < areas.Length; i++)
            {
                JsonElement e = areas[i];
                AreaRecord a = td.Areas[i];
                Assert.That(a.OsmRef, Is.EqualTo(e.GetProperty("osm_ref").GetUInt64()));
                Assert.That((int)a.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That((int)a.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(a.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                Assert.That(a.Vertices, Is.EqualTo(GoldenFiles.Ints(e.GetProperty("vertices"))));
                Assert.That(a.Indices, Is.EqualTo(GoldenFiles.Ints(e.GetProperty("indices"))));
                Assert.That(a.Rings, Is.EqualTo(e.GetProperty("rings").EnumerateArray().SelectMany(GoldenFiles.Ints).ToArray()));
            }

            var pois = x.GetProperty("pois").EnumerateArray().ToArray();
            Assert.That(td.Pois.Count, Is.EqualTo(pois.Length));
            for (int i = 0; i < pois.Length; i++)
            {
                JsonElement e = pois[i];
                PoiRecord p = td.Pois[i];
                Assert.That(p.OsmRef, Is.EqualTo(e.GetProperty("osm_ref").GetUInt64()));
                Assert.That((int)p.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That((int)p.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That((int)p.Importance, Is.EqualTo(e.GetProperty("importance").GetInt32()));
                Assert.That(p.XCm, Is.EqualTo(e.GetProperty("x_cm").GetInt32()));
                Assert.That(p.ZCm, Is.EqualTo(e.GetProperty("z_cm").GetInt32()));
                Assert.That(p.EleDm, Is.EqualTo(e.GetProperty("ele_dm").GetInt32()));
                Assert.That(p.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                Assert.That(p.SearchId, Is.EqualTo(e.GetProperty("search_id").GetUInt64()));
            }

            JsonElement seed = x.GetProperty("seed");
            Assert.That(td.HasSeed, Is.EqualTo(seed.ValueKind != JsonValueKind.Null));
            if (td.HasSeed)
            {
                Assert.That(td.TileSeed, Is.EqualTo(seed.GetProperty("tile_seed").GetUInt64()));
                Assert.That(td.TileSeed, Is.EqualTo(Hashes.TileSeed(td.Tile.Key, td.DataVersion)));
                Assert.That((int)td.ScatterRuleset, Is.EqualTo(seed.GetProperty("ruleset").GetInt32()));
            }
            JsonElement meta = x.GetProperty("meta_json");
            Assert.That(td.MetaJson, Is.EqualTo(meta.ValueKind == JsonValueKind.Null ? null : meta.GetString()));
        }

        [Test]
        public void DecodesTheGoldenTile()
        {
            byte[] blob = GoldenFiles.Bytes("golden.ght");
            JsonElement x = GoldenFiles.Json("golden_tile.json");
            Assert.That(blob.Length, Is.EqualTo(x.GetProperty("bytes").GetInt32()));
            TileData td = TileReader.Decode(blob);
            AssertTileMatches(td, x);

            // every chunk type is present, and both codecs are exercised
            Assert.That(td.Chunks.Select(c => c.FourCCString), Is.EquivalentTo(new[] { "AREA", "BIOM", "BLDG", "HGHT", "LINE", "META", "NAME", "POIS", "ROAD", "SEED" }));
            Assert.That(td.Chunks.Select(c => c.Codec).Distinct(), Is.EquivalentTo(new byte[] { 0, 1 }));

            // Devanagari names and the META JSON parse with Core's own JSON reader
            Assert.That(td.Names.Any(n => n.Ne == "बौद्धनाथ"), Is.True);
            JsonObject metaObj = Save.Json.Parse(td.MetaJson).AsObject();
            Assert.That(metaObj.GetString("region"), Is.EqualTo("golden_region"));
            Assert.That(metaObj.GetObject("sources").GetString("dem"), Is.EqualTo("glo30"));
            Assert.That(metaObj.GetString("note"), Is.EqualTo("नमस्ते \"quoted\""));
            Assert.That(td.Name(0), Is.Null);
            Assert.That(td.Name(td.Roads.Single(r => r.OsmWayId == 4000000123UL).RefRef).Default, Is.EqualTo("NH04"));

            double gx, gz;
            td.LocalToGame(150, -250, out gx, out gz);
            Assert.That(gx, Is.EqualTo(td.Tile.X0 + 1.5));
            Assert.That(gz, Is.EqualTo(td.Tile.Z0 - 2.5));
        }

        [Test]
        public void SkipsUnknownChunks()
        {
            byte[] blob = GoldenFiles.Bytes("golden_unknown.ght");
            JsonElement x = GoldenFiles.Json("golden_tile.json");
            JsonElement u = x.GetProperty("unknown_variant");
            TileData td = TileReader.Decode(blob);
            Assert.That(td.Chunks.Select(c => c.FourCCString), Is.EqualTo(u.GetProperty("chunks").EnumerateArray().Select(e => e.GetString())));
            Assert.That(td.Chunks.Any(c => c.FourCCString == "XTRA"), Is.True);
            TileData plain = TileReader.Decode(GoldenFiles.Bytes("golden.ght"));
            Assert.That(td.Roads.Count, Is.EqualTo(plain.Roads.Count));
            Assert.That(td.HeightsQ, Is.EqualTo(plain.HeightsQ));
            Assert.That(td.MetaJson, Is.EqualTo(plain.MetaJson));
            Assert.That(td.Pois.Select(p => p.OsmRef), Is.EqualTo(plain.Pois.Select(p => p.OsmRef)));
        }

        [Test]
        public void RejectsCorruption()
        {
            byte[] good = GoldenFiles.Bytes("golden.ght");

            byte[] b = (byte[])good.Clone();
            b[b.Length - 3] ^= 0x40;
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(b), "payload bit flip");
            Assert.DoesNotThrow(() => TileReader.ReadHeader(b), "header is still fine");

            b = (byte[])good.Clone();
            b[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(b), "magic");

            b = (byte[])good.Clone();
            b[4] = 2;
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(b), "version");

            Assert.Throws<InvalidDataException>(() => TileReader.Decode(good.Take(good.Length - 1).ToArray()), "truncated");
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(good.Take(20).ToArray()), "short header");

            b = (byte[])good.Clone();
            b[8] = 30; // level out of range
            Assert.Throws<InvalidDataException>(() => TileReader.ReadHeader(b), "level");
        }

        [Test]
        public void DecodesAWindowInsideALargerBuffer()
        {
            byte[] good = GoldenFiles.Bytes("golden.ght");
            var big = new byte[good.Length + 37];
            Array.Copy(good, 0, big, 17, good.Length);
            TileData td = TileReader.Decode(big, 17, good.Length);
            AssertTileMatches(td, GoldenFiles.Json("golden_tile.json"));
        }

        [Test]
        public void HeightQuantisation()
        {
            Assert.That(Ght.Dequantize(0), Is.EqualTo(-100f));
            Assert.That(Ght.Dequantize(65535), Is.EqualTo(9730.25f));
            Assert.That(Ght.Quantize(-100.0), Is.EqualTo(0));
            Assert.That(Ght.Quantize(-500.0), Is.EqualTo(0));
            Assert.That(Ght.Quantize(20000.0), Is.EqualTo(65535));
            Assert.That(Ght.Quantize(8848.86), Is.EqualTo((ushort)Math.Round((8848.86 + 100.0) / 0.15)));
        }
    }
}
