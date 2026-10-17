using System;
using Ghumante.Core.Characters;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The boom's early look (owner playtest #5: "the camera constantly keeps moving"; W2 owner feedback: "houses block the
    /// view"): a long boom (the far views, portrait, a bus) meets overbridges, upper floors over the street and corners in
    /// time and changes its pitch smoothly instead of being cut short in one frame. Synthetic worlds
    /// (<see cref="CharactersCameraTests.BoxWorld"/>); the real streets are in <see cref="CharactersCameraRideTests"/>.
    /// </summary>
    public class CharactersCameraBoomTests
    {
        private const float Dt = 1f / 30f;

        [Test]
        public void UnderAFootOverbridgeTheFarBoomLowersInsteadOfPullingIn()
        {
            // Portrait, far view of a car (12.2 m boom at 30.5°, pivot 1.2 m over the road) driving at 8.7 m/s under a foot
            // overbridge: a 4 m wide deck 5.5 m over the road (RoadClearance.MinUnderpassClearanceM). A steeper boom would cut
            // through the deck once the car is past it; a flatter one passes beneath. The camera never enters the deck, the
            // boom keeps most of its length and never jumps by more than 3 % in a frame.
            var world = new CharactersCameraTests.BoxWorld().Add(-15, 5.5, 25, 15, 6.5, 29);
            const float pitch = 30.5f, want = 12.16f, min = 3.5f, speed = 8.7f, pivotY = 1.2f;
            var boom = new ChaseBoom();
            float lead = speed * ChaseBoom.PredictS * ChaseBoom.LongBoomScale(want);
            float shortest = want, lowest = 0f, worstStep = 0f, last = -1f;
            int inside = 0, through = 0;
            for (int i = 0; i * Dt * speed < 70f; i++)
            {
                double z = i * Dt * speed;
                boom.Solve(world, 0, pivotY, z, 0f, pitch, want, min, i == 0 ? 0f : Dt, 0f, lead, 0f);
                double ox, oy, oz;
                boom.CameraOffset(0f, out ox, out oy, out oz);
                double cx = ox, cy = pivotY + oy, cz = z + oz;
                if (world.Distance(cx, cy, cz) < ChaseBoom.ProbeRadiusM - 0.02) inside++;
                if (world.Blocks(0, pivotY, z, cx, cy, cz)) through++;
                shortest = Math.Min(shortest, boom.DistanceM);
                lowest = Math.Min(lowest, boom.LiftDeg);
                if (last >= 0f) worstStep = Math.Max(worstStep, Math.Abs(boom.DistanceM - last) / Math.Max(4f, last));
                last = boom.DistanceM;
            }
            TestContext.WriteLine("shortest boom " + shortest.ToString("0.00") + " m, lowest pitch change " + lowest.ToString("0.0") + "°, worst step " +
                                  (100 * worstStep).ToString("0.0") + " %");
            Assert.AreEqual(0, inside, "the camera inside the deck");
            Assert.AreEqual(0, through, "the deck between the car and the camera");
            Assert.Less(lowest, -5f, "the boom went under the deck");
            Assert.Greater(shortest, 0.8f * want, "it kept most of its length");
            Assert.LessOrEqual(worstStep, CharactersCameraRideTests.BoomStepRelMax, "no jump");
        }

        [Test]
        public void BackingTowardsALowWallTheBoomRisesBeforeItArrives()
        {
            // A rider reverses at 1.5 m/s towards a 1.6 m garden wall behind them (the scooter's 7.8 m boom at 11° from a
            // 1 m pivot). The early look raises the boom before the wall reaches it, so the length is never cut short in
            // one frame, the camera never enters the wall and the boom never jumps.
            var world = new CharactersCameraTests.BoxWorld().Add(-15, 0, 0, 15, 1.6, 0.4);
            const float pitch = 11f, want = 7.8f, min = 3f, speed = -1.5f, pivotY = 1f;
            var boom = new ChaseBoom();
            float lead = speed * ChaseBoom.PredictS * ChaseBoom.LongBoomScale(want);
            float last = -1f, worstStep = 0f, highest = 0f;
            int inside = 0, clamped = 0;
            for (int i = 0; 14.0 + i * Dt * speed > 2.5; i++)
            {
                double z = 14.0 + i * Dt * speed;
                boom.Solve(world, 0, pivotY, z, 0f, pitch, want, min, i == 0 ? 0f : Dt, 0f, lead, 0f);
                double ox, oy, oz;
                boom.CameraOffset(0f, out ox, out oy, out oz);
                if (world.Distance(ox, pivotY + oy, z + oz) < ChaseBoom.ProbeRadiusM - 0.02) inside++;
                if (boom.Clamped) clamped++;
                highest = Math.Max(highest, boom.LiftDeg);
                if (last >= 0f) worstStep = Math.Max(worstStep, Math.Abs(boom.DistanceM - last) / Math.Max(4f, last));
                last = boom.DistanceM;
            }
            TestContext.WriteLine("highest lift " + highest.ToString("0.0") + "°, worst step " + (100 * worstStep).ToString("0.0") + " %, clamped " + clamped +
                                  ", boom " + boom.DistanceM.ToString("0.00") + " m");
            Assert.AreEqual(0, inside, "the camera inside the wall");
            Assert.Greater(highest, 5f, "rose over it");
            Assert.Greater(boom.DistanceM, 0.85f * want, "keeping its length");
            Assert.AreEqual(0, clamped, "never cut short by the hard limit");
            Assert.LessOrEqual(worstStep, CharactersCameraRideTests.BoomStepRelMax, "no jump");
        }

        [Test]
        public void TheYawPredictionMatchesTheSpring()
        {
            // The boom looks where its yaw will be: the closed form agrees with stepping the spring itself through a steady
            // turn (the goal turning at 40°/s), and predicts nothing when nothing turns.
            var s = new CameraAngleSpring();
            s.Reset(10f);
            float goal = 10f;
            for (int i = 0; i < 20; i++)
            {
                goal += 40f * Dt;
                s.Step(goal, 0.3f, 0f, Dt);
            }
            float predicted = s.PredictTurn(goal, 40f, 0.3f, 0.5f);
            CameraAngleSpring copy = s;
            float g = goal, start = copy.Value;
            for (int i = 0; i < 50; i++)
            {
                g += 40f * 0.01f;
                copy.Step(g, 0.3f, 0f, 0.01f);
            }
            float actual = CameraAngleSpring.Delta(start, copy.Value);
            TestContext.WriteLine("predicted " + predicted.ToString("0.000") + "°, stepped " + actual.ToString("0.000") + "°");
            Assert.AreEqual(actual, predicted, 0.01f);
            Assert.Greater(predicted, 10f, "it keeps turning");

            var still = new CameraAngleSpring();
            still.Reset(-170f);
            Assert.AreEqual(0f, still.PredictTurn(-170f, 0f, 0.3f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, still.PredictTurn(float.NaN, 0f, 0.3f, 0.5f));
            Assert.AreEqual(0f, still.PredictTurn(0f, 10f, 0.3f, 0f));
        }

        [Test]
        public void LongBoomsLookFurtherAhead()
        {
            Assert.AreEqual(1f, ChaseBoom.LongBoomScale(6f));
            Assert.AreEqual(1f, ChaseBoom.LongBoomScale(ChaseBoom.LongBoomM));
            Assert.AreEqual(MathF.Sqrt(12.16f / ChaseBoom.LongBoomM), ChaseBoom.LongBoomScale(12.16f), 1e-5f);
            Assert.AreEqual(ChaseBoom.MaxLongBoomScale, ChaseBoom.LongBoomScale(40f));
            Assert.AreEqual(1f, ChaseBoom.LongBoomScale(float.NaN));
        }
    }
}
