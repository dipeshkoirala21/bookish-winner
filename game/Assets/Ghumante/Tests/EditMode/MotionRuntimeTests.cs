using Ghumante.Core.Motion;
using Ghumante.Core.Services;
using Ghumante.UI.Motion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    /// <summary>UI/Motion without a panel: the animator is ticked by hand.</summary>
    public class MotionRuntimeTests
    {
        private static void Run(UiAnimator a, float seconds, System.Action<float> each = null)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f)
            {
                a.Tick(1f / 60f);
                if (each != null) each(t);
            }
        }

        [Test]
        public void PressSquashesOntoTheBaseThenStretchesBack()
        {
            var host = new VisualElement();
            var button = new Button { name = "b" };
            host.Add(button);
            using (var animator = new UiAnimator(host, new MotionSettings()))
            {
                var feel = new PressFeel(animator, button, new NullHaptics(), HapticKind.LightImpact, null);
                feel.Node.Press(true);
                Run(animator, 1f);
                Vector3 pressed = button.style.scale.value.value;
                Assert.AreEqual(1.06f, pressed.x, 2e-3, "scale 1.06 x 0.92 when held");
                Assert.AreEqual(0.92f, pressed.y, 2e-3);

                feel.Node.Press(false);
                float tallest = 0f;
                Run(animator, 1.5f, _ => tallest = Mathf.Max(tallest, button.style.scale.value.value.y));
                Assert.Greater(tallest, 1.003f, "springs back past rest (stretch) before settling");
                Assert.AreEqual(1f, button.style.scale.value.value.y, 1e-3);
                Assert.AreEqual(StyleKeyword.Null, button.style.translate.keyword, "translate left to USS (:active sink)");
            }
        }

        [Test]
        public void TweensWriteOnlyTheirOwnProperties()
        {
            var e = new VisualElement();
            using (var animator = new UiAnimator(e, new MotionSettings()))
            {
                MotionNode node = animator.Node(e);
                animator.Play(node, MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear, 0.1f));
                animator.Commit();
                Assert.AreEqual(0f, e.style.opacity.value, 1e-6);
                Assert.AreEqual(StyleKeyword.Null, e.style.scale.keyword);
                Assert.AreEqual(StyleKeyword.Null, e.style.rotate.keyword);
                Assert.AreEqual(StyleKeyword.Null, e.style.translate.keyword);
                Run(animator, 0.35f);
                Assert.AreEqual(1f, e.style.opacity.value, 1e-6);
                Assert.IsFalse(node.IsTweening(MotionChannel.Opacity));
            }
        }

        [Test]
        public void AfterFiresOnceOnTheAnimatorClock()
        {
            var e = new VisualElement();
            var animator = new UiAnimator(e, new MotionSettings());
            int fired = 0;
            animator.After(0.25f, () => fired++);
            animator.Tick(0.2f);
            Assert.AreEqual(0, fired);
            animator.Tick(0.1f);
            Assert.AreEqual(1, fired);
            animator.Tick(1f);
            Assert.AreEqual(1, fired);

            animator.After(0.05f, () => fired++);
            animator.Dispose();
            animator.Tick(1f);
            Assert.AreEqual(1, fired, "nothing fires after Dispose");
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void TicksOnEveryRenderedFrame(int fps)
        {
            // A model of the panel scheduler (UIElements Scheduler.UpdateScheduledEvents in 6000.3): once per
            // panel update, now is whole milliseconds since startup; an item runs when now - interval >= lastRun,
            // and lastRun becomes now, so nothing carries over. Frames are paced at fps with up to 0.7 ms of
            // ordinary jitter. Every(16) or Every(33) skips frames here, and the motion judders.
            var random = new MotionRandom(7);
            double period = 1000.0 / fps;
            long lastRun = 1000;
            int skipped = 0;
            for (int frame = 1; frame <= 600; frame++)
            {
                long now = (long)(1000.0 + frame * period + random.Range(-0.7f, 0.7f));
                if (now - UiAnimator.TickIntervalMs >= lastRun) lastRun = now;
                else skipped++;
            }
            Assert.AreEqual(0, skipped, "frames rendered without animation progress at " + fps + " fps");
        }

        [Test]
        public void LongFramesAreClamped()
        {
            var e = new VisualElement();
            using (var animator = new UiAnimator(e, new MotionSettings()))
            {
                animator.Tick(5f);
                Assert.AreEqual(UiAnimator.MaxStep, animator.Time, 1e-6);
                animator.Tick(-1f);
                Assert.AreEqual(UiAnimator.MaxStep, animator.Time, 1e-6);
            }
        }

        [Test]
        public void ReduceMotionStopsIdleLoopsAndSettlesNodes()
        {
            var e = new VisualElement();
            var motion = new MotionSettings();
            using (var animator = new UiAnimator(e, motion))
            {
                MotionNode node = animator.Node(e);
                node.SetRest(MotionChannel.RotateDegrees, 8f);
                int idleCalls = 0;
                bool rested = false;
                animator.OnIdle((t, dt) =>
                {
                    idleCalls++;
                    node.SetOffsetRotate(5f * Wave.Sine(t, 1f));
                }, () => rested = true);
                Run(animator, 0.3f);
                Assert.Greater(idleCalls, 0);

                motion.ReduceMotion = true;
                Assert.IsTrue(rested);
                int calls = idleCalls;
                Run(animator, 0.3f);
                Assert.AreEqual(calls, idleCalls, "no idle loops with Reduce motion");
                Assert.AreEqual(8f, e.style.rotate.value.angle.value, 1e-3, "back at the rest pose (USS tilt kept)");
            }
        }
    }
}
