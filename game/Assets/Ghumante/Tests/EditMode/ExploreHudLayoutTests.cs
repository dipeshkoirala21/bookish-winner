using System;
using System.Collections.Generic;
using Ghumante.Characters.Cameras;
using Ghumante.Core.Characters;
using Ghumante.UI.Hud;
using Ghumante.World.Cameras;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// HUD layout rules of the route chip (W2 detail pass; owner: "direction info blocks the view"): on phones and
    /// tablets in both orientations, with notches, punch holes and home indicators, the chip lands inside the safe area,
    /// clear of every other HUD element (modelled from Hud.uss) and never over the road ahead; and the road-ahead zone
    /// really covers where every chase rig shows the road (the rig maths of <see cref="ChaseRigProfile"/>). Engine-free
    /// (no UnityEngine), so it also runs under plain <c>dotnet test</c> with the engine-free sources.
    /// </summary>
    public class ExploreHudLayoutTests
    {
        /// <summary>Panel units: the PanelSettings scale keeps the panel's area constant (about 2069 x 931 on 20:9).</summary>
        private const float PanelArea = 2069f * 931f;

        private struct Device
        {
            public string Name;
            public float LongOverShort;

            /// <summary>Safe-area insets in panel units, portrait (top, bottom) and landscape (left, right, bottom).</summary>
            public float PortraitTop, PortraitBottom, LandLeft, LandRight, LandBottom;

            public override string ToString()
            {
                return Name;
            }
        }

        private static readonly Device[] Devices =
        {
            new Device { Name = "iPhone 19.5:9 with Dynamic Island", LongOverShort = 2556f / 1179f, PortraitTop = 142f, PortraitBottom = 82f, LandLeft = 142f, LandRight = 142f, LandBottom = 50f },
            new Device { Name = "Android 20:9 punch hole", LongOverShort = 2400f / 1080f, PortraitTop = 117f, PortraitBottom = 48f, LandLeft = 117f, LandRight = 0f, LandBottom = 48f },
            new Device { Name = "Android 16:9 no notch", LongOverShort = 16f / 9f },
            new Device { Name = "Notched 19:9", LongOverShort = 19f / 9f, PortraitTop = 96f, PortraitBottom = 40f, LandLeft = 96f, LandRight = 96f, LandBottom = 40f },
            new Device { Name = "iPad 4:3", LongOverShort = 4f / 3f, PortraitTop = 30f, PortraitBottom = 30f, LandBottom = 30f },
        };

        private enum Layout
        {
            Walk,
            Ride,
            Passenger,
        }

        /// <summary>The screen, safe area and the HUD's rectangles as Hud.uss lays them out (approximately).</summary>
        private static void Hud(Device d, bool portrait, Layout layout, out HudRect screen, out HudRect safe, out HudRect speedo, out HudRect topBar,
                                List<HudRect> obstacles)
        {
            float w = (float)Math.Sqrt(PanelArea * d.LongOverShort), h = PanelArea / w;
            if (portrait)
            {
                float t = w;
                w = h;
                h = t;
            }
            screen = new HudRect(0f, 0f, w, h);
            safe = portrait
                ? HudRect.Edges(0f, d.PortraitTop, w, h - d.PortraitBottom)
                : HudRect.Edges(d.LandLeft, 0f, w - d.LandRight, h - d.LandBottom);
            obstacles.Clear();

            // Top bar: actions row on the right (compass + 5 buttons), the place pill left (landscape) or under it (portrait).
            var actions = new HudRect(safe.Right - 24f - 652f, safe.Y + 16f, 652f, 108f);
            HudRect place = portrait
                ? new HudRect(safe.X + 0.5f * safe.W - 0.42f * safe.W, actions.Bottom + 4f, 0.84f * safe.W, 92f)
                : new HudRect(safe.X + 24f, safe.Y + 28f, Math.Min(0.52f * safe.W, 760f), 92f);
            topBar = HudRect.Edges(Math.Min(actions.X, place.X), actions.Y, Math.Max(actions.Right, place.Right), Math.Max(actions.Bottom, place.Bottom));

            // Speedometer cluster (digits, surface chip) and the map credit under it.
            float cx = safe.X + 0.5f * safe.W;
            float speedoBottom = portrait ? safe.Bottom - 0.42f * safe.H - 12f : safe.Bottom - 44f;
            var digits = new HudRect(cx - 150f, speedoBottom - 190f, 300f, 190f);
            var credit = new HudRect(cx - 175f, safe.Bottom - 6f - 24f, 350f, 24f);
            speedo = HudRect.Edges(Math.Min(digits.X, credit.X), digits.Y, Math.Max(digits.Right, credit.Right), credit.Bottom);
            obstacles.Add(credit);

            // Touch controls (resting places) per layout.
            if (!portrait)
            {
                if (layout != Layout.Passenger) obstacles.Add(new HudRect(safe.X + 70f, safe.Bottom - 60f - 230f, 230f, 230f)); // stick
                switch (layout)
                {
                    case Layout.Walk:
                        obstacles.Add(new HudRect(safe.Right - 0.04f * safe.W - 184f, safe.Bottom - 0.14f * safe.H - 184f, 184f, 184f)); // action
                        obstacles.Add(new HudRect(safe.Right - 0.2f * safe.W - 129f, safe.Bottom - 0.1f * safe.H - 129f, 129f, 129f)); // namaste
                        break;
                    case Layout.Ride:
                        obstacles.Add(new HudRect(safe.Right - 48f - 396f, safe.Bottom - 56f - 190f, 396f, 190f)); // pedals
                        obstacles.Add(new HudRect(safe.Right - 0.03f * safe.W - 129f, safe.Bottom - 0.46f * safe.H - 129f, 129f, 129f)); // hop off
                        obstacles.Add(new HudRect(safe.Right - 0.2f * safe.W - 129f, safe.Bottom - 0.3f * safe.H - 129f, 129f, 129f)); // horn
                        break;
                    default:
                        obstacles.Add(new HudRect(safe.Right - 0.04f * safe.W - 184f, safe.Bottom - 0.14f * safe.H - 184f, 184f, 184f)); // hop off
                        obstacles.Add(new HudRect(safe.Right - 0.2f * safe.W - 129f, safe.Bottom - 0.14f * safe.H - 129f, 129f, 129f)); // bell
                        break;
                }
            }
            else
            {
                if (layout == Layout.Walk) obstacles.Add(new HudRect(cx - 115f, safe.Bottom - 120f - 230f, 230f, 230f)); // stick
                if (layout == Layout.Ride) obstacles.Add(new HudRect(safe.X + 36f, safe.Bottom - 140f - 150f, 150f, 150f)); // brake
                float action = layout == Layout.Ride ? 120f : 166f;
                obstacles.Add(new HudRect(safe.Right - (layout == Layout.Ride ? 0.03f : 0.06f) * safe.W - action,
                                          safe.Bottom - (layout == Layout.Ride ? 0.3f : 0.12f) * safe.H - action, action, action));
                obstacles.Add(new HudRect(safe.Right - 0.07f * safe.W - 129f, safe.Bottom - 0.26f * safe.H - 129f, 129f, 129f)); // namaste/horn/bell
                obstacles.Add(new HudRect(safe.X + 0.08f * safe.W, safe.Bottom - 0.54f * safe.H - 60f, 0.84f * safe.W, 60f)); // prompt
            }
            obstacles.Add(digits);
        }

        [Test]
        public void TheChipFindsAClearSlotOnEveryPhoneAndTablet()
        {
            var obstacles = new List<HudRect>();
            foreach (Device d in Devices)
            {
                foreach (bool portrait in new[] { false, true })
                {
                    foreach (Layout layout in new[] { Layout.Walk, Layout.Ride, Layout.Passenger })
                    {
                        foreach (float chipW in new[] { 330f, 470f })
                        {
                            HudRect screen, safe, speedo, topBar;
                            Hud(d, portrait, layout, out screen, out safe, out speedo, out topBar, obstacles);
                            HudRect[] arr = obstacles.ToArray();
                            RouteChipSlot slot;
                            HudRect chip = RouteChipLayout.Place(screen, safe, portrait, chipW, 92f, speedo, topBar, arr, arr.Length, out slot);
                            string what = d + (portrait ? " portrait " : " landscape ") + layout + " chip " + chipW + ": " + chip + " in " + slot;
                            Assert.AreNotEqual(RouteChipSlot.Fallback, slot, what);
                            Assert.IsTrue(safe.Contains(chip), what + " leaves the safe area " + safe);
                            Assert.AreEqual(0f, RoadAheadZone.For(portrait).Cover(chip, screen), what + " covers the road ahead");
                            Assert.IsFalse(chip.Overlaps(topBar, 8f), what + " hits the top bar");
                            Assert.IsFalse(chip.Overlaps(speedo, 8f), what + " hits the speedometer");
                            foreach (HudRect o in arr) Assert.IsFalse(chip.Overlaps(o, 8f), what + " hits " + o);
                        }
                    }
                }
            }
        }

        [Test]
        public void PhonesPutItBesideTheSpeedometerInLandscapeAndUnderTheTopBarInPortrait()
        {
            var obstacles = new List<HudRect>();
            foreach (Device d in Devices)
            {
                if (d.LongOverShort < 1.7f) continue; // phones
                HudRect screen, safe, speedo, topBar;
                Hud(d, false, Layout.Ride, out screen, out safe, out speedo, out topBar, obstacles);
                RouteChipSlot slot;
                HudRect chip = RouteChipLayout.Place(screen, safe, false, 360f, 92f, speedo, topBar, obstacles.ToArray(), obstacles.Count, out slot);
                Assert.AreEqual(RouteChipSlot.BesideSpeedoLeft, slot, d + " landscape");
                Assert.Less(chip.Right, speedo.X, "left of the speedometer");
                Assert.Greater(chip.Y, screen.H * 0.75f, d + ": on the bottom edge, below the explorer");

                Hud(d, true, Layout.Ride, out screen, out safe, out speedo, out topBar, obstacles);
                chip = RouteChipLayout.Place(screen, safe, true, 360f, 92f, speedo, topBar, obstacles.ToArray(), obstacles.Count, out slot);
                Assert.AreEqual(RouteChipSlot.UnderTopBarLeft, slot, d + " portrait");
                Assert.Less(chip.Bottom, screen.H * 0.3f, d + ": high above the horizon");
            }
        }

        [Test]
        public void TheOldTopCentreBannerWouldHaveCoveredTheRoad()
        {
            // The W2 stage 1 banner sat top-centre at 128 units, up to 70% wide (landscape): exactly where the road runs
            // to the horizon. The zone must say so, or it protects nothing.
            float w = (float)Math.Sqrt(PanelArea * 20f / 9f), h = PanelArea / w;
            var screen = new HudRect(0f, 0f, w, h);
            var banner = new HudRect(0.5f * w - 0.3f * w, 128f, 0.6f * w, 96f);
            Assert.Greater(RoadAheadZone.For(false).Cover(banner, screen), 0.05f);
            var under = new HudRect(0.5f * w - 120f, h - 60f, 240f, 40f);
            Assert.AreEqual(0f, RoadAheadZone.For(false).Cover(under, screen), "below the explorer is the road already ridden");
        }

        [Test]
        public void TheRoadAheadZoneCoversEveryChaseRig()
        {
            // For every class, the near and far chase views, at rest and at full look-ahead, on 4:3 to 20:9 screens: the
            // explorer's ground point and the horizon (where the road runs to) fall inside the zone of the orientation.
            float[] longOverShort = { 4f / 3f, 16f / 9f, 19.5f / 9f, 20f / 9f };
            foreach (bool portrait in new[] { false, true })
            {
                RoadAheadZone zone = RoadAheadZone.For(portrait);
                foreach (float a in longOverShort)
                {
                    float aspect = portrait ? 1f / a : a;
                    for (int c = 0; c < CameraViews.RigCount; c++)
                    {
                        var rig = (RigClass)c;
                        foreach (CameraView view in new[] { CameraViews.Default(rig), CameraView.Far })
                        {
                            ChaseRigProfile p = ChaseRigProfile.For(rig, portrait, RigClass.Bus, view);
                            float vfov = CameraFov.VerticalFromHorizontal(p.MinHorizontalFovDeg, aspect);
                            foreach (float speed in new[] { 0f, 40f })
                            {
                                float foot = p.FootScreenY(speed);
                                double pitch = p.PitchDeg * Math.PI / 180.0;
                                float above = p.AimHeightM + p.DistanceM * (float)Math.Sin(pitch);
                                float behind = p.DistanceM * (float)Math.Cos(pitch);
                                float viewPitch = ChaseRigProfile.ViewPitchDeg(above, behind, foot, vfov);
                                float horizon = (float)(Math.Tan(viewPitch * Math.PI / 180.0) / Math.Tan(vfov * 0.5 * Math.PI / 180.0));
                                string what = rig + " " + view + (portrait ? " portrait " : " landscape ") + a.ToString("0.00") + " at " + speed;
                                Assert.GreaterOrEqual(foot, zone.FootNdcY - 1e-4f, what + ": ground point below the zone");
                                Assert.LessOrEqual(Math.Min(horizon, 1f), zone.FarNdcY + 1e-4f, what + ": horizon above the zone");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void RectanglesAndTheZoneBehave()
        {
            var a = new HudRect(0f, 0f, 10f, 10f);
            Assert.IsTrue(a.Overlaps(new HudRect(9f, 9f, 5f, 5f)));
            Assert.IsFalse(a.Overlaps(new HudRect(12f, 0f, 5f, 5f)));
            Assert.IsTrue(a.Overlaps(new HudRect(12f, 0f, 5f, 5f), 3f), "within the gap");
            Assert.AreEqual(1f, a.OverlapArea(new HudRect(9f, 9f, 5f, 5f)), 1e-5f);
            Assert.IsTrue(new HudRect(float.NaN, 0f, 1f, 1f).IsEmpty);
            Assert.IsFalse(a.Overlaps(new HudRect(float.NaN, 0f, 5f, 5f)), "unlaid-out elements never block");
            RoadAheadZone z = RoadAheadZone.For(true);
            Assert.AreEqual(z.FootHalfWidthNdc, z.HalfWidthAt(z.FootNdcY), 1e-5f);
            Assert.AreEqual(z.FarHalfWidthNdc, z.HalfWidthAt(z.FarNdcY), 1e-5f);
            Assert.AreEqual(0f, z.HalfWidthAt(z.FarNdcY + 0.1f));
            RouteChipSlot slot;
            HudRect none = RouteChipLayout.Place(new HudRect(0f, 0f, 100f, 100f), new HudRect(0f, 0f, 100f, 100f), false, 400f, 90f, default(HudRect),
                                                 default(HudRect), null, 5, out slot);
            Assert.AreEqual(RouteChipSlot.Fallback, slot, "a chip bigger than the screen has no clear slot");
            Assert.IsFalse(none.IsEmpty);
        }
    }
}
