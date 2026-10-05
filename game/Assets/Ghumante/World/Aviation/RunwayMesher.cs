using System;
using System.Collections.Generic;
using Ghumante.Core.Aviation;
using Ghumante.Core.Meshing;

namespace Ghumante.World.Aviation
{
    /// <summary>One airport light (game metres, absolute Y) and its colour (sRGB hex).</summary>
    public struct AirportLight
    {
        public double X, Z;
        public float Y;
        public uint Colour;

        /// <summary>Size of the stud in metres.</summary>
        public float Size;
    }

    /// <summary>
    /// The TIA runway as a flattened corridor (W2_DESIGN 8.1): the paved strip from pavement end to pavement end, 45 m
    /// wide on the true heading between the real thresholds, its profile a straight line between the threshold
    /// elevations (the displaced sections continue the slope), raised wherever the drawn terrain pokes above it, with
    /// markings (threshold bars, centreline dashes 30 / 20 m, touchdown zone, edge stripes, the designators "02" and "20"
    /// as stroke digits) and the lights: HIRL edge lights every 60 m (white, yellow on the last 600 m), green threshold and
    /// red end bars, and the 870 m approach lights south of threshold 02 among the Koteshwor houses. Mesh positions are
    /// relative to <see cref="OriginX"/> / <see cref="OriginZ"/> (threshold 02) with absolute Y. Engine-free.
    /// </summary>
    public sealed class RunwayMesher
    {
        public const float HirlSpacingM = 60f, HirlCautionM = 600f, ApproachLengthM = 870f, ApproachSpacingM = 30f;
        public const float SurfaceLiftM = 0.3f;

        private const uint Asphalt = 0x4A4D52FF, Shoulder = 0x6B6A66FF, Paint = 0xF2F0E8FF;

        public double OriginX, OriginZ;

        /// <summary>Unit vector along the runway from 02 to 20 and its left normal.</summary>
        public double DirX, DirZ;

        /// <summary>Along distance of each pavement end and threshold from threshold 02.</summary>
        public double SouthEndS, NorthEndS, Threshold20S;

        private float _y02, _y20;
        private IHeightSampler _ground;

        /// <summary>Builds the runway into <paramref name="m"/> and its lights into <paramref name="lights"/>.
        /// <paramref name="ground"/> (game metres) lifts the strip over the terrain where needed; may be null.</summary>
        public int Build(AviationConfig c, IHeightSampler ground, MeshData m, List<AirportLight> lights)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (m == null) throw new ArgumentNullException(nameof(m));
            int t0 = m.TriangleCount;
            _ground = ground;
            OriginX = c.Threshold02.X;
            OriginZ = c.Threshold02.Z;
            double dx = c.Threshold20.X - OriginX, dz = c.Threshold20.Z - OriginZ;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 100) return 0;
            DirX = dx / len;
            DirZ = dz / len;
            Threshold20S = len;
            SouthEndS = Along(c.PavementSouthX, c.PavementSouthZ);
            NorthEndS = Along(c.PavementNorthX, c.PavementNorthZ);
            if (!(SouthEndS < 0)) SouthEndS = -285;
            if (!(NorthEndS > len)) NorthEndS = len + 277;
            _y02 = c.Threshold02.ElevM;
            _y20 = c.Threshold20.ElevM;
            float hw = 0.5f * c.RunwayWidthM;

