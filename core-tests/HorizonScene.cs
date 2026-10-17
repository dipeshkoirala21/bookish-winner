using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Horizon;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Rendering;
using Ghumante.World.Sky;

namespace Ghumante.Core.Tests
{
    /// <summary>A real place the skyline is checked from (WGS84), the eye <see cref="EyeAboveGroundM"/> above the drawn
    /// ground.</summary>
    internal readonly struct HorizonViewpoint
    {
        public readonly string Name;
        public readonly double Lat, Lon;
        public readonly float EyeAboveGroundM;

        public HorizonViewpoint(string name, double lat, double lon, float eyeAboveGroundM = 2f)
        {
            Name = name;
            Lat = lat;
            Lon = lon;
            EyeAboveGroundM = eyeAboveGroundM;
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>Basantapur (Kathmandu Durbar Square), the open brick square south of Hanuman Dhoka.</summary>
        public static readonly HorizonViewpoint Basantapur = new HorizonViewpoint("Basantapur", 27.7041, 85.3075);

        /// <summary>Boudhanath, on the kora at the foot of the stupa.</summary>
        public static readonly HorizonViewpoint Boudha = new HorizonViewpoint("Boudha", 27.7215, 85.3620);

        /// <summary>Swayambhunath, the stupa platform on the hilltop.</summary>
        public static readonly HorizonViewpoint Swayambhu = new HorizonViewpoint("Swayambhu", 27.7149, 85.2903);

        public static readonly HorizonViewpoint[] All = { Basantapur, Boudha, Swayambhu };
    }

    /// <summary>The skyline seen along one bearing: the highest elevation angle anything drawn reaches (curvature
    /// included), how far away that is, its altitude and how much of its colour survives the haze.</summary>
    internal struct HorizonRay
    {
        public double ElevationDeg;
        public double DistanceM;
        public double AltitudeM;
        public double Transmittance;
        public bool Hit;

        /// <summary>Drawn by the skyline ring (else by the streamed terrain).</summary>
        public bool FromRing;
    }

    /// <summary>A point of the ring's curtain as the skyline shader shades it: horizontal distance, altitude, the quad's
    /// flat normal, the rib coordinate, rock exposure (0..1) and band.</summary>
    internal struct RingPoint
    {
        public double DistanceM, AltitudeM, NormalX, NormalY, NormalZ, AlongM, Rock;
        public int Band;
    }

    /// <summary>
    /// What a player standing at a <see cref="HorizonViewpoint"/> sees on the horizon, rebuilt headlessly from the
    /// committed kathmandu_core sample: the streamed terrain exactly as the game selects and decimates it
    /// (<see cref="TileSelector"/> with the tier's <see cref="StreamingConfig"/>, <see cref="StreamingConfig.TerrainStep"/>,
    /// the rendered triangle surface through <see cref="TileHeightSampler.ForArea"/>), lowered by the shaders' earth
    /// curvature, cut by the camera's far clip (<c>WorldRoot.ConfigureCamera</c>) and faded by the valley haze
    /// <c>WorldSky</c> applies (exp² fog, at least the tier's floor).
    /// </summary>
    internal static class HorizonFields
    {
        private static readonly Dictionary<double, HorizonHeightField> Cache = new Dictionary<double, HorizonHeightField>();

        /// <summary>The sample pack's horizon field at a grid spacing (built once).</summary>
        public static HorizonHeightField For(double spacingM)
        {
            lock (Cache)
            {
                HorizonHeightField f;
                if (!Cache.TryGetValue(spacingM, out f))
                {
                    f = HorizonHeightField.FromPack(StreamingSampleRegion.Pack, spacingM);
                    Cache[spacingM] = f;
                }
                return f;
            }
        }
    }

    internal sealed class HorizonScene
    {
        private readonly int _tier;
        private readonly StreamingConfig _config;
        private readonly Dictionary<ulong, TileHeightSampler> _samplers = new Dictionary<ulong, TileHeightSampler>();
        private readonly Dictionary<ulong, SelectedNode> _nodes = new Dictionary<ulong, SelectedNode>();
        private readonly List<int> _levels = new List<int>();
        private static readonly Dictionary<ulong, TileData> Decoded = new Dictionary<ulong, TileData>();

        public readonly HorizonViewpoint View;
        public readonly double X, Z, GroundM, EyeM;
        public readonly double FarClipM, FogDensity, AirDensity;

        /// <summary>Local solar time of the scene (light and haze).</summary>
        public readonly double Hours;

        /// <summary>The scene of <paramref name="tier"/> around <paramref name="view"/> at <paramref name="hours"/>
        /// local solar time (the haze follows the time of day).</summary>
        public HorizonScene(HorizonViewpoint view, int tier, double hours = 9.0, bool withSkyline = true, double buildOffsetM = 0.0,
                            double buildOffsetBearingDeg = 0.0)
        {
            View = view;
            Hours = hours;
            _tier = tier;
            _config = StreamingConfig.ForTier(tier);
            double x, z;
            WorldFrame.LonLatToGame(view.Lon, view.Lat, out x, out z);
            X = x;
            Z = z;
            var selector = TileSelector.ForPack(_config, StreamingSampleRegion.Pack);
            var selected = new List<SelectedNode>();
            selector.Select(x, z, selected);
            foreach (SelectedNode n in selected)
            {
                _nodes[n.Area.Key] = n;
                if (!_levels.Contains(n.Area.Level)) _levels.Add(n.Area.Level);
            }
            _levels.Sort((a, b) => b.CompareTo(a));
            float g;
            if (!TryHeight(x, z, out g)) throw new InvalidOperationException("no terrain under " + view.Name);
            GroundM = g;
            EyeM = g + view.EyeAboveGroundM;
            FarClipM = _config.ViewRadiusM * 1.25 + 5000.0; // WorldRoot.ConfigureCamera
            // WorldSky.Apply: the palette's haze, at least the valley haze and the floor that hides the end of the last
            // ring (SkyPalette.SceneFogDensity); the skyline's air is the palette's haze alone.
            AirDensity = SkyPalette.Evaluate(hours).FogDensity;
            FogDensity = SkyPalette.SceneFogDensity((float)AirDensity, _config.ViewRadiusM);
            if (!withSkyline) return;
            // The skyline ring as the world builds it (SkylineRing): the tier's settings, the pack's horizon field, built
            // for an eye that may lie up to the rebuild distance away from where the camera is now.
            Settings = HorizonSettings.ForTier(tier);
            Field = HorizonFields.For(Settings.GridSpacingM);
            double ob = buildOffsetBearingDeg * Math.PI / 180.0;
            double bx = x + Math.Sin(ob) * buildOffsetM, bz = z + Math.Cos(ob) * buildOffsetM;
            float bg;
            if (!TryHeight(bx, bz, out bg)) bg = g;
            Panorama = new HorizonPanorama();
            Panorama.Build(Field, bx, bz, bg + view.EyeAboveGroundM, Settings);
            Ring = new MeshData();
            new HorizonMesher().Build(Panorama, Settings, Ring);
            CollectRingEdges();
        }

        /// <summary>The skyline ring of the scene (null without it), its settings, field and panorama.</summary>
        public readonly MeshData Ring;

        public readonly HorizonSettings Settings;
        public readonly HorizonHeightField Field;
        public readonly HorizonPanorama Panorama;

        /// <summary>One quad of the ring seen from the scene's eye: its top edge (bearing, elevation, distance and altitude
        /// at both ends), its vertices (top A, top B, bottom B, bottom A) and its band.</summary>
        private sealed class RingQuad
        {
            public double B0, E0, D0, Y0, B1, E1, D1, Y1;
            public int TopA, TopB, BotB, BotA, Band;

            /// <summary>Where <paramref name="bearingDeg"/> falls along the top edge (0..1), or -1 when outside.</summary>
            public double At(double bearingDeg)
            {
                double span = B1 - B0;
                if (span > 180) span -= 360;
                if (span < -180) span += 360;
                double off = bearingDeg - B0;
                if (off > 180) off -= 360;
                if (off < -180) off += 360;
                if (Math.Abs(span) < 1e-9 || off * span < 0 || Math.Abs(off) > Math.Abs(span)) return -1.0;
                return off / span;
            }
        }

        private readonly List<RingQuad> _quads = new List<RingQuad>();

        /// <summary>The ring's quads seen from this scene's eye.</summary>
        private void CollectRingEdges()
        {
            double ox = Panorama.EyeX, oz = Panorama.EyeZ;
            for (int t = 0; t + 5 < Ring.IndexCount; t += 6)
            {
                // Triangle pairs (topA, topB, botB), (topA, botB, botA): the top edge is topA -> topB.
                var q = new RingQuad
                {
                    TopA = Ring.Indices[t], TopB = Ring.Indices[t + 1], BotB = Ring.Indices[t + 2], BotA = Ring.Indices[t + 5],
                };
                q.Band = Ring.Colors[q.TopA * 4 + 1];
                q.Y0 = Ring.Positions[q.TopA * 3 + 1];
                q.Y1 = Ring.Positions[q.TopB * 3 + 1];
                Sight(ox + Ring.Positions[q.TopA * 3], oz + Ring.Positions[q.TopA * 3 + 2], q.Y0, out q.B0, out q.D0, out q.E0);
                Sight(ox + Ring.Positions[q.TopB * 3], oz + Ring.Positions[q.TopB * 3 + 2], q.Y1, out q.B1, out q.D1, out q.E1);
                _quads.Add(q);
            }
        }

        /// <summary>The skyline ring's top along <paramref name="bearingDeg"/> seen from the eye (the highest of the
        /// bands), haze from <see cref="HorizonLook"/>.</summary>
        public HorizonRay RingRay(double bearingDeg)
        {
            var best = new HorizonRay { ElevationDeg = -90.0 };
            if (Ring == null) return best;
            foreach (RingQuad q in _quads)
            {
                double t = q.At(bearingDeg);
                if (t < 0) continue;
                double el = q.E0 + (q.E1 - q.E0) * t;
                if (el <= best.ElevationDeg) continue;
                best.ElevationDeg = el;
                best.DistanceM = q.D0 + (q.D1 - q.D0) * t;
                best.AltitudeM = q.Y0 + (q.Y1 - q.Y0) * t;
                best.Hit = true;
                best.FromRing = true;
            }
            if (best.Hit) best.Transmittance = HorizonLook.Transmittance(best.DistanceM, EyeM, best.AltitudeM, FogDensity, AirDensity);
            return best;
        }

        /// <summary>What the skyline shader draws at (<paramref name="bearingDeg"/>, <paramref name="elevationDeg"/>):
        /// the nearest band whose curtain reaches that high there (nearer bands are drawn in front). False when no
        /// curtain covers it.</summary>
        public bool RingSurface(double bearingDeg, double elevationDeg, out RingPoint point)
        {
            point = default(RingPoint);
            RingQuad hit = null;
            double ht = 0;
            foreach (RingQuad q in _quads)
            {
                double t = q.At(bearingDeg);
                if (t < 0) continue;
                double top = q.E0 + (q.E1 - q.E0) * t;
                if (elevationDeg > top + 1e-9) continue;
                if (hit != null && hit.Band <= q.Band) continue;
                hit = q;
                ht = t;
            }
            if (hit == null) return false;
            double d = hit.D0 + (hit.D1 - hit.D0) * ht;
            point.DistanceM = d;
            point.AltitudeM = EyeM + d * Math.Tan(elevationDeg * Math.PI / 180.0) + d * d * EarthCurvature.InverseTwoEffectiveRadius;
            point.NormalX = Ring.Normals[hit.TopA * 3];
            point.NormalY = Ring.Normals[hit.TopA * 3 + 1];
            point.NormalZ = Ring.Normals[hit.TopA * 3 + 2];
            point.AlongM = Ring.Uv0[hit.TopA * 2] + (Ring.Uv0[hit.TopB * 2] - Ring.Uv0[hit.TopA * 2]) * ht;
            point.Rock = (Ring.Colors[hit.TopA * 4] + (Ring.Colors[hit.TopB * 4] - Ring.Colors[hit.TopA * 4]) * ht) / 255.0;
            point.Band = hit.Band;
            return true;
        }

        /// <summary>The scene's fog density at another local solar time (WorldSky: SkyPalette.SceneFogDensity).</summary>
        public double FogDensityAt(double hours)
        {
            return SkyPalette.SceneFogDensity(SkyPalette.Evaluate(hours).FogDensity, _config.ViewRadiusM);
        }

        public int Tier
        {
            get { return _tier; }
        }

        public StreamingConfig Config
        {
            get { return _config; }
        }

        /// <summary>The selected nodes (areas drawn).</summary>
        public IEnumerable<SelectedNode> Nodes
        {
            get { return _nodes.Values; }
        }

        /// <summary>Selected nodes (areas drawn), for counting.</summary>
        public int NodeCount
        {
            get { return _nodes.Count; }
        }

        /// <summary>Height of the drawn terrain surface at game (x, z): the node of the selection covering the point,
        /// at the node's decimation step.</summary>
        public bool TryHeight(double x, double z, out float h)
        {
            for (int k = 0; k < _levels.Count; k++)
            {
                TileId a = TileId.At(_levels[k], x, z);
                SelectedNode n;
                if (!_nodes.TryGetValue(a.Key, out n)) continue;
                TileHeightSampler s;
                if (!_samplers.TryGetValue(a.Key, out s))
                {
                    TileData src = Source(n.Source);
                    int step = _config.TerrainStep(n.Area, n.Source, src.HeightsN);
                    s = TileHeightSampler.ForArea(src, n.Area, step);
                    _samplers[a.Key] = s;
                }
                return s.TryHeight(x, z, out h);
            }
            h = 0f;
            return false;
        }

        /// <summary>The streamed terrain's skyline along <paramref name="bearingDeg"/> (clockwise from north), from
        /// <paramref name="fromM"/> out to the far clip.</summary>
        public HorizonRay TerrainRay(double bearingDeg, double fromM = 30.0, double toM = double.PositiveInfinity)
        {
            double a = bearingDeg * Math.PI / 180.0, dx = Math.Sin(a), dz = Math.Cos(a);
            var best = new HorizonRay { ElevationDeg = -90.0 };
            double k = EarthCurvature.InverseTwoEffectiveRadius;
            for (double d = fromM; d <= Math.Min(FarClipM, toM); d += Math.Max(15.0, d * 0.0015))
            {
                float h;
                if (!TryHeight(X + dx * d, Z + dz * d, out h)) continue;
                double e = Math.Atan2(h - EyeM - d * d * k, d) * 180.0 / Math.PI;
                if (e <= best.ElevationDeg) continue;
                best.ElevationDeg = e;
                best.DistanceM = d;
                best.AltitudeM = h;
                best.Hit = true;
            }
            best.Transmittance = best.Hit ? SceneTransmittance(best.DistanceM) : 0.0;
            return best;
        }

        /// <summary>The skyline checks start this far out: the square's own buildings and the DSM bumps they leave in the
        /// terrain (the GLO-30 heights include roofs) are not the horizon (the GLO-30 line-of-sight analysis starts here
        /// too).</summary>
        public const double NearM = 500.0;

        /// <summary>
        /// The skyline the game draws along <paramref name="bearingDeg"/>: the streamed terrain in front of the ring's
        /// handover distance, and above it whichever is higher of the ring and (where the ring has no curtain) the
        /// streamed terrain beyond. Without a ring, the streamed terrain alone (the game before the skyline).
        /// </summary>
        public HorizonRay DrawnRay(double bearingDeg)
        {
            if (Ring == null) return TerrainRay(bearingDeg, NearM);
            HorizonRay ring = RingRay(bearingDeg);
            if (!ring.Hit) return TerrainRay(bearingDeg, NearM);
            HorizonRay near = TerrainRay(bearingDeg, NearM, Settings.HandoverM);
            return near.Hit && near.ElevationDeg >= ring.ElevationDeg ? near : ring;
        }

        /// <summary>exp(-(ρ·d)²), the share of a surface's colour that survives the scene fog at distance d.</summary>
        public double SceneTransmittance(double distanceM)
        {
            double t = FogDensity * distanceM;
            return Math.Exp(-t * t);
        }

        /// <summary>Elevation angle (curvature included) of a point of altitude <paramref name="altitudeM"/> at game
        /// (x, z), and its bearing and distance from the eye.</summary>
        public void Sight(double x, double z, double altitudeM, out double bearingDeg, out double distanceM, out double elevationDeg)
        {
            double dx = x - X, dz = z - Z;
            distanceM = Math.Sqrt(dx * dx + dz * dz);
            bearingDeg = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (bearingDeg < 0) bearingDeg += 360.0;
            elevationDeg = Math.Atan2(altitudeM - EyeM - distanceM * distanceM * EarthCurvature.InverseTwoEffectiveRadius, distanceM) * 180.0 / Math.PI;
        }

        private static TileData Source(TileId id)
        {
            lock (Decoded)
            {
                TileData t;
                if (!Decoded.TryGetValue(id.Key, out t))
                {
                    t = StreamingSampleRegion.Tile(id);
                    Decoded[id.Key] = t;
                }
                return t;
            }
        }
    }
}
