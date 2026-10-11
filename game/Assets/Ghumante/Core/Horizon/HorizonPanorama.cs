using System;

namespace Ghumante.Core.Horizon
{
    /// <summary>
    /// The skyline seen from one eye point, band by band (engine-free; reuse one instance per worker, a rebuild
    /// allocates nothing once its arrays fit). For every ray of <see cref="HorizonSettings.AzimuthStepDeg"/> it marches
    /// a <see cref="HorizonHeightField"/> outwards and keeps, per distance band, the point that rises highest above the
    /// eye's horizon after earth curvature (<see cref="InverseTwoEffectiveRadius"/>, the shaders' R_eff = 7/6 R): the
    /// silhouette of that band, with its distance, altitude and steepness. Curated summits are sampled exactly at the
    /// point of each ray nearest to them, so a peak shows its published height, not a sample a few hundred metres off.
    /// </summary>
    public sealed class HorizonPanorama
    {
        /// <summary>1 / (2·R_eff), R_eff = 7/6 × 6 371 000 m: the earth curvature of the world shaders
        /// (<c>GH_INV_TWO_R_EFF</c>, World.Rendering.EarthCurvature.InverseTwoEffectiveRadius).</summary>
        public const double InverseTwoEffectiveRadius = 1.0 / (2.0 * 6371000.0 * 7.0 / 6.0);

        private float[] _tan = new float[0], _dist = new float[0], _alt = new float[0], _steep = new float[0], _gx = new float[0], _gz = new float[0];
        private int[] _cand = new int[32];
        private double[] _sumAz = new double[0], _sumX = new double[0], _sumZ = new double[0];
        private float[] _sumDist = new float[0], _sumAlt = new float[0];
        private int[] _sumBand = new int[0], _sumPeak = new int[0];

        /// <summary>Rays (azimuths) and bands of the last build.</summary>
        public int Azimuths { get; private set; }

        public int Bands { get; private set; }

        /// <summary>Degrees between rays; ray i points at bearing i × step (clockwise from north).</summary>
        public double AzimuthStepDeg { get; private set; }

        /// <summary>The eye the panorama was built for (game metres, absolute altitude).</summary>
        public double EyeX { get; private set; }

        public double EyeZ { get; private set; }

        public double EyeAltitudeM { get; private set; }

        /// <summary>A fixed point of the height field (its centre): the mesher measures its rib coordinate as bearings
        /// from here, so the detail along the curtains stays on the ground from one rebuild to the next.</summary>
        public double AnchorX { get; private set; }

        public double AnchorZ { get; private set; }

        /// <summary>Distance band edges of the last build (copied from the settings).</summary>
        public double[] BandEdgesM { get; private set; }

        /// <summary>Height samples taken by the last build (cost counter).</summary>
        public long Samples { get; private set; }

        /// <summary>
        /// Marches every ray from the eye at game (<paramref name="x"/>, <paramref name="z"/>), altitude
        /// <paramref name="eyeAltitudeM"/>, through <paramref name="field"/>. Thread-safe for distinct panoramas (the
        /// field is read-only).
        /// </summary>
        public void Build(HorizonHeightField field, double x, double z, double eyeAltitudeM, HorizonSettings settings)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            int n = settings.Azimuths, bands = settings.Bands;
            Azimuths = n;
            Bands = bands;
            AzimuthStepDeg = 360.0 / n;
            EyeX = x;
            EyeZ = z;
            EyeAltitudeM = eyeAltitudeM;
            AnchorX = field.X0 + field.Nx * field.SpacingM * 0.5;
            AnchorZ = field.Z0 + field.Nz * field.SpacingM * 0.5;
            if (BandEdgesM == null || BandEdgesM.Length != settings.BandEdgesM.Length) BandEdgesM = new double[settings.BandEdgesM.Length];
            Array.Copy(settings.BandEdgesM, BandEdgesM, BandEdgesM.Length);
            int total = n * bands;
            if (_tan.Length < total)
            {
                _tan = new float[total];
                _dist = new float[total];
                _alt = new float[total];
                _steep = new float[total];
                _gx = new float[total];
                _gz = new float[total];
            }
            for (int k = 0; k < total; k++) _tan[k] = float.NaN;
            if (_cand.Length < field.ConeCount) _cand = new int[field.ConeCount];

