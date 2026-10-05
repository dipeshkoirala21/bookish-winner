using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Save;

namespace Ghumante.Core.Aviation
{
    /// <summary>One point of a procedure: game metres, altitude and the ground under it (m above sea level).</summary>
    public struct ProcedurePoint
    {
        public double X, Z;
        public float AltM, GroundM;
        public string Note;
    }

    /// <summary>A procedure from the sidecar (W2_DESIGN 8.3), imported unchanged.</summary>
    public sealed class Procedure
    {
        public string Name, Title;
        public ProcedurePoint[] Points;
    }

    /// <summary>A runway threshold.</summary>
    public struct Threshold
    {
        public string Designator;
        public double X, Z;
        public float ElevM;
    }

    /// <summary>
    /// The airport sidecar <c>&lt;region&gt;.aviation.json</c> (format ghumante-aviation, version 1; D20): runway
    /// thresholds and pavement ends, navaids, holds, the procedure point tables (AV §5, imported unchanged), the
    /// movement schedule per real hour (AV §4), month and weather factors, separations, procedure weights and the
    /// generic liveries. Apron positions come from the tiles' APRON areas (<see cref="AddApronsFrom"/>), with fallbacks.
    /// </summary>
    public sealed class AviationConfig
    {
        public const string Format = "ghumante-aviation";

        public string Icao = "", Iata = "";
        public float ElevationM;
        public Threshold Threshold02, Threshold20;
        public double PavementSouthX, PavementSouthZ, PavementNorthX, PavementNorthZ;
        public float RunwayWidthM = 45f;
        public float BacktrackM = 1200f;
        public int OpenFromHour = 6, OpenToHour = 24;
        public readonly Dictionary<string, Procedure> Procedures = new Dictionary<string, Procedure>(StringComparer.Ordinal);

        /// <summary>Movements per real hour by game hour (0–23) and schedule class.</summary>
        public readonly float[,] Rates = new float[24, 4];

        public readonly float[] MonthFactor = new float[13];
        public float SepAfterTurbopropS = 100f, SepAfterNarrowbodyS = 100f, SepAfterWidebodyS = 120f, SepHeliApronS = 60f;
        public float BusyMax = 2f;
        public float ArrivalShareDomestic = 0.5f, ArrivalShareIntl = 0.5f, HeliArrivalBefore10 = 0.3f, HeliArrival10To12 = 0.5f,
                     HeliArrivalAfter12 = 0.7f;
        public float DomesticWestShare = 0.45f, IntlWestShare = 0.85f;
        public float TurbopropShare = 0.75f, StretchShare = 0.10f, StolShare = 0.15f, WidebodyShareIntl = 0.08f;
        public int FogDomesticUntilHour = 10, FogBacklogUntilHour = 14, FogMaxPerHour = 33;
        public int[] FogMonths = { 12, 1 };
        public float StormDomestic = 0.7f, StormHeli = 0.4f;
        public int StormFromHour = 14, StormToHour = 18;
        public int[] StormMonths = { 6, 7, 8, 9 };
        public string[] FixedWingLiveries = new string[0];
        public string[] HelicopterColours = new string[0];

        /// <summary>Apron reference points (game metres): where domestic aircraft and helicopters, and international
        /// aircraft, park and start.</summary>
        public double DomesticApronX, DomesticApronZ, IntlApronX, IntlApronZ;

        public string DomesticApronId = "", IntlApronId = "";
        private bool _domSet, _intlSet;

        /// <summary>Runway true heading of 02 in game frame (degrees, clockwise from north).</summary>
        public float Heading02Deg;

