using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    internal static class KpTaleju
    {
        public const double PlinthTopM = 6.8;
        public static void Build(ref KpFrame f, MeshData m) { Kp.Box(m, f.Plan(45.1, 44.4, 0, 103), -10, 10, 0, 35, -10, 10, Kp.Brick); }
    }

    internal static class KpTowers
    {
        public const int BasantapurRoofs = 4;
        public static void Basantapur(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 8.2, 22.6, 0, 30, -72, -57.9, Kp.Brick); }
        public static void Kirtipur(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 20.5, 29.5, 0, 23, -42.6, -33.6, Kp.Brick); }
        public static void Bhaktapur(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 44, 54, 0, 19, -43, -33, Kp.Brick); }
        public static void Lalitpur(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 47, 58, 0, 21, -70, -59, Kp.Brick); }
    }

    internal static class KpGaddi
    {
        public static void Build(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, -74.3, -32.7, 0, 21, -80.6, -64.2, Kp.GaddiWhite); }
    }

    internal static class KpKumari
    {
        public static void Build(ref KpFrame f, MeshData m) { Kp.Box(m, f.Plan(-102.3, -84, 0, 100), -10.5, 10.5, 0, 14, -12.6, 12.6, Kp.BrickKumari); }
    }

    internal static class KpRanges
    {
        public static void Dakha(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, -38, 0.9, 0, 15, -64, -9.6, Kp.White); }
        public static void SouthWing(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, -33, 6.1, 0, 16.5, -75.7, -60.2, Kp.White); }
    }

    internal static class KpCourts
    {
        public static void Mohan(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, -3.5, 15.8, 0, 13, -1, 40.5, Kp.Brick); }
        public static void Nasal(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 0.9, 21, 0, 0.1, -56, -8.9, Kp.Flags); }
        public static void Mul(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 25, 50.9, 0, 9, -31.9, 1.1, Kp.Brick); }
        public static void Lohan(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, 21, 57.7, 0, 13, -70, -32.6, Kp.Brick); }
    }

    internal static class KpTemples
    {
        public static void DeguTaleju(ref KpFrame f, MeshData m) { Kp.Box(m, f.Grid, -9.5, 1.5, 0, 26, 12.6, 23.6, Kp.Brick); }
        public static void PanchMukhi(ref KpFrame f, MeshData m) { Shapes.Cylinder(m, f.GridFace(19.5, -3.5, 0, 0), Kp.White, 3.5, 25, 12); }
    }
}
