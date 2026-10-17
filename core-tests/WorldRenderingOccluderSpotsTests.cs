using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Synth.Look;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #4 and #12 ("buildings constantly flicker and stutter... make them stable and solid"; the screenshot
    /// at Hiti Pokhari shows a house next to the camera drawn dithered while the explorer is in plain view): the occluder
    /// fade, measured on the real <c>kathmandu_core</c> sample at the owner's spots. A chase camera follows the explorer
    /// along real streets (two-wheeler rig, boom pulled in by the world's <see cref="IViewObstacleQuery"/> as the game's
    /// boom does) and every frame the harness evaluates the fade the game would draw on the near-ring B0 houses (the
    /// richest level, as built for the camera): how much house surface in view is dissolved while the explorer is in plain
    /// view (truth: rays from the camera to the explorer's head, chest and hips against the drawn house triangles), and
    /// how often each patch of wall flips between solid and dissolved (flicker).
    /// </summary>
    public class WorldRenderingOccluderSpotsTests
    {
        /// <summary>The owner's spots (WGS84): the Hiti Pokhari primary by Nag Pokhari, the Asan lanes, Kalopul.</summary>
        internal static readonly OccluderSpot[] Spots =
        {
            new OccluderSpot("hiti_pokhari", 85.32297, 27.71364),
            new OccluderSpot("asan", 85.31130, 27.70790),
            new OccluderSpot("kalopul", 85.33749, 27.71184),
        };

        /// <summary>The fade model the game runs (what ToonLook feeds the shader every frame).</summary>
        internal static IOccluderModel GameModel()
        {
            return new GateOccluderModel();
        }

        [Test]
        public void HousesNeverDissolveWhileTheExplorerIsInPlainView()
        {
            var report = new StringBuilder();
            int worst = 0;
            foreach (OccluderSpot spot in Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                foreach (OccluderRide ride in scene.Rides)
                {
                    RideStats legacy = scene.Run(ride, new LegacyOccluderModel());
                    RideStats game = scene.Run(ride, GameModel());
                    report.Append(spot.Name).Append(' ').Append(ride.Name).Append('\n')
                          .Append("  legacy: ").Append(legacy).Append('\n')
                          .Append("  game:   ").Append(game).Append('\n');
                    worst = Math.Max(worst, game.FadedVisibleFrames);
                }
            }
            TestContext.WriteLine(report.ToString());
            OccluderSpotScene.WriteReport("occluder_spots.txt", report.ToString());
            Assert.That(worst, Is.EqualTo(0), "frames with house walls dissolved while the explorer is in plain view:\n" + report);
        }

        [Test]
        public void AHouseThatReallyHidesTheExplorerFadesSmoothlyAfterAMoment()
        {
            // A camera on one side of a real house and the explorer on the other, at each spot: the gate waits out the
            // engage time (nothing dissolves for a brief occlusion), then fades in smoothly and clears every house surface
            // on the line to the explorer's chest.
            OccluderFadeSettings st = OccluderFadeSettings.Default;
            var report = new StringBuilder();
            foreach (OccluderSpot spot in Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                List<OccluderSpotScene.CameraFrame> setups = scene.HiddenSetups(3);
                report.Append(spot.Name).Append(": ").Append(setups.Count).Append(" setups\n");
                Assert.That(setups.Count, Is.GreaterThan(0), spot.Name + ": no camera/explorer pair across a house");
                foreach (OccluderSpotScene.CameraFrame setup in setups)
                {
                    var model = new GateOccluderModel();
                    float prev = 0f;
                    for (int k = 1; k <= 45; k++)
                    {
                        float t = k * OccluderSpotScene.Dt, strength;
                        OccluderSpotScene.CameraFrame f = setup;
                        f.Capsule = model.Frame(OccluderSpotScene.Ground, scene.Tile.Tile.X0, scene.Tile.Tile.Z0, f.Cx, f.Cy, f.Cz, f.Fx, f.Fy + 1.0f,
                                                f.Fz, OccluderSpotScene.Dt, OccluderSpotScene.TanHalfY, OccluderSpotScene.Aspect, out strength);
                        f.Strength = strength;
                        Assert.That(model.Gate.Hidden, Is.True, spot.Name + ": the world's solids hide the explorer");
                        if (t < st.EngageS - 1e-3f) Assert.That(strength, Is.EqualTo(0f), "nothing dissolves before the engage time");
                        Assert.That(strength - prev, Is.LessThanOrEqualTo(1.6f * OccluderSpotScene.Dt / st.FadeInS + 1e-4f), "smooth fade-in");
                        prev = strength;
                        if (t >= st.EngageS + st.FadeInS + OccluderSpotScene.Dt)
                        {
                            Assert.That(strength, Is.EqualTo(1f).Within(1e-5f));
                            Assert.That(scene.ChestCleared(f), Is.True, spot.Name + ": the line to the explorer's chest is cleared");
                        }
                    }
                }
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// Before/after OBJ scenes for tools/mesh-preview (GHUMANTE_PREVIEW_DIR): at each spot the frame where the old fade
        /// dissolved the most house wall while the explorer was in plain view, with the old fade and with the gate (the
        /// dither emulated by dropping 6 cm cells of the drawn houses where the shader's 4 × 4 screen door would discard
        /// them), and a camera/explorer pair across a house with the gate engaged (the fade still clears what really hides
        /// the explorer).
        /// </summary>
        [Test]
        public void DumpOwnerSpotPreviews()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the previews");
            var index = new StringBuilder();
            foreach (OccluderSpot spot in Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                OccluderRide bestRide = null;
                int bestFrame = -1;
                float bestArea = 0f;
                foreach (OccluderRide ride in scene.Rides)
                {
                    RideStats st = scene.Run(ride, new LegacyOccluderModel());
                    if (st.WorstFrame >= 0 && st.MaxFadedAreaM2 > bestArea)
                    {
                        bestArea = st.MaxFadedAreaM2;
                        bestRide = ride;
                        bestFrame = st.WorstFrame;
                    }
                }
                if (bestRide == null)
                {
                    bestRide = scene.Rides[scene.Rides.Count - 1];
                    bestFrame = 60;
                }
                var before = new OccluderSpotScene.CameraFrame();
                var after = new OccluderSpotScene.CameraFrame();
                scene.Run(bestRide, new LegacyOccluderModel(), (i, f) =>
                {
                    if (i == bestFrame) before = f;
                });
                scene.Run(bestRide, new GateOccluderModel(), (i, f) =>
                {
                    if (i == bestFrame) after = f;
                });
                index.Append(spot.Name).Append(": ").Append(bestRide.Name).Append(" frame ").Append(bestFrame).Append(", old fade dissolved ")
                     .Append(bestArea.ToString("0.00", CultureInfo.InvariantCulture)).Append(" m² in plain view; gate strength ")
                     .Append(after.Strength.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
                scene.DumpFrame(spot.Name + "/plain_before", before);
                scene.DumpFrame(spot.Name + "/plain_after", after);

                List<OccluderSpotScene.CameraFrame> setups = scene.HiddenSetups(1);
                if (setups.Count > 0)
                {
                    OccluderSpotScene.CameraFrame h = setups[0];
                    var gate = new GateOccluderModel();
                    for (int k = 0; k < 30; k++)
                    {
                        float strength;
                        h.Capsule = gate.Frame(OccluderSpotScene.Ground, scene.Tile.Tile.X0, scene.Tile.Tile.Z0, h.Cx, h.Cy, h.Cz, h.Fx, h.Fy + 1f, h.Fz,
                                               OccluderSpotScene.Dt, OccluderSpotScene.TanHalfY, OccluderSpotScene.Aspect, out strength);
                        h.Strength = strength;
                    }
                    scene.DumpFrame(spot.Name + "/hidden_after", h);
                    OccluderSpotScene.CameraFrame h0 = h;
                    h0.Strength = 0f;
                    h0.Capsule.Strength = 0f;
                    scene.DumpFrame(spot.Name + "/hidden_solid", h0);
                }
            }
            OccluderSpotScene.WriteReport("previews.txt", index.ToString());
            TestContext.WriteLine(index.ToString());
        }

        [Test]
        public void HouseWallsNeverFlickerAsTheCameraMoves()
        {
            var report = new StringBuilder();
            int worstFlicker = 0;
            foreach (OccluderSpot spot in Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                foreach (OccluderRide ride in scene.Rides)
                {
                    RideStats game = scene.Run(ride, GameModel());
                    report.Append(spot.Name).Append(' ').Append(ride.Name).Append(": ").Append(game).Append('\n');
                    // A patch may dissolve once when the explorer really disappears behind it and come back once after:
                    // anything more is flicker.
                    worstFlicker = Math.Max(worstFlicker, game.FlickerPatches);
                }
            }
            TestContext.WriteLine(report.ToString());
            Assert.That(worstFlicker, Is.EqualTo(0), "wall patches that flip between solid and dissolved three times or more:\n" + report);
        }
    }

    /// <summary>A place of the owner's playtest.</summary>
    internal readonly struct OccluderSpot
    {
        public readonly string Name;
        public readonly double Lon, Lat;

        public OccluderSpot(string name, double lon, double lat)
        {
            Name = name;
            Lon = lon;
            Lat = lat;
        }
    }

    /// <summary>One frame of the fade as the shader receives it.</summary>
    internal interface IOccluderModel
    {
        void Reset();

        /// <summary>The capsule for this frame (positions in the tile-local frame; <paramref name="ox"/>.. is the tile
        /// origin in game metres for the obstacle query) and its strength (0 = nothing fades, 1 = full).</summary>
        OccluderCapsule Frame(IViewObstacleQuery query, double ox, double oz, float camX, float camY, float camZ, float tx, float ty, float tz,
                              float dt, float tanHalfFovY, float aspect, out float strength);
    }

    /// <summary>The fade before the playtest-fix round: the camera-to-player capsule of
    /// <c>OccluderFadeSettings.Default</c> at that time, on every frame the world is open (ToonLook set
    /// <c>_GhOccluderB.w = 1</c>).</summary>
    internal sealed class LegacyOccluderModel : IOccluderModel
    {
        private static readonly OccluderFadeSettings Old = new OccluderFadeSettings
        {
            CameraRadiusM = 1.0f, NearCoverM = 0.8f, MaxCameraRadiusM = 2.0f, PlayerRadiusM = 1.3f, SoftFraction = 0.12f,
            SoftMinM = 0.15f, SoftMaxM = 0.6f, SolidRadiusM = 0.5f, MinOpacity = 0.18f, TargetHeightM = 1.0f, GroundKeepM = 0.5f,
            OverheadM = 0.8f,
        };

        public void Reset()
        {
        }

        public OccluderCapsule Frame(IViewObstacleQuery query, double ox, double oz, float camX, float camY, float camZ, float tx, float ty,
                                     float tz, float dt, float tanHalfFovY, float aspect, out float strength)
        {
            strength = 1f;
            return OccluderCapsule.From(Old, camX, camY, camZ, tx, ty, tz, tanHalfFovY, aspect);
        }
    }

    /// <summary>The fade since the playtest-fix round: <see cref="OccluderFadeGate"/> (sight test against the world's
    /// solids, hysteresis, smooth strength) with the narrow cone of <see cref="OccluderFadeSettings.Default"/>, exactly as
    /// ToonLook runs it.</summary>
    internal sealed class GateOccluderModel : IOccluderModel
    {
        public readonly OccluderFadeGate Gate = new OccluderFadeGate();

        public void Reset()
        {
            Gate.Reset();
        }

        public OccluderCapsule Frame(IViewObstacleQuery query, double ox, double oz, float camX, float camY, float camZ, float tx, float ty,
                                     float tz, float dt, float tanHalfFovY, float aspect, out float strength)
        {
            OccluderCapsule c = Gate.Update(query, ox, 0.0, oz, camX, camY, camZ, tx, ty, tz, dt, tanHalfFovY, aspect);
            strength = c.Strength;
            return c;
        }
    }

    /// <summary>A ride (or walk) along a real street: tile-local samples every 0.25 m.</summary>
    internal sealed class OccluderRide
    {
        public string Name;
        public float SpeedMps;
        public float BoomM, PitchDeg, PivotM;
        public readonly List<float> X = new List<float>(), Z = new List<float>();
    }

    /// <summary>Counters of one ride.</summary>
    internal struct RideStats
    {
        public int Frames, HiddenFrames, SustainedHiddenFrames, ClearedHiddenFrames;

        /// <summary>Frames where the explorer had been in plain view for at least a second and more than 0.02 m² of house
        /// wall in view was dissolved.</summary>
        public int FadedVisibleFrames;

        public float MaxFadedAreaM2;
        public int Toggles;

        /// <summary>Wall patches (≤ 0.3 m) that flipped between solid and dissolved three times or more.</summary>
        public int FlickerPatches;

        /// <summary>Frames where the camera's near plane cuts a house (the camera inside a projection).</summary>
        public int NearCutFrames;

        /// <summary>The frame with the most house surface dissolved while the explorer was in plain view (-1: none).</summary>
        public int WorstFrame;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                 "frames {0}, hidden {1} (sustained {2}, cleared {3}), dissolved-in-plain-view frames {4}, max dissolved {5:0.00} m², " +
                                 "toggles {6}, flicker patches {7}, near-plane cuts {8}",
                                 Frames, HiddenFrames, SustainedHiddenFrames, ClearedHiddenFrames, FadedVisibleFrames, MaxFadedAreaM2, Toggles,
                                 FlickerPatches, NearCutFrames);
        }
    }

    /// <summary>
    /// One spot: its level-10 tile, the world's obstacle query over the sample pack, the near-ring B0 houses within 45 m of
    /// the rides (tile-local metres) in a 2 m triangle grid, and the rides: the nearest motor road, and the nearest lane,
    /// service road or footway (walked), each in both directions.
    /// </summary>
    internal sealed class OccluderSpotScene
    {
        public const float Dt = 1f / 30f;
        public const float NearClipM = 0.4f;
        public const float PatchM = 0.3f;

        // Two-wheeler, landscape (CameraRigTable): 8 m boom, 13°, pivot 1 m, 62° horizontal FOV at 16:9.
        public const float Aspect = 16f / 9f;
        public static readonly float TanHalfX = (float)Math.Tan(31.0 * Math.PI / 180.0);
        public static readonly float TanHalfY = TanHalfX / Aspect;

        private static TileGroundQuery _ground;
        private static readonly Dictionary<string, OccluderSpotScene> Cache = new Dictionary<string, OccluderSpotScene>();

        public string Name;
        public TileData Tile;
        public MeshData Houses;
        public readonly List<OccluderRide> Rides = new List<OccluderRide>();
        private TriGrid _grid;
        private readonly Dictionary<int, int> _patchStart = new Dictionary<int, int>();
        private readonly List<float> _patches = new List<float>(); // per patch: cx, cy, cz, nx, ny, nz, area

        public static TileGroundQuery Ground
        {
            get
            {
                if (_ground == null) _ground = DrivingData.SampleGround(true);
                return _ground;
            }
        }

        public static OccluderSpotScene Load(OccluderSpot spot)
        {
            lock (Cache)
            {
                OccluderSpotScene s;
                if (Cache.TryGetValue(spot.Name, out s)) return s;
                s = new OccluderSpotScene();
                s.Build(spot);
                Cache[spot.Name] = s;
                return s;
            }
        }

        /// <summary>Writes a report under GHUMANTE_PREVIEW_DIR (no-op without it).</summary>
        public static void WriteReport(string name, string text)
        {
            if (!ObjDump.Enabled) return;
            string dir = Path.Combine(ObjDump.Dir, "stable");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), text);
        }

        private void Build(OccluderSpot spot)
        {
            Name = spot.Name;
            double gx, gz;
            WorldFrame.LonLatToGame(spot.Lon, spot.Lat, out gx, out gz);
            Tile = DrivingData.SampleTiles()[TileId.At(10, gx, gz)];
            float sx = (float)(gx - Tile.Tile.X0), sz = (float)(gz - Tile.Tile.Z0);
            RoadRecord main = Nearest(sx, sz, 80f, true, null);
            Assert.That(main, Is.Not.Null, spot.Name + ": no motor road");
            RoadRecord minor = Nearest(sx, sz, 150f, false, main);
            AddRides(main, sx, sz, "ride " + Label(main), 7f, 8.0f, 13f, 1.0f, 1.2f);
            if (minor != null) AddRides(minor, sx, sz, "walk " + Label(minor), 1.6f, 8.4f, 12f, 1.0f, 0.8f);
            // The owner's screenshot at Hiti Pokhari: on foot between two houses, off the road.
            AddGapWalk(sx, sz);

            // The near-ring B0 houses within 45 m of any ride sample, at the tier's richest level (High).
            MeshingSettings settings = StreamingScene.Settings(2, 10, false);
            int step = StreamingConfig.ForTier(2).TerrainStep(Tile.Tile, Tile.Tile, Tile.HeightsN);
            var sampler = TileHeightSampler.ForArea(Tile, Tile.Tile, step);
            _settings = settings;
            _sampler = sampler;
            BuildingOptions o = BuildingBandTable.NearOptions(2, settings.DetailBuildings);
            Houses = new MeshData();
            Houses.HasUv0 = true;
            var gen = new GenColliders();
            for (int i = 0; i < Tile.Buildings.Count; i++)
            {
                if (!NearRides(Tile.Buildings[i], 45f)) continue;
                BuildingDetailMesher.One(Tile, i, sampler, o, Houses, gen);
            }
            // The B0 houses' own colliders (plinths, stoops, roof parts), registered with the ground query as the
            // streamer's detail cells do.
            if (gen.Boxes.Count > 0 || gen.Ramps.Count > 0)
            {
                var sc = new StructureColliders();
                sc.AddFrom(gen);
                Ground.Register(Tile.Tile.Key, sc);
            }
            _grid = new TriGrid(Houses, 2f);
        }

        private static string Label(RoadRecord r)
        {
            return r.RoadClass.ToString().ToLowerInvariant();
        }

        internal bool NearRides(BuildingRecord b, float reach)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            if (b.Rings == null || b.Rings.Length == 0) return false;
            int[] p = b.Rings[0];
            for (int k = 0; k + 1 < p.Length; k += 2)
            {
                minX = Math.Min(minX, p[k] / 100f);
                maxX = Math.Max(maxX, p[k] / 100f);
                minZ = Math.Min(minZ, p[k + 1] / 100f);
                maxZ = Math.Max(maxZ, p[k + 1] / 100f);
            }
            foreach (OccluderRide r in Rides)
                for (int i = 0; i < r.X.Count; i += 8)
                {
                    float dx = Math.Max(0f, Math.Max(minX - r.X[i], r.X[i] - maxX)), dz = Math.Max(0f, Math.Max(minZ - r.Z[i], r.Z[i] - maxZ));
                    if (dx * dx + dz * dz < reach * reach) return true;
                }
            return false;
        }

        private RoadRecord Nearest(float x, float z, float within, bool motor, RoadRecord except)
        {
            RoadRecord best = null;
            float bestD = within;
            foreach (RoadRecord r in Tile.Roads)
            {
                if (r == except) continue;
                bool isMotor = r.RoadClass >= RoadClass.Motorway && r.RoadClass <= RoadClass.Service;
                if (motor != isMotor && !(!motor && (r.RoadClass == RoadClass.Residential || r.RoadClass == RoadClass.Service ||
                                                     r.RoadClass == RoadClass.LivingStreet || r.RoadClass == RoadClass.Unclassified)))
                    continue;
                if (!motor && r.RoadClass != RoadClass.Residential && r.RoadClass != RoadClass.Service && r.RoadClass != RoadClass.LivingStreet &&
                    r.RoadClass != RoadClass.Pedestrian && r.RoadClass != RoadClass.Footway && r.RoadClass != RoadClass.Path &&
                    r.RoadClass != RoadClass.Unclassified)
                    continue;
                int first, last;
                RoadSpatialIndex.RenderedRange(r, out first, out last);
                float len = 0f;
                for (int k = first; k < last; k++)
                {
                    float ax = r.Points[2 * k] / 100f, az = r.Points[2 * k + 1] / 100f, bx = r.Points[2 * k + 2] / 100f, bz = r.Points[2 * k + 3] / 100f;
                    len += (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                }
                if (len < 30f) continue;
                for (int k = first; k < last; k++)
                {
                    float ax = r.Points[2 * k] / 100f, az = r.Points[2 * k + 1] / 100f, bx = r.Points[2 * k + 2] / 100f, bz = r.Points[2 * k + 3] / 100f;
                    float d = SegDist(x, z, ax, az, bx, bz);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = r;
                    }
                }
            }
            return best;
        }

        private static float SegDist(float x, float z, float ax, float az, float bx, float bz)
        {
            float vx = bx - ax, vz = bz - az, l2 = vx * vx + vz * vz;
            float t = l2 > 1e-9f ? Math.Max(0f, Math.Min(1f, ((x - ax) * vx + (z - az) * vz) / l2)) : 0f;
            float px = ax + vx * t - x, pz = az + vz * t - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }

        /// <summary>Both directions along a road from its point nearest the spot, up to 150 m each way, offset to the left
        /// (Nepal keeps left) and kept out of the houses.</summary>
        private void AddRides(RoadRecord r, float sx, float sz, string name, float speed, float boom, float pitch, float pivot, float keepLeft)
        {
            int first, last;
            RoadSpatialIndex.RenderedRange(r, out first, out last);
            var px = new List<float>();
            var pz = new List<float>();
            for (int k = first; k <= last; k++)
            {
                float x = r.Points[2 * k] / 100f, z = r.Points[2 * k + 1] / 100f;
                if (px.Count > 0 && Math.Abs(x - px[px.Count - 1]) + Math.Abs(z - pz[pz.Count - 1]) < 1e-3f) continue;
                px.Add(x);
                pz.Add(z);
            }
            // Arc length of the nearest point.
            var s = new float[px.Count];
            for (int k = 1; k < px.Count; k++)
                s[k] = s[k - 1] + (float)Math.Sqrt((px[k] - px[k - 1]) * (px[k] - px[k - 1]) + (pz[k] - pz[k - 1]) * (pz[k] - pz[k - 1]));
            float nearS = 0f, bestD = float.MaxValue;
            for (int k = 0; k + 1 < px.Count; k++)
            {
                float vx = px[k + 1] - px[k], vz = pz[k + 1] - pz[k], l2 = vx * vx + vz * vz;
                float t = l2 > 1e-9f ? Math.Max(0f, Math.Min(1f, ((sx - px[k]) * vx + (sz - pz[k]) * vz) / l2)) : 0f;
                float d = (px[k] + vx * t - sx) * (px[k] + vx * t - sx) + (pz[k] + vz * t - sz) * (pz[k] + vz * t - sz);
                if (d < bestD)
                {
                    bestD = d;
                    nearS = s[k] + t * (s[k + 1] - s[k]);
                }
            }
            float total = s[s.Length - 1];
            float halfWidth = Math.Max(1.2f, r.WidthCm / 200f);
            foreach (int dir in new[] { 1, -1 })
            {
                float s0 = dir > 0 ? Math.Max(0f, nearS - 40f) : Math.Min(total, nearS + 40f);
                float s1 = dir > 0 ? Math.Min(total, s0 + 190f) : Math.Max(0f, s0 - 190f);
                if (Math.Abs(s1 - s0) < 25f) continue;
                var ride = new OccluderRide
                {
                    Name = name + (dir > 0 ? " forward" : " back"), SpeedMps = speed, BoomM = boom, PitchDeg = pitch, PivotM = pivot,
                };
                for (float a = s0; dir > 0 ? a <= s1 : a >= s1; a += dir * 0.25f)
                {
                    float x, z, hx, hz;
                    At(px, pz, s, a, out x, out z, out hx, out hz);
                    hx *= dir;
                    hz *= dir;
                    // Left of travel (x east, z north: left = (-hz, hx)).
                    float off = Math.Min(keepLeft, 0.45f * halfWidth);
                    float lx = x - hz * off, lz = z + hx * off;
                    double gxx = Tile.Tile.X0 + lx, gzz = Tile.Tile.Z0 + lz;
                    float h;
                    if (!Ground.TryTerrainHeight(gxx, gzz, out h)) h = 0f;
                    if (Ground.InsideSolid(gxx, gzz, h))
                    {
                        lx = x;
                        lz = z;
                    }
                    ride.X.Add(lx);
                    ride.Z.Add(lz);
                }
                Rides.Add(ride);
            }
        }

        /// <summary>
        /// A walk through the narrowest open gap between houses (1.6 to 5 m wide, at least 14 m long) within 70 m of the
        /// spot, both ways: the owner's "between buildings, off road" at Hiti Pokhari.
        /// </summary>
        private void AddGapWalk(float sx, float sz)
        {
            double ox = Tile.Tile.X0, oz = Tile.Tile.Z0;
            float bestScore = float.MaxValue, bx = 0f, bz = 0f, ax = 0f, az = 0f;
            for (float gz = sz - 70f; gz <= sz + 70f; gz += 1f)
            for (float gx = sx - 70f; gx <= sx + 70f; gx += 1f)
            {
                float y = FeetY(gx, gz);
                if (Ground.InsideSolid(ox + gx, oz + gz, y)) continue;
                for (int a = 0; a < 4; a++)
                {
                    double ang = a * Math.PI / 4.0, dx = Math.Sin(ang), dz = Math.Cos(ang), d1, d2;
                    if (!Ground.SphereCast(ox + gx, y + 1.0, oz + gz, dx, 0, dz, 0.2, 3.0, out d1)) continue;
                    if (!Ground.SphereCast(ox + gx, y + 1.0, oz + gz, -dx, 0, -dz, 0.2, 3.0, out d2)) continue;
                    float w = (float)(d1 + d2) + 0.4f;
                    if (w < 1.6f || w > 5f) continue;
                    // Along the gap: open for at least 7 m each way.
                    float alx = (float)dz, alz = (float)-dx;
                    if (OpenRun(gx, gz, alx, alz, 7f) < 7f || OpenRun(gx, gz, -alx, -alz, 7f) < 7f) continue;
                    float score = w + 0.02f * (float)Math.Sqrt((gx - sx) * (gx - sx) + (gz - sz) * (gz - sz));
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bx = gx;
                        bz = gz;
                        ax = alx;
                        az = alz;
                    }
                }
            }
            if (bestScore == float.MaxValue) return;
            foreach (int dir in new[] { 1, -1 })
            {
                float fx = ax * dir, fz = az * dir;
                float back = OpenRun(bx, bz, -fx, -fz, 25f), ahead = OpenRun(bx, bz, fx, fz, 25f);
                var ride = new OccluderRide { Name = "walk between houses" + (dir > 0 ? " forward" : " back"), SpeedMps = 1.4f, BoomM = 8.4f, PitchDeg = 12f, PivotM = 1.0f };
                for (float a = -back; a <= ahead; a += 0.25f)
                {
                    ride.X.Add(bx + fx * a);
                    ride.Z.Add(bz + fz * a);
                }
                if (ride.X.Count > 40) Rides.Add(ride);
            }
        }

        /// <summary>How far one can walk from (x, z) along a unit direction without entering a solid or walking through
        /// a wall or railing (0.25 m steps at knee and chest height, up to <paramref name="max"/>).</summary>
        private float OpenRun(float x, float z, float dx, float dz, float max)
        {
            double ox = Tile.Tile.X0, oz = Tile.Tile.Z0;
            float a = 0f;
            while (a < max)
            {
                float cx = x + dx * a, cz = z + dz * a, nx = x + dx * (a + 0.25f), nz = z + dz * (a + 0.25f);
                float y = FeetY(cx, cz);
                double hit;
                if (Ground.InsideSolid(ox + nx, oz + nz, FeetY(nx, nz))) break;
                if (Ground.SphereCast(ox + cx, y + 0.5, oz + cz, dx, 0, dz, 0.2, 0.45, out hit) ||
                    Ground.SphereCast(ox + cx, y + 1.3, oz + cz, dx, 0, dz, 0.2, 0.45, out hit))
                    break;
                a += 0.25f;
            }
            return a;
        }

        private static void At(List<float> px, List<float> pz, float[] s, float a, out float x, out float z, out float hx, out float hz)
        {
            int k = 0;
            while (k + 2 < s.Length && s[k + 1] < a) k++;
            float l = Math.Max(1e-6f, s[k + 1] - s[k]);
            float t = Math.Max(0f, Math.Min(1f, (a - s[k]) / l));
            x = px[k] + (px[k + 1] - px[k]) * t;
            z = pz[k] + (pz[k + 1] - pz[k]) * t;
            hx = (px[k + 1] - px[k]) / l;
            hz = (pz[k + 1] - pz[k]) / l;
        }

        /// <summary>Feet height at a tile-local point: the road or a bridge deck where there is one, else the terrain.</summary>
        public float FeetY(float x, float z)
        {
            GroundSample g;
            if (Ground.TrySample(Tile.Tile.X0 + x, Tile.Tile.Z0 + z, out g)) return g.Height;
            float h;
            return Ground.TryTerrainHeight(Tile.Tile.X0 + x, Tile.Tile.Z0 + z, out h) ? h : 0f;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The ride

        /// <summary>Rides at the ride's speed with the chase camera and evaluates <paramref name="model"/> every frame.</summary>
        public RideStats Run(OccluderRide ride, IOccluderModel model, Action<int, CameraFrame> onFrame = null)
        {
            model.Reset();
            var stats = new RideStats { WorstFrame = -1 };
            float worst = 0f;
            var cam = new ChaseCam(ride.BoomM, ride.PitchDeg, ride.PivotM);
            var state = new Dictionary<int, byte>(); // patch -> (faded bit | transitions << 1)
            var fadedNow = new HashSet<int>();
            var fadedPrev = new HashSet<int>();
            float visibleFor = 0f, hiddenFor = 0f;
            bool prevOn = false;
            double ox = Tile.Tile.X0, oz = Tile.Tile.Z0;
            float stepS = 0.25f / ride.SpeedMps;
            float clock = 0f;
            int frame = 0;
            for (float t = 0f; ; t += Dt)
            {
                int i = (int)(t / stepS);
                if (i + 4 >= ride.X.Count) break;
                float fx = ride.X[i], fz = ride.Z[i];
                float hx = ride.X[i + 4] - fx, hz = ride.Z[i + 4] - fz;
                float fy = FeetY(fx, fz);
                cam.Update(Ground, ox, oz, fx, fy, fz, hx, hz, Dt, frame == 0);
                var f = new CameraFrame { Cx = cam.X, Cy = cam.Y, Cz = cam.Z, Fx = fx, Fy = fy, Fz = fz };
                float strength;
                f.Capsule = model.Frame(Ground, ox, oz, cam.X, cam.Y, cam.Z, fx, fy + 1.0f, fz, Dt, TanHalfY, Aspect, out strength);
                f.Strength = strength;
                bool visible = PlayerVisible(f);
                f.Visible = visible;
                stats.Frames++;
                if (visible)
                {
                    visibleFor += Dt;
                    hiddenFor = 0f;
                }
                else
                {
                    hiddenFor += Dt;
                    visibleFor = 0f;
                    stats.HiddenFrames++;
                }
                bool on = strength > 0.001f;
                if (on != prevOn) stats.Toggles++;
                prevOn = on;

                float faded = Evaluate(f, fadedNow);
                if (faded > stats.MaxFadedAreaM2 && visible) stats.MaxFadedAreaM2 = faded;
                if (visibleFor >= 1f && faded > 0.02f) stats.FadedVisibleFrames++;
                if (visible && faded > worst)
                {
                    worst = faded;
                    stats.WorstFrame = frame;
                }
                if (hiddenFor >= 1f)
                {
                    stats.SustainedHiddenFrames++;
                    if (ChestCleared(f)) stats.ClearedHiddenFrames++;
                }
                if (NearCut(f)) stats.NearCutFrames++;

                // Flips between solid and dissolved, per patch.
                foreach (int p in fadedNow)
                    if (!fadedPrev.Contains(p)) Flip(state, p);
                foreach (int p in fadedPrev)
                    if (!fadedNow.Contains(p)) Flip(state, p);
                HashSet<int> tmp = fadedPrev;
                fadedPrev = fadedNow;
                fadedNow = tmp;
                fadedNow.Clear();
                onFrame?.Invoke(frame, f);
                frame++;
                clock += Dt;
            }
            foreach (KeyValuePair<int, byte> kv in state)
                if (kv.Value >= 3) stats.FlickerPatches++;
            return stats;
        }

        private static void Flip(Dictionary<int, byte> state, int patch)
        {
            byte v;
            state.TryGetValue(patch, out v);
            if (v < 255) v++;
            state[patch] = v;
        }

        /// <summary>The frame as the harness saw it.</summary>
        internal struct CameraFrame
        {
            public float Cx, Cy, Cz, Fx, Fy, Fz;
            public OccluderCapsule Capsule;
            public float Strength;
            public bool Visible;
        }

        /// <summary>The camera's look direction (at the pivot over the explorer) and its right/up axes.</summary>
        private static void Basis(in CameraFrame f, out float fx, out float fy, out float fz, out float rx, out float rz, out float ux,
                                  out float uy, out float uz)
        {
            fx = f.Fx - f.Cx;
            fy = f.Fy + 1.0f - f.Cy;
            fz = f.Fz - f.Cz;
            float l = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            fx /= l;
            fy /= l;
            fz /= l;
            // right = up × forward (left-handed, x east, z north), up' = forward × right.
            rx = fz;
            rz = -fx;
            float rl = (float)Math.Sqrt(rx * rx + rz * rz);
            rx /= rl;
            rz /= rl;
            ux = fy * rz;
            uy = fz * rx - fx * rz;
            uz = -fy * rx;
        }

        /// <summary>True when the point is in front of the camera's near plane and inside its view.</summary>
        private static bool InView(in CameraFrame f, float px, float py, float pz)
        {
            float fx, fy, fz, rx, rz, ux, uy, uz;
            Basis(f, out fx, out fy, out fz, out rx, out rz, out ux, out uy, out uz);
            float dx = px - f.Cx, dy = py - f.Cy, dz = pz - f.Cz;
            float d = dx * fx + dy * fy + dz * fz;
            if (d < NearClipM) return false;
            float x = dx * rx + dz * rz, y = dx * ux + dy * uy + dz * uz;
            return Math.Abs(x) <= TanHalfX * d && Math.Abs(y) <= TanHalfY * d;
        }

        /// <summary>Dissolved house area in view this frame (m², weighted by how dissolved), and the patches that are
        /// at least half dissolved.</summary>
        private float Evaluate(in CameraFrame f, HashSet<int> fadedPatches)
        {
            OccluderCapsule c = f.Capsule;
            if (f.Strength <= 0.001f) return 0f;
            float reach = Math.Max(c.CameraRadius, c.PlayerRadius) + c.Soft + 0.2f;
            float total = 0f;
            float minO = c.MinOpacity;
            foreach (int tri in _grid.Near(c.Ax, c.Az, c.Bx, c.Bz, reach))
            {
                float bx, by, bz, br;
                _grid.Sphere(tri, out bx, out by, out bz, out br);
                if (SegmentDistance(bx, by, bz, c.Ax, c.Ay, c.Az, c.Bx, c.By, c.Bz) > br + reach) continue;
                int start = Patches(tri), count = PatchCount(tri);
                for (int k = 0; k < count; k++)
                {
                    int o = (start + k) * 7;
                    float px = _patches[o], py = _patches[o + 1], pz = _patches[o + 2];
                    float nx = _patches[o + 3], ny = _patches[o + 4], nz = _patches[o + 5], area = _patches[o + 6];
                    if (nx * (f.Cx - px) + ny * (f.Cy - py) + nz * (f.Cz - pz) <= 0f) continue; // back face: culled
                    if (!InView(f, px, py, pz)) continue;
                    float op = c.Opacity(px, py, pz, ny, false);
                    if (op >= 0.999f) continue;
                    float w = (1f - op) / Math.Max(1e-3f, 1f - minO);
                    total += area * w;
                    if (op < 0.5f) fadedPatches.Add(start + k);
                }
            }
            return total;
        }

        private int Patches(int tri)
        {
            int start;
            if (_patchStart.TryGetValue(tri, out start)) return start;
            start = _patches.Count / 7;
            _patchStart[tri] = start;
            float ax, ay, az, bx, by, bz, cx, cy, cz;
            _grid.Corners(tri, out ax, out ay, out az, out bx, out by, out bz, out cx, out cy, out cz);
            float ex1 = bx - ax, ey1 = by - ay, ez1 = bz - az, ex2 = cx - ax, ey2 = cy - ay, ez2 = cz - az;
            float nx = ey1 * ez2 - ez1 * ey2, ny = ez1 * ex2 - ex1 * ez2, nz = ex1 * ey2 - ey1 * ex2;
            float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (nl < 1e-9f)
            {
                _patchCount[tri] = 0;
                return start;
            }
            // Unity winding: front = cross(b - a, c - a) is the left-handed normal; the meshes store outward normals
            // with that convention, which in this right-handed arithmetic is -(cross). Use the stored vertex normals.
            float snx, sny, snz;
            _grid.Normal(tri, out snx, out sny, out snz);
            Split(ax, ay, az, bx, by, bz, cx, cy, cz, snx, sny, snz, 0);
            _patchCount[tri] = _patches.Count / 7 - start;
            return start;
        }

        private readonly Dictionary<int, int> _patchCount = new Dictionary<int, int>();

        private int PatchCount(int tri)
        {
            return _patchCount[tri];
        }

        private void Split(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, float nx, float ny, float nz, int depth)
        {
            float ab = D2(ax, ay, az, bx, by, bz), bc = D2(bx, by, bz, cx, cy, cz), ca = D2(cx, cy, cz, ax, ay, az);
            float m = Math.Max(ab, Math.Max(bc, ca));
            if (m <= PatchM * PatchM || depth > 18)
            {
                float ex1 = bx - ax, ey1 = by - ay, ez1 = bz - az, ex2 = cx - ax, ey2 = cy - ay, ez2 = cz - az;
                float qx = ey1 * ez2 - ez1 * ey2, qy = ez1 * ex2 - ex1 * ez2, qz = ex1 * ey2 - ey1 * ex2;
                float area = 0.5f * (float)Math.Sqrt(qx * qx + qy * qy + qz * qz);
                _patches.Add((ax + bx + cx) / 3f);
                _patches.Add((ay + by + cy) / 3f);
                _patches.Add((az + bz + cz) / 3f);
                _patches.Add(nx);
                _patches.Add(ny);
                _patches.Add(nz);
                _patches.Add(area);
                return;
            }
            if (m == ab)
            {
                float mx = 0.5f * (ax + bx), my = 0.5f * (ay + by), mz = 0.5f * (az + bz);
                Split(ax, ay, az, mx, my, mz, cx, cy, cz, nx, ny, nz, depth + 1);
                Split(mx, my, mz, bx, by, bz, cx, cy, cz, nx, ny, nz, depth + 1);
            }
            else if (m == bc)
            {
                float mx = 0.5f * (bx + cx), my = 0.5f * (by + cy), mz = 0.5f * (bz + cz);
                Split(ax, ay, az, bx, by, bz, mx, my, mz, nx, ny, nz, depth + 1);
                Split(ax, ay, az, mx, my, mz, cx, cy, cz, nx, ny, nz, depth + 1);
            }
            else
            {
                float mx = 0.5f * (cx + ax), my = 0.5f * (cy + ay), mz = 0.5f * (cz + az);
                Split(ax, ay, az, bx, by, bz, mx, my, mz, nx, ny, nz, depth + 1);
                Split(mx, my, mz, bx, by, bz, cx, cy, cz, nx, ny, nz, depth + 1);
            }
        }

        private static float D2(float ax, float ay, float az, float bx, float by, float bz)
        {
            return (bx - ax) * (bx - ax) + (by - ay) * (by - ay) + (bz - az) * (bz - az);
        }

        private static float SegmentDistance(float px, float py, float pz, float ax, float ay, float az, float bx, float by, float bz)
        {
            float vx = bx - ax, vy = by - ay, vz = bz - az, l2 = vx * vx + vy * vy + vz * vz;
            float t = l2 > 1e-9f ? Math.Max(0f, Math.Min(1f, ((px - ax) * vx + (py - ay) * vy + (pz - az) * vz) / l2)) : 0f;
            float dx = ax + vx * t - px, dy = ay + vy * t - py, dz = az + vz * t - pz;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>The explorer is in plain view when any of the rays from the camera to the hips, chest and head misses
        /// every house triangle (hits within 0.25 m of the body ignored).</summary>
        public bool PlayerVisible(in CameraFrame f)
        {
            foreach (float h in new[] { 0.6f, 1.1f, 1.6f })
            {
                float d;
                if (!_grid.Raycast(f.Cx, f.Cy, f.Cz, f.Fx, f.Fy + h, f.Fz, 0.25f, out d)) return true;
            }
            return false;
        }

        /// <summary>Every house surface the ray from the camera to the chest crosses is at least half dissolved.</summary>
        public bool ChestCleared(in CameraFrame f)
        {
            float tx = f.Fx, ty = f.Fy + 1.1f, tz = f.Fz;
            float dx = tx - f.Cx, dy = ty - f.Cy, dz = tz - f.Cz;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float from = 0f;
            for (int guard = 0; guard < 16; guard++)
            {
                float sx = f.Cx + dx / len * from, sy = f.Cy + dy / len * from, sz = f.Cz + dz / len * from, d;
                if (!_grid.Raycast(sx, sy, sz, tx, ty, tz, 0.25f, out d)) return true;
                float hx = sx + dx / len * d, hy = sy + dy / len * d, hz = sz + dz / len * d;
                float op = f.Capsule.Opacity(hx, hy, hz, 0f, false);
                if (op > 0.5f) return false;
                from += d + 0.01f;
                if (from >= len - 0.25f) return true;
            }
            return true;
        }

        /// <summary>
        /// Up to <paramref name="max"/> camera/explorer pairs across a small house (2.5 to 6.5 m from its centre to its
        /// corners) near the rides, about a chase camera's distance apart: the camera 4 m outside one side of its footprint
        /// at 2.5 m, the explorer 3 m outside the opposite side, both outside every solid, the
        /// explorer hidden from the camera by the drawn houses (truth) and by the world's solids (what the gate sees).
        /// </summary>
        public List<CameraFrame> HiddenSetups(int max)
        {
            var found = new List<CameraFrame>();
            OccluderFadeSettings st = OccluderFadeSettings.Default;
            double ox = Tile.Tile.X0, oz = Tile.Tile.Z0;
            for (int i = 0; i < Tile.Buildings.Count && found.Count < max; i++)
            {
                BuildingRecord b = Tile.Buildings[i];
                if (b.Rings == null || b.Rings.Length == 0 || !NearRides(b, 25f)) continue;
                int[] p = b.Rings[0];
                int n = p.Length / 2;
                if (n < 3) continue;
                float cx = 0f, cz = 0f;
                for (int k = 0; k < n; k++)
                {
                    cx += p[2 * k] / 100f;
                    cz += p[2 * k + 1] / 100f;
                }
                cx /= n;
                cz /= n;
                float r = 0f;
                for (int k = 0; k < n; k++)
                    r = Math.Max(r, (float)Math.Sqrt((p[2 * k] / 100f - cx) * (p[2 * k] / 100f - cx) + (p[2 * k + 1] / 100f - cz) * (p[2 * k + 1] / 100f - cz)));
                if (r < 2.5f || r > 6.5f) continue;
                for (int a = 0; a < 360 && found.Count < max; a += 45)
                {
                    float ux = (float)Math.Sin(a * Math.PI / 180.0), uz = (float)Math.Cos(a * Math.PI / 180.0);
                    float camX = cx + ux * (r + 4f), camZ = cz + uz * (r + 4f), fx = cx - ux * (r + 3f), fz = cz - uz * (r + 3f);
                    float camGround = FeetY(camX, camZ), fy = FeetY(fx, fz);
                    if (Ground.InsideSolid(ox + camX, oz + camZ, camGround) || Ground.InsideSolid(ox + fx, oz + fz, fy)) continue;
                    var f = new CameraFrame { Cx = camX, Cy = camGround + 2.5f, Cz = camZ, Fx = fx, Fy = fy, Fz = fz };
                    if (PlayerVisible(f)) continue;
                    if (!OccluderFadeGate.IsHidden(Ground, st, ox + f.Cx, f.Cy, oz + f.Cz, ox + fx, fy + st.TargetHeightM, oz + fz)) continue;
                    found.Add(f);
                }
            }
            return found;
        }

        private TileBuild _context;
        private MeshData _lite, _b1;
        private readonly Dictionary<MeshData, TriGrid> _grids = new Dictionary<MeshData, TriGrid>();
        private TileHeightSampler _sampler;
        private MeshingSettings _settings;

        /// <summary>The same houses as <see cref="Houses"/> at the lite ring's level (High), as the streamer's detail cells
        /// build them beyond the near ring.</summary>
        public MeshData LiteHouses
        {
            get
            {
                if (_lite != null) return _lite;
                BuildingOptions lite = BuildingBandTable.LiteOptions(2, _settings.DetailBuildings);
                _lite = new MeshData();
                _lite.HasUv0 = true;
                for (int i = 0; i < Tile.Buildings.Count; i++)
                    if (NearRides(Tile.Buildings[i], 45f)) BuildingDetailMesher.One(Tile, i, _sampler, lite, _lite, null);
                return _lite;
            }
        }

        /// <summary>The tile's B1 band (styled extrusions with their street-front detail), as the streamer's B1 layer.</summary>
        public MeshData B1Houses
        {
            get
            {
                if (_b1 != null) return _b1;
                _b1 = new MeshData();
                _b1.HasUv0 = true;
                BuildingMesher.Build(Tile, _sampler, _settings.Buildings, _b1);
                return _b1;
            }
        }

        /// <summary>A layer of the spot's tile as the streamer uploads it (High, October), cropped to
        /// <paramref name="within"/> m of the spot's rides' first sample.</summary>
        public MeshData ContextLayer(int layer, float within)
        {
            if (_context == null) _context = StreamingScene.Build(Tile.Tile, StreamingScene.Settings(2, 10));
            float x = Rides.Count > 0 ? Rides[0].X[0] : 512f, z = Rides.Count > 0 ? Rides[0].Z[0] : 512f;
            return ObjDump.Crop(StreamingScene.Layer(_context, layer), x - within, z - within, x + within, z + within, true);
        }

        private TriGrid GridOf(MeshData m)
        {
            if (m == Houses) return _grid;
            TriGrid g;
            if (!_grids.TryGetValue(m, out g)) _grids[m] = g = new TriGrid(m, 2f);
            return g;
        }

        /// <summary>
        /// The cross-fade at a band edge <paramref name="edgeM"/> from the camera between an inner band's mesh (fading out)
        /// and an outer band's (fading in), as the band shader draws them: for wall patches of the inner mesh in the 4 m
        /// fade ring seen from the rides' chase camera (every 2 s), the share of the 16 Bayer levels at which no band's
        /// fragment on the patch's pixel survives (a hole into the house) and at which both do within 3 mm (z-fighting),
        /// under three schemes: complementary dithers (the shader before playtest #4), complementary plus a 12% overlap of
        /// the incoming band (the first attempt) and the stencil hand-off (ToonLitInput.hlsl GhBandClip: the outgoing band
        /// dithers out, the incoming band draws its whole fade-in where the outgoing one did not draw). Back faces are
        /// culled, so only front-facing outer fragments within 3 m behind the patch count.
        /// </summary>
        public BandSeam MeasureSeam(MeshData inner, MeshData outer, float edgeM)
        {
            TriGrid gi = GridOf(inner), go = GridOf(outer);
            var seam = new BandSeam();
            float h = 0.5f * BandConfig.FadeM;
            var pieces = new List<float>();
            var outerT = new float[6];
            var outerD = new float[6];
            foreach (OccluderRide ride in Rides)
            {
                Run(ride, new GateOccluderModel(), (i, f) =>
                {
                    if (i % 60 != 30) return;
                    seam.Cameras++;
                    for (int t = 0; t < inner.IndexCount / 3; t++)
                    {
                        float bx, by, bz, br;
                        gi.Sphere(t, out bx, out by, out bz, out br);
                        float dc = (float)Math.Sqrt((bx - f.Cx) * (bx - f.Cx) + (bz - f.Cz) * (bz - f.Cz));
                        if (dc < edgeM - h - br || dc > edgeM + h + br) continue;
                        float ax, ay, az, b2x, b2y, b2z, cx, cy, cz, nx, ny, nz;
                        gi.Corners(t, out ax, out ay, out az, out b2x, out b2y, out b2z, out cx, out cy, out cz);
                        gi.Normal(t, out nx, out ny, out nz);
                        if (nx * (f.Cx - bx) + ny * (f.Cy - by) + nz * (f.Cz - bz) <= -br) continue; // faces away throughout
                        pieces.Clear();
                        SplitInto(pieces, ax, ay, az, b2x, b2y, b2z, cx, cy, cz, 0.5f, 0, default(OccluderCapsule), float.MaxValue);
                        for (int k = 0; k < pieces.Count; k += 9)
                        {
                            float px = (pieces[k] + pieces[k + 3] + pieces[k + 6]) / 3f, py = (pieces[k + 1] + pieces[k + 4] + pieces[k + 7]) / 3f,
                                  pz = (pieces[k + 2] + pieces[k + 5] + pieces[k + 8]) / 3f;
                            float d = (float)Math.Sqrt((px - f.Cx) * (px - f.Cx) + (pz - f.Cz) * (pz - f.Cz));
                            if (d < edgeM - h || d > edgeM + h) continue;
                            if (nx * (f.Cx - px) + ny * (f.Cy - py) + nz * (f.Cz - pz) <= 0f || !InView(f, px, py, pz)) continue;
                            float ex1 = pieces[k + 3] - pieces[k], ey1 = pieces[k + 4] - pieces[k + 1], ez1 = pieces[k + 5] - pieces[k + 2];
                            float ex2 = pieces[k + 6] - pieces[k], ey2 = pieces[k + 7] - pieces[k + 1], ez2 = pieces[k + 8] - pieces[k + 2];
                            float qx = ey1 * ez2 - ez1 * ey2, qy = ez1 * ex2 - ex1 * ez2, qz = ex1 * ey2 - ey1 * ex2;
                            float area = 0.5f * (float)Math.Sqrt(qx * qx + qy * qy + qz * qz);
                            // The pixel's ray through the patch.
                            float rx = px - f.Cx, ry = py - f.Cy, rz = pz - f.Cz;
                            float len = (float)Math.Sqrt(rx * rx + ry * ry + rz * rz);
                            float ux = rx / len, uy = ry / len, uz = rz / len;
                            float tA;
                            if (gi.FirstHit(f.Cx, f.Cy, f.Cz, ux, uy, uz, len + 0.02f, out tA) < 0) continue;
                            if (tA < len - 0.05f) continue; // the patch is hidden behind the inner band's own surfaces
                            float hz = (float)Math.Sqrt(ux * ux + uz * uz);
                            // The outer band's front faces along the ray, to 3 m behind the patch.
                            int no = 0;
                            float from = 0f;
                            while (no < outerT.Length)
                            {
                                float tB;
                                int tri = go.FirstHit(f.Cx + ux * from, f.Cy + uy * from, f.Cz + uz * from, ux, uy, uz, len + 3f - from, out tB);
                                if (tri < 0) break;
                                tB += from;
                                float onx, ony, onz;
                                go.Normal(tri, out onx, out ony, out onz);
                                if (onx * ux + ony * uy + onz * uz < 0f)
                                {
                                    outerT[no] = tB;
                                    outerD[no] = hz * tB;
                                    no++;
                                }
                                from = tB + 0.001f;
                            }
                            if (no == 0) continue; // nothing of the outer band behind: the inner band's detail fades out as intended
                            float fa = Clamp01((edgeM + h - hz * tA) / BandConfig.FadeM);
                            seam.Patches++;
                            seam.AreaM2 += area;
                            for (int scheme = 0; scheme < 3; scheme++)
                            {
                                int hole = 0, both = 0;
                                for (int lvl = 0; lvl < 16; lvl++)
                                {
                                    float b = (lvl + 0.5f) / 16f;
                                    bool aShows = b < fa;
                                    bool anyOuter = false, coplanar = false;
                                    for (int o = 0; o < no; o++)
                                    {
                                        float fo = Clamp01((edgeM + h - outerD[o]) / BandConfig.FadeM);
                                        bool shows = scheme == 2
                                            ? !aShows && outerD[o] >= edgeM - h // stencil: fills what the inner band left
                                            : b >= fo - (scheme == 1 ? 0.12f : 0f); // dithered fade-in, complementary (+ overlap)
                                        if (!shows) continue;
                                        anyOuter = true;
                                        if (Math.Abs(outerT[o] - tA) < 0.003f) coplanar = true;
                                    }
                                    if (!aShows && !anyOuter) hole++;
                                    if (aShows && coplanar) both++;
                                }
                                seam.Holes[scheme] += area * hole / 16.0;
                                seam.Doubles[scheme] += area * both / 16.0;
                            }
                        }
                    }
                });
            }
            return seam;
        }

        /// <summary>One building of the spot's tile as the near ring draws it (High).</summary>
        public MeshData House(int i)
        {
            var m = new MeshData();
            m.HasUv0 = true;
            BuildingDetailMesher.One(Tile, i, _sampler, BuildingBandTable.NearOptions(2, _settings.DetailBuildings), m, null);
            return m;
        }

        /// <summary>The chase-camera frame (every 2 s of every ride) with the most front-facing house wall area of
        /// <paramref name="m"/> in the 4 m fade ring around <paramref name="edgeM"/> and in view.</summary>
        public CameraFrame FrameWithMostRing(MeshData m, float edgeM)
        {
            TriGrid g = GridOf(m);
            float h = 0.5f * BandConfig.FadeM;
            var best = new CameraFrame();
            double bestArea = -1;
            foreach (OccluderRide ride in Rides)
                Run(ride, new GateOccluderModel(), (i, f) =>
                {
                    if (i % 60 != 30) return;
                    double area = 0, ax = 0, ay = 0, az = 0;
                    for (int t = 0; t < m.IndexCount / 3; t++)
                    {
                        float bx, by, bz, br;
                        g.Sphere(t, out bx, out by, out bz, out br);
                        float d = (float)Math.Sqrt((bx - f.Cx) * (bx - f.Cx) + (bz - f.Cz) * (bz - f.Cz));
                        if (Math.Abs(d - edgeM) > h || !InView(f, bx, by, bz)) continue;
                        float nx, ny, nz;
                        g.Normal(t, out nx, out ny, out nz);
                        if (Math.Abs(ny) > 0.3f || nx * (f.Cx - bx) + ny * (f.Cy - by) + nz * (f.Cz - bz) <= 0f) continue;
                        area += br * br;
                        ax += br * br * bx;
                        ay += br * br * by;
                        az += br * br * bz;
                    }
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = f;
                        // Aim at the middle of that wall (Basis looks at F + 1 m up).
                        best.Fx = (float)(ax / area);
                        best.Fy = (float)(ay / area) - 1f;
                        best.Fz = (float)(az / area);
                    }
                });
            return best;
        }

        /// <summary>
        /// Ray-traces <paramref name="frame"/> at 480 × 270 through the inner band's and the outer band's meshes and
        /// writes a binary PPM: what each pixel shows under the band <paramref name="scheme"/> (0 complementary dithers, 2
        /// stencil hand-off; see <see cref="MeasureSeam"/>), the 4 × 4 Bayer level taken from the pixel position as the
        /// GPU does. Inner fragments warm, outer cool (shaded by a fixed sun), no band magenta where a band's house is
        /// there but nothing draws, sky grey-blue. <paramref name="holes"/>: magenta pixels.
        /// </summary>
        public void TraceSeam(in CameraFrame frame, MeshData inner, MeshData outer, float edgeM, int scheme, string path, out int holes, float zoom = 1f)
        {
            const int W = 480, H = 270;
            TriGrid gi = GridOf(inner), go = GridOf(outer);
            float h = 0.5f * BandConfig.FadeM;
            float fx, fy, fz, rx, rz, ux, uy, uz;
            Basis(frame, out fx, out fy, out fz, out rx, out rz, out ux, out uy, out uz);
            var px = new byte[W * H * 3];
            holes = 0;
            float lx = 0.45f, ly = 0.8f, lz = 0.4f;
            float ll = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= ll;
            ly /= ll;
            lz /= ll;
            for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                float sx = ((c + 0.5f) / W * 2f - 1f) * TanHalfX / zoom, sy = (1f - (r + 0.5f) / H * 2f) * TanHalfY / zoom;
                float dx = fx + rx * sx + ux * sy, dy = fy + uy * sy, dz = fz + rz * sx + uz * sy;
                float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                dx /= dl;
                dy /= dl;
                dz /= dl;
                float hz = (float)Math.Sqrt(dx * dx + dz * dz);
                float b = (Bayer[(c & 3) + 4 * (r & 3)] + 0.5f) / 16f;
                // The nearest drawn fragment of each band along the ray (front faces only), under the scheme.
                float tI = float.MaxValue, tO = float.MaxValue;
                int triI = -1, triO = -1;
                bool anyBand = false;
                for (int band = 0; band < 2; band++)
                {
                    TriGrid g = band == 0 ? gi : go;
                    float from = 0.4f;
                    for (int k = 0; k < 8; k++)
                    {
                        float t;
                        int tri = g.FirstHit(frame.Cx + dx * from, frame.Cy + dy * from, frame.Cz + dz * from, dx, dy, dz, 150f - from, out t);
                        if (tri < 0) break;
                        t += from;
                        from = t + 0.001f;
                        float nx, ny, nz;
                        g.Normal(tri, out nx, out ny, out nz);
                        if (nx * dx + ny * dy + nz * dz >= 0f) continue;
                        float d = hz * t;
                        bool inRange = band == 0 ? d <= edgeM + h : d >= edgeM - h;
                        if (!inRange) continue;
                        anyBand = true;
                        float fade = Clamp01((edgeM + h - d) / BandConfig.FadeM); // the inner band's fade-out at d
                        bool draws = band == 0 ? b < fade : scheme == 2 ? true : b >= fade;
                        if (!draws) continue;
                        if (band == 0)
                        {
                            tI = t;
                            triI = tri;
                        }
                        else
                        {
                            tO = t;
                            triO = tri;
                        }
                        break;
                    }
                }
                // Stencil: the outer band never draws where the inner one drew.
                if (scheme == 2 && triI >= 0) triO = -1;
                int shown = -1;
                bool isInner = false;
                if (triI >= 0 && (triO < 0 || tI <= tO))
                {
                    shown = triI;
                    isInner = true;
                }
                else if (triO >= 0) shown = triO;
                int o = 3 * (r * W + c);
                if (shown < 0)
                {
                    if (anyBand)
                    {
                        holes++;
                        px[o] = 255;
                        px[o + 1] = 0;
                        px[o + 2] = 255;
                    }
                    else
                    {
                        px[o] = 170;
                        px[o + 1] = 190;
                        px[o + 2] = 210;
                    }
                    continue;
                }
                MeshData m = isInner ? inner : outer;
                TriGrid gs = isInner ? gi : go;
                float snx, sny, snz;
                gs.Normal(shown, out snx, out sny, out snz);
                float lit = 0.45f + 0.55f * Math.Max(0f, snx * lx + sny * ly + snz * lz);
                int v = m.Indices[3 * shown];
                float cr = m.Colors[4 * v] / 255f, cg = m.Colors[4 * v + 1] / 255f, cb = m.Colors[4 * v + 2] / 255f;
                // Tint: near warm, lite cool (subtle, so the house still reads).
                if (isInner)
                {
                    cr = 0.75f * cr + 0.25f;
                    cb = 0.85f * cb;
                }
                else
                {
                    cb = 0.75f * cb + 0.25f;
                    cr = 0.85f * cr;
                }
                px[o] = (byte)Math.Min(255, (int)(255 * cr * lit));
                px[o + 1] = (byte)Math.Min(255, (int)(255 * cg * lit));
                px[o + 2] = (byte)Math.Min(255, (int)(255 * cb * lit));
            }
            using (var fs = File.Create(path))
            {
                byte[] head = Encoding.ASCII.GetBytes("P6\n" + W + " " + H + "\n255\n");
                fs.Write(head, 0, head.Length);
                fs.Write(px, 0, px.Length);
            }
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>
        /// What the rides' chase camera sees of a house mesh, every <paramref name="every"/> frames, as a grid of
        /// <paramref name="cols"/> × <paramref name="rows"/> rays from the near plane over the view: rays whose first hit
        /// within <paramref name="maxM"/> is a house surface seen from behind (back-face culled: the wall is see-through,
        /// the street or the sky behind the house shows), split into those within 1.5 m of the lens (the camera brushing a
        /// house's skin) and those beyond (gaps and open shells in the mesh).
        /// </summary>
        public SeeThrough MeasureSeeThrough(MeshData m, int every, int cols, int rows, float maxM, Action<int> onBackHit = null)
        {
            TriGrid g = GridOf(m);
            var st = new SeeThrough();
            var channels = new Dictionary<int, int>();
            foreach (OccluderRide ride in Rides)
            {
                Run(ride, new GateOccluderModel(), (i, f) =>
                {
                    if (i % every != every / 2) return;
                    st.Cameras++;
                    float fx, fy, fz, rx, rz, ux, uy, uz;
                    Basis(f, out fx, out fy, out fz, out rx, out rz, out ux, out uy, out uz);
                    for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        float sx = ((c + 0.5f) / cols * 2f - 1f) * TanHalfX, sy = ((r + 0.5f) / rows * 2f - 1f) * TanHalfY;
                        float dx = fx + rx * sx + ux * sy, dy = fy + uy * sy, dz = fz + rz * sx + uz * sy;
                        float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        dx /= dl;
                        dy /= dl;
                        dz /= dl;
                        float t0 = NearClipM * dl; // the near plane along this ray
                        st.Rays++;
                        float hit;
                        int tri = g.FirstHit(f.Cx + dx * t0, f.Cy + dy * t0, f.Cz + dz * t0, dx, dy, dz, maxM, out hit);
                        if (tri < 0) continue;
                        st.Hits++;
                        float nx, ny, nz;
                        g.Normal(tri, out nx, out ny, out nz);
                        if (nx * dx + ny * dy + nz * dz <= 0f) continue;
                        if (hit + t0 < 1.5f) st.NearBack++;
                        else
                        {
                            st.FarBack++;
                            onBackHit?.Invoke(tri);
                            int v = m.Indices[3 * tri];
                            int ch = m.HasUv0 ? (int)Math.Round(m.Uv0[2 * v]) : -1;
                            channels[ch] = (channels.TryGetValue(ch, out int n) ? n : 0) + 1;
                            if (st.Examples.Count < 12)
                                st.Examples.Add(string.Format(CultureInfo.InvariantCulture, "{0} at ({1:0.00}, {2:0.00}, {3:0.00}) {4:0.0} m, n ({5:0.00}, {6:0.00}, {7:0.00}), channel {8}",
                                                              Name, f.Cx + dx * (t0 + hit), f.Cy + dy * (t0 + hit), f.Cz + dz * (t0 + hit), t0 + hit, nx, ny, nz,
                                                              ch >= 0 ? ((MaterialChannel)ch).ToString() : "-"));
                        }
                    }
                });
            }
            foreach (KeyValuePair<int, int> kv in channels)
                st.Channels.Add((kv.Key >= 0 ? ((MaterialChannel)kv.Key).ToString() : "-") + " " + kv.Value);
            return st;
        }

        /// <summary>
        /// Writes one frame as OBJ files under <c>stable/occluder/&lt;name&gt;/</c>: the drawn houses with the frame's fade
        /// emulated (6 cm cells dropped where the 4 × 4 screen door discards them) within 60 m of the camera, the tile's
        /// terrain, roads, markings and areas within 90 m, and the explorer; the camera as the files' eye, the explorer's chest as the
        /// look-at point, and a render.sh with the preview commands.
        /// </summary>
        public void DumpFrame(string name, in CameraFrame f)
        {
            if (!ObjDump.Enabled) return;
            CultureInfo c = CultureInfo.InvariantCulture;
            string dir = "stable/occluder/" + name;
            string eye = string.Format(c, "{0:0.###},{1:0.###},{2:0.###}", f.Cx, f.Cy, -f.Cz);
            string look = string.Format(c, "{0:0.###},{1:0.###},{2:0.###}", f.Fx, f.Fy + 1.0f, -f.Fz);
            var directives = new[]
            {
                "occluder fade at " + Name + ": strength " + f.Strength.ToString("0.00", c) + ", explorer " + (f.Visible ? "in plain view" : "hidden"),
                "meshpreview: grid=0 sun=160,62 eye=" + eye + " look=" + look,
            };
            if (_context == null) _context = StreamingScene.Build(Tile.Tile, StreamingScene.Settings(2, 10));
            const float r = 90f;
            var files = new List<string>();
            float x0 = f.Cx - r, z0 = f.Cz - r, x1 = f.Cx + r, z1 = f.Cz + r;
            void Put(string layer, MeshData m)
            {
                MeshData cropped = ObjDump.Crop(m, x0, z0, x1, z1, true);
                if (cropped.TriangleCount == 0) return;
                ObjDump.Write(cropped, dir + "/" + layer + ".obj", directives);
                files.Add(layer + ".obj");
            }
            Put("terrain", StreamingScene.Layer(_context, TileLayers.Terrain));
            Put("roads", StreamingScene.Layer(_context, TileLayers.Roads));
            Put("decals", StreamingScene.Layer(_context, TileLayers.RoadDecals));
            Put("areas", StreamingScene.Layer(_context, TileLayers.Areas));
            Put("houses", Dithered(f.Capsule, 0.06f, f.Cx, f.Cz, 60f));
            var body = new MeshData();
            HumanoidMesher.Build(CharacterRecipe.NewPlayer(1u), 0, body, null);
            var player = new MeshData();
            player.HasUv0 = body.HasUv0;
            float yaw = (float)(Math.Atan2(f.Fx - f.Cx, f.Fz - f.Cz) * 180.0 / Math.PI);
            FloraObj.Append(player, body, f.Fx, f.Fy, f.Fz, yaw);
            Put("explorer", player);
            string full = Path.Combine(ObjDump.Dir, dir);
            string all = string.Join(" ", files.ConvertAll(x => "$D/" + x));
            var sh = new StringBuilder();
            sh.Append("#!/bin/sh\nD=").Append(full).Append('\n');
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(all).Append(" -o $D/view.png --size 960 --height 600 --fov 38 --fog 300\n");
            File.WriteAllText(Path.Combine(full, "render.sh"), sh.ToString());
        }

        private static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>The drawn houses with the capsule's screen door emulated in the surface: triangles within reach of the
        /// capsule are cut into cells of about <paramref name="cell"/> m and a cell is dropped where its opacity is below
        /// its 4 × 4 Bayer threshold (cells laid out in the triangle's plane).</summary>
        public MeshData Dithered(in OccluderCapsule c, float cell, float cx0, float cz0, float within)
        {
            var m = new MeshData(Houses.VertexCount + 1024, Houses.IndexCount + 3072);
            m.HasUv0 = Houses.HasUv0;
            float reach = Math.Max(c.CameraRadius, c.PlayerRadius) + c.Soft + 0.2f;
            bool any = c.Strength > 0.001f;
            var pieces = new List<float>();
            for (int t = 0; t < Houses.IndexCount / 3; t++)
            {
                float bx, by, bz, br;
                _grid.Sphere(t, out bx, out by, out bz, out br);
                if (Math.Abs(bx - cx0) > within || Math.Abs(bz - cz0) > within) continue;
                bool near = any && SegmentDistance(bx, by, bz, c.Ax, c.Ay, c.Az, c.Bx, c.By, c.Bz) <= br + reach;
                int a = Houses.Indices[3 * t], b = Houses.Indices[3 * t + 1], d = Houses.Indices[3 * t + 2];
                if (!near)
                {
                    CopyTri(m, a, b, d, null);
                    continue;
                }
                float ax, ay, az, b2x, b2y, b2z, cx, cy, cz;
                _grid.Corners(t, out ax, out ay, out az, out b2x, out b2y, out b2z, out cx, out cy, out cz);
                float nx, ny, nz;
                _grid.Normal(t, out nx, out ny, out nz);
                float t1x = b2x - ax, t1y = b2y - ay, t1z = b2z - az;
                float tl = (float)Math.Sqrt(t1x * t1x + t1y * t1y + t1z * t1z);
                if (tl < 1e-6f) continue;
                t1x /= tl;
                t1y /= tl;
                t1z /= tl;
                float t2x = ny * t1z - nz * t1y, t2y = nz * t1x - nx * t1z, t2z = nx * t1y - ny * t1x;
                pieces.Clear();
                SplitInto(pieces, ax, ay, az, b2x, b2y, b2z, cx, cy, cz, cell, 0, c, reach);
                for (int k = 0; k < pieces.Count; k += 9)
                {
                    float px = (pieces[k] + pieces[k + 3] + pieces[k + 6]) / 3f, py = (pieces[k + 1] + pieces[k + 4] + pieces[k + 7]) / 3f,
                          pz = (pieces[k + 2] + pieces[k + 5] + pieces[k + 8]) / 3f;
                    float op = c.Opacity(px, py, pz, ny, false);
                    int i = (int)Math.Floor((px * t1x + py * t1y + pz * t1z) / cell) & 3, j = (int)Math.Floor((px * t2x + py * t2y + pz * t2z) / cell) & 3;
                    if (op < (Bayer[i + 4 * j] + 0.5f) / 16f) continue;
                    CopyTri(m, a, a, a, pieces, k);
                }
            }
            return m;
        }

        private void CopyTri(MeshData m, int a, int b, int c, List<float> pos, int k = 0)
        {
            m.Reserve(3, 3);
            int v0 = m.VertexCount;
            int[] src = { a, b, c };
            for (int q = 0; q < 3; q++)
            {
                int v = src[q];
                int o = v0 + q;
                if (pos == null)
                {
                    m.Positions[3 * o] = Houses.Positions[3 * v];
                    m.Positions[3 * o + 1] = Houses.Positions[3 * v + 1];
                    m.Positions[3 * o + 2] = Houses.Positions[3 * v + 2];
                }
                else
                {
                    m.Positions[3 * o] = pos[k + 3 * q];
                    m.Positions[3 * o + 1] = pos[k + 3 * q + 1];
                    m.Positions[3 * o + 2] = pos[k + 3 * q + 2];
                }
                m.Normals[3 * o] = Houses.Normals[3 * v];
                m.Normals[3 * o + 1] = Houses.Normals[3 * v + 1];
                m.Normals[3 * o + 2] = Houses.Normals[3 * v + 2];
                for (int ch = 0; ch < 4; ch++) m.Colors[4 * o + ch] = Houses.Colors[4 * v + ch];
                if (m.HasUv0)
                {
                    m.Uv0[2 * o] = Houses.Uv0[2 * v];
                    m.Uv0[2 * o + 1] = Houses.Uv0[2 * v + 1];
                }
                m.Indices[m.IndexCount + q] = o;
            }
            m.VertexCount += 3;
            m.IndexCount += 3;
        }

        private static void SplitInto(List<float> dst, float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, float cell,
                                      int depth, in OccluderCapsule cap, float reach)
        {
            float ab = D2(ax, ay, az, bx, by, bz), bc = D2(bx, by, bz, cx, cy, cz), ca = D2(cx, cy, cz, ax, ay, az);
            float m = Math.Max(ab, Math.Max(bc, ca));
            // Pieces wholly out of the capsule's reach stay whole (only the dithered tube is cut fine).
            float sx = (ax + bx + cx) / 3f, sy = (ay + by + cy) / 3f, sz = (az + bz + cz) / 3f;
            float sr = (float)Math.Sqrt(Math.Max(D2(sx, sy, sz, ax, ay, az), Math.Max(D2(sx, sy, sz, bx, by, bz), D2(sx, sy, sz, cx, cy, cz))));
            bool outside = SegmentDistance(sx, sy, sz, cap.Ax, cap.Ay, cap.Az, cap.Bx, cap.By, cap.Bz) > sr + reach;
            if (outside || m <= cell * cell || depth > 22)
            {
                dst.Add(ax);
                dst.Add(ay);
                dst.Add(az);
                dst.Add(bx);
                dst.Add(by);
                dst.Add(bz);
                dst.Add(cx);
                dst.Add(cy);
                dst.Add(cz);
                return;
            }
            if (m == ab)
            {
                float mx = 0.5f * (ax + bx), my = 0.5f * (ay + by), mz = 0.5f * (az + bz);
                SplitInto(dst, ax, ay, az, mx, my, mz, cx, cy, cz, cell, depth + 1, cap, reach);
                SplitInto(dst, mx, my, mz, bx, by, bz, cx, cy, cz, cell, depth + 1, cap, reach);
            }
            else if (m == bc)
            {
                float mx = 0.5f * (bx + cx), my = 0.5f * (by + cy), mz = 0.5f * (bz + cz);
                SplitInto(dst, ax, ay, az, bx, by, bz, mx, my, mz, cell, depth + 1, cap, reach);
                SplitInto(dst, ax, ay, az, mx, my, mz, cx, cy, cz, cell, depth + 1, cap, reach);
            }
            else
            {
                float mx = 0.5f * (cx + ax), my = 0.5f * (cy + ay), mz = 0.5f * (cz + az);
                SplitInto(dst, ax, ay, az, bx, by, bz, mx, my, mz, cell, depth + 1, cap, reach);
                SplitInto(dst, mx, my, mz, bx, by, bz, cx, cy, cz, cell, depth + 1, cap, reach);
            }
        }

        /// <summary>The camera's near plane (corners included) reaches into a house.</summary>
        private bool NearCut(in CameraFrame f)
        {
            float reach = NearClipM * (float)Math.Sqrt(1.0 + TanHalfX * TanHalfX + TanHalfY * TanHalfY);
            return _grid.AnyWithin(f.Cx, f.Cy, f.Cz, reach);
        }

        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A plain chase camera as the game's boom works: behind the explorer along the smoothed heading, the
        /// rig's pitch and length from the pivot, pulled in at once to stay 0.1 m short of the first solid a 0.3 m sphere
        /// meets (the world's view obstacle query), easing back out over 0.6 s after a 0.2 s hold.</summary>
        internal sealed class ChaseCam
        {
            private readonly float _boom, _pitch, _pivot;
            private float _hx, _hz, _dist = -1f, _hold;
            public float X, Y, Z;

            public ChaseCam(float boom, float pitchDeg, float pivot)
            {
                _boom = boom;
                _pitch = pitchDeg * (float)Math.PI / 180f;
                _pivot = pivot;
            }

            public void Update(IViewObstacleQuery q, double ox, double oz, float fx, float fy, float fz, float hx, float hz, float dt, bool snap)
            {
                float hl = (float)Math.Sqrt(hx * hx + hz * hz);
                if (hl > 1e-4f)
                {
                    hx /= hl;
                    hz /= hl;
                }
                else
                {
                    hx = _hx;
                    hz = _hz;
                }
                if (snap || _dist < 0f)
                {
                    _hx = hx;
                    _hz = hz;
                }
                else
                {
                    float k = 1f - (float)Math.Exp(-dt / 0.3f);
                    _hx += (hx - _hx) * k;
                    _hz += (hz - _hz) * k;
                    float l = (float)Math.Sqrt(_hx * _hx + _hz * _hz);
                    _hx /= l;
                    _hz /= l;
                }
                float py = fy + _pivot;
                double dx = -_hx * Math.Cos(_pitch), dy = Math.Sin(_pitch), dz = -_hz * Math.Cos(_pitch);
                double hit;
                float clear = _boom;
                if (q.SphereCast(ox + fx, py, oz + fz, dx, dy, dz, 0.3, _boom, out hit)) clear = (float)Math.Max(0.0, hit - 0.1);
                if (snap || _dist < 0f)
                {
                    _dist = clear;
                    _hold = 0f;
                }
                else if (clear < _dist)
                {
                    _dist = clear;
                    _hold = 0.2f;
                }
                else if (_hold > 0f)
                {
                    _hold -= dt;
                }
                else
                {
                    _dist += (clear - _dist) * (1f - (float)Math.Exp(-dt * 3f / 0.6f));
                }
                _dist = Math.Min(_dist, clear);
                X = fx + (float)(dx * _dist);
                Y = py + (float)(dy * _dist);
                Z = fz + (float)(dz * _dist);
            }
        }
    }

    /// <summary>Triangles of a mesh in a uniform xz grid: candidates near a segment, segment casts, proximity.</summary>
    internal sealed class TriGrid
    {
        private readonly MeshData _m;
        private readonly float _cell, _x0, _z0;
        private readonly int _nx, _nz;
        private readonly List<int>[] _cells;
        private readonly int[] _stamp;
        private int _query;
        private readonly List<int> _out = new List<int>();

        public TriGrid(MeshData m, float cell)
        {
            _m = m;
            _cell = cell;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int v = 0; v < m.VertexCount; v++)
            {
                minX = Math.Min(minX, m.Positions[3 * v]);
                maxX = Math.Max(maxX, m.Positions[3 * v]);
                minZ = Math.Min(minZ, m.Positions[3 * v + 2]);
                maxZ = Math.Max(maxZ, m.Positions[3 * v + 2]);
            }
            if (m.VertexCount == 0)
            {
                minX = minZ = 0f;
                maxX = maxZ = 1f;
            }
            _x0 = minX - cell;
            _z0 = minZ - cell;
            _nx = Math.Max(1, (int)((maxX - _x0) / cell) + 2);
            _nz = Math.Max(1, (int)((maxZ - _z0) / cell) + 2);
            _cells = new List<int>[_nx * _nz];
            int tris = m.IndexCount / 3;
            _stamp = new int[tris];
            for (int t = 0; t < tris; t++)
            {
                float a0, a1, b0, b1;
                Bounds(t, out a0, out b0, out a1, out b1);
                int i0 = Cx(a0), i1 = Cx(a1), j0 = Cz(b0), j1 = Cz(b1);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _nx + i;
                    if (_cells[c] == null) _cells[c] = new List<int>(8);
                    _cells[c].Add(t);
                }
            }
        }

        private int Cx(float x)
        {
            return Math.Max(0, Math.Min(_nx - 1, (int)Math.Floor((x - _x0) / _cell)));
        }

        private int Cz(float z)
        {
            return Math.Max(0, Math.Min(_nz - 1, (int)Math.Floor((z - _z0) / _cell)));
        }

        private void Bounds(int t, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = minZ = float.MaxValue;
            maxX = maxZ = float.MinValue;
            for (int k = 0; k < 3; k++)
            {
                int v = _m.Indices[3 * t + k];
                minX = Math.Min(minX, _m.Positions[3 * v]);
                maxX = Math.Max(maxX, _m.Positions[3 * v]);
                minZ = Math.Min(minZ, _m.Positions[3 * v + 2]);
                maxZ = Math.Max(maxZ, _m.Positions[3 * v + 2]);
            }
        }

        public void Corners(int t, out float ax, out float ay, out float az, out float bx, out float by, out float bz, out float cx, out float cy, out float cz)
        {
            int a = _m.Indices[3 * t], b = _m.Indices[3 * t + 1], c = _m.Indices[3 * t + 2];
            ax = _m.Positions[3 * a];
            ay = _m.Positions[3 * a + 1];
            az = _m.Positions[3 * a + 2];
            bx = _m.Positions[3 * b];
            by = _m.Positions[3 * b + 1];
            bz = _m.Positions[3 * b + 2];
            cx = _m.Positions[3 * c];
            cy = _m.Positions[3 * c + 1];
            cz = _m.Positions[3 * c + 2];
        }

        /// <summary>The face normal from the triangle's winding (Unity's convention: front = cross(b - a, c - a) seen as
        /// clockwise from the front), oriented to agree with the stored vertex normals.</summary>
        public void Normal(int t, out float nx, out float ny, out float nz)
        {
            float ax, ay, az, bx, by, bz, cx, cy, cz;
            Corners(t, out ax, out ay, out az, out bx, out by, out bz, out cx, out cy, out cz);
            float ex1 = bx - ax, ey1 = by - ay, ez1 = bz - az, ex2 = cx - ax, ey2 = cy - ay, ez2 = cz - az;
            nx = ey1 * ez2 - ez1 * ey2;
            ny = ez1 * ex2 - ex1 * ez2;
            nz = ex1 * ey2 - ey1 * ex2;
            float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-12f)
            {
                nx = ny = nz = 0f;
                return;
            }
            nx /= l;
            ny /= l;
            nz /= l;
            int a = _m.Indices[3 * t];
            float sx = _m.Normals[3 * a] + _m.Normals[3 * _m.Indices[3 * t + 1]] + _m.Normals[3 * _m.Indices[3 * t + 2]];
            float sy = _m.Normals[3 * a + 1] + _m.Normals[3 * _m.Indices[3 * t + 1] + 1] + _m.Normals[3 * _m.Indices[3 * t + 2] + 1];
            float sz = _m.Normals[3 * a + 2] + _m.Normals[3 * _m.Indices[3 * t + 1] + 2] + _m.Normals[3 * _m.Indices[3 * t + 2] + 2];
            if (nx * sx + ny * sy + nz * sz < 0f)
            {
                nx = -nx;
                ny = -ny;
                nz = -nz;
            }
        }

        public void Sphere(int t, out float x, out float y, out float z, out float r)
        {
            float ax, ay, az, bx, by, bz, cx, cy, cz;
            Corners(t, out ax, out ay, out az, out bx, out by, out bz, out cx, out cy, out cz);
            x = (ax + bx + cx) / 3f;
            y = (ay + by + cy) / 3f;
            z = (az + bz + cz) / 3f;
            float r2 = Math.Max((ax - x) * (ax - x) + (ay - y) * (ay - y) + (az - z) * (az - z),
                                Math.Max((bx - x) * (bx - x) + (by - y) * (by - y) + (bz - z) * (bz - z),
                                         (cx - x) * (cx - x) + (cy - y) * (cy - y) + (cz - z) * (cz - z)));
            r = (float)Math.Sqrt(r2);
        }

        /// <summary>Triangles in the cells of the segment's xz box grown by <paramref name="reach"/> (each once).</summary>
        public List<int> Near(float ax, float az, float bx, float bz, float reach)
        {
            _out.Clear();
            _query++;
            int i0 = Cx(Math.Min(ax, bx) - reach), i1 = Cx(Math.Max(ax, bx) + reach);
            int j0 = Cz(Math.Min(az, bz) - reach), j1 = Cz(Math.Max(az, bz) + reach);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                List<int> c = _cells[j * _nx + i];
                if (c == null) continue;
                for (int k = 0; k < c.Count; k++)
                {
                    int t = c[k];
                    if (_stamp[t] == _query) continue;
                    _stamp[t] = _query;
                    _out.Add(t);
                }
            }
            return _out;
        }

        /// <summary>First triangle hit on the segment from (a) to (b), ignoring hits within <paramref name="endMargin"/>
        /// of b (double-sided).</summary>
        public bool Raycast(float ax, float ay, float az, float bx, float by, float bz, float endMargin, out float hitDist)
        {
            float dx = bx - ax, dy = by - ay, dz = bz - az;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            hitDist = len;
            if (len < 1e-6f) return false;
            dx /= len;
            dy /= len;
            dz /= len;
            float max = len - endMargin;
            if (max <= 0f) return false;
            float t;
            if (FirstHit(ax, ay, az, dx, dy, dz, max, out t) < 0) return false;
            hitDist = t;
            return true;
        }

        /// <summary>The first triangle a ray from (o) along the unit direction (d) hits within <paramref name="max"/>
        /// (either side), or -1: walks the grid cells the ray crosses in order (Amanatides-Woo) and stops at the first
        /// cell whose far side lies beyond the nearest hit. Of two hits within 0.5 mm (a two-sided panel: two quads in one
        /// plane facing opposite ways) the one facing the ray wins, as the rasterizer would show it.</summary>
        public int FirstHit(float ox, float oy, float oz, float dx, float dy, float dz, float max, out float hitDist)
        {
            hitDist = max;
            int best = -1;
            bool bestFront = false;
            float tMin = 0f, tMax = max;
            if (!Slab(ox, dx, _x0, _x0 + _nx * _cell, ref tMin, ref tMax) || !Slab(oz, dz, _z0, _z0 + _nz * _cell, ref tMin, ref tMax)) return -1;
            _query++;
            int i = Cx(ox + dx * tMin), j = Cz(oz + dz * tMin);
            int si = dx > 0f ? 1 : -1, sj = dz > 0f ? 1 : -1;
            float stepX = Math.Abs(dx) > 1e-9f ? _cell / Math.Abs(dx) : float.PositiveInfinity;
            float stepZ = Math.Abs(dz) > 1e-9f ? _cell / Math.Abs(dz) : float.PositiveInfinity;
            float nextX = dx > 1e-9f ? (_x0 + (i + 1) * _cell - ox) / dx : dx < -1e-9f ? (_x0 + i * _cell - ox) / dx : float.PositiveInfinity;
            float nextZ = dz > 1e-9f ? (_z0 + (j + 1) * _cell - oz) / dz : dz < -1e-9f ? (_z0 + j * _cell - oz) / dz : float.PositiveInfinity;
            while (true)
            {
                List<int> c = _cells[j * _nx + i];
                if (c != null)
                    for (int k = 0; k < c.Count; k++)
                    {
                        int tri = c[k];
                        if (_stamp[tri] == _query) continue;
                        _stamp[tri] = _query;
                        float t;
                        if (!RayTri(tri, ox, oy, oz, dx, dy, dz, out t) || t < 0f || t >= hitDist + 0.0005f) continue;
                        float tnx, tny, tnz;
                        Normal(tri, out tnx, out tny, out tnz);
                        bool front = tnx * dx + tny * dy + tnz * dz < 0f;
                        if (t < hitDist - 0.0005f || (front && !bestFront))
                        {
                            hitDist = t;
                            best = tri;
                            bestFront = front;
                        }
                    }
                float exit = Math.Min(Math.Min(nextX, nextZ), tMax);
                if (hitDist <= exit || exit >= tMax) break;
                if (nextX < nextZ)
                {
                    i += si;
                    nextX += stepX;
                    if (i < 0 || i >= _nx) break;
                }
                else
                {
                    j += sj;
                    nextZ += stepZ;
                    if (j < 0 || j >= _nz) break;
                }
            }
            return best;
        }

        /// <summary>Clips the ray parameter range to one axis slab of the grid.</summary>
        private static bool Slab(float o, float d, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (Math.Abs(d) < 1e-9f) return o >= lo && o <= hi;
            float a = (lo - o) / d, b = (hi - o) / d;
            if (a > b)
            {
                float s = a;
                a = b;
                b = s;
            }
            tMin = Math.Max(tMin, a);
            tMax = Math.Min(tMax, b);
            return tMin <= tMax;
        }

        /// <summary>Any triangle within <paramref name="r"/> of a point.</summary>
        public bool AnyWithin(float px, float py, float pz, float r)
        {
            List<int> cands = Near(px, pz, px, pz, r);
            for (int k = 0; k < cands.Count; k++)
            {
                float ax, ay, az, bx, by, bz, cx, cy, cz;
                Corners(cands[k], out ax, out ay, out az, out bx, out by, out bz, out cx, out cy, out cz);
                if (PointTriDistance(px, py, pz, ax, ay, az, bx, by, bz, cx, cy, cz) < r) return true;
            }
            return false;
        }

        private bool RayTri(int t, float ox, float oy, float oz, float dx, float dy, float dz, out float dist)
        {
            float ax, ay, az, bx, by, bz, cx, cy, cz;
            Corners(t, out ax, out ay, out az, out bx, out by, out bz, out cx, out cy, out cz);
            dist = 0f;
            float e1x = bx - ax, e1y = by - ay, e1z = bz - az, e2x = cx - ax, e2y = cy - ay, e2z = cz - az;
            float px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
            float det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-12f) return false;
            float inv = 1f / det;
            float tx = ox - ax, ty = oy - ay, tz = oz - az;
            float u = (tx * px + ty * py + tz * pz) * inv;
            if (u < 0f || u > 1f) return false;
            float qx = ty * e1z - tz * e1y, qy = tz * e1x - tx * e1z, qz = tx * e1y - ty * e1x;
            float v = (dx * qx + dy * qy + dz * qz) * inv;
            if (v < 0f || u + v > 1f) return false;
            dist = (e2x * qx + e2y * qy + e2z * qz) * inv;
            return true;
        }

        /// <summary>Distance from a point to a triangle (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        public static float PointTriDistance(float px, float py, float pz, float ax, float ay, float az, float bx, float by, float bz, float cx,
                                             float cy, float cz)
        {
            float abx = bx - ax, aby = by - ay, abz = bz - az, acx = cx - ax, acy = cy - ay, acz = cz - az;
            float apx = px - ax, apy = py - ay, apz = pz - az;
            float d1 = abx * apx + aby * apy + abz * apz, d2 = acx * apx + acy * apy + acz * apz;
            float qx, qy, qz;
            if (d1 <= 0f && d2 <= 0f)
            {
                qx = ax;
                qy = ay;
                qz = az;
            }
            else
            {
                float bpx = px - bx, bpy = py - by, bpz = pz - bz;
                float d3 = abx * bpx + aby * bpy + abz * bpz, d4 = acx * bpx + acy * bpy + acz * bpz;
                float cpx = px - cx, cpy = py - cy, cpz = pz - cz;
                float d5 = abx * cpx + aby * cpy + abz * cpz, d6 = acx * cpx + acy * cpy + acz * cpz;
                float vc = d1 * d4 - d3 * d2, vb = d5 * d2 - d1 * d6, va = d3 * d6 - d5 * d4;
                if (d3 >= 0f && d4 <= d3)
                {
                    qx = bx;
                    qy = by;
                    qz = bz;
                }
                else if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                {
                    float v = d1 / (d1 - d3);
                    qx = ax + abx * v;
                    qy = ay + aby * v;
                    qz = az + abz * v;
                }
                else if (d6 >= 0f && d5 <= d6)
                {
                    qx = cx;
                    qy = cy;
                    qz = cz;
                }
                else if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                {
                    float w = d2 / (d2 - d6);
                    qx = ax + acx * w;
                    qy = ay + acy * w;
                    qz = az + acz * w;
                }
                else if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
                {
                    float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                    qx = bx + (cx - bx) * w;
                    qy = by + (cy - by) * w;
                    qz = bz + (cz - bz) * w;
                }
                else
                {
                    float denom = 1f / (va + vb + vc);
                    float v = vb * denom, w = vc * denom;
                    qx = ax + abx * v + acx * w;
                    qy = ay + aby * v + acy * w;
                    qz = az + abz * v + acz * w;
                }
            }
            float ddx = px - qx, ddy = py - qy, ddz = pz - qz;
            return (float)Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
        }
    }
}
