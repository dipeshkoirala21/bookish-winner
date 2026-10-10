using System;
using System.Globalization;

namespace Ghumante.UI.Hud
{
    /// <summary>What the route chip's big line says (<c>ExploreScreen.SetRouteStep</c>).</summary>
    public enum RouteStepKind : byte
    {
        /// <summary>Nothing yet: the destination's name.</summary>
        None = 0,

        /// <summary>The distance to the next turn (the arrow shows the turn).</summary>
        Turn = 1,

        /// <summary>No turn for a while: "Straight on".</summary>
        Straight = 2,

        /// <summary>Off the route: "Back to the route" (the arrow points at it).</summary>
        BackToRoute = 3,
    }

    /// <summary>An axis-aligned rectangle in panel units (y grows downwards), engine-free.</summary>
    public struct HudRect
    {
        public float X, Y, W, H;

        public HudRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public static HudRect Edges(float left, float top, float right, float bottom)
        {
            return new HudRect(left, top, right - left, bottom - top);
        }

        public float Right
        {
            get { return X + W; }
        }

        public float Bottom
        {
            get { return Y + H; }
        }

        /// <summary>No area, or not laid out yet (NaN).</summary>
        public bool IsEmpty
        {
            get { return !(W > 0f && H > 0f) || float.IsNaN(X) || float.IsNaN(Y); }
        }

        /// <summary>True when the rectangles come closer than <paramref name="gap"/> (empty ones never do).</summary>
        public bool Overlaps(in HudRect o, float gap = 0f)
        {
            if (IsEmpty || o.IsEmpty) return false;
            return X < o.Right + gap && o.X < Right + gap && Y < o.Bottom + gap && o.Y < Bottom + gap;
        }

        /// <summary>Area shared with <paramref name="o"/>.</summary>
        public float OverlapArea(in HudRect o)
        {
            if (IsEmpty || o.IsEmpty) return 0f;
            float w = Math.Min(Right, o.Right) - Math.Max(X, o.X);
            float h = Math.Min(Bottom, o.Bottom) - Math.Max(Y, o.Y);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        /// <summary>True when <paramref name="o"/> lies inside this rectangle (half a unit of slack).</summary>
        public bool Contains(in HudRect o)
        {
            return o.X >= X - 0.5f && o.Y >= Y - 0.5f && o.Right <= Right + 0.5f && o.Bottom <= Bottom + 0.5f;
        }

        public override string ToString()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return "(" + X.ToString("0", c) + ", " + Y.ToString("0", c) + ", " + W.ToString("0", c) + " x " + H.ToString("0", c) + ")";
        }
    }

    /// <summary>
    /// Where on screen the road ahead shows behind a chase camera, in normalised device coordinates (x −1 left … +1
    /// right, y −1 bottom … +1 top): a trapezoid from the explorer's ground point (<see cref="FootNdcY"/>, wide) up to
    /// the horizon or the top edge (<see cref="FarNdcY"/>, narrow). Below the explorer is the road already ridden, not
    /// ahead. <see cref="For"/> gives the envelope over every chase rig of W2_DESIGN 6.4 (near and far views, rest and
    /// full look-ahead) on 4:3 to 20:9 screens; <c>ExploreHudLayoutTests</c> checks the envelope against the rig maths.
    /// </summary>
    public struct RoadAheadZone
    {
        /// <summary>Screen height of the lowest ground point any rig pins the explorer at.</summary>
        public float FootNdcY;

        /// <summary>Half-width of the zone there (a 6 m street three wheels wide, plus room to steer).</summary>
        public float FootHalfWidthNdc;

        /// <summary>The far end: the highest horizon of any rig (landscape: the top edge).</summary>
        public float FarNdcY;

        /// <summary>Half-width at the far end (the street narrows to its vanishing point, plus bends).</summary>
        public float FarHalfWidthNdc;

        /// <summary>The envelope for an orientation.</summary>
        public static RoadAheadZone For(bool portrait)
        {
            return portrait
                ? new RoadAheadZone { FootNdcY = -0.62f, FootHalfWidthNdc = 0.86f, FarNdcY = 0.42f, FarHalfWidthNdc = 0.2f }
                : new RoadAheadZone { FootNdcY = -0.62f, FootHalfWidthNdc = 0.72f, FarNdcY = 1.0f, FarHalfWidthNdc = 0.2f };
        }

