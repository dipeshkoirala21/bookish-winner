using System;

namespace Ghumante.Core.Synth
{
    // Recipes for footsteps, vehicle one-shots, sacred sounds, street and UI sounds (W2_DESIGN 7.3; A §2, §4.3).
    // Every value marked [E] in the research is a starting point to tune by ear.
    public static partial class ProceduralBank
    {
        // ---------------------------------------------------------------------------------------------------
        // Footsteps (A §2.1). Pure synthesis for now (the design's CC0 recordings can replace any set later).
        // ---------------------------------------------------------------------------------------------------

        private static float[] RenderFootstep(FootstepSurface surface, ref BankContext c)
        {
            int v = c.Variant;
            FootstepGait gait = v < 6 ? FootstepGait.Walk : v < 12 ? FootstepGait.Run : v == 12 ? FootstepGait.Land : FootstepGait.Scuff;
            float len = surface == FootstepSurface.Mud ? 0.42f : surface == FootstepSurface.Metal ? 0.45f : 0.35f;
            var o = new float[c.Samples(len)];
            float heel = gait == FootstepGait.Run ? 1.2f : gait == FootstepGait.Land ? 1.5f : 1f;
            float toeAt = gait == FootstepGait.Run ? c.Range(0.025f, 0.035f) : c.Range(0.045f, 0.07f);
            float pitch = c.Range(0.92f, 1.08f);

            if (gait == FootstepGait.Scuff)
            {
                // A drag: band noise with a slow swell.
                BankKit.NoiseBurst(o, 0f, ref c, 0.04f, 0.06f, 0.2f, BiquadKind.BandPass, 1800f * pitch, 0.8f, 0.5f);
                Impact(o, 0.12f, ref c, surface, 0.4f, pitch);
            }
            else
            {
                Impact(o, 0.002f, ref c, surface, heel, pitch);
                if (gait != FootstepGait.Land) Impact(o, toeAt, ref c, surface, heel * 0.45f, pitch * 1.08f);
                else Impact(o, 0.012f, ref c, surface, heel * 0.8f, pitch * 0.95f);
            }

            // Surface extras.
            if (surface == FootstepSurface.Brick && (v % 10) == 3)
            {
                // 10% of steps: a loose-brick clink.
                BankKit.Modal(o, 0.03f, ref c, new[] { 2100f * pitch, 4900f * pitch }, new[] { 0.08f, 0.05f }, new[] { 1f, 0.5f }, 0.35f, 0.15f);
            }
            if (surface == FootstepSurface.Mud)
            {
                // Lift squelch 120–200 ms after contact (comic, exaggerated).
                float lift = c.Range(0.12f, 0.2f);
                SquelchLift(o, lift, ref c);
            }
            BankKit.FadeOut(o, c, 0.02f);
            Dsp.Normalize(o, 0, o.Length, gait == FootstepGait.Land ? 0.95f : gait == FootstepGait.Run ? 0.85f : 0.7f);
            return o;
        }

