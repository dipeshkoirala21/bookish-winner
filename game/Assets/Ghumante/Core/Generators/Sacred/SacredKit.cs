using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>Where a generator builds (W2_DESIGN 10.3): tile-local metres, the ground height and the main door's
    /// compass bearing (the outward normal of the door, clockwise from north).</summary>
    public struct GenFrame
    {
        public double X, Z;
        public float GroundY, YawDeg;

        public GenFrame(double x, double z, float groundY, float yawDeg)
        {
            X = x;
            Z = z;
            GroundY = groundY;
            YawDeg = yawDeg;
        }

        /// <summary>The kit frame: +W points through the main door, U across it, origin at ground level.</summary>
        public KitFrame Kit
        {
            get { return KitFrame.FromYaw(X, GroundY, Z, YawDeg); }
        }
    }

    /// <summary>Roof finish of a pagoda (W2_DESIGN 3.2).</summary>
    public enum RoofFinish : byte
    {
        Tile = 0,
        GiltTop = 1,
        GiltAll = 2,
    }

    /// <summary>Guardian figures on the main stair, bottom to top.</summary>
    public enum GuardianSet : byte
    {
        None = 0,

        /// <summary>One pair of lions at the foot of the stair (the default).</summary>
        Lions = 1,

        /// <summary>Nyatapola: wrestlers, elephants, lions, griffins, goddesses (one pair per plinth level).</summary>
        Nyatapola = 2,

        /// <summary>The wrestler pair (Jaya and Patta) of Dattatreya.</summary>
        Wrestlers = 3,

        /// <summary>Stone elephants (Vishwanath's east door).</summary>
        Elephants = 4,
    }

    /// <summary>What a generator built, for checks (V4) and for presenters.</summary>
    public sealed class SacredStats
    {
        public int Tiers, PlinthLevels, Bells, Struts, Pinnacles, Rings, Terraces, Steps, Windows, Niches, FlagLines;

        /// <summary>Highest point above the frame's ground (the finial tip).</summary>
        public float TopM;

        /// <summary>Height of the top plinth or terrace above the ground.</summary>
        public float PlinthTopM;

        /// <summary>Main door bearing (degrees clockwise from north) as built.</summary>
        public float DoorYawDeg;

        /// <summary>Sanctum volume in frame space (u, w half extents and v range); empty when there is none.</summary>
        public float SanctumHalfU, SanctumHalfW, SanctumV0, SanctumV1;

        public void Clear()
        {
            Tiers = PlinthLevels = Bells = Struts = Pinnacles = Rings = Terraces = Steps = Windows = Niches = FlagLines = 0;
            TopM = PlinthTopM = DoorYawDeg = 0f;
            SanctumHalfU = SanctumHalfW = SanctumV0 = SanctumV1 = 0f;
        }
    }

    /// <summary>Cartoon palette of the sacred generators (temples.md 2.0, W2_DESIGN 2.5).</summary>
    public static class SacredPalette
    {
        public static readonly uint BrickDachi = MeshColor.FromHex(0xA0472E);
        public static readonly uint BrickPlinth = MeshColor.FromHex(0xB5603E);
        public static readonly uint Ochre = MeshColor.FromHex(0x9A5A32);
        public static readonly uint Tile = MeshColor.FromHex(0x7A2E22);
        public static readonly uint TileLight = MeshColor.FromHex(0x8E3A2A);
        public static readonly uint WoodCarved = MeshColor.FromHex(0x3E2416);
        public static readonly uint WoodRed = MeshColor.FromHex(0xB3261E);
        public static readonly uint Gilt = MeshColor.FromHex(0xD9A62E);
        public static readonly uint GiltHi = MeshColor.FromHex(0xF2CC5B);
        public static readonly uint GiltLo = MeshColor.FromHex(0x9C7317);
        public static readonly uint Silver = MeshColor.FromHex(0xC9CED6);
        public static readonly uint StoneGrey = MeshColor.FromHex(0x8E8A80);
        public static readonly uint StoneBuff = MeshColor.FromHex(0xB9A684);
        public static readonly uint Whitewash = MeshColor.FromHex(0xF6F0E2);
        public static readonly uint Cream = MeshColor.FromHex(0xECCCA0);
        public static readonly uint Saffron = MeshColor.FromHex(0xF2A93B);
        public static readonly uint Sindoor = MeshColor.FromHex(0xE0412B);
        public static readonly uint EyesInk = MeshColor.FromHex(0x1B1B24);
        public static readonly uint EyesBlue = MeshColor.FromHex(0x2F5DA8);
        public static readonly uint Terracotta = MeshColor.FromHex(0xB5603E);
        public static readonly uint Lime = MeshColor.FromHex(0xF4EFE0);

        /// <summary>The closed sanctum door: an opaque dark quad (never an interior).</summary>
        public static readonly uint SanctumDark = MeshColor.FromHex(0x14100C);

        /// <summary>The lamp glow on a sanctum door.</summary>
        public static readonly uint LampGlow = MeshColor.FromHex(0xFFC860);

        /// <summary>Prayer flags in the fixed order blue, white, red, green, yellow (sky, air, fire, water, earth).</summary>
        public static readonly uint[] Flags =
        {
            MeshColor.FromHex(0x2E6FD8), MeshColor.FromHex(0xFFFFFF), MeshColor.FromHex(0xD93A2B), MeshColor.FromHex(0x2E9E4F),
            MeshColor.FromHex(0xF2C230),
        };

        /// <summary>Dhyani Buddha colours of the chaitya niches E, S, W, N (Akshobhya blue, Ratnasambhava yellow,
        /// Amitabha red, Amoghasiddhi green).</summary>
        public static readonly uint[] NicheBuddha =
        {
            MeshColor.FromHex(0x2F5DA8), MeshColor.FromHex(0xE8B923), MeshColor.FromHex(0xC8282E), MeshColor.FromHex(0x2E8B3F),
        };
    }

    /// <summary>Shared pieces of the sacred generators: steps and stairs, guardian figures, toranas, sanctum doors,
    /// bells, the gajur finial and prayer-flag lines. All take a <see cref="KitFrame"/> and local (u, v, w).</summary>
    internal static class SacredKit
    {
        /// <summary>A flight of steps along +W from w0 (top edge, against the platform) to w0 + run, rising from v0 to
        /// v1 toward w0, between u0 and u1; LOD ≥ 2 draws one sloped slab. Adds a walkable ramp. Returns the steps.</summary>
        public static int Stair(MeshData m, GenColliders c, in KitFrame f, double u0, double u1, double w0, double run, double v0, double v1,
                                int lod, uint col, byte material)
        {
            double rise = v1 - v0;
            if (rise <= 0.01 || run <= 0.01) return 0;
            int steps = Math.Max(1, (int)Math.Ceiling(rise / 0.3));
            if (lod >= 2)
            {
                MeshKit.QuadLocal(m, f, u0, v0, w0 + run, u1, v0, w0 + run, u1, v1, w0, u0, v1, w0, 0, 1, 1, col);
                MeshKit.QuadLocal(m, f, u0, v0, w0 + run, u0, v1, w0, u0, v0, w0, u0, v0, w0 + run, -1, 0, 0, col);
            }
            else
            {
                double tread = run / steps, step = rise / steps;
                for (int k = 0; k < steps; k++)
                {
                    double top = v0 + (k + 1) * step, wa = w0 + run - (k + 1) * tread, wb = w0 + run - k * tread;
                    MeshKit.Box(m, f, u0, u1, v0, top, wa, wb, col, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                }
            }
            if (c != null)
            {
                double x0, y0, z0, x1, y1, z1;
                f.ToWorld(0.5 * (u0 + u1), v0, w0 + run, out x0, out y0, out z0);
                f.ToWorld(0.5 * (u0 + u1), v1, w0, out x1, out y1, out z1);
                c.AddRamp(new GenRamp { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Y0 = (float)y0, Y1 = (float)y1, HalfWidth = (float)(0.5 * (u1 - u0)), Material = material });
            }
            return steps;
        }

        /// <summary>A stylised guardian on a pedestal at (u, w), facing +W; kind 0 lion, 1 wrestler, 2 elephant,
        /// 3 griffin, 4 goddess. Height <paramref name="h"/>.</summary>
        public static void Guardian(MeshData m, in KitFrame f, double u, double w, double v, double h, int kind, int lod)
        {
            uint stone = kind == 0 ? SacredPalette.Lime : SacredPalette.StoneGrey;
            double ped = 0.25 * h, bodyW = 0.35 * h;
            MeshKit.Box(m, f, u - 0.5 * bodyW, u + 0.5 * bodyW, v, v + ped, w - 0.6 * bodyW, w + 0.6 * bodyW, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod >= 2)
            {
                MeshKit.Box(m, f, u - 0.35 * bodyW, u + 0.35 * bodyW, v + ped, v + h, w - 0.4 * bodyW, w + 0.4 * bodyW, stone, BoxFaces.All & ~BoxFaces.Bottom);
                return;
            }
            double x, y, z;
            switch (kind)
            {
                case 2: // elephant: big body, head, trunk
                    f.ToWorld(u, v + ped + 0.35 * h, w - 0.1 * h, out x, out y, out z);
                    MeshKit.Blob(m, x, y, z, 0.3 * h, 0.3 * h, 0.4 * h, stone);
                    f.ToWorld(u, v + ped + 0.45 * h, w + 0.3 * h, out x, out y, out z);
                    MeshKit.Blob(m, x, y, z, 0.2 * h, 0.2 * h, 0.2 * h, stone);
                    f.ToWorld(u, v + ped + 0.1 * h, w + 0.45 * h, out x, out y, out z);
                    MeshKit.Blob(m, x, y, z, 0.06 * h, 0.2 * h, 0.06 * h, stone);
                    break;
                default: // seated lion, griffin, or a standing figure
                {
                    bool standing = kind == 1 || kind == 4;
                    f.ToWorld(u, v + ped + (standing ? 0.3 : 0.25) * h, w, out x, out y, out z);
                    MeshKit.Blob(m, x, y, z, 0.2 * h, (standing ? 0.3 : 0.25) * h, 0.22 * h, stone);
                    f.ToWorld(u, v + ped + (standing ? 0.68 : 0.6) * h, w + 0.08 * h, out x, out y, out z);
                    MeshKit.Blob(m, x, y, z, 0.13 * h, 0.13 * h, 0.13 * h, kind == 4 ? SacredPalette.Sindoor : stone);
                    if (kind == 3)
                    {
                        // Griffin wings.
                        f.ToWorld(u, v + ped + 0.5 * h, w - 0.1 * h, out x, out y, out z);
                        MeshKit.Blob(m, x, y, z, 0.32 * h, 0.08 * h, 0.12 * h, SacredPalette.Gilt);
                    }
                    break;
                }
            }
        }

        /// <summary>A semicircular torana (a flat fan) above a door centred at u, its base at v, on the wall plane w.</summary>
        public static void Torana(MeshData m, in KitFrame f, double u, double v, double w, double width, uint col, int lod)
        {
            int seg = lod >= 2 ? 3 : 6;
            double r = 0.5 * width;
            for (int k = 0; k < seg; k++)
            {
                double a0 = Math.PI * k / seg, a1 = Math.PI * (k + 1) / seg;
                MeshKit.TriLocal(m, f, u, v, w, u + r * Math.Cos(a0), v + r * Math.Sin(a0) * 0.8, w, u + r * Math.Cos(a1), v + r * Math.Sin(a1) * 0.8, w,
                                 0, 0, 1, col);
            }
        }

        /// <summary>A closed sanctum door on the wall plane w: a carved frame, the opaque dark doorway and a lamp glow.</summary>
        public static void SanctumDoor(MeshData m, in KitFrame f, double u, double v, double w, double dw, double dh, uint frame, int lod)
        {
            MeshKit.Box(m, f, u - 0.5 * dw - 0.15, u + 0.5 * dw + 0.15, v, v + dh + 0.15, w, w + 0.08, frame, lod >= 2 ? BoxFaces.Front : BoxFaces.Wall);
            MeshKit.Panel(m, f, u - 0.5 * dw, v, u + 0.5 * dw, v + dh, w + 0.085, SacredPalette.SanctumDark);
            if (lod < 2) MeshKit.Panel(m, f, u - 0.08, v + 0.6, u + 0.08, v + 0.8, w + 0.09, SacredPalette.LampGlow);
        }

        /// <summary>The gajur finial: a stacked gilt bell (kalasha), cone and parasol from v0, total height h.</summary>
        public static int Gajur(MeshData m, double x, double z, double v0, double h, double baseR, int lod, uint gilt)
        {
            int t = 0;
            int sides = lod >= 2 ? 4 : 8;
            t += MeshKit.Frustum(m, x, z, baseR, v0, 0.6 * baseR, v0 + 0.2 * h, sides, false, gilt);
            if (lod < 2)
            {
                t += MeshKit.Blob(m, x, v0 + 0.35 * h, z, 0.7 * baseR, 0.18 * h, 0.7 * baseR, gilt);
                t += MeshKit.Frustum(m, x, z, 0.9 * baseR, v0 + 0.55 * h, 0.15 * baseR, v0 + 0.62 * h, sides, false, SacredPalette.GiltHi);
            }
            t += MeshKit.Frustum(m, x, z, 0.35 * baseR, v0 + 0.5 * h, 0.0, v0 + h, sides, false, gilt);
            return t;
        }

        /// <summary>A line of prayer flags from (x0, y0, z0) to (x1, y1, z1) with a sag, as <paramref name="segments"/>
        /// two-sided quads cycling the five colours. Returns the triangles.</summary>
        public static int FlagLine(MeshData m, double x0, double y0, double z0, double x1, double y1, double z1, double sag, int segments, double flagH)
        {
            int t = 0;
            double dx = x1 - x0, dz = z1 - z0, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-6) return 0;
            double nx = -dz / l, nz = dx / l;
            for (int k = 0; k < segments; k++)
            {
                double a = (double)k / segments, b = (double)(k + 1) / segments;
                double ya = y0 + (y1 - y0) * a - 4 * sag * a * (1 - a), yb = y0 + (y1 - y0) * b - 4 * sag * b * (1 - b);
                double xa = x0 + dx * a, za = z0 + dz * a, xb = x0 + dx * b, zb = z0 + dz * b;
                uint c = SacredPalette.Flags[k % 5];
                t += MeshKit.Quad(m, xa, ya, za, xb, yb, zb, xb, yb - flagH, zb, xa, ya - flagH, za, nx, 0, nz, c);
                t += MeshKit.Quad(m, xa, ya, za, xb, yb, zb, xb, yb - flagH, zb, xa, ya - flagH, za, -nx, 0, -nz, c);
            }
            return t;
        }

        /// <summary>A rectangular ring of 8 points (corners and edge midpoints, counter-clockwise from above) of half
        /// sizes (hu, hw) in the frame at height v, corners raised by <paramref name="lift"/>.</summary>
        public static void Ring8(in KitFrame f, double hu, double hw, double v, double lift, double[] x, double[] y, double[] z)
        {
            // Frame (u, w) is turned: W = (U.z, -U.x) is U rotated clockwise, so counter-clockwise from above runs
            // u+ → w− ... Build in the order (+u,+w), (0,+w), (−u,+w), (−u,0), (−u,−w), (0,−w), (+u,−w), (+u,0) and fix
            // the winding by the signed area.
            double[] us = { hu, 0, -hu, -hu, -hu, 0, hu, hu }, ws = { hw, hw, hw, 0, -hw, -hw, -hw, 0 };
            for (int k = 0; k < 8; k++)
            {
                double yy;
                f.ToWorld(us[k], v + (k % 2 == 0 ? lift : 0), ws[k], out x[k], out yy, out z[k]);
                y[k] = yy;
            }
            double a = 0;
            for (int i = 0, j = 7; i < 8; j = i++) a += x[j] * z[i] - x[i] * z[j];
            if (a < 0)
            {
                Array.Reverse(x, 0, 8);
                Array.Reverse(y, 0, 8);
                Array.Reverse(z, 0, 8);
            }
        }
    }
}
