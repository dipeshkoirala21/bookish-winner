using System;
using Ghumante.Core.Aviation;
using Ghumante.Core.Meshing;
using Ghumante.World.Instancing;

namespace Ghumante.World.Aviation
{
    /// <summary>
    /// Generic cartoon aircraft for TIA (W2_DESIGN 8.5), built in code: the high-wing T-tail turboprop with two 6-blade
    /// propeller discs (and its stretched preset), the strutted STOL with fixed gear, the twin-fan narrow-body and
    /// wide-body (fuselage radius × 1.2, big nacelles, wrap-around cockpit visor) and the single-engine helicopter with
    /// skids and a rotor blur disc. No text, registrations, logos or flags: the body is white, the tail fin and the
    /// cheat line carry the livery colour as the per-instance tint (vertex alpha 255). Frame: origin on the ground under
    /// the fuselage centre, +Z nose, +X right wing, +Y up. LOD0 ≤ 8,000, LOD1 ≤ 2,500, LOD2 ≤ 800 triangles.
    /// Engine-free, deterministic.
    /// </summary>
    public static class AircraftMesher
    {
        public static readonly int[] Budget = { 8000, 2500, 800 };

        private const uint Body = 0xF4F6F800, Belly = 0xC9CED400, Window = 0x22303D00, Engine = 0xB9BEC300, Dark = 0x3A3F4500;
        private const uint Livery = 0xFFFFFFFF, Prop = 0x5A5F6600, Gear = 0x2B2F3300;

        /// <summary>Overall box (length, span, height) per class (W2_DESIGN 8.5).</summary>
        public static void Dims(AircraftClass c, out float length, out float span, out float height)
        {
            switch (c)
            {
                case AircraftClass.TurbopropStretch: length = 32.8f; span = 28.4f; height = 8.3f; return;
                case AircraftClass.Stol: length = 15.8f; span = 19.8f; height = 5.9f; return;
                case AircraftClass.Narrowbody: length = 38f; span = 36f; height = 12f; return;
                case AircraftClass.Widebody: length = 60f; span = 60f; height = 17f; return;
                case AircraftClass.Helicopter: length = 12.9f; span = 10.7f; height = 3.3f; return;
                default: length = 27f; span = 27f; height = 7.7f; return;
            }
        }