            double k2 = InverseTwoEffectiveRadius;
            double start = settings.BandEdgesM[0], max = settings.MaxDistanceM;
            double stepMin = settings.StepMinM, stepRel = settings.StepRelative;
            long samples = 0;
            for (int i = 0; i < n; i++)
            {
                double a = i * AzimuthStepDeg * Math.PI / 180.0, dx = Math.Sin(a), dz = Math.Cos(a);
                // Cones whose reach the ray passes through.
                int cands = 0;
                for (int c = 0; c < field.ConeCount; c++)
                {
                    HorizonCone cone = field.Cone(c);
                    double vx = cone.X - x, vz = cone.Z - z;
                    double along = vx * dx + vz * dz, perp = Math.Abs(vx * dz - vz * dx);
                    if (perp < cone.ReachM && along > start - cone.ReachM && along < max + cone.ReachM) _cand[cands++] = c;
                }
                int band = 0, row = 0;
                double bandEnd = settings.BandEdgesM[1];
                for (double d = start; d <= max; d += Math.Max(stepMin, d * stepRel))
                {
                    while (d >= bandEnd && band < bands - 1)
                    {
                        band++;
                        row = band * n;
                        bandEnd = settings.BandEdgesM[band + 1];
                    }
                    double px = x + dx * d, pz = z + dz * d;
                    float h;
                    bool has = field.TryGridHeight(px, pz, out h);
                    if (!has) h = float.MinValue;
                    for (int c = 0; c < cands; c++)
                    {
                        float ch = field.ConeHeight(_cand[c], px, pz);
                        if (ch > h)
                        {
                            h = ch;
                            has = true;
                        }
                    }
                    samples++;
                    if (!has) continue;
                    Offer(row + i, d, h, eyeAltitudeM, k2);
                }
                // Each candidate summit exactly where the ray passes closest to it.
                for (int c = 0; c < cands; c++)
                {
                    HorizonCone cone = field.Cone(_cand[c]);
                    double vx = cone.X - x, vz = cone.Z - z;
                    double along = vx * dx + vz * dz;
                    if (along < start || along > max) continue;
                    double px = x + dx * along, pz = z + dz * along;
                    float h = field.ConeHeight(_cand[c], px, pz), g;
                    if (field.TryGridHeight(px, pz, out g) && g > h) h = g;
                    if (h == float.MinValue) continue;
                    int b = BandOf(settings.BandEdgesM, along);
                    Offer(b * n + i, along, h, eyeAltitudeM, k2);
                    samples++;
                }
            }
            // Ground slope at each silhouette point, grid and summit cones alike: steepness (rock sheds snow) and the
            // gradient that turns the face below it towards or away from the light.
            for (int b = 0; b < bands; b++)
            {
                for (int i = 0; i < n; i++)
                {
                    int k = b * n + i;
                    if (float.IsNaN(_tan[k])) continue;
                    double a = i * AzimuthStepDeg * Math.PI / 180.0;
                    double px = x + Math.Sin(a) * _dist[k], pz = z + Math.Cos(a) * _dist[k];
                    float gx, gz;
                    Gradient(field, px, pz, out gx, out gz);
                    _gx[k] = gx;
                    _gz[k] = gz;
                    _steep[k] = (float)Math.Sqrt(gx * gx + gz * gz);
                }
            }
            FindSummits(field, x, z, eyeAltitudeM, settings);
            Samples = samples;
        }

        /// <summary>
        /// The curated summits that stand on their band's silhouette (higher than the rays either side of them): the
        /// mesher gives each one a column of its own at its exact position and published height, so a peak is drawn
        /// true from wherever the camera is, not only from the eye the panorama was built for.
        /// </summary>
        private void FindSummits(HorizonHeightField field, double x, double z, double eye, HorizonSettings settings)
        {
            if (_sumAz.Length < field.ConeCount)
            {
                _sumAz = new double[field.ConeCount];
                _sumDist = new float[field.ConeCount];
                _sumAlt = new float[field.ConeCount];
                _sumX = new double[field.ConeCount];
                _sumZ = new double[field.ConeCount];
                _sumBand = new int[field.ConeCount];
                _sumPeak = new int[field.ConeCount];
            }
            SummitCount = 0;
            int n = Azimuths;
            for (int c = 0; c < field.ConeCount; c++)
            {
                HorizonCone cone = field.Cone(c);
                double vx = cone.X - x, vz = cone.Z - z, d = Math.Sqrt(vx * vx + vz * vz);
                if (d < settings.BandEdgesM[0] || d > settings.MaxDistanceM) continue;
                double az = Math.Atan2(vx, vz) * 180.0 / Math.PI;
                if (az < 0) az += 360.0;
                int band = BandOf(settings.BandEdgesM, d);
                int i0 = (int)Math.Floor(az / AzimuthStepDeg) % n, i1 = (i0 + 1) % n;
                if (!Has(band, i0) || !Has(band, i1)) continue;
                double t = (cone.HeightM - eye - d * d * InverseTwoEffectiveRadius) / d;
                double e = Math.Atan(t) * 180.0 / Math.PI;
                double around = Math.Max(ElevationDeg(band, i0), ElevationDeg(band, i1));
                if (e < around - 0.02) continue; // a nearer ridge of the same band stands in front of it
                int k = SummitCount++;
                _sumAz[k] = az;
                _sumDist[k] = (float)d;
                _sumAlt[k] = cone.HeightM;
                _sumX[k] = cone.X;
                _sumZ[k] = cone.Z;
                _sumBand[k] = band;
                _sumPeak[k] = c;
            }
        }

