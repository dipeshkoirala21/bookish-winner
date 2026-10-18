using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>A barrier the carve builds, for the solids (game metres): a parapet or guard rail along a drop beside a
    /// road, or a breast wall holding a cut (<see cref="TerrainCarve.CopyBarriers"/>).</summary>
    public struct CarveBarrier
    {
        public double AX, AZ, BX, BZ;
        public float Bottom, Top;

        /// <summary>A breast wall holding a cut (else a parapet or guard rail along a drop).</summary>
        public bool Wall;
    }

    public sealed partial class TerrainCarve
    {
        /// <summary>Depth of a retaining wall block beyond the corridor (one terrain sub-quad, so the steep carved face
        /// inside it is hidden).</summary>
        public double WallDepthM
        {
            get { return SubM; }
        }

        /// <summary>Course height of a gabion wall (one wire basket).</summary>
        public const float GabionCourseM = 1.0f;

        /// <summary>Basket length along the wall.</summary>
        public const float GabionLengthM = 2.0f;

        /// <summary>How far each course steps back (the wall's batter).</summary>
        public const float GabionStepM = 0.15f;

        /// <summary>Spacing of the parapet posts along a big drop (the Low tier's coarse grids: twice that).</summary>
        public const float PostSpacingM = 3f;

        /// <summary>Drop of the carved ground <see cref="ParapetReachM"/> beyond the road's drawn extent above which its edge
        /// gets parapet posts (a real drop: the hillside falls more than 50 % below the embankment's crest).</summary>
        public const float ParapetDropM = 2f;

        /// <summary>How far beyond the drawn extent the drop is measured.</summary>
        public const float ParapetReachM = 4f;

        private const uint GabionStone = 0x8F8B82FFu, MasonryStone = 0x9C978EFFu, Coping = 0xB3AEA4FFu;
        private const uint PostWhite = 0xEDEBE3FFu, PostBlack = 0x2B2B2BFFu, RailSteel = 0xB2B6B8FFu;

        /// <summary>
        /// The retaining walls and parapets of the carve, appended to <paramref name="m"/> (area-local x/z, absolute y, the
        /// terrain mesh's frame): wherever the carve built a wall (<see cref="WallMinM"/> and up), a gabion wall of 1 m wire
        /// baskets stepping back course by course (fills, and cuts beside main roads) or a battered dry stone wall with a
        /// coping (cuts beside minor roads), as a block one terrain sub-quad deep that hides the carved face inside it and
        /// meets the ground at its top or toe; and along big drops (a fill wall, or more than <see cref="ParapetDropM"/>
        /// within 2.5 m of the corridor) white parapet posts with a black band, carrying a steel W-beam rail beside main
        /// roads. UV0 channels (Stone, Concrete, Paint, Metal) with baked AO. Returns the triangles added.
        /// </summary>
        public int AppendWalls(MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int before = m.IndexCount;
            for (int e = 0; e < _segCount; e++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float wa = side == 0 ? _wla[e] : _wra[e], wb = side == 0 ? _wlb[e] : _wrb[e];
                    if (Math.Max(Math.Abs(wa), Math.Abs(wb)) >= 0.5f) Wall(m, e, side, wa, wb);
                    Parapet(m, e, side, wa, wb);
                }
            }
            return (m.IndexCount - before) / 3;
        }

        private static bool Major(RoadClass c)
        {
            return c == RoadClass.Motorway || c == RoadClass.Trunk || c == RoadClass.Primary || c == RoadClass.Secondary || c == RoadClass.Tertiary;
        }

        /// <summary>Point of segment <paramref name="e"/> at fraction <paramref name="t"/>, offset <paramref name="off"/>
        /// toward <paramref name="side"/> (tile-local).</summary>
        private void At(int e, double t, int side, double off, out double x, out double z)
        {
            double sign = side == 0 ? 1 : -1;
            // Along the ribbon's section direction there, as the extent's offsets are measured.
            double wx = _wxa[e] + (_wxb[e] - _wxa[e]) * t, wz = _wza[e] + (_wzb[e] - _wza[e]) * t;
            x = _ax[e] + _dx[e] * _len[e] * t + wx * sign * off;
            z = _az[e] + _dz[e] * _len[e] * t + wz * sign * off;
        }

        private float Ext(int e, int side, float t)
        {
            return side == 0 ? _cla[e] + (_clb[e] - _cla[e]) * t : _cra[e] + (_crb[e] - _cra[e]) * t;
        }

        private float EdgeBench(int e, int side, float t)
        {
            float edge = side == 0 ? _ela[e] + (_elb[e] - _ela[e]) * t : _era[e] + (_erb[e] - _era[e]) * t;
            return _a0a[e] + (_a0b[e] - _a0a[e]) * t + edge * (_a1a[e] + (_a1b[e] - _a1a[e]) * t);
        }

        /// <summary>The drawn (carved) terrain at tile-local (x, z).</summary>
        private float Drawn(double lx, double lz)
        {
            double gx, gz;
            return (float)DrawnSurface((lx - _offX) / Grid.CellM, (lz - _offZ) / Grid.CellM, out gx, out gz);
        }

        /// <summary>How deep a wall block may reach outward from tile-local (x0, z0) along (ux, uz) before it would stand on
        /// another road's drawn extent (a lane or path running along the top of the cut, a street below the fill): the
        /// largest of <paramref name="depth"/> and the clear distance less a margin; 0 when there is no room for a wall.</summary>
        private double ClearDepth(double x0, double z0, double ux, double uz, double depth)
        {
            const double Step = 0.25, Margin = 0.15;
            for (double d = 0.0; d <= depth + 1e-6; d += Step)
            {
                float b;
                double gx, gz;
                if (!ExtentAt(x0 + ux * d, z0 + uz * d, out b, out gx, out gz)) continue;
                double clear = d - Margin;
                return clear < 0.3 ? 0.0 : clear;
            }
            return depth;
        }

        /// <summary>Longest outward step a wall takes along the ribbon's section direction (a mitred bend; an oblique junction
        /// or border section is held to this, so a wall bay never shears far off its road).</summary>
        private const double MaxWallMitre = 1.6;

        /// <summary>The outward direction of a wall at fraction <paramref name="t"/> of segment <paramref name="e"/> on
        /// <paramref name="side"/>: the ribbon's section direction there (mitred at bends, shared by both segments at a
        /// row, so neighbouring bays meet without a gap), its length held to <see cref="MaxWallMitre"/>.</summary>
        private void Outward(int e, double t, int side, out double ox, out double oz)
        {
            double sign = side == 0 ? 1 : -1;
            double wx = _wxa[e] + (_wxb[e] - _wxa[e]) * t, wz = _wza[e] + (_wzb[e] - _wza[e]) * t;
            double l = Math.Sqrt(wx * wx + wz * wz);
            if (l < 1e-6)
            {
                wx = -_dz[e];
                wz = _dx[e];
                l = 1.0;
            }
            double k = Math.Min(l, MaxWallMitre) / l;
            ox = wx * k * sign;
            oz = wz * k * sign;
        }

        private void Wall(MeshData m, int e, int side, float wa, float wb)
        {
            double len = _len[e];
            int n = Math.Max(1, (int)Math.Ceiling(len / GabionLengthM));
            double fullDepth = WallDepthM;
            RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
            bool cutSide = wa + wb > 0f;
            bool gabion = !cutSide || Major(cls);
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n, tm = 0.5f * (t0 + t1);
                float w = Math.Abs(wa + (wb - wa) * tm);
                if (w < 0.5f) continue;
                // A breast wall stands where the cut slope starts (beyond the verge), a fill wall at the drawn edge.
                float e0 = Ext(e, side, t0) + (cutSide ? WallVergeM : 0f), e1 = Ext(e, side, t1) + (cutSide ? WallVergeM : 0f);
                float b0 = EdgeBench(e, side, t0), b1 = EdgeBench(e, side, t1);
                double fx0, fz0, fx1, fz1, ox0, oz0, ox1, oz1;
                At(e, t0, side, e0, out fx0, out fz0);
                At(e, t1, side, e1, out fx1, out fz1);
                Outward(e, t0, side, out ox0, out oz0);
                Outward(e, t1, side, out ox1, out oz1);
                double oxm = 0.5 * (ox0 + ox1), ozm = 0.5 * (oz0 + oz1);
                // Never over another road: the block stops short of any drawn extent behind it (a breast wall) or between
                // it and the valley (a fill wall's body); no room at all, no wall (the terrain carve meets that road).
                double depth = Math.Min(ClearDepth(fx0 + ox0 * 0.05, fz0 + oz0 * 0.05, ox0, oz0, fullDepth),
                                        ClearDepth(fx1 + ox1 * 0.05, fz1 + oz1 * 0.05, ox1, oz1, fullDepth));
                double fxm = 0.5 * (fx0 + fx1), fzm = 0.5 * (fz0 + fz1);
                depth = Math.Min(depth, ClearDepth(fxm + oxm * 0.05, fzm + ozm * 0.05, oxm, ozm, fullDepth));
                if (depth <= 0.0) continue;
                uint jitter = WallHash(e, side, i);
                if (!_refine)
                {
                    // The Low tier's coarse grids: one battered stone face per bay.
                    float lb0 = cutSide ? b0 - 0.15f : Math.Min(b0 - 0.6f, b0 - w), lb1 = cutSide ? b1 - 0.15f : Math.Min(b1 - 0.6f, b1 - w);
                    float lt0 = cutSide ? b0 + w : b0 + 0.06f, lt1 = cutSide ? b1 + w : b1 + 0.06f;
                    double s = cutSide ? 0 : depth, bt = cutSide ? 0.1 : -0.1;
                    double ax0 = fx0 + ox0 * s, az0 = fz0 + oz0 * s, ax1 = fx1 + ox1 * s, az1 = fz1 + oz1 * s;
                    Quad(m, ax0, lb0, az0, ax1, lb1, az1, ax1 + ox1 * bt * w, lt1, az1 + oz1 * bt * w, ax0 + ox0 * bt * w, lt0, az0 + oz0 * bt * w,
                         (float)(cutSide ? -oxm : oxm), 0.1f, (float)(cutSide ? -ozm : ozm), MasonryStone, MaterialChannel.Flagstone, 0.85f);
                    continue;
                }
                if (cutSide)
                {
                    // A breast wall: from the verge up to the cut ground behind it, the courses stepping back into the hill.
                    float base0 = b0 - 0.15f, base1 = b1 - 0.15f;
                    float top0 = Math.Max(base0 + 0.6f, Drawn(fx0 + ox0 * depth, fz0 + oz0 * depth));
                    float top1 = Math.Max(base1 + 0.6f, Drawn(fx1 + ox1 * depth, fz1 + oz1 * depth));
                    WallFace(m, fx0, fz0, fx1, fz1, ox0, oz0, ox1, oz1, base0, base1, top0, top1, depth, gabion, jitter, CutFill.FillEarth);
                }
                else
                {
                    // A wall holding the fill: from the toe up to the verge, the courses stepping back toward the road.
                    float top0 = b0 + 0.06f, top1 = b1 + 0.06f;
                    double d = depth + 0.3;
                    float base0 = Math.Min(top0 - 0.6f, Drawn(fx0 + ox0 * d, fz0 + oz0 * d) - 0.3f);
                    float base1 = Math.Min(top1 - 0.6f, Drawn(fx1 + ox1 * d, fz1 + oz1 * d) - 0.3f);
                    WallFace(m, fx0 + ox0 * depth, fz0 + oz0 * depth, fx1 + ox1 * depth, fz1 + oz1 * depth, -ox0, -oz0, -ox1, -oz1, base0, base1, top0, top1,
                             depth, gabion, jitter, CutFill.Verge);
                }
            }
        }

        private static uint WallHash(int e, int side, int i)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)e) * 16777619u;
                h = (h ^ (uint)side) * 16777619u;
                h = (h ^ (uint)i) * 16777619u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>How far a gabion basket's face bulges out at its middle (the stones press the wire mesh out).</summary>
        private const float BasketBulgeM = 0.06f;

        /// <summary>Depth of a gabion basket (its top shows as stone this far back from the face; the earth it holds lies
        /// behind).</summary>
        private const double BasketDepthM = 1.0;

        /// <summary>Depth of a dry stone wall's concrete coping.</summary>
        private const double CopingDepthM = 0.45;

        /// <summary>
        /// One 2 m bay of wall between front points (x0, z0) and (x1, z1) on its foot line; (b0x, b0z) and (b1x, b1z) point
        /// from the face into the wall's body at either end (the ribbon's mitred section directions, so neighbouring bays
        /// share their ends). A cut wall's face looks at the road (its courses step back into the hill as they rise); a fill
        /// wall's face looks at the valley (its courses step back toward the road).
        /// Gabion: 1 m courses counted up from the foot (so they run level across bays), each course a wire basket per bay
        /// bulging out between its wired seams, set back <see cref="GabionStepM"/> with a stone ledge on it. Dry stone: one battered
        /// rubble face and a concrete coping. Behind the top course (or the coping) the wall's body is drawn as the earth it
        /// holds back, tinted <paramref name="behind"/>.
        /// </summary>
        private static void WallFace(MeshData m, double x0, double z0, double x1, double z1, double b0x, double b0z, double b1x, double b1z, float base0,
                                     float base1, float top0, float top1, double depth, bool gabion, uint jitter, uint behind)
        {
            // Face normal: away from the body, square to the bay's chord.
            double cx = x1 - x0, cz = z1 - z0, cl = Math.Sqrt(cx * cx + cz * cz);
            if (cl < 1e-6) return;
            cx /= cl;
            cz /= cl;
            double nxd = -cz, nzd = cx;
            if (nxd * (b0x + b1x) + nzd * (b0z + b1z) > 0)
            {
                nxd = -nxd;
                nzd = -nzd;
            }
            float nx = (float)nxd, nz = (float)nzd;
            if (!gabion)
            {
                // Dry stone rubble, 10 % batter, and a coping slab on top.
                float h0 = top0 - base0, h1 = top1 - base1;
                double ax0 = x0 + b0x * 0.1 * h0, az0 = z0 + b0z * 0.1 * h0, ax1 = x1 + b1x * 0.1 * h1, az1 = z1 + b1z * 0.1 * h1;
                uint stone = MeshColor.Scale(MasonryStone, 0.92f + 0.16f * ((jitter & 255) / 255f));
                Quad(m, x0, base0, z0, x1, base1, z1, ax1, top1, az1, ax0, top0, az0, nx, 0.1f, nz, stone, MaterialChannel.Flagstone, 0.85f);
                // The coping: a 0.45 m concrete slab overhanging the face by 8 cm; behind it the earth the wall holds.
                double cd = Math.Min(depth, CopingDepthM);
                double kx0 = ax0 + b0x * cd, kz0 = az0 + b0z * cd, kx1 = ax1 + b1x * cd, kz1 = az1 + b1z * cd;
                Quad(m, ax0 - b0x * 0.08, top0 + 0.12f, az0 - b0z * 0.08, ax1 - b1x * 0.08, top1 + 0.12f, az1 - b1z * 0.08, kx1, top1 + 0.12f, kz1, kx0,
                     top0 + 0.12f, kz0, 0f, 1f, 0f, Coping, MaterialChannel.Concrete, 1f);
                Quad(m, ax0 - b0x * 0.08, top0 - 0.02f, az0 - b0z * 0.08, ax1 - b1x * 0.08, top1 - 0.02f, az1 - b1z * 0.08, ax1 - b1x * 0.08, top1 + 0.12f,
                     az1 - b1z * 0.08, ax0 - b0x * 0.08, top0 + 0.12f, az0 - b0z * 0.08, nx, 0f, nz, Coping, MaterialChannel.Concrete, 0.9f);
                if (depth > cd + 0.01)
                    Quad(m, kx0, top0 + 0.04f, kz0, kx1, top1 + 0.04f, kz1, ax1 + b1x * depth, top1 + 0.04f, az1 + b1z * depth, ax0 + b0x * depth, top0 + 0.04f,
                         az0 + b0z * depth, 0f, 1f, 0f, behind, MaterialChannel.Dirt, 0.9f);
                return;
            }
            float hMax = Math.Max(top0 - base0, top1 - base1);
            int courses = Math.Max(1, Math.Min(6, (int)Math.Ceiling(hMax / GabionCourseM - 0.1f)));
            for (int c = 0; c < courses; c++)
            {
                // Course c: from foot + c m to foot + (c + 1) m at each end, stopping at the top (gone where the wall is lower).
                float yb0 = base0 + c * GabionCourseM, yb1 = base1 + c * GabionCourseM;
                float yt0 = Math.Max(yb0, Math.Min(top0, yb0 + GabionCourseM)), yt1 = Math.Max(yb1, Math.Min(top1, yb1 + GabionCourseM));
                if (yt0 - yb0 < 0.05f && yt1 - yb1 < 0.05f) break;
                double set = GabionStepM * c;
                bool next0 = top0 > yb0 + GabionCourseM + 0.05f, next1 = top1 > yb1 + GabionCourseM + 0.05f;
                uint stone = MeshColor.Scale(GabionStone, 0.88f + 0.24f * (((jitter >> (c * 5 % 24)) & 255) / 255f));
                float ao = 0.78f + 0.22f * Math.Min(1f, (c + 1f) / courses);
                Basket(m, x0 + b0x * set, z0 + b0z * set, x1 + b1x * set, z1 + b1z * set, yb0, yb1, yt0, yt1, nxd, nzd, cx, cz, stone, ao);
                // The ledge on top of this course: to the next course's face, or the wall's top surface back to the body's end.
                double l0 = next0 ? GabionStepM : depth - set, l1 = next1 ? GabionStepM : depth - set;
                if (l0 > 0.01 || l1 > 0.01)
                {
                    l0 = Math.Max(0.0, l0);
                    l1 = Math.Max(0.0, l1);
                    double ax0 = x0 + b0x * set, az0 = z0 + b0z * set, ax1 = x1 + b1x * set, az1 = z1 + b1z * set;
                    // The top of the basket itself (stone, one basket deep), then the earth it holds back (the road's
                    // shoulder behind a fill wall, the hillside behind a breast wall).
                    double s0 = next0 ? l0 : Math.Min(l0, BasketDepthM), s1 = next1 ? l1 : Math.Min(l1, BasketDepthM);
                    Quad(m, ax0, yt0, az0, ax1, yt1, az1, ax1 + b1x * s1, yt1, az1 + b1z * s1, ax0 + b0x * s0, yt0, az0 + b0z * s0, 0f, 1f, 0f,
                         MeshColor.Scale(stone, 1.04f), MaterialChannel.Flagstone, ao);
                    if (l0 > s0 + 0.01 || l1 > s1 + 0.01)
                        Quad(m, ax0 + b0x * s0, yt0, az0 + b0z * s0, ax1 + b1x * s1, yt1, az1 + b1z * s1, ax1 + b1x * l1, yt1, az1 + b1z * l1, ax0 + b0x * l0, yt0,
                             az0 + b0z * l0, 0f, 1f, 0f, behind, MaterialChannel.Dirt, 0.9f);
                }
            }
        }

        /// <summary>
        /// A course of a gabion bay as one wire basket: a 2 x 3 vertex patch (its ends; its foot, middle and top) bulging
        /// <see cref="BasketBulgeM"/> out at mid-height and shaded as a cushion (normals tilted down at the foot, up at the
        /// top and out toward the wired seams at its ends, the seams and the foot in shadow), so every basket reads on
        /// its own at the price of four triangles.
        /// </summary>
        private static void Basket(MeshData m, double x0, double z0, double x1, double z1, float yb0, float yb1, float yt0, float yt1, double nx, double nz,
                                   double cx, double cz, uint stone, float ao)
        {
            int v = m.VertexCount;
            for (int r = 0; r < 3; r++)
            for (int j = 0; j < 2; j++)
            {
                float yb = j == 0 ? yb0 : yb1, yt = j == 0 ? yt0 : yt1;
                float y = r == 0 ? yb : r == 1 ? 0.5f * (yb + yt) : yt;
                double bulge = r == 1 ? BasketBulgeM * Math.Min(1.0, (yt - yb) / 0.5) : 0.0;
                double px = (j == 0 ? x0 : x1) + nx * bulge, pz = (j == 0 ? z0 : z1) + nz * bulge;
                double tilt = r == 0 ? -0.45 : r == 2 ? 0.55 : 0.0, side = j == 0 ? -0.35 : 0.35;
                double gx = nx + cx * side, gy = tilt, gz = nz + cz * side;
                double gl = Math.Sqrt(gx * gx + gy * gy + gz * gz);
                float a = ao * (r == 1 ? 0.95f : r == 0 ? 0.66f : 0.84f) * 0.85f;
                m.AddVertex((float)px, y, (float)pz, (float)(gx / gl), (float)(gy / gl), (float)(gz / gl), stone, (float)MaterialChannel.Flagstone, a);
            }
            for (int r = 0; r < 2; r++)
            {
                int a = v + r * 2, b = a + 1, c = a + 3, d = a + 2;
                // Front faces the face normal: (b - a) x (c - a) along it (Unity winding as Quad).
                float ex = m.Positions[3 * b] - m.Positions[3 * a], ey = m.Positions[3 * b + 1] - m.Positions[3 * a + 1], ez = m.Positions[3 * b + 2] - m.Positions[3 * a + 2];
                float fx = m.Positions[3 * c] - m.Positions[3 * a], fy = m.Positions[3 * c + 1] - m.Positions[3 * a + 1], fz = m.Positions[3 * c + 2] - m.Positions[3 * a + 2];
                double kx = ey * fz - ez * fy, kz = ex * fy - ey * fx;
                if (kx * nx + kz * nz >= 0)
                {
                    m.AddTriangle(a, b, c);
                    m.AddTriangle(a, c, d);
                }
                else
                {
                    m.AddTriangle(a, c, b);
                    m.AddTriangle(a, d, c);
                }
            }
        }

        private void Parapet(MeshData m, int e, int side, float wa, float wb)
        {
            double len = _len[e];
            RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
            if (!HasParapet(cls, wa, wb)) return;
            int n = Math.Max(1, (int)Math.Round(len / (_refine ? PostSpacingM : 2f * PostSpacingM)));
            double sign = side == 0 ? 1 : -1;
            double ux = -_dz[e] * sign, uz = _dx[e] * sign;
            bool rail = Major(cls) && _refine;
            float lift = RoadMesher.LiftOf(Source.Roads[_road[e]], new RoadOptions());
            double prevX = 0, prevZ = 0;
            float prevY = 0f;
            bool prev = false;
            for (int k = 0; k <= n; k++)
            {
                if (k == 0 && (_flags[e] & ClipStart) == 0 && e > 0) { } // posts at segment starts are shared with the previous end
                float t = (float)k / n;
                float ext = Ext(e, side, t) + PostOffsetM;
                float bench = EdgeBench(e, side, t);
                double px, pz;
                At(e, t, side, ext, out px, out pz);
                bool drop = DropAt(e, side, t, wa, wb) && !OnOtherRoad(px, pz);
                if (!drop)
                {
                    prev = false;
                    continue;
                }
                float gy = bench + lift; // on the road's outer edge (shoulder or footpath)
                if (k < n || (_flags[e] & ClipEnd) != 0) Post(m, px, gy, pz, ux, uz, !_refine);
                if (rail && prev) WBeam(m, prevX - ux * 0.09, prevY + 0.42f, prevZ - uz * 0.09, px - ux * 0.09, gy + 0.42f, pz - uz * 0.09, -ux, -uz);
                prev = true;
                prevX = px;
                prevZ = pz;
                prevY = gy;
            }
        }

        /// <summary>True when tile-local (x, z) (beyond a road's own extent) lies on another road's drawn extent.</summary>
        private bool OnOtherRoad(double x, double z)
        {
            float b;
            double gx, gz;
            return ExtentAt(x, z, out b, out gx, out gz);
        }

        /// <summary>Which roads get parapet posts along their drops: the main roads (trunk to tertiary) wherever the ground
        /// falls away, other carriageways only along a wall holding their fill (Kathmandu's lanes run beside open drops);
        /// never footways, paths or steps.</summary>
        private static bool HasParapet(RoadClass cls, float wa, float wb)
        {
            if (RoadWidthModel.IsFootClass(cls)) return false;
            return Major(cls) || Math.Min(wa, wb) < -0.5f;
        }

        /// <summary>True when the ground falls away beside segment <paramref name="e"/> at fraction <paramref name="t"/> on
        /// <paramref name="side"/>: a wall holds the fill there, or the carved ground <see cref="ParapetReachM"/> beyond the
        /// drawn extent lies more than <see cref="ParapetDropM"/> below the road's edge. Measured on the carve of the tile's full-resolution ground,
        /// so every tier and the solids agree.</summary>
        private bool DropAt(int e, int side, float t, float wa, float wb)
        {
            float w = wa + (wb - wa) * t;
            if (w < -0.5f) return true;
            double ox, oz;
            At(e, t, side, Ext(e, side, t) + ParapetReachM, out ox, out oz);
            return EdgeBench(e, side, t) - CarvedAt(ox, oz, SourceNatural(ox, oz)) > ParapetDropM;
        }

        /// <summary>
        /// The barriers of the carve for the solids (game metres; appended to <paramref name="into"/>, returns how many):
        /// the parapets and guard rails along every drop (<see cref="AppendWalls"/> draws them; a body walking or riding off
        /// the road's edge there is stopped, from below the bench to 0.9 m above the road) and the breast walls holding the
        /// cuts (their face, from the verge up to the cut ground behind). Built from the bench and the full-resolution
        /// ground, so the solids of the 8 m sampler match what every tier draws.
        /// </summary>
        public int CopyBarriers(List<CarveBarrier> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int before = into.Count;
            double x0 = Source.Tile.X0, z0 = Source.Tile.Z0;
            for (int e = 0; e < _segCount; e++)
            {
                RoadClass cls = _road[e] >= 0 && _road[e] < Source.Roads.Count ? Source.Roads[_road[e]].RoadClass : RoadClass.Residential;
                float lift = _road[e] >= 0 && _road[e] < Source.Roads.Count ? RoadMesher.LiftOf(Source.Roads[_road[e]], new RoadOptions()) : 0.25f;
                for (int side = 0; side < 2; side++)
                {
                    float wa = side == 0 ? _wla[e] : _wra[e], wb = side == 0 ? _wlb[e] : _wrb[e];
                    // Parapets: the drop stretches between posts (as Parapet draws them).
                    if (HasParapet(cls, wa, wb))
                    {
                        int n = Math.Max(1, (int)Math.Round(_len[e] / (_refine ? PostSpacingM : 2f * PostSpacingM)));
                        bool prev = false;
                        double pxPrev = 0, pzPrev = 0;
                        float bPrev = 0f;
                        for (int k = 0; k <= n; k++)
                        {
                            float t = (float)k / n;
                            double px, pz;
                            At(e, t, side, Ext(e, side, t) + PostOffsetM, out px, out pz);
                            if (!DropAt(e, side, t, wa, wb) || OnOtherRoad(px, pz))
                            {
                                prev = false;
                                continue;
                            }
                            float bench = EdgeBench(e, side, t);
                            if (prev)
                                into.Add(new CarveBarrier
                                {
                                    AX = x0 + pxPrev, AZ = z0 + pzPrev, BX = x0 + px, BZ = z0 + pz, Bottom = Math.Min(bPrev, bench) - 0.3f,
                                    Top = Math.Max(bPrev, bench) + lift + 0.9f,
                                });
                            prev = true;
                            pxPrev = px;
                            pzPrev = pz;
                            bPrev = bench;
                        }
                    }
                    // Breast walls: the face line of every bay with a cut wall.
                    if (wa + wb <= 0f || Math.Max(wa, wb) < 0.5f) continue;
                    int bays = Math.Max(1, (int)Math.Ceiling(_len[e] / GabionLengthM));
                    for (int i = 0; i < bays; i++)
                    {
                        float t0 = (float)i / bays, t1 = (float)(i + 1) / bays, tm = 0.5f * (t0 + t1);
                        float w = wa + (wb - wa) * tm;
                        if (w < 0.5f) continue;
                        double ax, az, bx, bz;
                        At(e, t0, side, Ext(e, side, t0) + WallVergeM, out ax, out az);
                        At(e, t1, side, Ext(e, side, t1) + WallVergeM, out bx, out bz);
                        // As Wall draws it: no bay where another road leaves no room behind the face.
                        double oax, oaz, obx, obz;
                        Outward(e, t0, side, out oax, out oaz);
                        Outward(e, t1, side, out obx, out obz);
                        double omx = 0.5 * (oax + obx), omz = 0.5 * (oaz + obz);
                        if (ClearDepth(ax + oax * 0.05, az + oaz * 0.05, oax, oaz, WallDepthM) <= 0.0 ||
                            ClearDepth(bx + obx * 0.05, bz + obz * 0.05, obx, obz, WallDepthM) <= 0.0 ||
                            ClearDepth(0.5 * (ax + bx) + omx * 0.05, 0.5 * (az + bz) + omz * 0.05, omx, omz, WallDepthM) <= 0.0) continue;
                        float b0 = EdgeBench(e, side, t0), b1 = EdgeBench(e, side, t1);
                        into.Add(new CarveBarrier
                        {
                            AX = x0 + ax, AZ = z0 + az, BX = x0 + bx, BZ = z0 + bz, Bottom = Math.Min(b0, b1) - 0.3f, Top = Math.Max(b0, b1) + w, Wall = true,
                        });
                    }
                }
            }
            return into.Count - before;
        }

        /// <summary>A parapet post: white concrete, 0.18 m square, standing 0.78 m above the road surface
        /// <paramref name="y"/> with a black band at the top, its foot sunk 0.5 m into the embankment's crest.</summary>
        private static void Post(MeshData m, double x, float y, double z, double ux, double uz, bool lite)
        {
            const double H = 0.09;
            double vx = -uz, vz = ux; // along the road
            if (lite)
            {
                Box(m, x, y - 0.5f, z, ux, uz, vx, vz, H, 1.28f, PostWhite, MaterialChannel.Paint, 0.9f);
                return;
            }
            // Cast concrete posts have chamfered arrises (and read rounded at a glance): an eight-sided prism, the black
            // band on top.
            Prism(m, x, y - 0.5f, z, ux, uz, H, PostChamferM, 1.08f, PostWhite, 0.9f, false);
            Prism(m, x, y + 0.58f, z, ux, uz, H, PostChamferM, 0.2f, PostBlack, 1f, true);
        }

        /// <summary>The chamfer on a parapet post's vertical edges.</summary>
        private const double PostChamferM = 0.03;

        // The eight corners of a chamfered square (see Prism): unit square corner and chamfer direction.
        private static readonly double[] OctU = { 1, 1, 1, -1, -1, -1, -1, 1 }, OctV = { -1, 1, 1, 1, 1, -1, -1, -1 };
        private static readonly double[] OctCu = { 0, 0, 1, -1, 0, 0, -1, 1 }, OctCv = { -1, 1, 0, 0, 1, -1, 0, 0 };

        // The W-beam's profile from its bottom edge up: height, and how far it stands out toward the road.
        private static readonly float[] BeamY = { 0f, 0.06f, 0.10f, 0.155f, 0.21f, 0.25f, 0.31f };
        private static readonly float[] BeamD = { 0.015f, 0.083f, 0.083f, 0.03f, 0.083f, 0.083f, 0.015f };

        /// <summary>An upright eight-sided prism (a square of half side <paramref name="h"/> with its corners chamfered by
        /// <paramref name="c"/>, faces toward (ux, uz) and along the road) standing at (x, y, z), <paramref name="height"/>
        /// tall, with a top cap when <paramref name="cap"/>. Paint channel.</summary>
        private static void Prism(MeshData m, double x, float y, double z, double ux, double uz, double h, double c, float height, uint rgba, float ao, bool cap)
        {
            double vx = -uz, vz = ux;
            float y1 = y + height;
            // Corners counter-clockwise seen from above, starting on the outward face: corner k is (u, v) = (OctU[k] h -
            // OctCu[k] c, OctV[k] h - OctCv[k] c).
            for (int k = 0; k < 8; k++)
            {
                int k1 = (k + 1) & 7;
                double au = OctU[k] * h - OctCu[k] * c, av = OctV[k] * h - OctCv[k] * c, bu = OctU[k1] * h - OctCu[k1] * c, bv = OctV[k1] * h - OctCv[k1] * c;
                double ax = x + ux * au + vx * av, az = z + uz * au + vz * av;
                double bx = x + ux * bu + vx * bv, bz = z + uz * bu + vz * bv;
                double mu = 0.5 * (au + bu), mv = 0.5 * (av + bv);
                double nx = ux * mu + vx * mv, nz = uz * mu + vz * mv;
                float a = k == 2 || k == 6 || (k & 1) == 1 ? ao * 0.95f : ao;
                Quad(m, ax, y, az, bx, y, bz, bx, y1, bz, ax, y1, az, (float)nx, 0f, (float)nz, rgba, MaterialChannel.Paint, a);
            }
            if (!cap) return;
            int v0 = m.VertexCount;
            for (int k = 0; k < 8; k++)
            {
                double ku = OctU[k] * h - OctCu[k] * c, kv = OctV[k] * h - OctCv[k] * c;
                m.AddVertex((float)(x + ux * ku + vx * kv), y1, (float)(z + uz * ku + vz * kv), 0f, 1f, 0f, rgba, (float)MaterialChannel.Paint, ao);
            }
            // A fan, wound to face up (Unity: front = cross(b - a, c - a)).
            for (int k = 1; k < 7; k++)
            {
                float ex = m.Positions[3 * (v0 + k)] - m.Positions[3 * v0], ez = m.Positions[3 * (v0 + k) + 2] - m.Positions[3 * v0 + 2];
                float fx = m.Positions[3 * (v0 + k + 1)] - m.Positions[3 * v0], fz = m.Positions[3 * (v0 + k + 1) + 2] - m.Positions[3 * v0 + 2];
                if (ez * fx - ex * fz >= 0f) m.AddTriangle(v0, v0 + k, v0 + k + 1);
                else m.AddTriangle(v0, v0 + k + 1, v0 + k);
            }
        }

        /// <summary>
        /// A steel W-beam guard rail from (x0, y0, z0) to (x1, y1, z1) (its lower edge, on the road side of the posts;
        /// (fx, fz) points from the posts toward the road): the 31 cm two-ridged corrugated profile of the beams on Nepal's
        /// strategic roads (World Bank road safety programme, docs/research/w2/ref_cutfill.md), six facets on the road side and
        /// a flat back.
        /// </summary>
        private static void WBeam(MeshData m, double x0, float y0, double z0, double x1, float y1, double z1, double fx, double fz)
        {
            float[] py = BeamY, pd = BeamD;
            for (int k = 0; k + 1 < py.Length; k++)
            {
                float ya = py[k], yb = py[k + 1];
                double da = pd[k], db = pd[k + 1];
                // The facet's normal in the section: (outward run, rise) turned toward the road.
                double run = db - da, rise = yb - ya, l = Math.Sqrt(run * run + rise * rise);
                double nh = rise / l, nv = -run / l;
                float shade = nv > 0.3 ? 1.05f : nv < -0.3 ? 0.78f : 0.95f;
                Quad(m, x0 + fx * da, y0 + ya, z0 + fz * da, x1 + fx * da, y1 + ya, z1 + fz * da, x1 + fx * db, y1 + yb, z1 + fz * db, x0 + fx * db, y0 + yb,
                     z0 + fz * db, (float)(fx * nh), (float)nv, (float)(fz * nh), MeshColor.Scale(RailSteel, shade), MaterialChannel.Metal, nv < -0.3 ? 0.8f : 0.95f);
            }
            Quad(m, x1, y1, z1, x0, y0, z0, x0, y0 + 0.31f, z0, x1, y1 + 0.31f, z1, (float)-fx, 0f, (float)-fz, MeshColor.Scale(RailSteel, 0.8f), MaterialChannel.Metal,
                 0.8f);
        }

        /// <summary>Parapet posts and guard rails stand this far beyond the road's drawn extent, on the crest of the
        /// embankment (never on the carriageway, footpath or shoulder).</summary>
        public const float PostOffsetM = 0.15f;

        /// <summary>An upright box (four sides and a top) of half side <paramref name="h"/> and height <paramref name="height"/>
        /// standing at (x, y, z).</summary>
        private static void Box(MeshData m, double x, float y, double z, double ux, double uz, double vx, double vz, double h, float height, uint c,
                                MaterialChannel ch, float ao)
        {
            float y1 = y + height;
            for (int f = 0; f < 4; f++)
            {
                double nx = f == 0 ? ux : f == 1 ? vx : f == 2 ? -ux : -vx, nz = f == 0 ? uz : f == 1 ? vz : f == 2 ? -uz : -vz;
                double tx = -nz, tz = nx;
                double cx = x + nx * h, cz = z + nz * h;
                Quad(m, cx - tx * h, y, cz - tz * h, cx + tx * h, y, cz + tz * h, cx + tx * h, y1, cz + tz * h, cx - tx * h, y1, cz - tz * h, (float)nx, 0f,
                     (float)nz, c, ch, ao);
            }
            Quad(m, x - ux * h - vx * h, y1, z - uz * h - vz * h, x + ux * h - vx * h, y1, z + uz * h - vz * h, x + ux * h + vx * h, y1, z + uz * h + vz * h,
                 x - ux * h + vx * h, y1, z - uz * h + vz * h, 0f, 1f, 0f, c, ch, ao);
        }

        /// <summary>A quad (a, b, c, d in order around it) facing the normal (nx, ny, nz): the winding is chosen so the front
        /// (Unity: cross(b - a, c - a)) faces it.</summary>
        private static void Quad(MeshData m, double ax, float ay, double az, double bx, float by, double bz, double cx, float cy, double cz, double dx, float dy,
                                 double dz, float nx, float ny, float nz, uint rgba, MaterialChannel ch, float ao)
        {
            float l = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l < 1e-6f) return;
            nx /= l;
            ny /= l;
            nz /= l;
            int v = m.AddVertex((float)ax, ay, (float)az, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)bx, by, (float)bz, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)cx, cy, (float)cz, nx, ny, nz, rgba, (float)ch, ao);
            m.AddVertex((float)dx, dy, (float)dz, nx, ny, nz, rgba, (float)ch, ao);
            double ex = bx - ax, ey = by - ay, ez = bz - az, fx = cx - ax, fy = cy - ay, fz = cz - az;
            double cxp = ey * fz - ez * fy, cyp = ez * fx - ex * fz, czp = ex * fy - ey * fx;
            if (cxp * nx + cyp * ny + czp * nz >= 0)
            {
                m.AddTriangle(v, v + 1, v + 2);
                m.AddTriangle(v, v + 2, v + 3);
            }
            else
            {
                m.AddTriangle(v, v + 2, v + 1);
                m.AddTriangle(v, v + 3, v + 2);
            }
        }
    }
}
