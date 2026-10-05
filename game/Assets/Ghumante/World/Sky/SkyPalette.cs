using System;

namespace Ghumante.World.Sky
{
    /// <summary>An sRGB colour with components in [0, 1].</summary>
    public struct SkyColor
    {
        public float R, G, B;

        public SkyColor(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>From 0xRRGGBB.</summary>
        public static SkyColor Hex(uint rgb)
        {
            return new SkyColor(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        public static SkyColor Lerp(SkyColor a, SkyColor b, float t)
        {
            return new SkyColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        public override string ToString()
        {
            return "#" + ((int)Math.Round(R * 255)).ToString("X2") + ((int)Math.Round(G * 255)).ToString("X2") + ((int)Math.Round(B * 255)).ToString("X2");
        }
    }

    /// <summary>Everything the sky, the light, the fog and the ambient need at one moment.</summary>
    public struct SkyState
    {
        public float Hours;

        /// <summary>Sun elevation above the horizon and azimuth (clockwise from north), degrees.</summary>
        public double SunElevationDeg, SunAzimuthDeg;

        /// <summary>Unit vector towards the sun (X east, Y up, Z north).</summary>
        public float SunX, SunY, SunZ;

        /// <summary>Unit vector towards the moon (high in the sky opposite the sun).</summary>
        public float MoonX, MoonY, MoonZ;

        /// <summary>True when the directional light stands for the moon (sun more than 6 degrees below the horizon).</summary>
        public bool LightIsMoon;

        public SkyColor Zenith, Horizon, Ground, Fog, Light, AmbientSky, AmbientEquator, AmbientGround;

        public float LightIntensity;
        public float ShadowStrength;

        /// <summary>Exponential-squared fog density per metre.</summary>
        public float FogDensity;

        /// <summary>0..1: sun disc visibility, night amount and golden-hour amount.</summary>
        public float SunVisible, Night, Golden;
    }

    /// <summary>
    /// The day: sun position over the Kathmandu Valley and the time-of-day palette (ARCHITECTURE.md 8; ASSET_MANIFEST
    /// 11 "ghm_sky_gradient_atlas" keys, valley-haze preset), keyed by sun elevation so sunrise and sunset mirror each
    /// other, with a warmer, pinker evening. Sunrise is the hero moment: golden alpenglow light (w.alpenglow #FFB07A)
    /// grazing the snow peaks while the valley still lies in blue shadow. Colours are placeholders until the sky art
    /// lands, but the structure (keys, haze, ambient) is what art will tune. Engine-free and allocation-free.
    /// </summary>
    public static class SkyPalette
    {
        /// <summary>Kathmandu, degrees north.</summary>
        public const double DefaultLatitudeDeg = 27.7;

        /// <summary>Exponential-squared density of the valley haze in daylight: about 60 % of a peak 60 km away still
        /// shows through (exp(-(d·density)²)), 25 % at 100 km, so the Himalaya fade to blue but stay visible.</summary>
        public const float DayFogDensity = 1.2e-5f;

        private struct Key
        {
            public float Elevation;
            public uint Zenith, Horizon, Ground, Fog, Light, AmbSky, AmbEquator, AmbGround;
            public float Density;
        }

        // Keys by sun elevation (degrees). Night, pre-dawn, blue hour, sunrise alpenglow, golden, morning, midday.
        private static readonly Key[] Keys =
        {
            new Key { Elevation = -18f, Zenith = 0x0B1230, Horizon = 0x18244A, Ground = 0x0A0F1E, Fog = 0x16203A, Light = 0x8DA2D6,
                      AmbSky = 0x1C2848, AmbEquator = 0x141C34, AmbGround = 0x0A0E1A, Density = 1.0e-5f },
            new Key { Elevation = -10f, Zenith = 0x16224A, Horizon = 0x2C3C6C, Ground = 0x111830, Fog = 0x26345E, Light = 0x8DA2D6,
                      AmbSky = 0x26345E, AmbEquator = 0x1E2848, AmbGround = 0x0E1222, Density = 1.1e-5f },
            new Key { Elevation = -4f, Zenith = 0x2B3F7C, Horizon = 0x9A7090, Ground = 0x2A2A40, Fog = 0x6E6A8A, Light = 0xFF8A4A,
                      AmbSky = 0x3E4C7E, AmbEquator = 0x584A6A, AmbGround = 0x1E1C2A, Density = 1.5e-5f },
            new Key { Elevation = 0f, Zenith = 0x4870B8, Horizon = 0xFF9A55, Ground = 0x5A4A50, Fog = 0xE0A27A, Light = 0xFFA463,
                      AmbSky = 0x6A7CB0, AmbEquator = 0xB88A78, AmbGround = 0x463830, Density = 1.6e-5f },
            new Key { Elevation = 6f, Zenith = 0x4F86D0, Horizon = 0xFFC27A, Ground = 0x7A6A60, Fog = 0xE9C39A, Light = 0xFFB07A,
                      AmbSky = 0x7E98C8, AmbEquator = 0xC8A890, AmbGround = 0x5A4C40, Density = 1.5e-5f },
            new Key { Elevation = 15f, Zenith = 0x3F8EE6, Horizon = 0xBFE0F7, Ground = 0x8098A8, Fog = 0xB4CDE2, Light = 0xFFE6C4,
                      AmbSky = 0x8FB2E0, AmbEquator = 0xBCC8D0, AmbGround = 0x6A665A, Density = 1.3e-5f },
            new Key { Elevation = 35f, Zenith = 0x2F86E8, Horizon = 0xB6DDF7, Ground = 0x7E9CB2, Fog = 0xA8C6E3, Light = 0xFFF4E2,
                      AmbSky = 0x92B6E6, AmbEquator = 0xC4D2DE, AmbGround = 0x6E6A5C, Density = DayFogDensity },
            new Key { Elevation = 90f, Zenith = 0x2A7FE6, Horizon = 0xB0DAF6, Ground = 0x7A9AB0, Fog = 0xA4C3E0, Light = 0xFFF8EE,
                      AmbSky = 0x94B8E8, AmbEquator = 0xC6D4E0, AmbGround = 0x706C5E, Density = DayFogDensity },
        };

        private static readonly SkyColor EveningTint = SkyColor.Hex(0xFF6E6A);

        /// <summary>Sun declination in degrees for a day of the year (1..366), Cooper's formula.</summary>
        public static double DeclinationDeg(int dayOfYear)
        {
            return 23.44 * Math.Sin(2.0 * Math.PI * (284 + dayOfYear) / 365.0);
        }

        /// <summary>
        /// Unit vector towards the sun (X east, Y up, Z north) at local solar time <paramref name="hours"/> for a latitude
        /// and declination, plus its elevation and azimuth (clockwise from north) in degrees.
        /// </summary>
        public static void SunDirection(double hours, double latitudeDeg, double declinationDeg, out double x, out double y, out double z,
                                        out double elevationDeg, out double azimuthDeg)
        {
            double h = (hours - 12.0) * 15.0 * Math.PI / 180.0; // hour angle, + in the afternoon
            double phi = latitudeDeg * Math.PI / 180.0, dec = declinationDeg * Math.PI / 180.0;
            double east = -Math.Cos(dec) * Math.Sin(h);
            double north = Math.Sin(dec) * Math.Cos(phi) - Math.Cos(dec) * Math.Cos(h) * Math.Sin(phi);
            double up = Math.Sin(dec) * Math.Sin(phi) + Math.Cos(dec) * Math.Cos(h) * Math.Cos(phi);
            double len = Math.Sqrt(east * east + north * north + up * up);
            x = east / len;
            y = up / len;
            z = north / len;
            elevationDeg = Math.Asin(Math.Max(-1.0, Math.Min(1.0, y))) * 180.0 / Math.PI;
            azimuthDeg = Math.Atan2(x, z) * 180.0 / Math.PI;
            if (azimuthDeg < 0) azimuthDeg += 360.0;
        }

        /// <summary>The sky at local solar time <paramref name="hours"/> (wrapped to [0, 24)).</summary>
        public static SkyState Evaluate(double hours, int dayOfYear = 280, double latitudeDeg = DefaultLatitudeDeg)
        {
            hours %= 24.0;
            if (hours < 0) hours += 24.0;
            var s = new SkyState { Hours = (float)hours };
            double sx, sy, sz, elev, az;
            SunDirection(hours, latitudeDeg, DeclinationDeg(dayOfYear), out sx, out sy, out sz, out elev, out az);
            s.SunX = (float)sx;
            s.SunY = (float)sy;
            s.SunZ = (float)sz;
            s.SunElevationDeg = elev;
            s.SunAzimuthDeg = az;

            // The moon: high, opposite the sun's azimuth.
            double mx = -sx, mz = -sz, my = 0.45 + 0.5 * Math.Abs(sy);
            double ml = Math.Sqrt(mx * mx + my * my + mz * mz);
            s.MoonX = (float)(mx / ml);
            s.MoonY = (float)(my / ml);
            s.MoonZ = (float)(mz / ml);

            float e = (float)elev;
            int k = 0;
            while (k < Keys.Length - 2 && e > Keys[k + 1].Elevation) k++;
            Key a = Keys[k], b = Keys[k + 1];
            float t = Clamp01((e - a.Elevation) / (b.Elevation - a.Elevation));
            s.Zenith = Mix(a.Zenith, b.Zenith, t);
            s.Horizon = Mix(a.Horizon, b.Horizon, t);
            s.Ground = Mix(a.Ground, b.Ground, t);
            s.Fog = Mix(a.Fog, b.Fog, t);
            s.Light = Mix(a.Light, b.Light, t);
            s.AmbientSky = Mix(a.AmbSky, b.AmbSky, t);
            s.AmbientEquator = Mix(a.AmbEquator, b.AmbEquator, t);
            s.AmbientGround = Mix(a.AmbGround, b.AmbGround, t);
            s.FogDensity = a.Density + (b.Density - a.Density) * t;

            // Golden hour: strongest at the horizon, gone by 14 degrees; evenings are pinker than mornings.
            s.Golden = 1f - SmoothStep(2f, 14f, Math.Abs(e));
            if (e < 0f) s.Golden *= 1f - SmoothStep(-2f, -8f, e);
            bool evening = hours > 12.0;
            if (evening && s.Golden > 0f)
            {
                float w = 0.35f * s.Golden;
                s.Horizon = SkyColor.Lerp(s.Horizon, EveningTint, w);
                s.Fog = SkyColor.Lerp(s.Fog, EveningTint, w * 0.6f);
                s.Light = SkyColor.Lerp(s.Light, EveningTint, w * 0.5f);
            }

            s.SunVisible = SmoothStep(-1.5f, 1.0f, e);
            s.Night = 1f - SmoothStep(-14f, -4f, e);

            // One directional light: the sun from 1 degree below the horizon up (grazing alpenglow on the peaks), the
            // moon below -6 degrees; both fade to zero at the hand-over so it never jumps.
            if (e > -6f)
            {
                s.LightIsMoon = false;
                s.LightIntensity = SmoothStep(-1f, 4f, e) * (0.7f + 0.35f * SmoothStep(4f, 35f, e));
                s.ShadowStrength = 0.85f;
            }
            else
            {
                s.LightIsMoon = true;
                s.LightIntensity = 0.2f * SmoothStep(-6f, -12f, e);
                s.ShadowStrength = 0.45f;
            }
            return s;
        }

        private static SkyColor Mix(uint a, uint b, float t)
        {
            return SkyColor.Lerp(SkyColor.Hex(a), SkyColor.Hex(b), t);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>Hermite step from <paramref name="edge0"/> to <paramref name="edge1"/> (either order).</summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