        public static AviationConfig Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var root = Json.Parse(json) as JsonObject;
            if (root == null) throw new FormatException("aviation sidecar: not a JSON object");
            if (root.GetString("format") != Format) throw new FormatException("aviation sidecar: format is not " + Format);
            if (root.GetInt("version") != 1) throw new FormatException("aviation sidecar: unsupported version");
            var c = new AviationConfig();
            JsonObject ap = root.GetObject("airport") ?? throw new FormatException("aviation sidecar: no airport");
            c.Icao = ap.GetString("icao", "");
            c.Iata = ap.GetString("iata", "");
            c.ElevationM = (float)ap.GetDouble("elevation_m", 1338);
            JsonArray hours = ap.GetArray("open_hours");
            if (hours != null && hours.Count == 2)
            {
                c.OpenFromHour = hours[0].AsInt(6);
                c.OpenToHour = hours[1].AsInt(24);
            }
            JsonObject rw = ap.GetObject("runway") ?? throw new FormatException("aviation sidecar: no runway");
            c.RunwayWidthM = (float)rw.GetDouble("width_m", 45);
            JsonObject th = rw.GetObject("thresholds") ?? throw new FormatException("aviation sidecar: no thresholds");
            c.Threshold02 = ThresholdOf(th.GetObject("02"), "02");
            c.Threshold20 = ThresholdOf(th.GetObject("20"), "20");
            JsonObject pe = rw.GetObject("pavement_ends");
            if (pe != null)
            {
                JsonObject s = pe.GetObject("south"), n = pe.GetObject("north");
                if (s != null)
                {
                    c.PavementSouthX = s.GetDouble("x");
                    c.PavementSouthZ = s.GetDouble("z");
                }
                if (n != null)
                {
                    c.PavementNorthX = n.GetDouble("x");
                    c.PavementNorthZ = n.GetDouble("z");
                }
            }
            c.Heading02Deg = (float)(Math.Atan2(c.Threshold20.X - c.Threshold02.X, c.Threshold20.Z - c.Threshold02.Z) * 180.0 / Math.PI);
            JsonObject aprons = ap.GetObject("aprons");
            if (aprons != null)
            {
                c.DomesticApronId = aprons.GetString("domestic", "");
                c.IntlApronId = aprons.GetString("international", "");
            }

            JsonObject procs = root.GetObject("procedures");
            if (procs != null)
                foreach (var kv in procs)
                {
                    var po = kv.Value as JsonObject;
                    if (po == null) continue;
                    JsonArray pts = po.GetArray("points");
                    if (pts == null) continue;
                    var p = new Procedure { Name = kv.Key, Title = po.GetString("title", ""), Points = new ProcedurePoint[pts.Count] };
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var q = pts[i] as JsonObject;
                        if (q == null) throw new FormatException("aviation sidecar: bad point in " + kv.Key);
                        p.Points[i] = new ProcedurePoint
                        {
                            X = q.GetDouble("x"), Z = q.GetDouble("z"), AltM = (float)q.GetDouble("alt_m"), GroundM = (float)q.GetDouble("ground_m"),
                            Note = q.GetString("note", ""),
                        };
                    }
                    c.Procedures[kv.Key] = p;
                }

