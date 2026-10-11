using System;

namespace Ghumante.Core.Horizon
{
    /// <summary>The light and air the skyline is shaded with (linear RGB, intensity included).</summary>
    public struct HorizonLight
    {
        /// <summary>Unit vector towards the main light (sun, or moon at night): x east, y up, z north.</summary>
        public float LightX, LightY, LightZ;

        public float LightR, LightG, LightB;

        /// <summary>Sky ambient (what the shaded flanks catch).</summary>
        public float AmbientR, AmbientG, AmbientB;

        /// <summary>Valley haze colour (the scene fog colour).</summary>
        public float FogR, FogG, FogB;

        /// <summary>Sky colour at the horizon (clear-air aerial perspective tends to it).</summary>
        public float HorizonR, HorizonG, HorizonB;

        /// <summary>The scene's exp² fog density per metre (WorldSky: the palette's haze, at least the tier's floor).</summary>
        public float FogDensity;

        /// <summary>The air's own haze density (the palette's, without the floor that hides the end of the streamed
        /// world; 0 = unknown, then <see cref="FogDensity"/>).</summary>
        public float AirDensity;
    }

    /// <summary>
    /// The cartoon look of the distant mountains (engine-free twin of <c>World/Sky/Skyline.shader</c>, which hard-codes
    /// the same numbers; the previews and tests shade with this). Ground colour comes from the altitude, as in the
    /// reference photos of the Langtang and Ganesh ranges from the valley (docs/research/w2/ref_sky.md): dark green hill
    /// forest below about 2 700 m, cool conifer to 3 300 m, olive-brown alpine slopes to the rock band, grey-blue rock
    /// and scree up to the snow line (about 5 000 m in October, higher on steep faces), white snow with rock ribs above.
    /// Light is two-tone (lit flank in the sun's colour, shaded flank in the bluish sky light). Air: the valley haze
    /// (the scene's fog) only fills the part of a sight line that runs below <see cref="HazeTopM"/>, so lines of sight
    /// to the snow peaks pass above it and stay crisp, as they do over hazy Kathmandu; clear air then tints everything
    /// towards the horizon blue over <see cref="ClearAirLengthM"/> (longer for sight lines through thinner, higher air).
    /// Detail along the curtain (snow-line variation, rock ribs, snow flutings) is noise of the metres along it, so it
    /// is continuous across the ring's columns and fixed to the ground between rebuilds.
    /// </summary>
    public static class HorizonLook
    {
        /// <summary>Top of the haze layer (absolute altitude, metres): the dry-season haze over the valley and the
        /// foothills reaches about 3 km, so the foot of the Himalaya stands in a pale band and the snow above is crisp.</summary>
        public const double HazeTopM = 3000.0;

        /// <summary>Clear-air aerial perspective length at sea level (metres); it grows with the mean altitude of the sight
        /// line over the air's scale height (<see cref="AirScaleHeightM"/>). Blue sky light scattered into the sight line
        /// turns far ranges blue: from the valley a 4 000 m ridge 45 km away keeps about 65 % of its own colour, a 7 000 m
        /// peak 70 km away about 60 % (snow, much brighter than anything else, still stands out against the sky).</summary>
        public const double ClearAirLengthM = 75000.0;

        /// <summary>Scale height of the air's density (metres).</summary>
        public const double AirScaleHeightM = 8000.0;

        /// <summary>Period of the fine rock ribs and snow flutings along the curtain (metres).</summary>
        public const double RibPeriodM = 150.0;

        /// <summary>Weights of the sky ambient and the main light: sunlit snow just reaches white without clipping
        /// (it keeps the light's warmth), shaded snow stays a clear sky blue.</summary>
        public const double AmbientShare = 0.7, LightShare = 0.8;

        /// <summary>Mean October snow line on the south faces (metres).</summary>
        public const double SnowLineM = 5000.0;

        // sRGB palette (hex) of the altitude bands.
        public const uint ForestHex = 0x3B5636, ConiferHex = 0x2F4438, AlpineHex = 0x6C6A5A, RockHex = 0x545A68, SnowHex = 0xF4F6F8;

        /// <summary>Share of the sight line from altitude <paramref name="eyeM"/> to <paramref name="targetM"/> that runs
        /// below the haze top (1 when both are below, 0 when both are above).</summary>
        public static double HazeFraction(double eyeM, double targetM)
        {
            double lo = Math.Min(eyeM, targetM), hi = Math.Max(eyeM, targetM);
            if (hi <= HazeTopM) return 1.0;
            if (lo >= HazeTopM) return 0.0;
            return (HazeTopM - lo) / Math.Max(1.0, hi - lo);
        }

