using System;

namespace Ghumante.Core.Synth
{
    /// <summary>
    /// Distance, air absorption and Doppler math (W2_DESIGN 7.1 "Distance" and "Doppler in our own code";
    /// A §6.2–6.3). Engine-free so the Unity side and the tests share one implementation.
    /// </summary>
    public static class SoundSpace
    {
        /// <summary>Doppler pitch clamp (W2_DESIGN 7.1).</summary>
        public const float MinPitch = 0.7f, MaxPitch = 1.4f;

        /// <summary>Doppler smoothing time constant (s).</summary>
        public const float DopplerSmoothS = 0.05f;

        /// <summary>Fade knee: 1/r up to this fraction of max, then a smooth fade to 0 at max.</summary>
        public const float FadeKnee = 0.7f;

        /// <summary>Lowest one-shot level (dBFS at the listener) worth starting.</summary>
        public const float CullDb = -45f;

        // Air absorption cutoff by distance (W2_DESIGN 7.1): off below 50 m.
        private static readonly float[] AirDist = { 50f, 200f, 500f, 1000f, 2000f, 4000f, 8000f };
        private static readonly float[] AirHz = { 20000f, 11000f, 7000f, 4500f, 3000f, 1800f, 1100f };

        /// <summary>
        /// Custom rolloff gain: 1 inside <paramref name="minM"/>, <c>min / d</c> up to 0.7 × max, then a
        /// cosine fade to exactly 0 at <paramref name="maxM"/> (A §6.2: Unity's log rolloff never reaches 0).
        /// Continuous and non-increasing in distance.
        /// </summary>
        public static float Rolloff(float distanceM, float minM, float maxM)
        {
            float d = Dsp.Sanitize(distanceM);
            if (d < 0f) d = -d;
            float mn = minM > 0.01f ? minM : 0.01f;
            float mx = maxM > mn ? maxM : mn * 1.01f;
            if (d >= mx) return 0f;
            if (d <= mn) return 1f;
            float knee = FadeKnee * mx;
            if (knee <= mn) knee = mn;
            if (d <= knee) return mn / d;
            float atKnee = mn / knee;
            float u = (d - knee) / (mx - knee);
            return atKnee * (0.5f + 0.5f * Dsp.Cos(0.5 * u));
        }

        /// <summary>Rolloff for a <see cref="SoundClass"/>.</summary>
        public static float Rolloff(float distanceM, SoundClass c)
        {
            SoundClassInfo i = SoundClasses.Get(c);
            return Rolloff(distanceM, i.MinM, i.MaxM);
        }

        /// <summary>Air-absorption low-pass cutoff at a distance (Hz); 0 means off (closer than 50 m).</summary>
        public static float AirCutoffHz(float distanceM)
        {
            float d = Dsp.Sanitize(distanceM);
            if (d < 0f) d = -d;
            if (d <= AirDist[0]) return 0f;
            if (d >= AirDist[AirDist.Length - 1]) return AirHz[AirHz.Length - 1];
            for (int i = 1; i < AirDist.Length; i++)
            {
                if (d > AirDist[i]) continue;
                // Interpolate in log distance and log frequency.
                double t = (Math.Log(d) - Math.Log(AirDist[i - 1])) / (Math.Log(AirDist[i]) - Math.Log(AirDist[i - 1]));
                double hz = Math.Exp(Math.Log(AirHz[i - 1]) + (Math.Log(AirHz[i]) - Math.Log(AirHz[i - 1])) * t);
                return (float)hz;
            }
            return 0f;
        }

