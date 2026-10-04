using System;
using Ghumante.World.Cameras;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    public class CameraFovTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void SquareAspectKeepsTheAngle()
        {
            Assert.AreEqual(62f, CameraFov.VerticalFromHorizontal(62f, 1f), Tolerance);
        }

        [Test]
        public void LandscapeSixteenByNine()
        {
            // 2 * atan(tan(31 deg) / (16/9)) = 37.35 deg; this is the landscape driving camera.
            float expected = (float)(2.0 * Math.Atan(Math.Tan(31.0 * Math.PI / 180.0) / (16.0 / 9.0)) * 180.0 / Math.PI);
            Assert.AreEqual(expected, CameraFov.VerticalFromHorizontal(CameraFov.DrivingMinHorizontalFov, 16f / 9f), Tolerance);
            Assert.AreEqual(37.349f, CameraFov.VerticalFromHorizontal(62f, 16f / 9f), 0.01f);
        }

        [Test]
        public void PortraitWidensTheVerticalAngle()
        {
            // Walking camera on a 9:19.5 phone held upright: 2 * atan(tan(27.5 deg) * 19.5 / 9) = 96.88 deg.
            float v = CameraFov.VerticalFromHorizontal(CameraFov.WalkingMinHorizontalFov, 9f / 19.5f);
            Assert.AreEqual(96.879f, v, 0.01f);
            Assert.Greater(v, CameraFov.VerticalFromHorizontal(CameraFov.WalkingMinHorizontalFov, 19.5f / 9f));
        }

        [Test]
        public void ClampsAtOneHundredDegrees()
        {
            // Flying (70 deg) on a 9:20 portrait screen would need 114.5 deg vertical.
            Assert.AreEqual(CameraFov.MaxVerticalFovDegrees,
                CameraFov.VerticalFromHorizontal(CameraFov.FlyingMinHorizontalFov, 9f / 20f), Tolerance);
        }

        [Test]
        public void RoundTripsThroughHorizontal([Values(0.45f, 0.75f, 1f, 1.333f, 2.17f)] float aspect)
        {
            float v = CameraFov.VerticalFromHorizontal(55f, aspect);
            if (v < CameraFov.MaxVerticalFovDegrees)
            {
                Assert.AreEqual(55f, CameraFov.HorizontalFromVertical(v, aspect), Tolerance);
            }
            else
            {
                // Clamped: the horizontal view is then narrower than requested.
                Assert.Less(CameraFov.HorizontalFromVertical(v, aspect), 55f);
            }
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFov.VerticalFromHorizontal(0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFov.VerticalFromHorizontal(180f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFov.VerticalFromHorizontal(60f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFov.VerticalFromHorizontal(60f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraFov.VerticalFromHorizontal(60f, float.PositiveInfinity));
        }
    }
}
