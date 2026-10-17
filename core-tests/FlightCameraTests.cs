using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The flight cameras (<see cref="FlightCamera"/>) against the owner's camera complaint (docs/W2_PLAYTEST_1.md #5: "It
    /// constantly keeps moving which make the driving experience really bad"): through a take-off and a banked turn on
    /// the real sample, flown with uneven frame times (where a fixed-step model with an interpolated render pose shows
    /// any jitter), the chase view never rolls, keeps the aircraft steady on screen and at a steady distance, and stays
    /// above the ground; the cockpit eye of every craft sits inside its cabin (walls, roof and floor all around it).
    /// </summary>
    public class FlightCameraTests
    {
        private const float Deg = (float)(Math.PI / 180.0);

        [Test]
        public void TheChaseViewStaysSteadyThroughATakeoffAndABankedTurn()
        {
            FlightWorld w = FlightTests.World();
            var m = new FlightModel(FlightSpec.For(FlightCraft.Turboprop), w);
            double rx, rz;
            float ry, rh;
            Assert.That(w.TryRunwayStart(FlightTests.Config().Threshold02.X, FlightTests.Config().Threshold02.Z, out rx, out rz, out ry, out rh), Is.True);
            m.PlaceOnGround(rx, rz, rh);
            m.EngineRunning = true;
            var pilot = new FlightTestPilot(m, w);
            var cam = new FlightCamera();
            cam.Snap();
            // Uneven frames: 60, 45, 90, 60 and 30 fps in turn.
            float[] frames = { 1f / 60f, 1f / 45f, 1f / 90f, 1f / 60f, 1f / 30f };
            float turnTo = ArcadeVehicle.WrapAngle(pilot.RunwayHeading + 90f * Deg);
            double prevDist = double.NaN;
            float prevAim = float.NaN, prevYaw = float.NaN;
            float worstAimStep = 0f, worstYawRate = 0f, maxRoll = 0f, minClear = float.MaxValue;
            double worstDistStep = 0, minDist = double.MaxValue, maxDist = 0;
            bool turned = false;
            float t = 0f;
            for (int k = 0; k < 60 * 150; k++)
            {
                float dt = frames[k % frames.Length];
                t += dt;
                FlightInput i;
                if (m.OnGround)
                {
                    double s, l;
                    pilot.ToRunway(m.X, m.Z, out s, out l);
                    float hErr = ArcadeVehicle.WrapAngle(pilot.RunwayHeading - m.Heading);
                    i = new FlightInput(m.Airspeed > m.Spec.RotateMps ? 0.7f : 0f, 0f, Clamp(hErr * 3f - (float)l * 0.05f, -1f, 1f), 1f);
                }
                else
                {
                    bool turn = m.Agl > 200f;
                    i = pilot.Hold(turn ? 2f * Deg : 6f * Deg, 1.35f * m.Spec.StallCleanMps, turn ? turnTo : pilot.RunwayHeading);
                    if (!turn) i.Throttle = 1f;
                    if (m.Agl > 120f && m.Flaps > 0) i.FlapsUp = true;
                    if (turn && Math.Abs(ArcadeVehicle.WrapAngle(turnTo - m.Heading)) < 5f * Deg) turned = true;
                }
                FlightEvents ev = m.Advance(dt, i);
                Assert.That(ev & (FlightEvents.Bounce | FlightEvents.ResetAir | FlightEvents.ResetRunway), Is.EqualTo(FlightEvents.None));
                FlightPose p = m.Interpolated;
                FlightCameraPose c = cam.Update(dt, p, m.Vx, m.Vy, m.Vz, m.Spec, FlightView.Chase, 0f, 0f, false, w);

                // Where the aircraft sits in the view: the angle between the camera's forward and the aircraft's centre.
                double ax = p.X - c.X, ay = p.Y + m.Spec.FuselageCentreM - c.Y, az = p.Z - c.Z;
                double dist = Math.Sqrt(ax * ax + ay * ay + az * az);
                double fx = Math.Sin(c.Yaw) * Math.Cos(c.Pitch), fy = Math.Sin(c.Pitch), fz = Math.Cos(c.Yaw) * Math.Cos(c.Pitch);
                float aim = (float)Math.Acos(Math.Max(-1.0, Math.Min(1.0, (ax * fx + ay * fy + az * fz) / dist)));
                if (!double.IsNaN(prevDist))
                {
                    worstDistStep = Math.Max(worstDistStep, Math.Abs(dist - prevDist));
                    worstAimStep = Math.Max(worstAimStep, Math.Abs(aim - prevAim));
                    worstYawRate = Math.Max(worstYawRate, Math.Abs(ArcadeVehicle.WrapAngle(c.Yaw - prevYaw)) / dt);
                }
                prevDist = dist;
                prevAim = aim;
                prevYaw = c.Yaw;
                maxRoll = Math.Max(maxRoll, Math.Abs(c.Roll));
                minDist = Math.Min(minDist, dist);
                maxDist = Math.Max(maxDist, dist);
                FlightGroundSample g;
                w.Ground(c.X, c.Z, (float)c.Y, out g);
                minClear = Math.Min(minClear, (float)(c.Y - g.Height));
                if (turned && t > 120f) break;
            }
            TestContext.WriteLine("flew " + t.ToString("0") + " s (turned " + turned + "): distance " + minDist.ToString("0.0") + "–" + maxDist.ToString("0.0") +
                                  " m, worst step " + worstDistStep.ToString("0.000") + " m; aircraft on screen moves at most " +
                                  (worstAimStep / Deg).ToString("0.000") + "° a frame; camera yaw rate at most " + (worstYawRate / Deg).ToString("0.0") +
                                  "°/s; roll " + (maxRoll / Deg).ToString("0.0") + "°; lowest " + minClear.ToString("0.0") + " m above the ground");
            Assert.That(turned, Is.True, "turned 90° in a banked turn");
            Assert.That(maxRoll, Is.EqualTo(0f), "the chase view never rolls (steady horizon)");
            Assert.That(worstAimStep, Is.LessThan(0.35f * Deg), "the aircraft does not jump about on screen");
            Assert.That(worstDistStep, Is.LessThan(0.6), "the camera does not surge toward or away from the aircraft");
            Assert.That(maxDist - minDist, Is.LessThan(0.25 * FlightCamera.ChaseDistance(m.Spec)), "a steady distance");
            Assert.That(worstYawRate, Is.LessThan(25f * Deg), "the view swings round no faster than a gentle pan");
            Assert.That(minClear, Is.GreaterThanOrEqualTo(2.4f), "never in the ground");
        }

        [Test]
        public void TheCockpitEyeSitsInsideEveryCabin()
        {
            foreach (FlightCraft c in FlightSpec.All)
            {
                FlightSpec s = FlightSpec.For(c);
                var body = new MeshData();
                FlightMesher.Build(c, FlightPart.Body, 0, 0, body);
                double ex = s.EyeX, ey = s.EyeY, ez = s.EyeZ;
                double right = Cast(body, ex, ey, ez, 1, 0, 0), left = Cast(body, ex, ey, ez, -1, 0, 0);
                double up = Cast(body, ex, ey, ez, 0, 1, 0), down = Cast(body, ex, ey, ez, 0, -1, 0);
                TestContext.WriteLine(c + ": eye (" + ex + ", " + ey + ", " + ez + ") walls " + left.ToString("0.00") + " / " + right.ToString("0.00") + " m, roof " +
                                      up.ToString("0.00") + " m, floor " + down.ToString("0.00") + " m");
                Assert.That(left, Is.LessThan(2.5), c + ": a wall on the left");
                Assert.That(right, Is.LessThan(2.5), c + ": a wall on the right");
                Assert.That(up, Is.InRange(0.15, 1.2), c + ": the roof just over the pilot's head");
                Assert.That(down, Is.LessThan(2.5), c + ": the floor under the seat");
            }
        }

        /// <summary>Distance along a ray to the nearest triangle of <paramref name="m"/> (∞ when it hits none).</summary>
        private static double Cast(MeshData m, double ox, double oy, double oz, double dx, double dy, double dz)
        {
            double best = double.PositiveInfinity;
            float[] p = m.Positions;
            int[] idx = m.Indices;
            for (int t = 0; t + 2 < m.IndexCount; t += 3)
            {
                int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                double e1x = p[b] - p[a], e1y = p[b + 1] - p[a + 1], e1z = p[b + 2] - p[a + 2];
                double e2x = p[c] - p[a], e2y = p[c + 1] - p[a + 1], e2z = p[c + 2] - p[a + 2];
                double hx = dy * e2z - dz * e2y, hy = dz * e2x - dx * e2z, hz = dx * e2y - dy * e2x;
                double det = e1x * hx + e1y * hy + e1z * hz;
                if (Math.Abs(det) < 1e-12) continue;
                double f = 1.0 / det;
                double sx = ox - p[a], sy = oy - p[a + 1], sz = oz - p[a + 2];
                double u = f * (sx * hx + sy * hy + sz * hz);
                if (u < 0 || u > 1) continue;
                double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
                double v = f * (dx * qx + dy * qy + dz * qz);
                if (v < 0 || u + v > 1) continue;
                double d = f * (e2x * qx + e2y * qy + e2z * qz);
                if (d > 1e-4 && d < best) best = d;
            }
            return best;
        }

        private static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }
    }
}