        /// <summary>Half-width of the zone at height <paramref name="ndcY"/> (0 outside it).</summary>
        public float HalfWidthAt(float ndcY)
        {
            if (ndcY < FootNdcY || ndcY > FarNdcY) return 0f;
            float t = FarNdcY > FootNdcY ? (ndcY - FootNdcY) / (FarNdcY - FootNdcY) : 0f;
            return FootHalfWidthNdc + (FarHalfWidthNdc - FootHalfWidthNdc) * t;
        }

        /// <summary>Area (in NDC², roughly) of <paramref name="r"/> that covers the zone on <paramref name="screen"/>;
        /// 0 when it stays clear.</summary>
        public float Cover(in HudRect r, in HudRect screen)
        {
            if (r.IsEmpty || screen.IsEmpty) return 0f;
            float x0 = (r.X - screen.X) / screen.W * 2f - 1f;
            float x1 = (r.Right - screen.X) / screen.W * 2f - 1f;
            float yTop = 1f - (r.Y - screen.Y) / screen.H * 2f;
            float yBottom = 1f - (r.Bottom - screen.Y) / screen.H * 2f;
            float lo = Math.Max(yBottom, FootNdcY), hi = Math.Min(yTop, FarNdcY);
            if (!(hi > lo)) return 0f;
            float hw = HalfWidthAt(lo); // widest where the overlap is lowest
            float w = Math.Min(x1, hw) - Math.Max(x0, -hw);
            return w > 0f ? w * (hi - lo) : 0f;
        }
    }

    /// <summary>Where the route chip went (<see cref="RouteChipLayout.Place"/>).</summary>
    public enum RouteChipSlot : byte
    {
        /// <summary>On the bottom edge, left of the speedometer and the map credit under it (landscape first choice).</summary>
        BesideSpeedoLeft = 0,

        /// <summary>On the bottom edge, right of the speedometer.</summary>
        BesideSpeedoRight = 1,

        /// <summary>Under the top bar on the left (portrait first choice).</summary>
        UnderTopBarLeft = 2,

        /// <summary>Under the top bar on the right, below the compass.</summary>
        UnderTopBarRight = 3,

        /// <summary>No slot was clear: the least bad one.</summary>
        Fallback = 4,
    }

    /// <summary>
    /// The HUD layout rules of the route chip (W2 detail pass; owner: "Place direction info somewhere else where it's
    /// convenient to look. Right now it blocks the view."): the chip with the turn arrow, the distance to the turn and
    /// the distance and time left goes to the first slot that
    /// <list type="number">
    /// <item>lies inside the safe area (notches, punch holes, rounded corners, the home indicator),</item>
    /// <item>keeps <see cref="Gap"/> from every other HUD element (speedometer, top bar, touch controls, prompt), and</item>
    /// <item>never covers the road ahead (<see cref="RoadAheadZone"/>);</item>
    /// </list>
    /// trying, in landscape, on the bottom edge beside the speedometer cluster (left, then right: below the explorer,
    /// where the road already ridden shows), then under the top bar; in portrait under the top bar (left, then right
    /// under the compass: above the horizon), then beside the speedometer. With no clear slot it takes the one that
    /// covers least. Engine-free and allocation-free; <c>ExploreScreen</c> feeds it the laid-out rectangles whenever the
    /// HUD's geometry changes.
    /// </summary>
    public static class RouteChipLayout
    {
        /// <summary>Clearance between the chip and other HUD elements, and to the safe-area edges, panel units.</summary>
        public const float Gap = 16f;

        /// <summary>The chip's distance below the top bar when the top bar is not laid out yet.</summary>
        public const float DefaultTopBarHeight = 130f;

        /// <summary>Half the speedometer cluster's width when it is not laid out yet.</summary>
        public const float DefaultSpeedoHalfWidth = 180f;

        private static readonly RouteChipSlot[] LandscapeOrder =
        {
            RouteChipSlot.BesideSpeedoLeft, RouteChipSlot.BesideSpeedoRight, RouteChipSlot.UnderTopBarLeft, RouteChipSlot.UnderTopBarRight,
        };

        private static readonly RouteChipSlot[] PortraitOrder =
        {
            RouteChipSlot.UnderTopBarLeft, RouteChipSlot.UnderTopBarRight, RouteChipSlot.BesideSpeedoLeft, RouteChipSlot.BesideSpeedoRight,
        };