        /// <summary>Curated summits standing on their band's silhouette (last build).</summary>
        public int SummitCount { get; private set; }

        /// <summary>Summit <paramref name="k"/>: band, bearing from the eye (degrees), horizontal distance, published
        /// height and game position.</summary>
        public void GetSummit(int k, out int band, out double azimuthDeg, out float distanceM, out float altitudeM, out double x, out double z)
        {
            band = _sumBand[k];
            azimuthDeg = _sumAz[k];
            distanceM = _sumDist[k];
            altitudeM = _sumAlt[k];
            x = _sumX[k];
            z = _sumZ[k];
        }

        /// <summary>The cone index (<see cref="HorizonHeightField.Cone"/>) of summit <paramref name="k"/>.</summary>
        public int SummitCone(int k)
        {
            return _sumPeak[k];
        }

        /// <summary>Height gradient (rise per metre east and north) of the field, cones included, over one grid cell.</summary>
        private static void Gradient(HorizonHeightField field, double x, double z, out float gx, out float gz)
        {
            double d = field.SpacingM;
            float e, w, n, s;
            if (!field.TryHeight(x + d, z, out e) || !field.TryHeight(x - d, z, out w) || !field.TryHeight(x, z + d, out n) ||
                !field.TryHeight(x, z - d, out s))
            {
                gx = gz = 0f;
                return;
            }
            gx = (float)((e - w) / (2 * d));
            gz = (float)((n - s) / (2 * d));
        }

        private void Offer(int k, double d, float h, double eye, double k2)
        {
            float t = (float)((h - eye - d * d * k2) / d);
            if (!(t > _tan[k]) && !float.IsNaN(_tan[k])) return;
            _tan[k] = t;
            _dist[k] = (float)d;
            _alt[k] = h;
        }

        private static int BandOf(double[] edges, double d)
        {
            for (int b = edges.Length - 2; b >= 0; b--)
                if (d >= edges[b]) return b;
            return 0;
        }

        /// <summary>True when band <paramref name="band"/> has a silhouette point on ray <paramref name="i"/>.</summary>
        public bool Has(int band, int i)
        {
            return !float.IsNaN(_tan[band * Azimuths + i]);
        }

        /// <summary>The silhouette point of a band on a ray: tangent of its elevation above the eye's horizon (curvature
        /// included), horizontal distance, absolute altitude and ground steepness (rise per metre). False when the band
        /// has no ground on that ray.</summary>
        public bool TryGet(int band, int i, out float tanElevation, out float distanceM, out float altitudeM, out float steepness)
        {
            int k = band * Azimuths + i;
            tanElevation = _tan[k];
            distanceM = _dist[k];
            altitudeM = _alt[k];
            steepness = _steep[k];
            return !float.IsNaN(tanElevation);
        }

        /// <summary>Ground gradient (rise per metre east, north) at a band's silhouette point on ray <paramref name="i"/>.</summary>
        public void GetGradient(int band, int i, out float gx, out float gz)
        {
            int k = band * Azimuths + i;
            gx = _gx[k];
            gz = _gz[k];
        }

        /// <summary>Elevation of a band's silhouette on ray <paramref name="i"/> in degrees (NaN when none).</summary>
        public double ElevationDeg(int band, int i)
        {
            float t = _tan[band * Azimuths + i];
            return float.IsNaN(t) ? double.NaN : Math.Atan(t) * 180.0 / Math.PI;
        }

        /// <summary>Elevation of the whole skyline (all bands) on ray <paramref name="i"/> in degrees, and the band that
        /// forms it (-1 when nothing).</summary>
        public double SkylineDeg(int i, out int band)
        {
            band = -1;
            float best = float.NaN;
            for (int b = 0; b < Bands; b++)
            {
                float t = _tan[b * Azimuths + i];
                if (float.IsNaN(t) || (!float.IsNaN(best) && t <= best)) continue;
                best = t;
                band = b;
            }
            return float.IsNaN(best) ? double.NaN : Math.Atan(best) * 180.0 / Math.PI;
        }
    }
}