            // Paved strip in 50 m sections with 7.5 m shoulders.
            const double step = 50.0;
            for (double s = SouthEndS; s < NorthEndS - 1e-6; s += step)
            {
                double e = Math.Min(NorthEndS, s + step);
                Strip(m, s, e, -hw, hw, 0f, Asphalt);
                Strip(m, s, e, -hw - 7.5, -hw, -0.05f, Shoulder);
                Strip(m, s, e, hw, hw + 7.5, -0.05f, Shoulder);
            }
            // Markings: threshold bars, designators, touchdown zones, centreline, edge stripes.
            ThresholdBars(m, 0, 1, hw);
            ThresholdBars(m, len, -1, hw);
            Designator(m, 18 + 30, 1, "02");
            Designator(m, len - 18 - 30, -1, "20");
            for (int k = 0; k < 3; k++)
            {
                Touchdown(m, 150 + k * 150, hw, 1);
                Touchdown(m, len - 150 - k * 150, hw, -1);
            }
            for (double s = 90; s + 30 < len - 90; s += 50) Strip(m, s, s + 30, -0.45, 0.45, 0.02f, Paint);
            Strip(m, SouthEndS, NorthEndS, -hw + 0.5, -hw + 1.4, 0.02f, Paint);
            Strip(m, SouthEndS, NorthEndS, hw - 1.4, hw - 0.5, 0.02f, Paint);
            // Displaced-threshold arrows (chevrons) on both displaced sections.
            for (double s = SouthEndS + 40; s < -30; s += 60) Arrow(m, s, 1);
            for (double s = NorthEndS - 40; s > len + 30; s -= 60) Arrow(m, s, -1);