        /// <summary>
        /// The chip's rectangle (panel units) for a chip of <paramref name="chipW"/> × <paramref name="chipH"/> on
        /// <paramref name="screen"/> (the whole panel, what the camera fills) with the safe area <paramref name="safe"/>,
        /// beside <paramref name="speedo"/> (the speedometer cluster: digits, surface chip and the map credit under them)
        /// and under <paramref name="topBar"/> (the union of the place pill and the buttons; either may be empty), keeping
        /// clear of the first <paramref name="count"/> rectangles of <paramref name="obstacles"/> (may be null).
        /// </summary>
        public static HudRect Place(in HudRect screen, in HudRect safe, bool portrait, float chipW, float chipH, in HudRect speedo,
                                    in HudRect topBar, HudRect[] obstacles, int count, out RouteChipSlot slot)
        {
            RoadAheadZone zone = RoadAheadZone.For(portrait);
            RouteChipSlot[] order = portrait ? PortraitOrder : LandscapeOrder;
            if (obstacles == null) count = 0;
            count = Math.Min(count, obstacles != null ? obstacles.Length : 0);
            HudRect best = default(HudRect);
            float bestPenalty = float.PositiveInfinity;
            for (int i = 0; i < order.Length; i++)
            {
                HudRect r = Candidate(order[i], safe, chipW, chipH, speedo, topBar);
                float penalty = Penalty(r, screen, safe, zone, speedo, topBar, obstacles, count);
                if (penalty <= 0f)
                {
                    slot = order[i];
                    return r;
                }
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    best = r;
                }
            }
            slot = RouteChipSlot.Fallback;
            return best;
        }

        /// <summary>The rectangle a slot puts the chip in: beside the speedometer cluster on the bottom edge
        /// (<see cref="Gap"/> above the safe area's bottom), or <see cref="Gap"/> under the top bar.</summary>
        public static HudRect Candidate(RouteChipSlot slot, in HudRect safe, float chipW, float chipH, in HudRect speedo, in HudRect topBar)
        {
            float bottom = safe.Bottom - Gap - chipH;
            float speedoLeft = !speedo.IsEmpty ? speedo.X : safe.X + 0.5f * safe.W - DefaultSpeedoHalfWidth;
            float speedoRight = !speedo.IsEmpty ? speedo.Right : safe.X + 0.5f * safe.W + DefaultSpeedoHalfWidth;
            float topBottom = !topBar.IsEmpty ? topBar.Bottom : safe.Y + DefaultTopBarHeight;
            switch (slot)
            {
                case RouteChipSlot.BesideSpeedoLeft:
                    return new HudRect(speedoLeft - Gap - chipW, bottom, chipW, chipH);
                case RouteChipSlot.BesideSpeedoRight:
                    return new HudRect(speedoRight + Gap, bottom, chipW, chipH);
                case RouteChipSlot.UnderTopBarRight:
                    return new HudRect(safe.Right - Gap - chipW, topBottom + Gap, chipW, chipH);
                default:
                    return new HudRect(safe.X + Gap, topBottom + Gap, chipW, chipH);
            }
        }

        /// <summary>0 when <paramref name="r"/> keeps every rule, else how badly it breaks them (outside the safe area
        /// weighs most, then covering the road ahead, then overlapping other HUD).</summary>
        public static float Penalty(in HudRect r, in HudRect screen, in HudRect safe, in RoadAheadZone zone, in HudRect speedo, in HudRect topBar,
                                    HudRect[] obstacles, int count)
        {
            if (r.IsEmpty) return float.PositiveInfinity;
            float penalty = 0f;
            HudRect inner = HudRect.Edges(safe.X + 0.5f * Gap, safe.Y, safe.Right - 0.5f * Gap, safe.Bottom);
            if (!inner.Contains(r)) penalty += 1e6f + (r.W * r.H - r.OverlapArea(inner));
            float cover = zone.Cover(r, screen);
            if (cover > 0f) penalty += 1e4f * (1f + cover);
            if (r.Overlaps(speedo, 0.5f * Gap)) penalty += 100f + r.OverlapArea(speedo);
            if (r.Overlaps(topBar, 0.5f * Gap)) penalty += 100f + r.OverlapArea(topBar);
            for (int i = 0; i < count; i++)
            {
                if (r.Overlaps(obstacles[i], 0.5f * Gap)) penalty += 100f + r.OverlapArea(obstacles[i]);
            }
            return penalty;
        }
    }
}
