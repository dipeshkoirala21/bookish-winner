using System;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// The per-tier draw budget of the nature kit (W2_DESIGN 10.4: the vegetation slice of 18 k / 54 k / 100 k
    /// triangles on Low / Mid / High), shared by the game's instanced renderer (World DressingRenderer, through
    /// DressingConfig) and the preview scenes, so a preview shows what the game draws. Trees draw at four levels: the
    /// species' detailed model (LOD0) and simple model (LOD1) near the camera, then the family volume (LOD2) out to
    /// <see cref="TreeVolumeM"/> and the family impostor (LOD3) out to <see cref="TreeFarM"/>. Plants draw at LOD0 to
    /// <see cref="PlantLod0M"/> and LOD1 to <see cref="PlantM"/>, the large kinds (<see cref="FloraCatalog.DrawsFar"/>:
    /// straw stacks, boulders, hedges, flower beds, bougainvillea...) at LOD1 on to <see cref="PlantFarM"/>. Every
    /// level is filled nearest first under its instance cap and its triangle budget and what overflows steps down a
    /// level (<see cref="FloraLodPlan"/>); the volume budget is large enough that a forest inside the LOD1 radius
    /// spills to volumes, not impostors. Selection only counts what the camera can see (<see cref="FloraView"/>),
    /// so the budgets go to the trees in front. Engine-free.
    /// </summary>
    public sealed class FloraBudget
    {
        /// <summary>Tree level radii (metres): LOD0, LOD1, family volume, impostor (the vegetation radius).</summary>
        public float TreeLod0M, TreeLod1M, TreeVolumeM, TreeFarM;

        /// <summary>Instance caps per tree level; <see cref="TreeFarCap"/> counts volumes and impostors together.</summary>
        public int TreeLod0Cap, TreeLod1Cap, TreeVolumeCap, TreeFarCap;

        /// <summary>Triangle budgets per tree level.</summary>
        public int TreeLod0Tris, TreeLod1Tris, TreeVolumeTris, TreeImpostorTris;

        /// <summary>Plant radii: LOD0, LOD1 (every kind) and the far radius of the large kinds (LOD1 only).</summary>
        public float PlantLod0M, PlantM, PlantFarM;

        /// <summary>Plant instance cap and triangle budget (both levels, near and far).</summary>
        public int PlantCap, PlantTris;

        /// <summary>Horizontal margin (degrees) added to each side of the camera's field of view when culling.</summary>
        public float ViewMarginDeg = 12f;

        /// <summary>Everything within this distance (plus its own radius) is drawn whatever the view direction
        /// (beside and just behind the camera, the ground below a pitched camera).</summary>
        public float ViewNearM = 18f;

        /// <summary>The vegetation triangle budget: the sum of the tree and plant budgets.</summary>
        public int VegetationTris
        {
            get { return TreeLod0Tris + TreeLod1Tris + TreeVolumeTris + TreeImpostorTris + PlantTris; }
        }

        /// <summary>
        /// The budget of a device tier (0 Low, 1 Mid, 2 High; clamped). Sums: 18 000 / 54 000 / 100 000 triangles.
        /// At these numbers a dense rim forest (about 80 canopy trees per hectare) seen from a hill road is drawn as
        /// detailed and simple trees to the LOD1 radius, family volumes on to ~150 / 190 / 240 m and impostors beyond
        /// (in the view cone of a landscape camera).
        /// </summary>
        public static FloraBudget ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new FloraBudget
                    {
                        TreeLod0M = 22f, TreeLod0Cap = 3, TreeLod0Tris = 4200,
                        TreeLod1M = 90f, TreeLod1Cap = 14, TreeLod1Tris = 2800,
                        TreeVolumeM = 220f, TreeVolumeCap = 45, TreeVolumeTris = 4000,
                        TreeFarM = 750f, TreeFarCap = 260, TreeImpostorTris = 3600,
                        PlantLod0M = 10f, PlantM = 18f, PlantFarM = 45f, PlantCap = 80, PlantTris = 3400,
                    };
                case 1:
                    return new FloraBudget
                    {
                        TreeLod0M = 35f, TreeLod0Cap = 10, TreeLod0Tris = 14000,
                        TreeLod1M = 120f, TreeLod1Cap = 50, TreeLod1Tris = 10000,
                        TreeVolumeM = 320f, TreeVolumeCap = 170, TreeVolumeTris = 15000,
                        TreeFarM = 1250f, TreeFarCap = 820, TreeImpostorTris = 7500,
                        PlantLod0M = 16f, PlantM = 30f, PlantFarM = 80f, PlantCap = 220, PlantTris = 7500,
                    };
                default:
                    return new FloraBudget
                    {
                        TreeLod0M = 50f, TreeLod0Cap = 20, TreeLod0Tris = 27000,
                        TreeLod1M = 180f, TreeLod1Cap = 100, TreeLod1Tris = 20000,
                        TreeVolumeM = 450f, TreeVolumeCap = 330, TreeVolumeTris = 29100,
                        TreeFarM = 1750f, TreeFarCap = 1400, TreeImpostorTris = 11900,
                        PlantLod0M = 22f, PlantM = 45f, PlantFarM = 120f, PlantCap = 400, PlantTris = 12000,
                    };
            }
        }
    }

    /// <summary>Running counts of one frame's selection (<see cref="FloraLodPlan"/>); reset per frame.</summary>
    public struct FloraLodCounters
    {
        public int Lod0, Lod1, Volume, Impostor, Plants;
        public int Lod0Tris, Lod1Tris, VolumeTris, ImpostorTris, PlantTris;

        /// <summary>Volumes and impostors drawn so far (the far cap counts both).</summary>
        public int Far
        {
            get { return Volume + Impostor; }
        }
    }

    /// <summary>
    /// The level rules of the nature kit's renderer, applied to candidates in order of distance (nearest first):
    /// a tree takes LOD0 within <see cref="FloraBudget.TreeLod0M"/> while that level's cap and triangles last, else
    /// LOD1 within <see cref="FloraBudget.TreeLod1M"/>, else the family volume within
    /// <see cref="FloraBudget.TreeVolumeM"/> (while the far cap lasts), else the impostor within
    /// <see cref="FloraBudget.TreeFarM"/>, else it is not drawn. Plants take LOD0 near and LOD1 beyond. Feeding the
    /// candidates nearest first makes each level hold the nearest trees it can, and what overflows a level steps down
    /// (so a crowded forest never puts impostors in front of volumes). Allocation-free.
    /// </summary>
    public static class FloraLodPlan
    {
        /// <summary>The level returned for a candidate that is not drawn.</summary>
        public const int None = -1;

        /// <summary>The level of a tree at distance <paramref name="d"/> with the given per-level triangle counts
        /// (0-3, or <see cref="None"/>), counted into <paramref name="c"/>.</summary>
        public static int PickTree(FloraBudget b, ref FloraLodCounters c, float d, int lod0Tris, int lod1Tris, int volumeTris, int impostorTris)
        {
            if (d <= b.TreeLod0M && c.Lod0 < b.TreeLod0Cap && c.Lod0Tris + lod0Tris <= b.TreeLod0Tris)
            {
                c.Lod0++;
                c.Lod0Tris += lod0Tris;
                return 0;
            }
            if (d <= b.TreeLod1M && c.Lod1 < b.TreeLod1Cap && c.Lod1Tris + lod1Tris <= b.TreeLod1Tris)
            {
                c.Lod1++;
                c.Lod1Tris += lod1Tris;
                return 1;
            }
            if (c.Far >= b.TreeFarCap || d > b.TreeFarM) return None;
            if (d <= b.TreeVolumeM && c.Volume < b.TreeVolumeCap && c.VolumeTris + volumeTris <= b.TreeVolumeTris)
            {
                c.Volume++;
                c.VolumeTris += volumeTris;
                return 2;
            }
            if (c.ImpostorTris + impostorTris <= b.TreeImpostorTris)
            {
                c.Impostor++;
                c.ImpostorTris += impostorTris;
                return 3;
            }
            return None;
        }

        /// <summary>The level of a plant at distance <paramref name="d"/> (0, 1 or <see cref="None"/>):
        /// <paramref name="far"/> kinds reach <see cref="FloraBudget.PlantFarM"/>, the rest <see cref="FloraBudget.PlantM"/>.</summary>
        public static int PickPlant(FloraBudget b, ref FloraLodCounters c, float d, bool far, int lod0Tris, int lod1Tris)
        {
            if (d > (far ? b.PlantFarM : b.PlantM) || c.Plants >= b.PlantCap) return None;
            if (d <= b.PlantLod0M && c.PlantTris + lod0Tris <= b.PlantTris)
            {
                c.Plants++;
                c.PlantTris += lod0Tris;
                return 0;
            }
            if (c.PlantTris + lod1Tris <= b.PlantTris)
            {
                c.Plants++;
                c.PlantTris += lod1Tris;
                return 1;
            }
            return None;
        }
    }

    /// <summary>
    /// The horizontal view cone the dressing is selected in: the camera position and forward direction on the ground
    /// plane and half the horizontal field of view plus a margin. Anything within <see cref="NearM"/> of the camera is
    /// always seen (beside or just behind it, and the ground under a camera pitched down); a camera looking almost
    /// straight down sees everything around it. <see cref="All"/> sees everything (no culling). Allocation-free.
    /// </summary>
    public readonly struct FloraView
    {
        public readonly double X, Z, Fx, Fz;
        public readonly double HalfRad, NearM;
        public readonly bool Directional;

        private FloraView(double x, double z, double fx, double fz, double halfRad, double nearM, bool directional)
        {
            X = x;
            Z = z;
            Fx = fx;
            Fz = fz;
            HalfRad = halfRad;
            NearM = nearM;
            Directional = directional;
        }

        /// <summary>No culling: everything around (x, z) is seen.</summary>
        public static FloraView All(double x, double z)
        {
            return new FloraView(x, z, 0, 1, Math.PI, 0, false);
        }

        /// <summary>
        /// A cone from (x, z) along the camera's forward vector (<paramref name="fx"/>, <paramref name="fy"/>,
        /// <paramref name="fz"/>), half angle = half the horizontal field of view <paramref name="hFovDeg"/> plus
        /// <paramref name="marginDeg"/>. A forward vector within 15° of vertical, or a half angle past 90°, sees all.
        /// </summary>
        public static FloraView Cone(double x, double z, double fx, double fy, double fz, double hFovDeg, double marginDeg, double nearM)
        {
            double flat = Math.Sqrt(fx * fx + fz * fz), len = Math.Sqrt(flat * flat + fy * fy);
            double half = (0.5 * hFovDeg + marginDeg) * Math.PI / 180.0;
            if (len < 1e-9 || flat < 0.26 * len || half >= 0.5 * Math.PI || double.IsNaN(half)) return All(x, z);
            return new FloraView(x, z, fx / flat, fz / flat, half, nearM, true);
        }

        /// <summary>The horizontal field of view (degrees) of a camera with vertical field of view
        /// <paramref name="vFovDeg"/> and aspect (width over height) <paramref name="aspect"/>.</summary>
        public static double HorizontalFov(double vFovDeg, double aspect)
        {
            return 2.0 * Math.Atan(Math.Tan(0.5 * vFovDeg * Math.PI / 180.0) * Math.Max(0.05, aspect)) * 180.0 / Math.PI;
        }

        /// <summary>True when a disc of <paramref name="radius"/> at (x, z) may be in view.</summary>
        public bool Sees(double x, double z, double radius)
        {
            if (!Directional) return true;
            double dx = x - X, dz = z - Z, d2 = dx * dx + dz * dz, near = NearM + radius;
            if (d2 <= near * near) return true;
            double d = Math.Sqrt(d2);
            double cos = (dx * Fx + dz * Fz) / d;
            cos = cos < -1 ? -1 : cos > 1 ? 1 : cos;
            double off = Math.Acos(cos) - Math.Asin(Math.Min(1.0, radius / d));
            return off <= HalfRad;
        }
    }
}
