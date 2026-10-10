using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    public static partial class VehicleMesher
    {
        /// <summary>Where a family hung its plates (vehicle frame, plate centre); set by the family builders, read by
        /// <see cref="Plates"/>. A zero height means "use the default for the shape".</summary>
        internal struct PlateSpots
        {
            public float RearY, RearZ, FrontY, FrontZ, RearTilt, FrontTilt;
            public bool NoFront;
        }

        [ThreadStatic] private static PlateSpots s_spots;

        /// <summary>Records the plate positions of the vehicle being built (rear centre, front centre; tilt in radians,
        /// positive leans the plate face up).</summary>
        internal static void SetPlates(float rearY, float rearZ, float frontY, float frontZ, float rearTilt = 0f, float frontTilt = 0f,
                                       bool noFront = false)
        {
            s_spots = new PlateSpots
            {
                RearY = rearY, RearZ = rearZ, FrontY = frontY, FrontZ = frontZ, RearTilt = rearTilt, FrontTilt = frontTilt, NoFront = noFront,
            };
        }

        private static void Plates(ref Ctx k, in VehicleCatalogEntry e, Dims d, uint seed)
        {
            PlateText p = VehiclePlates.For(e, seed);
            bool numbers = k.Lod == VehicleLod.Lod0;
            PlateSpots s = s_spots;
            s_spots = default(PlateSpots);
            float zr = -d.Rear, zf = d.Wheelbase + d.Front;
            if (s.RearY <= 0f)
            {
                s.RearY = IsTwo(e.Shape) ? d.WheelR + 0.3f : Math.Max(0.35f, 0.75f * d.WheelR + 0.2f);
                s.RearZ = zr - 0.02f;
                s.FrontY = Math.Max(0.3f, 0.6f * d.WheelR + 0.15f);
                s.FrontZ = zf + 0.02f;
            }
            PlateAt(ref k, p, s.RearZ, s.RearY, false, p.WidthM, p.HeightM, true, numbers, s.RearTilt);
            if (s.NoFront) return;
            if (p.FrontWidthM > 0f)
                PlateAt(ref k, p, s.FrontZ, s.FrontY, true, p.FrontWidthM, p.FrontHeightM, false, numbers, s.FrontTilt);
            else if (IsTwo(e.Shape) && e.Shape != BodyShape.Bicycle)
                PlateAt(ref k, p, s.FrontZ, s.FrontY, true, 0.17f, 0.085f, false, numbers, s.FrontTilt); // small front plate on the mudguard
        }

        private static void PlateAt(ref Ctx k, PlateText p, float z, float y, bool front, float w, float h, bool twoLines, bool numbers, float tilt)
        {
            MeshData m = k.M;
            int v0 = m.VertexCount;
            Plate(m, p, z, y, front, w, h, twoLines, numbers);
            if (Math.Abs(tilt) > 1e-4f)
            {
                // Lean the plate about its own horizontal axis.
                Affine3 xf = T(0, y, z) * Affine3.RotationX(front ? -tilt : tilt) * T(0, -y, -z);
                Transform(m, v0, xf);
            }
        }

        /// <summary>Applies <paramref name="xf"/> to the vertices from <paramref name="from"/> to the end (rigid
        /// transforms: normals rotate with it).</summary>
        internal static void Transform(MeshData m, int from, in Affine3 xf)
        {
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x, y, z, nx, ny, nz;
                xf.Point(p[3 * v], p[3 * v + 1], p[3 * v + 2], out x, out y, out z);
                xf.Normal(n[3 * v], n[3 * v + 1], n[3 * v + 2], out nx, out ny, out nz);
                p[3 * v] = (float)x;
                p[3 * v + 1] = (float)y;
                p[3 * v + 2] = (float)z;
                Norm(n, v, nx, ny, nz);
            }
        }

        /// <summary>A plate on the front (+Z) or rear at z, centred at height y: a rounded plate with a raised rim and, at
        /// LOD0, the number in strokes (two lines on rear plates, one on front plates). Returns the triangles added.</summary>
        public static int Plate(MeshData m, PlateText p, float z, float y, bool front, float w, float h, bool twoLines, bool numbers)
        {
            int before = m.IndexCount / 3;
            uint bg = MeshColor.FromHex(p.Background), ink = MeshColor.FromHex(p.Ink);
            var c = new Ctx { M = m, Lod = numbers ? VehicleLod.Lod0 : VehicleLod.Lod1, S = new ShapeLod(numbers ? 0 : 1) };
            float dir = front ? 1f : -1f;
            // Plate body (thin rounded slab), a thin rim in the ink colour (as painted plates have), then the text; far
            // plates are one flat panel.
            if (!numbers)
            {
                Decal(ref c, Paint(bg), 0, y, z, 0, 0, dir, w, h, 0.01, 0.004);
                return m.IndexCount / 3 - before;
            }
            Panel(ref c, Paint(ink), 0, y, z, 0, 0, dir, w, h, 0.012, 0.004);
            Panel(ref c, Paint(bg), 0, y, z + dir * 0.004f, 0, 0, dir, w - 0.012, h - 0.012, 0.008, 0.003);
            float tz = z + dir * 0.0075f;
            if (twoLines && h >= 0.12f)
            {
                Text(m, p.Line1, tz, front, y + 0.22f * h, w * 0.86f, h * 0.34f, ink);
                Text(m, p.Line2, tz, front, y - 0.22f * h, w * 0.86f, h * 0.34f, ink);
            }
            else
            {
                Text(m, p.Full, tz, front, y, w * 0.9f, h * 0.66f, ink);
            }
            return m.IndexCount / 3 - before;
        }

        /// <summary>A line of glyph strokes centred at (0, y) on the plane z, fitted into maxW × maxH.</summary>
        private static int Text(MeshData m, string s, float z, bool front, float y, float maxW, float maxH, uint ink)
        {
            float units = PlateGlyphs.Width(s);
            if (units <= 0f) return 0;
            float scale = Math.Min(maxW / units, maxH / PlateGlyphs.CellH);
            float stroke = 0.65f * scale;
            float xu = -0.5f * units; // glyph units
            int t = 0;
            // A reader behind the vehicle looks along +Z, so their right is the vehicle's +X; in front they look along
            // −Z and their right is −X: mirror the strokes on front plates so both read.
            float dir = front ? -1f : 1f;
            float nz = front ? 1f : -1f;
            foreach (char ch in s)
            {
                float[] st = PlateGlyphs.Of(ch);
                for (int i = 0; i + 3 < st.Length; i += 4)
                {
                    float x0 = dir * (xu + st[i]) * scale, y0 = y + (st[i + 1] - 0.5f * PlateGlyphs.CellH) * scale;
                    float x1 = dir * (xu + st[i + 2]) * scale, y1 = y + (st[i + 3] - 0.5f * PlateGlyphs.CellH) * scale;
                    float dx = x1 - x0, dy = y1 - y0, l = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-6f)
                    {
                        dx = 1f;
                        dy = 0f;
                        l = 1f;
                    }
                    float px = -dy / l * 0.5f * stroke, py = dx / l * 0.5f * stroke;
                    float ex = dx / l * 0.5f * stroke, ey = dy / l * 0.5f * stroke; // square caps
                    int a = m.AddVertex(x0 - ex + px, y0 - ey + py, z, 0, 0, nz, ink, (float)MaterialChannel.Paint, 1f);
                    int b = m.AddVertex(x1 + ex + px, y1 + ey + py, z, 0, 0, nz, ink, (float)MaterialChannel.Paint, 1f);
                    int cc = m.AddVertex(x1 + ex - px, y1 + ey - py, z, 0, 0, nz, ink, (float)MaterialChannel.Paint, 1f);
                    int dd = m.AddVertex(x0 - ex - px, y0 - ey - py, z, 0, 0, nz, ink, (float)MaterialChannel.Paint, 1f);
                    ShapeEmit.TriOriented(m, a, b, cc);
                    ShapeEmit.TriOriented(m, a, cc, dd);
                    t += 2;
                }
                xu += PlateGlyphs.AdvanceOf(ch);
            }
            return t;
        }
    }
}