            JsonObject sch = root.GetObject("schedule");
            if (sch != null)
            {
                JsonObject rates = sch.GetObject("movements_per_real_hour");
                if (rates != null)
                    for (int h = 0; h < 24; h++)
                    {
                        JsonObject row = rates.GetObject(h.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if (row == null) continue;
                        c.Rates[h, 0] = (float)row.GetDouble("DOM_TP");
                        c.Rates[h, 1] = (float)row.GetDouble("HELI");
                        c.Rates[h, 2] = (float)row.GetDouble("INTL_NB");
                        c.Rates[h, 3] = (float)row.GetDouble("INTL_WB");
                    }
                JsonObject mf = sch.GetObject("month_factor");
                for (int m = 1; m <= 12; m++)
                    c.MonthFactor[m] = mf != null ? (float)mf.GetDouble(m.ToString(System.Globalization.CultureInfo.InvariantCulture), 1.0) : 1f;
                JsonObject sep = sch.GetObject("separation_s");
                if (sep != null)
                {
                    c.SepAfterTurbopropS = (float)sep.GetDouble("after_turboprop", 100);
                    c.SepAfterNarrowbodyS = (float)sep.GetDouble("after_narrowbody", 100);
                    c.SepAfterWidebodyS = (float)sep.GetDouble("after_widebody", 120);
                    c.SepHeliApronS = (float)sep.GetDouble("heli_apron", 60);
                }
                c.BacktrackM = (float)sch.GetDouble("backtrack_02_m", 1200);
                c.BusyMax = (float)sch.GetDouble("busy_airport_max", 2.0);
                JsonObject arr = sch.GetObject("arrival_share");
                if (arr != null)
                {
                    c.ArrivalShareDomestic = (float)arr.GetDouble("DOM_TP", 0.5);
                    c.ArrivalShareIntl = (float)arr.GetDouble("INTL_NB", 0.5);
                    c.HeliArrivalBefore10 = (float)arr.GetDouble("HELI_before_10", 0.3);
                    c.HeliArrival10To12 = (float)arr.GetDouble("HELI_10_12", 0.5);
                    c.HeliArrivalAfter12 = (float)arr.GetDouble("HELI_after_12", 0.7);
                }
                JsonObject fog = sch.GetObject("fog");
                if (fog != null)
                {
                    c.FogDomesticUntilHour = fog.GetInt("domestic_heli_factor_until_hour", 10);
                    c.FogBacklogUntilHour = fog.GetInt("backlog_until_hour", 14);
                    c.FogMaxPerHour = fog.GetInt("max_per_hour", 33);
                    c.FogMonths = Ints(fog.GetArray("months"), c.FogMonths);
                }
                JsonObject storm = sch.GetObject("monsoon_storms");
                if (storm != null)
                {
                    c.StormDomestic = (float)storm.GetDouble("domestic", 0.7);
                    c.StormHeli = (float)storm.GetDouble("heli", 0.4);
                    int[] sh = Ints(storm.GetArray("hours"), new[] { 14, 18 });
                    if (sh.Length == 2)
                    {
                        c.StormFromHour = sh[0];
                        c.StormToHour = sh[1];
                    }
                    c.StormMonths = Ints(storm.GetArray("months"), c.StormMonths);
                }
            }
            JsonObject classes = root.GetObject("classes");
            if (classes != null)
            {
                c.TurbopropShare = (float)Share(classes, "turboprop", "share_domestic_fixed_wing", 0.75);
                c.StretchShare = (float)Share(classes, "turboprop_stretch", "share_domestic_fixed_wing", 0.10);
                c.StolShare = (float)Share(classes, "stol", "share_domestic_fixed_wing", 0.15);
                c.WidebodyShareIntl = (float)Share(classes, "widebody", "share_international", 0.08);
            }
            JsonObject pw = root.GetObject("procedure_weights");
            if (pw != null)
            {
                JsonObject dd = pw.GetObject("DOM_departure"), idp = pw.GetObject("INTL_departure");
                if (dd != null) c.DomesticWestShare = (float)dd.GetDouble("west_share", 0.45);
                if (idp != null) c.IntlWestShare = (float)idp.GetDouble("west_share", 0.85);
            }
            JsonObject liv = root.GetObject("liveries");
            if (liv != null)
            {
                c.FixedWingLiveries = Strings(liv.GetArray("fixed_wing"));
                c.HelicopterColours = Strings(liv.GetArray("helicopter"));
            }
            // Apron fallbacks until the tiles give the real centroids: the helicopter lift-off point (domestic apron)
            // and 300 m left of the runway's middle (international apron, west of the runway).
            Procedure he;
            if (c.Procedures.TryGetValue("H-E", out he) && he.Points.Length > 0)
            {
                c.DomesticApronX = he.Points[0].X;
                c.DomesticApronZ = he.Points[0].Z;
            }
            else
            {
                c.DomesticApronX = c.Threshold20.X - 250;
                c.DomesticApronZ = c.Threshold20.Z;
            }
            double mx = 0.5 * (c.Threshold02.X + c.Threshold20.X), mz = 0.5 * (c.Threshold02.Z + c.Threshold20.Z);
            double hr = c.Heading02Deg * Math.PI / 180.0;
            c.IntlApronX = mx - 300 * Math.Cos(hr);
            c.IntlApronZ = mz + 300 * Math.Sin(hr);
            return c;
        }

