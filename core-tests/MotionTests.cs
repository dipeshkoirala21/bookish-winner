using System;
using Ghumante.Core.Motion;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MotionTests
    {
        private static readonly Ease[] AllEases = (Ease[])Enum.GetValues(typeof(Ease));

        [Test]
        public void EveryEaseStartsAtZeroAndEndsAtOne()
        {
            foreach (Ease ease in AllEases)
            {
                Assert.That(Easing.Evaluate(ease, 0f), Is.EqualTo(0f), ease + " at 0");
                Assert.That(Easing.Evaluate(ease, 1f), Is.EqualTo(1f), ease + " at 1");
                // Out-of-range time is clamped.
                Assert.That(Easing.Evaluate(ease, -3f), Is.EqualTo(0f), ease + " below 0");
                Assert.That(Easing.Evaluate(ease, 7f), Is.EqualTo(1f), ease + " above 1");
                Assert.That(Easing.Evaluate(ease, float.NaN), Is.EqualTo(0f), ease + " NaN");
            }
        }

        [TestCase(Ease.Linear)]
        [TestCase(Ease.OutCubic)]
        [TestCase(Ease.InCubic)]
        [TestCase(Ease.InOutSine)]
        public void PlainEasesAreMonotonicAndStayInRange(Ease ease)
        {
            float prev = 0f;
            for (int i = 1; i <= 1000; i++)
            {
                float v = Easing.Evaluate(ease, i / 1000f);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev), ease + " decreases at " + i);
                Assert.That(v, Is.InRange(0f, 1f), ease + " leaves [0,1] at " + i);
                prev = v;
            }
        }

        [Test]
        public void OutBackOvershootsByAboutTenPercent()
        {
            float peak = Peak(Ease.OutBack, out float at);
            // ASSET_MANIFEST.md 12: bouncy tweens with overshoot 1.1.
            Assert.That(peak, Is.InRange(1.09f, 1.11f));
            Assert.That(at, Is.InRange(0.5f, 0.7f));
            // It rises monotonically until the peak.
            float prev = 0f;
            for (int i = 1; i <= (int)(at * 1000f); i++)
            {
                float v = Easing.OutBack(i / 1000f);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev - 1e-6f));
                prev = v;
            }
            Assert.That(Easing.OutBack(0.5f, 0f), Is.EqualTo(Easing.OutCubic(0.5f)).Within(1e-6f),
                "no overshoot constant degenerates to a cubic");
        }

        [Test]
        public void OutElasticOvershootsThenRingsDown()
        {
            float peak = Peak(Ease.OutElastic, out float at);
            Assert.That(peak, Is.InRange(1.3f, 1.4f));
            Assert.That(at, Is.LessThan(0.25f));
            // The ringing has died down to under 1 % for the last fifth.
            for (int i = 800; i <= 1000; i++)
                Assert.That(Math.Abs(Easing.OutElastic(i / 1000f) - 1f), Is.LessThan(0.01f), "at " + i);
        }

        [Test]
        public void OutBounceNeverPassesTheEndAndTouchesIt()
        {
            int touches = 0;
            float prev = 0f;
            bool falling = false;
            for (int i = 0; i <= 10000; i++)
            {
                float v = Easing.OutBounce(i / 10000f);
                Assert.That(v, Is.InRange(0f, 1f), "at " + i);
                if (v < prev - 1e-6f && !falling)
                {
                    falling = true;
                    touches++;  // turned around at (or just below) 1: a bounce
                    Assert.That(prev, Is.GreaterThan(0.98f), "bounces off the end value");
                }
                else if (v > prev + 1e-6f)
                {
                    falling = false;
                }
                prev = v;
            }
            Assert.That(touches, Is.EqualTo(3), "lands, then three bounces before settling");
            Assert.That(Easing.OutBounce(1f / 2.75f), Is.EqualTo(1f).Within(1e-5f), "first landing at t = 0.36");
        }

        [Test]
        public void InBackDipsBelowStartBeforeLeaving()
        {
            Assert.That(Easing.InBack(0.2f), Is.LessThan(0f));
            Assert.That(Easing.InBack(0.9f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void UnderDampedSpringOvershootsAndSettles()
        {
            var s = new Spring(SpringParams.Press, 1f) { Target = 0f };
            float min = float.MaxValue;
            float t = 0f;
            const float dt = 1f / 60f;
            while (t < 2f)
            {
                s.Step(dt);
                t += dt;
                min = Math.Min(min, s.Value);
            }
            Assert.That(min, Is.LessThan(-0.05f), "release overshoots past rest (stretch)");
            Assert.That(s.IsSettled(1e-3f), Is.True, "settled within 2 s: value " + s.Value + " v " + s.Velocity);
        }

        [Test]
        public void CriticallyDampedSpringNeverOvershoots()
        {
            var s = new Spring(new SpringParams(4f, 1f), 0f) { Target = 10f };
            for (int i = 0; i < 240; i++)
            {
                s.Step(1f / 60f);
                Assert.That(s.Value, Is.LessThanOrEqualTo(10f + 1e-4f), "step " + i);
            }
            Assert.That(s.Value, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void OverDampedSpringCreepsWithoutOvershoot()
        {
            var s = new Spring(new SpringParams(2f, 2.5f), 0f) { Target = 1f };
            float prev = 0f;
            for (int i = 0; i < 600; i++)
            {
                s.Step(1f / 60f);
                Assert.That(s.Value, Is.GreaterThanOrEqualTo(prev - 1e-6f));
                Assert.That(s.Value, Is.LessThanOrEqualTo(1f + 1e-5f));
                prev = s.Value;
            }
            Assert.That(s.Value, Is.EqualTo(1f).Within(1e-2f));
        }

        [TestCase(0.25f)]
        [TestCase(1f)]
        [TestCase(1.7f)]
        public void SpringIsFrameRateIndependent(float damping)
        {
            var coarse = new Spring(new SpringParams(3f, damping), 2f) { Target = -1f };
            coarse.Kick(5f);
            var fine = coarse;
            coarse.Step(0.3f);
            for (int i = 0; i < 30; i++) fine.Step(0.01f);
            Assert.That(fine.Value, Is.EqualTo(coarse.Value).Within(1e-4f));
            Assert.That(fine.Velocity, Is.EqualTo(coarse.Velocity).Within(1e-3f));
        }

        [Test]
        public void SpringSurvivesHugeFramesAndKicks()
        {
            var s = new Spring(SpringParams.Wobble, 0f);
            s.Kick(400f);
            s.Step(10f);
            Assert.That(float.IsNaN(s.Value) || float.IsInfinity(s.Value), Is.False);
            Assert.That(s.IsSettled(1e-2f), Is.True);
            s.Step(0f);
            s.Step(-1f);
            Assert.That(s.IsSettled(1e-2f), Is.True, "non-positive dt is ignored");
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpringParams(0f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpringParams(2f, -0.1f));
        }

        [Test]
        public void TweenHoldsThroughDelayThenArrivesExactly()
        {
            var tw = new Tween(-140f, 0f, 0.4f, Ease.OutBounce, 0.25f);
            Assert.That(tw.EndTime, Is.EqualTo(0.65f).Within(1e-6f));
            Assert.That(tw.Sample(0f), Is.EqualTo(-140f));
            Assert.That(tw.Sample(0.25f), Is.EqualTo(-140f));
            Assert.That(tw.Sample(0.45f), Is.InRange(-140f, 0f));
            Assert.That(tw.Sample(0.65f), Is.EqualTo(0f));
            Assert.That(tw.Sample(9f), Is.EqualTo(0f));
            Assert.That(tw.IsComplete(0.64f), Is.False);
            Assert.That(tw.IsComplete(0.65f), Is.True);
            Assert.That(tw.Delayed(0.1f).EndTime, Is.EqualTo(0.75f).Within(1e-6f));

            var pop = new Tween(0.6f, 1f, 0.3f, Ease.OutBack);
            float max = 0f;
            for (int i = 0; i <= 300; i++) max = Math.Max(max, pop.Sample(i / 1000f));
            Assert.That(max, Is.GreaterThan(1.02f), "OutBack pops past the target scale");

            var jump = new Tween(0f, 5f, 0f, Ease.Linear, 0.5f);
            Assert.That(jump.Sample(0.49f), Is.EqualTo(0f));
            Assert.That(jump.Sample(0.5f), Is.EqualTo(5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tween(0f, 1f, -1f));
        }

        [Test]
        public void StaggerSpacesItemsAndReportsTheEnd()
        {
            // The main menu's four pills: start 0.4 s, 0.08 s apart, 0.35 s each.
            var pills = new Stagger(0.4f, 0.08f, 0.35f, Ease.OutBack);
            Assert.That(pills.DelayOf(0), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(pills.DelayOf(3), Is.EqualTo(0.64f).Within(1e-6f));
            Assert.That(pills.EndTime(4), Is.EqualTo(0.99f).Within(1e-6f));
            Assert.That(pills.EndTime(0), Is.EqualTo(0.4f).Within(1e-6f));
            Tween third = pills.At(2, 0.6f, 1f);
            Assert.That(third.Delay, Is.EqualTo(0.56f).Within(1e-6f));
            Assert.That(third.Sample(0.55f), Is.EqualTo(0.6f));
            Assert.That(third.Sample(third.EndTime), Is.EqualTo(1f));
            // Each item starts strictly after the previous one.
            for (int i = 1; i < 4; i++) Assert.That(pills.DelayOf(i), Is.GreaterThan(pills.DelayOf(i - 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => pills.DelayOf(-1));
        }

        [Test]
        public void WavePulseAndWiggle()
        {
            Assert.That(Wave.Sine(0f, 2f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Wave.Sine(0.5f, 2f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Wave.Sine(1f, 0f), Is.EqualTo(0f), "zero period is inert");

            Assert.That(Wave.Pulse(0.3f, 4f, 0.6f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Wave.Pulse(1f, 4f, 0.6f), Is.EqualTo(-1f));
            Assert.That(Wave.Pulse(4.3f, 4f, 0.6f), Is.EqualTo(0.5f).Within(1e-4f), "repeats every period");

            Assert.That(Wave.DampedWiggle(0f, 0.5f, 3f), Is.EqualTo(0f));
            Assert.That(Wave.DampedWiggle(0.5f, 0.5f, 3f), Is.EqualTo(0f));
            float strongest = 0f;
            for (int i = 1; i < 500; i++) strongest = Math.Max(strongest, Math.Abs(Wave.DampedWiggle(i / 1000f, 0.5f, 3f)));
            Assert.That(strongest, Is.InRange(0.5f, 1f));
        }

        [Test]
        public void SquashKeepsTheAskedProportions()
        {
            Squash.Scale(0.08f, 0.75f, out float sx, out float sy);
            Assert.That(sx, Is.EqualTo(1.06f).Within(1e-5f));
            Assert.That(sy, Is.EqualTo(0.92f).Within(1e-5f));
            Squash.Scale(-0.05f, 0.75f, out sx, out sy);
            Assert.That(sx, Is.LessThan(1f), "negative amount stretches");
            Assert.That(sy, Is.GreaterThan(1f));
            Squash.Scale(5f, 0.75f, out _, out sy);
            Assert.That(sy, Is.GreaterThan(0f), "never flips");
        }

        [Test]
        public void CountUpRollsToTheExactValue()
        {
            Assert.That(CountUp.Value(1250, 1300, 0f), Is.EqualTo(1250));
            Assert.That(CountUp.Value(1250, 1300, 0.5f), Is.EqualTo(1275));
            Assert.That(CountUp.Value(1250, 1300, 1f), Is.EqualTo(1300));
            Assert.That(CountUp.Value(1250, 1300, 3f), Is.EqualTo(1300));
            Assert.That(CountUp.Value(10, 0, 0.5f), Is.EqualTo(5), "counts down too");
            long prev = 0;
            for (int i = 0; i <= 100; i++)
            {
                long v = CountUp.Value(0, 80, Easing.OutCubic(i / 100f));
                Assert.That(v, Is.GreaterThanOrEqualTo(prev));
                prev = v;
            }
        }

        [Test]
        public void MotionRandomIsDeterministicAndInRange()
        {
            var a = new MotionRandom(1951);
            var b = new MotionRandom(1951);
            for (int i = 0; i < 1000; i++)
            {
                float fa = a.NextFloat();
                Assert.That(fa, Is.EqualTo(b.NextFloat()));
                Assert.That(fa, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int n = a.NextInt(7);
                b.NextInt(7);
                Assert.That(n, Is.InRange(0, 6));
            }
            var zero = new MotionRandom(0);
            Assert.That(zero.NextUInt(), Is.Not.EqualTo(0u), "a zero seed still produces numbers");
            var unset = default(MotionRandom);
            Assert.That(unset.NextUInt(), Is.Not.EqualTo(0u), "default(MotionRandom) works");
        }

        [Test]
        public void SheetOvershootStretchesInsteadOfLiftingOffItsEdge()
        {
            // The settings sheet opens with OutBack(1.1), which peaks about 4.5 % past open. Its flat edge has no
            // border, so it must never move past rest (a gap would show); the bounce becomes a stretch.
            const float size = 1231f, travel = size + 48f;
            float tallest = 1f;
            for (int i = 0; i <= 1000; i++)
            {
                float progress = Easing.OutBack(i / 1000f, 1.1f);
                EdgeSheet.Pose(progress, size, travel, out float offset, out float stretch);
                Assert.That(offset, Is.GreaterThanOrEqualTo(0f), "lifted off the edge at " + i);
                Assert.That(offset, Is.LessThanOrEqualTo(travel));
                Assert.That(stretch, Is.GreaterThanOrEqualTo(1f));
                // The inner edge (size * stretch - offset from the screen edge) still follows the eased curve.
                Assert.That(size * stretch - offset, Is.EqualTo(size - (1f - progress) * travel).Within(0.01f));
                tallest = Math.Max(tallest, stretch);
            }
            Assert.That(tallest, Is.GreaterThan(1.03f), "the bounce is kept");

            EdgeSheet.Pose(0f, size, travel, out float hidden, out float flat);
            Assert.That(hidden, Is.EqualTo(travel));
            Assert.That(flat, Is.EqualTo(1f));
            EdgeSheet.Pose(1f, size, travel, out float open, out flat);
            Assert.That(open, Is.EqualTo(0f));
            Assert.That(flat, Is.EqualTo(1f));
            EdgeSheet.Pose(float.NaN, 0f, travel, out hidden, out flat);
            Assert.That(hidden, Is.EqualTo(travel), "NaN counts as hidden");
            Assert.That(flat, Is.EqualTo(1f));
        }

        [Test]
        public void SheetDragFollowsTheFingerOutAndResistsPastOpen()
        {
            const float travel = 1000f;
            Assert.That(EdgeSheet.Drag(1f, 250f, travel), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(EdgeSheet.Drag(1f, 5000f, travel), Is.EqualTo(0f), "never past hidden");
            Assert.That(EdgeSheet.Drag(0.5f, -100f, travel), Is.EqualTo(0.6f).Within(1e-6f));
            float prev = 1f;
            for (int px = 1; px <= 2000; px++)
            {
                float p = EdgeSheet.Drag(1f, -px, travel);
                Assert.That(p, Is.GreaterThan(prev), "keeps giving a little at " + px);
                Assert.That(p, Is.LessThan(1f + EdgeSheet.MaxOverPull));
                Assert.That(p - 1f, Is.LessThan(px / travel), "resists");
                prev = p;
            }
            Assert.That(EdgeSheet.Drag(0.8f, 10f, 0f), Is.EqualTo(0.8f), "no travel yet: unchanged");
        }

        [TestCase(0.9f, 0f, false)]
        [TestCase(0.5f, 0f, true)]
        [TestCase(0.95f, -2f, true)]
        [TestCase(0.3f, 2f, false)]
        [TestCase(0.7f, -0.5f, false)]
        [TestCase(0.6f, 0.5f, true)]
        public void SheetDismissesWhenDraggedFarOrFlicked(float progress, float velocity, bool dismisses)
        {
            Assert.That(EdgeSheet.Dismisses(progress, velocity), Is.EqualTo(dismisses));
        }

        private static float Peak(Ease ease, out float at)
        {
            float peak = float.MinValue;
            at = 0f;
            for (int i = 0; i <= 10000; i++)
            {
                float t = i / 10000f;
                float v = Easing.Evaluate(ease, t);
                if (v > peak)
                {
                    peak = v;
                    at = t;
                }
            }
            return peak;
        }
    }
}
