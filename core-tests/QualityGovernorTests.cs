using System;
using Ghumante.Core.Platform;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The adaptive governor driven by simulated phones: dynamic resolution with hysteresis (never oscillates), the Auto
    /// tier step down after about 10 s too slow at the minimum scale, at most one tier step up per session, and the
    /// frames it must ignore (loading hitches, pause).
    /// </summary>
    public class QualityGovernorTests
    {
        private const float Budget30 = 1000f / 30f;

        /// <summary>A simulated phone: the GPU time at full resolution and a CPU floor; the frame interval is the larger of
        /// the two (and never below the frame budget, like a capped target frame rate).</summary>
        private sealed class Phone
        {
            public float GpuAtFullMs;
            public float CpuMs = 10f;
            public bool ReportGpu = true;

            /// <summary>GPU time reported at a render scale (pixels scale with its square).</summary>
            public float Gpu(float scale)
            {
                return GpuAtFullMs * scale * scale;
            }

            public float Interval(float scale, float budget)
            {
                return Math.Max(budget, Math.Max(CpuMs, Gpu(scale)));
            }
        }

        private struct RunLog
        {
            public int ScaleChanges, Downs, Ups, TierDowns, TierUps;
            public float FirstChangeS, FirstTierDownS;
        }

        /// <summary>Runs the governor for <paramref name="seconds"/> of simulated frames.</summary>
        private static RunLog Run(QualityGovernor g, Phone phone, float seconds, Func<float, bool> blockedAt = null, float t0 = 0f)
        {
            var log = new RunLog { FirstChangeS = -1f, FirstTierDownS = -1f };
            float t = t0;
            while (t < t0 + seconds)
            {
                float interval = phone.Interval(g.RenderScale, g.BudgetMs);
                float gpu = phone.ReportGpu ? phone.Gpu(g.RenderScale) : 0f;
                bool blocked = blockedAt != null && blockedAt(t);
                GovernorAction a = g.Tick(interval, gpu, blocked);
                t += interval / 1000f;
                if (a == GovernorAction.None) continue;
                if (log.FirstChangeS < 0f) log.FirstChangeS = t - t0;
                if (a == GovernorAction.RenderScaleDown) { log.ScaleChanges++; log.Downs++; }
                if (a == GovernorAction.RenderScaleUp) { log.ScaleChanges++; log.Ups++; }
                if (a == GovernorAction.TierDown)
                {
                    log.TierDowns++;
                    if (log.FirstTierDownS < 0f) log.FirstTierDownS = t - t0;
                }
                if (a == GovernorAction.TierUp) log.TierUps++;
            }
            return log;
        }

        private static QualityGovernor NewGovernor(QualityTier tier, bool auto, int fps = 30, QualityTier ceiling = QualityTier.High)
        {
            var g = new QualityGovernor();
            g.Reset(tier, ceiling, auto, fps);
            return g;
        }

        [Test]
        public void OverBudgetStepsDownAfterTwoSecondsAndStopsAtTheMinimum()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, false);
            Assert.That(g.RenderScale, Is.EqualTo(1f).Within(1e-5f), "starts at full resolution");
            var heavy = new Phone { GpuAtFullMs = 80f }; // far too slow at any scale
            RunLog log = Run(g, heavy, 1.0f);
            Assert.That(log.ScaleChanges, Is.EqualTo(0), "1 s of slow frames is not yet sustained");
            log = Run(g, heavy, 30f);
            Assert.That(g.RenderScale, Is.EqualTo(0.7f).Within(1e-5f), "down to the 0.7 floor");
            Assert.That(log.Downs, Is.EqualTo(3), "1.0 -> 0.9 -> 0.8 -> 0.7, never below");
            Assert.That(log.TierDowns, Is.EqualTo(0), "a manual tier never steps");
        }

        [Test]
        public void SettlesWhereTheFrameRateHoldsAndStaysThere()
        {
            // 40 ms at full scale (too slow at 30 fps), 32.4 ms at 0.9 (on target, but no headroom for another step up).
            QualityGovernor g = NewGovernor(QualityTier.High, false);
            RunLog log = Run(g, new Phone { GpuAtFullMs = 40f }, 600f);
            Assert.That(g.RenderScale, Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(log.ScaleChanges, Is.EqualTo(1), "one step down in ten minutes, then nothing");
        }

        [Test]
        public void HysteresisBandMovesNothing()
        {
            // Between "too slow" (12 % over budget) and "room" (GPU under 70 % of budget): a 5 % overrun and a GPU at
            // 90 % of budget must never change the scale, in either direction.
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            var phone = new Phone { GpuAtFullMs = Budget30 * 1.05f };
            RunLog log = Run(g, phone, 300f);
            Assert.That(log.ScaleChanges + log.TierDowns + log.TierUps, Is.EqualTo(0));

            phone = new Phone { GpuAtFullMs = Budget30 * 0.9f };
            QualityGovernor g2 = NewGovernor(QualityTier.High, false);
            Run(g2, new Phone { GpuAtFullMs = 80f }, 10f); // pushed down first
            float low = g2.RenderScale;
            log = Run(g2, phone, 300f);
            Assert.That(low, Is.LessThan(1f));
            Assert.That(log.Downs, Is.EqualTo(0));
        }

        [Test]
        public void HeadroomStepsBackUpToTheTierMaximum()
        {
            QualityGovernor g = NewGovernor(QualityTier.Mid, false);
            Run(g, new Phone { GpuAtFullMs = 80f }, 12f);
            Assert.That(g.RenderScale, Is.EqualTo(0.7f).Within(1e-5f));
            RunLog log = Run(g, new Phone { GpuAtFullMs = 12f }, 60f); // the heavy scene is behind us
            Assert.That(g.RenderScale, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(log.Ups, Is.EqualTo(3));
            Assert.That(log.FirstChangeS, Is.GreaterThanOrEqualTo(4.99f), "headroom must hold 5 s before a step up");
            log = Run(g, new Phone { GpuAtFullMs = 12f }, 60f);
            Assert.That(log.ScaleChanges, Is.EqualTo(0), "never above the tier maximum");
        }

        [Test]
        public void FailedProbesBackOffSoItNeverOscillates()
        {
            // The worst case for a probe: the GPU reports plenty of headroom at 0.9, but 1.0 is too slow (the extra
            // pixels push a bandwidth-bound phone over the edge). Without a back-off this would flip every 7 s.
            var g = NewGovernor(QualityTier.High, false);
            var phone = new LyingPhone();
            int changes = 0, slowFrames = 0, frames = 0;
            float t = 0f;
            while (t < 600f)
            {
                float scale = g.RenderScale;
                float interval = scale > 0.95f ? 45f : Budget30;
                GovernorAction a = g.Tick(interval, phone.GpuMs, false);
                if (a == GovernorAction.RenderScaleDown || a == GovernorAction.RenderScaleUp) changes++;
                frames++;
                if (interval > Budget30 * 1.1f) slowFrames++;
                t += interval / 1000f;
            }
            Assert.That(changes, Is.LessThanOrEqualTo(20), "probes back off: 5, 10, 20, 40, 80, 120 s");
            Assert.That((float)slowFrames / frames, Is.LessThan(0.06f), "almost all the time at the scale that holds");
            Assert.That(g.UpDelayS, Is.EqualTo(120f).Within(1e-3f));
        }

        private sealed class LyingPhone
        {
            public float GpuMs = 12f;
        }

        [Test]
        public void AutoStepsTheTierDownOnceAfterAboutTenSecondsAtTheMinimum()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            var phone = new Phone { GpuAtFullMs = 40f, CpuMs = 45f }; // CPU-bound: resolution cannot fix it
            float atFloor = -1f, tierDown = -1f, t = 0f;
            int downs = 0, tierDowns = 0;
            while (t < 25f)
            {
                float interval = phone.Interval(g.RenderScale, g.BudgetMs);
                GovernorAction a = g.Tick(interval, phone.Gpu(g.RenderScale), false);
                t += interval / 1000f;
                if (a == GovernorAction.RenderScaleDown && ++downs == 3) atFloor = t;
                if (a == GovernorAction.TierDown && ++tierDowns == 1) tierDown = t;
            }
            Assert.That(downs, Is.EqualTo(3), "first the render scale goes to its floor");
            Assert.That(tierDowns, Is.EqualTo(1), "then one tier step, not straight to Low");
            Assert.That(g.Tier, Is.EqualTo(QualityTier.Mid));
            Assert.That(tierDown - atFloor, Is.InRange(10f, 13f), "about 10 s too slow at the floor (after its cool-down)");
            Assert.That(g.RenderScale, Is.EqualTo(0.7f).Within(1e-5f), "the lighter tier starts from where it was");
        }

        [Test]
        public void TierDownNeedsSustainedSlownessAtTheMinimum()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            Run(g, new Phone { GpuAtFullMs = 80f }, 12f); // at the floor now
            Assert.That(g.AtMinimumScale, Is.True);
            // Alternating 3 s slow, 3 s fine: never 10 s of slowness, so no tier step.
            var slow = new Phone { GpuAtFullMs = 80f };
            var fine = new Phone { GpuAtFullMs = 15f };
            int tierDowns = 0;
            for (int i = 0; i < 20; i++)
            {
                tierDowns += Run(g, slow, 3f).TierDowns;
                tierDowns += Run(g, fine, 3f).TierDowns;
            }
            Assert.That(tierDowns, Is.EqualTo(0));
            Assert.That(g.Tier, Is.EqualTo(QualityTier.High));
        }

        [Test]
        public void TierDownGoesOneStepAtATimeAndStopsAtLow()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            var hopeless = new Phone { GpuAtFullMs = 100f, CpuMs = 60f };
            RunLog first = Run(g, hopeless, 24f);
            Assert.That(first.TierDowns, Is.EqualTo(1));
            Assert.That(g.Tier, Is.EqualTo(QualityTier.Mid));
            RunLog rest = Run(g, hopeless, 120f);
            Assert.That(rest.TierDowns, Is.EqualTo(1), "Mid -> Low, each after its own 10 s");
            Assert.That(g.Tier, Is.EqualTo(QualityTier.Low));
            Assert.That(Run(g, hopeless, 120f).TierDowns, Is.EqualTo(0), "nothing below Low");
        }

        [Test]
        public void NeverStepsTheTierUpMoreThanOncePerSession()
        {
            QualityGovernor g = NewGovernor(QualityTier.Low, true, 30, QualityTier.High);
            var light = new Phone { GpuAtFullMs = 6f, CpuMs = 5f };
            RunLog log = Run(g, light, 59f);
            Assert.That(log.TierUps, Is.EqualTo(0), "a minute of plenty of headroom first");
            log = Run(g, light, 600f);
            Assert.That(log.TierUps, Is.EqualTo(1), "once per session, even with headroom for ten minutes");
            Assert.That(g.Tier, Is.EqualTo(QualityTier.Mid));

            // Too slow again: Auto may step down again, but never back up this session.
            RunLog down = Run(g, new Phone { GpuAtFullMs = 90f, CpuMs = 50f }, 60f);
            Assert.That(down.TierDowns, Is.EqualTo(1));
            Assert.That(Run(g, light, 600f).TierUps, Is.EqualTo(0));
        }

        [Test]
        public void NeverStepsAboveTheCeiling()
        {
            QualityGovernor g = NewGovernor(QualityTier.Mid, true, 30, QualityTier.Mid);
            Assert.That(Run(g, new Phone { GpuAtFullMs = 5f, CpuMs = 4f }, 300f).TierUps, Is.EqualTo(0));
        }

        [Test]
        public void TierUpNeedsGpuTimings()
        {
            QualityGovernor g = NewGovernor(QualityTier.Low, true, 30, QualityTier.High);
            var blind = new Phone { GpuAtFullMs = 6f, CpuMs = 5f, ReportGpu = false };
            Assert.That(Run(g, blind, 300f).TierUps, Is.EqualTo(0), "on target says nothing about headroom");
        }

        [Test]
        public void WithoutTimingsProbesUpSlowlyWhenOnTarget()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, false);
            Run(g, new Phone { GpuAtFullMs = 80f }, 12f);
            Assert.That(g.RenderScale, Is.EqualTo(0.7f).Within(1e-5f));
            RunLog log = Run(g, new Phone { GpuAtFullMs = 10f, ReportGpu = false }, 120f);
            Assert.That(g.RenderScale, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(log.FirstChangeS, Is.GreaterThanOrEqualTo(9.99f), "probes wait 10 s without GPU timings");
        }

        [Test]
        public void IgnoresLoadingHitchesAndPause()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            var heavy = new Phone { GpuAtFullMs = 80f, CpuMs = 60f };
            // Blocked (loading, paused) for a minute of terrible frames: nothing happens.
            RunLog log = Run(g, heavy, 60f, t => true);
            Assert.That(log.ScaleChanges + log.TierDowns, Is.EqualTo(0));
            // Single long hitches (tile uploads, GC) between good frames: nothing happens.
            int changes = 0;
            for (int i = 0; i < 5000; i++)
            {
                float interval = i % 10 == 0 ? 400f : Budget30;
                if (g.Tick(interval, 15f, false) != GovernorAction.None) changes++;
            }
            Assert.That(changes, Is.EqualTo(0));
            Assert.That(g.RenderScale, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void PausedFramesNeverCountTowardsATierStep()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, true);
            var heavy = new Phone { GpuAtFullMs = 80f, CpuMs = 60f };
            Run(g, heavy, 12f); // at the floor, the countdown has started
            Assert.That(g.AtMinimumScale, Is.True);
            // A whole minute paused (or loading) with terrible frames: nothing.
            Assert.That(Run(g, heavy, 60f, t => true).TierDowns, Is.EqualTo(0));
            // Back in play (after a settle second) the countdown carries on from where the pause stopped it.
            RunLog log = Run(g, heavy, 8f);
            Assert.That(log.TierDowns, Is.EqualTo(0));
            log = Run(g, heavy, 6f);
            Assert.That(log.TierDowns, Is.EqualTo(1));
        }

        [Test]
        public void SixtyFpsBudgetAndLowTierRange()
        {
            QualityGovernor g = NewGovernor(QualityTier.High, false, 60);
            Assert.That(g.BudgetMs, Is.EqualTo(1000f / 60f).Within(1e-3f));
            // 20 ms at full scale: too slow for 60 (16.7 ms), fine at 0.9 (16.2 ms GPU, interval on budget).
            Run(g, new Phone { GpuAtFullMs = 20f, CpuMs = 8f }, 30f);
            Assert.That(g.RenderScale, Is.EqualTo(0.9f).Within(1e-5f));

            // Low is authored at 0.7: no dynamic range, nothing to change.
            QualityGovernor low = NewGovernor(QualityTier.Low, false);
            Assert.That(low.RenderScale, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(Run(low, new Phone { GpuAtFullMs = 90f }, 60f).ScaleChanges, Is.EqualTo(0));
        }

        [Test]
        public void IsDeterministic()
        {
            QualityGovernor a = NewGovernor(QualityTier.High, true), b = NewGovernor(QualityTier.High, true);
            var phone = new Phone { GpuAtFullMs = 42f, CpuMs = 20f };
            RunLog la = Run(a, phone, 200f), lb = Run(b, phone, 200f);
            Assert.That((la.ScaleChanges, la.TierDowns, a.RenderScale, a.Tier), Is.EqualTo((lb.ScaleChanges, lb.TierDowns, b.RenderScale, b.Tier)));
        }
    }
}