        /// <summary>Uses the APRON areas of resident tiles (areas carry <c>(osm_id &lt;&lt; 1) | is_relation</c>) for the
        /// domestic and international apron centroids. Returns how many aprons were found.</summary>
        public int AddApronsFrom(IEnumerable<TileData> tiles)
        {
            long dom = WayId(DomesticApronId), intl = WayId(IntlApronId);
            double dx = 0, dz = 0, ix = 0, iz = 0;
            int dn = 0, inn = 0;
            foreach (TileData t in tiles)
            {
                if (t == null) continue;
                foreach (AreaRecord a in t.Areas)
                {
                    if (a.Kind != AreaKind.Apron || a.Vertices == null) continue;
                    long id = (long)(a.OsmRef >> 1);
                    bool isDom = id == dom, isIntl = id == intl;
                    if (!isDom && !isIntl) continue;
                    int n = a.Vertices.Length / 2;
                    for (int i = 0; i < n; i++)
                    {
                        double x = t.Tile.X0 + a.Vertices[2 * i] / 100.0, z = t.Tile.Z0 + a.Vertices[2 * i + 1] / 100.0;
                        if (isDom)
                        {
                            dx += x;
                            dz += z;
                            dn++;
                        }
                        else
                        {
                            ix += x;
                            iz += z;
                            inn++;
                        }
                    }
                }
            }
            if (dn > 0)
            {
                DomesticApronX = dx / dn;
                DomesticApronZ = dz / dn;
                _domSet = true;
            }
            if (inn > 0)
            {
                IntlApronX = ix / inn;
                IntlApronZ = iz / inn;
                _intlSet = true;
            }
            return (_domSet ? 1 : 0) + (_intlSet ? 1 : 0);
        }

        private static long WayId(string osm)
        {
            if (string.IsNullOrEmpty(osm) || osm[0] != 'w') return -1;
            long v;
            return long.TryParse(osm.Substring(1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v)
                ? v : -1;
        }

        private static Threshold ThresholdOf(JsonObject o, string d)
        {
            if (o == null) throw new FormatException("aviation sidecar: threshold " + d + " missing");
            return new Threshold { Designator = d, X = o.GetDouble("x"), Z = o.GetDouble("z"), ElevM = (float)o.GetDouble("elev_m") };
        }

        private static double Share(JsonObject classes, string cls, string key, double fallback)
        {
            JsonObject o = classes.GetObject(cls);
            return o != null ? o.GetDouble(key, fallback) : fallback;
        }

        private static int[] Ints(JsonArray a, int[] fallback)
        {
            if (a == null) return fallback;
            var r = new int[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = a[i].AsInt();
            return r;
        }

        private static string[] Strings(JsonArray a)
        {
            if (a == null) return new string[0];
            var r = new string[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = a[i].AsString("");
            return r;
        }

        /// <summary>Movements per real hour of a class at a game hour (whole hours; the table row).</summary>
        public float RateAt(int hour, ScheduleClass c)
        {
            return Rates[((hour % 24) + 24) % 24, (int)c];
        }

        /// <summary>The runway elevation at a point projected on the centreline (a straight profile between the
        /// threshold elevations, continued over the displaced sections).</summary>
        public float RunwayElevationAt(double x, double z)
        {
            double ux = Threshold20.X - Threshold02.X, uz = Threshold20.Z - Threshold02.Z;
            double len2 = ux * ux + uz * uz;
            double t = len2 > 0 ? ((x - Threshold02.X) * ux + (z - Threshold02.Z) * uz) / len2 : 0;
            return (float)(Threshold02.ElevM + (Threshold20.ElevM - Threshold02.ElevM) * t);
        }

        /// <summary>Distance from (x, z) to the runway pavement (its centreline segment between the pavement ends).</summary>
        public double DistanceToRunway(double x, double z)
        {
            double ax = PavementSouthX != 0 ? PavementSouthX : Threshold02.X, az = PavementSouthZ != 0 ? PavementSouthZ : Threshold02.Z;
            double bx = PavementNorthX != 0 ? PavementNorthX : Threshold20.X, bz = PavementNorthZ != 0 ? PavementNorthZ : Threshold20.Z;
            double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            double t = l2 > 0 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double qx = ax + t * dx - x, qz = az + t * dz - z;
            return Math.Sqrt(qx * qx + qz * qz);
        }
    }
}