        /// <summary>
        /// Share of a surface's colour that survives the valley haze along the sight line: exp(-(ρ·d·f)²) over the hazy
        /// share f of the line only. A line that stays in the haze (f = 1) gets the scene fog <paramref name="fogDensity"/>
        /// exactly, so a ridge crossing the handover keeps its colour; a line that rises above it is hazed more and more
        /// by the air's own density <paramref name="airDensity"/> instead (the scene fog's floor on the low tiers only
        /// hides the end of the streamed world, which such a line looks past). 0 = unknown: the scene fog.
        /// </summary>
        public static double HazeTransmittance(double distanceM, double eyeM, double targetM, double fogDensity, double airDensity)
        {
            double f = HazeFraction(eyeM, targetM);
            double air = airDensity > 0 ? airDensity : fogDensity;
            double t = (air + (fogDensity - air) * f) * distanceM * f;
            return Math.Exp(-t * t);
        }

        /// <summary>Share that survives the clear air (aerial perspective towards the horizon blue).</summary>
        public static double ClearAirTransmittance(double distanceM, double eyeM, double targetM)
        {
            double length = ClearAirLengthM * Math.Exp(0.5 * (Math.Max(0.0, eyeM) + Math.Max(0.0, targetM)) / AirScaleHeightM);
            return Math.Exp(-distanceM / length);
        }

        /// <summary>Both together: how much of a peak's own colour reaches the eye.</summary>
        public static double Transmittance(double distanceM, double eyeM, double targetM, double fogDensity, double airDensity)
        {
            return HazeTransmittance(distanceM, eyeM, targetM, fogDensity, airDensity) * ClearAirTransmittance(distanceM, eyeM, targetM);
        }

        /// <summary>Slow variation in [0, 1) along the curtain (about 1.8 km per feature): moves the snow line up and down
        /// from one face to the next.</summary>
        public static double Variation(double alongM)
        {
            return ValueNoise((float)(alongM / 1800.0), 0.37f);
        }

        /// <summary>
        /// Snow line at a point <paramref name="alongM"/> metres along the curtain: the mean, ±300 m from face to face
        /// (<see cref="Variation"/>), and up to 400 m higher on rock-steep ground (<paramref name="rock"/> in [0, 1]).
        /// </summary>
        public static double SnowLine(double alongM, double rock)
        {
            return SnowLineM + (Variation(alongM) - 0.5) * 600.0 + rock * 400.0;
        }

        /// <summary>
        /// How much of the fine rib detail to show for a screen footprint of <paramref name="footprintM"/> metres per
        /// pixel: all of it while a rib spans 6 pixels or more, none below 3 (no shimmer at the wide landscape view;
        /// the shader passes <c>fwidth</c> of the metres along the curtain).
        /// </summary>
        public static double Detail(double footprintM)
        {
            double px = RibPeriodM / Math.Max(1e-3, footprintM);
            double d = (px - 3.0) / 3.0;
            return d < 0 ? 0 : d > 1 ? 1 : d;
        }

        /// <summary>
        /// Rib noise in [0, 1) at <paramref name="alongM"/> metres along the curtain and <paramref name="altitudeM"/>:
        /// fine ribs (<see cref="RibPeriodM"/> across, long down the slope) over larger slanting rock patches; the fine
        /// part fades with <paramref name="detail"/> (<see cref="Detail"/>).
        /// </summary>
        public static double Streak(double alongM, double altitudeM, double detail)
        {
            double coarse = ValueNoise((float)(alongM / 520.0 + altitudeM / 1800.0), (float)(altitudeM / 2500.0));
            double fine = ValueNoise((float)(alongM / RibPeriodM), (float)(altitudeM / 450.0));
            return coarse + (0.65 * fine + 0.35 * coarse - coarse) * detail;
        }

        /// <summary>
        /// Linear albedo at altitude <paramref name="altitudeM"/>, <paramref name="alongM"/> metres along the curtain:
        /// forest, conifer, alpine, rock, then snow above the snow line, broken by rock ribs and gullies
        /// (<see cref="Streak"/>), more of them on steep ground (<paramref name="rock"/> in [0, 1]).
        /// </summary>
        public static void Albedo(double altitudeM, double rock, double alongM, double detail, out float r, out float g, out float b)
        {
            double snow = SnowLine(alongM, rock), streak = Streak(alongM, altitudeM, detail);
            Lin(ForestHex, out double fr, out double fg, out double fb);
            Lin(ConiferHex, out double cr, out double cg, out double cb);
            Lin(AlpineHex, out double ar, out double ag, out double ab);
            Lin(RockHex, out double rr, out double rg, out double rb);
            Lin(SnowHex, out double sr, out double sg, out double sb);
            double x = Smooth(2600.0, 2900.0, altitudeM);
            double cr0 = fr + (cr - fr) * x, cg0 = fg + (cg - fg) * x, cb0 = fb + (cb - fb) * x;
            x = Smooth(3500.0, 4000.0, altitudeM);
            cr0 += (ar - cr0) * x;
            cg0 += (ag - cg0) * x;
            cb0 += (ab - cb0) * x;
            x = Smooth(snow - 900.0, snow - 500.0, altitudeM);
            cr0 += (rr - cr0) * x;
            cg0 += (rg - cg0) * x;
            cb0 += (rb - cb0) * x;
            double s = Smooth(snow - 80.0, snow + 80.0, altitudeM) * (1.0 - Smooth(0.56, 0.62, streak) * (0.3 + 0.6 * rock));
            r = (float)(cr0 + (sr - cr0) * s);
            g = (float)(cg0 + (sg - cg0) * s);
            b = (float)(cb0 + (sb - cb0) * s);
        }

