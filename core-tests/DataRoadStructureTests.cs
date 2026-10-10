using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;
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
                    Assert.That(RoadStructureRecord.ShiftCountFits(sh.Length, td.RoadAttrs[i].CorridorCount), Is.True,
                                "a shift every 5 m along the corridor samples' length");
                }
            }
            Assert.That(decks, Is.GreaterThanOrEqualTo(2));
            Assert.That(shifts, Is.EqualTo(1));
            Assert.That(td.RoadStructures.Any(s => s.Kind == RoadStructureKind.Bridge && s.IsElevated), Is.True);
            Assert.That(td.RoadStructures.Any(s => s.Kind == RoadStructureKind.Underpass && s.Has(RoadStructureFlags.Lowered)), Is.True);
            RoadStructureRecord passage = td.RoadStructures.First(s => s.Kind == RoadStructureKind.Passage);
            Assert.That(passage.ClearanceM, Is.EqualTo(4.5f).Within(1e-6), "a gateway keeps RoadClearance.MinOverheadClearanceM");
            Assert.That(passage.HasHeights, Is.False);
            Assert.That(td.FinalCorridors, Is.True, "META ratr_corridor = final");
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
            Assert.That(td.FinalCorridors, Is.False, "a stage-1 tile: RATR corridors are the space between buildings");
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
            Assert.That(k.GetProperty("PASSAGE").GetInt32(), Is.EqualTo((int)RoadStructureKind.Passage));
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

        /// <summary>A bridge: draped start, a ramp at 1300.25 m, a deck at 1301.00 m; five shifts (every 5 m, the
        /// 20 m of two RATR corridor samples).</summary>
        private static W Rstr(int deckPoints = 3, int shifts = 5)
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
            Assert.That(s.CorridorShiftCm, Is.EqualTo(new[] { -35, 120, 120, 120, 120 }));
            Assert.That(s.ShiftAtM(0), Is.EqualTo(-0.35f).Within(1e-6));
            Assert.That(s.ShiftAtM(2.5), Is.EqualTo(0.425f).Within(1e-6));
            Assert.That(s.ShiftAtM(12.5), Is.EqualTo(1.2f).Within(1e-6));
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
            // Shifts must cover the RATR corridor samples' length every 5 m (and need RATR).
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(3)), C("RSTR", Rstr().Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)), C("RSTR", Rstr(3, 4).Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)), C("RSTR", Rstr(3, 9).Bytes()))));
            Assert.DoesNotThrow(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)), C("RSTR", Rstr(3, 8).Bytes()))));
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RSTR", Rstr().Bytes()))));
            // Trailing bytes.
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)),
                                                                             C("RSTR", Rstr().U8(0).Bytes()))));
            // Truncated.
            byte[] full = Rstr().Bytes();
            Assert.Throws<InvalidDataException>(() => TileReader.Decode(Tile(C("ROAD", Road()), C("RATR", Ratr(2)),
                                                                             C("RSTR", full.Take(full.Length - 1).ToArray()))));
        }

        private static byte[] Meta(string json)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
            return new W().Varint((ulong)utf8.Length).Bytes().Concat(utf8).ToArray();
        }

        [Test]
        public void MetaMarksFinalCorridors()
        {
            Assert.That(TileReader.Decode(Tile(C("META", Meta("{\"ratr_corridor\":\"final\",\"region\":\"x\"}")))).FinalCorridors, Is.True);
            Assert.That(TileReader.Decode(Tile(C("META", Meta("{\"region\":\"x\", \"ratr_corridor\" : \"final\"}")))).FinalCorridors, Is.True);
            Assert.That(TileReader.Decode(Tile(C("META", Meta("{\"ratr_corridor\":\"finalish\"}")))).FinalCorridors, Is.False);
            Assert.That(TileReader.Decode(Tile(C("META", Meta("{\"note\":\"ratr_corridor\",\"region\":\"final\"}")))).FinalCorridors, Is.False);
            Assert.That(TileReader.Decode(Tile(C("META", Meta("{\"region\":\"x\"}")))).FinalCorridors, Is.False);
            Assert.That(TileReader.Decode(Tile(C("ROAD", Road()))).FinalCorridors, Is.False, "no META: a stage-1 reading");
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
                    if (s.HasShift) Assert.That(RoadStructureRecord.ShiftCountFits(s.CorridorShiftCm.Length, a.CorridorCount), Is.True);
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

        private static bool IsFootClass(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Cycleway || c == RoadClass.Bridleway;
        }

        /// <summary>Class ramp grade (pipeline structures.GRADE).</summary>
        private static double ClassGrade(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                case RoadClass.Primary:
                    return 0.05;
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                    return 0.06;
                case RoadClass.Track:
                    return 0.10;
                default:
                    return 0.08;
            }
        }

        /// <summary>The terrain the pipeline solved against: the tile's quantised HGHT samples, bilinear inside the cell
        /// (tile-local metres).</summary>
        private static double Terrain(TileData t, double x, double z)
        {
            int n = t.HeightsN;
            double step = t.Tile.Size / (n - 1);
            double fi = x / step, fj = z / step;
            int i0 = Math.Max(0, Math.Min(n - 2, (int)Math.Floor(fi)));
            int j0 = Math.Max(0, Math.Min(n - 2, (int)Math.Floor(fj)));
            double u = Math.Max(0, Math.Min(1, fi - i0)), v = Math.Max(0, Math.Min(1, fj - j0));
            return (t.HeightAt(j0, i0) * (1 - u) + t.HeightAt(j0, i0 + 1) * u) * (1 - v) +
                   (t.HeightAt(j0 + 1, i0) * (1 - u) + t.HeightAt(j0 + 1, i0 + 1) * u) * v;
        }

        /// <summary>A road piece's points (tile metres) and surface heights (deck or terrain).</summary>
        private static void Profile(TileData t, int road, out double[] x, out double[] z, out double[] y)
        {
            RoadRecord r = t.Roads[road];
            RoadStructureRecord s = t.RoadStructureOf(road);
            int n = r.PointCount;
            x = new double[n];
            z = new double[n];
            y = new double[n];
            for (int k = 0; k < n; k++)
            {
                x[k] = r.Points[2 * k] / 100.0;
                z[k] = r.Points[2 * k + 1] / 100.0;
                float d;
                y[k] = s.TryHeightAt(k, out d) ? d : Terrain(t, x[k], z[k]);
            }
        }

        private static bool Inside(TileData t, double x, double z)
        {
            return x >= 0 && z >= 0 && x <= t.Tile.Size && z <= t.Tile.Size;
        }

        /// <summary>
        /// W2 detail-pass review: no road steps. Along every motor-road piece with heights, the offset from the terrain
        /// changes by at most <see cref="RoadStructureRecord.SteepestRampGrade"/> per metre (+0.1 m), and a deck's
        /// height by at most that or the terrain's own fall; points that two pieces share in a tile carry one height
        /// (a difference of 6 m or more is a grade separation whose stations coincide at the crossing, not a step).
        /// </summary>
        [Test]
        public void SampleRoadHeightsNeverStepAndAgreeAtSharedPoints()
        {
            int pairs = 0, aboveClassGrade = 0, shared = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Roads.Count == 0 || t.HeightsN < 2) continue;
                var at = new Dictionary<long, KeyValuePair<double, ulong>>();
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    RoadStructureRecord s = t.RoadStructureOf(i);
                    double[] x, z, y;
                    Profile(t, i, out x, out z, out y);
                    for (int k = 0; k < x.Length; k++)
                    {
                        if (!Inside(t, x[k], z[k])) continue;
                        long key = ((long)r.Points[2 * k] << 32) ^ (uint)r.Points[2 * k + 1];
                        KeyValuePair<double, ulong> prev;
                        if (!at.TryGetValue(key, out prev))
                        {
                            at[key] = new KeyValuePair<double, ulong>(y[k], r.OsmWayId);
                            continue;
                        }
                        double dy = Math.Abs(prev.Key - y[k]);
                        if (dy >= 6.0) continue;
                        shared++;
                        Assert.That(dy, Is.LessThanOrEqualTo(0.05), id + ": w" + r.OsmWayId + " and w" + prev.Value + " meet at different heights");
                    }
                    if (s.DeckY == null || IsFootClass(r.RoadClass)) continue;
                    for (int k = 0; k + 1 < x.Length; k++)
                    {
                        if (!Inside(t, x[k], z[k]) || !Inside(t, x[k + 1], z[k + 1])) continue;
                        DeckPointRole ra = s.RoleAt(k), rb = s.RoleAt(k + 1);
                        if (ra == DeckPointRole.Draped && rb == DeckPointRole.Draped) continue;
                        double L = Math.Sqrt((x[k + 1] - x[k]) * (x[k + 1] - x[k]) + (z[k + 1] - z[k]) * (z[k + 1] - z[k]));
                        if (L < 0.05) continue;
                        double ta = Terrain(t, x[k], z[k]), tb = Terrain(t, x[k + 1], z[k + 1]);
                        pairs++;
                        string what = id + ": w" + r.OsmWayId + " point " + k;
                        if (ra == DeckPointRole.Deck && rb == DeckPointRole.Deck)
                        {
                            Assert.That(Math.Abs(y[k + 1] - y[k]), Is.LessThanOrEqualTo(Math.Max(RoadStructureRecord.SteepestRampGrade * L, Math.Abs(tb - ta)) + 0.1), what + " (deck)");
                            continue;
                        }
                        double step = Math.Abs((y[k + 1] - tb) - (y[k] - ta));
                        Assert.That(step, Is.LessThanOrEqualTo(RoadStructureRecord.SteepestRampGrade * L + 0.1), what + " steps");
                        if (step > ClassGrade(r.RoadClass) * L + 0.1) aboveClassGrade++;
                    }
                }
            }
            Assert.That(pairs, Is.GreaterThan(1000), "ramp and deck segments checked");
            Assert.That(shared, Is.GreaterThan(1000), "junction points checked");
            Assert.That(aboveClassGrade, Is.LessThan(pairs / 20), "steeper than the class grade only on a few riverside connectors");
        }

        /// <summary>
        /// Decision 3 under the deck's whole width: wherever a deck passes over another road in the sample, every point
        /// of the lower road within half the deck road's corridor of its centreline keeps 5.5 m (−5 cm) to the deck's
        /// underside (deck surface − <see cref="RoadStructureRecord.DeckDepthFor"/>).
        /// </summary>
        [Test]
        public void SampleClearanceHoldsUnderTheWholeDeck()
        {
            int crossings = 0, samples = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Roads.Count == 0 || t.HeightsN < 2) continue;
                for (int u = 0; u < t.Roads.Count; u++)
                {
                    RoadStructureRecord su = t.RoadStructureOf(u);
                    if (su.DeckY == null) continue;
                    double[] ux, uz, uy;
                    Profile(t, u, out ux, out uz, out uy);
                    var tu = t;
                    Func<int, double, double, double, double> hu = (k, f, px, pz) => SurfaceAt(tu, su, k, f, uy, px, pz);
                    double depth = su.DeckDepthFor(t.Roads[u].RoadClass);
                    int[] cdm = t.RoadAttrOf(u).CorridorDm;
                    double half = 0.5 * (cdm.Length > 0 ? cdm.Min() / 10.0 : 4.8);
                    for (int l = 0; l < t.Roads.Count; l++)
                    {
                        if (l == u) continue;
                        double[] lx, lz, ly;
                        Profile(t, l, out lx, out lz, out ly);
                        RoadStructureRecord sl = t.RoadStructureOf(l);
                        for (int a = 0; a + 1 < ux.Length; a++)
                        {
                            if (su.RoleAt(a) != DeckPointRole.Deck && su.RoleAt(a + 1) != DeckPointRole.Deck) continue;
                            for (int b = 0; b + 1 < lx.Length; b++)
                            {
                                double sa, sb;
                                if (!Intersect(ux[a], uz[a], ux[a + 1], uz[a + 1], lx[b], lz[b], lx[b + 1], lz[b + 1], out sa, out sb)) continue;
                                double cx = lx[b] + sb * (lx[b + 1] - lx[b]), cz = lz[b] + sb * (lz[b + 1] - lz[b]);
                                if (!Inside(t, cx, cz)) continue;
                                double yu = hu(a, sa, cx, cz), yl = SurfaceAt(t, sl, b, sb, ly, cx, cz);
                                if (yu - depth - yl < 2.0) continue; // a junction or an at-grade crossing, not a grade separation
                                crossings++;
                                // Walk the lower road both ways from the crossing while it stays under the deck road's corridor.
                                for (int dir = -1; dir <= 1; dir += 2)
                                {
                                    int seg = b;
                                    double f = sb;
                                    for (double walked = 0; walked <= 60.0; walked += 0.5)
                                    {
                                        double px, pz;
                                        if (!WalkTo(lx, lz, ref seg, ref f, dir, walked == 0 ? 0 : 0.5, out px, out pz)) break;
                                        double py = SurfaceAt(t, sl, seg, f, ly, px, pz);
                                        double qy, dist;
                                        bool interior;
                                        if (!Nearest(ux, uz, hu, px, pz, out dist, out qy, out interior)) continue;
                                        if (dist > half) continue;
                                        if (!interior) continue; // beyond the deck road's piece: its approach way carries it
                                        samples++;
                                        Assert.That(qy - depth - py, Is.GreaterThanOrEqualTo(RoadClearance.MinUnderpassClearanceM - 0.05),
                                                    id + ": w" + t.Roads[l].OsmWayId + " under w" + t.Roads[u].OsmWayId + " at " + px.ToString("F1") + "," + pz.ToString("F1"));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Assert.That(crossings, Is.GreaterThan(20), "decks over roads in the sample");
            Assert.That(samples, Is.GreaterThan(crossings * 5), "lower-road points under the decks");
        }

        private static bool Intersect(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, out double s, out double u)
        {
            s = u = 0;
            double rx = bx - ax, rz = bz - az, qx = dx - cx, qz = dz - cz;
            double den = rx * qz - rz * qx;
            if (Math.Abs(den) < 1e-12) return false;
            double wx = cx - ax, wz = cz - az;
            s = (wx * qz - wz * qx) / den;
            u = (wx * rz - wz * rx) / den;
            return s >= 0 && s <= 1 && u >= 0 && u <= 1;
        }

        /// <summary>The surface height at fraction <paramref name="f"/> of segment <paramref name="k"/> of a road piece
        /// (point (px, pz)): the terrain where both ends are draped, else linear between the two point heights (a
        /// point between two vertices is draped only when both are, DATA_FORMATS 1.15).</summary>
        private static double SurfaceAt(TileData t, RoadStructureRecord s, int k, double f, double[] y, double px, double pz)
        {
            if (s.RoleAt(k) == DeckPointRole.Draped && s.RoleAt(k + 1) == DeckPointRole.Draped) return Terrain(t, px, pz);
            return y[k] + f * (y[k + 1] - y[k]);
        }

        /// <summary>Move <paramref name="step"/> metres along a polyline from (seg, f) in direction dir; false past an end.</summary>
        private static bool WalkTo(double[] x, double[] z, ref int seg, ref double f, int dir, double step, out double px, out double pz)
        {
            px = pz = 0;
            double left = step;
            while (true)
            {
                double L = Math.Sqrt((x[seg + 1] - x[seg]) * (x[seg + 1] - x[seg]) + (z[seg + 1] - z[seg]) * (z[seg + 1] - z[seg]));
                double room = L <= 1e-9 ? 0 : (dir > 0 ? (1 - f) * L : f * L);
                if (left <= room || L <= 1e-9 && left <= 0)
                {
                    if (L > 1e-9) f += dir * left / L;
                    break;
                }
                left -= room;
                if (dir > 0)
                {
                    if (seg + 2 >= x.Length) return false;
                    seg++;
                    f = 0;
                }
                else
                {
                    if (seg == 0) return false;
                    seg--;
                    f = 1;
                }
            }
            px = x[seg] + f * (x[seg + 1] - x[seg]);
            pz = z[seg] + f * (z[seg + 1] - z[seg]);
            return true;
        }

        /// <summary>Nearest point of a polyline: distance, interpolated height, and whether it lies inside the polyline
        /// (not clamped to its first or last point).</summary>
        private static bool Nearest(double[] x, double[] z, Func<int, double, double, double, double> height, double px, double pz, out double dist, out double qy, out bool interior)
        {
            dist = double.MaxValue;
            qy = 0;
            interior = false;
            for (int k = 0; k + 1 < x.Length; k++)
            {
                double ex = x[k + 1] - x[k], ez = z[k + 1] - z[k];
                double l2 = ex * ex + ez * ez;
                double f = l2 <= 1e-12 ? 0 : ((px - x[k]) * ex + (pz - z[k]) * ez) / l2;
                bool clamped = (f < 0 && k == 0) || (f > 1 && k + 2 == x.Length);
                f = Math.Max(0, Math.Min(1, f));
                double qx = x[k] + f * ex, qz = z[k] + f * ez;
                double d = Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz));
                if (d < dist)
                {
                    dist = d;
                    qy = height(k, f, qx, qz);
                    interior = !clamped;
                }
            }
            return x.Length >= 2;
        }
    }
}
