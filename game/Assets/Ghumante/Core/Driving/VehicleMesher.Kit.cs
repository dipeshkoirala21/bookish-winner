using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The vehicle part kit on the shapes library (docs/W2_DETAIL_CONTRACT.md §4): the build context, the shared
    /// palette and brushes (material channel per part, §5), and thin helpers that place rounded boxes,
    /// superellipsoids, cylinders, tubes, rounded panels and side/front/plan extrusions directly in the vehicle frame
    /// (+X right, +Y up, +Z forward, origin on the ground under the rear axle). Post-transforms bend the parts into
    /// car-like shapes (plan taper, tumblehome, curved aprons) with correct normals. Per-thread scratch only.
    /// </summary>
    public static partial class VehicleMesher
    {
        // -----------------------------------------------------------------------------------------------------------
        // Palette (packed 0xRRGGBBAA)
        // -----------------------------------------------------------------------------------------------------------

        internal static readonly uint TyreC = MeshColor.FromHex(0x2A2C30), TrimC = MeshColor.FromHex(0x26292E), ChromeC = MeshColor.FromHex(0xD9DEE3),
                                      AlloyC = MeshColor.FromHex(0xB8BEC4), AlloyDarkC = MeshColor.FromHex(0x4A4F55), SteelC = MeshColor.FromHex(0x8A9198),
                                      GlassC = MeshColor.FromHex(0x2E4456), ScreenC = MeshColor.FromHex(0x3B566B), LensC = MeshColor.FromHex(0xF4F1E0),
                                      DrlC = MeshColor.FromHex(0xFFFFFF), TailC = MeshColor.FromHex(0xD32F2F), AmberC = MeshColor.FromHex(0xFFA000),
                                      SeatC = MeshColor.FromHex(0x2B2B2E), CrankC = MeshColor.FromHex(0xAEB4BA), FinC = MeshColor.FromHex(0x5E646B),
                                      EngineC = MeshColor.FromHex(0x2B2E33), UnderC = MeshColor.FromHex(0x1C1E21), MirrorC = MeshColor.FromHex(0x9FB7C9),
                                      WoodC = MeshColor.FromHex(0x8D5A34), ReflectC = MeshColor.FromHex(0xE65100);

        // -----------------------------------------------------------------------------------------------------------
        // Build context
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>What every family builder gets: the target mesh, the level, the shapes level and the model.</summary>
        internal struct Ctx
        {
            public MeshData M;
            public VehicleLod Lod;
            public ShapeLod S;
            public byte Model;
            public uint Seed;

            /// <summary>Full detail (player and nearest traffic).</summary>
            public bool L0
            {
                get { return Lod == VehicleLod.Lod0; }
            }

            /// <summary>LOD0 or LOD1: windows, lamps, mirrors and the medium parts.</summary>
            public bool L1
            {
                get { return Lod <= VehicleLod.Lod1; }
            }

            /// <summary>Radial segments for a part whose LOD0 count is <paramref name="n"/> (LOD2 keeps at least 6).</summary>
            public int R(int n)
            {
                return Lod == VehicleLod.Lod0 ? n : Lod == VehicleLod.Lod1 ? Math.Max(6, n / 2) : Math.Max(5, n / 3);
            }

            /// <summary>Bevel segments: LOD0 n, LOD1 about half, LOD2 1 (a chamfer).</summary>
            public int B(int n)
            {
                return Lod == VehicleLod.Lod0 ? n : Lod == VehicleLod.Lod1 ? Math.Max(1, (n + 1) / 2) : 1;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Brushes
        // -----------------------------------------------------------------------------------------------------------

        internal static ShapeBrush Paint(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Paint, ao);
        }

        internal static ShapeBrush Plain(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Plain, ao);
        }

        internal static ShapeBrush Metal(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Metal, ao);
        }

        internal static ShapeBrush Glass(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Glass, ao);
        }

        internal static ShapeBrush Rubber(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Rubber, ao);
        }

        internal static ShapeBrush Leather(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Leather, ao);
        }

        internal static ShapeBrush Fabric(uint c, float ao = 1f)
        {
            return new ShapeBrush(c, MaterialChannel.Fabric, ao);
        }

        internal static ShapeBrush Trim(float ao = 1f)
        {
            return new ShapeBrush(TrimC, MaterialChannel.Plain, ao);
        }

        internal static ShapeBrush Chrome(float ao = 1f)
        {
            return new ShapeBrush(ChromeC, MaterialChannel.Metal, ao);
        }

        /// <summary>A darker or lighter shade of a packed colour (alpha kept).</summary>
        internal static uint Shade(uint c, float f)
        {
            return MeshColor.Scale(c, f);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Transforms
        // -----------------------------------------------------------------------------------------------------------

        internal static Affine3 T(double x, double y, double z)
        {
            return Affine3.Translation(x, y, z);
        }

        /// <summary>Local +Y of the part along the vehicle's +X (wheels, lathes across the body).</summary>
        internal static Affine3 AcrossX(double x, double y, double z)
        {
            return Affine3.FromBasis(0, -1, 0, 1, 0, 0, 0, 0, 1, x, y, z);
        }

        /// <summary>Local +Y of the part along the vehicle's +Z (lamps facing forward, pipes along the body).</summary>
        internal static Affine3 AlongZ(double x, double y, double z)
        {
            return Affine3.FromBasis(1, 0, 0, 0, 0, 1, 0, -1, 0, x, y, z);
        }

        /// <summary>Local +Y of the part along the vehicle's −Z (tail lamps, rear plates).</summary>
        internal static Affine3 AlongMinusZ(double x, double y, double z)
        {
            return Affine3.FromBasis(-1, 0, 0, 0, 0, -1, 0, -1, 0, x, y, z);
        }

        /// <summary>Local +Y along an arbitrary unit direction (dx, dy, dz) at (x, y, z).</summary>
        internal static Affine3 Facing(double x, double y, double z, double dx, double dy, double dz)
        {
            double l;
            Affine3 a = Affine3.Along(x, y, z, x + dx, y + dy, z + dz, out l);
            return a;
        }

        /// <summary>Pitch about the vehicle X axis (positive tips +Y toward +Z) then translate.</summary>
        internal static Affine3 Pitched(double x, double y, double z, double rad)
        {
            return Affine3.Translation(x, y, z) * Affine3.RotationX(rad);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Primitives in the vehicle frame
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>A box with rounded edges centred at (x, y, z), sizes sx × sy × sz, edge radius r.</summary>
        internal static int RBox(ref Ctx c, in ShapeBrush b, double x, double y, double z, double sx, double sy, double sz, double r, int seg = 2)
        {
            return RBox(ref c, b, T(x, y, z), sx, sy, sz, r, seg);
        }

        /// <summary>A flat panel (truck box sides, planks, bed walls): rounded at LOD0, a plain box beyond.</summary>
        internal static int Slab(ref Ctx c, in ShapeBrush b, double x, double y, double z, double sx, double sy, double sz, double r, int seg = 1)
        {
            return c.L0 ? RBox(ref c, b, x, y, z, sx, sy, sz, r, seg) : OBox(ref c, b, T(x, y, z), sx, sy, sz);
        }

        /// <summary>Rounded boxes whose largest side is under this many metres become plain boxes at LOD1.</summary>
        private const double SmallPartM = 0.22;

        /// <summary>A rounded box placed by an arbitrary transform (centred on its local origin).</summary>
        internal static int RBox(ref Ctx c, in ShapeBrush b, in Affine3 xf, double sx, double sy, double sz, double r, int seg = 2)
        {
            // LOD2, and small parts at LOD1 (a rounded box costs at least 108 triangles), are plain boxes.
            if (c.Lod == VehicleLod.Lod2 || (c.Lod == VehicleLod.Lod1 && Math.Max(sx, Math.Max(sy, sz)) < SmallPartM)) return OBox(ref c, b, xf, sx, sy, sz);
            return Shapes.RoundedBox(c.M, xf, b, sx, sy, sz, Math.Min(r, 0.49 * Math.Min(sx, Math.Min(sy, sz))), seg, c.S);
        }

        /// <summary>A superellipsoid centred at (x, y, z) with radii (rx, ry, rz) and exponents (vertical, horizontal).</summary>
        internal static int Ell(ref Ctx c, in ShapeBrush b, double x, double y, double z, double rx, double ry, double rz, double ev = 1,
                                double eh = 1, int seg = 12)
        {
            return Shapes.Superellipsoid(c.M, T(x, y, z), b, rx, ry, rz, ev, eh, seg, c.S);
        }

        internal static int Ell(ref Ctx c, in ShapeBrush b, in Affine3 xf, double rx, double ry, double rz, double ev = 1, double eh = 1,
                                int seg = 12)
        {
            return Shapes.Superellipsoid(c.M, xf, b, rx, ry, rz, ev, eh, seg, c.S);
        }

        /// <summary>A closed cylinder from a to b (rounded rims of <paramref name="rim"/>).</summary>
        internal static int Cyl(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r, int seg = 12,
                                double rim = 0)
        {
            double len;
            Affine3 xf = Affine3.Along(ax, ay, az, bx, by, bz, out len);
            if (len < 1e-6) return c.M.VertexCount;
            double rr = Math.Min(rim, Math.Min(0.45 * r, 0.45 * len));
            return Shapes.Cylinder(c.M, xf, b, r, len, seg, rr, rr > 0 ? 2 : 0, true, true, c.S);
        }

        /// <summary>A frustum from a (radius ra) to b (radius rb), capped.</summary>
        internal static int Cone(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double ra, double rb,
                                 int seg = 12, double rim = 0)
        {
            double len;
            Affine3 xf = Affine3.Along(ax, ay, az, bx, by, bz, out len);
            if (len < 1e-6) return c.M.VertexCount;
            double rr = Math.Min(rim, Math.Min(0.45 * Math.Min(ra, rb), 0.45 * len));
            return Shapes.Frustum(c.M, xf, b, ra, rb, len, seg, rr, rr > 0 ? 2 : 0, true, true, c.S);
        }

        /// <summary>A round bar from a to b (capped).</summary>
        internal static int Rod(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r, int seg = 8)
        {
            return Shapes.Bar(c.M, Affine3.Identity, b, ax, ay, az, bx, by, bz, r, seg, c.S);
        }

        /// <summary>A capsule (rounded ends) from a to b.</summary>
        internal static int Caps(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r, int seg = 10)
        {
            double dx = bx - ax, dy = by - ay, dz = bz - az, len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6) return Shapes.Sphere(c.M, T(ax, ay, az), b, r, seg, c.S);
            double ex = dx / len * r, ey = dy / len * r, ez = dz / len * r, l2;
            Affine3 xf = Affine3.Along(ax - ex, ay - ey, az - ez, bx + ex, by + ey, bz + ez, out l2);
            return Shapes.Capsule(c.M, xf, b, r, l2, seg, c.S);
        }

        /// <summary>A sphere at (x, y, z).</summary>
        internal static int Ball(ref Ctx c, in ShapeBrush b, double x, double y, double z, double r, int seg = 10)
        {
            return Shapes.Superellipsoid(c.M, T(x, y, z), b, r, r, r, 1, 1, seg, c.S);
        }

        /// <summary>A round tube along the scratch path <see cref="P"/> (filled by the caller).</summary>
        internal static int Tube(ref Ctx c, in ShapeBrush b, Path3 path, double r, int seg = 8, bool caps = true, double rEnd = -1)
        {
            return Shapes.Tube(c.M, Affine3.Identity, b, path, r, seg, caps, false, c.S, rEnd);
        }

        /// <summary>A torus segment about the vehicle X axis centred at (x, y, z): arcs over wheels (mudguards, arch
        /// lips). Angles: 0 at +Z (forward), increasing toward +Y (up) when seen from +X.</summary>
        internal static int ArcX(ref Ctx c, in ShapeBrush b, double x, double y, double z, double major, double minor, double fromDeg, double sweepDeg,
                                 int majorSeg = 20, int minorSeg = 8)
        {
            // Torus lies in the local XZ plane (about +Y), compass angles from +Z clockwise seen from above. Map local Y to
            // vehicle X; local Z to vehicle +Z; local X to vehicle -Y so the compass angle grows from +Z toward +Y.
            Affine3 xf = Affine3.FromBasis(0, -1, 0, 1, 0, 0, 0, 0, 1, x, y, z);
            return Shapes.Torus(c.M, xf, b, major, minor, majorSeg, minorSeg, c.S, -fromDeg - sweepDeg, sweepDeg, true);
        }

        /// <summary>
        /// A mudguard or arch shell over a wheel: a crescent section <paramref name="width"/> wide and
        /// <paramref name="thick"/> thick, swept round the vehicle-X axis through (x, y, z) at radius
        /// <paramref name="radius"/>, from <paramref name="fromDeg"/> (0 = forward, 90 = up) over
        /// <paramref name="sweepDeg"/>. Capped ends.
        /// </summary>
        internal static int FenderArc(ref Ctx c, in ShapeBrush b, double x, double y, double z, double radius, double width, double thick,
                                      double fromDeg, double sweepDeg, int seg = 28)
        {
            double hw = 0.5 * width, crown = Math.Min(0.25 * width, 0.03);
            Profile2 p = Prof2.Clear(true);
            if (c.L0)
            {
                // Closed crescent in (radius, axial) space: outer surface bulges outward at the centre.
                p.Add(radius, -hw, true).Add(radius + 0.6 * crown, -0.5 * hw, false).Add(radius + crown, 0, false).Add(radius + 0.6 * crown, 0.5 * hw, false)
                 .Add(radius, hw, true).Add(radius - thick, hw * 0.92, true).Add(radius - thick + crown * 0.6, 0, false).Add(radius - thick, -hw * 0.92, true);
            }
            else
            {
                // Farther away a plain rectangular section, on fewer segments.
                p.Add(radius + 0.5 * crown, -hw, true).Add(radius + 0.5 * crown, hw, true).Add(radius - thick, hw, true).Add(radius - thick, -hw, true);
                seg = Math.Max(6, (seg * 2) / 3);
            }
            Affine3 xf = AcrossX(x, y, z);
            // Compass angle a of the lathe maps to vehicle (Y = −r sin a, Z = r cos a): θ (from +Z toward +Y) = −a.
            return Shapes.Lathe(c.M, xf, b, p, seg, c.S, -fromDeg - sweepDeg, sweepDeg, true);
        }

        /// <summary>A coil spring (helix tube) from a to b: shock absorbers. LOD1 and below draw a plain cylinder.</summary>
        internal static void Spring(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double r, double wire,
                                    int turns)
        {
            if (!c.L0)
            {
                Cyl(ref c, b, ax, ay, az, bx, by, bz, r, 8);
                return;
            }
            double len;
            Affine3 xf = Affine3.Along(ax, ay, az, bx, by, bz, out len);
            Path3 path = P;
            int n = turns * 5;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n, a = 2 * Math.PI * turns * t, px, py, pz;
                xf.Point(r * Math.Cos(a), len * t, r * Math.Sin(a), out px, out py, out pz);
                path.Add(px, py, pz);
            }
            Tube(ref c, b, path, wire, 3, false);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Extrusions (BevelExtrude with the polygon in a vehicle plane)
        // -----------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] s_pa, s_pb;
        [ThreadStatic] private static Profile2 s_prof, s_prof2;
        [ThreadStatic] private static Path3 s_path;

        /// <summary>Per-thread polygon scratch (two coordinate arrays of at least n).</summary>
        internal static void Poly(int n, out double[] a, out double[] b)
        {
            if (s_pa == null || s_pa.Length < n)
            {
                s_pa = new double[Math.Max(64, n)];
                s_pb = new double[Math.Max(64, n)];
            }
            a = s_pa;
            b = s_pb;
        }

        /// <summary>Per-thread profile scratch (the caller fills it).</summary>
        internal static Profile2 Prof
        {
            get { return s_prof ?? (s_prof = new Profile2(64)); }
        }

        internal static Profile2 Prof2
        {
            get { return s_prof2 ?? (s_prof2 = new Profile2(64)); }
        }

        /// <summary>Per-thread path scratch (cleared).</summary>
        internal static Path3 P
        {
            get { return (s_path ?? (s_path = new Path3(64))).Clear(); }
        }

        /// <summary>Rounds the corners of a polygon with <paramref name="seg"/> segments at LOD0, about half at LOD1 and
        /// none at LOD2 (the extrusion bevel still rounds its edges).</summary>
        internal static Profile2 Fil(ref Ctx c, Profile2 p, double r, int seg, bool all = true)
        {
            int n = c.Lod == VehicleLod.Lod0 ? seg : c.Lod == VehicleLod.Lod1 ? Math.Max(1, seg / 2) : 0;
            return n > 0 ? p.FilletCorners(r, n, all) : p;
        }

        /// <summary>
        /// A side-view polygon (z, y) extruded across the vehicle from x0 to x1 with every edge of its outline rounded
        /// by <paramref name="bevel"/> on both sides (roofs, hoods, arches, tanks, tail cowls). The polygon may be concave
        /// (wheel-arch notches); fillet sharp corners first with <see cref="Profile2.FilletCorners"/>.
        /// </summary>
        internal static int SideExtrude(ref Ctx c, in ShapeBrush b, Profile2 zy, double x0, double x1, double bevel, int seg = 3)
        {
            double w = x1 - x0;
            if (w <= 1e-4 || zy.Count < 3) return c.M.VertexCount;
            double bv = Math.Min(bevel, 0.45 * w);
            // Local (x, y, z) → vehicle (Z = x, X = x0 + y, Y = z).
            Affine3 xf = Affine3.FromBasis(0, 0, 1, 1, 0, 0, 0, 1, 0, x0, 0, 0);
            return Shapes.BevelExtrude(c.M, xf, b, zy.X, zy.Y, zy.Count, w, bv, seg, BevelStyle.Round, true, true, bv, 30, c.S);
        }

        /// <summary>A front-view polygon (x, y) extruded along the vehicle from z0 to z1 with rounded edges.</summary>
        internal static int FrontExtrude(ref Ctx c, in ShapeBrush b, Profile2 xy, double z0, double z1, double bevel, int seg = 3)
        {
            double d = z1 - z0;
            if (d <= 1e-4 || xy.Count < 3) return c.M.VertexCount;
            double bv = Math.Min(bevel, 0.45 * d);
            // Local (x, y, z) → vehicle (X = x, Z = z0 + y, Y = z).
            Affine3 xf = Affine3.FromBasis(1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, z0);
            return Shapes.BevelExtrude(c.M, xf, b, xy.X, xy.Y, xy.Count, d, bv, seg, BevelStyle.Round, true, true, bv, 30, c.S);
        }

        /// <summary>A plan polygon (x, z) extruded up from y0 to y1 with rounded top and bottom edges.</summary>
        internal static int PlanExtrude(ref Ctx c, in ShapeBrush b, Profile2 xz, double y0, double y1, double bevel, double bottomBevel, int seg = 3)
        {
            double h = y1 - y0;
            if (h <= 1e-4 || xz.Count < 3) return c.M.VertexCount;
            double bv = Math.Min(bevel, 0.45 * h), bb = Math.Min(bottomBevel, 0.45 * h);
            return Shapes.BevelExtrude(c.M, T(0, y0, 0), b, xz.X, xz.Y, xz.Count, h, bv, seg, BevelStyle.Round, true, true, bb, 30, c.S);
        }

        /// <summary>
        /// A thin rounded panel (lamp lens, window, plate) of w × h with corner radius r and thickness t, centred at
        /// (x, y, z) and facing (nx, ny, nz); its "up" on the face is the vehicle's +Y projected (or +Z for faces that
        /// look up or down). The back face sits on the surface, the front is proud by t. About 40 triangles.
        /// </summary>
        internal static int Panel(ref Ctx c, in ShapeBrush b, double x, double y, double z, double nx, double ny, double nz, double w, double h,
                                  double r, double t = 0.008, double roll = 0)
        {
            Affine3 xf = FaceFrame(x, y, z, nx, ny, nz) * Affine3.RotationY(roll);
            double rr = Math.Min(r, 0.49 * Math.Min(w, h));
            if (c.Lod != VehicleLod.Lod0 || rr < 0.004) return Decal(ref c, b, x, y, z, nx, ny, nz, w, h, r, t, roll);
            Profile2 p = Prof2.SetRoundedRect(w, h, rr, 1);
            return Shapes.BevelExtrude(c.M, xf, b, p.X, p.Y, p.Count, t, Math.Min(0.45 * t, rr), 1, BevelStyle.Chamfer, true, false, 0, 30,
                                       ShapeLod.Lod0);
        }

        /// <summary>A flat one-sided rounded rectangle (decal, stripe, graphic) lifted <paramref name="lift"/> off the
        /// surface along its normal: a triangle fan, 6-14 triangles.</summary>
        internal static int Decal(ref Ctx c, in ShapeBrush b, double x, double y, double z, double nx, double ny, double nz, double w, double h,
                                  double r, double lift = 0.003, double roll = 0)
        {
            Affine3 xf = FaceFrame(x, y, z, nx, ny, nz) * Affine3.RotationY(roll);
            double rr = Math.Min(r, 0.49 * Math.Min(w, h));
            int seg = c.Lod == VehicleLod.Lod0 ? 2 : c.Lod == VehicleLod.Lod1 ? 1 : 0;
            Profile2 p = seg > 0 && rr > 0.002 ? Prof2.SetRoundedRect(w, h, rr, seg) : Prof2.SetRect(w, h);
            int first = c.M.VertexCount;
            double cx, cy, cz, vx, vy, vz;
            xf.Point(0, lift, 0, out cx, out cy, out cz);
            xf.Normal(0, 1, 0, out vx, out vy, out vz);
            ShapeEmit.Normalize(ref vx, ref vy, ref vz);
            int centre = c.M.AddVertex((float)cx, (float)cy, (float)cz, (float)vx, (float)vy, (float)vz, b.Color, (float)b.Channel, b.Ao);
            for (int i = 0; i < p.Count; i++)
            {
                double px, py, pz;
                xf.Point(p.X[i], lift, p.Y[i], out px, out py, out pz);
                c.M.AddVertex((float)px, (float)py, (float)pz, (float)vx, (float)vy, (float)vz, b.Color, (float)b.Channel, b.Ao);
            }
            for (int i = 0; i < p.Count; i++) ShapeEmit.TriOriented(c.M, centre, centre + 1 + i, centre + 1 + (i + 1) % p.Count);
            return first;
        }

        /// <summary>A plain oriented box (12 flat-shaded triangles) centred on <paramref name="xf"/>'s origin: spokes,
        /// fins, lugs, brackets, slats; small parts where rounding would not show.</summary>
        internal static int OBox(ref Ctx c, in ShapeBrush b, in Affine3 xf, double sx, double sy, double sz)
        {
            int first = c.M.VertexCount;
            double hx = 0.5 * sx, hy = 0.5 * sy, hz = 0.5 * sz;
            for (int f = 0; f < 6; f++)
            {
                int axis = f >> 1;
                double sg = (f & 1) == 0 ? -1 : 1;
                double fnx = axis == 0 ? sg : 0, fny = axis == 1 ? sg : 0, fnz = axis == 2 ? sg : 0;
                double wx, wy, wz;
                xf.Normal(fnx, fny, fnz, out wx, out wy, out wz);
                ShapeEmit.Normalize(ref wx, ref wy, ref wz);
                int v0 = c.M.VertexCount;
                for (int k = 0; k < 4; k++)
                {
                    double a = (k == 1 || k == 2) ? 1 : -1, bb = k >= 2 ? 1 : -1, lx, ly, lz;
                    if (axis == 0)
                    {
                        lx = sg * hx;
                        ly = a * hy;
                        lz = bb * hz;
                    }
                    else if (axis == 1)
                    {
                        ly = sg * hy;
                        lx = a * hx;
                        lz = bb * hz;
                    }
                    else
                    {
                        lz = sg * hz;
                        lx = a * hx;
                        ly = bb * hy;
                    }
                    double px, py, pz;
                    xf.Point(lx, ly, lz, out px, out py, out pz);
                    c.M.AddVertex((float)px, (float)py, (float)pz, (float)wx, (float)wy, (float)wz, b.Color, (float)b.Channel, b.Ao);
                }
                ShapeEmit.TriOriented(c.M, v0, v0 + 1, v0 + 2);
                ShapeEmit.TriOriented(c.M, v0, v0 + 2, v0 + 3);
            }
            return first;
        }

        /// <summary>An oriented box between two points (its long axis from a to b), cross-section sx × sz.</summary>
        internal static int OBar(ref Ctx c, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz, double sx, double sz)
        {
            double len;
            Affine3 xf = Affine3.Along(ax, ay, az, bx, by, bz, out len);
            return OBox(ref c, b, xf * T(0, 0.5 * len, 0), sx, len, sz);
        }

        /// <summary>Frame for a face: local +Y = normal, local X = the face's right, local Z = the face's up.</summary>
        internal static Affine3 FaceFrame(double x, double y, double z, double nx, double ny, double nz)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= l;
            ny /= l;
            nz /= l;
            // Up hint: +Y unless the face looks (mostly) up or down, then +Z.
            double ux = 0, uy = 1, uz = 0;
            if (Math.Abs(ny) > 0.8)
            {
                uy = 0;
                uz = 1;
            }
            double d = ux * nx + uy * ny + uz * nz;
            ux -= d * nx;
            uy -= d * ny;
            uz -= d * nz;
            double ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            // Right = up × normal (any consistent choice: panels are symmetric).
            double rx = uy * nz - uz * ny, ry = uz * nx - ux * nz, rz = ux * ny - uy * nx;
            return Affine3.FromBasis(rx, ry, rz, nx, ny, nz, ux, uy, uz, x, y, z);
        }

        /// <summary>A round lamp: a short housing (rim colour) with a domed lens facing (nx, ny, nz).</summary>
        internal static void Lamp(ref Ctx c, uint lens, uint rim, double x, double y, double z, double nx, double ny, double nz, double r, double depth,
                                  int seg = 16)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= l;
            ny /= l;
            nz /= l;
            if (rim != 0)
                Cyl(ref c, Metal(rim), x - nx * depth, y - ny * depth, z - nz * depth, x, y, z, r * 1.12, seg, 0.25 * r);
            if (c.Lod == VehicleLod.Lod2)
            {
                Cyl(ref c, Glass(lens), x - nx * 0.01, y - ny * 0.01, z - nz * 0.01, x + nx * 0.004, y + ny * 0.004, z + nz * 0.004, r, seg);
                return;
            }
            Shapes.Dome(c.M, Facing(x, y, z, nx, ny, nz), Glass(lens), r, 0.3 * r, seg, false, c.S);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Hull: a smooth loft through rounded-rectangle sections (bodies, tanks, noses, cabs)
        // -----------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] s_hull;
        [ThreadStatic] private static int s_hullN;

        /// <summary>Starts a hull (see <see cref="HullAdd"/>, <see cref="HullEmit"/>).</summary>
        internal static void HullBegin()
        {
            if (s_hull == null) s_hull = new double[7 * 32];
            s_hullN = 0;
        }

        /// <summary>
        /// Adds a hull section at station <paramref name="t"/> along the hull axis: a rounded rectangle centred at
        /// (<paramref name="cu"/>, <paramref name="cv"/>) in the cross plane, half sizes <paramref name="hu"/> ×
        /// <paramref name="hv"/>, corner radius <paramref name="rTop"/> on the +v corners and <paramref name="rBottom"/>
        /// on the −v corners. Stations must increase.
        /// </summary>
        internal static void HullAdd(double t, double cu, double cv, double hu, double hv, double rTop, double rBottom)
        {
            if (7 * (s_hullN + 1) > s_hull.Length) Array.Resize(ref s_hull, s_hull.Length * 2);
            int o = 7 * s_hullN++;
            s_hull[o] = t;
            s_hull[o + 1] = cu;
            s_hull[o + 2] = cv;
            s_hull[o + 3] = Math.Max(1e-4, hu);
            s_hull[o + 4] = Math.Max(1e-4, hv);
            s_hull[o + 5] = rTop;
            s_hull[o + 6] = rBottom;
        }

        /// <summary>
        /// Emits the hull: one smooth grid through every section (normals rebuilt from the surface) with flat caps on
        /// the first and last section when asked. <paramref name="alongY"/> runs the hull up the vehicle Y axis with the
        /// cross plane (x, z) (+v = forward), otherwise along Z with the cross plane (x, y) (+v = up). Corner arcs have
        /// <paramref name="seg"/> segments at LOD0 (fewer further out). Returns the first vertex.
        /// </summary>
        internal static int HullEmit(ref Ctx c, in ShapeBrush b, int seg, bool capStart, bool capEnd, bool alongY = false)
        {
            int n = s_hullN;
            int first = c.M.VertexCount, i0 = c.M.IndexCount;
            if (n < 2) return first;
            int k = c.Lod == VehicleLod.Lod0 ? seg : c.Lod == VehicleLod.Lod1 ? Math.Max(1, seg / 3) : 1;
            int per = 4 * (k + 1);
            if (c.Lod == VehicleLod.Lod2 && n > 4)
            {
                // Far: keep every other section (and both ends).
                int w = 0;
                for (int i = 0; i < n; i++)
                    if (i == 0 || i == n - 1 || (i & 1) == 0)
                    {
                        if (w != i) Array.Copy(s_hull, 7 * i, s_hull, 7 * w, 7);
                        w++;
                    }
                n = w;
                s_hullN = n;
            }
            for (int i = 0; i < n; i++)
            {
                int o = 7 * i;
                double t = s_hull[o], cu = s_hull[o + 1], cv = s_hull[o + 2], hu = s_hull[o + 3], hv = s_hull[o + 4];
                double lim = 0.999 * Math.Min(hu, hv), r1 = Math.Min(Math.Max(0, s_hull[o + 5]), lim), r2 = Math.Min(Math.Max(0, s_hull[o + 6]), lim);
                for (int q = 0; q < 4; q++)
                {
                    // Corners counter-clockwise from bottom-right: (+u, −v), (+u, +v), (−u, +v), (−u, −v).
                    double su = q == 0 || q == 1 ? 1 : -1, sv = q == 1 || q == 2 ? 1 : -1, r = sv > 0 ? r1 : r2;
                    double a0 = -0.5 * Math.PI + 0.5 * Math.PI * q;
                    for (int j = 0; j <= k; j++)
                    {
                        double a = a0 + 0.5 * Math.PI * j / k, ca = Math.Cos(a), sa = Math.Sin(a);
                        double u = cu + su * (hu - r) + r * ca, v = cv + sv * (hv - r) + r * sa;
                        // Provisional normal (rebuilt below): the corner direction.
                        double nu = r > 1e-6 ? ca : su * 0.7, nv = r > 1e-6 ? sa : sv * 0.7;
                        if (alongY) ShapeEmit.Vertex(c.M, Affine3.Identity, b, u, t, v, nu, 0, nv);
                        else ShapeEmit.Vertex(c.M, Affine3.Identity, b, u, v, t, nu, nv, 0);
                    }
                }
            }
            ShapeEmit.Grid(c.M, first, n, per, true, false, null, null);
            ShapeNoise.RecomputeNormals(c.M, first, c.M.VertexCount - first, i0, c.M.IndexCount - i0, false);
            if (capStart) HullCap(ref c, b, first, per, 0, alongY);
            if (capEnd) HullCap(ref c, b, first + (n - 1) * per, per, n - 1, alongY);
            return first;
        }

        private static void HullCap(ref Ctx c, in ShapeBrush b, int ring, int per, int section, bool alongY)
        {
            int o = 7 * section;
            double dir = section == 0 ? -1 : 1;
            double t = s_hull[o], cu = s_hull[o + 1], cv = s_hull[o + 2];
            float[] p = c.M.Positions;
            double nx = 0, ny = alongY ? dir : 0, nz = alongY ? 0 : dir;
            int centre = alongY ? ShapeEmit.Vertex(c.M, Affine3.Identity, b, cu, t, cv, nx, ny, nz) : ShapeEmit.Vertex(c.M, Affine3.Identity, b, cu, cv, t, nx, ny, nz);
            int v0 = c.M.VertexCount;
            for (int j = 0; j < per; j++)
            {
                int v = ring + j;
                c.M.AddVertex(p[3 * v], p[3 * v + 1], p[3 * v + 2], (float)nx, (float)ny, (float)nz, b.Color, (float)b.Channel, b.Ao);
            }
            for (int j = 0; j < per; j++) ShapeEmit.TriOriented(c.M, centre, v0 + j, v0 + (j + 1) % per);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Post-transforms (vertices from `from` to the end)
        // -----------------------------------------------------------------------------------------------------------

        private static double Smooth01(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        private static double Smooth01d(double t)
        {
            if (t <= 0 || t >= 1) return 0;
            return 6 * t * (1 - t);
        }

        /// <summary>Scales x by s(z), easing from 1 at <paramref name="zIn"/> to <paramref name="sEnd"/> at
        /// <paramref name="zEnd"/> (either direction): a nose or tail narrowing in plan.</summary>
        internal static void TaperByZ(MeshData m, int from, double zIn, double zEnd, double sEnd)
        {
            double span = zEnd - zIn;
            if (Math.Abs(span) < 1e-6) return;
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x = p[3 * v], z = p[3 * v + 2], t = (z - zIn) / span;
                if (t <= 0) continue;
                double s = 1 + (sEnd - 1) * Smooth01(t), ds = (sEnd - 1) * Smooth01d(t) / span;
                p[3 * v] = (float)(x * s);
                double nx = n[3 * v] / s, ny = n[3 * v + 1], nz = n[3 * v + 2] - x * ds * n[3 * v] / s;
                Norm(n, v, nx, ny, nz);
            }
        }

        /// <summary>Scales x by s(y), easing from 1 at <paramref name="y0"/> to <paramref name="sTop"/> at
        /// <paramref name="y1"/> (tumblehome of a cabin, the taper of a tank).</summary>
        internal static void TaperByY(MeshData m, int from, double y0, double y1, double sTop)
        {
            double span = y1 - y0;
            if (Math.Abs(span) < 1e-6) return;
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x = p[3 * v], y = p[3 * v + 1], t = (y - y0) / span;
                if (t <= 0) continue;
                if (t > 1) t = 1;
                double s = 1 + (sTop - 1) * t, ds = t >= 1 ? 0 : (sTop - 1) / span;
                p[3 * v] = (float)(x * s);
                double nx = n[3 * v] / s, ny = n[3 * v + 1] - x * ds * n[3 * v] / s, nz = n[3 * v + 2];
                Norm(n, v, nx, ny, nz);
            }
        }

        /// <summary>Moves z by −k·x² (a front apron or windscreen that curves back at its sides).</summary>
        internal static void BendZ(MeshData m, int from, double k)
        {
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x = p[3 * v];
                p[3 * v + 2] = (float)(p[3 * v + 2] - k * x * x);
                double fp = -2 * k * x;
                Norm(n, v, n[3 * v] - fp * n[3 * v + 2], n[3 * v + 1], n[3 * v + 2]);
            }
        }

        /// <summary>Scales x by s(y) as a smooth bulge: 1 + k·sin(π·t) for t = (y − y0)/(y1 − y0) in [0, 1] (rounded
        /// flanks of a body).</summary>
        internal static void BulgeX(MeshData m, int from, double y0, double y1, double k)
        {
            double span = y1 - y0;
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x = p[3 * v], y = p[3 * v + 1], t = (y - y0) / span;
                if (t <= 0 || t >= 1) continue;
                double s = 1 + k * Math.Sin(Math.PI * t), ds = k * Math.PI * Math.Cos(Math.PI * t) / span;
                p[3 * v] = (float)(x * s);
                Norm(n, v, n[3 * v] / s, n[3 * v + 1] - x * ds * n[3 * v] / s, n[3 * v + 2]);
            }
        }

        /// <summary>Curves the ends of a body back at the sides in plan: z moves by −k·x²·w(z), w easing from 0 at
        /// <paramref name="zIn"/> to 1 at <paramref name="zEnd"/> (either direction; for the tail pass a negative k so
        /// the corners move forward).</summary>
        internal static void CurveEnd(MeshData m, int from, double zIn, double zEnd, double k)
        {
            double span = zEnd - zIn;
            if (Math.Abs(span) < 1e-6) return;
            float[] p = m.Positions, n = m.Normals;
            for (int v = from; v < m.VertexCount; v++)
            {
                double x = p[3 * v], z = p[3 * v + 2], t = (z - zIn) / span;
                if (t <= 0) continue;
                double w = Smooth01(t > 1 ? 1 : t), dw = t >= 1 ? 0 : Smooth01d(t) / span;
                double fx = -2 * k * x * w, fz = -k * x * x * dw;
                p[3 * v + 2] = (float)(z - k * x * x * w);
                double nz = n[3 * v + 2], d = 1 + fz;
                if (Math.Abs(d) < 0.2) d = d < 0 ? -0.2 : 0.2;
                Norm(n, v, n[3 * v] - fx * nz / d, n[3 * v + 1], nz / d);
            }
        }

        private static void Norm(float[] n, int v, double nx, double ny, double nz)
        {
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-12) return;
            n[3 * v] = (float)(nx / l);
            n[3 * v + 1] = (float)(ny / l);
            n[3 * v + 2] = (float)(nz / l);
        }

        /// <summary>Copies the vertices and triangles from (<paramref name="v0"/>, <paramref name="i0"/>) to the end,
        /// mirrored across x = 0 (the other side of a symmetric part).</summary>
        internal static void MirrorX(ref Ctx c, int v0, int i0)
        {
            int nv = c.M.VertexCount - v0, ni = c.M.IndexCount - i0;
            if (nv <= 0 || ni <= 0) return;
            Shapes.CopyTransformed(c.M, c.M, Affine3.Scaling(-1, 1, 1), 0xFFFFFFFFu, v0, nv, i0, ni);
        }

        /// <summary>Copies the vertices and triangles from (<paramref name="v0"/>, <paramref name="i0"/>) to the end
        /// through <paramref name="xf"/> (repeated parts: spokes, lugs, seats).</summary>
        internal static void Copy(ref Ctx c, int v0, int i0, in Affine3 xf)
        {
            int nv = c.M.VertexCount - v0, ni = c.M.IndexCount - i0;
            if (nv <= 0 || ni <= 0) return;
            Shapes.CopyTransformed(c.M, c.M, xf, 0xFFFFFFFFu, v0, nv, i0, ni);
        }

        /// <summary>Recolours the vertices from <paramref name="from"/> to the end whose y lies in [y0, y1] (paint
        /// bands on an extruded body whose vertices sit at the band edges).</summary>
        internal static void BandY(MeshData m, int from, double y0, double y1, uint color)
        {
            for (int v = from; v < m.VertexCount; v++)
            {
                double y = m.Positions[3 * v + 1];
                if (y < y0 || y > y1) continue;
                m.Colors[4 * v] = (byte)(color >> 24);
                m.Colors[4 * v + 1] = (byte)(color >> 16);
                m.Colors[4 * v + 2] = (byte)(color >> 8);
            }
        }

        /// <summary>Multiplies the AO (UV0.v) of the vertices from <paramref name="from"/> to the end by
        /// <paramref name="f"/> (inner, deep or hidden parts).</summary>
        internal static void Darken(MeshData m, int from, float f)
        {
            if (!m.HasUv0) return;
            for (int v = from; v < m.VertexCount; v++) m.Uv0[2 * v + 1] *= f;
        }

        /// <summary>Bakes AO over everything from (<paramref name="v0"/>, <paramref name="i0"/>): ground contact,
        /// undersides and creases (docs/W2_DETAIL_CONTRACT.md §5).</summary>
        internal static void BakeAo(MeshData m, int v0, int i0, bool ground)
        {
            AoSettings s = ShapeAo.Defaults(0);
            if (!ground) s.GroundFade = 0;
            s.Concavity = 0.5f;
            ShapeAo.Bake(m, v0, -1, i0, -1, s);
        }
    }
}