        /// <summary>
        /// Final linear colour of a skyline point: the albedo lit two-tone by <paramref name="light"/> through the
        /// normal (nx, ny, nz) (<see cref="RibNormal"/>), then the valley haze and the clear air for a sight line of
        /// <paramref name="distanceM"/> from <paramref name="eyeM"/> up to the point's altitude.
        /// </summary>
        public static void Shade(double altitudeM, double rock, double alongM, double detail, double nx, double ny, double nz, double distanceM,
                                 double eyeM, in HorizonLight light, out float r, out float g, out float b)
        {
            float ar, ag, ab;
            Albedo(altitudeM, rock, alongM, detail, out ar, out ag, out ab);
            double ndl = nx * light.LightX + ny * light.LightY + nz * light.LightZ;
            double lit = Smooth(0.02, 0.22, ndl);
            double cr = ar * (light.AmbientR * AmbientShare + light.LightR * lit * LightShare);
            double cg = ag * (light.AmbientG * AmbientShare + light.LightG * lit * LightShare);
            double cb = ab * (light.AmbientB * AmbientShare + light.LightB * lit * LightShare);
            double th = HazeTransmittance(distanceM, eyeM, altitudeM, light.FogDensity, light.AirDensity);
            cr = light.FogR + (cr - light.FogR) * th;
            cg = light.FogG + (cg - light.FogG) * th;
            cb = light.FogB + (cb - light.FogB) * th;
            double tc = ClearAirTransmittance(distanceM, eyeM, altitudeM);
            r = (float)(light.HorizonR + (cr - light.HorizonR) * tc);
            g = (float)(light.HorizonG + (cg - light.HorizonG) * tc);
            b = (float)(light.HorizonB + (cb - light.HorizonB) * tc);
        }

        /// <summary>Hash noise in [0, 1) ("hash without sine", the shader's GhSkylineHash): stable on every GPU.</summary>
        public static float Hash(float x, float y)
        {
            float px = Frac(x * 0.1031f), py = Frac(y * 0.1031f), pz = Frac(x * 0.1031f);
            float d = px * (py + 33.33f) + py * (pz + 33.33f) + pz * (px + 33.33f);
            px += d;
            py += d;
            pz += d;
            return Frac((px + py) * pz);
        }

        /// <summary>Value noise in [0, 1) with smooth interpolation between integer lattice points.</summary>
        public static float ValueNoise(float x, float y)
        {
            float ix = (float)Math.Floor(x), iy = (float)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
            return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
        }

        /// <summary>
        /// Bends the face normal (nx, ny, nz) across the curtain by the slope of the rib noise at a point
        /// <paramref name="alongM"/> metres along it (tangent tx, 0, tz) and <paramref name="altitudeM"/> high: snow
        /// flutings and rock ribs then catch the light on one side and fall into shade on the other, as on the real faces.
        /// </summary>
        public static void RibNormal(double nx, double ny, double nz, double tx, double tz, double alongM, double altitudeM, double detail,
                                     double strength, out double rx, out double ry, out double rz)
        {
            const double h = 20.0;
            double slope = (Streak(alongM + h, altitudeM, detail) - Streak(alongM - h, altitudeM, detail)) / (2 * h) * RibPeriodM;
            rx = nx + tx * slope * strength;
            ry = ny;
            rz = nz + tz * slope * strength;
            double l = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            rx /= l;
            ry /= l;
            rz /= l;
        }

        /// <summary>sRGB hex to linear components.</summary>
        public static void Lin(uint hex, out double r, out double g, out double b)
        {
            r = ToLinear(((hex >> 16) & 0xFF) / 255.0);
            g = ToLinear(((hex >> 8) & 0xFF) / 255.0);
            b = ToLinear((hex & 0xFF) / 255.0);
        }

        /// <summary>sRGB transfer function (component in [0, 1]).</summary>
        public static double ToLinear(double c)
        {
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>Inverse sRGB transfer function.</summary>
        public static double ToSrgb(double c)
        {
            if (c <= 0) return 0;
            if (c >= 1) return 1;
            return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
        }

        private static double Smooth(double e0, double e1, double x)
        {
            double t = (x - e0) / (e1 - e0);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        private static float Frac(float v)
        {
            return v - (float)Math.Floor(v);
        }
    }
}
