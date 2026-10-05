using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>A Newar votive chaitya (W2_DESIGN 3.2; temples.md 2.4): 0.8-3 m tall, 0.6-2 m square.</summary>
    public struct ChaityaParams
    {
        public float HeightM, BaseW;
        public int Steps;

        public static ChaityaParams Defaults(float height)
        {
            return new ChaityaParams { HeightM = height, BaseW = Math.Max(0.6f, Math.Min(2.0f, 0.6f * height)), Steps = 3 };
        }
    }

    /// <summary>
    /// The chaitya generator: a stepped base, a drum with four niches holding the four directional Buddhas in their
    /// fixed compass order (E Akshobhya, S Ratnasambhava, W Amitabha, N Amoghasiddhi; Vairocana at the centre unseen),
    /// a dome, harmika, a 13-ring cone and a parasol, in grey stone with sindoor at the niches. The niches face the real
    /// compass directions whatever the frame's yaw. ≤ 600 triangles at LOD0.
    /// </summary>
    public static class ChaityaGenerator
    {
        /// <summary>Niche order: east, south, west, north (W2_DESIGN 3.2).</summary>
        public static readonly string[] NicheOrder = { "Akshobhya", "Ratnasambhava", "Amitabha", "Amoghasiddhi" };

        /// <summary>Bearings (degrees clockwise from north) of the niches in <see cref="NicheOrder"/>.</summary>
        public static readonly float[] NicheBearing = { 90f, 180f, 270f, 0f };

        public static int Build(in ChaityaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in ChaityaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int t0 = m.TriangleCount;
            double h = Math.Max(0.5, p.HeightM), bw = Math.Max(0.4, p.BaseW);
            int steps = Math.Max(1, Math.Min(4, p.Steps));
            var north = KitFrame.FromYaw(f.X, f.GroundY, f.Z, 0);
            double y = 0, stepH = 0.08 * h;
            for (int s = 0; s < steps; s++)
            {
                double half = 0.5 * bw * (1 - 0.1 * s);
                MeshKit.Box(m, north, -half, half, s == 0 ? -0.2 : y, y + stepH, -half, half, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                y += stepH;
            }
            if (lod >= 3)
            {
                MeshKit.Frustum(m, f.X, f.Z, 0.3 * bw, f.GroundY + y, 0, f.GroundY + h, 4, false, SacredPalette.StoneGrey);
                return m.TriangleCount - t0;
            }
            double drumHalf = 0.32 * bw, drumH = 0.22 * h;
            MeshKit.Box(m, north, -drumHalf, drumHalf, y, y + drumH, -drumHalf, drumHalf, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            // Four niches with their Buddhas, in compass order.
            for (int q = 0; q < 4; q++)
            {
                var nf = KitFrame.FromYaw(f.X, f.GroundY, f.Z, NicheBearing[q]);
                MeshKit.Panel(m, nf, -0.5 * drumHalf, y + 0.15 * drumH, 0.5 * drumHalf, y + 0.85 * drumH, drumHalf + 0.005, MeshColor.FromHex(0x3A3630));
                if (lod <= 1)
                {
                    double x, yy, z;
                    nf.ToWorld(0, y + 0.4 * drumH, drumHalf + 0.06, out x, out yy, out z);
                    MeshKit.Blob(m, x, yy, z, 0.18 * drumHalf, 0.3 * drumH, 0.08 * drumHalf, SacredPalette.NicheBuddha[q]);
                    MeshKit.Panel(m, nf, -0.1 * drumHalf, y + 0.02 * drumH, 0.1 * drumHalf, y + 0.12 * drumH, drumHalf + 0.01, SacredPalette.Sindoor);
                }
                else
                {
                    MeshKit.Panel(m, nf, -0.2 * drumHalf, y + 0.2 * drumH, 0.2 * drumHalf, y + 0.7 * drumH, drumHalf + 0.01, SacredPalette.NicheBuddha[q]);
                }
            }
            if (stats != null) stats.Niches = 4;
            y += drumH;
            double domeR = 0.3 * bw, domeH = 0.16 * h;
            MeshKit.Dome(m, f.X, f.Z, domeR, f.GroundY + y, domeH, lod <= 1 ? 10 : 6, lod <= 1 ? 3 : 2, SacredPalette.StoneGrey);
            y += domeH * 0.9;
            double hh = 0.07 * h, hw = 0.12 * bw;
            MeshKit.Box(m, north, -hw, hw, y, y + hh, -hw, hw, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            y += hh;
            double coneH = Math.Max(0.1, h - y - 0.06 * h);
            MeshKit.Frustum(m, f.X, f.Z, 0.11 * bw, f.GroundY + y, 0.02 * bw, f.GroundY + y + coneH, lod <= 1 ? 8 : 4, false, SacredPalette.StoneGrey);
            MeshKit.Frustum(m, f.X, f.Z, 0.08 * bw, f.GroundY + h - 0.06 * h, 0.0, f.GroundY + h, 6, false, SacredPalette.Gilt);
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + h, 0.5 * bw, 0.5 * bw, 1, 0, GenColliderFlags.NoClimb, GenColliders.Stone);
            if (stats != null)
            {
                stats.TopM = (float)h;
                stats.Rings = 13;
            }
            return m.TriangleCount - t0;
        }
    }

    /// <summary>Kind of a small shrine (temples.md 2.7), from the name or religion.</summary>
    public enum ShrineKind : byte
    {
        Generic = 0,
        Ganesh = 1,
        Bhairav = 2,
        Linga = 3,
        Nag = 4,
    }

    /// <summary>Form of a small shrine.</summary>
    public enum ShrineForm : byte
    {
        Niche = 0,
        MiniPagoda = 1,
        TinCanopy = 2,
    }

    /// <summary>A small shrine (corner Ganesh, Bhairav, linga, nag stone; W2_DESIGN 3.2).</summary>
    public struct ShrineParams
    {
        public ShrineKind Kind;
        public ShrineForm Form;
        public float LongSideM, ShortSideM;
        public float ImageM;
    }

    /// <summary>The shrine generator: an open niche, a one-tier mini pagoda or a tin canopy over a sindoor-red image.
    /// ≤ 1,500 triangles at LOD0.</summary>
    public static class ShrineGenerator
    {
        public static int Build(in ShrineParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            double l = Math.Max(0.8, p.LongSideM), s = Math.Max(0.8, p.ShortSideM > 0 ? p.ShortSideM : 0.8 * l);
            double img = p.ImageM > 0 ? p.ImageM : Math.Min(0.8, 0.12 * l + 0.3);
            switch (p.Form)
            {
                case ShrineForm.MiniPagoda:
                {
                    PagodaParams pp = PagodaParams.Defaults((float)l, (float)s, 1);
                    pp.PlinthLevels = 1;
                    pp.StepRiseM = 0.3f;
                    pp.CoreFrac = 0.6f;
                    pp.FirstEaveFrac = 1.6f;
                    pp.TotalHeightM = (float)Math.Max(2.5, Math.Min(6.0, 0.9 * l));
                    pp.Guardians = GuardianSet.None;
                    pp.BellSpacingM = 0.6f;
                    PagodaGenerator.Build(pp, f, Math.Max(1, lod), m, c);
                    break;
                }
                case ShrineForm.TinCanopy:
                {
                    for (int q = 0; q < 4; q++)
                    {
                        double u = (q % 2 == 0 ? -1 : 1) * (0.5 * l - 0.1), w = (q < 2 ? -1 : 1) * (0.5 * s - 0.1), x, y, z;
                        k.ToWorld(u, 0, w, out x, out y, out z);
                        MeshKit.Cylinder(m, x, z, 0.05, y, y + 2.2, 4, false, MeshColor.FromHex(0x6B6B6B));
                    }
                    MeshKit.QuadLocal(m, k, -0.5 * l - 0.2, 2.4, -0.5 * s - 0.2, 0.5 * l + 0.2, 2.4, -0.5 * s - 0.2, 0.5 * l + 0.2, 2.1, 0.5 * s + 0.2,
                                      -0.5 * l - 0.2, 2.1, 0.5 * s + 0.2, 0, 1, 0.2, MeshColor.FromHex(0x3D7CC9));
                    MeshKit.QuadLocal(m, k, -0.5 * l - 0.2, 2.4, -0.5 * s - 0.2, 0.5 * l + 0.2, 2.4, -0.5 * s - 0.2, 0.5 * l + 0.2, 2.1, 0.5 * s + 0.2,
                                      -0.5 * l - 0.2, 2.1, 0.5 * s + 0.2, 0, -1, -0.2, MeshColor.FromHex(0x9AA0A6));
                    MeshKit.Box(m, k, -0.4 * l, 0.4 * l, 0, 0.3, -0.4 * s, 0.4 * s, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                    Image(m, k, p.Kind, 0.3, 0, img, lod);
                    break;
                }
                default:
                {
                    double hgt = Math.Max(1.2, Math.Min(3.0, 0.6 * l + 0.8));
                    MeshKit.Box(m, k, -0.5 * l, 0.5 * l, -0.2, hgt, -0.5 * s, 0.5 * s, SacredPalette.BrickDachi, BoxFaces.All & ~BoxFaces.Bottom);
                    MeshKit.Frustum(m, f.X, f.Z, 0.75 * Math.Max(l, s), f.GroundY + hgt, 0.0, f.GroundY + hgt + 0.5 * l, 4, false, SacredPalette.Tile);
                    MeshKit.Panel(m, k, -0.3 * l, 0.25, 0.3 * l, hgt - 0.3, 0.5 * s + 0.005, SacredPalette.SanctumDark);
                    Image(m, k, p.Kind, 0.3, 0.5 * s - 0.15, img, lod);
                    break;
                }
            }
            if (lod <= 1)
            {
                // A hanging bell by the shrine.
                double x, y, z;
                k.ToWorld(0.5 * l + 0.2, 0, 0.5 * s + 0.2, out x, out y, out z);
                MeshKit.Frustum(m, x, z, 0.12, f.GroundY + 1.6, 0.06, f.GroundY + 1.85, 6, true, SacredPalette.Gilt);
            }
            if (c != null) c.AddBox(f.X, f.Z, f.GroundY, f.GroundY + 2.0, 0.5 * l, 0.5 * s, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Stone);
            return m.TriangleCount - t0;
        }

        /// <summary>A sindoor-red image: Ganesh (a blob with a trunk), a linga (a stone cylinder on a yoni), a nag stone
        /// or a generic image.</summary>
        private static void Image(MeshData m, in KitFrame k, ShrineKind kind, double v, double w, double size, int lod)
        {
            double x, y, z;
            k.ToWorld(0, v, w, out x, out y, out z);
            switch (kind)
            {
                case ShrineKind.Linga:
                    MeshKit.Frustum(m, x, z, 0.35 * size, y, 0.3 * size, y + 0.12 * size, 8, true, SacredPalette.StoneGrey);
                    MeshKit.Cylinder(m, x, z, 0.12 * size, y + 0.12 * size, y + 0.55 * size, 8, true, MeshColor.FromHex(0x2A2A2E));
                    break;
                case ShrineKind.Nag:
                    MeshKit.Box(m, k, -0.2 * size, 0.2 * size, v, v + 0.7 * size, w - 0.05, w + 0.05, SacredPalette.StoneGrey, BoxFaces.All);
                    break;
                default:
                    MeshKit.Blob(m, x, y + 0.35 * size, z, 0.25 * size, 0.35 * size, 0.2 * size, SacredPalette.Sindoor);
                    if (kind == ShrineKind.Ganesh && lod <= 1)
                    {
                        k.ToWorld(0, v + 0.3 * size, w + 0.18 * size, out x, out y, out z);
                        MeshKit.Blob(m, x, y, z, 0.05 * size, 0.18 * size, 0.05 * size, SacredPalette.Sindoor);
                    }
                    break;
            }
        }
    }
}
