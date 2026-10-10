using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Rigid rounded details of the fauna built with the shared <c>Meshing.Shapes</c> library and adopted into the
    /// skinned mesh (<see cref="FaunaBuilder.Adopt"/>): cloven hooves (oval frusta with rounded rims), tapered curved
    /// horns (smoothed tubes), and the rope collars with a brass bell of the owned village goats and buffalo. Coordinates are species units
    /// scaled by the sketch's <see cref="FaunaSketch.S"/>.
    /// </summary>
    internal static class FaunaShapes
    {
        /// <summary>
        /// A hoof standing on the ground under (<paramref name="x"/>, <paramref name="z"/>): two claws (one on the far
        /// level) each an oval frustum wider at the sole, with a soft rim, tipped forward a little.
        /// </summary>
        public static void Hoof(FaunaSketch c, float x, float z, float width, float height, FaunaBone bone, uint colour)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            var lod = new ShapeLod(d.Lod);
            var brush = new ShapeBrush(colour, FaunaPalette.KeratinChannel);
            int claws = d.Coarse ? 1 : 2;
            for (int k = 0; k < claws; k++)
            {
                float ox = claws == 1 ? 0f : (k == 0 ? -0.5f : 0.5f) * width * 0.95f;
                float w = claws == 1 ? width : width * 0.52f;
                double s = c.S;
                Affine3 xf = Affine3.Translation((x + ox) * s, 0, (z + 0.15f * width) * s) * Affine3.Scaling(1, 1, 1.4) * Affine3.RotationX(0.12);
                int v0 = b.VertexCount;
                Shapes.Frustum(b.Target.Mesh, xf, brush, w * s, w * 0.72f * s, height * s, Math.Max(4, d.SmallSegs), 0.22 * w * s, 1, true, true, default);
                b.Adopt(v0, bone, FaunaPart.Hoof);
            }
        }

        /// <summary>
        /// A horn through <paramref name="count"/> control points (species units, base first): a Catmull-Rom-smoothed
        /// tube tapering from <paramref name="rBase"/> to <paramref name="rTip"/>, coloured from
        /// <paramref name="baseColour"/> to <paramref name="tipColour"/> along its length, with fine growth rings on
        /// the near level.
        /// </summary>
        public static void Horn(FaunaSketch c, float[] pts, int count, float rBase, float rTip, uint baseColour, uint tipColour, FaunaBone bone,
                                float rings = 0f)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            var lod = new ShapeLod(d.Lod);
            Path3 ctrl = c.Ctrl.Clear();
            int step = d.Far && count > 3 ? count - 1 : d.Coarse && count > 3 ? 2 : 1;
            for (int i = 0; i < count; i += step) ctrl.Add(pts[3 * i] * c.S, pts[3 * i + 1] * c.S, pts[3 * i + 2] * c.S);
            if ((count - 1) % step != 0) ctrl.Add(pts[3 * (count - 1)] * c.S, pts[3 * (count - 1) + 1] * c.S, pts[3 * (count - 1) + 2] * c.S);
            Path3 path = Curves.CatmullRom(ctrl, c.Path.Clear(), d.Fine ? 2 : 1);
            int v0 = b.VertexCount;
            Shapes.Tube(b.Target.Mesh, Affine3.Identity, new ShapeBrush(baseColour, FaunaPalette.KeratinChannel), path, rBase * c.S,
                        Math.Max(4, d.LimbSegs - 2), true, false, default, rTip * c.S);
            b.Adopt(v0, bone, FaunaPart.Horn);
            // Colour by distance from the base, with rings.
            var basePt = new Fv3((float)ctrl.X[0], (float)ctrl.Y[0], (float)ctrl.Z[0]);
            float len = 0f;
            for (int i = 1; i < path.Count; i++)
                len += (float)Math.Sqrt(Sq(path.X[i] - path.X[i - 1]) + Sq(path.Y[i] - path.Y[i - 1]) + Sq(path.Z[i] - path.Z[i - 1]));
            len = Math.Max(1e-4f, len);
            for (int v = v0; v < b.VertexCount; v++)
            {
                float t = FMath.Clamp01(Fv3.Distance(b.PositionOf(v), basePt) / len);
                uint col = FMath.LerpColour(baseColour, tipColour, FMath.SmoothStep(0.25f, 1f, t));
                if (rings > 0f && d.Fine && t < 0.6f)
                {
                    float r = 0.5f + 0.5f * FMath.Sin(t * len / c.S * rings);
                    col = FMath.Shade(col, 0.9f + 0.1f * r);
                }
                b.SetColour(v, col);
            }
        }

        /// <summary>
        /// A rope collar round the neck with a small brass bell hanging under the throat (owned village cattle and
        /// goats): a torus round the neck circle (centre, radius, tilt) and a lathed bell.
        /// </summary>
        public static void CollarBell(FaunaSketch c, Fv3 centre, float radius, float tiltDeg, float bellSize, FaunaBone bone)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            if (d.Far) return;
            var lod = new ShapeLod(d.Lod);
            double s = c.S;
            Affine3 ring = Affine3.Translation(centre.X * s, centre.Y * s, centre.Z * s) * Affine3.RotationX((90 - tiltDeg) * Math.PI / 180);
            int v0 = b.VertexCount;
            Shapes.Torus(b.Target.Mesh, ring, new ShapeBrush(0x8A6A4200u, MaterialChannel.Fabric), radius * s, 0.012 * s, d.BodySegs,
                         3, default);
            b.Adopt(v0, bone, FaunaPart.Collar);
            // Bell: a lathe profile (flared mouth, rounded crown) hanging from the bottom of the collar.
            float a = tiltDeg * FMath.Deg;
            var down = new Fv3(0f, -(float)Math.Cos(a), (float)Math.Sin(a));
            Fv3 top = centre + down * radius;
            Profile2 p = c.Prof.Clear(false);
            float h = bellSize;
            p.Add(0, 0, true).Add(0.5 * h, 0.02 * h).Add(0.45 * h, 0.15 * h).Add(0.33 * h, 0.55 * h).Add(0.25 * h, 0.85 * h).Add(0.12 * h, h).Add(0, 1.04 * h, true);
            Affine3 bell = Affine3.Translation(top.X * s, (top.Y - 1.05f * h) * s, top.Z * s) * Affine3.Scaling(s);
            v0 = b.VertexCount;
            Shapes.Lathe(b.Target.Mesh, bell, new ShapeBrush(0xC8A04800u, MaterialChannel.Gilt), p, d.SmallSegs, default);
            b.Adopt(v0, bone, FaunaPart.Collar);
        }

        private static double Sq(double v)
        {
            return v * v;
        }
    }
}
