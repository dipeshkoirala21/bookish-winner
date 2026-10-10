using System;
using System.Collections.Generic;
using Ghumante.Characters;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Streaming;
using Ghumante.World.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The W2 detail-pass camera on a real <see cref="Camera"/> (owner: "camera view gets blocked if I try to back up my
    /// motorbike in the narrower streets", "houses block the view", "multiple camera angles"): reversing in a 4.8 m lane
    /// against a box-world <see cref="IViewObstacleQuery"/> never puts the camera inside or behind a house; the views per
    /// class, their memory and blends; the mounted views on the rider's eyes; and both orientations keep the CameraFov
    /// minimum horizontal FOV.
    /// </summary>
    public class ExploreCameraTests
    {
        /// <summary>Axis-aligned boxes (game metres) with an exact sphere sweep.</summary>
        private sealed class Boxes : IViewObstacleQuery
        {
            private readonly List<double[]> _b = new List<double[]>();

            public Boxes Add(double x0, double y0, double z0, double x1, double y1, double z1)
            {
                _b.Add(new[] { x0, y0, z0, x1, y1, z1 });
                return this;
            }

            public double Distance(double x, double y, double z)
            {
                double best = double.PositiveInfinity;
                foreach (double[] b in _b)
                {
                    double dx = Math.Max(Math.Max(b[0] - x, 0.0), x - b[3]);
                    double dy = Math.Max(Math.Max(b[1] - y, 0.0), y - b[4]);
                    double dz = Math.Max(Math.Max(b[2] - z, 0.0), z - b[5]);
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                return best;
            }

            public bool SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, double radius, double maxDist, out double hitDist)
            {
                double t = 0.0;
                while (t <= maxDist)
                {
                    double d = Distance(ox + dx * t, oy + dy * t, oz + dz * t);
                    if (d <= radius + 1e-6)
                    {
                        hitDist = t;
                        return true;
                    }
                    t += Math.Max(0.002, d - radius);
                }
                hitDist = 0.0;
                return false;
            }

            public bool Blocks(Vector3 a, Vector3 b)
            {
                int n = Math.Max(1, (int)Math.Ceiling(Vector3.Distance(a, b) / 0.05f));
                for (int i = 0; i <= n; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)i / n);
                    if (Distance(p.x, p.y, p.z) <= 0.0) return true;
                }
                return false;
            }
        }

        private GameObject _go;
        private Camera _camera;
        private ChaseCameraRig _rig;
        private readonly FlatGround _ground = new FlatGround { Height = 0f };

        [SetUp]
        public void CreateCamera()
        {
            _go = new GameObject("Explore Camera Test");
            _camera = _go.AddComponent<Camera>();
            _camera.aspect = 2400f / 1080f;
            _rig = new ChaseCameraRig();
            _rig.Attach(_camera, StreamingConfig.ForTier(1));
        }

        [TearDown]
        public void DestroyCamera()
        {
            _rig.Detach();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        private void Tick(float dt, Vector3 ground, float headingRad, float speed, RigClass rig, bool mount = false, CameraMount m = default(CameraMount))
        {
            var t = new CameraTarget { Ground = ground, HeadingRad = headingRad, SpeedMps = speed, Rig = rig, PassengerOf = RigClass.Car, HasMount = mount, Mount = m };
            _rig.Tick(dt, t, new ControlFrame(), _ground, default(WorldPos));
        }

        [Test]
        public void ReversingAMotorbikeInANarrowLaneKeepsTheViewClear()
        {
            // A 4.8 m lane between 10 m houses, closed 7 m behind the bike, a side lane to the east. Ride in, stop,
            // back up 4 m towards the corner: every frame the camera is outside the houses and sees the bike.
            Boxes houses = new Boxes().Add(-14, 0, -60, -2.4, 10, 60).Add(2.4, 0, -2.0, 14, 10, 60).Add(-14, 0, -20, 30, 10, -6.8);
            _rig.Obstacles = houses;
            _rig.Snap();
            float z = 0f;
            for (float t = 0f; t < 5f; t += 1f / 30f)
            {
                float speed = t < 1f ? 0f : z > -4f ? -1.5f : 0f;
                z += speed / 30f;
                var ground = new Vector3(0f, 0f, z);
                Tick(1f / 30f, ground, 0f, speed, RigClass.TwoWheeler);
                Vector3 cam = _camera.transform.position;
                Vector3 pivot = ground + Vector3.up;
                string when = "t = " + t.ToString("0.00");
                Assert.GreaterOrEqual(houses.Distance(cam.x, cam.y, cam.z), ChaseBoom.ProbeRadiusM - 0.05, when + ": inside a house at " + cam);
                Assert.IsFalse(houses.Blocks(pivot, cam), when + ": a house hides the bike from " + cam);
                Assert.Greater(cam.y, ChaseCameraRig.GroundClearanceM - 1e-3f);
            }
            Assert.IsTrue(_rig.Reverse.Reversing, "the reversing frame is on");
            Assert.Greater(_rig.Reverse.Raise01, 0.9f);
        }

        [Test]
        public void EachClassShowsItsRememberedViewAndBlendsToIt()
        {
            var memory = new CameraViewMemory();
            _rig.Views = memory;
            _rig.Snap();
            Tick(0.02f, Vector3.zero, 0f, 0f, RigClass.TwoWheeler);
            Assert.AreEqual(CameraView.Near, _rig.View);
            float near = Vector3.Distance(_camera.transform.position, Vector3.up);

            Assert.AreEqual(CameraView.Far, memory.Cycle(RigClass.TwoWheeler));
            for (int i = 0; i < 90; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.TwoWheeler);
            Assert.AreEqual(CameraView.Far, _rig.View);
            Assert.Greater(Vector3.Distance(_camera.transform.position, Vector3.up), near * 1.3f, "the far chase sits farther back");

            // Off the scooter: the walking view is its own (still the default), and the scooter keeps "far".
            for (int i = 0; i < 60; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.Walk);
            Assert.AreEqual(CameraView.Near, _rig.View);
            Assert.AreEqual(CameraView.Far, memory.Get(RigClass.TwoWheeler));
        }

        [Test]
        public void TheHandlebarViewSitsAtTheRidersEyes()
        {
            var memory = new CameraViewMemory();
            memory.Set(RigClass.TwoWheeler, CameraView.Handlebar);
            _rig.Views = memory;
            var mount = new CameraMount { Head = new Vector3(0f, 1.6f, 0.4f), Origin = Vector3.zero, HeadingDeg = 0f };

            // Not seated yet (hopping on): the chase view stands in.
            _rig.Snap();
            Tick(0.02f, Vector3.zero, 0f, 0f, RigClass.TwoWheeler);
            Assert.IsFalse(_rig.Mounted);
            Assert.AreEqual(CameraView.Near, _rig.View);

            // Seated: blends to the eyes, in front of the face, looking ahead with the near plane at 0.1 m.
            for (int i = 0; i < 60; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.TwoWheeler, true, mount);
            Assert.IsTrue(_rig.Mounted);
            Assert.AreEqual(CameraView.Handlebar, _rig.View);
            Vector3 eye = mount.Head + new Vector3(0f, CameraViews.EyeUpM, CameraViews.EyeForwardM);
            Assert.Less(Vector3.Distance(_camera.transform.position, eye), 0.02f, "at the eyes");
            Assert.AreEqual(CameraViews.MountedNearClipM, _camera.nearClipPlane, 1e-4f);
            Assert.Greater(_camera.transform.forward.z, 0.9f, "looking along the road");
            Assert.Less(_camera.transform.forward.y, 0f, "tilted down to the handlebar");

            // A bonnet view needs a bonnet: a scooter falls back to its default.
            memory.Set(RigClass.Car, CameraView.Hood);
            Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.Car, true, mount);
            Assert.IsFalse(_rig.Mounted);
            mount.HasHood = true;
            mount.HoodLocal = new Vector3(0f, 1.15f, 3.0f);
            for (int i = 0; i < 60; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.Car, true, mount);
            Assert.IsTrue(_rig.Mounted);
            Assert.Less(Vector3.Distance(_camera.transform.position, mount.HoodLocal), 0.02f, "on the bonnet");
        }

        [Test]
        public void BothOrientationsKeepTheMinimumHorizontalFov()
        {
            foreach (float aspect in new[] { 2400f / 1080f, 1080f / 2400f, 4f / 3f, 3f / 4f })
            {
                _camera.aspect = aspect;
                foreach (RigClass rig in new[] { RigClass.Walk, RigClass.TwoWheeler, RigClass.Bus })
                {
                    _rig.Views = null;
                    _rig.Snap();
                    Tick(0.02f, Vector3.zero, 0f, 0f, rig);
                    float horizontal = CameraFov.HorizontalFromVertical(_camera.fieldOfView, aspect);
                    float min = CameraRigTable.For(rig, aspect < 1f).MinHFovDeg;
                    bool clamped = _camera.fieldOfView >= CameraFov.MaxVerticalFovDegrees - 0.01f;
                    Assert.IsTrue(clamped || horizontal >= min - 0.1f, rig + " at aspect " + aspect + ": " + horizontal + " < " + min);
                }
            }
        }

        [Test]
        public void OverTheShoulderSitsCloseBesideTheWalker()
        {
            var memory = new CameraViewMemory();
            memory.Set(RigClass.Walk, CameraView.OverShoulder);
            _rig.Views = memory;
            _rig.Snap();
            for (int i = 0; i < 30; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.Walk);
            Vector3 cam = _camera.transform.position;
            Assert.Less(Vector3.Distance(cam, new Vector3(0f, 1.5f, 0f)), 4f, "close behind");
            Assert.Greater(cam.x, 0.3f, "over the right shoulder");
            Assert.Less(cam.z, 0f, "behind");

            // A wall right of the walker keeps the shoulder pivot (and the camera) out of it.
            _rig.Obstacles = new Boxes().Add(0.4, 0, -20, 3, 6, 20);
            _rig.Snap();
            for (int i = 0; i < 30; i++) Tick(1f / 60f, Vector3.zero, 0f, 0f, RigClass.Walk);
            Assert.Less(_camera.transform.position.x, 0.4f - 0.25f, "not inside the wall");
        }
    }
}
