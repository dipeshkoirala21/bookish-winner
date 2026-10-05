using System;
using System.Diagnostics;
using Ghumante.Core.Synth;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Engine, bicycle and aircraft voices (W2_DESIGN 7.1, 7.3, 8.6; V9 audio golden hashes).</summary>
    public class SynthEngineTests
    {
        private const int Sr = 48000;

        private static EngineModel[] AllModels()
        {
            var list = new System.Collections.Generic.List<EngineModel>();
            foreach (EngineModel m in Enum.GetValues(typeof(EngineModel)))
                if (m != EngineModel.None) list.Add(m);
            return list.ToArray();
        }

        /// <summary>A 2 s drive: idle, rev up under load, cruise on brick, overrun on stone, with Doppler and air LPF.</summary>
        private static float[] Drive(EngineModel m, uint seed, int sr = Sr)
        {
            EnginePreset p = EnginePresets.Get(m);
            var v = new EngineVoice(p, seed);
            var dt = new Drivetrain(p);
            int block = 512;
            int total = sr * 2;
            var outBuf = new float[total];
            var buf = new float[block];
            EngineVoiceParams prev = EngineVoiceParams.Idle(p);
            float speed = 0f;
            for (int at = 0; at < total; at += block)
            {
                float t = (float)at / sr;
                float throttle = t < 0.3f ? 0f : t < 1.3f ? 1f : 0f;
                float brake = t > 1.6f ? 0.6f : 0f;
                speed = Math.Max(0f, speed + (throttle * 4f - brake * 5f - 0.3f) * block / sr);
                dt.Step((float)block / sr, speed, throttle, brake);
                var next = new EngineVoiceParams
                {
                    Rpm = dt.Rpm, Load = dt.Load, SpeedMps = speed, Gain = 1f,
                    DopplerPitch = t < 1f ? 1.05f : 0.95f,
                    AirCutoffHz = t > 1.5f ? 4500f : 0f,
                    Surface = (byte)(t < 1f ? FootstepSurface.Asphalt : t < 1.5f ? FootstepSurface.Brick : FootstepSurface.Stone),
                };
                v.Render(buf, block, sr, prev, next);
                Array.Copy(buf, 0, outBuf, at, Math.Min(block, total - at));
                prev = next;
            }
            return outBuf;
        }

        [Test]
        public void EveryModelIsBoundedFiniteAndAudible()
        {
            foreach (EngineModel m in AllModels())
            {
                float[] o = Drive(m, 42);
                SynthDspTests.AssertClean(o, o.Length, m.ToString());
                double rms = SynthDspTests.RmsOf(o, Sr / 2, Sr);
                Assert.That(rms, Is.GreaterThan(1e-3), m + " is silent while moving");
                Assert.That(rms, Is.LessThan(0.7), m + " is far too loud");
            }
        }

        [Test]
        public void SameSeedSameSoundDifferentSeedDifferentSound()
        {
            foreach (EngineModel m in AllModels())
            {
                float[] a = Drive(m, 7), b = Drive(m, 7), c = Drive(m, 8);
                Assert.That(SynthDspTests.Pcm16Hash(a, a.Length), Is.EqualTo(SynthDspTests.Pcm16Hash(b, b.Length)), m + " not deterministic");
                Assert.That(SynthDspTests.Pcm16Hash(a, a.Length), Is.Not.EqualTo(SynthDspTests.Pcm16Hash(c, c.Length)), m + " ignores its seed");
            }
        }

        [Test]
        public void RenderIsBlockSizeIndependentInLength()
        {
            // A voice must accept any buffer size (Unity's DSP buffer is 256–1,024 frames, sometimes odd).
            EnginePreset p = EnginePresets.Get(EngineModel.Car3Cyl);
            var v = new EngineVoice(p, 1);
            var prm = EngineVoiceParams.Idle(p);
            foreach (int n in new[] { 1, 31, 32, 33, 257, 1024, 4096 })
            {
                var buf = new float[n];
                v.Render(buf, n, Sr, prm, prm);
                SynthDspTests.AssertClean(buf, n, "frames " + n);
            }
            v.Render(new float[8], 100, Sr, prm, prm); // frames > buffer is clamped, never throws
            v.Render(null, 100, Sr, prm, prm);
        }

        [Test]
        public void GarbageParametersNeverProduceNaN()
        {
            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1e9f, 1e9f, 0f };
            foreach (EngineModel m in AllModels())
            {
                EnginePreset p = EnginePresets.Get(m);
                var v = new EngineVoice(p, 3);
                var buf = new float[300];
                foreach (float x in bad)
                {
                    var prm = new EngineVoiceParams
                    {
                        Rpm = x, Load = x, SpeedMps = x, Gain = x, DopplerPitch = x, AirCutoffHz = x, Wetness = x, Surface = 250,
                    };
                    v.Render(buf, buf.Length, Sr, prm, prm);
                    SynthDspTests.AssertClean(buf, buf.Length, m + " with " + x);
                }
                // And it recovers to normal output.
                var ok = new EngineVoiceParams { Rpm = p.IdleRpm * 2f, Load = 0.5f, SpeedMps = 5f, Gain = 1f, DopplerPitch = 1f };
                v.Render(buf, buf.Length, 22050, ok, ok);
                SynthDspTests.AssertClean(buf, buf.Length, m + " after garbage");
            }
            Assert.DoesNotThrow(() => new EngineVoice(EnginePresets.Get((EngineModel)200), 1).Render(new float[64], 64, 0, default, default));
        }

        [Test]
        public void FiringFrequencyFollowsRpmAndDoppler()
        {
            // car_4cyl 4-stroke at 3,000 rpm fires at 3000 / 60 × 4 / 2 = 100 Hz.
            EnginePreset p = EnginePresets.Get(EngineModel.Car4Cyl);
            Assert.That(p.FiringHz(3000f), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(EnginePresets.Get(EngineModel.MotoCommuter).FiringHz(8500f), Is.EqualTo(70.83f).Within(0.01f));
            Assert.That(EnginePresets.Get(EngineModel.Moto2Stroke).FiringHz(1500f), Is.EqualTo(25f).Within(1e-3f));
            Assert.That(EnginePresets.Get(EngineModel.BusCity).FiringHz(2600f), Is.EqualTo(130f).Within(1e-3f));

            float[] Tone(float dop)
            {
                var v = new EngineVoice(p, 5);
                var prm = new EngineVoiceParams { Rpm = 3000f, Load = 0.6f, Gain = 1f, DopplerPitch = dop };
                var buf = new float[Sr];
                v.Render(buf, buf.Length, Sr, prm, prm);
                return buf;
            }
            float[] a = Tone(1f);
            Assert.That(SynthDspTests.Goertzel(a, 4800, 24000, 200f, Sr), Is.GreaterThan(3 * SynthDspTests.Goertzel(a, 4800, 24000, 170f, Sr)));
            float[] b = Tone(1.2f);
            Assert.That(SynthDspTests.Goertzel(b, 4800, 24000, 240f, Sr), Is.GreaterThan(3 * SynthDspTests.Goertzel(b, 4800, 24000, 200f, Sr)));
        }

        [Test]
        public void LoadMakesTheEngineBrighterAndLouder()
        {
            EnginePreset p = EnginePresets.Get(EngineModel.MotoCommuter);
            double Rms(float load)
            {
                var v = new EngineVoice(p, 9);
                var prm = new EngineVoiceParams { Rpm = 5000f, Load = load, Gain = 1f, DopplerPitch = 1f };
                var buf = new float[Sr / 2];
                v.Render(buf, buf.Length, Sr, prm, prm);
                return SynthDspTests.RmsOf(buf, 2400, buf.Length - 2400);
            }
            Assert.That(Rms(1f), Is.GreaterThan(Rms(-0.5f)));
        }

        [Test]
        public void ElectricWhineRisesWithSpeedAndLagsOnTheTempo()
        {
            EnginePreset p = EnginePresets.Get(EngineModel.TempoSafa);
            Assert.That(p.WhineMinHz, Is.EqualTo(200f));
            Assert.That(p.WhineMaxHz, Is.EqualTo(1500f));
            var v = new EngineVoice(p, 2);
            var slow = new EngineVoiceParams { SpeedMps = 2f, Load = 0.5f, Gain = 1f, DopplerPitch = 1f };
            var fast = new EngineVoiceParams { SpeedMps = 12.5f, Load = 0.5f, Gain = 1f, DopplerPitch = 1f };
            var buf = new float[Sr * 2];
            v.Render(buf, buf.Length, Sr, fast, fast);
            // At 45 km/h (12.5 m/s) the whine settles at 1,500 Hz (after the 300 ms lag).
            Assert.That(PeakHz(buf, Sr * 3 / 2, Sr / 2, 1000f, 2000f), Is.InRange(1470f, 1530f));
            v.Reset(p, 2);
            var b2 = new float[Sr * 2];
            v.Render(b2, b2.Length, Sr, slow, slow);
            float f = 200f + (1500f - 200f) * (2f / 12.5f);
            Assert.That(PeakHz(b2, Sr * 3 / 2, Sr / 2, 250f, 800f), Is.InRange(f - 15f, f + 15f));
            // The lag: 100 ms after a jump from standstill the whine is still far below its target.
            v.Reset(p, 2);
            var b3 = new float[Sr / 10 + 2400];
            v.Render(b3, b3.Length, Sr, fast, fast);
            Assert.That(PeakHz(b3, Sr / 10 - 2400, 4800, 200f, 2000f), Is.LessThan(1000f));
        }

        private static float PeakHz(float[] buf, int start, int n, float lo, float hi)
        {
            double best = -1;
            float at = lo;
            for (float hz = lo; hz <= hi; hz += 5f)
            {
                double g = SynthDspTests.Goertzel(buf, start, n, hz, Sr);
                if (g > best)
                {
                    best = g;
                    at = hz;
                }
            }
            return at;
        }

        [Test]
        public void BicycleFreewheelClicksAtThePawlRate()
        {
            // 20 pawls × (4.17 m/s / 2.1 m) ≈ 40 clicks/s at 15 km/h (A §2.3: 36–48).
            EnginePreset p = EnginePresets.Get(EngineModel.Bicycle);
            var v = new EngineVoice(p, 11);
            var coast = new EngineVoiceParams { SpeedMps = 4.17f, Load = 0f, Gain = 1f, DopplerPitch = 1f };
            var buf = new float[Sr];
            v.Render(buf, buf.Length, Sr, coast, coast);
            SynthDspTests.AssertClean(buf, buf.Length, "bicycle");
            int clicks = 0;
            bool above = false;
            float thr = 0.3f * MaxAbs(buf);
            int lastClick = -1000;
            for (int i = 0; i < buf.Length; i++)
            {
                bool now = Math.Abs(buf[i]) > thr;
                if (now && !above && i - lastClick > Sr / 200)
                {
                    clicks++;
                    lastClick = i;
                }
                above = now;
            }
            Assert.That(clicks, Is.InRange(30, 50));
        }

        [Test]
        public void DrivetrainShiftsAndStaysInRange()
        {
            EnginePreset p = EnginePresets.Get(EngineModel.MotoCommuter);
            var dt = new Drivetrain(p);
            float speed = 0f;
            float maxRpm = 0f;
            for (int i = 0; i < 600; i++)
            {
                speed = Math.Min(26f, speed + 0.05f);
                dt.Step(1f / 60f, speed, 1f, 0f);
                Assert.That(dt.Rpm, Is.InRange(p.IdleRpm * 0.85f, p.RedRpm));
                Assert.That(dt.Load, Is.InRange(-1f, 1f));
                maxRpm = Math.Max(maxRpm, dt.Rpm);
            }
            Assert.That(dt.Shifts, Is.GreaterThanOrEqualTo(3), "a full-throttle run shifts through the gears");
            Assert.That(dt.Gear, Is.EqualTo(p.Gears.Length - 1));
            Assert.That(maxRpm, Is.GreaterThan(0.8f * p.RedRpm));
            for (int i = 0; i < 60; i++) dt.Step(1f / 60f, speed, 0f, 0.5f);
            Assert.That(dt.Load, Is.LessThan(0f), "overrun");

            // CVT: rpm jumps towards the hold rpm at launch and stays near it while speed rises.
            EnginePreset sc = EnginePresets.Get(EngineModel.Scooter);
            var cvt = new Drivetrain(sc);
            for (int i = 0; i < 60; i++) cvt.Step(1f / 60f, i * 0.1f, 1f, 0f);
            Assert.That(cvt.Rpm, Is.InRange(sc.CvtHoldRpm * 0.85f, sc.CvtHoldRpm * 1.05f));

            // Electric follows speed; non-finite inputs are ignored.
            var ev = new Drivetrain(EnginePresets.Get(EngineModel.CarEv));
            ev.Step(0.1f, float.NaN, float.NaN, float.NaN);
            Assert.That(float.IsNaN(ev.Rpm), Is.False);
            var d0 = default(Drivetrain);
            d0.Step(0.016f, 5f, 0.5f, 0f);
            Assert.That(float.IsNaN(d0.Rpm), Is.False);
        }

        [Test]
        public void VehicleKindsAndTrafficClassesMapToRealPresets()
        {
            for (byte k = 0; k <= 13; k++)
            {
                EngineModel m = EnginePresets.ForVehicleKind(k, 77);
                if (k == 2) Assert.That(m, Is.EqualTo(EngineModel.None), "walker");
                else Assert.That(EnginePresets.Get(m).Model, Is.EqualTo(m));
            }
            Assert.That(EnginePresets.ForVehicleKind(13, 1), Is.EqualTo(EngineModel.MotoCruiser));
            Assert.That(EnginePresets.ForVehicleKind(8, 1), Is.EqualTo(EngineModel.TempoSafa));
            var counts = new int[EnginePresets.Count];
            for (uint s = 0; s < 10000; s++) counts[(int)EnginePresets.ForTrafficClass(0, s)]++;
            Assert.That(counts[(int)EngineModel.MotoCommuter], Is.InRange(4700, 5300));
            Assert.That(counts[(int)EngineModel.Moto2Stroke], Is.InRange(100, 300), "2-strokes stay ≤ 2-3% of bikes");
            for (byte c = 0; c <= 13; c++) Assert.That(EnginePresets.ForTrafficClass(c, 5), Is.Not.EqualTo(EngineModel.None));
            Assert.That(EnginePresets.HornFor(EngineModel.BusCity), Is.EqualTo(HornKind.Bus));
            Assert.That(EnginePresets.HornFor(EngineModel.Bicycle), Is.EqualTo(HornKind.BicycleBell));
        }

        [Test]
        public void RenderDoesNotAllocateAndFitsTheDspBudget()
        {
            EnginePreset p = EnginePresets.Get(EngineModel.BusCity);
            var v = new EngineVoice(p, 1);
            var buf = new float[1024];
            var a = new EngineVoiceParams { Rpm = 1500f, Load = 0.8f, SpeedMps = 10f, Gain = 1f, DopplerPitch = 1f, AirCutoffHz = 3000f, Surface = 2 };
            var b = a;
            b.Rpm = 1700f;
            int blocks = Sr / 1024 * 2;
            Action engine = () =>
            {
                for (int i = 0; i < blocks; i++) v.Render(buf, buf.Length, Sr, a, b);
            };
            var air = new AircraftVoice(AircraftSoundClass.NarrowBody, 1);
            var ap = new AircraftVoiceParams { Thrust01 = 1f, Gain = 1f, DopplerPitch = 1f, ReflectionDelayS = 0.01f, ReflectionGain = 0.7f };
            Action aircraft = () =>
            {
                for (int i = 0; i < 20; i++) air.Render(buf, buf.Length, Sr, ap, ap);
            };
            // Tiered JIT promotes hot methods in the background and the switch-over allocates a little on the calling
            // thread, so warm up across the tiering delay and take the best of three measurements.
            for (int w = 0; w < 4; w++)
            {
                engine();
                aircraft();
                System.Threading.Thread.Sleep(60);
            }
            var sw = new Stopwatch();
            long engineBytes = long.MaxValue, aircraftBytes = long.MaxValue;
            double ms = double.MaxValue;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                sw.Reset();
                long before = GC.GetAllocatedBytesForCurrentThread();
                sw.Start();
                engine();
                sw.Stop();
                long mid = GC.GetAllocatedBytesForCurrentThread();
                aircraft();
                long after = GC.GetAllocatedBytesForCurrentThread();
                engineBytes = Math.Min(engineBytes, mid - before);
                aircraftBytes = Math.Min(aircraftBytes, after - mid);
                ms = Math.Min(ms, sw.Elapsed.TotalMilliseconds);
            }
            Assert.That(engineBytes, Is.EqualTo(0), "engine allocation on the audio thread");
            Assert.That(aircraftBytes, Is.EqualTo(0), "aircraft allocation on the audio thread");
            // 2 s of the heaviest preset (16 harmonics + 6 filters) on a desktop core: generous bound, catches regressions.
            Assert.That(ms, Is.LessThan(400.0));
        }

        private static float[] Flyby(AircraftSoundClass c, uint seed)
        {
            var v = new AircraftVoice(c, seed);
            int block = 480;
            var o = new float[Sr * 2];
            var buf = new float[block];
            var prev = new AircraftVoiceParams { Thrust01 = 0.3f, Gain = 1f, DopplerPitch = 1.1f, ReflectionGain = 0.7f, ReflectionDelayS = 0.002f };
            for (int at = 0; at < o.Length; at += block)
            {
                float t = (float)at / Sr;
                var next = prev;
                next.Thrust01 = t < 1f ? 0.3f : 1f;
                next.DopplerPitch = 1.1f - 0.2f * t / 2f;
                next.ReflectionDelayS = 0.002f + 0.03f * t;
                next.AirCutoffHz = 3000f + 2000f * t;
                next.Reverse01 = t > 1.5f ? 1f : 0f;
                next.OnGround = (byte)(t > 1.5f ? 1 : 0);
                v.Render(buf, block, Sr, prev, next);
                Array.Copy(buf, 0, o, at, block);
                prev = next;
            }
            return o;
        }

        [Test]
        public void AircraftVoicesAreBoundedDeterministicAndOnPitch()
        {
            foreach (AircraftSoundClass c in Enum.GetValues(typeof(AircraftSoundClass)))
            {
                float[] a = Flyby(c, 3), b = Flyby(c, 3);
                SynthDspTests.AssertClean(a, a.Length, c.ToString());
                Assert.That(SynthDspTests.RmsOf(a, 0, a.Length), Is.GreaterThan(1e-3), c + " silent");
                Assert.That(SynthDspTests.Pcm16Hash(a, a.Length), Is.EqualTo(SynthDspTests.Pcm16Hash(b, b.Length)), c + " not deterministic");
            }

            // ATR on approach: blade-pass 98 Hz (6 blades × 980 rpm).
            var atr = new AircraftVoice(AircraftSoundClass.Turboprop, 1);
            var app = new AircraftVoiceParams { Thrust01 = 0f, Gain = 1f, DopplerPitch = 1f };
            var buf = new float[Sr];
            atr.Render(buf, buf.Length, Sr, app, app);
            Assert.That(SynthDspTests.Goertzel(buf, 4800, 38400, 98f, Sr), Is.GreaterThan(4 * SynthDspTests.Goertzel(buf, 4800, 38400, 80f, Sr)));

            // Helicopter: 19.5 Hz blade pass carried by 39 / 58.5 / 78 Hz harmonics for phone speakers.
            var heli = new AircraftVoice(AircraftSoundClass.Helicopter, 1);
            var hp = new AircraftVoiceParams { Thrust01 = 0.5f, Gain = 1f, DopplerPitch = 1f };
            heli.Render(buf, buf.Length, Sr, hp, hp);
            Assert.That(SynthDspTests.Goertzel(buf, 4800, 38400, 58.5f, Sr), Is.GreaterThan(3 * SynthDspTests.Goertzel(buf, 4800, 38400, 50f, Sr)));
        }

        // ---- V9 audio golden hashes: change only on purpose (and say why in the commit). ----

        private static readonly (EngineModel Model, ulong Hash)[] EngineGolden =
        {
            (EngineModel.MotoCommuter, 0xD51A96FED50936D7UL),
            (EngineModel.MotoCruiser, 0x1BF4E17E493C4261UL),
            (EngineModel.Scooter, 0x7C1E8074208D0E51UL),
            (EngineModel.TempoSafa, 0xE97C63518D926AB8UL),
            (EngineModel.Car3Cyl, 0xB2AC828EF8605B01UL),
            (EngineModel.BusCity, 0x356C6FD5EACB8099UL),
            (EngineModel.Bicycle, 0x3F79D55C8B479103UL),
        };

        private static readonly (AircraftSoundClass Class, ulong Hash)[] AircraftGolden =
        {
            (AircraftSoundClass.Turboprop, 0x44CEDD1269F62228UL),
            (AircraftSoundClass.NarrowBody, 0x853B764420E4B898UL),
            (AircraftSoundClass.Helicopter, 0xC26C60F13599F920UL),
        };

        [Test]
        public void GoldenHashesPerSeed()
        {
            var report = new System.Text.StringBuilder();
            bool fail = false;
            foreach (var (m, h) in EngineGolden)
            {
                float[] o = Drive(m, 2026);
                ulong got = SynthDspTests.Pcm16Hash(o, o.Length);
                report.AppendLine("(EngineModel." + m + ", 0x" + got.ToString("X16") + "UL),");
                if (got != h) fail = true;
            }
            foreach (var (c, h) in AircraftGolden)
            {
                float[] o = Flyby(c, 2026);
                ulong got = SynthDspTests.Pcm16Hash(o, o.Length);
                report.AppendLine("(AircraftSoundClass." + c + ", 0x" + got.ToString("X16") + "UL),");
                if (got != h) fail = true;
            }
            if (fail) Assert.Fail("Synth golden hashes changed:\n" + report);
        }

        private static float MaxAbs(float[] b)
        {
            float m = 0f;
            foreach (float x in b) m = Math.Max(m, Math.Abs(x));
            return m;
        }
    }
}
