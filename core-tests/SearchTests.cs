using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using Ghumante.Core.Search;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class SearchTests
    {
        private static SearchIndexData Index()
        {
            return SearchIndexReader.Read(GoldenFiles.Bytes("golden.ghsi"));
        }

        [Test]
        public void StopWordsAndRankConstantsMatchPython()
        {
            JsonElement root = GoldenFiles.Json("golden_search.json");
            var expected = root.GetProperty("stop_words").EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.That(SearchEngine.StopWordsFolded.OrderBy(w => w, StringComparer.Ordinal).ToList(), Is.EqualTo(expected));
            JsonElement rb = root.GetProperty("rank_bonus");
            Assert.That(SearchEngine.RankBonusLandmark, Is.EqualTo(rb.GetProperty("landmark").GetInt32()));
            Assert.That(SearchEngine.RankBonusHeritage, Is.EqualTo(rb.GetProperty("heritage").GetInt32()));
            Assert.That(SearchEngine.HeritageKindMin, Is.EqualTo(rb.GetProperty("heritage_kinds")[0].GetInt32()));
            Assert.That(SearchEngine.HeritageKindMax, Is.EqualTo(rb.GetProperty("heritage_kinds")[1].GetInt32()));
            Assert.That(SearchEngine.TokenMatchScore, Is.EqualTo(rb.GetProperty("token_match").GetInt32()));
        }

        [Test]
        public void ReaderMatchesPythonDecode()
        {
            SearchIndexData idx = Index();
            JsonElement x = GoldenFiles.Json("golden_search.json");
            Assert.That(idx.Entries.Length, Is.EqualTo(x.GetProperty("entry_count").GetInt32()));
            Assert.That(idx.Keys.Length, Is.EqualTo(x.GetProperty("key_count").GetInt32()));
            var names = x.GetProperty("names").EnumerateArray().ToArray();
            Assert.That(idx.Names.Length, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; i++) GoldenFiles.AssertName(names[i], idx.Names[i], "name " + i);

            var keys = x.GetProperty("keys").EnumerateArray().ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(idx.Keys[i], Is.EqualTo(keys[i][0].GetString()));
                Assert.That(idx.KeyEntries[i], Is.EqualTo(keys[i][1].GetInt32()));
            }

            var entries = x.GetProperty("entries").EnumerateArray().ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                JsonElement e = entries[i];
                SearchEntry s = idx.Entries[i];
                GoldenFiles.AssertName(e.GetProperty("name"), s.Name, "entry name " + i);
                GoldenFiles.AssertName(e.GetProperty("district"), s.District, "district " + i);
                GoldenFiles.AssertName(e.GetProperty("province"), s.Province, "province " + i);
                Assert.That(s.Kind, Is.EqualTo(e.GetProperty("kind").GetInt32()));
                Assert.That((int)s.Importance, Is.EqualTo(e.GetProperty("importance").GetInt32()));
                Assert.That((int)s.Flags, Is.EqualTo(e.GetProperty("flags").GetInt32()));
                Assert.That(s.X, Is.EqualTo(e.GetProperty("x").GetDouble()));
                Assert.That(s.Z, Is.EqualTo(e.GetProperty("z").GetDouble()));
                Assert.That(s.Lon, Is.EqualTo(e.GetProperty("lon").GetDouble()));
                Assert.That(s.Lat, Is.EqualTo(e.GetProperty("lat").GetDouble()));
                Assert.That(s.OsmRef, Is.EqualTo(e.GetProperty("osm_ref").GetUInt32()));
            }
            Assert.That(idx.Entries.Any(e => e.IsPlace && e.PlaceKind == PlaceKind.City), Is.True);
            Assert.That(idx.Entries.Any(e => e.TransportHub && e.PoiKind == PoiKind.Airport), Is.True);
            Assert.That(idx.Entries.Any(e => e.Landmark && e.Discoverable), Is.True);
        }

        [Test]
        public void FoldAndRomanizeMatchPython()
        {
            foreach (JsonElement c in GoldenFiles.Json("golden_search.json").GetProperty("fold_cases").EnumerateArray())
            {
                string t = c.GetProperty("text").GetString();
                Assert.That(Romanize.Apply(t), Is.EqualTo(c.GetProperty("romanize").GetString()), "romanize " + t);
                Assert.That(Fold.Clean(t), Is.EqualTo(c.GetProperty("clean").GetString()), "clean " + t);
                Assert.That(Fold.Apply(t), Is.EqualTo(c.GetProperty("fold").GetString()), "fold " + t);
                Assert.That(Fold.Apply(Fold.Apply(t)), Is.EqualTo(Fold.Apply(t)), "idempotent " + t);
            }
        }

        [Test]
        public void FoldExamplesFromTheSpec()
        {
            Assert.That(Fold.Apply("Kathmandu"), Is.EqualTo("katmandu"));
            Assert.That(Fold.Apply("काठमाडौं"), Is.EqualTo("katamadaun"));
            Assert.That(Fold.Apply("बौद्ध"), Is.EqualTo(Fold.Apply("Boudha")));
            Assert.That(Fold.Apply("स्वयम्भू"), Is.EqualTo("sbayambu"));
            Assert.That(Fold.Apply("ठमेल"), Is.EqualTo("tamel"));
            Assert.That(Fold.Apply("पोखरा"), Is.EqualTo("pokara"));
            Assert.That(Fold.Apply(null), Is.EqualTo(""));
            Assert.That(Romanize.HasDevanagari("abc"), Is.False);
        }

        [Test]
        public void OsaDistanceMatchesPython()
        {
            foreach (JsonElement c in GoldenFiles.Json("golden_search.json").GetProperty("osa_cases").EnumerateArray())
                Assert.That(SearchEngine.OsaDistance(c.GetProperty("a").GetString(), c.GetProperty("b").GetString(), c.GetProperty("max_d").GetInt32()),
                            Is.EqualTo(c.GetProperty("d").GetInt32()), c.ToString());
        }

        [Test]
        public void SearchResultsIdenticalToPython()
        {
            var engine = new SearchEngine(Index());
            int nonEmpty = 0;
            foreach (JsonElement q in GoldenFiles.Json("golden_search.json").GetProperty("queries").EnumerateArray())
            {
                string query = q.GetProperty("query").GetString();
                int limit = q.GetProperty("limit").GetInt32();
                Assert.That(Fold.Apply(query), Is.EqualTo(q.GetProperty("folded").GetString()), "fold " + query);
                List<SearchResult> got = engine.Search(query, limit);
                var expected = q.GetProperty("results").EnumerateArray().ToArray();
                Assert.That(got.Select(r => r.EntryIndex), Is.EqualTo(expected.Select(e => e.GetProperty("entry").GetInt32())), "entries for '" + query + "'");
                Assert.That(got.Select(r => r.ScoreInt), Is.EqualTo(expected.Select(e => e.GetProperty("score_int").GetInt64())), "scores for '" + query + "'");
                Assert.That(got.Select(r => r.Score), Is.EqualTo(expected.Select(e => e.GetProperty("score").GetDouble())));
                Assert.That(got.Select(r => r.Entry.OsmRef), Is.EqualTo(expected.Select(e => e.GetProperty("osm_ref").GetUInt32())));
                if (expected.Length > 0) nonEmpty++;
            }
            Assert.That(nonEmpty, Is.GreaterThan(30));
        }

        [Test]
        public void ExpectedTopHits()
        {
            var engine = new SearchEngine(Index());
            Assert.That(engine.Search("काठमाडौं")[0].Entry.Name.En, Is.EqualTo("Kathmandu"));
            Assert.That(engine.Search("Bouddha")[0].Entry.DisplayName, Is.EqualTo("Baudha").Or.EqualTo("Boudhanath"));
            Assert.That(engine.Search("evrest")[0].Entry.Name.Default, Is.EqualTo("Mount Everest"));
            Assert.That(engine.Search("thamel", 0), Is.Empty);
            Assert.That(engine.Search("   "), Is.Empty);
        }

        [Test]
        public void RejectsCorruptIndex()
        {
            byte[] good = GoldenFiles.Bytes("golden.ghsi");
            byte[] b = (byte[])good.Clone();
            b[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => SearchIndexReader.Read(b));
            b = (byte[])good.Clone();
            b[4] = 2;
            Assert.Throws<InvalidDataException>(() => SearchIndexReader.Read(b));
            Assert.Throws<InvalidDataException>(() => SearchIndexReader.Read(good.Take(good.Length - 1).ToArray()));
            Assert.Throws<InvalidDataException>(() => SearchIndexReader.Read(good.Concat(new byte[] { 0 }).ToArray()));
            Assert.Throws<InvalidDataException>(() => SearchIndexReader.Read(new byte[8]));
        }

        [Test]
        public void Utf8Ordering()
        {
            Assert.That(SearchIndexReader.CompareUtf8("a", "b"), Is.LessThan(0));
            Assert.That(SearchIndexReader.CompareUtf8("ab", "a"), Is.GreaterThan(0));
            Assert.That(SearchIndexReader.CompareUtf8("�", "\U0001F600"), Is.LessThan(0)); // BMP before astral in UTF-8
        }
    }
}
