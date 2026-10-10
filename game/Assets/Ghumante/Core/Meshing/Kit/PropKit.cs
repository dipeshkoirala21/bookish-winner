using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Rounded roof and street-face props for the building grammar, modelled on what stands on Kathmandu Valley roofs
    /// today (docs/research/w2/ref_buildings.md): the ribbed black or blue polyethylene water tank (1,000 L, about
    /// Ø1.1 × 1.1 m) on a steel or brick stand, the evacuated-tube solar water heater (15 tubes Ø58 mm × 1.8 m under a
    /// horizontal storage drum), rebar stubs on column heads, a satellite dish, terracotta pots with plants, laundry
    /// lines and cafe umbrellas. Positions are tile-local metres with absolute Y; <c>(ux, uz)</c> is a horizontal unit
    /// facing for the oriented props. Positions, normals and colours only (paint afterwards). No allocation once warm.
    /// </summary>
    public static class PropKit
    {
        public static readonly uint Steel = MeshColor.FromHex(0x5B6168);
        public static readonly uint Galvanised = MeshColor.FromHex(0xB9BEC3);
        public static readonly uint Terracotta = MeshColor.FromHex(0xB5582F);

        private static int K(double[] r, double[] y, ref int k, double rr, double yy)
        {
            r[k] = rr;
            y[k] = yy;
            return ++k;
        }

        /// <summary>
        /// A rooftop polyethylene tank of radius <paramref name="rad"/> and body height <paramref name="h"/> standing
        /// on y: three raised hoop ribs, a domed shoulder and a screw lid (the common Indian-made 500-2,000 L tank).
        /// <paramref name="sides"/> 8-12. Returns the triangles added.
        /// </summary>
        public static int Tank(MeshData m, double x, double y, double z, double rad, double h, int sides, uint c, uint lid)
        {
            double[] r = KitRound.ProfileR, yy = KitRound.ProfileY;
            int k = 0;
            K(r, yy, ref k, rad * 0.94, 0);
            K(r, yy, ref k, rad, 0.04 * h);
            for (int i = 0; i < 2; i++)
            {
                double y0 = h * (0.28 + 0.34 * i);
                K(r, yy, ref k, rad, y0);
                K(r, yy, ref k, rad * 1.035, y0 + 0.035 * h);
                K(r, yy, ref k, rad, y0 + 0.07 * h);
            }
            K(r, yy, ref k, rad * 0.98, 0.86 * h);
            K(r, yy, ref k, rad * 0.62, 1.0 * h);
            K(r, yy, ref k, 0, 1.03 * h);
            int t = KitRound.LatheScratch(m, x, y, z, k, sides, c, 55);
            k = 0;
            K(r, yy, ref k, rad * 0.36, 0);
            K(r, yy, ref k, rad * 0.36, 0.08 * h);
            K(r, yy, ref k, 0, 0.09 * h);
            t += KitRound.LatheScratch(m, x, y + 1.0 * h, z, k, Math.Max(5, sides - 3), lid, 50);
            return t;
        }

        /// <summary>A steel angle stand under a tank: four legs and a square top frame, <paramref name="half"/> half
        /// side, from y0 to y1.</summary>
        public static int Stand(MeshData m, double x, double y0, double y1, double z, double half, double ux, double uz, uint c)
        {
            int t = 0;
            double wx = uz, wz = -ux;
            for (int i = 0; i < 4; i++)
            {
                double su = (i & 1) == 0 ? -1 : 1, sw = (i & 2) == 0 ? -1 : 1;
                double px = x + (su * ux + sw * wx) * half, pz = z + (su * uz + sw * wz) * half;
                t += MeshKit.Bar(m, px, y0, pz, px, y1, pz, 0.05, c);
            }
            for (int e = -1; e <= 1; e += 2)
            {
                double ax = x + (-ux + e * wx) * half, az = z + (-uz + e * wz) * half, bx = x + (ux + e * wx) * half, bz = z + (uz + e * wz) * half;
                t += MeshKit.Bar(m, ax, y1 - 0.03, az, bx, y1 - 0.03, bz, 0.05, c);
            }
            return t;
        }

        /// <summary>
        /// An evacuated-tube solar water heater facing (ux, uz) (the tubes slope down toward it): a horizontal
        /// storage drum of length <paramref name="len"/> at the back top, <paramref name="tubes"/> glass tubes 1.8 m
        /// long at <paramref name="tiltDeg"/>, a bottom rail and two side frames. Base centre (x, y, z).
        /// </summary>
        public static int SolarHeater(MeshData m, double x, double y, double z, double ux, double uz, double len, int tubes, double tiltDeg,
                                      uint drum, uint tube, uint frame)
        {
            // Local frame: a along the drum, f toward the facing (down-slope).
            double ax = -uz, az = ux;
            double tilt = tiltDeg * Math.PI / 180.0, tl = 1.8;
            double run = tl * Math.Cos(tilt), rise = tl * Math.Sin(tilt);
            double backY = y + 0.25 + rise, frontY = y + 0.25;
            double bx = x - ux * 0.5 * run, bz = z - uz * 0.5 * run; // back (drum) line centre
            double fx = x + ux * 0.5 * run, fz = z + uz * 0.5 * run; // front (rail) line centre
            double hl = 0.5 * len;
            int t = 0;
            // Drum.
            t += KitRound.Rod(m, bx - ax * hl - ux * 0.12, backY + 0.22, bz - az * hl - uz * 0.12, bx + ax * hl - ux * 0.12, backY + 0.22,
                              bz + az * hl - uz * 0.12, 0.23, 10, true, drum);
            // Tubes.
            for (int i = 0; i < tubes; i++)
            {
                double s = -hl + 0.1 + (len - 0.2) * (i + 0.5) / tubes;
                t += KitRound.Rod(m, bx + ax * s, backY, bz + az * s, fx + ax * s, frontY, fz + az * s, 0.032, 5, false, tube);
            }
            // Bottom rail and the two side frames (back leg, front leg, the slope bar).
            t += MeshKit.Bar(m, fx - ax * hl, frontY - 0.04, fz - az * hl, fx + ax * hl, frontY - 0.04, fz + az * hl, 0.05, frame);
            for (int e = -1; e <= 1; e += 2)
            {
                double ex = ax * hl * e, ez = az * hl * e;
                t += MeshKit.Bar(m, bx + ex, y, bz + ez, bx + ex, backY + 0.05, bz + ez, 0.045, frame);
                t += MeshKit.Bar(m, fx + ex, y, fz + ez, fx + ex, frontY, fz + ez, 0.045, frame);
                t += MeshKit.Bar(m, bx + ex, backY - 0.03, bz + ez, fx + ex, frontY - 0.03, fz + ez, 0.045, frame);
                t += MeshKit.Bar(m, bx + ex, y + 0.05, bz + ez, fx + ex, y + 0.05, fz + ez, 0.04, frame);
            }
            return t;
        }

        /// <summary>A column stub with rebar: a bevelled concrete stub (0.23 m square, <paramref name="stub"/> high)
        /// and four bars rising <paramref name="bars"/> metres, some with a plastic bottle capping the end.</summary>
        public static int RebarColumn(MeshData m, double x, double y, double z, double ux, double uz, double stub, double bars, uint concrete,
                                      uint rust, uint cap, bool capped)
        {
            var f = new KitFrame(x, y, z, ux, uz);
            int t = MeshKit.Box(m, f, -0.115, 0.115, 0, stub, -0.115, 0.115, concrete, BoxFaces.All & ~BoxFaces.Bottom);
            for (int i = 0; i < 4; i++)
            {
                double su = (i & 1) == 0 ? -0.07 : 0.07, sw = (i & 2) == 0 ? -0.07 : 0.07;
                double lean = 0.04 * (i - 1.5);
                double px, py, pz, qx, qy, qz;
                f.ToWorld(su, stub, sw, out px, out py, out pz);
                f.ToWorld(su + lean, stub + bars * (0.85 + 0.05 * i), sw - lean * 0.5, out qx, out qy, out qz);
                t += KitRound.Rod(m, px, py, pz, qx, qy, qz, 0.012, 3, false, rust);
                if (capped && i == 1) t += KitRound.Ellipsoid(m, qx, qy, qz, 0.04, 0.07, 0.04, 4, 3, cap);
            }
            return t;
        }

        /// <summary>A satellite dish (Ø <paramref name="dia"/>) on a short mast, facing the bearing (ux, uz) and
        /// tilted up 35°.</summary>
        public static int Dish(MeshData m, double x, double y, double z, double ux, double uz, double dia, uint c, uint mast)
        {
            double ul = Math.Sqrt(ux * ux + uz * uz);
            if (ul < 1e-9)
            {
                ux = 0;
                uz = 1;
            }
            else
            {
                ux /= ul;
                uz /= ul;
            }
            int t = KitRound.Rod(m, x, y, z, x, y + 0.75, z, 0.025, 4, false, mast);
            double[] r = KitRound.ProfileR, yy = KitRound.ProfileY;
            int k = 0;
            K(r, yy, ref k, 0, 0);
            K(r, yy, ref k, 0.25 * dia, 0.02 * dia);
            K(r, yy, ref k, 0.5 * dia, 0.1 * dia);
            K(r, yy, ref k, 0.48 * dia, 0.09 * dia);
            K(r, yy, ref k, 0, -0.01 * dia);
            int v0 = m.VertexCount;
            t += KitRound.LatheScratch(m, 0, 0, 0, k, 8, c, 60);
            // Tilt the dish: rotate about the horizontal axis perpendicular to the facing, then place it.
            double tilt = 55 * Math.PI / 180, ct = Math.Cos(tilt), st = Math.Sin(tilt);
            float[] p = m.Positions, n = m.Normals;
            for (int v = v0; v < m.VertexCount; v++)
            {
                int i = 3 * v;
                for (int pass = 0; pass < 2; pass++)
                {
                    float[] a = pass == 0 ? p : n;
                    double lx = a[i], ly = a[i + 1], lz = a[i + 2];
                    // Local up (y) tips toward the facing: forward component along (ux, uz).
                    double fwd = lx * ux + lz * uz, side = -lx * uz + lz * ux;
                    double f2 = fwd * ct + ly * st, y2 = -fwd * st + ly * ct;
                    double ox = pass == 0 ? x : 0, oy = pass == 0 ? y + 0.8 : 0, oz = pass == 0 ? z : 0;
                    a[i] = (float)(ox + f2 * ux - side * uz);
                    a[i + 1] = (float)(oy + y2);
                    a[i + 2] = (float)(oz + f2 * uz + side * ux);
                }
            }
            return t;
        }

        /// <summary>A terracotta pot (height <paramref name="h"/>) with a leafy plant and, when
        /// <paramref name="flower"/> is not 0, a few blossoms (marigold, geranium).</summary>
        public static int PottedPlant(MeshData m, double x, double y, double z, double h, uint pot, uint leaf, uint flower)
        {
            double[] r = KitRound.ProfileR, yy = KitRound.ProfileY;
            int k = 0;
            K(r, yy, ref k, 0.32 * h, 0);
            K(r, yy, ref k, 0.5 * h, 0.88 * h);
            K(r, yy, ref k, 0.54 * h, h);
            K(r, yy, ref k, 0.4 * h, h);
            int t = KitRound.LatheScratch(m, x, y, z, k, 6, pot, 50);
            t += KitRound.Ellipsoid(m, x, y + 1.35 * h, z, 0.6 * h, 0.55 * h, 0.6 * h, 6, 3, leaf);
            if (flower != 0)
                for (int i = 0; i < 2; i++)
                {
                    double a = 2.6 * i + 0.4;
                    t += KitRound.Ellipsoid(m, x + 0.42 * h * Math.Cos(a), y + 1.6 * h + 0.1 * h * i, z + 0.42 * h * Math.Sin(a), 0.16 * h, 0.13 * h,
                                            0.16 * h, 4, 2, flower);
                }
            return t;
        }

        /// <summary>A laundry line from a to b (a sagging rod) with <paramref name="count"/> clothes hanging from it,
        /// each a two-sided panel; <paramref name="colours"/> is cycled from <paramref name="seed"/>.</summary>
        public static int Laundry(MeshData m, double ax, double ay, double az, double bx, double by, double bz, int count, uint[] colours, uint seed,
                                  uint line)
        {
            int t = 0;
            double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz), my = 0.5 * (ay + by) - 0.12;
            t += KitRound.Rod(m, ax, ay, az, mx, my, mz, 0.008, 3, false, line);
            t += KitRound.Rod(m, mx, my, mz, bx, by, bz, 0.008, 3, false, line);
            double dx = bx - ax, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.5) return t;
            double ux = dx / len, uz = dz / len;
            var rng = new GrammarRng(seed, 0x4C4E4452);
            double pos = 0.25;
            for (int i = 0; i < count && pos < len - 0.3; i++)
            {
                double w = rng.Range(0.35f, 0.75f), h = rng.Range(0.45f, 0.95f);
                if (pos + w > len - 0.15) break;
                double s0 = pos / len, s1 = (pos + w) / len;
                double y0 = Sag(ay, by, s0), y1 = Sag(ay, by, s1);
                double x0 = ax + dx * s0, z0 = az + dz * s0, x1 = ax + dx * s1, z1 = az + dz * s1;
                uint c = colours[(int)((seed + (uint)i * 7u) % (uint)colours.Length)];
                t += MeshKit.Quad(m, x0, y0, z0, x1, y1, z1, x1, y1 - h, z1, x0, y0 - h, z0, uz, 0, -ux, c);
                t += MeshKit.Quad(m, x0, y0, z0, x1, y1, z1, x1, y1 - h, z1, x0, y0 - h, z0, -uz, 0, ux, c);
                pos += w + rng.Range(0.08f, 0.35f);
            }
            return t;
        }

        private static double Sag(double ay, double by, double s)
        {
            return ay + (by - ay) * s - 0.48 * s * (1 - s);
        }

        /// <summary>A cafe umbrella: pole, an eight-panel canopy of radius <paramref name="rad"/> (top and underside)
        /// with its rim at y + <paramref name="rimH"/>, and a round table under it.</summary>
        public static int Umbrella(MeshData m, double x, double y, double z, double rad, double rimH, uint canopy, uint pole, uint table)
        {
            int t = KitRound.Rod(m, x, y, z, x, y + rimH + 0.45, z, 0.025, 4, false, pole);
            double[] r = KitRound.ProfileR, yy = KitRound.ProfileY;
            int k = 0;
            K(r, yy, ref k, rad, 0);
            K(r, yy, ref k, 0.5 * rad, 0.28);
            K(r, yy, ref k, 0, 0.42);
            t += KitRound.LatheScratch(m, x, y + rimH, z, k, 8, canopy, 30);
            k = 0;
            K(r, yy, ref k, 0, 0.38);
            K(r, yy, ref k, rad * 0.98, -0.02);
            t += KitRound.LatheScratch(m, x, y + rimH, z, k, 8, MeshColor.Scale(canopy, 0.8f), 30);
            k = 0;
            K(r, yy, ref k, 0.45, 0.72);
            K(r, yy, ref k, 0.45, 0.76);
            K(r, yy, ref k, 0, 0.76);
            t += KitRound.LatheScratch(m, x, y, z, k, 8, table, 50);
            return t;
        }

        /// <summary>A plastic chair (the moulded one on every Kathmandu terrace): seat, back and four legs, facing
        /// (ux, uz).</summary>
        public static int Chair(MeshData m, double x, double y, double z, double ux, double uz, uint c)
        {
            var f = new KitFrame(x, y, z, ux, uz);
            int t = KitRound.BoxU(m, f, -0.22, 0.22, 0.42, 0.47, -0.2, 0.2, 0.02, 1, BoxFaces.All, c);
            t += KitRound.BoxU(m, f, -0.2, 0.2, 0.47, 0.85, -0.22, -0.18, 0.015, 1, BoxFaces.All, c);
            for (int i = 0; i < 4; i++)
            {
                double su = (i & 1) == 0 ? -0.18 : 0.18, sw = (i & 2) == 0 ? -0.17 : 0.17;
                double px, py, pz, qx, qy, qz;
                f.ToWorld(su * 1.1, 0, sw * 1.1, out px, out py, out pz);
                f.ToWorld(su, 0.43, sw, out qx, out qy, out qz);
                t += MeshKit.Bar(m, px, py, pz, qx, qy, qz, 0.03, c);
            }
            return t;
        }

        /// <summary>A string of prayer flags from a to b in the fixed order blue, white, red, green, yellow, flags
        /// 0.24 × 0.3 m at a 0.36 m pitch.</summary>
        public static int PrayerFlags(MeshData m, double ax, double ay, double az, double bx, double by, double bz, uint line)
        {
            int t = KitRound.Rod(m, ax, ay, az, bx, by, bz, 0.006, 3, false, line);
            double dx = bx - ax, dy = by - ay, dz = bz - az, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.5) return t;
            double ux = dx / len, uz = dz / len;
            int n = (int)((len - 0.2) / 0.36);
            for (int i = 0; i < n; i++)
            {
                double s0 = (0.1 + 0.36 * i) / len, s1 = (0.1 + 0.36 * i + 0.24) / len;
                double sag0 = 0.35 * s0 * (1 - s0) * 4 * 0.25, sag1 = 0.35 * s1 * (1 - s1) * 4 * 0.25;
                double x0 = ax + dx * s0, y0 = ay + dy * s0 - sag0, z0 = az + dz * s0, x1 = ax + dx * s1, y1 = ay + dy * s1 - sag1, z1 = az + dz * s1;
                uint c = Flag[i % 5];
                t += MeshKit.Quad(m, x0, y0, z0, x1, y1, z1, x1, y1 - 0.3, z1, x0, y0 - 0.3, z0, uz, 0, -ux, c);
                t += MeshKit.Quad(m, x0, y0, z0, x1, y1, z1, x1, y1 - 0.3, z1, x0, y0 - 0.3, z0, -uz, 0, ux, c);
            }
            return t;
        }

        /// <summary>Prayer-flag colours in their fixed order (blue, white, red, green, yellow).</summary>
        public static readonly uint[] Flag =
        {
            MeshColor.FromHex(0x2E6FD8), MeshColor.FromHex(0xFFFFFF), MeshColor.FromHex(0xD93A2B), MeshColor.FromHex(0x2E9E4F), MeshColor.FromHex(0xF2C230),
        };
    }
}