            if (lights != null) Lights(c, hw, len, lights);
            return m.TriangleCount - t0;
        }

        /// <summary>Along distance from threshold 02 of a game point.</summary>
        public double Along(double x, double z)
        {
            return (x - OriginX) * DirX + (z - OriginZ) * DirZ;
        }

        /// <summary>Runway surface height at along distance s (the straight profile, raised over the terrain).</summary>
        public float HeightAt(double s, double side = 0)
        {
            float y = (float)(_y02 + (_y20 - _y02) * (s / Math.Max(1.0, Threshold20S)));
            if (_ground != null)
            {
                double x, z;
                Point(s, side, out x, out z);
                float g;
                if (_ground.TryHeight(x, z, out g) && g + 0.05f > y) y = g + 0.05f;
            }
            return y + SurfaceLiftM;
        }

        private void Point(double s, double side, out double x, out double z)
        {
            // Left normal of (DirX, DirZ) is (−DirZ, DirX).
            x = OriginX + DirX * s - DirZ * side;
            z = OriginZ + DirZ * s + DirX * side;
        }

        private void Strip(MeshData m, double s0, double s1, double a0, double a1, float lift, uint c)
        {
            double x0, z0, x1, z1, x2, z2, x3, z3;
            Point(s0, a0, out x0, out z0);
            Point(s1, a0, out x1, out z1);
            Point(s1, a1, out x2, out z2);
            Point(s0, a1, out x3, out z3);
            float y0 = HeightAt(s0, 0.5 * (a0 + a1)) + lift, y1 = HeightAt(s1, 0.5 * (a0 + a1)) + lift;
            MeshKit.Quad(m, x0 - OriginX, y0, z0 - OriginZ, x1 - OriginX, y1, z1 - OriginZ, x2 - OriginX, y1, z2 - OriginZ,
                         x3 - OriginX, y0, z3 - OriginZ, 0, 1, 0, c);
        }

        private void ThresholdBars(MeshData m, double s, int dir, float hw)
        {
            // 8 bars each side of the centreline, 1.8 m wide, 30 m long, starting 6 m in.
            for (int k = 0; k < 8; k++)
            {
                double a = 3 + k * 2.7;
                if (a + 1.8 > hw - 2) break;
                double s0 = s + dir * 6, s1 = s + dir * 36;
                Strip(m, Math.Min(s0, s1), Math.Max(s0, s1), a, a + 1.8, 0.02f, Paint);
                Strip(m, Math.Min(s0, s1), Math.Max(s0, s1), -a - 1.8, -a, 0.02f, Paint);
            }
            Strip(m, Math.Min(s, s + dir * 1.8), Math.Max(s, s + dir * 1.8), -hw + 1.5, hw - 1.5, 0.02f, Paint);
        }

        private void Touchdown(MeshData m, double s, float hw, int dir)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                {
                    double a0 = side * (6 + k * 3.0), a1 = a0 + side * 1.8;
                    Strip(m, Math.Min(s, s + 22.5 * dir), Math.Max(s, s + 22.5 * dir), Math.Min(a0, a1), Math.Max(a0, a1), 0.02f, Paint);
                }
        }

        /// <summary>A displaced-threshold arrow: a 12 m shaft and a head pointing along <paramref name="dir"/>.</summary>
        private void Arrow(MeshData m, double s, int dir)
        {
            Strip(m, s - 6, s + 6, -0.45, 0.45, 0.02f, Paint);
            double tip = s + dir * 9, back = s + dir * 4;
            for (int side = -1; side <= 1; side += 2)
            {
                double x0, z0, x1, z1;
                Point(tip, 0, out x0, out z0);
                Point(back, side * 3.0, out x1, out z1);
                float y = HeightAt(s) + 0.02f;
                MeshKit.Bar(m, x0 - OriginX, y, z0 - OriginZ, x1 - OriginX, y, z1 - OriginZ, 0.6, Paint);
            }
        }

        // Stroke digits on a 3 × 5 grid (9 m wide, 18 m long) read from the approach end.
        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        private void Designator(MeshData m, double s, int dir, string text)
        {
            const double cell = 3.0, gap = 3.0, width = 3 * cell;
            double total = text.Length * width + (text.Length - 1) * gap;
            for (int d = 0; d < text.Length; d++)
            {
                string g = Digits[text[d] - '0'];
                // Left to right as seen by a pilot facing along dir: the pilot's right is −side for dir +1.
                double left = -0.5 * total + d * (width + gap);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (g[row * 3 + col] != '1') continue;
                        double a0 = -(left + col * cell + cell) * dir, a1 = -(left + col * cell) * dir;
                        double r0 = s + dir * (5 - row - 1) * cell * 1.2, r1 = r0 + dir * cell * 1.2;
                        Strip(m, Math.Min(r0, r1), Math.Max(r0, r1), Math.Min(a0, a1), Math.Max(a0, a1), 0.02f, Paint);
                    }
            }
        }

        private void Lights(AviationConfig c, float hw, double len, List<AirportLight> lights)
        {
            const uint white = 0xFFF6E0, yellow = 0xFFC93C, green = 0x3DFF6A, red = 0xFF3B30;
            // HIRL edge lights.
            for (double s = SouthEndS; s <= NorthEndS + 1e-6; s += HirlSpacingM)
            {
                bool caution02 = s > len - HirlCautionM; // landing on 02 sees yellow in its last 600 m
                for (int side = -1; side <= 1; side += 2) Add(lights, s, side * (hw + 1.5), caution02 ? yellow : white, 0.6f);
            }
            // Threshold (green) and end (red) bars.
            for (double a = -hw; a <= hw + 1e-6; a += 3)
            {
                Add(lights, -1.5, a, green, 0.6f);
                Add(lights, len + 1.5, a, green, 0.6f);
                Add(lights, SouthEndS - 1.0, a, red, 0.6f);
                Add(lights, NorthEndS + 1.0, a, red, 0.6f);
            }
            // HIALS on 02: a centre row every 30 m to 870 m with crossbars every 150 m.
            for (double d = ApproachSpacingM; d <= ApproachLengthM + 1e-6; d += ApproachSpacingM)
            {
                double s = SouthEndS - d;
                for (int k = -2; k <= 2; k++) Add(lights, s, k * 1.0, white, 0.8f, 6f);
                if (Math.Abs(d % 150) < 1e-3)
                    for (int k = -7; k <= 7; k++) Add(lights, s, k * 1.5, white, 0.8f, 6f);
            }
        }

        private void Add(List<AirportLight> lights, double s, double side, uint colour, float size, float mastM = 0.4f)
        {
            double x, z;
            Point(s, side, out x, out z);
            float y = s >= SouthEndS && s <= NorthEndS ? HeightAt(s, side) : GroundAt(x, z, s);
            lights.Add(new AirportLight { X = x, Z = z, Y = y + mastM, Colour = colour, Size = size });
        }

        private float GroundAt(double x, double z, double s)
        {
            float g;
            if (_ground != null && _ground.TryHeight(x, z, out g)) return g;
            return HeightAt(s);
        }
    }
}