        /// <summary>Builds one class at LOD 0-2; returns the triangles added.</summary>
        public static int Build(AircraftClass c, int lod, MeshData m)
        {
            int t0 = m.TriangleCount;
            int n = lod == 0 ? 16 : lod == 1 ? 10 : 6;
            float len, span, h;
            Dims(c, out len, out span, out h);
            switch (c)
            {
                case AircraftClass.Helicopter:
                    Helicopter(lod, n, m);
                    break;
                case AircraftClass.Narrowbody:
                case AircraftClass.Widebody:
                    Jet(c == AircraftClass.Widebody, len, span, h, lod, n, m);
                    break;
                default:
                    Turboprop(c == AircraftClass.Stol, len, span, h, lod, n, m);
                    break;
            }
            return m.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------

        private static void Turboprop(bool stol, float len, float span, float h, int lod, int n, MeshData m)
        {
            double r = stol ? 0.85 : 1.35 * 1.15; // fuselage radius × 1.15
            double cy = stol ? 1.2 : 1.9;
            double z0 = -len * 0.5, z1 = len * 0.5;
            // Fuselage: rounder nose, tapering tail cone.
            double[] zs = { z1, z1 - 0.6, z1 - 2.2, z0 + len * 0.35, z0 + 3.5, z0 };
            double[] rs = { 0.15, r * 0.7, r, r, r * 0.6, r * 0.12 };
            double[] ys = { cy - r * 0.15, cy - r * 0.08, cy, cy, cy + r * 0.35, cy + r * 0.8 };
            Tube(m, zs, rs, ys, n, Body);
            if (lod <= 1) Windows(m, cy + r * 0.25, r, z0 + 5, z1 - 4, stol ? 4 : 10, lod);
            double wingY = cy + r * 0.95, chord = stol ? 1.8 : 2.6, wz = z0 + len * 0.55;
            Plate(m, -span * 0.5, span * 0.5, wingY, wz - chord, wz, 0.28 * 1.3, Body);
            // T-tail (fin tinted) and stabiliser on top.
            double tz = z0 + 1.2;
            Fin(m, tz, tz + 3.5, cy + r * 0.6, h, 0.25, Livery);
            Plate(m, -span * (stol ? 0.18 : 0.16), span * (stol ? 0.18 : 0.16), stol ? cy + 1.6 : h - 0.2, tz, tz + 2.0, 0.18, Body);
            // Engines and propeller blur discs (×1.1).
            if (!stol)
                for (int s = -1; s <= 1; s += 2)
                {
                    double ex = s * span * 0.2;
                    int v0 = m.VertexCount;
                    Tube(m, new[] { wz + 1.4, wz - 3.6 }, new[] { 0.55, 0.45 }, new[] { wingY - 0.35, wingY - 0.35 }, Math.Max(6, n / 2), Engine);
                    ShiftFrom(m, v0, ex);
                    Disc(m, ex, wingY - 0.35, wz + 1.5, 1.95 * 1.1, n, Prop);
                }
            else
            {
                Disc(m, 0, cy, z1 + 0.05, 1.25, n, Prop);
                // Wing struts and fixed gear.
                for (int s = -1; s <= 1; s += 2)
                {
                    MeshKit.Bar(m, s * 0.6, cy - r * 0.6, wz - 1.0, s * span * 0.28, wingY - 0.1, wz - 1.0, 0.1, Dark);
                    MeshKit.Bar(m, s * 0.5, cy - r, wz - 1.5, s * 1.2, 0.4, wz - 1.5, 0.1, Gear);
                    WheelBox(m, s * 1.2, wz - 1.5, 0.4);
                }
                MeshKit.Bar(m, 0, cy - r, z1 - 2.0, 0, 0.35, z1 - 2.0, 0.1, Gear);
                WheelBox(m, 0, z1 - 2.0, 0.35);
            }
            if (!stol) Gears(m, cy - r, z1 - 3.0, wz - 1.5, 1.6, 0.45);
            CheatLine(m, cy, r, z0 + 4, z1 - 3, n);
        }

        private static void Jet(bool wide, float len, float span, float h, int lod, int n, MeshData m)
        {
            double r = (wide ? 2.9 : 1.95) * 1.2;
            double cy = r + (wide ? 2.4 : 1.35); // nacelles clear the ground
            double z0 = -len * 0.5, z1 = len * 0.5;
            double[] zs = { z1, z1 - 1.2, z1 - 4.0, z0 + len * 0.3, z0 + 5.0, z0 };
            double[] rs = { 0.2, r * 0.75, r, r, r * 0.55, r * 0.15 };
            double[] ys = { cy - r * 0.2, cy - r * 0.1, cy, cy, cy + r * 0.3, cy + r * 0.75 };
            Tube(m, zs, rs, ys, n, Body);
            if (wide) Tube(m, new[] { z1 - 4.5, z0 + 6 }, new[] { r * 1.01, r * 1.01 }, new[] { cy - r * 0.05, cy - r * 0.05 }, n, Belly);
            // Wrap-around cockpit visor.
            Box(m, -r * 0.55, r * 0.55, cy + r * 0.2, cy + r * 0.55, z1 - 3.2, z1 - 1.6, Window);
            if (lod <= 1) Windows(m, cy + r * 0.3, r, z0 + 7, z1 - 5, wide ? 24 : 14, lod);
            double wingY = cy - r * 0.6, chord = wide ? 8.5 : 5.5, wz = z0 + len * 0.52;
            // Swept wings: a plate per side, root to tip moved aft.
            for (int s = -1; s <= 1; s += 2)
            {
                double sweep = span * 0.5 * 0.45;
                MeshKit.Quad(m, s * r * 0.8, wingY + 0.25, wz, s * span * 0.5, wingY + 0.9, wz - sweep, s * span * 0.5, wingY + 0.9, wz - sweep - chord * 0.3,
                             s * r * 0.8, wingY + 0.25, wz - chord, 0, 1, 0, Body);
                MeshKit.Quad(m, s * r * 0.8, wingY - 0.15, wz, s * span * 0.5, wingY + 0.7, wz - sweep, s * span * 0.5, wingY + 0.7, wz - sweep - chord * 0.3,
                             s * r * 0.8, wingY - 0.15, wz - chord, 0, -1, 0, Belly);
                // Nacelle (× 1.25) under the wing with a spinner.
                double ex = s * span * (wide ? 0.2 : 0.17), nr = (wide ? 1.7 : 1.05) * 1.25;
                double ez = wz + nr * 1.2 - span * 0.5 * 0.45 * (Math.Abs(ex) / (span * 0.5));
                int v0 = m.VertexCount;
                Tube(m, new[] { ez + 0.2, ez - nr * 2.6 }, new[] { nr, nr * 0.75 }, new[] { wingY - nr * 0.6, wingY - nr * 0.6 }, Math.Max(8, n), Engine);
                ShiftFrom(m, v0, ex);
                Disc(m, ex, wingY - nr * 0.6, ez + 0.21, nr * 0.85, Math.Max(8, n), Dark);
            }
            double tz = z0 + 1.0;
            Fin(m, tz, tz + (wide ? 7.5 : 5.5), cy + r * 0.5, h, 0.35, Livery);
            Plate(m, -span * 0.2, span * 0.2, cy + r * 0.45, tz, tz + (wide ? 5.0 : 3.6), 0.25, Body);
            Gears(m, cy - r, z1 - 5.0, wz - chord * 0.6, r * 0.9, wide ? 0.65 : 0.5);
            CheatLine(m, cy, r, z0 + 6, z1 - 4, n);
        }

        private static void Helicopter(int lod, int n, MeshData m)
        {
            double r = 1.0, cy = 1.4;
            Tube(m, new[] { 2.6, 2.2, 0.8, -1.8, -2.4 }, new[] { 0.2, r * 0.8, r, r * 0.85, 0.35 }, new[] { cy - 0.2, cy, cy, cy + 0.1, cy + 0.3 }, n, Livery);
            Box(m, -0.75, 0.75, cy - 0.05, cy + 0.6, 1.2, 2.3, Window);
            // Tail boom and fin with the tail rotor.
            Tube(m, new[] { -2.3, -7.8 }, new[] { 0.32, 0.15 }, new[] { cy + 0.35, cy + 0.6 }, Math.Max(6, n / 2), Livery);
            Fin(m, -8.2, -7.3, cy + 0.5, 3.3, 0.12, Livery);
            int tr = m.VertexCount;
            Disc(m, 0, cy + 1.3, 0, 0.85, Math.Max(6, n / 2), Prop);
            TurnToSide(m, tr, 0.25, -7.9);
            // Skids.
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit.Bar(m, s * 1.0, 0.08, 2.2, s * 1.0, 0.08, -1.8, 0.1, Gear);
                MeshKit.Bar(m, s * 1.0, 0.08, 1.2, s * 0.6, cy - 0.7, 1.0, 0.08, Gear);
                MeshKit.Bar(m, s * 1.0, 0.08, -1.0, s * 0.6, cy - 0.7, -0.8, 0.08, Gear);
            }
            // Mast and the 3-blade rotor as a blur disc (Ø 10.7 m).
            MeshKit.Cylinder(m, 0, 0.2, 0.12, cy + r * 0.8, 3.0, 6, false, Dark);
            MeshKit.Cylinder(m, 0, 0.2, 5.35, 3.0, 3.06, lod == 0 ? 24 : 12, true, Prop);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers

        /// <summary>A horizontal tube along Z through stations (z, radius, centre y), n sides, facing out; a zero radius
        /// closes it to a point.</summary>
        public static void Tube(MeshData m, double[] z, double[] r, double[] y, int n, uint c)
        {
            for (int k = 0; k + 1 < z.Length; k++)
                for (int i = 0; i < n; i++)
                {
                    double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                    double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                    double hx = c0 + c1, hy = s0 + s1;
                    if (r[k] <= 1e-6)
                        MeshKit.Tri(m, 0, y[k], z[k], r[k + 1] * c1, y[k + 1] + r[k + 1] * s1, z[k + 1], r[k + 1] * c0, y[k + 1] + r[k + 1] * s0, z[k + 1], hx, hy, 0, c);
                    else if (r[k + 1] <= 1e-6)
                        MeshKit.Tri(m, r[k] * c0, y[k] + r[k] * s0, z[k], r[k] * c1, y[k] + r[k] * s1, z[k], 0, y[k + 1], z[k + 1], hx, hy, 0, c);
                    else
                        MeshKit.Quad(m, r[k] * c0, y[k] + r[k] * s0, z[k], r[k] * c1, y[k] + r[k] * s1, z[k],
                                     r[k + 1] * c1, y[k + 1] + r[k + 1] * s1, z[k + 1], r[k + 1] * c0, y[k + 1] + r[k + 1] * s0, z[k + 1], hx, hy, 0, c);
                }
            // Close the open ends with a cap facing outwards along Z.
            CapZ(m, z[0], r[0], y[0], n, z[0] > z[z.Length - 1] ? 1 : -1, c);
            CapZ(m, z[z.Length - 1], r[r.Length - 1], y[y.Length - 1], n, z[0] > z[z.Length - 1] ? -1 : 1, c);
        }

        private static void CapZ(MeshData m, double z, double r, double y, int n, int dir, uint c)
        {
            if (r <= 1e-6) return;
            for (int i = 0; i < n; i++)
            {
                double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                MeshKit.Tri(m, 0, y, z, r * Math.Cos(a0), y + r * Math.Sin(a0), z, r * Math.Cos(a1), y + r * Math.Sin(a1), z, 0, 0, dir, c);
            }
        }

        /// <summary>A disc facing ±Z at (x, y, z) (propeller and fan blur).</summary>
        private static void Disc(MeshData m, double x, double y, double z, double r, int n, uint c)
        {
            for (int i = 0; i < n; i++)
            {
                double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                MeshKit.Tri(m, x, y, z, x + r * Math.Cos(a0), y + r * Math.Sin(a0), z, x + r * Math.Cos(a1), y + r * Math.Sin(a1), z, 0, 0, 1, c);
                MeshKit.Tri(m, x, y, z - 0.02, x + r * Math.Cos(a0), y + r * Math.Sin(a0), z - 0.02, x + r * Math.Cos(a1), y + r * Math.Sin(a1), z - 0.02, 0, 0, -1, c);
            }
        }

        /// <summary>Moves vertices [v0, end) sideways (nacelles are built on the axis, then shifted out).</summary>
        private static void ShiftFrom(MeshData m, int v0, double dx)
        {
            for (int v = v0; v < m.VertexCount; v++) m.Positions[v * 3] += (float)dx;
        }

        /// <summary>Turns vertices [v0, end) (a disc built facing ±Z at the origin's x/z) to face ±X and moves them to
        /// (x, z): the tail rotor.</summary>
        private static void TurnToSide(MeshData m, int v0, double x, double z)
        {
            for (int v = v0; v < m.VertexCount; v++)
            {
                int p = v * 3;
                float px = m.Positions[p], pz = m.Positions[p + 2];
                m.Positions[p] = (float)(x + pz);
                m.Positions[p + 2] = (float)(z - px);
                float nx = m.Normals[p], nz = m.Normals[p + 2];
                m.Normals[p] = nz;
                m.Normals[p + 2] = -nx;
            }
        }

        /// <summary>A flat plate (wing, stabiliser) spanning x0..x1 at height y, from z0 (trailing) to z1 (leading).</summary>
        private static void Plate(MeshData m, double x0, double x1, double y, double z0, double z1, double t, uint c)
        {
            KitMeshes.Box(m, x0, x1, y - t * 0.5, y + t * 0.5, z0, z1, c);
        }

        private static void Box(MeshData m, double x0, double x1, double y0, double y1, double z0, double z1, uint c)
        {
            KitMeshes.Box(m, x0, x1, y0, y1, z0, z1, c);
        }

        /// <summary>A vertical tail fin from z0 to z1 rising from y0 to y1, swept back at the top.</summary>
        private static void Fin(MeshData m, double z0, double z1, double y0, double y1, double t, uint c)
        {
            double top0 = z0 - 0.2, top1 = z0 + (z1 - z0) * 0.45;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.Quad(m, s * t, y0, z0, s * t, y0, z1, s * t * 0.6, y1, top1, s * t * 0.6, y1, top0, s, 0, 0, c);
            MeshKit.Quad(m, -t, y0, z1, t, y0, z1, t * 0.6, y1, top1, -t * 0.6, y1, top1, 0, 0.3, 1, c);
            MeshKit.Quad(m, -t, y0, z0, t, y0, z0, t * 0.6, y1, top0, -t * 0.6, y1, top0, 0, 0, -1, c);
        }

        /// <summary>Dark window squares along both sides of the fuselage.</summary>
        private static void Windows(MeshData m, double y, double r, double z0, double z1, int count, int lod)
        {
            if (count < 1) return;
            double step = (z1 - z0) / count, w = Math.Min(0.45, step * 0.45);
            for (int i = 0; i < count; i++)
            {
                double z = z0 + step * (i + 0.5);
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.Quad(m, s * (r + 0.02), y - w * 0.5, z - w * 0.5, s * (r + 0.02), y - w * 0.5, z + w * 0.5,
                                 s * (r + 0.02), y + w * 0.5, z + w * 0.5, s * (r + 0.02), y + w * 0.5, z - w * 0.5, s, 0, 0, Window);
            }
        }

        /// <summary>The livery cheat line along both sides (tinted).</summary>
        private static void CheatLine(MeshData m, double cy, double r, double z0, double z1, int n)
        {
            for (int s = -1; s <= 1; s += 2)
                MeshKit.Quad(m, s * (r + 0.015), cy - 0.25, z0, s * (r + 0.015), cy - 0.25, z1, s * (r + 0.015), cy - 0.05, z1,
                             s * (r + 0.015), cy - 0.05, z0, s, 0, 0, Livery);
        }

        private static void Gears(MeshData m, double bellyY, double noseZ, double mainZ, double track, double wheel)
        {
            MeshKit.Bar(m, 0, bellyY + 0.1, noseZ, 0, wheel, noseZ, 0.15, Gear);
            WheelBox(m, 0, noseZ, wheel);
            for (int s = -1; s <= 1; s += 2)
            {
                MeshKit.Bar(m, s * track * 0.5, bellyY + 0.1, mainZ, s * track * 0.5, wheel, mainZ, 0.2, Gear);
                WheelBox(m, s * track * 0.5, mainZ, wheel);
            }
        }

        private static void WheelBox(MeshData m, double x, double z, double r)
        {
            KitMeshes.Box(m, x - r * 0.4, x + r * 0.4, 0, 2 * r, z - r, z + r, Gear);
        }
    }
}
