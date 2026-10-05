using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The W2 readers (W2_DESIGN 9.3, DATA_FORMATS 1.11-1.14, 6, 7): RATR, JNCT, BFNT and PROP against the pipeline's
    /// golden tile, the curated DB (.ghcd and its JSON bridge) and the transit file (.ghrt) against their goldens,
    /// W1 tiles without the chunks, and corruption (count mismatches, bad edges, trailing bytes).
    /// </summary>
    public class DataW2ReaderTests
    {
        // ------------------------------------------------------------------------------------------------------------
        // Golden tile (shared/golden/golden_w2.ght, written by make_golden.py with the pipeline's encoder)
        // ------------------------------------------------------------------------------------------------------------

        [Test]
        public void DecodesTheW2GoldenTileChunks()
        {
            TileData td = TileReader.Decode(GoldenFiles.Bytes("golden_w2.ght"));
            JsonElement x = GoldenFiles.Json("golden_w2.json");

            JsonElement[] ratr = x.GetProperty("road_attrs").EnumerateArray().ToArray();
            Assert.That(td.RoadAttrs.Count, Is.EqualTo(ratr.Length));
            Assert.That(td.HasRoadAttrs, Is.EqualTo(ratr.Length > 0));
            for (int i = 0; i < ratr.Length; i++)
            {
                JsonElement e = ratr[i];
                RoadAttrRecord a = td.RoadAttrs[i];
                Assert.That((int)a.Area, Is.EqualTo(e.GetProperty("area_type").GetInt32()), "area " + i);
                Assert.That(a.Sidewalk, Is.EqualTo(e.GetProperty("sidewalk").GetInt32()), "sidewalk " + i);
                Assert.That(a.LanesFwd, Is.EqualTo(e.GetProperty("lanes_fwd").GetInt32()));
                Assert.That(a.LanesBwd, Is.EqualTo(e.GetProperty("lanes_bwd").GetInt32()));
                Assert.That(a.MaxspeedKmh, Is.EqualTo(e.GetProperty("maxspeed_kmh").GetInt32()));
                Assert.That(a.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(a.PartnerWayId, Is.EqualTo(e.GetProperty("partner_way_id").GetInt64()));
                Assert.That(a.MedianCm, Is.EqualTo(e.GetProperty("median_cm").GetInt32()));
                Assert.That(a.CorridorDm, Is.EqualTo(GoldenFiles.Ints(e.GetProperty("corridor_dm"))));
                Assert.That(a.CorridorDm, Is.Not.Null);
            }

            JsonElement[] jn = x.GetProperty("junctions").EnumerateArray().ToArray();
            Assert.That(td.Junctions.Count, Is.EqualTo(jn.Length));
            for (int i = 0; i < jn.Length; i++)
            {
                JsonElement e = jn[i];
                JunctionRecord j = td.Junctions[i];
                Assert.That(j.OsmNodeId, Is.EqualTo(e.GetProperty("osm_node_id").GetInt64()));
                Assert.That((int)j.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That(j.Arms, Is.EqualTo(e.GetProperty("arms").GetInt32()));
                Assert.That(j.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(j.XCm, Is.EqualTo(e.GetProperty("x_cm").GetInt32()));
                Assert.That(j.ZCm, Is.EqualTo(e.GetProperty("z_cm").GetInt32()));
                Assert.That(j.RingDiameterCm, Is.EqualTo(e.GetProperty("ring_diameter_cm").GetInt32()));
                Assert.That(j.IslandDiameterCm, Is.EqualTo(e.GetProperty("island_diameter_cm").GetInt32()));
                Assert.That(j.IslandAreaRef, Is.EqualTo(e.GetProperty("island_area_osm_ref").GetInt64()));
                Assert.That(j.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
            }

            JsonElement[] bf = x.GetProperty("building_fronts").EnumerateArray().ToArray();
            Assert.That(td.BuildingFronts.Count, Is.EqualTo(bf.Length));
            Assert.That(td.BuildingFronts.Count, Is.EqualTo(td.Buildings.Count), "one front per building");
            for (int i = 0; i < bf.Length; i++)
            {
                JsonElement e = bf[i];
                BuildingFrontRecord f = td.BuildingFronts[i];
                Assert.That((int)f.Profile, Is.EqualTo(e.GetProperty("style_profile").GetInt32()));
                Assert.That((int)f.Area, Is.EqualTo(e.GetProperty("area_type").GetInt32()));
                Assert.That(f.FrontEdge, Is.EqualTo(e.GetProperty("front_edge").GetInt32()));
                Assert.That(f.FrontDistDm, Is.EqualTo(e.GetProperty("front_dist_dm").GetInt32()));
                Assert.That(f.ShopBays, Is.EqualTo(e.GetProperty("shop_bays").GetInt32()));
                Assert.That(f.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(f.SecondEdge, Is.EqualTo(e.GetProperty("second_edge").GetInt32()));
                Assert.That(f.HasFront, Is.EqualTo(f.FrontEdge != BuildingFrontRecord.NoEdge));
                Assert.That(f.ShopCount, Is.EqualTo(f.ShopBays & 0x0F));
            }

            JsonElement[] props = x.GetProperty("props").EnumerateArray().ToArray();
            Assert.That(td.Props.Count, Is.EqualTo(props.Length));
            for (int i = 0; i < props.Length; i++)
            {
                JsonElement e = props[i];
                PropRecord p = td.Props[i];
                Assert.That(p.OsmRef, Is.EqualTo(e.GetProperty("osm_ref").GetUInt64()));
                Assert.That((int)p.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That(p.Subtype, Is.EqualTo(e.GetProperty("subtype").GetInt32()));
                Assert.That((int)p.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(p.XCm, Is.EqualTo(e.GetProperty("x_cm").GetInt32()));
                Assert.That(p.ZCm, Is.EqualTo(e.GetProperty("z_cm").GetInt32()));
                Assert.That(p.YawCdeg, Is.EqualTo(e.GetProperty("yaw_cdeg").GetInt32()));
                Assert.That(p.HeightDm, Is.EqualTo(e.GetProperty("height_dm").GetInt32()));
                Assert.That(p.NameRef, Is.EqualTo(e.GetProperty("name_ref").GetInt32()));
                Assert.That(p.RefRef, Is.EqualTo(e.GetProperty("ref_ref").GetInt32()));
                if (p.Kind == ObjectKind.Tree) Assert.That((int)p.Tree, Is.EqualTo(p.Subtype));
                if (!p.Has(PropFlags.Yaw)) Assert.That(p.YawDeg, Is.EqualTo(0f));
            }
            // Areas carry the new flags (HERITAGE_ZONE, SACRED_NO_VEHICLE).
            JsonElement[] areas = x.GetProperty("areas").EnumerateArray().ToArray();
            for (int i = 0; i < areas.Length; i++) Assert.That((int)td.Areas[i].Flags, Is.EqualTo(areas[i].GetProperty("flags").GetInt32()));
        }

        [Test]
        public void W1TilesHaveNoW2ChunksAndSafeDefaults()
        {
            TileData td = TileReader.Decode(GoldenFiles.Bytes("golden.ght"));
            Assert.That(td.RoadAttrs, Is.Empty);
            Assert.That(td.Junctions, Is.Empty);
            Assert.That(td.BuildingFronts, Is.Empty);
            Assert.That(td.Props, Is.Empty);
            Assert.That(td.HasRoadAttrs, Is.False);
            Assert.That(td.HasBuildingFronts, Is.False);
            if (td.Roads.Count > 0)
            {
                RoadAttrRecord a = td.RoadAttrOf(0);
                Assert.That(a.Area, Is.EqualTo(AreaType.Unknown));
                Assert.That(a.CorridorDm, Is.Not.Null.And.Empty);
            }
            if (td.Buildings.Count > 0)
            {
                BuildingFrontRecord f = td.BuildingFrontOf(0);
                Assert.That(f.HasFront, Is.False);
                Assert.That(f.SecondEdge, Is.EqualTo(BuildingFrontRecord.NoEdge));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Corruption: a hand-built GHT1 with stored chunks
        // ------------------------------------------------------------------------------------------------------------

        private sealed class Writer
        {
            private readonly List<byte> _b = new List<byte>();

            public Writer U8(int v)
            {
                _b.Add((byte)v);
                return this;
            }

            public Writer U16(int v)
            {
                _b.Add((byte)v);
                _b.Add((byte)(v >> 8));
                return this;
            }

            public Writer U32(uint v)
            {
                for (int i = 0; i < 4; i++) _b.Add((byte)(v >> (8 * i)));
                return this;
            }

            public Writer Varint(ulong v)
            {
                while (v >= 0x80)
                {
                    _b.Add((byte)(v | 0x80));
                    v >>= 7;
                }
                _b.Add((byte)v);
                return this;
            }

            public Writer Svarint(long v)
            {
                return Varint((ulong)((v << 1) ^ (v >> 63)));
            }

            public Writer Str(string s)
            {
                byte[] b = Encoding.UTF8.GetBytes(s);
                Varint((ulong)b.Length);
                _b.AddRange(b);
                return this;
            }

            public byte[] Bytes()
            {
                return _b.ToArray();
            }
        }

        /// <summary>A GHT1 tile 10/516/161 with stored (codec 0) chunks, CRC set.</summary>
        private static byte[] Tile(params KeyValuePair<string, byte[]>[] chunks)
        {
            int headerAndTable = Ght.HeaderSize + Ght.ChunkEntrySize * chunks.Length;
            var payload = new List<byte>();
            var entries = new Writer();
            foreach (KeyValuePair<string, byte[]> c in chunks.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var stored = new Writer().U32((uint)c.Value.Length).Bytes().Concat(c.Value).ToArray();
                entries.U32(Ght.FourCC(c.Key)).U8(0).U8(0).U16(0).U32((uint)(headerAndTable + payload.Count)).U32((uint)stored.Length);
                payload.AddRange(stored);
            }
            byte[] p = payload.ToArray();
            var all = new Writer().U32(Ght.Magic).U16(1).U16(Ght.FlagHasDetail).U8(10).U8(0).U16(0).U32(516).U32(161).U32(1)
                                  .U16(chunks.Length).U16(0).U32(Hashes.Crc32(p)).Bytes().Concat(entries.Bytes()).Concat(p).ToArray();
            return all;
        }

        private static KeyValuePair<string, byte[]> C(string fourcc, byte[] body)
        {
            return new KeyValuePair<string, byte[]>(fourcc, body);
        }

        private static byte[] OneRoad()
        {
            // varint count; osm_way_id, class, surface, source, flags, lanes, sac, vis, layer, width, access, name, ref, n, points
            return new Writer().Varint(1).Varint(7).U8(7).U8(1).U8(0).U8(0).U8(0).U8(0).U8(0).U8(0).Varint(0).U8(127).Varint(0).Varint(0)
                               .Varint(2).Svarint(1000).Svarint(1000).Svarint(5000).Svarint(0).Bytes();
        }

        private static byte[] OneBuilding()
        {
            return new Writer().Varint(1).Varint(42).U8(1).U8(1).U8(3).U8(0).Varint(900).Varint(0).U8(0).U8(0).U8(0).U32(12345).Varint(0)
                               .Varint(1).Varint(4).Svarint(1000).Svarint(1000).Svarint(800).Svarint(0).Svarint(0).Svarint(600).Svarint(-800).Svarint(0).Bytes();
        }

        private static byte[] Ratr(int count, int corridor = 2)
        {
            var w = new Writer().Varint((ulong)count);
            for (int i = 0; i < count; i++)
            {
                w.U8(2).U8(4).U8(1).U8(1).U8(40).U8(0x80).Varint(0).Varint(0).Varint((ulong)corridor);
                for (int k = 0; k < corridor; k++) w.Varint((ulong)(100 + k));
            }
            return w.Bytes();
        }

        [Test]
        public void HandBuiltW2ChunksDecodeAndCorruptionIsRejected()
        {
            byte[] ok = Tile(C("ROAD", OneRoad()), C("RATR", Ratr(1)), C("BLDG", OneBuilding()),
                             C("BFNT", new Writer().Varint(1).U8(3).U8(1).U8(2).U8(37).U8(0x82).U8(2).U8(255).Bytes()),
                             C("JNCT", new Writer().Varint(1).Varint(99).U8(1).U8(4).U8(0x30).Svarint(500).Svarint(-20).Varint(4380).Varint(0).Varint(0).Varint(0).Bytes()),
                             C("PROP", new Writer().Varint(1).Varint(44 << 2).U8(1).U8(1).U8(5).Svarint(10).Svarint(20).U16(9000).Varint(180).Varint(0).Varint(0).Bytes()));
            TileData td = TileReader.Decode(ok);
            Assert.That(td.RoadAttrs.Count, Is.EqualTo(1));
            Assert.That(td.RoadAttrs[0].CorridorDm, Is.EqualTo(new[] { 100, 101 }));
            Assert.That(td.RoadAttrs[0].Has(RoadAttrFlags.Paintable), Is.True);
            Assert.That(td.RoadAttrs[0].SidewalkKind, Is.EqualTo(Sidewalk.Both));
            Assert.That(td.BuildingFronts[0].Profile, Is.EqualTo(StyleProfile.Patan));
            Assert.That(td.BuildingFronts[0].ShopCount, Is.EqualTo(2));
            Assert.That(td.BuildingFronts[0].Has(BuildingFrontFlags.Corner), Is.True);
            Assert.That(td.Junctions[0].Kind, Is.EqualTo(JunctionKind.Roundabout));
            Assert.That(td.Junctions[0].Has(JunctionFlags.HasPolice), Is.True);
            Assert.That(td.Props[0].Tree, Is.EqualTo(TreeClass.Pipal));
            Assert.That(td.Props[0].YawDeg, Is.EqualTo(90f));
            Assert.That(td.Props[0].Has(PropFlags.Chautari), Is.True);

            // RATR count must equal the ROAD count; BFNT the BLDG count; front edges inside the ring; no trailing bytes.
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", OneRoad()), C("RATR", Ratr(2)))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("RATR", Ratr(1)))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("BLDG", OneBuilding()),
                                                                             C("BFNT", new Writer().Varint(1).U8(1).U8(1).U8(4).U8(0).U8(0).U8(0).U8(255).Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("BLDG", OneBuilding()),
                                                                             C("BFNT", new Writer().Varint(2).U8(1).U8(1).U8(0).U8(0).U8(0).U8(0).U8(255).Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", OneRoad()), C("RATR", Ratr(1).Concat(new byte[] { 0 }).ToArray()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("PROP",
                new Writer().Varint(1).Varint(4).U8(4).U8(0).U8(1).Svarint(0).Svarint(0).U16(36000).Varint(0).Varint(0).Varint(0).Bytes()))));
            // A name reference beyond the (empty) name table.
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("JNCT",
                new Writer().Varint(1).Varint(1).U8(0).U8(3).U8(0).Svarint(0).Svarint(0).Varint(0).Varint(0).Varint(0).Varint(1).Bytes()))));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Curated DB and transit routes
        // ------------------------------------------------------------------------------------------------------------

        private static void AssertCurated(CuratedDb db, JsonElement golden)
        {
            JsonElement[] recs = golden.GetProperty("records").EnumerateArray().ToArray();
            Assert.That(db.Count, Is.EqualTo(recs.Length));
            foreach (JsonElement e in recs)
            {
                string id = e.GetProperty("id").GetString();
                HeritageRecord r;
                Assert.That(db.TryGetHeritage(id, out r), Is.True, id);
                Assert.That((int)r.Kind, Is.EqualTo(e.GetProperty("kind_value").GetInt32()), id);
                Assert.That(r.Kind.ToString().ToUpperInvariant(), Is.EqualTo(e.GetProperty("kind").GetString().Replace("_", "")), id);
                Assert.That(r.Stage, Is.EqualTo(e.GetProperty("stage").GetInt32()));
                Assert.That((int)r.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()), id);
                Assert.That(r.Tiers, Is.EqualTo(e.GetProperty("tiers").GetInt32()));
                Assert.That(r.PlinthLevels, Is.EqualTo(e.GetProperty("plinth_levels").GetInt32()));
                Assert.That(r.Doors, Is.EqualTo(e.GetProperty("doors").GetInt32()));
                Assert.That(r.Finish.ToString().ToUpperInvariant(), Is.EqualTo(e.GetProperty("finish").GetString().Replace("_", "")));
                Assert.That(r.Entry.ToString().ToUpperInvariant(), Is.EqualTo(e.GetProperty("entry_rule").GetString().Replace("_", "")));
                Assert.That(r.Kora.ToString().ToUpperInvariant(), Is.EqualTo(e.GetProperty("kora").GetString().Replace("_", "")));
                JsonElement yaw = e.GetProperty("yaw_deg");
                if (yaw.ValueKind == JsonValueKind.Null) Assert.That(float.IsNaN(r.YawDeg), Is.True, id);
                else Assert.That(r.YawDeg, Is.EqualTo(yaw.GetDouble()).Within(0.01), id);
                double h = e.GetProperty("height_m").GetDouble();
                if (h == 0) Assert.That(float.IsNaN(r.HeightM), Is.True, id);
                else Assert.That(r.HeightM, Is.EqualTo(h).Within(0.01), id);
                Assert.That(r.Name, Is.EqualTo(e.GetProperty("name_en").GetString()));
                Assert.That(r.NameNe ?? "", Is.EqualTo(e.GetProperty("name_ne").GetString()));
                Assert.That(r.Deity ?? "", Is.EqualTo(e.GetProperty("deity").GetString()));
                Assert.That(r.AnchorRef, Is.EqualTo(e.GetProperty("anchor_ref").GetUInt64()));
                Assert.That(r.FootprintRef, Is.EqualTo(e.GetProperty("footprint_ref").GetUInt64()));
                Assert.That(r.CompoundRef, Is.EqualTo(e.GetProperty("compound_ref").GetUInt64()));
                Assert.That(r.AnchorTileKey, Is.EqualTo(e.GetProperty("anchor_tile").GetUInt64()));
                Assert.That(r.Hidden, Is.EqualTo(e.GetProperty("hidden").EnumerateArray().Select(v => v.GetUInt64()).ToArray()));
                if (r.AnchorRef != 0)
                {
                    Assert.That(r.X, Is.EqualTo(e.GetProperty("x").GetDouble()).Within(0.006), id);
                    Assert.That(r.Z, Is.EqualTo(e.GetProperty("z").GetDouble()).Within(0.006), id);
                    Assert.That(r.Osm, Is.EqualTo(HeritageRecord.OsmOf(r.AnchorRef)));
                }
                foreach (JsonProperty a in e.GetProperty("attrs").EnumerateObject())
                    Assert.That(r.AttrText[a.Name], Is.EqualTo(a.Value.GetString()), id + " " + a.Name);
                Assert.That(r.Provenance ?? "", Is.EqualTo(e.GetProperty("provenance").GetString()));
                Assert.That(r.Review ?? "", Is.EqualTo(e.GetProperty("review").GetString()));
            }
        }

        [Test]
        public void DecodesTheGoldenCuratedDbAndItsJsonBridge()
        {
            JsonElement golden = GoldenFiles.Json("golden_curated.json");
            CuratedDb db = CuratedDb.Read(GoldenFiles.Bytes("golden.ghcd"));
            AssertCurated(db, golden);
            // The JSON bridge (hero_recipes.json has the same record shape) parses to the same records.
            CuratedDb json = CuratedDb.Read(File.ReadAllBytes(GoldenFiles.PathOf("golden_curated.json")));
            AssertCurated(json, golden);
            HeritageRecord nyat;
            Assert.That(db.TryGetHeritage("her.bkt.nyatapola", out nyat), Is.True);
            Assert.That(nyat.AttrFloat("plinth_rise_m", 0f), Is.EqualTo(1.4f));
            Assert.That(nyat.Guardians, Does.StartWith("wrestlers"));
        }

        [Test]
        public void CuratedDbRejectsCorruption()
        {
            byte[] good = GoldenFiles.Bytes("golden.ghcd");
            byte[] bad = (byte[])good.Clone();
            bad[bad.Length - 1] ^= 0x55;
            Assert.Throws<InvalidDataException>(() => CuratedDb.Read(bad), "CRC");
            byte[] ver = (byte[])good.Clone();
            ver[4] = 9;
            Assert.Throws<InvalidDataException>(() => CuratedDb.Read(ver), "version");
            Assert.Throws<InvalidDataException>(() => CuratedDb.Read(good.Take(20).ToArray()), "truncated");
            Assert.Throws<InvalidDataException>(() => CuratedDb.Read(Encoding.UTF8.GetBytes("not a db")));
        }

        [Test]
        public void DecodesTheGoldenTransitFile()
        {
            JsonElement golden = GoldenFiles.Json("golden_transit.json");
            RouteSet set = RouteSet.Read(GoldenFiles.Bytes("golden.ghrt"));
            JsonElement[] routes = golden.GetProperty("routes").EnumerateArray().ToArray();
            Assert.That(set.Routes.Count, Is.EqualTo(routes.Length));
            for (int i = 0; i < routes.Length; i++)
            {
                JsonElement e = routes[i];
                TransitRoute r = set.Routes[i];
                Assert.That(r.OsmRelationId, Is.EqualTo(e.GetProperty("osm_id").GetInt64()));
                Assert.That(r.Id, Is.EqualTo(e.GetProperty("id").GetString()));
                Assert.That(r.Ref, Is.EqualTo(e.GetProperty("ref").GetString()));
                Assert.That((int)r.Mode, Is.EqualTo(e.GetProperty("mode").GetInt32()));
                Assert.That((int)r.Livery, Is.EqualTo(e.GetProperty("livery").GetInt32()));
                Assert.That((int)r.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                GoldenFiles.AssertName(e.GetProperty("name"), r.Name, r.Id);
                Assert.That(r.From == null ? "" : r.From.Default, Is.EqualTo(e.GetProperty("from").GetString()));
                Assert.That(r.To == null ? "" : r.To.Default, Is.EqualTo(e.GetProperty("to").GetString()));
                Assert.That(r.HeadwayPeakS, Is.EqualTo(e.GetProperty("headway_peak_s").GetInt32()));
                Assert.That(r.HeadwayOffS, Is.EqualTo(e.GetProperty("headway_off_s").GetInt32()));
                Assert.That(r.LengthM, Is.EqualTo(e.GetProperty("length_m").GetInt32()));
                Assert.That(r.WayIds, Is.EqualTo(GoldenFiles.Longs(e.GetProperty("way_ids"))));
                Assert.That(r.WayForward, Is.EqualTo(e.GetProperty("way_forward").EnumerateArray().Select(v => v.GetBoolean()).ToArray()));
                JsonElement[] stops = e.GetProperty("stops").EnumerateArray().ToArray();
                Assert.That(r.Stops.Length, Is.EqualTo(stops.Length));
                for (int k = 0; k < stops.Length; k++)
                {
                    Assert.That(r.Stops[k].OsmRef, Is.EqualTo(stops[k].GetProperty("osm_ref").GetUInt64()));
                    Assert.That((int)r.Stops[k].Flags, Is.EqualTo(stops[k].GetProperty("flags").GetInt32()));
                    Assert.That(r.Stops[k].X, Is.EqualTo(stops[k].GetProperty("x_dm").GetInt64() / 10.0).Within(1e-9));
                    Assert.That(r.Stops[k].Z, Is.EqualTo(stops[k].GetProperty("z_dm").GetInt64() / 10.0).Within(1e-9));
                    Assert.That(r.Stops[k].AlongM, Is.EqualTo(stops[k].GetProperty("along_dm").GetInt64() / 10f).Within(1e-4));
                    GoldenFiles.AssertName(stops[k].GetProperty("name"), r.Stops[k].Name, r.Id + " stop " + k);
                }
            }
            JsonElement[] res = golden.GetProperty("restrictions").EnumerateArray().ToArray();
            Assert.That(set.Restrictions.Count, Is.EqualTo(res.Length));
            for (int i = 0; i < res.Length; i++)
            {
                Assert.That(set.Restrictions[i].OsmRelationId, Is.EqualTo(res[i].GetProperty("osm_id").GetInt64()));
                Assert.That((int)set.Restrictions[i].Kind, Is.EqualTo(res[i].GetProperty("kind").GetInt32()));
                Assert.That(set.Restrictions[i].FromWay, Is.EqualTo(res[i].GetProperty("from_way").GetInt64()));
                Assert.That(set.Restrictions[i].ViaNode, Is.EqualTo(res[i].GetProperty("via_node").GetInt64()));
                Assert.That(set.Restrictions[i].ViaWay, Is.EqualTo(res[i].GetProperty("via_way").GetInt64()));
                Assert.That(set.Restrictions[i].ToWay, Is.EqualTo(res[i].GetProperty("to_way").GetInt64()));
            }
            byte[] bad = (byte[])GoldenFiles.Bytes("golden.ghrt").Clone();
            bad[bad.Length - 1] ^= 1;
            Assert.Throws<InvalidDataException>(() => RouteSet.Read(bad));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Area-type grid and sacred zones
        // ------------------------------------------------------------------------------------------------------------

        [Test]
        public void AreaTypeGridVotesAndFallsBackToLandStatistics()
        {
            var grid = new AreaTypeGrid();
            TileId id = new TileId(10, 516, 161);
            TileData t = StreamingSampleRegion.Tile(id);
            grid.AddTile(id, t);
            Assert.That(grid.CellCount, Is.GreaterThan(0));
            // Asan Tol (the old core): the vote of BFNT/RATR area types says OLD_CORE.
            double ax, az;
            Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out ax, out az);
            Assert.That(grid.At(ax, az), Is.EqualTo(AreaType.OldCore));
            var w = new float[AreaTypeGrid.TypeCount];
            grid.Weights(ax + 30, az - 40, w);
            Assert.That(w.Sum(), Is.EqualTo(1f).Within(1e-5));
            Assert.That(grid.At(-1e6, -1e6), Is.EqualTo(AreaType.Unknown));
            grid.RemoveTile(id);
            Assert.That(grid.CellCount, Is.EqualTo(0));
            Assert.That(grid.At(ax, az), Is.EqualTo(AreaType.Unknown));

            // Without BFNT/RATR (a W1 tile): built cover classifies.
            Assert.That(AreaTypeGrid.Classify(62500, 0.5 * 62500, 200, 10, 0, 0, 0), Is.EqualTo(AreaType.OldCore));
            Assert.That(AreaTypeGrid.Classify(62500, 0.25 * 62500, 60, 0, 0, 0, 0), Is.EqualTo(AreaType.Urban));
            Assert.That(AreaTypeGrid.Classify(62500, 0.08 * 62500, 20, 0, 0, 0, 0), Is.EqualTo(AreaType.PeriUrban));
            Assert.That(AreaTypeGrid.Classify(62500, 0, 0, 0, 9, 0, 0), Is.EqualTo(AreaType.Forest));
            Assert.That(AreaTypeGrid.Classify(62500, 0, 0, 0, 0, 5, 4), Is.EqualTo(AreaType.Hill));
            Assert.That(AreaTypeGrid.Classify(62500, 0, 0, 0, 0, 1, 8), Is.EqualTo(AreaType.Rural));
            AreaType[] cells = AreaTypeGrid.ClassifyTile(t, 8);
            Assert.That(cells.Length, Is.EqualTo(64));
            Assert.That(cells.Count(c => c == AreaType.OldCore), Is.GreaterThan(10));
        }

        [Test]
        public void SacredZonesCoverCompoundsKorasAndSquares()
        {
            var index = new SacredZoneIndex();
            CuratedDb db = CuratedDb.Read(GoldenFiles.Bytes("golden.ghcd"));
            int zones = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                index.AddTile(id, StreamingSampleRegion.Tile(id), db);
                zones = index.ZoneCount;
            }
            Assert.That(zones, Is.GreaterThan(20));
            // Boudhanath: the stupa lies in a kora zone walked clockwise.
            double bx, bz;
            Geo.WorldFrame.LonLatToGame(85.362004, 27.721436, out bx, out bz);
            SacredZone z;
            Assert.That(index.TryGetZone(bx, bz, out z), Is.True, "Boudha");
            Assert.That(z.Kora, Is.True);
            Assert.That(z.KoraDir, Is.EqualTo(KoraDirection.Clockwise));
            Assert.That(Math.Sqrt((z.CX - bx) * (z.CX - bx) + (z.CZ - bz) * (z.CZ - bz)), Is.LessThan(15.0), "kora centre on the stupa");
            // Basantapur / Kathmandu Durbar Square is a no-motor zone; a segment across it intersects.
            var ktm = new TileId(10, 516, 161);
            TileData kt = StreamingSampleRegion.Tile(ktm);
            AreaRecord sq = kt.Areas.First(a => a.Kind == AreaKind.Pedestrian && (a.Flags & AreaFlags.HeritageZone) != 0 && a.Indices.Length >= 3);
            double kx = ktm.X0 + (sq.Vertices[2 * sq.Indices[0]] + sq.Vertices[2 * sq.Indices[1]] + sq.Vertices[2 * sq.Indices[2]]) / 300.0;
            double kz = ktm.Z0 + (sq.Vertices[2 * sq.Indices[0] + 1] + sq.Vertices[2 * sq.Indices[1] + 1] + sq.Vertices[2 * sq.Indices[2] + 1]) / 300.0;
            SacredZone dz;
            Assert.That(index.TryGetZone(kx, kz, out dz), Is.True);
            Assert.That(dz.Kind, Is.EqualTo(SacredZoneKind.HeritageSquare).Or.EqualTo(SacredZoneKind.Courtyard).Or.EqualTo(SacredZoneKind.Compound));
            Assert.That(index.Contains(kx, kz), Is.True, "Durbar Square");
            Assert.That(index.IntersectsSegment(kx - 200, kz, kx + 200, kz), Is.True);
            Assert.That(index.IntersectsSegment(kx - 5000, kz - 5000, kx - 4990, kz - 5000), Is.False);
            // The zone set is the pipeline's SACRED_NO_VEHICLE set (D14 routing): no secular building court (Singha
            // Durbar, ministries, malls) becomes a courtyard zone.
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var flagged = new HashSet<long>(t.Areas.Where(a => (a.Flags & AreaFlags.SacredNoVehicle) != 0 && a.Indices.Length >= 3).Select(a => (long)a.OsmRef));
                var indexed = new HashSet<long>(index.ZoneRefs(id));
                Assert.That(indexed, Is.EquivalentTo(flagged), id.ToString());
                var secularCourts = t.Buildings.Where(b => b.Rings.Length > 1 && !SacredZoneIndex.IsCourtyardName(t, b.NameRef)).Select(b => (long)b.OsmRef);
                Assert.That(indexed.Intersect(secularCourts.Except(flagged)), Is.Empty, id.ToString());
            }
            foreach (TileId id in StreamingSampleRegion.TilesAt(10)) index.RemoveTile(id);
            Assert.That(index.ZoneCount, Is.EqualTo(0));
            Assert.That(index.Contains(bx, bz), Is.False);
        }

        [Test]
        public void CuratedStupaKoraIsCentredOnTheStupa()
        {
            // Swayambhu (curated: kind STUPA, kora CLOCKWISE, compound w115379177): the landuse centroid is ≈ 59 m from
            // the dome (w201223707), so the kora must be centred on the anchor building, not on the compound.
            string path = DrivingData.SamplePath(".curated.ghcd");
            if (!File.Exists(path)) Assert.Ignore("no curated DB in the sample region");
            CuratedDb db = CuratedDb.Read(File.ReadAllBytes(path));
            var index = new SacredZoneIndex();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10)) index.AddTile(id, StreamingSampleRegion.Tile(id), db);
            // The dome: the curated anchor footprint (BLDG ref = way id << 1).
            ulong stupaRef = HeritageRecord.WayRelationRef("w201223707");
            double sx = double.NaN, sz = double.NaN;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                BuildingRecord b = StreamingSampleRegion.Tile(id).Buildings.FirstOrDefault(r => r.OsmRef == stupaRef);
                if (b == null) continue;
                int n = b.Rings[0].Length / 2;
                sx = id.X0 + Enumerable.Range(0, n).Average(i => b.Rings[0][2 * i]) / 100.0;
                sz = id.Z0 + Enumerable.Range(0, n).Average(i => b.Rings[0][2 * i + 1]) / 100.0;
                break;
            }
            if (double.IsNaN(sx)) Assert.Ignore("Swayambhu stupa not in the sample region");
            SacredZone z;
            Assert.That(index.TryGetZone(sx, sz, out z), Is.True, "the stupa lies in a zone");
            Assert.That(z.Kora, Is.True);
            Assert.That(z.Kind, Is.EqualTo(SacredZoneKind.StupaKora));
            Assert.That(z.KoraDir, Is.EqualTo(KoraDirection.Clockwise));
            Assert.That(Math.Sqrt((z.CX - sx) * (z.CX - sx) + (z.CZ - sz) * (z.CZ - sz)), Is.LessThan(5.0), "kora centre on the Swayambhu dome");
        }
    }
}
