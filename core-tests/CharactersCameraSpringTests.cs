using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The camera's springs and frame clock (owner playtest #5: "the camera constantly keeps moving"): the exact critically
    /// damped step gives the same curve whatever the frame clock (Unity's <c>SmoothDamp</c>, which the old rig used, does
    /// not), never overshoots, takes the short way round, and the clock evens out a phone's frame-time jitter while
    /// keeping game time equal to real time.
    /// </summary>
    public class CharactersCameraSpringTests
    {
        /// <summary>A phone's frame clock: 33.3 ms ± 4 ms, a 16.7 ms frame every 13, a 50 ms hitch every 97.</summary>
        private static float Jittery(int i)
        {
            return CameraRides.FrameDt(i);
        }

        [Test]
        public void TheSpringGivesTheSameCurveWhateverTheFrameClock()
        {
            // A goal driving off at 9 m/s, braking and turning back: followed at 60 fps exactly and on the jittery clock. At
            // every jittery frame time the value must equal the 60 fps run (interpolated), so nothing about the frame clock
            // shows in the camera. Unity's SmoothDamp, on the same goal, is off by centimetres.
            Func<double, double> goal = t => t < 3 ? 9 * t : t < 5 ? 27 + 9 * (t - 3) - 2.25 * (t - 3) * (t - 3) : 36 - 3 * (t - 5);
            var fine = new CameraSpring();
            var fineT = new List<double> { 0 };
            var fineV = new List<double> { 0 };
            fine.Reset(0f);
            for (int i = 1; i <= 8 * 600; i++)
            {
                double t = i / 600.0;
                fine.Step((float)goal(t), 0.1f, 0f, 1f / 600f);
                fineT.Add(t);
                fineV.Add(fine.Value);
            }
            var coarse = new CameraSpring();
            coarse.Reset(0f);
            float old = 0f, oldVel = 0f;
            double time = 0, worst = 0, worstOld = 0;
            for (int i = 0; time < 7.9; i++)
            {
                float dt = Jittery(i);
                time += dt;
                coarse.Step((float)goal(time), 0.1f, 0f, dt);
                old = LegacyChaseRig.SmoothDamp(old, (float)goal(time), ref oldVel, 0.1f, float.PositiveInfinity, dt);
                int k = (int)Math.Floor(time * 600.0);
                double f = time * 600.0 - k;
                double reference = fineV[k] + (fineV[k + 1] - fineV[k]) * f;
                if (time > 0.5)
                {
                    worst = Math.Max(worst, Math.Abs(coarse.Value - reference));
                    worstOld = Math.Max(worstOld, Math.Abs(old - reference));
                }
            }
            TestContext.WriteLine("exact spring off by " + (worst * 100).ToString("0.00") + " cm, Unity SmoothDamp by " + (worstOld * 100).ToString("0.0") + " cm");
            Assert.Less(worst, 0.01, "the exact spring does not see the frame clock");
            Assert.Greater(worstOld, 3 * worst, "SmoothDamp does (the old camera's shake)");
        }

        [Test]
        public void ThePlainSpringTrailsASteadyGoalAndTheTrackingOneDoesNot()
        {
            var plain = new CameraSpring();
            var tracking = new CameraSpring();
            plain.Reset(0f);
            tracking.Reset(0f);
            double t = 0;
            for (int i = 0; i < 300; i++)
            {
                float dt = Jittery(i);
                t += dt;
                plain.Step((float)(5 * t), 0.3f, 0f, dt);
                tracking.StepTracking((float)(5 * t), 5f, 0.3f, dt);
            }
            Assert.AreEqual(5 * 0.3, 5 * t - plain.Value, 0.01, "a plain spring trails a steady goal by speed × smooth time");
            Assert.AreEqual(0.0, 5 * t - tracking.Value, 0.01, "with the goal's speed fed forward it does not trail at all");
            Assert.AreEqual(5f, plain.Velocity, 0.01f);
        }

        [Test]
        public void ASpringNeverOvershootsAStep()
        {
            // A kerb: the goal steps up 0.15 m. The value rises smoothly, never past the goal, never faster than a few cm
            // per frame.
            var s = new CameraSpring();
            s.Reset(0f);
            float last = 0f;
            for (int i = 0; i < 120; i++)
            {
                s.StepTracking(0.15f, 0f, 0.28f, 1f / 30f);
                Assert.LessOrEqual(s.Value, 0.15f + 1e-6f, "frame " + i);
                Assert.GreaterOrEqual(s.Value, last - 1e-6f, "monotonic");
                Assert.Less(s.Value - last, 0.025f, "no jump");
                last = s.Value;
            }
            Assert.AreEqual(0.15f, last, 1e-3f);
        }

        [Test]
        public void AnAngleSpringTakesTheShortWayRound()
        {
            var a = new CameraAngleSpring();
            a.Reset(170f);
            for (int i = 0; i < 60; i++)
            {
                a.Step(-170f, 0.2f, 0f, 1f / 60f);
                Assert.IsTrue(a.Value >= 170f || a.Value <= -170f, "through 180, not through 0: " + a.Value);
            }
            Assert.AreEqual(-170f, a.Value, 0.2f);
            Assert.AreEqual(-180f, CameraAngleSpring.Wrap(180f));
            Assert.AreEqual(20f, CameraAngleSpring.Delta(170f, -170f), 1e-4f);
            Assert.AreEqual(0f, CameraAngleSpring.Wrap(float.NaN));
        }

        [Test]
        public void ASpringIgnoresNonsense()
        {
            var s = new CameraSpring();
            s.Reset(1f);
            s.Step(float.NaN, 0.1f, 0f, 0.016f);
            s.Step(2f, 0.1f, 0f, float.NaN);
            s.Step(2f, 0.1f, 0f, 0f);
            Assert.AreEqual(1f, s.Value);
            s.Step(2f, 0f, 0f, 0.016f);
            Assert.AreEqual(2f, s.Value, "no smoothing time: at the goal");
        }

        [Test]
        public void TheClockEvensOutJitterAndKeepsRealTime()
        {
            var clock = new CameraClock();
            var outs = new List<float>();
            double real = 0, game = 0;
            for (int i = 0; i < 2000; i++)
            {
                float raw = 1f / 30f + (CharMath.Unit(CharMath.Hash((uint)i, 7u)) - 0.5f) * 0.008f;
                real += raw;
                float dt = clock.Tick(raw);
                game += dt;
                if (i > 60) outs.Add(dt);
            }
            double mean = 0, sd = 0;
            foreach (float d in outs) mean += d;
            mean /= outs.Count;
            foreach (float d in outs) sd += (d - mean) * (d - mean);
            sd = Math.Sqrt(sd / outs.Count);
            TestContext.WriteLine("raw ±2.3 ms → smoothed sd " + (sd * 1000).ToString("0.00") + " ms; drift " + ((real - game) * 1000).ToString("0.0") + " ms");
            Assert.Less(sd, 0.0011, "±4 ms of measured jitter becomes about ±1 ms");
            Assert.Less(Math.Abs(real - game), 0.01, "game time stays real time");

            // 40 fps on a 60 Hz screen: 16.7 and 33.3 ms in turn become an even 25 ms.
            clock.Reset();
            for (int i = 0; i < 400; i++)
            {
                float dt = clock.Tick(i % 2 == 0 ? 1f / 60f : 1f / 30f);
                if (i > 100) Assert.AreEqual(0.025f, dt, 0.0012f, "frame " + i);
            }

            // A 100 ms hitch is spread over the next half second, not dumped into one frame.
            clock.Reset();
            for (int i = 0; i < 100; i++) clock.Tick(1f / 30f);
            float worst = 0f;
            double owed = 0;
            for (int i = 0; i < 60; i++)
            {
                float raw = i == 0 ? 0.1f : 1f / 30f;
                float dt = clock.Tick(raw);
                owed += raw - dt;
                worst = Math.Max(worst, dt);
            }
            Assert.Less(worst, 0.06f, "the hitch never shows as one long frame");
            Assert.Less(Math.Abs(owed), 0.005, "and is fully repaid within two seconds");
        }
    }
}
