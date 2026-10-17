using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Props
{
    /// <summary>
    /// The street furniture kit of the instanced dressing (W2 detail pass, package props; reference brief
    /// docs/research/w2/ref_props.md): every <see cref="StreetPropKind"/> built from the Shapes library as it stands on
    /// Kathmandu's streets today — the galvanised swan-neck grid lamp, the solar lamp, New Road's black-and-gold heritage
    /// lanterns, the blue bus stop sign, KMC's falcha bus shelter, the hooded three-aspect signal, cast-iron benches,
    /// compound gates, the community water tap, NEA's concrete pole with its coils of slack cable and the H-frame
    /// transformer, and the airport, mast, tower, tank, well, solar and chimney props. Frame: origin on the ground,
    /// +Y up, +Z forward (the side that faces the road or the traffic: lamp arms reach along +Z, signs and signal heads
    /// face +Z, shelters and benches open to +Z, gates let traffic through along Z), +X right. Built at
    /// <see cref="DefaultHeight"/> in metres (instances scale uniformly by their height over it). Every vertex carries
    /// UV0 = (material channel, baked AO) (detail-pass contract §5); vertex alpha is the instance tint mask (0 on fixed
    /// colours; 255 only on the tank body, which the renderer tints black or blue). Three levels: LOD0 (near, at most
    /// <see cref="Budget"/>[0] = 400 triangles: the existing per-prop budget of the instanced kit), LOD1 and LOD2.
    /// Deterministic and engine-free; allocation-free once the Shapes scratch has grown.
    /// </summary>
    public static partial class StreetPropMesher
    {
        /// <summary>Levels of detail per kind.</summary>
        public const int Lods = 3;

        /// <summary>Triangle caps per level: LOD0 is the instanced kit's per-prop budget (400), LOD1 under half of it,
        /// LOD2 under a fifth.</summary>
        public static readonly int[] Budget = { 400, 180, 72 };

        /// <summary>Number of kinds with a mesh (every <see cref="StreetPropKind"/>).</summary>
        public const int Kinds = (int)StreetPropKind.TrafficSignalLongMirrored + 1;

        /// <summary>Height (metres) a kind's mesh is built at; an instance of height h is scaled by h / this.</summary>
        public static float DefaultHeight(StreetPropKind k)
        {
            switch (k)
            {
                case StreetPropKind.StreetLamp: return 10f;
                case StreetPropKind.StreetLampSolar: return 8f;
                case StreetPropKind.StreetLampHeritage: return 6f;
                case StreetPropKind.BusStop: return 2.7f;
                case StreetPropKind.BusShelter: return 4.05f;
                case StreetPropKind.TrafficSignal:
                case StreetPropKind.TrafficSignalLong:
                case StreetPropKind.TrafficSignalMirrored:
                case StreetPropKind.TrafficSignalLongMirrored: return (float)SignalPoleTop;
                case StreetPropKind.Gate: return 3.2f;
                case StreetPropKind.StorageTank: return 2.6f;
                case StreetPropKind.Bench: return 0.85f;
                case StreetPropKind.WaterTap: return 1.0f;
                case StreetPropKind.Windsock: return 6f;
                case StreetPropKind.Helipad: return 0.06f;
                case StreetPropKind.TaxiStand: return 2.6f;
                case StreetPropKind.AerowayGate: return 2.4f;
                case StreetPropKind.ParkingPosition: return 0.03f;
                case StreetPropKind.Mast: return 25f;
                case StreetPropKind.Artwork: return 2.5f;
                case StreetPropKind.PowerTower: return 30f;
                case StreetPropKind.PowerPole: return 9f;
                case StreetPropKind.TransformerPole: return 9f;
                case StreetPropKind.Well: return 1.9f;
                case StreetPropKind.SolarPanel: return 1.6f;
                case StreetPropKind.Chimney: return 18f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Appends kind <paramref name="k"/> at level <paramref name="lod"/> (0..2) to <paramref name="m"/> and returns
        /// the triangles added (UV0 written, AO baked over the new vertices only).
        /// </summary>
        public static int Build(StreetPropKind k, int lod, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            lod = lod < 0 ? 0 : lod >= Lods ? Lods - 1 : lod;
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            float rayDistance = 0.4f;
            switch (k)
            {
                case StreetPropKind.StreetLamp: GridLamp(m, lod); break;
                case StreetPropKind.StreetLampSolar: SolarLamp(m, lod); break;
                case StreetPropKind.StreetLampHeritage: HeritageLamp(m, lod); break;
                case StreetPropKind.BusStop: SignPost(m, lod, Pictogram.Bus); break;
                case StreetPropKind.TaxiStand: SignPost(m, lod, Pictogram.Car); break;
                case StreetPropKind.AerowayGate: SignPost(m, lod, Pictogram.Gate); break;
                case StreetPropKind.BusShelter:
                    Falcha(m, lod);
                    rayDistance = 1.2f;
                    break;
                case StreetPropKind.TrafficSignal: Signal(m, lod, SignalReachM); break;
                case StreetPropKind.TrafficSignalLong: Signal(m, lod, SignalLongReachM); break;
                case StreetPropKind.TrafficSignalMirrored:
                    Signal(m, lod, SignalReachM);
                    MirrorX(m, v0, i0);
                    break;
                case StreetPropKind.TrafficSignalLongMirrored:
                    Signal(m, lod, SignalLongReachM);
                    MirrorX(m, v0, i0);
                    break;
                case StreetPropKind.Gate: CompoundGate(m, lod); break;
                case StreetPropKind.StorageTank: Tank(m, lod); break;
                case StreetPropKind.Bench:
                    Bench(m, lod);
                    rayDistance = 0.25f;
                    break;
                case StreetPropKind.WaterTap: Tap(m, lod); break;
                case StreetPropKind.Windsock: Windsock(m, lod); break;
                case StreetPropKind.Helipad: Helipad(m, lod); break;
                case StreetPropKind.ParkingPosition: StandMarking(m, lod); break;
                case StreetPropKind.Mast: Mast(m, lod); break;
                case StreetPropKind.Artwork: Statue(m, lod); break;
                case StreetPropKind.PowerTower: Pylon(m, lod); break;
                case StreetPropKind.PowerPole: UtilityPole(m, lod); break;
                case StreetPropKind.TransformerPole: Transformer(m, lod); break;
                case StreetPropKind.Well: Well(m, lod); break;
                case StreetPropKind.SolarPanel: Solar(m, lod); break;
                case StreetPropKind.Chimney: Chimney(m, lod); break;
                default: return 0;
            }
            if (m.VertexCount > v0)
            {
                AoSettings s = ShapeAo.Defaults(0);
                if (lod == 0)
                {
                    s.Rays = 6;
                    s.RayDistance = rayDistance;
                }
                ShapeAo.Bake(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0, s);
            }
            return m.TriangleCount - t0;
        }

        /// <summary>Where a traffic signal kind carries its main head, in the kind's frame at its default height.</summary>
        public struct SignalHead
        {
            /// <summary>Head centre ahead (+Z) of and right (+X) of the pole axis.</summary>
            public double ForwardM, RightM;

            /// <summary>Bottom and top of the head housing above the base.</summary>
            public double BottomM, TopM;

            /// <summary>Where the lenses face, clockwise from the kind's +Z (90 = +X).</summary>
            public double FacingDeg;
        }

        /// <summary>True for the signal kinds (a mast arm with a head hanging over the lanes).</summary>
        public static bool IsSignal(StreetPropKind k)
        {
            return k == StreetPropKind.TrafficSignal || k == StreetPropKind.TrafficSignalLong || k == StreetPropKind.TrafficSignalMirrored ||
                   k == StreetPropKind.TrafficSignalLongMirrored;
        }

        /// <summary>True for the mirrored signal kinds (head facing -X).</summary>
        public static bool IsMirroredSignal(StreetPropKind k)
        {
            return k == StreetPropKind.TrafficSignalMirrored || k == StreetPropKind.TrafficSignalLongMirrored;
        }

        /// <summary>Arm reach (pole axis to arm tip, metres at the default height) of a signal kind; 0 for other kinds.</summary>
        public static double SignalReach(StreetPropKind k)
        {
            switch (k)
            {
                case StreetPropKind.TrafficSignal:
                case StreetPropKind.TrafficSignalMirrored: return SignalReachM;
                case StreetPropKind.TrafficSignalLong:
                case StreetPropKind.TrafficSignalLongMirrored: return SignalLongReachM;
                default: return 0;
            }
        }

        /// <summary>The main head of a signal kind at its default height: its centre <see cref="SignalHead.ForwardM"/>
        /// along the arm (+Z), the bottom of the arrow unit under it and the top of the housing, lenses facing +X (the
        /// traffic it serves comes from +X: left-hand traffic with the pole on its left kerb), or -X for the mirrored
        /// kinds (the pole on the traffic's right, on a median or the far kerb). False for other kinds.</summary>
        public static bool TryHangingHead(StreetPropKind k, out SignalHead head)
        {
            head = default(SignalHead);
            if (!IsSignal(k)) return false;
            head = new SignalHead
            {
                ForwardM = SignalReach(k) - SignalHeadInset, RightM = 0, BottomM = SignalArrowBottom, TopM = SignalHeadTop, FacingDeg = IsMirroredSignal(k) ? 270 : 90,
            };
            return true;
        }

        /// <summary>Mirrors the vertices from <paramref name="v0"/> and triangles from <paramref name="i0"/> across the
        /// YZ plane (x → -x), keeping the winding outward.</summary>
        private static void MirrorX(MeshData m, int v0, int i0)
        {
            for (int v = v0; v < m.VertexCount; v++)
            {
                m.Positions[3 * v] = -m.Positions[3 * v];
                m.Normals[3 * v] = -m.Normals[3 * v];
            }
            for (int i = i0; i + 2 < m.IndexCount; i += 3)
            {
                int t = m.Indices[i + 1];
                m.Indices[i + 1] = m.Indices[i + 2];
                m.Indices[i + 2] = t;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers

        /// <summary>A fixed (untinted: vertex alpha 0) brush from a 0xRRGGBB colour.</summary>
        internal static ShapeBrush B(uint rgb, MaterialChannel ch)
        {
            return new ShapeBrush(rgb << 8, ch, 1f);
        }

        /// <summary>A brush the instance tint applies to (vertex alpha 255).</summary>
        internal static ShapeBrush Tinted(uint rgb, MaterialChannel ch)
        {
            return new ShapeBrush(rgb << 8 | 0xFF, ch, 1f);
        }

        /// <summary>Per-level count: <paramref name="a"/> at LOD0, <paramref name="b"/> at LOD1, <paramref name="c"/> beyond.</summary>
        internal static int P(int lod, int a, int b, int c)
        {
            return lod <= 0 ? a : lod == 1 ? b : c;
        }

        private static readonly ShapeLod L0 = ShapeLod.Lod0;

        /// <summary>A box centred on the origin of <paramref name="xf"/> with flat faces (12 triangles).</summary>
        internal static void Box(MeshData m, in Affine3 xf, in ShapeBrush b, double sx, double sy, double sz)
        {
            double hx = 0.5 * sx, hy = 0.5 * sy, hz = 0.5 * sz;
            for (int f = 0; f < 6; f++)
            {
                int axis = f >> 1;
                double s = (f & 1) == 0 ? -1 : 1;
                double nx = axis == 0 ? s : 0, ny = axis == 1 ? s : 0, nz = axis == 2 ? s : 0;
                int v = m.VertexCount;
                for (int c = 0; c < 4; c++)
                {
                    double u = (c == 1 || c == 2) ? 1 : -1, w = c >= 2 ? 1 : -1;
                    double x, y, z;
                    if (axis == 0)
                    {
                        x = s * hx;
                        y = u * hy;
                        z = w * hz;
                    }
                    else if (axis == 1)
                    {
                        x = u * hx;
                        y = s * hy;
                        z = w * hz;
                    }
                    else
                    {
                        x = u * hx;
                        y = w * hy;
                        z = s * hz;
                    }
                    ShapeEmit.Vertex(m, xf, b, x, y, z, nx, ny, nz);
                }
                ShapeEmit.TriOriented(m, v, v + 1, v + 2);
                ShapeEmit.TriOriented(m, v, v + 2, v + 3);
            }
        }

        /// <summary>A box spanning [x0, x1] × [y0, y1] × [z0, z1] in the frame of <paramref name="xf"/>.</summary>
        internal static void BoxSpan(MeshData m, in Affine3 xf, in ShapeBrush b, double x0, double x1, double y0, double y1, double z0, double z1)
        {
            Box(m, xf * Affine3.Translation(0.5 * (x0 + x1), 0.5 * (y0 + y1), 0.5 * (z0 + z1)), b, Math.Abs(x1 - x0), Math.Abs(y1 - y0), Math.Abs(z1 - z0));
        }

        /// <summary>
        /// A tapered rectangular prism standing on y = 0 (bottom w0 × d0, top w1 × d1, height h) whose vertical corners
        /// are chamfered by <paramref name="chamfer"/> (fraction of the half width, 0 = a plain 4-sided prism); flat
        /// faces, no caps. Concrete poles, carved posts, pillars.
        /// </summary>
        internal static void Prism(MeshData m, in Affine3 xf, in ShapeBrush b, double w0, double d0, double w1, double d1, double h, double chamfer,
                                   bool capTop = false, bool capBottom = false)
        {
            int n = chamfer > 0 ? 8 : 4;
            double[] px = ShapeEmit.Scratch(0, 16), pz = ShapeEmit.Scratch(1, 16);
            // Unit outline (corners at ±1), counter-clockwise from the +X side.
            if (n == 4)
            {
                px[0] = 1; pz[0] = -1; px[1] = 1; pz[1] = 1; px[2] = -1; pz[2] = 1; px[3] = -1; pz[3] = -1;
            }
            else
            {
                double c = 1 - chamfer;
                px[0] = 1; pz[0] = -c; px[1] = 1; pz[1] = c; px[2] = c; pz[2] = 1; px[3] = -c; pz[3] = 1;
                px[4] = -1; pz[4] = c; px[5] = -1; pz[5] = -c; px[6] = -c; pz[6] = -1; px[7] = c; pz[7] = -1;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double ax0 = px[i] * 0.5 * w0, az0 = pz[i] * 0.5 * d0, bx0 = px[j] * 0.5 * w0, bz0 = pz[j] * 0.5 * d0;
                double ax1 = px[i] * 0.5 * w1, az1 = pz[i] * 0.5 * d1, bx1 = px[j] * 0.5 * w1, bz1 = pz[j] * 0.5 * d1;
                // Face normal: horizontal outward normal of the edge, tilted by the taper.
                double ex = bx0 - ax0, ez = bz0 - az0, el = Math.Sqrt(ex * ex + ez * ez);
                if (el < 1e-12) continue;
                double nx = ez / el, nz = -ex / el;
                double mx0 = 0.5 * (ax0 + bx0), mz0 = 0.5 * (az0 + bz0), mx1 = 0.5 * (ax1 + bx1), mz1 = 0.5 * (az1 + bz1);
                double inward = (mx0 - mx1) * nx + (mz0 - mz1) * nz; // how far the face leans in over h
                double ny = h > 1e-9 ? inward / h : 0;
                double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= l;
                ny /= l;
                nz /= l;
                int v = m.VertexCount;
                ShapeEmit.Vertex(m, xf, b, ax0, 0, az0, nx, ny, nz);
                ShapeEmit.Vertex(m, xf, b, bx0, 0, bz0, nx, ny, nz);
                ShapeEmit.Vertex(m, xf, b, bx1, h, bz1, nx, ny, nz);
                ShapeEmit.Vertex(m, xf, b, ax1, h, az1, nx, ny, nz);
                ShapeEmit.TriOriented(m, v, v + 1, v + 2);
                ShapeEmit.TriOriented(m, v, v + 2, v + 3);
            }
            if (capTop)
            {
                int v = m.VertexCount;
                for (int i = 0; i < n; i++) ShapeEmit.Vertex(m, xf, b, px[i] * 0.5 * w1, h, pz[i] * 0.5 * d1, 0, 1, 0);
                for (int i = 1; i + 1 < n; i++) ShapeEmit.TriOriented(m, v, v + i, v + i + 1);
            }
            if (capBottom)
            {
                int v = m.VertexCount;
                for (int i = 0; i < n; i++) ShapeEmit.Vertex(m, xf, b, px[i] * 0.5 * w0, 0, pz[i] * 0.5 * d0, 0, -1, 0);
                for (int i = 1; i + 1 < n; i++) ShapeEmit.TriOriented(m, v, v + i, v + i + 1);
            }
        }

        /// <summary>A flat quad (a, b, c, d around its perimeter) facing <paramref name="n"/> side.</summary>
        internal static void Quad(MeshData m, in Affine3 xf, in ShapeBrush b, double ax, double ay, double az, double bx, double by, double bz,
                                  double cx, double cy, double cz, double dx, double dy, double dz, double nx, double ny, double nz)
        {
            int v = m.VertexCount;
            ShapeEmit.Vertex(m, xf, b, ax, ay, az, nx, ny, nz);
            ShapeEmit.Vertex(m, xf, b, bx, by, bz, nx, ny, nz);
            ShapeEmit.Vertex(m, xf, b, cx, cy, cz, nx, ny, nz);
            ShapeEmit.Vertex(m, xf, b, dx, dy, dz, nx, ny, nz);
            ShapeEmit.TriOriented(m, v, v + 1, v + 2);
            ShapeEmit.TriOriented(m, v, v + 2, v + 3);
        }

        /// <summary>A round tube through a path built by the caller (radius tapering to <paramref name="r1"/>).</summary>
        internal static void TubeAlong(MeshData m, in Affine3 xf, in ShapeBrush b, Path3 path, double r0, double r1, int radial, bool caps = true)
        {
            Shapes.Tube(m, xf, b, path, r0, radial, caps, false, L0, r1);
        }

        /// <summary>Recolours the vertices from <paramref name="first"/> whose normal (in mesh space) points down more
        /// than <paramref name="dot"/>: the glowing lens under a lamp head.</summary>
        internal static void Underside(MeshData m, int first, uint rgb, MaterialChannel ch, float dot = -0.55f)
        {
            uint c = rgb << 8;
            for (int v = first; v < m.VertexCount; v++)
            {
                if (m.Normals[3 * v + 1] > dot) continue;
                m.Colors[4 * v] = (byte)(c >> 24);
                m.Colors[4 * v + 1] = (byte)(c >> 16);
                m.Colors[4 * v + 2] = (byte)(c >> 8);
                m.Colors[4 * v + 3] = (byte)c;
                m.Uv0[2 * v] = (float)ch;
            }
        }

        [ThreadStatic] private static Path3 s_path, s_ctrl;
        [ThreadStatic] private static Profile2 s_prof;

        internal static Path3 PathA
        {
            get { return s_path ?? (s_path = new Path3(64)); }
        }

        internal static Path3 PathB
        {
            get { return s_ctrl ?? (s_ctrl = new Path3(64)); }
        }

        internal static Profile2 Prof
        {
            get { return s_prof ?? (s_prof = new Profile2(64)); }
        }

        private const double Deg = Math.PI / 180.0;
    }
}
