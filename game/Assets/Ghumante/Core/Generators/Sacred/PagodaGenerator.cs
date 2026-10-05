using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// Parameters of a Newar pagoda (W2_DESIGN 3.2, temples.md 2.1). Defaults from <see cref="Defaults"/>. Optional
    /// arrays override the derived proportions for heroes measured in OSM parts (Nyatapola's plinth widths, eave
    /// widths and heights). <see cref="OutlineX"/>/<see cref="OutlineZ"/> (tile-local metres, counter-clockwise) give the
    /// exact bottom plinth outline (V4: plan within 0.1 m of OSM).
    /// </summary>
    public struct PagodaParams
    {
        public float PlinthW, PlinthD;
        public int PlinthLevels;
        public float StepRiseM, StepInsetFrac;
        public float CoreFrac, CoreHeightFrac, FirstEaveFrac, TierShrink, TierSpacingFrac;
        public int Tiers;
        public float PitchBottomDeg, PitchTopDeg, CornerLift, GajurFrac, TotalHeightM;
        public RoofFinish Finish;
        public byte Doors;
        public bool Pataka;
        public GuardianSet Guardians;
        public float BellSpacingM;

        public float[] PlinthWidths, EaveWidths, EaveHeights;
        public double[] OutlineX, OutlineZ;

        /// <summary>Open mandapa: posts all round the ground floor and a small central shrine instead of sanctum walls.</summary>
        public bool Open;

        /// <summary>No stair (a pagoda on a palace block).</summary>
        public bool NoStair;

        /// <summary>Colour overrides (0 = finish default).</summary>
        public uint RoofColour, WallColour, PlinthColour;

        /// <summary>A Nandi (Shiva) or Garuda (Vishnu) facing the main door at this distance in front (0 = none).</summary>
        public float VahanaDistM;

        public bool VahanaGaruda;

        public static PagodaParams Defaults(float plinthW, float plinthD, int tiers)
        {
            return new PagodaParams
            {
                PlinthW = plinthW, PlinthD = plinthD, PlinthLevels = 1, StepRiseM = 0.45f, StepInsetFrac = 0.07f, CoreFrac = 0.45f,
                CoreHeightFrac = 0.5f, FirstEaveFrac = 1.9f, TierShrink = 0.74f, TierSpacingFrac = 0.23f, Tiers = tiers,
                PitchBottomDeg = 32f, PitchTopDeg = 40f, CornerLift = 0.05f, GajurFrac = 0.12f, Finish = RoofFinish.Tile, Doors = 1,
                Guardians = GuardianSet.Lions, BellSpacingM = 0.4f,
            };
        }
    }

    /// <summary>
    /// The pagoda generator (W2_DESIGN 3.2; temples.md 6.1): stepped plinth with its real stair and guardians, the
    /// sanctum (0.45 × plinth, closed door with a lamp glow, never an interior), N tiers whose eaves shrink by the tier
    /// ratio on struts (<c>max(2, round(wall / 1.1)) + 1</c> per side plus corner struts), bells every 0.4 m along each
    /// eave, a straight eave line with a small corner lift, the gilt gajur, optional pataka, and the finish (tile, gilt
    /// top, gilt all). The total height is hit exactly when given (heroes ±3%, V4). LOD 0-3 thin the detail: LOD1 drops
    /// to one strut per side and bell strips, LOD2 keeps plain roofs, LOD3 is a box with pyramid roofs.
    /// </summary>
    public static class PagodaGenerator
    {
        [ThreadStatic] private static double[] _ax, _ay, _az, _bx, _by, _bz;

        private static void Scratch()
        {
            if (_ax != null) return;
            _ax = new double[8];
            _ay = new double[8];
            _az = new double[8];
            _bx = new double[8];
            _by = new double[8];
            _bz = new double[8];
        }

        /// <summary>Struts per side under a wall (temples.md 2.1), corner struts excluded.</summary>
        public static int StrutsPerSide(double wallM)
        {
            return Math.Max(2, (int)Math.Round(wallM / 1.1)) + 1;
        }

        public static int Build(in PagodaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in PagodaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            Scratch();
            int t0 = m.TriangleCount;
            KitFrame k = f.Kit;
            int tiers = Math.Max(1, p.Tiers), levels = Math.Max(0, p.PlinthLevels);
            double w0 = p.PlinthW, d0 = p.PlinthD > 0 ? p.PlinthD : p.PlinthW;
            double aspect = d0 / w0;
            double coreW = Math.Max(1.0, p.CoreFrac * w0), coreD = coreW * aspect;
            double rise = levels > 0 ? p.StepRiseM : 0;
            double plinthTop = levels * rise;

            // Plinth widths: explicit, or an even inset that keeps the top level wider than the sanctum.
            var pw = new double[Math.Max(1, levels)];
            var pd = new double[Math.Max(1, levels)];
            double inset = levels > 1 ? Math.Min(p.StepInsetFrac * w0, Math.Max(0, (w0 - (coreW + 1.2)) / (2.0 * (levels - 1)))) : 0;
            for (int i = 0; i < levels; i++)
            {
                pw[i] = p.PlinthWidths != null && i < p.PlinthWidths.Length ? p.PlinthWidths[i] : w0 - 2 * i * inset;
                pd[i] = pw[i] * aspect;
            }

            // Eaves and heights.
            var ew = new double[tiers];
            var sw = new double[tiers];
            var eh = new double[tiers];
            var rr = new double[tiers];
            double e0 = p.EaveWidths != null && p.EaveWidths.Length > 0 ? p.EaveWidths[0] : p.FirstEaveFrac * coreW;
            ew[0] = e0;
            for (int i = 1; i < tiers; i++)
            {
                double shrink = i == tiers - 1 && tiers >= 3 ? p.TierShrink - 0.08 : p.TierShrink;
                ew[i] = p.EaveWidths != null && i < p.EaveWidths.Length ? p.EaveWidths[i] : ew[i - 1] * shrink;
            }
            sw[0] = coreW;
            for (int i = 1; i < tiers; i++) sw[i] = ew[i] * coreW / e0;
            double coreH = Math.Max(2.0, p.CoreHeightFrac * coreW), spacing = p.TierSpacingFrac * e0;
            for (int i = 0; i < tiers; i++)
            {
                double pitch = (tiers == 1 ? p.PitchTopDeg : p.PitchBottomDeg + (p.PitchTopDeg - p.PitchBottomDeg) * i / (tiers - 1)) * Math.PI / 180;
                rr[i] = i < tiers - 1 ? Math.Tan(pitch) * 0.5 * (ew[i] - sw[i + 1]) : Math.Tan(pitch) * 0.5 * ew[i];
                if (i < tiers - 1) rr[i] = Math.Min(rr[i], 0.8 * spacing);
            }
            bool explicitH = p.EaveHeights != null && p.EaveHeights.Length >= tiers;
            double gajur;
            if (explicitH)
            {
                for (int i = 0; i < tiers; i++) eh[i] = p.EaveHeights[i];
                double total = p.TotalHeightM > 0 ? p.TotalHeightM : eh[tiers - 1] + rr[tiers - 1] + p.GajurFrac * (eh[tiers - 1] + rr[tiers - 1]);
                gajur = p.GajurFrac * total;
                rr[tiers - 1] = Math.Max(0.5, total - gajur - eh[tiers - 1]);
                for (int i = 0; i + 1 < tiers; i++) rr[i] = Math.Min(rr[i], 0.85 * (eh[i + 1] - eh[i]));
                coreH = eh[0] - plinthTop;
            }
            else
            {
                double derived = plinthTop + coreH + (tiers - 1) * spacing + rr[tiers - 1];
                double total = p.TotalHeightM > 0 ? p.TotalHeightM : derived / (1 - p.GajurFrac);
                gajur = p.GajurFrac * total;
                double body = coreH + (tiers - 1) * spacing + rr[tiers - 1];
                double s = Math.Max(0.3, (total - gajur - plinthTop) / body);
                coreH *= s;
                spacing *= s;
                for (int i = 0; i < tiers; i++) rr[i] *= s;
                for (int i = 0; i < tiers; i++) eh[i] = plinthTop + coreH + i * spacing;
                rr[tiers - 1] = total - gajur - eh[tiers - 1];
            }
            double apex = eh[tiers - 1] + rr[tiers - 1], top = apex + gajur;

            uint plinthCol = p.PlinthColour != 0 ? p.PlinthColour : SacredPalette.BrickPlinth;
            uint wallCol = p.WallColour != 0 ? p.WallColour : SacredPalette.BrickDachi;
            uint tile = p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile;

            if (lod >= 3)
            {
                // Box and pyramid roofs (≤ 200 triangles).
                if (levels > 0) MeshKit.Box(m, k, -0.5 * w0, 0.5 * w0, 0, plinthTop, -0.5 * d0, 0.5 * d0, plinthCol, BoxFaces.All & ~BoxFaces.Bottom);
                MeshKit.Box(m, k, -0.5 * coreW, 0.5 * coreW, plinthTop, eh[0], -0.5 * coreD, 0.5 * coreD, wallCol, BoxFaces.All & ~BoxFaces.Bottom);
                for (int i = 0; i < tiers; i++)
                {
                    double nextW = i < tiers - 1 ? sw[i + 1] : 0;
                    SacredKit.Ring8(k, 0.5 * ew[i], 0.5 * ew[i] * aspect, eh[i], 0, _ax, _ay, _az);
                    double cx, cy, cz;
                    k.ToWorld(0, 0, 0, out cx, out cy, out cz);
                    MeshKit.Loft(m, _ax, _az, 8, cx, cz, eh[i] + f.GroundY, 1.0, eh[i] + rr[i] + f.GroundY, nextW / ew[i], RoofCol(p, i, tiers, tile));
                }
                Fill(stats, p, tiers, levels, plinthTop, top, f, coreW, coreD, eh[0], 0, 0);
                return m.TriangleCount - t0;
            }

            // 1. Plinth levels.
            for (int i = 0; i < levels; i++)
            {
                double v0 = i * rise, v1 = (i + 1) * rise;
                if (i == 0 && p.OutlineX != null && p.OutlineX.Length >= 3)
                {
                    MeshKit.RingWalls(m, p.OutlineX, p.OutlineZ, p.OutlineX.Length, f.GroundY - 0.5, f.GroundY + v1, plinthCol);
                    if (Polygon.IsConvex(p.OutlineX, p.OutlineZ, p.OutlineX.Length, 0.0))
                        MeshKit.ConvexCap(m, p.OutlineX, p.OutlineZ, p.OutlineX.Length, f.GroundY + v1, true, plinthCol);
                    else
                        MeshKit.Box(m, k, -0.5 * pw[0], 0.5 * pw[0], v1 - 0.01, v1, -0.5 * pd[0], 0.5 * pd[0], plinthCol, BoxFaces.Top);
                }
                else
                {
                    MeshKit.Box(m, k, -0.5 * pw[i], 0.5 * pw[i], i == 0 ? -0.5 : v0, v1, -0.5 * pd[i], 0.5 * pd[i], plinthCol, BoxFaces.All & ~BoxFaces.Bottom);
                }
                if (c != null)
                {
                    double x, y, z;
                    k.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + v0 - (i == 0 ? 0.5 : 0), f.GroundY + v1, 0.5 * pw[i], 0.5 * pd[i], k.UX, k.UZ, GenColliderFlags.Walkable, GenColliders.Brick);
                }
            }

            // 2. The real stair on the door side, flight by flight, with the guardians.
            int steps = 0;
            if (levels > 0 && !p.NoStair)
            {
                double stairW = Math.Min(6.0, Math.Max(1.2, 0.3 * w0));
                for (int i = 0; i < levels; i++)
                {
                    double face = 0.5 * pd[i];
                    double run = i == 0 ? rise / Math.Tan(40 * Math.PI / 180) : Math.Max(0.4, 0.5 * (pd[i - 1] - pd[i]));
                    steps += SacredKit.Stair(m, c, k, -0.5 * stairW, 0.5 * stairW, face, run, i * rise, (i + 1) * rise, lod, plinthCol, GenColliders.Stone);
                    int kind = GuardianKind(p.Guardians, i);
                    if (kind < 0) continue;
                    double gh = Math.Min(2.0, Math.Max(0.9, 0.06 * w0)) * (p.Guardians == GuardianSet.Nyatapola ? 1.0 - 0.06 * i : 1.0);
                    double gw = face + run - 0.35;
                    SacredKit.Guardian(m, k, -0.5 * stairW - 0.45, gw, i * rise, gh, kind, lod);
                    SacredKit.Guardian(m, k, 0.5 * stairW + 0.45, gw, i * rise, gh, kind, lod);
                }
            }
            else if (levels == 0 && p.Guardians != GuardianSet.None)
            {
                SacredKit.Guardian(m, k, -0.5 * coreW, 0.5 * coreD + 1.0, 0, 1.0, GuardianKind(p.Guardians, 0), lod);
                SacredKit.Guardian(m, k, 0.5 * coreW, 0.5 * coreD + 1.0, 0, 1.0, GuardianKind(p.Guardians, 0), lod);
            }

            // 3. The sanctum (or the open mandapa).
            if (p.Open)
            {
                int posts = Math.Max(3, (int)Math.Round(coreW / 2.5));
                for (int a = 0; a <= posts; a++)
                {
                    double u = -0.5 * coreW + coreW * a / posts;
                    MeshKit.Box(m, k, u - 0.15, u + 0.15, plinthTop, eh[0], 0.5 * coreD - 0.3, 0.5 * coreD, SacredPalette.WoodCarved, BoxFaces.Wall);
                    MeshKit.Box(m, k, u - 0.15, u + 0.15, plinthTop, eh[0], -0.5 * coreD, -0.5 * coreD + 0.3, SacredPalette.WoodCarved, BoxFaces.Wall);
                    MeshKit.Box(m, k.TurnedRight(), u * aspect - 0.15, u * aspect + 0.15, plinthTop, eh[0], 0.5 * coreW - 0.3, 0.5 * coreW, SacredPalette.WoodCarved, BoxFaces.Wall);
                    MeshKit.Box(m, k.TurnedRight(), u * aspect - 0.15, u * aspect + 0.15, plinthTop, eh[0], -0.5 * coreW, -0.5 * coreW + 0.3, SacredPalette.WoodCarved, BoxFaces.Wall);
                }
                // A beam ring under the eave and the central shrine (closed).
                MeshKit.Box(m, k, -0.5 * coreW, 0.5 * coreW, eh[0] - 0.4, eh[0], -0.5 * coreD, 0.5 * coreD, SacredPalette.WoodCarved, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right | BoxFaces.Bottom);
                double sh = 0.25 * coreW, shrineTop = Math.Min(eh[0] - 0.4, plinthTop + 2.6);
                MeshKit.Box(m, k, -sh, sh, plinthTop, shrineTop, -sh, sh, wallCol, BoxFaces.All & ~BoxFaces.Bottom);
                SacredKit.SanctumDoor(m, k, 0, plinthTop, sh, Math.Min(1.0, sh), 1.9, SacredPalette.WoodCarved, lod);
                if (c != null)
                {
                    // The closed shrine is a sanctum (W2-O1: never entered): the open hall around it stays walkable.
                    double x, y, z;
                    k.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + plinthTop, f.GroundY + shrineTop, sh, sh, k.UX, k.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
                }
                // Fill takes full widths: the shrine box is 2·sh square.
                Fill(stats, p, tiers, levels, plinthTop, top, f, 2 * sh, 2 * sh, shrineTop, 0, steps);
            }
            else
            {
                MeshKit.Box(m, k, -0.5 * coreW, 0.5 * coreW, plinthTop, eh[0], -0.5 * coreD, 0.5 * coreD, wallCol, BoxFaces.All & ~BoxFaces.Bottom);
                double dw = Math.Min(1.4, Math.Max(0.9, 0.25 * coreW)), dh = Math.Min(2.1, 0.8 * coreH);
                int doors = p.Doors >= 4 ? 4 : 1;
                KitFrame side = k;
                for (int d = 0; d < doors; d++)
                {
                    double half = d % 2 == 0 ? 0.5 * coreD : 0.5 * coreW;
                    uint door = p.Doors >= 4 ? SacredPalette.Silver : SacredPalette.WoodCarved;
                    SacredKit.SanctumDoor(m, side, 0, plinthTop, half, dw, dh, door, lod);
                    SacredKit.Torana(m, side, 0, plinthTop + dh + 0.2, half + 0.09, dw + 0.6, SacredPalette.Gilt, lod);
                    side = side.TurnedRight();
                }
                if (c != null)
                {
                    double x, y, z;
                    k.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + plinthTop, f.GroundY + eh[0], 0.5 * coreW, 0.5 * coreD, k.UX, k.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
                }
                Fill(stats, p, tiers, levels, plinthTop, top, f, coreW, coreD, eh[0], 0, steps);
            }

            // 4. Tiers.
            int bells = 0, struts = 0;
            for (int i = 0; i < tiers; i++)
            {
                uint roof = RoofCol(p, i, tiers, tile);
                double halfE = 0.5 * ew[i], halfEd = halfE * aspect;
                double lift = Math.Min(0.08, Math.Max(0, p.CornerLift)) * halfE;
                SacredKit.Ring8(k, halfE, halfEd, f.GroundY + eh[i] - k.OY, lift, _ax, _ay, _az);
                double nextW = i < tiers - 1 ? sw[i + 1] : 0;
                if (i < tiers - 1) SacredKit.Ring8(k, 0.5 * nextW, 0.5 * nextW * aspect, f.GroundY + eh[i] + rr[i] - k.OY, 0, _bx, _by, _bz);
                double cx, cy, cz;
                k.ToWorld(0, 0, 0, out cx, out cy, out cz);
                for (int a = 0; a < 8; a++)
                {
                    int b = a + 1 == 8 ? 0 : a + 1;
                    double hx = 0.5 * (_ax[a] + _ax[b]) - cx, hz = 0.5 * (_az[a] + _az[b]) - cz;
                    if (i == tiers - 1)
                        MeshKit.Tri(m, _ax[a], _ay[a], _az[a], _ax[b], _ay[b], _az[b], cx, f.GroundY + apex, cz, hx, 1e-3, hz, roof);
                    else
                        MeshKit.Quad(m, _ax[a], _ay[a], _az[a], _ax[b], _ay[b], _az[b], _bx[b], _by[b], _bz[b], _bx[a], _by[a], _bz[a], hx, 1e-3, hz, roof);
                }
                // Soffit under the eave, out from the wall below.
                double wallHalf = 0.5 * sw[i];
                SacredKit.Ring8(k, wallHalf, wallHalf * aspect, f.GroundY + eh[i] - k.OY, 0, _bx, _by, _bz);
                for (int a = 0; a < 8; a++)
                {
                    int b = a + 1 == 8 ? 0 : a + 1;
                    MeshKit.Quad(m, _ax[a], _ay[a], _az[a], _ax[b], _ay[b], _az[b], _bx[b], _by[b], _bz[b], _bx[a], _by[a], _bz[a], 0, -1, 0, SacredPalette.WoodCarved);
                }
                // The storey wall under this tier (the sanctum carries tier 0).
                if (i > 0)
                {
                    double v0 = eh[i - 1] + rr[i - 1] - 0.2;
                    MeshKit.Box(m, k, -wallHalf, wallHalf, v0, eh[i], -wallHalf * aspect, wallHalf * aspect, i % 2 == 0 ? wallCol : SacredPalette.WoodCarved,
                                BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                }
                // Struts: per side by the formula plus four corner struts.
                double storeyH = i == 0 ? coreH : eh[i] - (eh[i - 1] + rr[i - 1]);
                int perSide = StrutsPerSide(sw[i]);
                struts += 4 * perSide + 4;
                if (lod <= 1)
                {
                    int drawPerSide = lod == 0 ? perSide : 1;
                    KitFrame sf = k;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        double half = sd % 2 == 0 ? wallHalf * aspect : wallHalf, alongHalf = sd % 2 == 0 ? wallHalf : wallHalf * aspect;
                        double eaveOut = sd % 2 == 0 ? halfEd : halfE;
                        for (int q = 0; q < drawPerSide; q++)
                        {
                            double u = drawPerSide == 1 ? 0 : -alongHalf + 2 * alongHalf * (q + 0.5) / drawPerSide;
                            double x0, y0, z0, x1, y1, z1;
                            sf.ToWorld(u, eh[i] - 0.5 * storeyH, half + 0.05, out x0, out y0, out z0);
                            sf.ToWorld(u * 1.1, eh[i] - 0.1, half + 0.8 * (eaveOut - half), out x1, out y1, out z1);
                            MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.14, SacredPalette.WoodCarved);
                        }
                        // Corner strut (the winged kunsala).
                        if (lod == 0)
                        {
                            double x0, y0, z0, x1, y1, z1;
                            sf.ToWorld(alongHalf, eh[i] - 0.5 * storeyH, half, out x0, out y0, out z0);
                            sf.ToWorld(alongHalf + 0.75 * (sd % 2 == 0 ? halfE - wallHalf : halfEd - wallHalf * aspect), eh[i] - 0.1,
                                       half + 0.75 * (eaveOut - half), out x1, out y1, out z1);
                            MeshKit.Bar(m, x0, y0, z0, x1, y1, z1, 0.18, SacredPalette.WoodRed);
                        }
                        sf = sf.TurnedRight();
                    }
                }
                // Bells every BellSpacingM along each eave.
                int perEave = Math.Max(1, (int)Math.Floor(ew[i] / Math.Max(0.1, p.BellSpacingM)));
                bells += 4 * perEave;
                if (lod == 0)
                {
                    KitFrame bf = k;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        double along = sd % 2 == 0 ? halfE : halfEd, out_ = sd % 2 == 0 ? halfEd : halfE;
                        for (int q = 0; q < perEave; q++)
                        {
                            double u = -along + 2 * along * (q + 0.5) / perEave;
                            MeshKit.TriLocal(m, bf, u - 0.07, eh[i] - 0.02, out_ - 0.05, u + 0.07, eh[i] - 0.02, out_ - 0.05, u, eh[i] - 0.28, out_ - 0.05, 0, 0, 1, SacredPalette.Gilt);
                        }
                        bf = bf.TurnedRight();
                    }
                }
                else if (lod == 1)
                {
                    KitFrame bf = k;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        double along = sd % 2 == 0 ? halfE : halfEd, out_ = sd % 2 == 0 ? halfEd : halfE;
                        MeshKit.Panel(m, bf, -along, eh[i] - 0.25, along, eh[i] - 0.02, out_ - 0.05, SacredPalette.GiltLo);
                        bf = bf.TurnedRight();
                    }
                }
            }

            // 5. Gajur and 6. pataka.
            double ax2, ay2, az2;
            k.ToWorld(0, 0, 0, out ax2, out ay2, out az2);
            SacredKit.Gajur(m, ax2, az2, f.GroundY + apex - 0.05, gajur + 0.05, Math.Max(0.25, 0.06 * ew[tiers - 1]), lod, SacredPalette.Gilt);
            if (p.Pataka && lod <= 1)
            {
                for (int i = tiers - 1; i >= 0; i--)
                {
                    double outer = 0.5 * ew[i] * aspect, inner = i < tiers - 1 ? 0.5 * sw[i + 1] * aspect : 0.15 * ew[i];
                    double vTop = i < tiers - 1 ? eh[i] + rr[i] : eh[i] + 0.7 * rr[i];
                    MeshKit.QuadLocal(m, k, -0.25, eh[i] + 0.06, outer + 0.02, 0.25, eh[i] + 0.06, outer + 0.02, 0.25, vTop + 0.06, inner, -0.25, vTop + 0.06, inner, 0, 1, 1, SacredPalette.GiltHi);
                    if (i > 0)
                    {
                        double wallFront = 0.5 * sw[i] * aspect + 0.03;
                        MeshKit.Panel(m, k, -0.25, eh[i - 1] + rr[i - 1], 0.25, eh[i], wallFront, SacredPalette.GiltHi);
                    }
                }
            }

            // 7. Vahana facing the door.
            if (p.VahanaDistM > 0 && lod <= 2)
            {
                double vw = 0.5 * (levels > 0 ? pd[0] : coreD) + p.VahanaDistM;
                double x, y, z;
                k.ToWorld(0, 0, vw, out x, out y, out z);
                MeshKit.Box(m, k, -0.6, 0.6, 0, 0.6, vw - 0.8, vw + 0.8, SacredPalette.StoneGrey, BoxFaces.All & ~BoxFaces.Bottom);
                uint col = p.VahanaGaruda ? SacredPalette.StoneGrey : SacredPalette.Gilt;
                MeshKit.Blob(m, x, f.GroundY + 1.2, z, 0.45, 0.55, 0.7, col);
                k.ToWorld(0, 0, vw - 0.6, out x, out y, out z);
                MeshKit.Blob(m, x, f.GroundY + 1.75, z, 0.25, 0.25, 0.25, col);
            }

            // Roofs and spire are not climbable.
            if (c != null)
            {
                double x, y, z;
                k.ToWorld(0, 0, 0, out x, out y, out z);
                c.AddBox(x, z, f.GroundY + eh[0], f.GroundY + top, 0.5 * ew[0], 0.5 * ew[0] * aspect, k.UX, k.UZ, GenColliderFlags.NoClimb, GenColliders.Wood);
            }
            if (stats != null)
            {
                stats.Bells = bells;
                stats.Struts = struts;
            }
            return m.TriangleCount - t0;
        }

        private static uint RoofCol(in PagodaParams p, int i, int tiers, uint tile)
        {
            if (p.Finish == RoofFinish.GiltAll || p.Finish == RoofFinish.GiltTop && i == tiers - 1) return SacredPalette.Gilt;
            return tile;
        }

        private static int GuardianKind(GuardianSet g, int level)
        {
            switch (g)
            {
                case GuardianSet.Lions: return level == 0 ? 0 : -1;
                case GuardianSet.Wrestlers: return level == 0 ? 1 : -1;
                case GuardianSet.Elephants: return level == 0 ? 2 : -1;
                case GuardianSet.Nyatapola: return level < 5 ? new[] { 1, 2, 0, 3, 4 }[level] : -1;
                default: return -1;
            }
        }

        private static void Fill(SacredStats s, in PagodaParams p, int tiers, int levels, double plinthTop, double top, in GenFrame f,
                                 double coreW, double coreD, double sanctumTop, int bells, int steps)
        {
            if (s == null) return;
            s.Tiers = tiers;
            s.PlinthLevels = levels;
            s.PlinthTopM = (float)plinthTop;
            s.TopM = (float)top;
            s.DoorYawDeg = f.YawDeg;
            s.SanctumHalfU = (float)(0.5 * coreW);
            s.SanctumHalfW = (float)(0.5 * coreD);
            s.SanctumV0 = (float)plinthTop;
            s.SanctumV1 = (float)sanctumTop;
            s.Bells = bells;
            s.Steps = steps;
        }
    }
}
