using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Horizon;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Sky;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check of the skyline (docs/W2_DETAIL_CONTRACT.md §6): from Basantapur, Boudha and Swayambhu, the
    /// streamed terrain in front of the handover distance, the skyline ring and the game's sky, all coloured on the CPU
    /// with the same models the shaders use (<see cref="HorizonLook"/> for the ring, a toon-lit, fogged copy of the
    /// terrain colours, the SkyGradient formula for the dome), written as OBJs relative to the eye with the earth
    /// curvature applied, ready for <c>tools/mesh-preview/render.py --shading unlit</c>. Gated by GHUMANTE_PREVIEW_DIR;
    /// <c>render.sh</c> in the output folder holds the render commands.
    /// </summary>
    [TestFixture]
    public class HorizonPreviews
    {
        private const double Curv = HorizonPanorama.InverseTwoEffectiveRadius;

        private struct Shot
        {
            public string Name;
            public HorizonViewpoint View;
            public float EyeAboveGround;
            public double Hours;
            public double BearingDeg, PitchDeg, FovDeg;
            public int Width, Height;
        }

        [Test]
        public void DumpSkylinePanoramas()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set " + ObjDump.EnvVar + " to dump the skyline previews");
            string tierList = Environment.GetEnvironmentVariable("GHUMANTE_SKY_TIERS") ?? "2";
            string only = Environment.GetEnvironmentVariable("GHUMANTE_SKY_SHOTS");
            var shots = new List<Shot>
            {
                // Game-like views (62 degrees across in landscape) and telephoto views like the reference photos.
                new Shot { Name = "basantapur_north", View = HorizonViewpoint.Basantapur, EyeAboveGround = 25f, Hours = 9.0, BearingDeg = 2, PitchDeg = 2.5, FovDeg = 37, Width = 1600, Height = 900 },
                new Shot { Name = "basantapur_ganesh_tele", View = HorizonViewpoint.Basantapur, EyeAboveGround = 25f, Hours = 9.0, BearingDeg = 345, PitchDeg = 3.6, FovDeg = 4.5, Width = 1600, Height = 700 },
                new Shot { Name = "boudha_north", View = HorizonViewpoint.Boudha, EyeAboveGround = 20f, Hours = 9.0, BearingDeg = 15, PitchDeg = 2.5, FovDeg = 37, Width = 1600, Height = 900 },
                new Shot { Name = "boudha_east_tele", View = HorizonViewpoint.Boudha, EyeAboveGround = 20f, Hours = 9.0, BearingDeg = 62, PitchDeg = 3.0, FovDeg = 6, Width = 1600, Height = 700 },
                new Shot { Name = "swayambhu_north", View = HorizonViewpoint.Swayambhu, EyeAboveGround = 3f, Hours = 9.0, BearingDeg = 25, PitchDeg = 2.5, FovDeg = 37, Width = 1600, Height = 900 },
                new Shot { Name = "swayambhu_langtang_tele", View = HorizonViewpoint.Swayambhu, EyeAboveGround = 3f, Hours = 9.0, BearingDeg = 22, PitchDeg = 4.3, FovDeg = 4.5, Width = 1600, Height = 700 },
                new Shot { Name = "swayambhu_golden", View = HorizonViewpoint.Swayambhu, EyeAboveGround = 28f, Hours = 17.3, BearingDeg = 40, PitchDeg = 1.5, FovDeg = 37, Width = 1600, Height = 900 },
                // Ganesh Himal from the top of Chandragiri (ref: "Ganesh Mountain Range seen from Chandragiri Hill", Saroj Pandey).
                new Shot { Name = "chandragiri_ganesh_tele", View = new HorizonViewpoint("Chandragiri", 27.66625, 85.20525), EyeAboveGround = 5f, Hours = 10.0, BearingDeg = 349, PitchDeg = 1.6, FovDeg = 4.0, Width = 1600, Height = 1066 },
                new Shot { Name = "basantapur_sunrise", View = HorizonViewpoint.Basantapur, EyeAboveGround = 25f, Hours = 6.0, BearingDeg = 2, PitchDeg = 2.5, FovDeg = 37, Width = 1600, Height = 900 },
            };
            var sh = new System.Text.StringBuilder();
            sh.Append("#!/bin/bash\nR=${R:-tools/mesh-preview/render.py}\nD=$(dirname \"$0\")/..\nmkdir -p $D/png\n");
            foreach (string ts in tierList.Split(','))
            {
                int tier = int.Parse(ts, CultureInfo.InvariantCulture);
                foreach (Shot s in shots)
                {
                    if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), s.Name) < 0) continue;
                    var view = new HorizonViewpoint(s.View.Name, s.View.Lat, s.View.Lon, s.EyeAboveGround);
                    var scene = new HorizonScene(view, tier, s.Hours);
                    SkyState sky = SkyPalette.Evaluate(s.Hours);
                    HorizonLight light = LightFor(sky, scene.FogDensity);
                    string dir = "sky/" + s.Name + "_t" + tier;
                    double halfAcross = Math.Atan(Math.Tan(s.FovDeg * Math.PI / 360.0) * s.Width / s.Height) * 180.0 / Math.PI;
                    ObjDump.Write(Tessellate(scene, light, s.FovDeg < 10 ? 40 : 20, s.BearingDeg, halfAcross + 2.0, s.FovDeg < 10 ? 0.012 : 0.08,
                                                       s.FovDeg * Math.PI / 180.0 / s.Height), dir + "/ring.obj");
                    ObjDump.Write(Terrain(scene, sky, light, scene.Settings.HandoverM, s.BearingDeg, halfAcross + 4.0), dir + "/terrain.obj");
                    ObjDump.Write(Dome(sky), dir + "/sky.obj");
                    double b = s.BearingDeg * Math.PI / 180.0, p = s.PitchDeg * Math.PI / 180.0;
                    double lx = Math.Sin(b) * Math.Cos(p) * 10000, ly = Math.Sin(p) * 10000, lz = Math.Cos(b) * Math.Cos(p) * 10000;
                    sh.AppendFormat(CultureInfo.InvariantCulture,
                                    "python3 $R --combine $D/{0}/sky.obj $D/{0}/terrain.obj $D/{0}/ring.obj -o $D/png/{7}_t{8}.png --unity --eye 0,0,0 " +
                                    "--look={1:0.###},{2:0.###},{3:0.###} --fov {4} --size {5} --height {6} --shading unlit --no-outline --no-shadows " +
                                    "--no-grid --backfaces show --no-header -q\n", dir, lx, ly, lz, s.FovDeg, s.Width, s.Height, s.Name, tier);
                    TestContext.WriteLine(s.Name + " tier " + tier + ": ring " + scene.Ring.TriangleCount + " tris, fog " + scene.FogDensity.ToString("0.000e0"));
                }
            }
            string root = Path.Combine(ObjDump.Dir, "sky");
            Directory.CreateDirectory(Path.Combine(root, "..", "png"));
            File.WriteAllText(Path.Combine(root, "render.sh"), sh.ToString());
        }

        /// <summary>The light and air of a sky state (linear, as the shader receives them).</summary>
        internal static HorizonLight LightFor(SkyState s, double fogDensity)
        {
            var l = new HorizonLight();
            float lx = s.LightIsMoon ? s.MoonX : s.SunX, ly = s.LightIsMoon ? s.MoonY : s.SunY, lz = s.LightIsMoon ? s.MoonZ : s.SunZ;
            if (ly < 0.02f) ly = 0.02f; // as WorldSky turns the light
            float ll = (float)Math.Sqrt(lx * lx + ly * ly + lz * lz);
            l.LightX = lx / ll;
            l.LightY = ly / ll;
            l.LightZ = lz / ll;
            Lin(s.Light, s.LightIntensity, out l.LightR, out l.LightG, out l.LightB);
            SkyColor amb = SkyColor.Lerp(s.AmbientSky, s.AmbientEquator, 0.5f);
            Lin(amb, 1f, out l.AmbientR, out l.AmbientG, out l.AmbientB);
            Lin(s.Fog, 1f, out l.FogR, out l.FogG, out l.FogB);
            Lin(s.Horizon, 1f, out l.HorizonR, out l.HorizonG, out l.HorizonB);
            l.FogDensity = (float)fogDensity;
            l.AirDensity = s.FogDensity;
            return l;
        }

        private static void Lin(SkyColor c, float k, out float r, out float g, out float b)
        {
            r = (float)HorizonLook.ToLinear(c.R) * k;
            g = (float)HorizonLook.ToLinear(c.G) * k;
            b = (float)HorizonLook.ToLinear(c.B) * k;
        }

        private static uint Srgb(double r, double g, double b)
        {
            uint R = (uint)Math.Round(HorizonLook.ToSrgb(r) * 255), G = (uint)Math.Round(HorizonLook.ToSrgb(g) * 255), B = (uint)Math.Round(HorizonLook.ToSrgb(b) * 255);
            return R << 24 | G << 16 | B << 8 | 0xFF;
        }

        /// <summary>The ring's curtains cut into rows and coloured per vertex with <see cref="HorizonLook.Shade"/>
        /// (what the skyline shader does per pixel), relative to the eye with curvature applied;
        /// <paramref name="pixelRad"/> is the angle one pixel spans (the rib detail fades as the shader's does).</summary>
        internal static MeshData Tessellate(HorizonScene scene, HorizonLight light, int rows, double bearingDeg, double halfWindowDeg, double subDeg,
                                            double pixelRad)
        {
            MeshData ring = scene.Ring;
            var m = new MeshData();
            double ox = scene.Panorama.EyeX - scene.X, oz = scene.Panorama.EyeZ - scene.Z;
            for (int t = 0; t + 5 < ring.IndexCount; t += 6)
            {
                int pt = ring.Indices[t], top = ring.Indices[t + 1], bot = ring.Indices[t + 2], pb = ring.Indices[t + 5];
                double ba = Bearing(ring, pt, ox, oz), bb = Bearing(ring, top, ox, oz);
                double span = ((bb - ba) % 360.0 + 540.0) % 360.0 - 180.0;
                double off = ((ba + span * 0.5 - bearingDeg) % 360.0 + 540.0) % 360.0 - 180.0;
                if (Math.Abs(off) > halfWindowDeg + Math.Abs(span)) continue;
                int cols = Math.Max(1, Math.Min(400, (int)Math.Ceiling(Math.Abs(span) / subDeg)));
                int first = m.VertexCount;
                for (int c = 0; c <= cols; c++)
                {
                    double u = c / (double)cols;
                    for (int r = 0; r <= rows; r++)
                    {
                        double f = Math.Pow(r / (double)rows, 1.7);
                        // Bilinear over the curtain quad (top edge pt -> top, bottom edge pb -> bot).
                        double tx = Lerp(P(ring, pt, 0), P(ring, top, 0), u), ty = Lerp(P(ring, pt, 1), P(ring, top, 1), u), tz = Lerp(P(ring, pt, 2), P(ring, top, 2), u);
                        double bx = Lerp(P(ring, pb, 0), P(ring, bot, 0), u), by = Lerp(P(ring, pb, 1), P(ring, bot, 1), u), bz = Lerp(P(ring, pb, 2), P(ring, bot, 2), u);
                        double x = tx + (bx - tx) * f + ox, y = ty + (by - ty) * f, z = tz + (bz - tz) * f + oz;
                        double nx = Lerp(ring.Normals[pt * 3], ring.Normals[top * 3], u), ny = Lerp(ring.Normals[pt * 3 + 1], ring.Normals[top * 3 + 1], u),
                               nz = Lerp(ring.Normals[pt * 3 + 2], ring.Normals[top * 3 + 2], u);
                        double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        nx /= nl;
                        ny /= nl;
                        nz /= nl;
                        double d = Math.Sqrt(x * x + z * z), phi = Math.Atan2(x, z);
                        double along = Lerp(ring.Uv0[pt * 2], ring.Uv0[top * 2], u);
                        double rock = Lerp(ring.Colors[pt * 4], ring.Colors[top * 4], u) / 255.0;
                        double detail = HorizonLook.Detail(d * pixelRad);
                        double rx, ry, rz;
                        HorizonLook.RibNormal(nx, ny, nz, Math.Cos(phi), -Math.Sin(phi), along, y, detail, 0.9, out rx, out ry, out rz);
                        float cr, cg, cb;
                        HorizonLook.Shade(y, rock, along, detail, rx, ry, rz, d, scene.EyeM, light, out cr, out cg, out cb);
                        m.AddVertex((float)x, (float)(y - scene.EyeM - d * d * Curv), (float)z, (float)nx, (float)ny, (float)nz, Srgb(cr, cg, cb));
                    }
                }
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rows; r++)
                    {
                        int a0 = first + c * (rows + 1) + r, a1 = a0 + 1, b0 = a0 + rows + 1, b1 = b0 + 1;
                        m.AddTriangle(a0, b0, b1);
                        m.AddTriangle(a0, b1, a1);
                    }
            }
            return m;
        }

        private static double P(MeshData m, int v, int axis)
        {
            return m.Positions[v * 3 + axis];
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double Bearing(MeshData m, int v, double ox, double oz)
        {
            double b = Math.Atan2(m.Positions[v * 3] + ox, m.Positions[v * 3 + 2] + oz) * 180.0 / Math.PI;
            return b < 0 ? b + 360.0 : b;
        }

        /// <summary>The streamed terrain nearer than <paramref name="maxM"/>, toon-lit and fogged like ToonLit, relative
        /// to the eye with curvature applied.</summary>
        internal static MeshData Terrain(HorizonScene scene, SkyState sky, HorizonLight light, double maxM, double bearingDeg, double halfWindowDeg)
        {
            var m = new MeshData();
            var piece = new MeshData();
            foreach (SelectedNode n in scene.Nodes)
            {
                TileId a = n.Area;
                double cx = Math.Max(a.X0, Math.Min(scene.X, a.X0 + a.Size)), cz = Math.Max(a.Z0, Math.Min(scene.Z, a.Z0 + a.Size));
                if ((cx - scene.X) * (cx - scene.X) + (cz - scene.Z) * (cz - scene.Z) > maxM * maxM) continue;
                TileData src = StreamingSampleRegion.Tile(n.Source);
                piece.Clear();
                int step = scene.Config.TerrainStep(a, n.Source, src.HeightsN);
                TerrainMesher.Build(src, a, new TerrainOptions { Step = step, SkirtDepthM = 0f, Season = BiomePalette.DefaultSeason }, piece);
                int v0 = m.VertexCount;
                for (int v = 0; v < piece.VertexCount; v++)
                {
                    double x = a.X0 + piece.Positions[v * 3] - scene.X, y = piece.Positions[v * 3 + 1], z = a.Z0 + piece.Positions[v * 3 + 2] - scene.Z;
                    double d = Math.Sqrt(x * x + z * z);
                    double nx = piece.Normals[v * 3], ny = piece.Normals[v * 3 + 1], nz = piece.Normals[v * 3 + 2];
                    double ar = HorizonLook.ToLinear(piece.Colors[v * 4] / 255.0), ag = HorizonLook.ToLinear(piece.Colors[v * 4 + 1] / 255.0),
                           ab = HorizonLook.ToLinear(piece.Colors[v * 4 + 2] / 255.0);
                    double ndl = nx * light.LightX + ny * light.LightY + nz * light.LightZ;
                    double lit = ndl > 0.05 ? 1.0 : ndl > -0.05 ? (ndl + 0.05) / 0.1 : 0.0;
                    double cr = ar * (light.AmbientR + light.LightR * lit), cg = ag * (light.AmbientG + light.LightG * lit), cb = ab * (light.AmbientB + light.LightB * lit);
                    double tf = Math.Exp(-(scene.FogDensity * d) * (scene.FogDensity * d));
                    cr = light.FogR + (cr - light.FogR) * tf;
                    cg = light.FogG + (cg - light.FogG) * tf;
                    cb = light.FogB + (cb - light.FogB) * tf;
                    m.AddVertex((float)x, (float)(y - scene.EyeM - d * d * Curv), (float)z, (float)nx, (float)ny, (float)nz, Srgb(cr, cg, cb));
                }
                for (int i = 0; i + 2 < piece.IndexCount; i += 3)
                {
                    int p0 = piece.Indices[i], p1 = piece.Indices[i + 1], p2 = piece.Indices[i + 2];
                    double mx = (piece.Positions[p0 * 3] + piece.Positions[p1 * 3] + piece.Positions[p2 * 3]) / 3 + a.X0 - scene.X;
                    double mz = (piece.Positions[p0 * 3 + 2] + piece.Positions[p1 * 3 + 2] + piece.Positions[p2 * 3 + 2]) / 3 + a.Z0 - scene.Z;
                    if (mx * mx + mz * mz > maxM * maxM) continue;
                    double off = Math.Atan2(mx, mz) * 180.0 / Math.PI - bearingDeg;
                    off = ((off % 360.0) + 540.0) % 360.0 - 180.0;
                    if (Math.Abs(off) > halfWindowDeg && mx * mx + mz * mz > 250000.0) continue;
                    m.AddTriangle(v0 + p0, v0 + p1, v0 + p2);
                }
            }
            return Compact(m);
        }

        /// <summary>Drops vertices no triangle uses (keeps the OBJ small).</summary>
        private static MeshData Compact(MeshData m)
        {
            var map = new int[m.VertexCount];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            var o = new MeshData(Math.Max(16, m.IndexCount / 2), Math.Max(16, m.IndexCount));
            for (int i = 0; i < m.IndexCount; i++)
            {
                int v = m.Indices[i];
                if (map[v] < 0)
                {
                    uint c = (uint)m.Colors[v * 4] << 24 | (uint)m.Colors[v * 4 + 1] << 16 | (uint)m.Colors[v * 4 + 2] << 8 | m.Colors[v * 4 + 3];
                    map[v] = o.AddVertex(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2], m.Normals[v * 3], m.Normals[v * 3 + 1],
                                         m.Normals[v * 3 + 2], c);
                }
            }
            for (int i = 0; i + 2 < m.IndexCount; i += 3) o.AddTriangle(map[m.Indices[i]], map[m.Indices[i + 1]], map[m.Indices[i + 2]]);
            return o;
        }

        /// <summary>The game's sky (Ghumante/SkyGradient, its formula on the CPU) as a dome round the eye.</summary>
        internal static MeshData Dome(SkyState s)
        {
            var m = new MeshData();
            double[] el = { -12, -6, -3, -1.5, -0.5, 0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7, 8, 10, 12, 15, 18, 22, 27, 33, 40, 50, 62, 76, 89.9 };
            const int seg = 180;
            const double R = 400000.0;
            for (int j = 0; j < el.Length; j++)
            {
                double e = el[j] * Math.PI / 180.0;
                for (int i = 0; i <= seg; i++)
                {
                    double az = i * 2.0 * Math.PI / seg;
                    double dx = Math.Sin(az) * Math.Cos(e), dy = Math.Sin(e), dz = Math.Cos(az) * Math.Cos(e);
                    double cr, cg, cb;
                    SkyColour(s, dx, dy, dz, out cr, out cg, out cb);
                    m.AddVertex((float)(dx * R), (float)(dy * R), (float)(dz * R), (float)-dx, (float)-dy, (float)-dz, Srgb(cr, cg, cb));
                }
            }
            for (int j = 0; j + 1 < el.Length; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                    m.AddTriangle(a, c, d);
                    m.AddTriangle(a, d, b);
                }
            return m;
        }

        /// <summary>Linear colour of the game's sky (SkyGradient) in direction (dx, dy, dz).</summary>
        internal static void SkyColour(SkyState s, double dx, double dy, double dz, out double cr, out double cg, out double cb)
        {
            double zr, zg, zb, hr, hg, hb, gr, gg, gb, fr, fg, fb, sr, sg, sb;
            L(s.Zenith, out zr, out zg, out zb);
            L(s.Horizon, out hr, out hg, out hb);
            L(s.Ground, out gr, out gg, out gb);
            L(s.Fog, out fr, out fg, out fb);
            L(s.Light, out sr, out sg, out sb);
            double up = Math.Max(0, dy), down = Math.Max(0, -dy);
            double t = Math.Sqrt(up);
            cr = hr + (zr - hr) * t;
            cg = hg + (zg - hg) * t;
            cb = hb + (zb - hb) * t;
            double gt = Math.Min(1, down * 6);
            cr += (gr - cr) * gt;
            cg += (gg - cg) * gt;
            cb += (gb - cb) * gt;
            double fsx = Math.Sqrt(dx * dx + dz * dz) + 1e-9, ssx = Math.Sqrt(s.SunX * s.SunX + s.SunZ * s.SunZ) + 1e-9;
            double flat = (dx * s.SunX + dz * s.SunZ) / (fsx * ssx);
            double glow = Math.Pow(Math.Max(0, flat * 0.5 + 0.5), 6.0) * Math.Pow(1 - up, 4.0) * (1 - Math.Min(1, down * 8)) * s.Golden * 0.55;
            double sunDot = dx * s.SunX + dy * s.SunY + dz * s.SunZ;
            double halo = (Math.Pow(Math.Max(0, sunDot), 48) * 0.35 + Math.Pow(Math.Max(0, sunDot), 600) * 0.6) * s.SunVisible;
            cr += sr * (glow + halo);
            cg += sg * (glow + halo);
            cb += sb * (glow + halo);
            double band = 1 - SmoothStep(0, 0.08, dy);
            cr += (fr - cr) * band * 0.85;
            cg += (fg - cg) * band * 0.85;
            cb += (fb - cb) * band * 0.85;
        }

        private static void L(SkyColor c, out double r, out double g, out double b)
        {
            r = HorizonLook.ToLinear(c.R);
            g = HorizonLook.ToLinear(c.G);
            b = HorizonLook.ToLinear(c.B);
        }

        private static double SmoothStep(double e0, double e1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)));
            return t * t * (3 - 2 * t);
        }
    }
}