        private static void Impact(float[] o, float t, ref BankContext c, FootstepSurface s, float g, float pitch)
        {
            switch (s)
            {
                case FootstepSurface.Asphalt:
                    BankKit.NoiseBurst(o, t, ref c, 0.001f, 0.012f, 0.08f, BiquadKind.BandPass, 2200f * pitch, 1.0f, g);
                    BankKit.Tone(o, t, ref c, 120f * pitch, 90f, 0.06f, 0.002f, 0.04f, 0.3f * g, Wave.Sine, decayTauS: 0.015f);
                    break;
                case FootstepSurface.Concrete:
                    BankKit.NoiseBurst(o, t, ref c, 0.001f, 0.010f, 0.07f, BiquadKind.BandPass, 2600f * pitch, 1.2f, g);
                    BankKit.Tone(o, t, ref c, 140f * pitch, 100f, 0.05f, 0.002f, 0.03f, 0.25f * g, Wave.Sine, decayTauS: 0.012f);
                    break;
                case FootstepSurface.Brick:
                    BankKit.NoiseBurst(o, t, ref c, 0.001f, 0.012f, 0.08f, BiquadKind.BandPass, 2000f * pitch, 1.0f, g);
                    BankKit.Tone(o, t, ref c, 160f * pitch, 120f, 0.06f, 0.002f, 0.04f, 0.3f * g, Wave.Sine, decayTauS: 0.018f);
                    // Grit layer 3–6 kHz: a few micro-clicks.
                    for (int i = 0; i < 6; i++)
                        BankKit.NoiseBurst(o, t + c.Range(0.005f, 0.05f), ref c, 0.0005f, 0.002f, 0.01f, BiquadKind.BandPass, c.Range(3000f, 6000f), 2f, 0.25f * g);
                    break;
                case FootstepSurface.Stone:
                    BankKit.NoiseBurst(o, t, ref c, 0.0007f, 0.008f, 0.06f, BiquadKind.BandPass, 3000f * pitch, 1.5f, g);
                    BankKit.Modal(o, t, ref c, new[] { 1800f * pitch, 3700f * pitch }, new[] { 0.07f, 0.05f }, new[] { 0.6f, 0.3f }, 0.3f * g, 0.12f);
                    break;
                case FootstepSurface.Gravel:
                {
                    // Granular crunch: 15–40 micro-impulses over 100–200 ms, 2–8 kHz.
                    int grains = 15 + c.Rng.Index(26);
                    float span = c.Range(0.1f, 0.2f);
                    for (int i = 0; i < grains; i++)
                        BankKit.NoiseBurst(o, t + span * c.Rng.Next01() * c.Rng.Next01(), ref c, 0.0003f, c.Range(0.001f, 0.004f), 0.015f,
                                           BiquadKind.BandPass, c.Range(2000f, 8000f), 1.5f, c.Range(0.2f, 0.6f) * g);
                    BankKit.Tone(o, t, ref c, 110f * pitch, 80f, 0.07f, 0.003f, 0.05f, 0.25f * g, Wave.Sine, decayTauS: 0.02f);
                    break;
                }
                case FootstepSurface.Dirt:
                    BankKit.Tone(o, t, ref c, 250f * pitch, 160f, 0.08f, 0.003f, 0.05f, 0.7f * g, Wave.Sine, decayTauS: 0.025f);
                    BankKit.NoiseBurst(o, t + 0.01f, ref c, 0.008f, 0.04f, 0.12f, BiquadKind.BandPass, 1800f * pitch, 0.8f, 0.3f * g);
                    break;
                case FootstepSurface.Mud:
                    BankKit.NoiseBurst(o, t, ref c, 0.004f, 0.05f, 0.15f, BiquadKind.BandPass, 500f * pitch, 3f, 1.4f * g);
                    BankKit.Tone(o, t, ref c, 180f * pitch, 120f, 0.1f, 0.004f, 0.06f, 0.5f * g, Wave.Sine, decayTauS: 0.03f);
                    break;
                case FootstepSurface.Grass:
                    BankKit.NoiseBurst(o, t, ref c, 0.02f, 0.06f, 0.16f, BiquadKind.BandPass, 4000f * pitch, 0.7f, 0.8f * g);
                    BankKit.Tone(o, t, ref c, 120f * pitch, 90f, 0.06f, 0.004f, 0.04f, 0.2f * g, Wave.Sine, decayTauS: 0.02f);
                    break;
                case FootstepSurface.Wood:
                    BankKit.NoiseBurst(o, t, ref c, 0.0007f, 0.008f, 0.05f, BiquadKind.BandPass, 2000f * pitch, 1.2f, 0.8f * g);
                    BankKit.Modal(o, t, ref c, new[] { 180f * pitch, 260f * pitch, 520f * pitch }, new[] { 0.15f, 0.12f, 0.06f },
                                  new[] { 1f, 0.7f, 0.3f }, 0.9f * g, 0.25f);
                    break;
                case FootstepSurface.Metal:
                    BankKit.NoiseBurst(o, t, ref c, 0.0005f, 0.006f, 0.05f, BiquadKind.BandPass, 3000f * pitch, 1.5f, 0.6f * g);
                    BankKit.Modal(o, t, ref c, new[] { 400f * pitch, 1130f * pitch, 1870f * pitch, 2900f * pitch }, new[] { 0.35f, 0.3f, 0.22f, 0.15f },
                                  new[] { 0.8f, 0.6f, 0.45f, 0.3f }, 0.5f * g, 0.4f, 0.002f);
                    break;
                case FootstepSurface.Water:
                {
                    BankKit.NoiseBurst(o, t, ref c, 0.002f, 0.05f, 0.15f, BiquadKind.LowPass, 3000f, 0.7f, 0.8f * g);
                    int bubbles = 3 + c.Rng.Index(3);
                    for (int i = 0; i < bubbles; i++)
                    {
                        float f = c.Range(600f, 2000f);
                        BankKit.Tone(o, t + c.Range(0.01f, 0.12f), ref c, f, f * c.Range(1.1f, 1.2f), c.Range(0.02f, 0.04f), 0.002f, 0.01f,
                                     0.3f * g, Wave.Sine);
                    }
                    break;
                }
                default: // Barefoot: soft pat, low-passed.
                    BankKit.NoiseBurst(o, t, ref c, 0.002f, 0.015f, 0.06f, BiquadKind.LowPass, 2000f, 0.7f, 0.8f * g);
                    BankKit.Tone(o, t, ref c, 100f * pitch, 80f, 0.05f, 0.003f, 0.03f, 0.35f * g, Wave.Sine, decayTauS: 0.015f);
                    break;
            }
        }