        /// <summary>
        /// Doppler pitch <c>(c + v_listener·n) / (c − v_source·n)</c>, n the unit vector from source to listener,
        /// with velocities from the simulation (never from transform deltas, so floating-origin rebases cannot
        /// spike it). <paramref name="strength"/> scales the shift (0 = none). Clamped to 0.7–1.4.
        /// </summary>
        public static float DopplerPitch(float sx, float sy, float sz, float svx, float svy, float svz,
                                         float lx, float ly, float lz, float lvx, float lvy, float lvz, float strength)
        {
            float k = Dsp.Sanitize(strength);
            if (k <= 0f) return 1f;
            float nx = lx - sx, ny = ly - sy, nz = lz - sz;
            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (!(len > 1e-3f) || float.IsInfinity(len)) return 1f;
            nx /= len;
            ny /= len;
            nz /= len;
            float vs = Dsp.Sanitize(svx) * nx + Dsp.Sanitize(svy) * ny + Dsp.Sanitize(svz) * nz;   // source towards listener
            float vl = Dsp.Sanitize(lvx) * nx + Dsp.Sanitize(lvy) * ny + Dsp.Sanitize(lvz) * nz;   // listener away from source
            float c = Dsp.SpeedOfSound;
            vs = Dsp.Clamp(vs * k, -0.9f * c, 0.9f * c);
            vl = Dsp.Clamp(vl * k, -0.9f * c, 0.9f * c);
            float p = (c - vl) / (c - vs);
            return Dsp.Clamp(p, MinPitch, MaxPitch);
        }

        /// <summary>Estimated level at the listener (dBFS) of a class sound at a distance, for the start cull.</summary>
        public static float EstimateDb(SoundClass c, float distanceM, float extraDb = 0f)
        {
            SoundClassInfo i = SoundClasses.Get(c);
            float refGain = Rolloff(Math.Max(10f, i.MinM), i.MinM, i.MaxM);
            float g = Rolloff(distanceM, i.MinM, i.MaxM);
            if (!(g > 0f)) return -200f;
            return i.LevelDbAt10M + extraDb + Dsp.GainToDb(g / Math.Max(refGain, 1e-6f));
        }

        /// <summary>True when a one-shot is loud enough at the listener to be worth starting (≥ −45 dBFS).</summary>
        public static bool Audible(SoundClass c, float distanceM, float extraDb = 0f)
        {
            return EstimateDb(c, distanceM, extraDb) >= CullDb;
        }

        /// <summary>Galli slapback delay <c>2w / 343</c> (s) for a lane of width <paramref name="laneWidthM"/>.</summary>
        public static float SlapbackDelayS(float laneWidthM)
        {
            float w = Dsp.Clamp(laneWidthM, 1f, 30f);
            return 2f * w / Dsp.SpeedOfSound;
        }

        /// <summary>Ground-reflection delay <c>2h·sinθ / c</c> (s) for a source at height <paramref name="heightM"/>
        /// above ground seen at elevation angle θ (sinθ = height difference / distance).</summary>
        public static float ReflectionDelayS(float heightM, float distanceM)
        {
            float h = Math.Max(0f, Dsp.Sanitize(heightM));
            float d = Math.Max(1f, Dsp.Sanitize(distanceM));
            float sin = Dsp.Clamp(h / d, 0f, 1f);
            return 2f * h * sin / Dsp.SpeedOfSound;
        }

        /// <summary>Listener position: 35% of the way from the camera to the player's head (W2_DESIGN 7.1).</summary>
        public static void ListenerPoint(float cx, float cy, float cz, float hx, float hy, float hz,
                                         out float x, out float y, out float z)
        {
            const float t = 0.35f;
            x = cx + (hx - cx) * t;
            y = cy + (hy - cy) * t;
            z = cz + (hz - cz) * t;
        }
    }

    /// <summary>
    /// One-pole smoother with a time constant, frame-rate independent (Doppler 50 ms, crossfades). A mutable
    /// struct.
    /// </summary>
    public struct Smoothed
    {
        public float Value;
        private bool _init;

        public float Step(float target, float dt, float timeS)
        {
            float x = Dsp.Sanitize(target);
            if (!_init || !(timeS > 0f))
            {
                _init = true;
                Value = x;
                return Value;
            }
            float h = Dsp.Clamp(dt, 0f, 1f);
            float a = 1f - (float)Math.Exp(-h / timeS);
            Value += (x - Value) * a;
            return Value;
        }

        public void Reset(float v)
        {
            Value = v;
            _init = true;
        }
    }
}
