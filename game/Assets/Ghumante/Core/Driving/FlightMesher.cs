using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>The separately drawn parts of a flyable aircraft.</summary>
    public enum FlightPart : byte
    {
        /// <summary>The fuselage with its windows, doors, paint and small details.</summary>
        Body = 0,

        /// <summary>Wings, tail, nacelles, struts, fairings and lights: everything that stays put.</summary>
        Frame = 1,

        /// <summary>The landing gear (hidden when retracted; always drawn on fixed-gear types).</summary>
        Gear = 2,

        /// <summary>One propeller (spinner and blades) around the origin, spinning about +Z.</summary>
        Prop = 3,

        /// <summary>The helicopter's main rotor around the origin, spinning about +Y.</summary>
        Rotor = 4,

        /// <summary>The helicopter's tail rotor around the origin, spinning about +X.</summary>
        TailRotor = 5,

        /// <summary>The cockpit seen from the pilot's seat (panel, glareshield, yokes, window frames, the nose ahead).</summary>
        Cockpit = 6,
    }

    /// <summary>
    /// Procedural cartoon models of the flyable aircraft (docs/research/w2/ref_flight.md §4, built to the reference
    /// photos and the real dimensions of <see cref="FlightSpec"/>): ATR 72-class, Dash 8-400-class, Jetstream 41-class,
    /// Twin Otter-class and H125-class. Generic liveries only (<see cref="FlightLiveries"/>: no names, logos,
    /// registrations or flags). Frame: origin on the ground under the fuselage centre (the rotor mast for the
    /// helicopter), +Z nose, +X right wing, +Y up; wheels stand on y = 0. Every part writes UV0 (material channel, AO).
    /// LOD0 ≤ 9,000, LOD1 ≤ 3,000, LOD2 ≤ 900 triangles for a whole aircraft (<see cref="Budget"/>). Engine-free,
    /// deterministic; meshes are built once per (craft, livery, LOD) and cached by the caller.
    /// </summary>
    public static partial class FlightMesher
    {
        /// <summary>Triangle budgets of a whole aircraft (all parts, props included) per LOD.</summary>
        public static readonly int[] Budget = { 9000, 3000, 900 };

        private const double Pi = Math.PI;
        private const uint Glass = 0x1D2833, Rubber = 0x26282B, Metal = 0x9CA3AA, DarkMetal = 0x50565C, Interior = 0x3E4349;
        private const uint PropBlade = 0x2C3034, PropTip = 0xE9E6DD, Boot = 0x2A2D30;

        /// <summary>Builds one part at LOD 0–2 into <paramref name="m"/>; returns the triangles added (0 when the craft has
        /// no such part).</summary>
        public static int Build(FlightCraft craft, FlightPart part, int livery, int lod, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            lod = Math.Max(0, Math.Min(2, lod));
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            FlightLiveries.Livery liv = FlightLiveries.For(craft, livery);
            var k = new Kit(m, lod, liv);
            switch (part)
            {
                case FlightPart.Body:
                    BuildBody(craft, k);
                    break;
                case FlightPart.Frame:
                    BuildFrame(craft, k);
                    break;
                case FlightPart.Gear:
                    BuildGear(craft, k);
                    break;
                case FlightPart.Prop:
                    if (craft != FlightCraft.Helicopter) BuildProp(craft, k);
                    break;
                case FlightPart.Rotor:
                    if (craft == FlightCraft.Helicopter) BuildRotor(k);
                    break;
                case FlightPart.TailRotor:
                    if (craft == FlightCraft.Helicopter) BuildTailRotor(k);
                    break;
                case FlightPart.Cockpit:
                    BuildCockpit(craft, k);
                    break;
            }
            if (m.TriangleCount > t0 && part != FlightPart.Prop && part != FlightPart.Rotor && part != FlightPart.TailRotor)
                ShapeAo.Bake(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0, ShapeAo.Defaults(0.0));
            return m.TriangleCount - t0;
        }

        /// <summary>The whole aircraft as it stands parked (body, frame, gear, propellers at rest, rotors), for the parked
        /// fleet; returns the triangles added.</summary>
        public static int BuildParked(FlightCraft craft, int livery, int lod, MeshData m)
        {
            int t0 = m.TriangleCount;
            Build(craft, FlightPart.Body, livery, lod, m);
            Build(craft, FlightPart.Frame, livery, lod, m);
            Build(craft, FlightPart.Gear, livery, lod, m);
            var tmp = new MeshData(2048, 6144);
            for (int i = 0; i < PropCount(craft); i++)
            {
                tmp.Clear();
                Build(craft, FlightPart.Prop, livery, lod, tmp);
                float px, py, pz;
                bool cw;
                PropMount(craft, i, out px, out py, out pz, out cw);
                Shapes.CopyTransformed(m, tmp, Affine3.Translation(px, py, pz) * Affine3.RotationZ((i * 0.37 + 0.2) * Pi));
            }
            if (craft == FlightCraft.Helicopter)
            {
                float rx, ry, rz;
                tmp.Clear();
                Build(craft, FlightPart.Rotor, livery, lod, tmp);
                RotorMount(out rx, out ry, out rz);
                Shapes.CopyTransformed(m, tmp, Affine3.Translation(rx, ry, rz) * Affine3.RotationY(0.3));
                tmp.Clear();
                Build(craft, FlightPart.TailRotor, livery, lod, tmp);
                TailRotorMount(out rx, out ry, out rz);
                Shapes.CopyTransformed(m, tmp, Affine3.Translation(rx, ry, rz));
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Number of propellers of a craft.</summary>
        public static int PropCount(FlightCraft craft)
        {
            return craft == FlightCraft.Helicopter ? 0 : 2;
        }

        /// <summary>Hub position of propeller <paramref name="i"/> (0 left, 1 right) and its turning sense seen from
        /// behind (both turn clockwise on these types).</summary>
        public static void PropMount(FlightCraft craft, int i, out float x, out float y, out float z, out bool clockwise)
        {
            float side = i == 0 ? -1f : 1f;
            clockwise = true;
            switch (craft)
            {
                case FlightCraft.TurbopropStretch:
                    x = 4.3f * side;
                    y = 3.05f;
                    z = 6.15f;
                    return;
                case FlightCraft.Commuter:
                    x = 2.75f * side;
                    y = 1.95f;
                    z = 3.75f;
                    return;
                case FlightCraft.Stol:
                    x = 3.2f * side;
                    y = 2.35f;
                    z = 4.75f;
                    return;
                default:
                    x = 4.1f * side;
                    y = 3.0f;
                    z = 4.6f;
                    return;
            }
        }

        /// <summary>The helicopter's rotor hub and tail-rotor hub.</summary>
        public static void RotorMount(out float x, out float y, out float z)
        {
            x = 0f;
            y = 3.1f;
            z = 0f;
        }

        public static void TailRotorMount(out float x, out float y, out float z)
        {
            x = -0.32f;
            y = 2.05f;
            z = -6.75f;
        }

        // =============================================================================================== the kit

        /// <summary>The mesh being built, its LOD and livery, and scratch shapes (one per build; meshes are built once).</summary>
        private sealed class Kit
        {
            public readonly MeshData M;
            public readonly int Lod;
            public readonly ShapeLod SL;
            public readonly FlightLiveries.Livery Liv;
            public readonly Profile2 P = new Profile2(64), Q = new Profile2(64);
            public readonly Path3 Path = new Path3(32);

            public Kit(MeshData m, int lod, FlightLiveries.Livery liv)
            {
                M = m;
                Lod = lod;
                SL = new ShapeLod(lod);
                Liv = liv;
            }

            /// <summary>Radial segments for a round section at this LOD.</summary>
            public int Ring(int lod0)
            {
                return Lod == 0 ? lod0 : Lod == 1 ? Math.Max(8, lod0 / 2) : Math.Max(6, lod0 / 3);
            }

            public static ShapeBrush B(uint rgb, MaterialChannel ch)
            {
                return ShapeBrush.Hex(rgb, ch);
            }
        }

        // ================================================================================================ fuselage

        /// <summary>One cross-section of a lofted fuselage: station z, centre height, half width and half height, and
        /// the superellipse exponent (2 = ellipse, 3–4 = the boxy Twin Otter).</summary>
        private struct Station
        {
            public double Z, Yc, A, B, N;

            public Station(double z, double yc, double a, double b, double n = 2.1)
            {
                Z = z;
                Yc = yc;
                A = a;
                B = b;
                N = n;
            }
        }

        /// <summary>How a fuselage is painted, by station z and the height around the section (−1 belly … +1 crown).</summary>
        private sealed class FuselagePaint
        {
            public uint Body, Belly, Stripe, Tail;
            public double BellyBelow = -0.45, StripeLow = -0.22, StripeHigh = -0.08;

            /// <summary>Windscreen: between these stations, above this height fraction (and only ahead of the pilot).</summary>
            public double ScreenZ0 = double.MaxValue, ScreenZ1 = double.MaxValue, ScreenAbove = 0.2, ScreenSide = 1.0;

            /// <summary>Aft of this station the tail colour sweeps up over the crown (above <see cref="TailAbove"/>).</summary>
            public double TailZ = double.MinValue, TailAbove = 0.35;

            public void At(double z, double h, double side, out uint rgb, out MaterialChannel ch)
            {
                ch = MaterialChannel.Paint;
                if (z >= ScreenZ0 && z <= ScreenZ1 && h > ScreenAbove && Math.Abs(side) <= ScreenSide)
                {
                    rgb = Glass;
                    ch = MaterialChannel.Glass;
                    return;
                }
                if (z < TailZ && h > TailAbove - (TailZ - z) * 0.08)
                {
                    rgb = Tail;
                    return;
                }
                if (h < BellyBelow) rgb = Belly;
                else if (h >= StripeLow && h <= StripeHigh) rgb = Stripe;
                else rgb = Body;
            }
        }

        /// <summary>
        /// A lofted fuselage through <paramref name="st"/> (nose first) with <paramref name="ring"/> points per section,
        /// both ends closed, painted by <paramref name="paint"/>, smooth normals. Returns the first vertex.
        /// </summary>
        private static int Fuselage(Kit k, Station[] st, int ring, FuselagePaint paint)
        {
            MeshData m = k.M;
            int first = m.VertexCount, firstI = m.IndexCount;
            int n = st.Length;
            for (int s = 0; s < n; s++)
            {
                Station a = st[s];
                for (int j = 0; j < ring; j++)
                {
                    double t = 2 * Pi * j / ring;
                    double sx = Math.Sin(t), cy = Math.Cos(t);
                    double e = 2.0 / a.N;
                    double x = a.A * Math.Sign(sx) * Math.Pow(Math.Abs(sx), e);
                    double y = a.B * Math.Sign(cy) * Math.Pow(Math.Abs(cy), e);
                    uint rgb;
                    MaterialChannel ch;
                    paint.At(a.Z, cy, sx, out rgb, out ch);
                    m.AddVertex((float)x, (float)(a.Yc + y), (float)a.Z, (float)sx, (float)cy, 0f, (rgb << 8) | 0xFF, (float)ch, 1f);
                }
            }
            for (int s = 0; s + 1 < n; s++)
            {
                double cz = 0.5 * (st[s].Z + st[s + 1].Z), cyc = 0.5 * (st[s].Yc + st[s + 1].Yc);
                for (int j = 0; j < ring; j++)
                {
                    int j1 = (j + 1) % ring;
                    int a = first + s * ring + j, b = first + s * ring + j1, c = first + (s + 1) * ring + j, d = first + (s + 1) * ring + j1;
                    TriOut(m, a, b, c, 0, cyc, cz);
                    TriOut(m, b, d, c, 0, cyc, cz);
                }
            }
            // End caps (fans to a centre point slightly beyond the end section).
            Cap(k, st[0], first, ring, paint, 1);
            Cap(k, st[n - 1], first + (n - 1) * ring, ring, paint, -1);
            ShapeNoise.RecomputeNormals(m, first, m.VertexCount - first, firstI, m.IndexCount - firstI, true);
            return first;
        }

        private static void Cap(Kit k, Station a, int ringFirst, int ring, FuselagePaint paint, int dir)
        {
            MeshData m = k.M;
            uint rgb;
            MaterialChannel ch;
            paint.At(a.Z, 0.0, 0.0, out rgb, out ch);
            double z = a.Z + dir * Math.Min(a.A, a.B) * 0.6;
            int c = m.AddVertex(0f, (float)a.Yc, (float)z, 0f, 0f, dir, (rgb << 8) | 0xFF, (float)ch, 1f);
            for (int j = 0; j < ring; j++)
            {
                int j1 = (j + 1) % ring;
                TriOut(m, ringFirst + j, ringFirst + j1, c, 0, a.Yc, a.Z - dir * 2.0);
            }
        }

        /// <summary>Interpolated section at station z (stations nose first, z decreasing).</summary>
        private static Station SectionAt(Station[] st, double z)
        {
            if (z >= st[0].Z) return st[0];
            for (int i = 0; i + 1 < st.Length; i++)
            {
                if (z <= st[i].Z && z >= st[i + 1].Z)
                {
                    double t = (st[i].Z - z) / Math.Max(1e-9, st[i].Z - st[i + 1].Z);
                    return new Station(z, st[i].Yc + (st[i + 1].Yc - st[i].Yc) * t, st[i].A + (st[i + 1].A - st[i].A) * t,
                                       st[i].B + (st[i + 1].B - st[i].B) * t, st[i].N + (st[i + 1].N - st[i].N) * t);
                }
            }
            return st[st.Length - 1];
        }

        /// <summary>A point on the fuselage skin at station z and section angle t (0 crown, π/2 right, π belly) with its
        /// outward normal.</summary>
        private static void SkinPoint(Station[] st, double z, double t, out double x, out double y, out double nx, out double ny)
        {
            Station a = SectionAt(st, z);
            double sx = Math.Sin(t), cy = Math.Cos(t), e = 2.0 / a.N;
            x = a.A * Math.Sign(sx) * Math.Pow(Math.Abs(sx), e);
            y = a.Yc + a.B * Math.Sign(cy) * Math.Pow(Math.Abs(cy), e);
            // Gradient of the superellipse |x/A|^N + |y/B|^N.
            double gx = Math.Sign(x) * Math.Pow(Math.Abs(x) / a.A, a.N - 1) / a.A, gy = Math.Sign(y - a.Yc) * Math.Pow(Math.Abs(y - a.Yc) / a.B, a.N - 1) / a.B;
            double l = Math.Sqrt(gx * gx + gy * gy);
            if (l < 1e-9)
            {
                nx = sx;
                ny = cy;
            }
            else
            {
                nx = gx / l;
                ny = gy / l;
            }
        }

        /// <summary>
        /// <see cref="SkinPoint"/> with the skin's full 3D normal: the section's own normal tilted by the loft's slope along
        /// the fuselage (dA/dz, dB/dz, dYc/dz). On a tapering nose or tail cone the section normal alone points sideways
        /// while the skin faces forward; anything offset along it then lies flat on the skin and the fuselage's facets poke
        /// through (the helicopter's chin did, before).
        /// </summary>
        private static void SkinPoint3(Station[] st, double z, double t, out double x, out double y, out double nx, out double ny, out double nz)
        {
            SkinPoint(st, z, t, out x, out y, out nx, out ny);
            const double h = 0.03;
            Station f = SectionAt(st, z + h), b = SectionAt(st, z - h);
            double sx = Math.Sin(t), cy = Math.Cos(t), e = 2.0 / SectionAt(st, z).N;
            double fx = Math.Sign(sx) * Math.Pow(Math.Abs(sx), e), fy = Math.Sign(cy) * Math.Pow(Math.Abs(cy), e);
            double dxdz = (f.A - b.A) * fx / (2 * h), dydz = ((f.Yc - b.Yc) + (f.B - b.B) * fy) / (2 * h);
            // The normal is perpendicular to the section tangent (in the section plane) and to dP/dz = (dxdz, dydz, 1).
            nz = -(nx * dxdz + ny * dydz);
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= l;
            ny /= l;
            nz /= l;
        }

        /// <summary>The section angle on the right side where the skin reaches height fraction <paramref name="h"/>
        /// (−1 belly … +1 crown).</summary>
        private static double AngleAtHeight(double h)
        {
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, h)));
        }

        /// <summary>A row of cabin windows (rounded panes standing just proud of the skin) on both sides, from
        /// <paramref name="z0"/> to <paramref name="z1"/>, centred at height fraction <paramref name="h"/>.</summary>
        private static void Windows(Kit k, Station[] st, int count, double z0, double z1, double h, double w, double hgt, double round)
        {
            if (count <= 0) return;
            if (k.Lod >= 2) return;
            if (k.Lod == 1) count = Math.Max(1, (count + 1) / 2);
            ShapeBrush glass = Kit.B(Glass, MaterialChannel.Glass);
            double t = AngleAtHeight(h);
            for (int i = 0; i < count; i++)
            {
                double z = count == 1 ? z0 : z0 + (z1 - z0) * i / (count - 1);
                for (int side = -1; side <= 1; side += 2)
                    Pane(k, st, z, side * t, w, hgt, round, glass);
            }
        }

        /// <summary>
        /// One pane (window, door) hugging the skin at station z, section angle t: a rounded-rectangle outline sampled on
        /// the fuselage (so it follows the curvature) 2.2 cm proud of it along the skin's 3D normal, filled with concentric
        /// rings fanned from its centre. A big pane on a tight section (the helicopter's door windows) gets more rings and
        /// outline points, so its flat facets never dip under the skin (a single fan did: the body colour showed through as
        /// an "I" across each window). <paramref name="round"/> (0–1) rounds the corners (0 a rectangle, 1 an ellipse).
        /// </summary>
        private static void Pane(Kit k, Station[] st, double z, double t, double w, double h, double round, in ShapeBrush brush)
        {
            MeshData m = k.M;
            double xc, yc, nxc, nyc, nzc;
            SkinPoint3(st, z, t, out xc, out yc, out nxc, out nyc, out nzc);
            Station sec = SectionAt(st, z);
            // Angle per metre of skin around the section (a local estimate of the section's radius there).
            double rr = Math.Max(0.2, Math.Sqrt(xc * xc + (yc - sec.Yc) * (yc - sec.Yc)));
            double dt = 0.5 * h / rr;
            // How far a chord across the pane would sink under the skin: rings and points until it stays under 8 mm.
            double sag = rr * (1.0 - Math.Cos(Math.Min(dt, 1.2)));
            // A big pane on a tight skin keeps its LOD0 fill at LOD1 too (the coarser one sinks into the skin there).
            bool fine = k.Lod == 0 || k.Lod == 1 && sag > 0.02;
            int rings = fine ? Clamp((int)Math.Ceiling(Math.Sqrt(sag / 0.008)), 1, 3) : 1;
            int pts = fine ? (sag > 0.01 ? 16 : 8) : k.Lod == 1 ? 8 : 4;
            double e = 2.0 / (2.0 + 6.0 * (1.0 - Math.Max(0.0, Math.Min(1.0, round)))); // superellipse exponent: 1 ellipse … 0.25 boxy
            uint rgba = (brush.Color & 0xFFFFFF00u) | (brush.Color & 0xFFu);
            float u = (float)brush.Channel;
            const double lift = 0.022;
            int c = m.AddVertex((float)(xc + nxc * lift), (float)(yc + nyc * lift), (float)(z + nzc * lift), (float)nxc, (float)nyc, (float)nzc, rgba, u, 1f);
            int first = m.VertexCount;
            for (int r = 1; r <= rings; r++)
            {
                double f = (double)r / rings;
                for (int i = 0; i < pts; i++)
                {
                    double a = 2 * Pi * (i + 0.5) / pts;
                    double ca = Math.Cos(a), sa = Math.Sin(a);
                    double pz = z + f * 0.5 * w * Math.Sign(ca) * Math.Pow(Math.Abs(ca), e), pt = t + f * dt * Math.Sign(sa) * Math.Pow(Math.Abs(sa), e);
                    double x, y, nx, ny, nz;
                    SkinPoint3(st, pz, pt, out x, out y, out nx, out ny, out nz);
                    m.AddVertex((float)(x + nx * lift), (float)(y + ny * lift), (float)(pz + nz * lift), (float)nx, (float)ny, (float)nz, rgba, u, 1f);
                }
            }
            // Facing out: away from a point a metre inside the skin under the pane's centre.
            double ox = xc - nxc, oy = yc - nyc, oz = z - nzc;
            for (int i = 0; i < pts; i++) TriOut(m, c, first + i, first + (i + 1) % pts, ox, oy, oz);
            for (int r = 1; r < rings; r++)
            {
                int inner = first + (r - 1) * pts, outer = first + r * pts;
                for (int i = 0; i < pts; i++)
                {
                    int i1 = (i + 1) % pts;
                    TriOut(m, inner + i, outer + i, outer + i1, ox, oy, oz);
                    TriOut(m, inner + i, outer + i1, inner + i1, ox, oy, oz);
                }
            }
        }

        /// <summary>
        /// A livery band (cheatline) on both sides of the fuselage between stations <paramref name="z0"/> (front) and
        /// <paramref name="z1"/> (back), between height fractions <paramref name="hLow"/> and <paramref name="hHigh"/>: a strip
        /// sampled on the skin 1.5 cm proud of it, so its edges stay crisp (vertex paint on the coarse fuselage ring would
        /// smear). <paramref name="rise"/> lifts the band's back end by that height fraction (a sweep up to the tail).
        /// </summary>
        private static void Band(Kit k, Station[] st, double z0, double z1, double hLow, double hHigh, double rise, in ShapeBrush brush)
        {
            MeshData m = k.M;
            int segs = k.Lod == 0 ? 18 : k.Lod == 1 ? 8 : 4;
            uint rgba = (brush.Color & 0xFFFFFF00u) | (brush.Color & 0xFFu);
            float u = (float)brush.Channel;
            for (int side = -1; side <= 1; side += 2)
            {
                int first = m.VertexCount;
                for (int i = 0; i <= segs; i++)
                {
                    double f = (double)i / segs, z = z0 + (z1 - z0) * f, lift = rise * f * f;
                    for (int row = 0; row < 2; row++)
                    {
                        double h = Math.Min(0.98, (row == 0 ? hLow : hHigh) + lift);
                        double x, y, nx, ny, nz;
                        SkinPoint3(st, z, side * AngleAtHeight(h), out x, out y, out nx, out ny, out nz);
                        m.AddVertex((float)(x + nx * 0.015), (float)(y + ny * 0.015), (float)(z + nz * 0.015), (float)nx, (float)ny, (float)nz, rgba, u, 1f);
                    }
                }
                for (int i = 0; i < segs; i++)
                {
                    int a = first + i * 2, c = a + 2;
                    double zc = z0 + (z1 - z0) * (i + 0.5) / segs;
                    double yc = SectionAt(st, zc).Yc;
                    TriOut(m, a, a + 1, c, 0, yc, zc);
                    TriOut(m, a + 1, c + 1, c, 0, yc, zc);
                }
            }
        }

        /// <summary>
        /// The flight-deck glazing of a fixed-wing type as one dark visor on the nose with slim posts: on each side a
        /// windscreen pane (section angles 0.05–<paramref name="split"/>) and a side window (split + 0.08 – <paramref name="tMax"/>),
        /// each a patch of the skin 2 cm proud of it between a front edge at station <paramref name="zFront"/> −
        /// <paramref name="slopeFront"/>·|t| and a back edge at <paramref name="zBack"/> − <paramref name="slopeBack"/>·|t|
        /// (the glazing sweeps back down the sides, like the real cockpits). Every LOD: a grid fine enough to follow the
        /// skin's curve at LOD0 (rows every ~0.18 m along the fuselage, snapped onto the stations' knees, columns every
        /// ~0.24 rad around it), coarser at LOD1 (unless <paramref name="fineAtLod1"/>: a bubble that fills the view keeps
        /// its LOD0 grid), one quad per pane at LOD2. A coarse grid's flat facets dip under a strongly
        /// curved skin and the body colour pokes through as a saw-tooth (the helicopter's bubble did, before).
        /// </summary>
        private static void FlightDeck(Kit k, Station[] st, double zFront, double slopeFront, double zBack, double slopeBack, double split, double tMax,
                                       bool fineAtLod1 = false)
        {
            MeshData m = k.M;
            double len = Math.Max(Math.Abs(zFront - zBack), Math.Abs((zFront - slopeFront * tMax) - (zBack - slopeBack * tMax)));
            int lod = fineAtLod1 && k.Lod == 1 ? 0 : k.Lod;
            int nv = lod == 0 ? Clamp((int)Math.Ceiling(len / 0.18), 3, 12) : lod == 1 ? Clamp((int)Math.Ceiling(len / 0.5), 1, 4) : 1;
            uint rgba = (Glass << 8) | 0xFF;
            float u = (float)MaterialChannel.Glass;
            for (int pane = 0; pane < 2; pane++)
            {
                double a = pane == 0 ? 0.05 : split + 0.08, b = pane == 0 ? split : tMax;
                int nu = lod == 0 ? Clamp((int)Math.Ceiling((b - a) / 0.24), 2, 8) : lod == 1 ? 2 : 1;
                for (int side = -1; side <= 1; side += 2)
                {
                    int first = m.VertexCount;
                    for (int i = 0; i <= nu; i++)
                    {
                        double t = a + (b - a) * i / nu;
                        double zf = zFront - slopeFront * t, zb = zBack - slopeBack * t;
                        double half = 0.5 * Math.Abs(zf - zb) / nv;
                        for (int j = 0; j <= nv; j++)
                        {
                            double z = zb + (zf - zb) * j / nv, x, y, nx, ny, nz;
                            // Inner rows snap onto a station within half a row: the patch then bends where the skin does.
                            if (lod == 0 && j > 0 && j < nv)
                            {
                                for (int s = 0; s < st.Length; s++)
                                {
                                    if (Math.Abs(st[s].Z - z) < half)
                                    {
                                        z = st[s].Z;
                                        break;
                                    }
                                }
                            }
                            SkinPoint3(st, z, side * t, out x, out y, out nx, out ny, out nz);
                            m.AddVertex((float)(x + nx * 0.02), (float)(y + ny * 0.02), (float)(z + nz * 0.02), (float)nx, (float)ny, (float)nz, rgba, u, 1f);
                        }
                    }
                    for (int i = 0; i < nu; i++)
                    {
                        for (int j = 0; j < nv; j++)
                        {
                            int p00 = first + i * (nv + 1) + j, p01 = p00 + 1, p10 = p00 + nv + 1, p11 = p10 + 1;
                            double zc = 0.5 * (zFront + zBack);
                            double yc = SectionAt(st, zc).Yc;
                            TriOut(m, p00, p10, p01, 0, yc, zc);
                            TriOut(m, p10, p11, p01, 0, yc, zc);
                        }
                    }
                }
            }
        }

        // ================================================================================================= surfaces

        /// <summary>
        /// A lifting surface (wing, stabiliser, fin, strut) lofted between a root and a tip airfoil. Chord runs along −Z
        /// from the leading-edge points; <paramref name="thickX"/>/<paramref name="thickY"/> is the thickness direction
        /// (up for wings, sideways for fins). Leading-edge de-icing boots in <paramref name="boot"/> (0 = none) over the
        /// first 10% of the chord.
        /// </summary>
        private static void Surface(Kit k, in ShapeBrush brush, double rx, double ry, double rz, double rootChord, double rootThick, double tx, double ty,
                                    double tz, double tipChord, double tipThick, double thickX, double thickY, uint boot = 0)
        {
            MeshData m = k.M;
            int first = m.VertexCount;
            int pts = k.Lod == 0 ? 10 : k.Lod == 1 ? 6 : 3;
            Airfoil(k.P, rootChord, rootThick, pts);
            Airfoil(k.Q, tipChord, tipThick, pts);
            // Span direction (unit) from root to tip without the sweep's chordwise part; thickness axis as given.
            double sx = tx - rx, sy = ty - ry, sz = 0;
            double sl = Math.Sqrt(sx * sx + sy * sy);
            if (sl < 1e-6) return;
            sx /= sl;
            sy /= sl;
            Affine3 fa = Affine3.FromBasis(0, 0, -1, thickX, thickY, 0, sx, sy, sz, rx, ry, rz);
            Affine3 fb = Affine3.FromBasis(0, 0, -1, thickX, thickY, 0, sx, sy, sz, tx, ty, tz);
            int segs = k.Lod == 0 ? 2 : 1;
            Shapes.Loft(m, Affine3.Identity, brush, k.P, fa, k.Q, fb, segs, true, true, k.SL);
            if (boot == 0 || k.Lod >= 2) return;
            // Leading-edge boots: recolour the front tenth of the chord.
            uint rgba = (boot << 8) | 0xFF;
            for (int v = first; v < m.VertexCount; v++)
            {
                double px = m.Positions[v * 3], py = m.Positions[v * 3 + 1], pz = m.Positions[v * 3 + 2];
                double s = ((px - rx) * sx + (py - ry) * sy) / sl;
                if (s < -0.01 || s > 1.01) continue;
                double le = rz + (tz - rz) * s, chord = rootChord + (tipChord - rootChord) * s;
                if ((le - pz) / Math.Max(0.1, chord) < 0.09)
                {
                    m.Colors[v * 4] = (byte)(rgba >> 24);
                    m.Colors[v * 4 + 1] = (byte)(rgba >> 16);
                    m.Colors[v * 4 + 2] = (byte)(rgba >> 8);
                    if (m.HasUv0) m.Uv0[v * 2] = (float)MaterialChannel.Rubber;
                }
            }
        }

        /// <summary>A symmetric cartoon airfoil (NACA 00xx thickness with a round nose), closed, x along the chord from the
        /// leading edge, y the thickness.</summary>
        private static void Airfoil(Profile2 p, double chord, double thick, int n)
        {
            p.Clear(true);
            // Upper surface from the trailing edge forward, then the lower surface back (counter-clockwise).
            for (int i = n; i >= 0; i--)
            {
                double xc = 0.5 * (1 - Math.Cos(Pi * i / n));
                p.Add(xc * chord, Half(xc) * thick * chord, i == n);
            }
            for (int i = 1; i < n; i++)
            {
                double xc = 0.5 * (1 - Math.Cos(Pi * i / n));
                p.Add(xc * chord, -Half(xc) * thick * chord);
            }
        }

        private static double Half(double x)
        {
            x = Math.Max(0.0, Math.Min(1.0, x));
            return 5.0 * (0.2969 * Math.Sqrt(x) - 0.126 * x - 0.3516 * x * x + 0.2843 * x * x * x - 0.1036 * x * x * x * x);
        }

        // ================================================================================================= pieces

        /// <summary>A turboprop nacelle along +Z: an intake lip ahead, the fat engine body, tapering to a tail cone
        /// (profile radii over its length), at (x, y) between z0 (front) and z1 (back).</summary>
        private static void Nacelle(Kit k, in ShapeBrush brush, double x, double y, double zFront, double zBack, double r, double rFront, double rBack)
        {
            Profile2 p = k.P;
            double len = zFront - zBack;
            // Lathe about +Y, mapped onto +Z: profile bottom (y = 0) is the nacelle's tail.
            p.Clear(false);
            p.Add(0, 0, true);
            p.Add(rBack, 0.0, true);
            if (k.Lod < 2) p.Add(r * 0.9, len * 0.25);
            p.Add(r, len * 0.55);
            if (k.Lod < 2) p.Add(r * 0.92, len * 0.85);
            p.Add(rFront, len * 0.97);
            if (k.Lod < 2) p.Add(rFront * 0.6, len, true);
            p.Add(0, len, true);
            if (k.Lod == 0) p.Smooth(2);
            Shapes.Lathe(k.M, Affine3.Translation(x, y, zBack) * Affine3.RotationX(Pi / 2), brush, p, k.Ring(16), k.SL);
        }

        /// <summary>A wheel (tyre torus and hub) centred at (x, y, z), axle along X; a plain dark drum at LOD2.</summary>
        private static void Wheel(Kit k, double x, double y, double z, double r, double width)
        {
            if (k.Lod >= 2)
            {
                Shapes.Cylinder(k.M, Affine3.Translation(x - width * 0.5, y, z) * Affine3.RotationZ(-Pi / 2), Kit.B(Rubber, MaterialChannel.Rubber), r, width, 6, 0.0,
                                0, true, true, ShapeLod.Lod0);
                return;
            }
            double minor = Math.Min(0.45 * width, 0.35 * r);
            Affine3 f = Affine3.Translation(x, y, z) * Affine3.RotationZ(Pi / 2);
            Shapes.Torus(k.M, f, Kit.B(Rubber, MaterialChannel.Rubber), r - minor, minor, k.Lod == 0 ? 14 : 8, k.Lod == 0 ? 6 : 4, ShapeLod.Lod0);
            Shapes.Cylinder(k.M, Affine3.Translation(x - width * 0.32, y, z) * Affine3.RotationZ(-Pi / 2), Kit.B(Metal, MaterialChannel.Metal),
                            (r - minor) * 0.95, width * 0.64, k.Lod == 0 ? 10 : 6, 0.0, 0, true, true, ShapeLod.Lod0);
        }

        /// <summary>A pair of wheels on one leg, offset ±(<paramref name="dx"/>, <paramref name="dz"/>) from (x, z): side by
        /// side or in tandem; one wide drum at LOD2.</summary>
        private static void TwinWheel(Kit k, double x, double y, double z, double r, double width, double dx, double dz)
        {
            if (k.Lod >= 2)
            {
                Wheel(k, x, y, z, r, width + 2 * Math.Abs(dx));
                return;
            }
            Wheel(k, x - dx, y, z - dz, r, width);
            Wheel(k, x + dx, y, z + dz, r, width);
        }

        /// <summary>A round bar between two points.</summary>
        private static void Strut(Kit k, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r)
        {
            Shapes.Bar(k.M, Affine3.Identity, b, ax, ay, az, bx, by, bz, r, k.Ring(10), k.SL);
        }

        /// <summary>A small light (navigation, beacon) as a sphere.</summary>
        private static void Light(Kit k, uint rgb, double x, double y, double z, double r)
        {
            if (k.Lod >= 2) return;
            Shapes.Sphere(k.M, Affine3.Translation(x, y, z), Kit.B(rgb, MaterialChannel.Glass), r, 6, ShapeLod.Lod0);
        }

        /// <summary>A propeller around the origin turning about +Z: a lathed spinner and <paramref name="blades"/> blades
        /// of <paramref name="radius"/> with twist and light tips; a thin dark disc with the spinner at LOD2.</summary>
        private static void Propeller(Kit k, int blades, double radius, double spinnerR, double spinnerLen, uint spinner)
        {
            Profile2 p = k.P;
            p.Clear(false);
            p.Add(0, 0, true);
            p.Add(spinnerR, 0, true);
            p.Add(spinnerR * 0.97, spinnerLen * 0.35);
            p.Add(spinnerR * 0.7, spinnerLen * 0.75);
            p.Add(spinnerR * 0.25, spinnerLen * 0.95);
            p.Add(0, spinnerLen, true);
            Shapes.Lathe(k.M, Affine3.Translation(0, 0, -spinnerLen * 0.35) * Affine3.RotationX(Pi / 2), Kit.B(spinner, MaterialChannel.Paint), p,
                         k.Lod == 0 ? 12 : k.Lod == 1 ? 8 : 5, ShapeLod.Lod0);
            if (k.Lod >= 2)
            {
                Shapes.Cylinder(k.M, Affine3.Translation(0, 0, -0.03) * Affine3.RotationX(Pi / 2), Kit.B(PropBlade, MaterialChannel.Paint), radius, 0.03, 8, 0.0,
                                0, false, true, ShapeLod.Lod0);
                return;
            }
            double len = radius - spinnerR * 0.6;
            double chord = Math.Max(0.16, radius * 0.11);
            uint tip = (PropTip << 8) | 0xFF;
            for (int i = 0; i < blades; i++)
            {
                double a = 2 * Pi * i / blades;
                // Blade along local +Y, twisted 20° about its own axis; the outer tenth painted light.
                Affine3 f = Affine3.RotationZ(a) * Affine3.Translation(0, spinnerR * 0.6 + len * 0.5, 0) * Affine3.RotationY(0.35);
                int v0 = k.M.VertexCount;
                Shapes.Superellipsoid(k.M, f, Kit.B(PropBlade, MaterialChannel.Paint), chord * 0.5, len * 0.5, chord * 0.12, 0.6, 0.6, 8, ShapeLod.Lod0);
                double tipR = spinnerR * 0.6 + len * 0.86, ux = -Math.Sin(a), uy = Math.Cos(a);
                for (int v = v0; v < k.M.VertexCount; v++)
                {
                    double px = k.M.Positions[v * 3], py = k.M.Positions[v * 3 + 1];
                    if (px * ux + py * uy < tipR) continue;
                    k.M.Colors[v * 4] = (byte)(tip >> 24);
                    k.M.Colors[v * 4 + 1] = (byte)(tip >> 16);
                    k.M.Colors[v * 4 + 2] = (byte)(tip >> 8);
                }
            }
        }

        /// <summary>Adds triangle (a, b, c) wound so its face points away from (ox, oy, oz).</summary>
        private static void TriOut(MeshData m, int a, int b, int c, double ox, double oy, double oz)
        {
            float[] p = m.Positions;
            double ax = p[a * 3], ay = p[a * 3 + 1], az = p[a * 3 + 2];
            double bx = p[b * 3] - ax, by = p[b * 3 + 1] - ay, bz = p[b * 3 + 2] - az;
            double cx = p[c * 3] - ax, cy = p[c * 3 + 1] - ay, cz = p[c * 3 + 2] - az;
            // Unity front face: cross(b - a, c - a) (MeshData.AddTriangle: clockwise seen from the front).
            double nx = by * cz - bz * cy, ny = bz * cx - bx * cz, nz = bx * cy - by * cx;
            double mx = (ax + p[b * 3] + p[c * 3]) / 3 - ox, my = (ay + p[b * 3 + 1] + p[c * 3 + 1]) / 3 - oy, mz = (az + p[b * 3 + 2] + p[c * 3 + 2]) / 3 - oz;
            if (nx * mx + ny * my + nz * mz >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }
    }
}