        private static void SquelchLift(float[] o, float t, ref BankContext c)
        {
            int at = BankKit.At(c, t);
            int n = c.Samples(0.08f);
            var f = default(Biquad);
            for (int i = 0; i < n; i++)
            {
                int k = at + i;
                if (k >= o.Length) break;
                float u = (float)i / n;
                if ((i & 15) == 0) f.Set(BiquadKind.BandPass, Dsp.Lerp(300f, 900f, u), 4f, c.Rate, 0f, 0.002f);
                o[k] += f.Process(c.Rng.White()) * Dsp.Hann(u) * 1.2f;
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Vehicle one-shots (A §2.3–2.6).
        // ---------------------------------------------------------------------------------------------------

        private static float[] RenderOneShot(BankSound s, ref BankContext c)
        {
            switch (s)
            {
                case BankSound.HornMoto:
                case BankSound.HornScooter:
                case BankSound.HornCar:
                case BankSound.HornBus:
                case BankSound.HornTempo:
                    return Horn(s, ref c);
                case BankSound.BicycleBell: return CycleBell(ref c);
                case BankSound.RimSqueal: return RimSqueal(ref c);
                case BankSound.AirBrake: return AirBrake(ref c, false);
                case BankSound.ParkingBrake: return AirBrake(ref c, true);
                case BankSound.ReverseAlarm: return ReverseAlarm(ref c);
                case BankSound.Indicator: return Indicator(ref c);
                case BankSound.DoorHiss: return DoorHiss(ref c);
                case BankSound.ConductorSlap: return ConductorSlap(ref c);
                case BankSound.GearClunk: return GearClunk(ref c);
                case BankSound.KeyStart: return KeyStart(ref c);
                case BankSound.GearGrind: return GearGrind(ref c);
                case BankSound.ShrineBellSmall: return ShrineBell(ref c, 700f, 900f, 3f);
                case BankSound.ShrineBellMedium: return ShrineBell(ref c, 450f, 650f, 4f);
                case BankSound.ShrineBellLarge: return ShrineBell(ref c, 300f, 420f, 5f);
                case BankSound.HandBell: return HandBell(ref c);
                case BankSound.PrayerWheel: return PrayerWheel(ref c);
                case BankSound.Conch: return Conch(ref c);
                case BankSound.Cymbal: return Cymbal(ref c);
                case BankSound.GreatBell: return GreatBell(ref c);
                case BankSound.Shutter: return Shutter(ref c);
                case BankSound.PressureCooker: return PressureCooker(ref c);
                case BankSound.TeaClink: return TeaClink(ref c);
                case BankSound.UiBoing: return UiBoing(ref c);
                case BankSound.JumpWhoosh: return Whoosh(ref c);
                case BankSound.HelmetPop: return HelmetPop(ref c);
                case BankSound.ClothRustle: return ClothRustle(ref c);
                case BankSound.Thunder: return Thunder(ref c);
                default: return RenderNature(s, ref c);
            }
        }

        private static float[] Horn(BankSound s, ref BankContext c)
        {
            int v = c.Variant;
            // Variant 0 short tap, 1 tap, 2 double tap, 3 long blast (A §2.6: friendly taps, post-2017 rule).
            float tap = v == 0 ? c.Range(0.12f, 0.16f) : v == 1 ? c.Range(0.2f, 0.28f) : v == 2 ? c.Range(0.1f, 0.14f) : c.Range(0.5f, 0.7f);
            float gap = c.Range(0.07f, 0.1f);
            float total = v == 2 ? 2f * tap + gap + 0.05f : tap + 0.06f;
            var o = new float[c.Samples(total)];
            int blasts = v == 2 ? 2 : 1;
            for (int b = 0; b < blasts; b++) HornBlast(o, b * (tap + gap), ref c, s, tap, v == 3);
            // Band-limit to the horn band 300 Hz–3 kHz and soft clip.
            var hp = default(Biquad);
            hp.Set(BiquadKind.HighPass, 300f, 0.7f, c.Rate);
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 3000f, 0.7f, c.Rate);
            for (int i = 0; i < o.Length; i++) o[i] = Dsp.SoftClip(lp.Process(hp.Process(o[i])) * 1.8f);
            Dsp.Normalize(o, 0, o.Length, 0.9f);
            return o;
        }

        private static void HornBlast(float[] o, float t, ref BankContext c, BankSound s, float len, bool longBlast)
        {
            float sag = longBlast ? 0.98f : 1f; // 2% battery droop on long blasts
            switch (s)
            {
                case BankSound.HornMoto:
                {
                    float f = c.Range(400f, 450f);
                    BankKit.Tone(o, t, ref c, f * 0.98f, f * sag, len, 0.04f, 0.04f, 0.6f, Wave.Square);
                    break;
                }
                case BankSound.HornScooter:
                {
                    float f = c.Range(480f, 520f);
                    BankKit.Tone(o, t, ref c, f * 0.98f, f * sag, len, 0.03f, 0.04f, 0.5f, Wave.HornMix);
                    break;
                }
                case BankSound.HornCar:
                {
                    // Two disc horns about a major third apart (≈ 420 + 510 Hz), detuned 0.3–1%.
                    float d = 1f + c.Range(0.003f, 0.01f);
                    BankKit.Tone(o, t, ref c, 420f, 420f * sag, len, 0.025f, 0.05f, 0.45f, Wave.HornMix);
                    BankKit.Tone(o, t, ref c, 510f * d, 510f * d * sag, len, 0.025f, 0.05f, 0.4f, Wave.HornMix);
                    break;
                }
                case BankSound.HornBus:
                    BankKit.Tone(o, t, ref c, 290f, 290f * sag, len, 0.03f, 0.06f, 0.4f, Wave.HornMix);
                    BankKit.Tone(o, t, ref c, 345f, 345f * sag, len, 0.03f, 0.06f, 0.35f, Wave.HornMix);
                    BankKit.Tone(o, t, ref c, 435f, 435f * sag, len, 0.03f, 0.06f, 0.3f, Wave.HornMix);
                    break;
                default: // Tempo: small electric beep, two short beeps.
                {
                    float f = c.Range(600f, 700f);
                    float b = Math.Min(len, 0.09f);
                    BankKit.Tone(o, t, ref c, f, f, b, 0.005f, 0.01f, 0.5f, Wave.Sine, new[] { 0.2f, 0.1f });
                    BankKit.Tone(o, t + b + 0.06f, ref c, f, f, b, 0.005f, 0.01f, 0.5f, Wave.Sine, new[] { 0.2f, 0.1f });
                    break;
                }
            }
        }

        private static float[] CycleBell(ref BankContext c)
        {
            // Thumb bell "ding-ding": partials 1.00 / 2.76 / 5.40 / 8.93 × f0, f0 2.3–2.8 kHz, double strike 70–90 ms.
            float f0 = c.Range(2300f, 2800f);
            float t60 = c.Range(0.4f, 0.7f);
            var o = new float[c.Samples(0.1f + t60)];
            float[] fr = { f0, 2.76f * f0, 5.4f * f0, 8.93f * f0 };
            float[] tt = { t60, t60 * 0.7f, t60 * 0.5f, t60 * 0.35f };
            float[] am = { 1f, 0.5f, 0.3f, 0.15f };
            BankKit.Modal(o, 0f, ref c, fr, tt, am, 1f, 0.1f + t60);
            if (c.Variant != 3) BankKit.Modal(o, c.Range(0.07f, 0.09f), ref c, fr, tt, am, 0.85f, t60);
            Dsp.Normalize(o, 0, o.Length, 0.8f);
            return o;
        }

        private static float[] RimSqueal(ref BankContext c)
        {
            float len = c.Range(0.2f, 0.6f);
            var o = new float[c.Samples(len)];
            float f = c.Range(2000f, 4000f);
            BankKit.Tone(o, 0f, ref c, f, f * 0.97f, len, 0.03f, 0.08f, 0.5f, Wave.Sine, new[] { 0.2f }, 6f, 0.01f);
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] AirBrake(ref BankContext c, bool parking)
        {
            // White noise, HP 1 kHz, peak 4 kHz +6 dB, attack 5 ms, decay τ 0.15–0.4 s (parking: longer, louder, falling).
            float tau = parking ? c.Range(0.35f, 0.5f) : c.Range(0.15f, 0.4f);
            float len = parking ? c.Range(0.8f, 1.4f) : tau * 4f;
            var o = new float[c.Samples(len)];
            var hp = default(Biquad);
            hp.Set(BiquadKind.HighPass, 1000f, 0.7f, c.Rate);
            var pk = default(Biquad);
            pk.Set(BiquadKind.Peak, 4000f, 1.2f, c.Rate, 6f);
            int atk = Math.Max(1, (int)(0.005f * c.Rate));
            float dec = (float)Math.Exp(-1.0 / (tau * c.Rate));
            float env = 1f;
            for (int i = 0; i < o.Length; i++)
            {
                if (parking && (i & 63) == 0)
                    pk.Set(BiquadKind.Peak, Dsp.Lerp(4500f, 3000f, (float)i / o.Length), 1.2f, c.Rate, 6f, 0.004f);
                float a = i < atk ? (float)i / atk : 1f;
                o[i] = pk.Process(hp.Process(c.Rng.White())) * a * env;
                if (i >= atk) env *= dec;
            }
            BankKit.FadeOut(o, c, 0.05f);
            Dsp.Normalize(o, 0, o.Length, parking ? 0.9f : 0.75f);
            return o;
        }

        private static float[] ReverseAlarm(ref BankContext c)
        {
            // 1.0–1.4 kHz, 0.5 s on / 0.5 s off; one period, looped.
            var o = new float[c.Samples(1f)];
            float f = 1200f;
            BankKit.Tone(o, 0f, ref c, f, f, 0.5f, 0.01f, 0.02f, 0.5f, Wave.Square);
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 3000f, 0.7f, c.Rate);
            for (int i = 0; i < o.Length; i++) o[i] = lp.Process(o[i]);
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] Indicator(ref BankContext c)
        {
            // 1.5 Hz tick-tock: two 3 ms clicks at 1.2 kHz per period.
            var o = new float[c.Samples(1f / 1.5f)];
            BankKit.NoiseBurst(o, 0f, ref c, 0.0003f, 0.0015f, 0.006f, BiquadKind.BandPass, 1200f, 3f, 1f);
            BankKit.Tone(o, 0f, ref c, 1200f, 1200f, 0.004f, 0.0005f, 0.002f, 0.4f, Wave.Sine);
            BankKit.NoiseBurst(o, 1f / 3f, ref c, 0.0003f, 0.0015f, 0.006f, BiquadKind.BandPass, 1000f, 3f, 0.8f);
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] DoorHiss(ref BankContext c)
        {
            var o = new float[c.Samples(0.8f)];
            BankKit.NoiseBurst(o, 0f, ref c, 0.02f, 0.15f, 0.55f, BiquadKind.HighPass, 1500f, 0.7f, 0.7f);
            BankKit.Tone(o, c.Range(0.45f, 0.55f), ref c, 110f, 80f, 0.12f, 0.003f, 0.08f, 0.8f, Wave.Sine, new[] { 0.4f }, decayTauS: 0.03f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] ConductorSlap(ref BankContext c)
        {
            // Two flat palm slaps on the bus side, 150–250 ms apart [V].
            float gap = c.Range(0.15f, 0.25f);
            var o = new float[c.Samples(gap + 0.2f)];
            for (int i = 0; i < 2; i++)
            {
                float t = i * gap;
                BankKit.NoiseBurst(o, t, ref c, 0.0005f, 0.006f, 0.04f, BiquadKind.BandPass, 1200f, 0.9f, 1f);
                BankKit.Modal(o, t, ref c, new[] { 180f, 310f, 470f }, new[] { 0.12f, 0.09f, 0.06f }, new[] { 1f, 0.6f, 0.3f }, 0.8f, 0.18f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.8f);
            return o;
        }

        private static float[] GearClunk(ref BankContext c)
        {
            var o = new float[c.Samples(0.2f)];
            BankKit.Tone(o, 0f, ref c, 90f, 70f, 0.12f, 0.002f, 0.08f, 0.8f, Wave.Sine, new[] { 0.5f, 0.3f }, decayTauS: 0.03f);
            BankKit.NoiseBurst(o, 0f, ref c, 0.0005f, 0.004f, 0.03f, BiquadKind.BandPass, 2500f, 1.5f, 0.5f);
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] KeyStart(ref BankContext c)
        {
            // Cranking 4–6 Hz for 0.8–1.2 s, then the engine catches.
            float crank = c.Range(0.8f, 1.2f);
            float rate = c.Range(4f, 6f);
            var o = new float[c.Samples(crank + 0.5f)];
            BankKit.Tone(o, 0f, ref c, 240f, 260f, crank, 0.02f, 0.05f, 0.15f, Wave.Saw);
            for (float t = 0f; t < crank; t += 1f / rate)
                BankKit.Tone(o, t, ref c, 70f, 55f, 0.12f, 0.005f, 0.06f, 0.6f, Wave.Sine, new[] { 0.6f, 0.4f, 0.2f }, decayTauS: 0.05f);
            for (int i = 0; i < 8; i++)
                BankKit.Tone(o, crank + i * 0.045f, ref c, 90f, 80f, 0.08f, 0.003f, 0.05f, 0.8f, Wave.Sine, new[] { 0.7f, 0.5f, 0.3f }, decayTauS: 0.04f);
            BankKit.FadeOut(o, c, 0.15f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] GearGrind(ref BankContext c)
        {
            // Comic gear grind (manifest asks for it on jeeps): band noise 1–3 kHz with 30 Hz AM, 0.3 s.
            var o = new float[c.Samples(0.32f)];
            var bp = default(Biquad);
            bp.Set(BiquadKind.BandPass, 1800f, 0.9f, c.Rate);
            for (int i = 0; i < o.Length; i++)
            {
                float u = (float)i / o.Length;
                float am = 0.5f + 0.5f * Dsp.Sin(30.0 * i / c.Rate);
                o[i] = bp.Process(c.Rng.White()) * am * Dsp.Hann(Math.Min(1f, u * 0.5f + 0.25f));
            }
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        // ---------------------------------------------------------------------------------------------------
        // Sacred sounds (A §2.8). Instrumental only; never chant.
        // ---------------------------------------------------------------------------------------------------

        private static float[] ShrineBell(ref BankContext c, float f0Lo, float f0Hi, float len)
        {
            // Hung bell partials: hum 0.5, prime 1, tierce 1.19, quint 1.5, nominal 2, 2.5, 2.66, 3, 4 × f0, doublets
            // ±0.15% for beating, plus a light FM shimmer layer.
            float f0 = c.Range(f0Lo, f0Hi) * (1f + c.Range(-0.015f, 0.015f));
            float hum = Math.Min(len * 1.6f, c.Range(6f, 10f) * (300f / f0));
            float[] r = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.5f, 2.66f, 3f, 4f };
            float[] a = { 0.5f, 0.8f, 0.6f, 0.35f, 1f, 0.4f, 0.35f, 0.25f, 0.2f };
            float[] k = { 1f, 0.8f, 0.6f, 0.5f, 0.45f, 0.18f, 0.16f, 0.14f, 0.12f };
            var fr = new float[r.Length];
            var tt = new float[r.Length];
            var am = new float[r.Length];
            for (int i = 0; i < r.Length; i++)
            {
                fr[i] = f0 * r[i];
                tt[i] = Math.Max(0.4f, hum * k[i]);
                am[i] = a[i] * Dsp.DbToGain(c.Range(-2f, 2f));
            }
            var o = new float[c.Samples(len)];
            BankKit.Modal(o, 0f, ref c, fr, tt, am, 1f, len, 0.0015f);
            BankKit.FmBell(o, 0f, ref c, f0 * 2f, 1.4f, 3f, 0.3f, len * 0.5f, len, 0.08f);
            BankKit.FadeOut(o, c, 0.4f);
            Dsp.Normalize(o, 0, o.Length, 0.85f);
            return o;
        }

        private static float[] HandBell(ref BankContext c)
        {
            // Ghanti: partials 1.00 / 2.32 / 4.25 / 6.63 × f0 (1.8–3.2 kHz), clapper 4–8 Hz, T60 0.6–1.2 s.
            float f0 = c.Range(1800f, 3200f);
            float t60 = c.Range(0.6f, 1.2f);
            float rate = c.Range(4f, 8f);
            float ring = 1.4f;
            var o = new float[c.Samples(ring + t60)];
            float[] fr = { f0, 2.32f * f0, 4.25f * f0, 6.63f * f0 };
            float[] tt = { t60, t60 * 0.6f, t60 * 0.4f, t60 * 0.3f };
            float[] am = { 1f, 0.5f, 0.3f, 0.15f };
            for (float t = 0f; t < ring; t += 1f / rate)
                BankKit.Modal(o, t, ref c, fr, tt, am, c.Range(0.6f, 1f), t60 + 0.1f, 0.001f);
            BankKit.FadeOut(o, c, 0.2f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] PrayerWheel(ref BankContext c)
        {
            // Axle creak (friction squeak 400–900 Hz, 0.2–0.4 s) and the striker's bell once per revolution.
            var o = new float[c.Samples(1.6f)];
            float creak = c.Range(0.2f, 0.4f);
            float f = c.Range(400f, 900f);
            BankKit.Tone(o, 0f, ref c, f, f * c.Range(1.05f, 1.2f), creak, 0.03f, 0.05f, 0.25f, Wave.Saw, null, 23f, 0.04f);
            float bf = c.Range(1400f, 2200f);
            BankKit.Modal(o, creak + 0.05f, ref c, new[] { bf, 2.32f * bf, 4.25f * bf }, new[] { 1.0f, 0.6f, 0.4f }, new[] { 1f, 0.45f, 0.25f },
                          0.8f, 1.2f, 0.001f);
            BankKit.FadeOut(o, c, 0.2f);
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] Conch(ref BankContext c)
        {
            // Shankha: f0 250–450 Hz, strong odd harmonics, formant 0.8–1.2 kHz, vibrato 4–5 Hz ±0.5%, 2–4 s,
            // pitch drops 5–10% at the end; breath noise HP 2 kHz at −18 dB.
            float len = c.Range(2.2f, 3.2f);
            float f0 = c.Range(250f, 450f);
            var o = new float[c.Samples(len + 0.2f)];
            int n = c.Samples(len);
            var formant = default(Biquad);
            formant.Set(BiquadKind.Peak, c.Range(800f, 1200f), 2.5f, c.Rate, 9f);
            var breath = default(Biquad);
            breath.Set(BiquadKind.HighPass, 2000f, 0.7f, c.Rate);
            double ph = 0, vib = 0;
            float drop = c.Range(0.05f, 0.1f);
            float invSr = 1f / c.Rate;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float bend = u > 0.8f ? 1f - drop * (u - 0.8f) / 0.2f : 1f;
                vib += 4.5f * invSr;
                float f = f0 * bend * (1f + 0.005f * Dsp.Sin(vib));
                ph += f * invSr;
                if (ph >= 1.0) ph -= Math.Floor(ph);
                float p = (float)ph;
                float x3 = p * 3f; x3 -= (int)x3;
                float x5 = p * 5f; x5 -= (int)x5;
                float x7 = p * 7f; x7 -= (int)x7;
                float tone = Dsp.Sin01(p) + 0.5f * Dsp.Sin01(x3) + 0.3f * Dsp.Sin01(x5) + 0.15f * Dsp.Sin01(x7);
                float env = Math.Min(1f, u / 0.08f) * Math.Min(1f, (1f - u) / 0.12f);
                o[i] = (formant.Process(tone) * 0.35f + breath.Process(c.Rng.White()) * 0.125f) * env;
            }
            Dsp.Normalize(o, 0, o.Length, 0.8f);
            return o;
        }

        private static float[] Cymbal(ref BankContext c)
        {
            // Jhyali: noise into 6–10 resonances 3–9 kHz, T60 0.3–1.0 s.
            float t60 = c.Range(0.3f, 1.0f);
            int modes = 6 + c.Rng.Index(5);
            var fr = new float[modes];
            var tt = new float[modes];
            var am = new float[modes];
            for (int i = 0; i < modes; i++)
            {
                fr[i] = c.Range(3000f, 9000f);
                tt[i] = t60 * c.Range(0.6f, 1f);
                am[i] = c.Range(0.4f, 1f);
            }
            var o = new float[c.Samples(t60 + 0.1f)];
            BankKit.Modal(o, 0f, ref c, fr, tt, am, 1f, t60 + 0.1f, 0f, 0.002f);
            BankKit.NoiseBurst(o, 0f, ref c, 0.0005f, 0.03f, 0.15f, BiquadKind.HighPass, 5000f, 0.7f, 0.3f);
            BankKit.FadeOut(o, c, 0.08f);
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] GreatBell(ref BankContext c)
        {
            // Taleju bells (Kathmandu, Patan, Bhaktapur by variant): f0 150–300 Hz, long hum; baked at 16 kHz in S1
            // (real-time modal voices in S2). FM body + modal hum and partials.
            float f0 = c.Variant == 0 ? 170f : c.Variant == 1 ? 215f : 260f;
            float len = 8f;
            var o = new float[c.Samples(len)];
            float[] r = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.66f, 3f };
            float[] a = { 0.7f, 0.9f, 0.6f, 0.35f, 0.8f, 0.3f, 0.2f };
            float[] k = { 16f, 12f, 9f, 7f, 6f, 3f, 2f };
            var fr = new float[r.Length];
            for (int i = 0; i < r.Length; i++) fr[i] = f0 * r[i];
            BankKit.Modal(o, 0f, ref c, fr, k, a, 1f, len, 0.0015f, 0.006f);
            BankKit.FmBell(o, 0f, ref c, f0 * 2f, 1.4f, 6f, 0.5f, 8f, len, 0.15f);
            BankKit.FadeOut(o, c, 1.5f);
            Dsp.Normalize(o, 0, o.Length, 0.9f);
            return o;
        }

        // ---------------------------------------------------------------------------------------------------
        // Street, foley, UI, weather.
        // ---------------------------------------------------------------------------------------------------

        private static float[] Shutter(ref BankContext c)
        {
            // Rolling steel shutter: 25–40 impulses/s for 1.2–2.0 s through a 180 Hz comb, then a clang.
            float len = c.Range(1.2f, 2.0f);
            float rate = c.Range(25f, 40f);
            var o = new float[c.Samples(len + 0.6f)];
            var comb = new DelayLine(c.Rate / 50);
            float delay = 0.0055f * c.Rate;
            int n = c.Samples(len);
            var bp = default(Biquad);
            bp.Set(BiquadKind.BandPass, 2500f, 0.8f, c.Rate);
            double ph = 0;
            float env = 0f;
            for (int i = 0; i < n; i++)
            {
                ph += rate / c.Rate;
                if (ph >= 1.0)
                {
                    ph -= 1.0;
                    env = c.Range(0.6f, 1f);
                }
                float x = bp.Process(c.Rng.White()) * env;
                env *= 0.993f;
                float y = x + comb.Read(delay) * 0.6f;
                comb.Write(y);
                float u = (float)i / n;
                o[i] = y * (0.6f + 0.4f * u);
            }
            BankKit.Modal(o, len, ref c, new[] { 310f, 740f, 1290f, 2050f }, new[] { 0.5f, 0.4f, 0.3f, 0.2f }, new[] { 1f, 0.7f, 0.5f, 0.3f }, 1.5f, 0.6f);
            BankKit.FadeOut(o, c, 0.1f);
            Dsp.Normalize(o, 0, o.Length, 0.7f);
            return o;
        }

        private static float[] PressureCooker(ref BankContext c)
        {
            // Steam whistle 2–3 kHz with a noisy onset, 1–3 s.
            float len = c.Range(1.2f, 2.4f);
            float f = c.Range(2000f, 3000f);
            var o = new float[c.Samples(len)];
            BankKit.NoiseBurst(o, 0f, ref c, 0.1f, 0.4f, len, BiquadKind.BandPass, f, 6f, 0.9f);
            BankKit.Tone(o, 0.15f, ref c, f * 0.97f, f, len - 0.15f, 0.25f, 0.2f, 0.25f, Wave.Sine, null, 7f, 0.004f);
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] TeaClink(ref BankContext c)
        {
            var o = new float[c.Samples(0.4f)];
            float f = c.Range(2300f, 3200f);
            BankKit.Modal(o, 0f, ref c, new[] { f, f * 2.3f, f * 3.9f }, new[] { 0.3f, 0.2f, 0.12f }, new[] { 1f, 0.5f, 0.3f }, 0.8f, 0.4f, 0.002f);
            if (c.Variant % 2 == 1)
                BankKit.Modal(o, c.Range(0.08f, 0.14f), ref c, new[] { f * 1.1f, f * 2.5f }, new[] { 0.25f, 0.15f }, new[] { 1f, 0.4f }, 0.5f, 0.25f);
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] UiBoing(ref BankContext c)
        {
            // Squash "boing": FM 220 → 440 Hz.
            var o = new float[c.Samples(0.35f)];
            float start = c.Variant == 0 ? 220f : 260f;
            int n = o.Length;
            double pc = 0, pm = 0;
            float invSr = 1f / c.Rate;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float f = start * (float)Math.Pow(2.0, u);
                pm += f * 2f * invSr;
                pc += f * invSr;
                float idx = 2.5f * (1f - u);
                float y = Dsp.Sin(pc + idx * Dsp.Sin(pm) / Dsp.TwoPi);
                o[i] = y * (float)Math.Exp(-4.0 * u) * Math.Min(1f, u * 80f);
            }
            Dsp.Normalize(o, 0, o.Length, 0.6f);
            return o;
        }

        private static float[] Whoosh(ref BankContext c)
        {
            float len = c.Range(0.12f, 0.22f);
            var o = new float[c.Samples(len)];
            var bp = default(Biquad);
            for (int i = 0; i < o.Length; i++)
            {
                float u = (float)i / o.Length;
                if ((i & 15) == 0) bp.Set(BiquadKind.BandPass, Dsp.Lerp(300f, 900f, u) * 2f, 1.2f, c.Rate, 0f, 0.002f);
                o[i] = Dsp.SoftClip(bp.Process(c.Rng.White()) * 1.5f) * Dsp.Hann(u);
            }
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] HelmetPop(ref BankContext c)
        {
            var o = new float[c.Samples(0.25f)];
            BankKit.NoiseBurst(o, 0f, ref c, 0.0005f, 0.004f, 0.02f, BiquadKind.BandPass, 1500f, 1f, 1f);
            BankKit.Tone(o, 0.005f, ref c, 500f, 900f, 0.12f, 0.003f, 0.06f, 0.5f, Wave.Sine, decayTauS: 0.05f);
            Dsp.Normalize(o, 0, o.Length, 0.5f);
            return o;
        }

        private static float[] ClothRustle(ref BankContext c)
        {
            float len = c.Range(0.2f, 0.35f);
            var o = new float[c.Samples(len)];
            var bp = default(Biquad);
            bp.Set(BiquadKind.BandPass, c.Range(2500f, 3500f), 0.6f, c.Rate);
            float g = 0f;
            for (int i = 0; i < o.Length; i++)
            {
                if ((i & 255) == 0) g = c.Range(0.3f, 1f);
                float u = (float)i / o.Length;
                o[i] = bp.Process(c.Rng.White()) * g * Dsp.Hann(u);
            }
            Dsp.Normalize(o, 0, o.Length, 0.35f);
            return o;
        }

        private static float[] Thunder(ref BankContext c)
        {
            // Distant thunder: low-passed noise 40–300 Hz, 3–8 s, 2–5 rumble peaks.
            float len = c.Range(3.5f, 6f);
            var o = new float[c.Samples(len)];
            var lp = default(Biquad);
            lp.Set(BiquadKind.LowPass, 300f, 0.7f, c.Rate);
            var hp = default(Biquad);
            hp.Set(BiquadKind.HighPass, 40f, 0.7f, c.Rate);
            int peaks = 2 + c.Rng.Index(4);
            var pt = new float[peaks];
            var pa = new float[peaks];
            for (int p = 0; p < peaks; p++)
            {
                pt[p] = c.Range(0.05f, 0.7f) * len;
                pa[p] = c.Range(0.4f, 1f);
            }
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / c.Rate;
                float env = 0f;
                for (int p = 0; p < peaks; p++)
                {
                    float d = t - pt[p];
                    if (d < -0.15f) continue;
                    env += pa[p] * (d < 0f ? (d + 0.15f) / 0.15f : (float)Math.Exp(-d / 0.9f));
                }
                o[i] = hp.Process(lp.Process(c.Rng.Brown())) * env;
            }
            BankKit.FadeOut(o, c, 0.5f);
            Dsp.Normalize(o, 0, o.Length, 0.85f);
            return o;
        }
    }
}
