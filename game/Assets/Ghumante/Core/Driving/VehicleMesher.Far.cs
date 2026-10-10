using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Driving
{
    public static partial class VehicleMesher
    {
        /// <summary>An axis-aligned box [x0, x1] × [y0, y1] × [z0, z1] with flat normals, UV0 = (channel, AO)
        /// (12 triangles): the parked block-out and box levels.</summary>
        internal static void FlatBox(MeshData m, double x0, double x1, double y0, double y1, double z0, double z1, uint color, MaterialChannel ch,
                                     float ao = 1f)
        {
            float u = (float)ch;
            for (int f = 0; f < 6; f++)
            {
                int axis = f >> 1;
                double s = (f & 1) == 0 ? -1 : 1;
                double nx = axis == 0 ? s : 0, ny = axis == 1 ? s : 0, nz = axis == 2 ? s : 0;
                // Corners of the face in a fixed order; TriOriented fixes the winding.
                double[] q = FaceQuad(axis, s, x0, x1, y0, y1, z0, z1);
                float a = (float)(ny < 0 ? ao * 0.6f : ao);
                int v0 = m.AddVertex((float)q[0], (float)q[1], (float)q[2], (float)nx, (float)ny, (float)nz, color, u, a);
                int v1 = m.AddVertex((float)q[3], (float)q[4], (float)q[5], (float)nx, (float)ny, (float)nz, color, u, a);
                int v2 = m.AddVertex((float)q[6], (float)q[7], (float)q[8], (float)nx, (float)ny, (float)nz, color, u, a);
                int v3 = m.AddVertex((float)q[9], (float)q[10], (float)q[11], (float)nx, (float)ny, (float)nz, color, u, a);
                Meshing.Shapes.ShapeEmit.TriOriented(m, v0, v1, v2);
                Meshing.Shapes.ShapeEmit.TriOriented(m, v0, v2, v3);
            }
        }

        [ThreadStatic] private static double[] s_quad;

        private static double[] FaceQuad(int axis, double s, double x0, double x1, double y0, double y1, double z0, double z1)
        {
            double[] q = s_quad ?? (s_quad = new double[12]);
            for (int k = 0; k < 4; k++)
            {
                double a = (k == 1 || k == 2) ? 1 : 0, b = k >= 2 ? 1 : 0;
                double x, y, z;
                switch (axis)
                {
                    case 0:
                        x = s < 0 ? x0 : x1;
                        y = y0 + (y1 - y0) * a;
                        z = z0 + (z1 - z0) * b;
                        break;
                    case 1:
                        y = s < 0 ? y0 : y1;
                        x = x0 + (x1 - x0) * a;
                        z = z0 + (z1 - z0) * b;
                        break;
                    default:
                        z = s < 0 ? z0 : z1;
                        x = x0 + (x1 - x0) * a;
                        y = y0 + (y1 - y0) * b;
                        break;
                }
                q[3 * k] = x;
                q[3 * k + 1] = y;
                q[3 * k + 2] = z;
            }
            return q;
        }

        /// <summary>A single palette-tinted box (12 triangles), parked 80–150 m.</summary>
        private static void BuildBox(in VehicleCatalogEntry e, Dims d, in Livery p, MeshData m)
        {
            bool two = IsTwo(e.Shape);
            double hw = two ? 0.2 : d.Half;
            FlatBox(m, -hw, hw, 0.1, d.Height * (two ? 0.9 : 1.0), -d.Rear, d.Wheelbase + d.Front, p.Body, MaterialChannel.Paint);
        }

        /// <summary>Parked block-out (≤ 80 triangles): body, cabin or seat, wheel boxes (tandem pairs merged).</summary>
        private static void BuildBlock(in VehicleCatalogEntry e, Dims d, in Livery p, MeshData m)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half;
            if (IsTwo(e.Shape) || e.Shape == BodyShape.Rickshaw)
            {
                FlatBox(m, -0.17, 0.17, d.WheelR * 0.9, d.WheelR + 0.45, zr + 0.1, zf - 0.15, p.Body, MaterialChannel.Paint);
                FlatBox(m, -0.15, 0.15, 0.78, 0.86, 0.0, 0.62, SeatC, MaterialChannel.Leather);
            }
            else
            {
                double belt = (e.Shape == BodyShape.Bus || e.Shape == BodyShape.Van) ? 0.9 * d.Height : 0.55 * d.Height;
                FlatBox(m, -hw, hw, 0.45 * d.WheelR, belt, zr, zf, p.Body, MaterialChannel.Paint);
                if (belt < 0.85 * d.Height)
                {
                    double c0 = zr + 0.15 * (zf - zr), c1 = zf - 0.3 * (zf - zr);
                    if (e.Shape == BodyShape.Truck || e.Shape == BodyShape.Tipper || e.Shape == BodyShape.Tanker)
                    {
                        c0 = zf - 1.7;
                        c1 = zf - 0.1;
                    }
                    FlatBox(m, -hw * 0.9, hw * 0.9, belt, d.Height, c0, c1, e.Shape == BodyShape.Tractor ? p.Body : GlassC, MaterialChannel.Glass);
                }
                else FlatBox(m, -hw - 0.005, hw + 0.005, 0.55 * d.Height, 0.82 * d.Height, zr + 0.3, zf - 0.2, GlassC, MaterialChannel.Glass);
            }
            WheelSocket[] ws = Wheels(e);
            for (int i = 0; i < ws.Length; i++)
            {
                WheelSocket w = ws[i];
                double z0 = w.Z - w.Radius * 0.85, z1 = w.Z + w.Radius * 0.85;
                bool merged = false;
                for (int j = 0; j < ws.Length; j++)
                {
                    if (j == i || ws[j].X != w.X || Math.Abs(ws[j].Z - w.Z) > 1.5f) continue;
                    if (j < i)
                    {
                        merged = true;
                        break;
                    }
                    z0 = Math.Min(z0, ws[j].Z - ws[j].Radius * 0.85);
                    z1 = Math.Max(z1, ws[j].Z + ws[j].Radius * 0.85);
                }
                if (merged) continue;
                FlatBox(m, w.X - 0.5 * w.Width, w.X + 0.5 * w.Width, 0, 2 * w.Radius * 0.92, z0, z1, TyreC, MaterialChannel.Rubber, 0.8f);
            }
        }
    }
}
