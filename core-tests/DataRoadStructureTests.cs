using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The RSTR chunk (W2 detail pass, DATA_FORMATS 1.15): the golden tile written by the pipeline's encoder, tiles
    /// without the chunk, hand-built chunks and their corruption, the record helpers, the generated enums, and the
    /// committed sample region (one record per road, decks over water, foot overbridges, underpass clearances, the
    /// 4.8 m corridor floor and trimmed buildings).
    /// </summary>
    public class DataRoadStructureTests
    {
        // ------------------------------------------------------------------------------------------------------------
        // Golden tile
        // ------------------------------------------------------------------------------------------------------------

        [Test]
        public void DecodesTheGoldenRstrChunk()
        {
            TileData td = TileReader.Decode(GoldenFiles.Bytes("golden_w2.ght"));
            JsonElement[] rs = GoldenFiles.Json("golden_w2.json").GetProperty("road_structures").EnumerateArray().ToArray();
            Assert.That(rs.Length, Is.EqualTo(td.Roads.Count));
            Assert.That(td.HasRoadStructures, Is.True);
            Assert.That(td.RoadStructures.Count, Is.EqualTo(rs.Length));
            int decks = 0, shifts = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                JsonElement e = rs[i];
                RoadStructureRecord s = td.RoadStructureOf(i);
                Assert.That((int)s.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()), "kind " + i);
                Assert.That((int)s.Layer, Is.EqualTo(e.GetProperty("layer").GetInt32()), "layer " + i);
                Assert.That((int)s.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()), "flags " + i);
                Assert.That(s.ClearanceM, Is.EqualTo(e.GetProperty("clearance_cm").GetInt32() / 100f).Within(1e-6));
                Assert.That(s.RailingHeightM, Is.EqualTo(e.GetProperty("railing_dm").GetInt32() / 10f).Within(1e-6));
                JsonElement[] roles = e.GetProperty("deck_role").EnumerateArray().ToArray();
                JsonElement[] cm = e.GetProperty("deck_cm").EnumerateArray().ToArray();
                if (roles.Length == 0)
                {
                    Assert.That(s.DeckY, Is.Null, "draped " + i);
                    Assert.That(s.DeckRole, Is.Null);
                    Assert.That(s.HasHeights, Is.False);
                }
                else
                {
                    decks++;
                    Assert.That(s.DeckY.Length, Is.EqualTo(td.Roads[i].PointCount), "one height per road point");
                    for (int k = 0; k < roles.Length; k++)
                    {
                        Assert.That((int)s.DeckRole[k], Is.EqualTo(roles[k].GetInt32()), "role " + i + "/" + k);
                        Assert.That((int)s.RoleAt(k), Is.EqualTo(roles[k].GetInt32()));
                        float y;
                        if (cm[k].ValueKind == JsonValueKind.Null)
                        {
                            Assert.That(float.IsNaN(s.DeckY[k]), Is.True);
                            Assert.That(s.TryHeightAt(k, out y), Is.False);
                        }
                        else
                        {
                            Assert.That(s.DeckY[k], Is.EqualTo((float)(cm[k].GetInt32() / 100.0)), "height " + i + "/" + k);
                            Assert.That(s.TryHeightAt(k, out y), Is.True);
                            Assert.That(y, Is.EqualTo(s.DeckY[k]));
                        }
                    }
                }
                int[] sh = GoldenFiles.Ints(e.GetProperty("shift_cm"));
                if (sh.Length == 0) Assert.That(s.HasShift, Is.False);
                else
                {
                    shifts++;
                    Assert.That(s.CorridorShiftCm, Is.EqualTo(sh));
                    Assert.That(sh.Length, Is.EqualTo(td.RoadAttrs[i].CorridorCount), "one shift per corridor sample");
                }
            }
            Assert.That(decks, Is.GreaterThanOrEqualTo(2));
            Assert.That(shifts, Is.EqualTo(1));
            Assert.That(td.RoadStructures.Any(s => s.Kind == RoadStructureKind.Bridge && s.IsElevated), Is.True);
            Assert.That(td.RoadStructures.Any(s => s.Kind == RoadStructureKind.Underpass && s.Has(RoadStructureFlags.Lowered)), Is.True);
        }

        [Test]
        public void TilesWithoutRstrReadAbsent()
        {
            TileData td = TileReader.Decode(GoldenFiles.Bytes("golden.ght"));
            Assert.That(td.RoadStructures, Is.Empty);
            Assert.That(td.HasRoadStructures, Is.False);
            Assert.That(td.Roads.Count, Is.GreaterThan(0));
            RoadStructureRecord s = td.RoadStructureOf(0);
            Assert.That(s.Kind, Is.EqualTo(RoadStructureKind.None));
            Assert.That(s.Has(RoadStructureFlags.CarAccessible), Is.True);
            Assert.That(s.DeckY, Is.Null);
            Assert.That(s.IsElevated, Is.False);
            Assert.That(s.ShiftAtM(30), Is.EqualTo(0f));
            Assert.That(s.RoleAt(0), Is.EqualTo(DeckPointRole.Draped));
        }

        [Test]
        public void EnumsMatchThePipeline()
        {
            JsonElement enums = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenFiles.RepoRoot, "shared", "enums.json"))).RootElement;
            Assert.That(enums.GetProperty("version").GetInt32(), Is.GreaterThanOrEqualTo(4));
            JsonElement k = enums.GetProperty("enums").GetProperty("RoadStructureKind");
            Assert.That(k.GetProperty("BRIDGE").GetInt32(), Is.EqualTo((int)RoadStructureKind.Bridge));
            Assert.That(k.GetProperty("FLYOVER").GetInt32(), Is.EqualTo((int)RoadStructureKind.Flyover));
            Assert.That(k.GetProperty("UNDERPASS").GetInt32(), Is.EqualTo((int)RoadStructureKind.Underpass));
            Assert.That(k.GetProperty("TUNNEL").GetInt32(), Is.EqualTo((int)RoadStructureKind.Tunnel));
            Assert.That(k.GetProperty("FORD").GetInt32(), Is.EqualTo((int)RoadStructureKind.Ford));
            JsonElement f = enums.GetProperty("enums").GetProperty("RoadStructureFlags");
            Assert.That(f.GetProperty("CAR_ACCESSIBLE").GetInt32(), Is.EqualTo((int)RoadStructureFlags.CarAccessible));
            Assert.That(f.GetProperty("FOOT_OVERBRIDGE").GetInt32(), Is.EqualTo((int)RoadStructureFlags.FootOverbridge));
            Assert.That(f.GetProperty("OVER_ROAD").GetInt32(), Is.EqualTo((int)RoadStructureFlags.OverRoad));
            Assert.That(enums.GetProperty("enums").GetProperty("BuildingFrontFlags").GetProperty("TRIMMED_FOR_ROAD").GetInt32(),
                        Is.EqualTo((int)BuildingFrontFlags.TrimmedForRoad));
            Assert.That(enums.GetProperty("flag_enums").EnumerateArray().Select(v => v.GetString()), Has.Member("RoadStructureFlags"));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Hand-built chunks
        // ------------------------------------------------------------------------------------------------------------

        private sealed class W
        {
            private readonly List<byte> _b = new List<byte>();

            public W U8(int v)
            {
                _b.Add((byte)v);
                return this;
            }

            public W U16(int v)
            {
                _b.Add((byte)v);
                _b.Add((byte)(v >> 8));
                return this;
            }

            public W U32(uint v)
            {
                for (int i = 0; i < 4; i++) _b.Add((byte)(v >> (8 * i)));
                return this;
            }

            public W Varint(ulong v)
            {
                while (v >= 0x80)
                {
                    _b.Add((byte)(v | 0x80));
                    v >>= 7;
                }
                _b.Add((byte)v);
                return this;
            }

            public W Svarint(long v)
            {
                return Varint((ulong)((v << 1) ^ (v >> 63)));
            }

            /// <summary>An RSTR deck code: 0 draped, else ((zigzag(dy) &lt;&lt; 1) | ramp) + 1.</summary>
            public W Deck(long dy, bool ramp)
            {
                ulong zz = (ulong)((dy << 1) ^ (dy >> 63));
                return Varint(((zz << 1) | (ramp ? 1UL : 0UL)) + 1);
            }

            public byte[] Bytes()
            {
                return _b.ToArray();
            }
        }

        private static byte[] Tile(params KeyValuePair<string, byte[]>[] chunks)
        {
            int headerAndTable = Ght.HeaderSize + Ght.ChunkEntrySize * chunks.Length;
            var payload = new List<byte>();
            var entries = new W();
            foreach (KeyValuePair<string, byte[]> c in chunks.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                byte[] stored = new W().U32((uint)c.Value.Length).Bytes().Concat(c.Value).ToArray();
                entries.U32(Ght.FourCC(c.Key)).U8(0).U8(0).U16(0).U32((uint)(headerAndTable + payload.Count)).U32((uint)stored.Length);
                payload.AddRange(stored);
            }
            byte[] p = payload.ToArray();
            return new W().U32(Ght.Magic).U16(1).U16(Ght.FlagHasDetail).U8(10).U8(0).U16(0).U32(516).U32(161).U32(2)
                          .U16(chunks.Length).U16(0).U32(Hashes.Crc32(p)).Bytes().Concat(entries.Bytes()).Concat(p).ToArray();
        }

        private static KeyValuePair<string, byte[]> C(string fourcc, byte[] body)
        {
            return new KeyValuePair<string, byte[]>(fourcc, body);
        }

        /// <summary>One three-point road.</summary>
        private static byte[] Road()
        {
            return new W().Varint(1).Varint(7).U8(3).U8(1).U8(0).U8(0).U8(0).U8(0).U8(0).U8(1).Varint(0).U8(127).Varint(0).Varint(0)
                          .Varint(3).Svarint(1000).Svarint(1000).Svarint(2000).Svarint(0).Svarint(2000).Svarint(0).Bytes();
        }

        private static byte[] Ratr(int corridor)
        {
            var w = new W().Varint(1).U8(2).U8(0).U8(0).U8(0).U8(0).U8(0).Varint(0).Varint(0).Varint((ulong)corridor);
            for (int k = 0; k < corridor; k++) w.Varint(48);
            return w.Bytes();
        }

        /// <summary>A bridge: draped start, a ramp at 1300.25 m, a deck at 1301.00 m; two shifts.</summary>
        private static W Rstr(int deckPoints = 3, int shifts = 2)
        {
            var w = new W().Varint(1).U8((int)RoadStructureKind.Bridge).U8(1).U8(0x07).Varint(0).U8(11).Varint((ulong)deckPoints);
            if (deckPoints > 0) w.Varint(0).Deck(130025, true).Deck(75, false);
            for (int k = 3; k < deckPoints; k++) w.Varint(0);
            w.Varint((ulong)shifts);
            for (int k = 0; k < shifts; k++) w.Svarint(k == 0 ? -35 : 120);
            return w;
        }

        [Test]
        public void HandBuiltRstrDecodes()
        {
            TileData td = TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)), C("RSTR", Rstr().Bytes())));
            RoadStructureRecord s = td.RoadStructureOf(0);
            Assert.That(s.Kind, Is.EqualTo(RoadStructureKind.Bridge));
            Assert.That(s.Layer, Is.EqualTo(1));
            Assert.That(s.Has(RoadStructureFlags.CarAccessible) && s.Has(RoadStructureFlags.WaterCrossing) &&
                        s.Has(RoadStructureFlags.DeckFromTags), Is.True);
            Assert.That(s.RailingHeightM, Is.EqualTo(1.1f).Within(1e-6));
            Assert.That(s.IsElevated, Is.True);
            Assert.That(s.DeckRole, Is.EqualTo(new[] { DeckPointRole.Draped, DeckPointRole.Ramp, DeckPointRole.Deck }));
            Assert.That(float.IsNaN(s.DeckY[0]), Is.True);
            Assert.That(s.DeckY[1], Is.EqualTo(1300.25f).Within(1e-3));
            Assert.That(s.DeckY[2], Is.EqualTo(1301.00f).Within(1e-3));
            Assert.That(s.CorridorShiftCm, Is.EqualTo(new[] { -35, 120 }));
            Assert.That(s.ShiftAtM(0), Is.EqualTo(-0.35f).Within(1e-6));
            Assert.That(s.ShiftAtM(10), Is.EqualTo(0.425f).Within(1e-6));
            Assert.That(s.ShiftAtM(500), Is.EqualTo(1.2f).Within(1e-6));
            Assert.That(s.DeckDepth, Is.EqualTo(RoadStructureRecord.DeckDepthM));

            // A draped record with no shifts, and a foot overbridge's thinner deck.
            byte[] plain = new W().Varint(1).U8(0).U8(0).U8((int)(RoadStructureFlags.FootOverbridge)).Varint(560).U8(0).Varint(0).Varint(0).Bytes();
            s = TileReader.Decode(Tile(C("ROAD", Road()), C("RSTR", plain))).RoadStructureOf(0);
            Assert.That(s.DeckY, Is.Null);
            Assert.That(s.HasShift, Is.False);
            Assert.That(s.ClearanceM, Is.EqualTo(5.6f).Within(1e-6));
            Assert.That(s.DeckDepth, Is.EqualTo(RoadStructureRecord.FootDeckDepthM));
        }

        [Test]
        public void RstrCorruptionIsRejected()
        {
            // Record count must equal the ROAD count.
            byte[] two = new W().Varint(2).U8(0).U8(0).U8(1).Varint(0).U8(0).Varint(0).Varint(0)
                                .U8(0).U8(0).U8(1).Varint(0).U8(0).Varint(0).Varint(0).Bytes();
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RSTR", two))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("RSTR", Rstr(3, 0).Bytes()))));
            // Deck points must equal the road's points (context included).
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RSTR", Rstr(4, 0).Bytes()))));
            // Shifts must match the RATR corridor samples (and need RATR).
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(3)), C("RSTR", Rstr().Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RSTR", Rstr().Bytes()))));
            // Trailing bytes.
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)),
                                                                             C("RSTR", Rstr().U8(0).Bytes()))));
            // Truncated.
            byte[] full = Rstr().Bytes();
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)),
                                                                             C("RSTR", full.Take(full.Length - 1).ToArray()))));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Sample region (shared/sample-regions/kathmandu_core)
        // ------------------------------------------------------------------------------------------------------------

        [Test]
        public void SampleRegionCarriesStructuresCorridorsAndTrims()
        {
            int tiles = 0, bridgesOverWater = 0, footOverbridges = 0, underpasses = 0, trimmed = 0, galliLike = 0;
            int corridorSamples = 0, belowMin = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Roads.Count == 0) continue;
                tiles++;
                Assert.That(t.HasRoadStructures, Is.True, "RSTR in " + id);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadStructureRecord s = t.RoadStructures[i];
                    RoadAttrRecord a = t.RoadAttrOf(i);
                    foreach (int dm in a.CorridorDm)
                    {
                        corridorSamples++;
                        if (dm < 48) belowMin++;
                    }
                    if (a.CorridorDm.Any(dm => dm < 48))
                        Assert.That(s.Has(RoadStructureFlags.Squeezed), Is.True, "corridor under 4.8 m only where squeezed");
                    if (s.HasShift) Assert.That(s.CorridorShiftCm.Length, Is.EqualTo(a.CorridorCount));
                    if (s.Kind == RoadStructureKind.Bridge && s.Has(RoadStructureFlags.WaterCrossing) && s.IsElevated) bridgesOverWater++;
                    if (s.Has(RoadStructureFlags.FootOverbridge) && s.IsElevated) footOverbridges++;
                    if (s.Kind == RoadStructureKind.Underpass && s.ClearanceM > 0)
                    {
                        underpasses++;
                        Assert.That(s.ClearanceM, Is.GreaterThanOrEqualTo(5.5f - 0.05f), "underpass clearance " + t.Roads[i].OsmWayId);
                    }
                    if (!s.Has(RoadStructureFlags.CarAccessible) && RoadWidthClassAllowsCars(t.Roads[i].RoadClass)) galliLike++;
                    if (s.DeckY != null)
                    {
                        Assert.That(s.DeckY.Length, Is.EqualTo(t.Roads[i].PointCount));
                        foreach (float y in s.DeckY)
                            if (!float.IsNaN(y)) Assert.That(y, Is.InRange(1000f, 2000f), "deck height in the valley");
                    }
                }
                for (int b = 0; b < t.Buildings.Count; b++)
                    if (t.BuildingFrontOf(b).Has(BuildingFrontFlags.TrimmedForRoad)) trimmed++;
            }
            Assert.That(tiles, Is.GreaterThan(30));
            Assert.That(corridorSamples, Is.GreaterThan(50000));
            Assert.That(belowMin, Is.LessThan(corridorSamples / 200), "corridors under 4.8 m are rare (squeezed only)");
            Assert.That(bridgesOverWater, Is.GreaterThan(20), "Bagmati, Bishnumati and Dhobi Khola bridges");
            Assert.That(footOverbridges, Is.GreaterThan(5), "foot overbridges over the arterials");
            Assert.That(underpasses, Is.GreaterThan(5));
            Assert.That(trimmed, Is.GreaterThan(1000), "buildings trimmed back to the corridors");
            Assert.That(galliLike, Is.GreaterThan(10), "gallis and narrow ways are not car-accessible");
        }

        private static bool RoadWidthClassAllowsCars(RoadClass c)
        {
            return c == RoadClass.Residential || c == RoadClass.Unclassified || c == RoadClass.Service || c == RoadClass.LivingStreet;
        }
    }
}
