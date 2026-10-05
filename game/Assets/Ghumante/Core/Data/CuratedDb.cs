using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Save;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// One heritage record of the curated DB (W2_DESIGN 9.4, D12): the hero's id, its OSM anchor, compound and hide
    /// references, the generator kind and the parameters that differ from the generator defaults. Unknown numbers are
    /// NaN (floats) or 0 (counts); <see cref="Attrs"/> keeps the raw attribute object for generator-specific keys.
    /// </summary>
    public sealed class HeritageRecord
    {
        public string Id;
        public string Name;

        public HeritageKind Kind;

        /// <summary>OSM anchor as written in the DB: <c>w56688295</c>, <c>n3569849497</c> or <c>r4624856</c>.</summary>
        public string Osm;

        public string Compound;
        public string[] Hide = new string[0];

        public int Tiers;
        public int PlinthLevels;
        public int Doors;
        public float HeightM = float.NaN;
        public float YawDeg = float.NaN;

        /// <summary>Plan width and depth (m) when the anchor has no footprint.</summary>
        public float PlanWM = float.NaN;

        public float PlanDM = float.NaN;

        public HeritageFinish Finish;

        public string Guardians;
        public EntryRule Entry;
        public KoraDirection Kora;
        public HeritageFlags Flags;

        /// <summary>Anchor position in WGS84 for anchors without OSM geometry (NaN when unset).</summary>
        public double Lon = double.NaN, Lat = double.NaN;

        public JsonObject Attrs;

        // Compiled .ghcd fields (DATA_FORMATS 7); zero / NaN / empty when the record came from elsewhere.
        public int Stage;
        public string NameNe, Deity, Provenance, Review;

        /// <summary>The OSM anchor as <c>(osm_id &lt;&lt; 2) | type</c>, 0 = manual position.</summary>
        public ulong AnchorRef;

        /// <summary>The BLDG record that is the hero's plan, <c>(osm_id &lt;&lt; 1) | is_relation</c>, 0 = none.</summary>
        public ulong FootprintRef;

        /// <summary>The compound AREA, <c>(osm_id &lt;&lt; 1) | is_relation</c>, 0 = none.</summary>
        public ulong CompoundRef;

        /// <summary>Key of the leaf tile holding the anchor (0 = unknown).</summary>
        public ulong AnchorTileKey;

        /// <summary>Anchor position in absolute game metres (NaN when unknown).</summary>
        public double X = double.NaN, Z = double.NaN;

        /// <summary>osm_ref of every BLDG record this hero hides (LANDMARK).</summary>
        public ulong[] Hidden = new ulong[0];

        /// <summary>Other generator parameters as text (the .ghcd attrs); null when none.</summary>
        public Dictionary<string, string> AttrText;

        /// <summary>A text attribute parsed as a float, or <paramref name="fallback"/>.</summary>
        public float AttrFloat(string key, float fallback)
        {
            string v;
            float f;
            if (AttrText != null && AttrText.TryGetValue(key, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            return Attr(key, fallback);
        }

        /// <summary>A comma-separated float list attribute (<c>"81.5,62.5,50.2"</c>), or null.</summary>
        public float[] AttrFloats(string key)
        {
            string v;
            if (AttrText == null || !AttrText.TryGetValue(key, out v) || string.IsNullOrEmpty(v)) return null;
            string[] parts = v.Split(',');
            var a = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a[i])) return null;
            return a;
        }

        /// <summary>The anchor in the "n123" / "w123" / "r123" form from <see cref="AnchorRef"/>.</summary>
        public static string OsmOf(ulong nwrRef)
        {
            if (nwrRef == 0) return null;
            char t = (nwrRef & 3) == 0 ? 'n' : (nwrRef & 3) == 1 ? 'w' : 'r';
            return t + (nwrRef >> 2).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Parse an OSM reference such as <c>w56688295</c>; type is 'n', 'w' or 'r'.</summary>
        public static bool TryParseOsm(string s, out char type, out long id)
        {
            type = '\0';
            id = 0;
            if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
            char t = char.ToLowerInvariant(s[0]);
            if (t != 'n' && t != 'w' && t != 'r') return false;
            if (!long.TryParse(s.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0) return false;
            type = t;
            return true;
        }

        /// <summary>The BLDG/AREA reference <c>(osm_id &lt;&lt; 1) | is_relation</c> of a way or relation anchor, 0 for
        /// nodes or unparsable anchors.</summary>
        public static ulong WayRelationRef(string osm)
        {
            char t;
            long id;
            if (!TryParseOsm(osm, out t, out id) || t == 'n') return 0;
            return (ulong)id << 1 | (t == 'r' ? 1UL : 0UL);
        }

        /// <summary>The POIS reference <c>(osm_id &lt;&lt; 2) | type</c> (0 node, 1 way, 2 relation), 0 when unparsable.</summary>
        public static ulong NwrRef(string osm)
        {
            char t;
            long id;
            if (!TryParseOsm(osm, out t, out id)) return 0;
            return (ulong)id << 2 | (t == 'n' ? 0UL : t == 'w' ? 1UL : 2UL);
        }

        /// <summary>The plan footprint (BLDG ref): <see cref="FootprintRef"/>, else the way or relation anchor.</summary>
        public ulong AnchorWayRelationRef
        {
            get { return FootprintRef != 0 ? FootprintRef : WayRelationRef(Osm); }
        }

        public ulong AnchorNwrRef
        {
            get { return NwrRef(Osm); }
        }

        /// <summary>A float attribute from <see cref="Attrs"/>, or <paramref name="fallback"/>.</summary>
        public float Attr(string key, float fallback)
        {
            JsonValue v;
            if (Attrs != null && Attrs.TryGetValue(key, out v) && v != null && v.Kind == JsonKind.Number) return (float)v.AsDouble(fallback);
            return fallback;
        }

        public HeritageRecord Clone()
        {
            var c = (HeritageRecord)MemberwiseClone();
            c.Hide = (string[])Hide.Clone();
            c.Hidden = (ulong[])Hidden.Clone();
            if (AttrText != null) c.AttrText = new Dictionary<string, string>(AttrText, StringComparer.Ordinal);
            return c;
        }
    }

    /// <summary>
    /// The curated heritage DB (D12). <see cref="Read"/> accepts the compiled <c>.ghcd</c> bytes or, until D12 lands,
    /// the UTF-8 JSON bridge <c>hero_recipes.json</c> (W2_DESIGN 9.4): an object with a <c>heritage</c> array (or a
    /// bare array) of records with the fields <c>id</c>, <c>name</c>, <c>kind</c>, <c>osm</c>, <c>compound</c>,
    /// <c>hide</c>, <c>attrs</c> {tiers, plinth_levels, height_m, yaw_deg, finish, doors, guardians, entry_rule, kora,
    /// plan_w_m, plan_d_m, lon, lat}. Records without an id are skipped; a later duplicate id replaces the earlier.
    /// </summary>
    public sealed class CuratedDb
    {
        private readonly Dictionary<string, HeritageRecord> _byId = new Dictionary<string, HeritageRecord>(StringComparer.Ordinal);
        private readonly List<HeritageRecord> _ordered = new List<HeritageRecord>();

        public IEnumerable<HeritageRecord> Heritage
        {
            get { return _ordered; }
        }

        public int Count
        {
            get { return _ordered.Count; }
        }

        public bool TryGetHeritage(string id, out HeritageRecord r)
        {
            if (id == null)
            {
                r = null;
                return false;
            }
            return _byId.TryGetValue(id, out r);
        }

        /// <summary>The record whose anchor is this way/relation reference (BLDG/AREA osm_ref), or null.</summary>
        public HeritageRecord ByWayRelationRef(ulong osmRef)
        {
            if (osmRef == 0) return null;
            foreach (HeritageRecord r in _ordered)
                if (r.AnchorWayRelationRef == osmRef) return r;
            return null;
        }

        public void Add(HeritageRecord r)
        {
            if (r == null || string.IsNullOrEmpty(r.Id)) return;
            HeritageRecord old;
            if (_byId.TryGetValue(r.Id, out old)) _ordered.Remove(old);
            _byId[r.Id] = r;
            _ordered.Add(r);
        }

        public const ushort GhcdVersion = 1;
        public const int GhcdHeaderSize = 32;
        public static readonly uint GhcdMagic = Ght.FourCC("GHCD");

        /// <summary>Decode a curated DB blob: the compiled <c>.ghcd</c> (magic <c>GHCD</c>, DATA_FORMATS 7), or the JSON
        /// bridge when the first non-blank byte is '{' or '['. Corruption throws <see cref="InvalidDataException"/>.</summary>
        public static CuratedDb Read(byte[] blob)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            if (blob.Length >= 4 && BinReader.ReadU32(blob, 0) == GhcdMagic) return ReadGhcd(blob);
            int i = 0;
            if (blob.Length >= 3 && blob[0] == 0xEF && blob[1] == 0xBB && blob[2] == 0xBF) i = 3;
            while (i < blob.Length && (blob[i] == ' ' || blob[i] == '\t' || blob[i] == '\r' || blob[i] == '\n')) i++;
            if (i < blob.Length && (blob[i] == '{' || blob[i] == '['))
                return FromJson(new UTF8Encoding(false, true).GetString(blob, i, blob.Length - i));
            throw new InvalidDataException("not a curated DB (neither GHCD nor JSON)");
        }

        private static ulong Ref(BinReader r)
        {
            return r.Varint();
        }

        private static CuratedDb ReadGhcd(byte[] blob)
        {
            if (blob.Length < GhcdHeaderSize) throw new InvalidDataException("curated DB shorter than its header");
            var h = new BinReader(blob, 0, GhcdHeaderSize);
            h.U32();
            ushort version = h.U16();
            if (version != GhcdVersion) throw new InvalidDataException("unsupported GHCD version " + version);
            h.U16();
            uint count = h.U32(), payload = h.U32(), crc = h.U32();
            if ((long)GhcdHeaderSize + payload != blob.Length) throw new InvalidDataException("curated DB payload size mismatch");
            if (Hashes.Crc32(blob, GhcdHeaderSize, (int)payload) != crc) throw new InvalidDataException("curated DB CRC mismatch");
            var r = new BinReader(blob, GhcdHeaderSize, (int)payload);
            if (count > (uint)r.Remaining) throw new InvalidDataException("curated record count exceeds the payload");
            var db = new CuratedDb();
            for (uint k = 0; k < count; k++)
            {
                var rec = new HeritageRecord { Id = r.Str() };
                rec.Kind = (HeritageKind)r.U8();
                rec.Stage = r.U8();
                rec.Flags = (HeritageFlags)r.U8();
                rec.Finish = (HeritageFinish)r.U8();
                rec.Tiers = r.U8();
                rec.PlinthLevels = r.U8();
                rec.Doors = r.U8();
                rec.Entry = (EntryRule)r.U8();
                rec.Kora = (KoraDirection)r.U8();
                ushort yaw = r.U16();
                rec.YawDeg = yaw == 65535 ? float.NaN : yaw / 100f;
                if (yaw != 65535 && yaw > 35999) throw new InvalidDataException("curated yaw_cdeg " + yaw + " outside 0..35999");
                ulong hcm = r.Varint();
                rec.HeightM = hcm == 0 ? float.NaN : hcm / 100f;
                rec.Name = r.Str();
                rec.NameNe = r.Str();
                rec.Deity = r.Str();
                rec.AnchorRef = Ref(r);
                rec.Osm = HeritageRecord.OsmOf(rec.AnchorRef);
                rec.FootprintRef = Ref(r);
                rec.CompoundRef = Ref(r);
                rec.AnchorTileKey = r.U64();
                long x = r.Svarint(), z = r.Svarint();
                if (rec.AnchorRef != 0 || (rec.Flags & HeritageFlags.ManualPosition) != 0)
                {
                    rec.X = x / 100.0;
                    rec.Z = z / 100.0;
                }
                int hidden = CountOf(r);
                rec.Hidden = new ulong[hidden];
                for (int i = 0; i < hidden; i++) rec.Hidden[i] = r.Varint();
                int attrs = CountOf(r);
                if (attrs > 0) rec.AttrText = new Dictionary<string, string>(attrs, StringComparer.Ordinal);
                for (int i = 0; i < attrs; i++)
                {
                    string key = r.Str(), value = r.Str();
                    rec.AttrText[key] = value;
                }
                rec.Provenance = r.Str();
                rec.Review = r.Str();
                ApplyAttrs(rec);
                db.Add(rec);
            }
            if (r.Remaining != 0) throw new InvalidDataException(r.Remaining + " trailing bytes in the curated DB");
            return db;
        }

        private static int CountOf(BinReader r)
        {
            ulong n = r.Varint();
            if (n > (ulong)r.Remaining) throw new InvalidDataException("count " + n + " exceeds the " + r.Remaining + " bytes left");
            return (int)n;
        }

        /// <summary>Plan and guardian attributes from the text attrs (<c>plan_m</c> "w,d").</summary>
        private static void ApplyAttrs(HeritageRecord rec)
        {
            float[] plan = rec.AttrFloats("plan_m");
            if (plan != null && plan.Length >= 1)
            {
                rec.PlanWM = plan[0];
                rec.PlanDM = plan.Length >= 2 ? plan[1] : plan[0];
            }
            string g;
            if (rec.AttrText != null && rec.AttrText.TryGetValue("guardians", out g)) rec.Guardians = g;
        }

        public static CuratedDb FromJson(string text)
        {
            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonParseException e)
            {
                throw new InvalidDataException("curated DB: " + e.Message, e);
            }
            JsonArray list = null;
            if (root.Kind == JsonKind.Array) list = root.AsArray();
            else if (root.Kind == JsonKind.Object)
            {
                JsonValue h;
                if (root.AsObject().TryGetValue("heritage", out h) && h != null && h.Kind == JsonKind.Array) list = h.AsArray();
                else if (root.AsObject().TryGetValue("records", out h) && h != null && h.Kind == JsonKind.Array) list = h.AsArray();
            }
            var db = new CuratedDb();
            if (list == null) return db;
            foreach (JsonValue v in list)
            {
                if (v == null || v.Kind != JsonKind.Object) continue;
                HeritageRecord r = Parse(v.AsObject());
                if (r != null) db.Add(r);
            }
            return db;
        }

        private static string Str(JsonObject o, string key)
        {
            JsonValue v;
            return o.TryGetValue(key, out v) && v != null && v.Kind == JsonKind.String ? v.AsString() : null;
        }

        private static long Lng(JsonObject o, string key)
        {
            JsonValue v;
            return o != null && o.TryGetValue(key, out v) && v != null && v.Kind == JsonKind.Number ? v.AsLong() : 0;
        }

        private static float Num(JsonObject o, string key)
        {
            JsonValue v;
            return o != null && o.TryGetValue(key, out v) && v != null && v.Kind == JsonKind.Number ? (float)v.AsDouble() : float.NaN;
        }

        private static HeritageRecord Parse(JsonObject o)
        {
            string id = Str(o, "id");
            if (string.IsNullOrEmpty(id)) return null;
            var r = new HeritageRecord
            {
                Id = id, Name = Str(o, "name") ?? Str(o, "name_en"), Kind = ParseEnum<HeritageKind>(Str(o, "kind")), Osm = Str(o, "osm"),
                Compound = Str(o, "compound"), Flags = HeritageFlags.SanctumClosed, NameNe = Str(o, "name_ne"), Deity = Str(o, "deity"),
                Provenance = Str(o, "provenance"), Review = Str(o, "review"),
            };
            // hero_recipes.json / golden_curated.json shape (curated.records_json): refs, position and top-level numbers.
            float num;
            if (!float.IsNaN(num = Num(o, "anchor_ref"))) r.AnchorRef = (ulong)Lng(o, "anchor_ref");
            if (!float.IsNaN(num = Num(o, "footprint_ref"))) r.FootprintRef = (ulong)Lng(o, "footprint_ref");
            if (!float.IsNaN(num = Num(o, "compound_ref"))) r.CompoundRef = (ulong)Lng(o, "compound_ref");
            if (!float.IsNaN(num = Num(o, "anchor_tile"))) r.AnchorTileKey = (ulong)Lng(o, "anchor_tile");
            if (r.Osm == null && r.AnchorRef != 0) r.Osm = HeritageRecord.OsmOf(r.AnchorRef);
            if (!float.IsNaN(num = Num(o, "stage"))) r.Stage = (int)num;
            if (!float.IsNaN(num = Num(o, "tiers"))) r.Tiers = (int)num;
            if (!float.IsNaN(num = Num(o, "plinth_levels"))) r.PlinthLevels = (int)num;
            if (!float.IsNaN(num = Num(o, "doors"))) r.Doors = (int)num;
            if (!float.IsNaN(num = Num(o, "height_m")) && num > 0) r.HeightM = num;
            if (!float.IsNaN(num = Num(o, "yaw_deg"))) r.YawDeg = num;
            if (!float.IsNaN(num = Num(o, "flags"))) r.Flags = (HeritageFlags)(int)num;
            if (Str(o, "finish") != null) r.Finish = ParseEnum<HeritageFinish>(Str(o, "finish"));
            if (Str(o, "entry_rule") != null) r.Entry = ParseEnum<EntryRule>(Str(o, "entry_rule"));
            if (Str(o, "kora") != null) r.Kora = ParseEnum<KoraDirection>(Str(o, "kora"));
            if (r.AnchorRef != 0 || (r.Flags & HeritageFlags.ManualPosition) != 0)
            {
                JsonValue xv, zv;
                if (o.TryGetValue("x", out xv) && xv != null && xv.Kind == JsonKind.Number) r.X = xv.AsDouble();
                if (o.TryGetValue("z", out zv) && zv != null && zv.Kind == JsonKind.Number) r.Z = zv.AsDouble();
            }
            JsonValue hid;
            if (o.TryGetValue("hidden", out hid) && hid != null && hid.Kind == JsonKind.Array)
            {
                var l = new List<ulong>();
                foreach (JsonValue v in hid.AsArray())
                    if (v != null && v.Kind == JsonKind.Number) l.Add((ulong)v.AsLong());
                r.Hidden = l.ToArray();
            }
            JsonValue hide;
            if (o.TryGetValue("hide", out hide) && hide != null)
            {
                var list = new List<string>();
                if (hide.Kind == JsonKind.Array)
                {
                    foreach (JsonValue h in hide.AsArray())
                        if (h != null && h.Kind == JsonKind.String) list.Add(h.AsString());
                }
                else if (hide.Kind == JsonKind.String)
                {
                    list.Add(hide.AsString());
                }
                r.Hide = list.ToArray();
            }
            JsonValue av;
            JsonObject a = o.TryGetValue("attrs", out av) && av != null && av.Kind == JsonKind.Object ? av.AsObject() : null;
            r.Attrs = a;
            if (a != null)
            {
                foreach (KeyValuePair<string, JsonValue> kv in a)
                {
                    if (kv.Value == null || kv.Value.Kind != JsonKind.String) continue;
                    if (r.AttrText == null) r.AttrText = new Dictionary<string, string>(StringComparer.Ordinal);
                    r.AttrText[kv.Key] = kv.Value.AsString();
                }
            }
            ApplyAttrs(r);
            if (a != null)
            {
                float f;
                if (!float.IsNaN(f = Num(a, "tiers"))) r.Tiers = (int)f;
                if (!float.IsNaN(f = Num(a, "plinth_levels"))) r.PlinthLevels = (int)f;
                if (!float.IsNaN(f = Num(a, "doors"))) r.Doors = (int)f;
                if (!float.IsNaN(f = Num(a, "height_m"))) r.HeightM = f;
                if (!float.IsNaN(f = Num(a, "yaw_deg"))) r.YawDeg = f;
                if (!float.IsNaN(f = Num(a, "plan_w_m"))) r.PlanWM = f;
                if (!float.IsNaN(f = Num(a, "plan_d_m"))) r.PlanDM = f;
                if (!float.IsNaN(f = Num(a, "lon"))) r.Lon = f;
                if (!float.IsNaN(f = Num(a, "lat"))) r.Lat = f;
                if (Str(a, "finish") != null) r.Finish = ParseEnum<HeritageFinish>(Str(a, "finish"));
                if (Str(a, "guardians") != null) r.Guardians = Str(a, "guardians");
                if (Str(a, "entry_rule") != null) r.Entry = ParseEnum<EntryRule>(Str(a, "entry_rule"));
                JsonValue k;
                if (a.TryGetValue("kora", out k) && k != null)
                {
                    if (k.Kind == JsonKind.Bool) r.Kora = k.AsBool() ? KoraDirection.Clockwise : KoraDirection.None;
                    else if (k.Kind == JsonKind.String) r.Kora = ParseEnum<KoraDirection>(k.AsString());
                }
                if (!double.IsNaN(r.Lon) && !double.IsNaN(r.Lat) && string.IsNullOrEmpty(r.Osm)) r.Flags |= HeritageFlags.ManualPosition;
            }
            return r;
        }

        /// <summary>An enum member from its snake-case token (<c>gilt_top</c>, <c>SHIKHARA_STONE</c>); unknown
        /// tokens give the zero member.</summary>
        public static T ParseEnum<T>(string token) where T : struct
        {
            if (string.IsNullOrEmpty(token)) return default(T);
            var sb = new StringBuilder();
            foreach (string w in token.Trim().Split('_', '-', ' '))
            {
                if (w.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(w[0]));
                sb.Append(w.Substring(1).ToLowerInvariant());
            }
            T v;
            return Enum.TryParse(sb.ToString(), false, out v) ? v : default(T);
        }
    }
}
