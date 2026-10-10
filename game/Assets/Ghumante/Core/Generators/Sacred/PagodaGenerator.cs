using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// Parameters of a Newar pagoda (W2_DESIGN 3.2, temples.md 2.1, ref_temples 1). Defaults from <see cref="Defaults"/>.
    /// Optional arrays override the derived proportions for heroes measured in OSM parts (Nyatapola's plinth widths, eave
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

        /// <summary>A ring of carved wooden posts round the sanctum under the first roof (Nyatapola, Maju Dega).</summary>
        public bool Ambulatory;

        /// <summary>Strut pitch along a wall (the struts-per-side formula uses it; 0 = 1.1 m).</summary>
        public float StrutPitchM;

        /// <summary>The red cloth valance under every eave (Kathmandu Durbar Square temples).</summary>
        public bool Fringe;

        /// <summary>The big bell-shaped gajur with four corner finials (Taleju, Kathmandu).</summary>
        public bool GajurBell;

        /// <summary>Whitewashed upper storeys with a wooden balcony gallery (Kasthamandap).</summary>
        public bool PlasterUpper, Balcony;

        /// <summary>Upper storey width as a fraction of its own eave (0 = the sanctum's proportion, ew × core / first eave).
        /// Kasthamandap's wide upper hall: 0.86.</summary>
        public float UpperWallFrac;

        /// <summary>A bronze bell on two posts beside the stair and this many lamp pillars in front.</summary>
        public bool FrontBell;

        public byte LampPillars;

        /// <summary>Hero detail (finer tile courses, richer guardians); <see cref="Rich"/>: the centrepiece detail of
        /// carved figure struts and leaf-clapper bells; lime-washed painted guardians; a whitewashed stair balustrade
        /// (Maju Dega).</summary>
        public bool Hero, Rich, PaintedGuardians, WhiteStair;

        public static PagodaParams Defaults(float plinthW, float plinthD, int tiers)
        {
            return new PagodaParams
            {
                PlinthW = plinthW, PlinthD = plinthD, PlinthLevels = 1, StepRiseM = 0.45f, StepInsetFrac = 0.07f, CoreFrac = 0.45f,
                CoreHeightFrac = 0.62f, FirstEaveFrac = 1.9f, TierShrink = 0.74f, TierSpacingFrac = 0.23f, Tiers = tiers,
                PitchBottomDeg = 32f, PitchTopDeg = 40f, CornerLift = 0.06f, GajurFrac = 0.12f, Finish = RoofFinish.Tile, Doors = 1,
                Guardians = GuardianSet.Lions, BellSpacingM = 0.4f, StrutPitchM = 1.1f, Ambulatory = tiers >= 2 && plinthW >= 11f,
            };
        }
    }

    /// <summary>
    /// The pagoda generator (W2_DESIGN 3.2; temples.md 6.1; ref_temples 1): a moulded brick plinth with its real stair,
    /// balustrade walls and guardian pairs on pedestals; the banded sanctum of glazed brick (0.45 × plinth) with carved
    /// door frames, gilt toranas and lattice windows (closed: an opaque dark doorway with a lamp glow, never an interior),
    /// an optional ring of carved posts; N tiers whose eaves shrink by the tier ratio, each a real roof with tile courses
    /// (or gilt standing seams), fascia, purlined soffit, hip rolls, upturned corner horns, an optional red fringe,
    /// carved struts (<c>max(2, round(wall / pitch)) + 1</c> per side plus winged corner struts) and bells every 0.4 m;
    /// upper storeys with carved window bands; the gilt gajur, the pataka, the vahana, bells and lamp pillars in front.
    /// Total heights are met exactly (heroes ±3%, V4). LOD1 thins struts and bells and drops courses, LOD2 keeps plain
    /// blocks and roofs, LOD3 is a box with pyramid roofs. Every vertex carries the material channel and baked AO.
    /// </summary>
    public static class PagodaGenerator
    {
        /// <summary>Struts per side under a wall (temples.md 2.1), corner struts excluded.</summary>
        public static int StrutsPerSide(double wallM)
        {
            return StrutsPerSide(wallM, 1.1);
        }

        /// <summary>Struts per side under a wall at a pitch, corner struts excluded.</summary>
        public static int StrutsPerSide(double wallM, double pitchM)
        {
            return Math.Max(2, (int)Math.Round(wallM / (pitchM > 0.2 ? pitchM : 1.1))) + 1;
        }

        public static int Build(in PagodaParams p, in GenFrame f, int lod, MeshData m, GenColliders c)
        {
            return Build(p, f, lod, m, c, null);
        }

        public static int Build(in PagodaParams p, in GenFrame f, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int t0 = m.TriangleCount, v0 = m.VertexCount, i0 = m.IndexCount;
            KitFrame kf = f.Kit;
            Affine3 k = SacredDraw.Xf(kf);
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

            // Eaves and heights (unchanged proportions: heights are held exactly).
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
            for (int i = 1; i < tiers; i++) sw[i] = p.UpperWallFrac > 0 ? p.UpperWallFrac * ew[i] : ew[i] * coreW / e0;
            double coreH = Math.Max(2.0, p.CoreHeightFrac * coreW), spacing = p.TierSpacingFrac * e0;
            for (int i = 0; i < tiers; i++)
            {
                double pitch = (tiers == 1 ? p.PitchTopDeg : p.PitchBottomDeg + (p.PitchTopDeg - p.PitchBottomDeg) * i / (tiers - 1)) * Math.PI / 180;
                rr[i] = i < tiers - 1 ? Math.Tan(pitch) * 0.5 * (ew[i] - sw[i + 1]) : Math.Tan(pitch) * 0.5 * ew[i];
                if (i < tiers - 1) rr[i] = Math.Min(rr[i], 0.5 * spacing);
            }
            bool explicitH = p.EaveHeights != null && p.EaveHeights.Length >= tiers;
            double gajur;
            if (explicitH)
            {
                for (int i = 0; i < tiers; i++) eh[i] = p.EaveHeights[i];
                double total = p.TotalHeightM > 0 ? p.TotalHeightM : eh[tiers - 1] + rr[tiers - 1] + p.GajurFrac * (eh[tiers - 1] + rr[tiers - 1]);
                gajur = p.GajurFrac * total;
                rr[tiers - 1] = Math.Max(0.5, total - gajur - eh[tiers - 1]);
                for (int i = 0; i + 1 < tiers; i++) rr[i] = Math.Min(rr[i], 0.5 * (eh[i + 1] - eh[i]));
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

            uint tileCol = p.RoofColour != 0 ? p.RoofColour : SacredPalette.Tile;
            ShapeBrush plinthB = p.PlinthColour != 0 ? Looks.Of(p.PlinthColour, MaterialChannel.Brick) : Looks.PlinthBrick;
            ShapeBrush copingB = Looks.Of(MeshColor.Scale(plinthB.Color, 1.12f), MaterialChannel.Brick);
            ShapeBrush wallB = p.WallColour != 0 ? Looks.Of(p.WallColour, MaterialChannel.BrickGlazed) : Looks.WallGlazed;

            if (lod >= 3)
            {
                Lod3(p, k, levels, plinthTop, w0, d0, coreW, coreD, ew, eh, rr, sw, aspect, tiers, apex, top, plinthB, wallB, tileCol, m);
                Fill(stats, tiers, levels, plinthTop, top, f, coreW, coreD, eh[0], 0, 0);
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                return m.TriangleCount - t0;
            }

            // 1. Plinth levels (the first on the exact OSM outline when there is one).
            for (int i = 0; i < levels; i++)
            {
                double lv0 = i * rise, lv1 = (i + 1) * rise;
                if (i == 0 && p.NoStair && rise > 3.0)
                {
                    PalaceBlock(m, k, 0.5 * pw[0], 0.5 * pd[0], rise, lod);
                }
                else if (i == 0 && p.OutlineX != null && p.OutlineX.Length >= 3)
                {
                    int n = p.OutlineX.Length;
                    double[] ou = new double[n], ow = new double[n];
                    for (int q = 0; q < n; q++) Local(kf, p.OutlineX[q], p.OutlineZ[q], out ou[q], out ow[q]);
                    SacredParts.PlinthLevel(m, k, ou, ow, n, lv0, lv1, 0.5, plinthB, copingB, lod);
                }
                else
                {
                    SacredParts.PlinthLevel(m, k, 0.5 * pw[i], 0.5 * pd[i], lv0, lv1, i == 0 ? 0.5 : 0.05, plinthB, copingB, lod);
                }
                if (c != null)
                {
                    double x, y, z;
                    kf.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + lv0 - (i == 0 ? 0.5 : 0), f.GroundY + lv1, 0.5 * pw[i], 0.5 * pd[i], kf.UX, kf.UZ, GenColliderFlags.Walkable, GenColliders.Brick);
                }
            }

            // 2. The real stair on the door side, flight by flight, with balustrade walls and guardians on pedestals.
            int steps = 0;
            if (levels > 0 && !p.NoStair)
            {
                bool monumental = levels >= 3 || rise >= 0.9;
                double stairW = monumental ? Math.Min(4.5, Math.Max(1.6, 0.16 * w0)) : Math.Min(6.0, Math.Max(1.2, 0.3 * w0));
                double sideW = monumental && lod <= 1 ? 0.55 : 0, sideH = 0.4;
                ShapeBrush sideB = p.WhiteStair ? Looks.Whitewash : p.PlinthColour != 0 ? copingB : Looks.StoneLight;
                for (int i = 0; i < levels; i++)
                {
                    double face = 0.5 * pd[i];
                    double run = i == 0 ? rise / Math.Tan(40 * Math.PI / 180) : Math.Max(0.4, 0.5 * (pd[i - 1] - pd[i]));
                    steps += SacredParts.Stair(m, c, kf, k, 0.5 * stairW, face, run, i * rise, (i + 1) * rise, sideW, sideH, Looks.Stone, sideB, lod, GenColliders.Stone);
                    int kind = GuardianKindAt(p.Guardians, i);
                    if (kind < 0) continue;
                    bool nyata = p.Guardians == GuardianSet.Nyatapola;
                    double gh = nyata ? Math.Min(2.8, 0.11 * w0) * (1.0 - 0.08 * i) : Math.Min(2.2, Math.Max(0.95, 0.075 * w0));
                    double ph = nyata ? 0.9 : Math.Min(0.8, Math.Max(0.4, 0.4 * gh));
                    double gu = 0.5 * stairW + (sideW > 0 ? 0.5 * sideW : 0.25 + 0.2 * gh);
                    double gw = face + run - (sideW > 0 ? 0.45 : -0.1 - 0.15 * gh);
                    double gv = i * rise + (sideW > 0 ? 0.35 * rise : 0);
                    double ped = Math.Max(0.3, 0.26 * gh);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        if (lod <= 1) SacredParts.Pedestal(m, k, sgn * gu, gv, gw, ped, 1.15 * ped, ph, Looks.Stone, lod);
                        SacredFigures.Guardian(m, k, sgn * gu, gv + (lod <= 1 ? ph : 0), gw, gh + (lod <= 1 ? 0 : ph), (GuardianKind)kind, p.PaintedGuardians, p.Hero, lod);
                    }
                }
            }
            else if (levels == 0 && p.Guardians != GuardianSet.None)
            {
                int kind = Math.Max(0, GuardianKindAt(p.Guardians, 0));
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * 0.5 * coreW, 0, 0.5 * coreD + 1.0, 0.3, 0.35, 0.4, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * 0.5 * coreW, 0.4, 0.5 * coreD + 1.0, 1.0, (GuardianKind)kind, p.PaintedGuardians, p.Hero, lod);
                }
            }

            // 3. Roof specs (needed by the walls, struts, soffits and pataka).
            var roofs = new RoofSpec[tiers];
            bool gilt0 = p.Finish == RoofFinish.GiltAll;
            double amb = p.Ambulatory && !p.Open ? Math.Min(2.0, Math.Max(0.9, 0.12 * coreW)) : 0;
            double ring0U = 0.5 * coreW + amb, ring0W = 0.5 * coreD + amb;
            for (int i = 0; i < tiers; i++)
            {
                bool gilt = gilt0 || p.Finish == RoofFinish.GiltTop && i == tiers - 1;
                double hu = 0.5 * ew[i], hw = hu * aspect;
                double wallU = i == 0 ? ring0U : 0.5 * sw[i], wallW = i == 0 ? ring0W : 0.5 * sw[i] * aspect;
                double topU, topW, topV = eh[i] + rr[i];
                if (i < tiers - 1)
                {
                    topU = 0.5 * sw[i + 1] - 0.03;
                    topW = 0.5 * sw[i + 1] * aspect - 0.03;
                }
                else
                {
                    topU = Math.Max(0.18, 0.06 * ew[i]);
                    topW = topU;
                }
                double fasciaH = Math.Min(0.3, Math.Max(0.14, 0.012 * ew[i]));
                // Soffit meets the wall a roof thickness under the slope.
                double tw = (hu - wallU) / Math.Max(1e-6, hu - topU);
                double wallTop = eh[i] + tw * (topV - eh[i]) - Math.Max(0.22, 0.6 * fasciaH);
                roofs[i] = new RoofSpec
                {
                    EaveU = hu, EaveW = hw, EaveV = eh[i], TopU = topU, TopW = topW, TopV = topV, WallU = wallU, WallW = wallW, WallTopV = wallTop,
                    Lift = Math.Min(0.08, Math.Max(0, p.CornerLift)) * hu, Sag = 0.02 * (hu - topU), FasciaH = fasciaH, CoursePitch = p.Rich ? 0.62 : p.Hero ? 0.8 : 1.0,
                    Gilt = gilt, Fringe = p.Fringe, FringeH = 0.28 + 0.008 * ew[i], Horns = true,
                    Roof = gilt ? (p.RoofColour != 0 ? Looks.Of(p.RoofColour, MaterialChannel.Gilt) : Looks.Gilt) : Looks.Of(tileCol, MaterialChannel.RoofTile),
                    Ridge = gilt ? Looks.GiltHi : Looks.Of(MeshColor.Scale(tileCol, 0.72f), MaterialChannel.RoofTile),
                    Fascia = gilt ? Looks.GiltAged : Looks.WoodDark, Soffit = Looks.Ao(Looks.WoodDark, 0.55f), Horn = gilt ? Looks.Gilt : Looks.WoodDark,
                };
            }

            // 4. The ground storey: sanctum (or open hall) and the ambulatory.
            RoofSpec r0 = roofs[0];
            double doorDh = 0;
            if (p.Open)
            {
                OpenHall(kf, k, coreW, coreD, plinthTop, r0, wallB, lod, m, c, stats, tiers, levels, top, f);
                doorDh = 1.9;
            }
            else
            {
                // The sanctum wall reaches under the roof over its own line.
                double sanctumWallTop = amb > 0 ? RoofUnder(r0, 0.5 * coreW) - 0.25 : r0.WallTopV;
                SacredParts.BandedWall(m, k, 0.5 * coreW, 0.5 * coreD, plinthTop, sanctumWallTop, wallB, Looks.WoodDark, lod, true, amb <= 0);
                double groundH0 = r0.EaveV - plinthTop;
                double dw = Math.Min(1.4, Math.Max(0.7, 0.22 * coreW)), dh = Math.Min(Math.Min(2.2, Math.Max(1.6, 0.75 * coreH)), 0.62 * groundH0);
                double doorMax = amb > 0 ? PostTop(r0, plinthTop) - 0.1 : r0.WallTopV - 0.6;
                doorDh = dh;
                int doors = p.Doors >= 4 ? 4 : 1;
                ShapeBrush frame = p.Doors >= 4 ? Looks.Silver : Looks.WoodDark;
                for (int d = 0; d < doors; d++)
                {
                    double half = d % 2 == 0 ? 0.5 * coreD : 0.5 * coreW, across = d % 2 == 0 ? 0.5 * coreW : 0.5 * coreD;
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, d * 0.5 * Math.PI);
                    SacredParts.Door(m, side, 0, plinthTop, half, dw, dh, frame, lod, true, doorMax);
                    if (lod == 0 && across > dw + 1.6)
                    {
                        double wu = Math.Min(across - 0.6, 0.5 * dw + 0.9);
                        SacredParts.LatticeWindow(m, side, -wu, plinthTop + 0.6 * dh, half, 0.7, 0.8, Looks.WoodDark, lod);
                        SacredParts.LatticeWindow(m, side, wu, plinthTop + 0.6 * dh, half, 0.7, 0.8, Looks.WoodDark, lod);
                    }
                }
                if (amb > 0 && lod <= 1) Ambulatory(m, k, ring0U, ring0W, plinthTop, PostTop(r0, plinthTop), r0.WallTopV, AmbPosts(2 * ring0U, p), AmbPosts(2 * ring0W, p), lod);
                if (c != null)
                {
                    double x, y, z;
                    kf.ToWorld(0, 0, 0, out x, out y, out z);
                    c.AddBox(x, z, f.GroundY + plinthTop, f.GroundY + eh[0], 0.5 * coreW, 0.5 * coreD, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
                }
                Fill(stats, tiers, levels, plinthTop, top, f, coreW, coreD, eh[0], 0, steps);
            }
            if (stats != null) stats.Steps = steps;

            // 5. Tiers: the storey wall under each upper roof, struts, the roof, bells.
            int bells = 0, struts = 0;
            double pitchM = p.StrutPitchM > 0.2 ? p.StrutPitchM : 1.1;
            for (int i = 0; i < tiers; i++)
            {
                RoofSpec r = roofs[i];
                double storeyBase = i == 0 ? plinthTop : roofs[i - 1].TopV - 0.3;
                if (i > 0)
                {
                    ShapeBrush upper = p.PlasterUpper ? Looks.Whitewash : wallB;
                    SacredParts.BandedWall(m, k, r.WallU, r.WallW, storeyBase, r.WallTopV, upper, Looks.WoodDark, lod, true, true);
                    if (lod <= 1) UpperWindows(m, k, r, roofs[i - 1].TopV, p.Rich && lod == 0, lod);
                    if (p.Balcony && i == 1 && lod <= 1) Balcony(m, k, r, Math.Max(roofs[i - 1].TopV + 0.3, r.WallTopV - 1.9));
                }
                // Struts spring from a ledge at about half the visible storey (from the posts on an open or ringed storey).
                double visibleBase = i == 0 ? plinthTop : roofs[i - 1].TopV;
                bool onPosts = i == 0 && (amb > 0 || p.Open);
                double groundH = r.EaveV - plinthTop;
                double footV = i > 0 ? visibleBase + 0.3 : onPosts ? PostTop(r, plinthTop) + 0.3 : plinthTop + Math.Min(0.72 * groundH, Math.Max(doorDh + 0.35, 0.5 * groundH));
                int perU = onPosts ? (p.Open ? OpenPosts(2 * r.WallU) : AmbPosts(2 * r.WallU, p)) : StrutsPerSide(2 * r.WallU, pitchM);
                int perW = onPosts ? (p.Open ? OpenPosts(2 * r.WallW) : AmbPosts(2 * r.WallW, p)) : StrutsPerSide(2 * r.WallW, pitchM);
                struts += 2 * perU + 2 * perW + 4;
                if (lod <= 1)
                {
                    if (!onPosts)
                    {
                        Mould ledge = SacredDraw.M;
                        ledge.Add(0, footV - 0.16, Looks.WoodDark).Add(0.12, footV - 0.12).Add(0.12, footV).Add(0, footV);
                        double[] lu = ShapeScratch.U2, lw = ShapeScratch.W2;
                        int ln = SacredDraw.Rect(0, 0, r.WallU, r.WallW, lu, lw);
                        SacredDraw.Ring(m, k, lu, lw, ln, ledge);
                    }
                    SacredParts.Struts(m, k, r, footV, perU, perW, p.Rich, Looks.WoodDark, (uint)(i * 977 + tiers), lod);
                }
                SacredParts.Roof(m, k, r, lod);
                int perEave = Math.Max(1, (int)Math.Floor(ew[i] / Math.Max(0.1, p.BellSpacingM)));
                bells += 4 * perEave;
                SacredParts.Bells(m, k, r, p.BellSpacingM, 1.0 + 0.004 * ew[i], p.Rich, lod);
            }

            // 6. Gajur on the apex block and the pataka down the front.
            RoofSpec rt = roofs[tiers - 1];
            SacredParts.Gajur(m, k, apex - 0.05, gajur + 0.05, Math.Max(0.22, 0.85 * rt.TopU), Looks.Gilt, p.Hero ? lod : lod + 1, p.GajurBell);
            if (p.Pataka && lod <= 1) Pataka(m, k, roofs, tiers, plinthTop + doorDh + 1.2, lod);

            // 7. Vahana, bell and lamp pillars in front.
            double frontW = 0.5 * (levels > 0 ? pd[0] : coreD);
            if (p.VahanaDistM > 0 && lod <= 2)
            {
                double vw = frontW + p.VahanaDistM;
                Affine3 facing = SacredDraw.Turn(k, 0, 0, vw, Math.PI);
                if (p.VahanaGaruda)
                {
                    Column(m, k, 0, vw, 2.6, lod);
                    SacredFigures.Garuda(m, facing, 0, 2.6, 0, 1.4, Looks.Stone, lod);
                }
                else
                {
                    SacredParts.Pedestal(m, k, 0, 0, vw, 0.7, 1.1, 0.6, Looks.Stone, lod);
                    SacredFigures.Nandi(m, facing, 0, 0.6, 0, 1.8, Looks.Gilt, lod);
                }
            }
            if (p.FrontBell && lod <= 1)
            {
                double bu = 0.5 * (levels > 0 ? pw[0] : coreW) - 1.2;
                SacredFigures.TempleBell(m, k, bu, 0, frontW + 1.6, 0.5, true, lod);
            }
            for (int q = 0; q < p.LampPillars && lod <= 1; q++)
            {
                double lu = (q % 2 == 0 ? -1 : 1) * (1.6 + 1.2 * (q / 2));
                SacredFigures.LampPillar(m, k, lu, 0, frontW + 2.4, 2.4, lod);
            }

            // Roofs and spire are not climbable.
            if (c != null)
            {
                double x, y, z;
                kf.ToWorld(0, 0, 0, out x, out y, out z);
                c.AddBox(x, z, f.GroundY + eh[0], f.GroundY + top, 0.5 * ew[0], 0.5 * ew[0] * aspect, kf.UX, kf.UZ, GenColliderFlags.NoClimb, GenColliders.Wood);
            }
            if (stats != null)
            {
                stats.Bells = bells;
                stats.Struts = struts;
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            return m.TriangleCount - t0;
        }

        /// <summary>A palace block under a temple (Patan Taleju's tower rises from the palace): brick storeys with wooden
        /// string courses and rows of carved windows, a moulded top.</summary>
        private static void PalaceBlock(MeshData m, in Affine3 k, double hu, double hw, double h, int lod)
        {
            int storeys = Math.Max(2, (int)Math.Round(h / 3.2));
            double sh = h / storeys;
            for (int s = 0; s < storeys; s++)
                SacredParts.BandedWall(m, k, hu, hw, s == 0 ? -0.4 : s * sh, (s + 1) * sh, Looks.WallGlazed, Looks.WoodDark, lod, true, s == storeys - 1);
            SacredDraw.CapRect(m, k, 0, 0, hu + 0.17, hw + 0.17, h, true, Looks.WoodDark);
            if (lod >= 2) return;
            for (int sd = 0; sd < 4; sd++)
            {
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, sd * 0.5 * Math.PI);
                double half = sd % 2 == 0 ? hw : hu, along = sd % 2 == 0 ? hu : hw;
                int n = Math.Max(1, (int)Math.Round(2 * along / 3.0));
                for (int s = 1; s < storeys; s++)
                    for (int q = 0; q < n; q++)
                        SacredParts.LatticeWindow(m, side, -along + 2 * along * (q + 0.5) / n, s * sh + 0.5 * sh, half, 0.9, Math.Min(1.4, sh - 1.0), Looks.WoodDark, lod + 1, 3);
            }
        }

        /// <summary>Posts (and tier-0 struts) per side of an ambulatory ring: an even count so the door axis stays open.</summary>
        private static int AmbPosts(double len, in PagodaParams p)
        {
            double pitch = Math.Max(1.7, p.StrutPitchM > 0.2 ? p.StrutPitchM : 1.1);
            int n = Math.Max(2, (int)Math.Round(len / pitch));
            return n % 2 == 0 ? n : n + 1;
        }

        private static int OpenPosts(double len)
        {
            int n = Math.Max(4, (int)Math.Round(len / 2.2));
            return n + n % 2;
        }

        /// <summary>Height of the roof slope over a wall line of half size <paramref name="half"/> (no sag).</summary>
        private static double RoofUnder(in RoofSpec r, double half)
        {
            double t = (r.EaveU - half) / Math.Max(1e-6, r.EaveU - r.TopU);
            t = Math.Max(0, Math.Min(1, t));
            return r.EaveV + t * (r.TopV - r.EaveV);
        }

        private static void Local(in KitFrame k, double x, double z, out double u, out double w)
        {
            double dx = x - k.OX, dz = z - k.OZ;
            // Inverse of ToWorld: x = OX + u UX + w UZ, z = OZ + u UZ − w UX.
            u = dx * k.UX + dz * k.UZ;
            w = dx * k.UZ - dz * k.UX;
        }

        /// <summary>Top of the ground-storey posts of a ringed or open storey (well under the eave, so the struts above
        /// them stand steep).</summary>
        private static double PostTop(in RoofSpec r, double plinthTop)
        {
            return plinthTop + 0.62 * (r.EaveV - plinthTop);
        }

        /// <summary>The ring of carved posts round the sanctum (Nyatapola, Maju Dega): posts at the strut positions plus
        /// the corners, a moulded beam on them and a dark carved frieze from the beam up to the soffit.</summary>
        private static void Ambulatory(MeshData m, in Affine3 k, double hu, double hw, double v0, double postTop, double wallTop, int nu, int nw, int lod)
        {
            double t = 0.24;
            for (int s = 0; s < 4; s++)
            {
                int n = s % 2 == 0 ? nu : nw;
                double along = s % 2 == 0 ? hu : hw, wall = s % 2 == 0 ? hw : hu;
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                for (int q = 0; q < n; q++)
                {
                    double x = -along + 2 * along * (q + 0.5) / n;
                    SacredParts.Post(m, side, x, wall, v0, postTop, t, Looks.WoodDark, lod);
                }
                SacredParts.Post(m, side, along, wall, v0, postTop, t, Looks.WoodDark, lod);
            }
            Mould b = SacredDraw.M;
            b.Add(-0.14, postTop, Looks.Ao(Looks.WoodDark, 0.6f)).Add(0.16, postTop, Looks.WoodDark).Add(0.16, postTop + 0.14).Add(0.22, postTop + 0.14)
             .Add(0.22, postTop + 0.3);
            if (wallTop > postTop + 0.4) b.Add(0.02, postTop + 0.3, Looks.Ao(Looks.WoodMid, 0.7f)).Add(0.02, wallTop);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int nn = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(m, k, pu, pw, nn, b);
        }

        /// <summary>Kasthamandap-type open hall: an outer ring of posts on the plinth, an inner ring, the closed central
        /// shrine (a NoClimb sanctum), a low railing open at the door.</summary>
        private static void OpenHall(in KitFrame kf, in Affine3 k, double coreW, double coreD, double plinthTop, in RoofSpec r0, in ShapeBrush wallB, int lod,
                                     MeshData m, GenColliders c, SacredStats stats, int tiers, int levels, double top, in GenFrame f)
        {
            double hu = 0.5 * coreW, hw = 0.5 * coreD, v1 = PostTop(r0, plinthTop);
            int nu = Math.Max(4, (int)Math.Round(coreW / 2.2)), nw = Math.Max(4, (int)Math.Round(coreD / 2.2));
            nu += nu % 2;
            nw += nw % 2;
            Ambulatory(m, k, hu, hw, plinthTop, v1, r0.WallTopV, nu, nw, lod);
            if (lod == 0) Ambulatory(m, k, 0.62 * hu, 0.62 * hw, plinthTop, v1, v1, Math.Max(2, nu - 2), Math.Max(2, nw - 2), lod);
            // The ceiling of the hall (the floor of the upper storey).
            SacredDraw.CapRect(m, k, 0, 0, hu + 0.2, hw + 0.2, v1 + 0.31, false, Looks.Ao(Looks.WoodDark, 0.5f));
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    double along = s % 2 == 0 ? hu : hw, wall = s % 2 == 0 ? hw : hu;
                    Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                    double gap = s == 0 ? 1.6 : 0;
                    for (int part = 0; part < (gap > 0 ? 2 : 1); part++)
                    {
                        double a0 = gap > 0 ? (part == 0 ? -along : gap) : -along, a1 = gap > 0 ? (part == 0 ? -gap : along) : along;
                        SacredDraw.Box(m, side, a0, a1, plinthTop + 0.75, plinthTop + 0.85, wall - 0.05, wall + 0.05, Looks.WoodDark, BoxFaces.All & ~BoxFaces.Bottom);
                        SacredDraw.Box(m, side, a0, a1, plinthTop + 0.1, plinthTop + 0.16, wall - 0.04, wall + 0.04, Looks.WoodDark, BoxFaces.Front | BoxFaces.Top | BoxFaces.Back);
                    }
                }
            }
            double sh = Math.Min(2.2, 0.25 * coreW), shrineTop = Math.Min(v1 - 0.4, plinthTop + 2.6);
            SacredParts.BandedWall(m, k, sh, sh, plinthTop, shrineTop, wallB, Looks.WoodDark, lod, true, true);
            SacredDraw.CapRect(m, k, 0, 0, sh + 0.17, sh + 0.17, shrineTop, true, Looks.WoodDark);
            SacredParts.Door(m, k, 0, plinthTop, sh, Math.Min(1.0, sh), 1.9, Looks.WoodDark, lod);
            if (c != null)
            {
                double x, y, z;
                kf.ToWorld(0, 0, 0, out x, out y, out z);
                c.AddBox(x, z, f.GroundY + plinthTop, f.GroundY + shrineTop, sh, sh, kf.UX, kf.UZ, GenColliderFlags.NoClimb | GenColliderFlags.SoftMargin, GenColliders.Brick);
            }
            Fill(stats, tiers, levels, plinthTop, top, f, 2 * sh, 2 * sh, shrineTop, 0, 0);
        }

        /// <summary>Carved windows in the visible band of an upper storey (1–3 per side, up to 5 on a rich centrepiece at LOD0).</summary>
        private static void UpperWindows(MeshData m, in Affine3 k, in RoofSpec r, double baseV, bool rich, int lod)
        {
            double vis0 = baseV + 0.15, vis1 = r.WallTopV - 0.55;
            if (vis1 - vis0 < 0.6) return;
            double wh = Math.Min(1.1, 0.6 * (vis1 - vis0)), vc = 0.5 * (vis0 + vis1);
            for (int s = 0; s < 4; s++)
            {
                double along = s % 2 == 0 ? r.WallU : r.WallW, wall = s % 2 == 0 ? r.WallW : r.WallU;
                // A centrepiece carries a continuous row of carved windows (Nyatapola: five a side on the lower storeys).
                int n = rich ? Math.Max(1, Math.Min(5, (int)Math.Round(along / 1.1))) : Math.Max(1, Math.Min(3, (int)Math.Round(2 * along / 3.0)));
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                for (int q = 0; q < n; q++)
                {
                    double x = n == 1 ? 0 : -along + 2 * along * (q + 0.5) / n;
                    SacredParts.LatticeWindow(m, side, x, vc, wall, Math.Min(0.9, 0.8 * wh), wh, Looks.WoodDark, lod + 1, 2);
                }
            }
        }

        /// <summary>A projecting wooden balcony gallery round an upper storey (Kasthamandap).</summary>
        private static void Balcony(MeshData m, in Affine3 k, in RoofSpec r, double baseV)
        {
            double v = baseV, depth = 0.8;
            Mould b = SacredDraw.M;
            b.Add(0, v - 0.25, Looks.WoodDark).Add(depth, v - 0.05).Add(depth, v + 0.05).Add(depth, v + 0.9, Looks.WoodMid).Add(depth - 0.08, v + 0.9)
             .Add(depth - 0.08, v + 1.0, Looks.WoodDark);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, r.WallU, r.WallW, pu, pw);
            SacredDraw.Ring(m, k, pu, pw, n, b);
        }

        /// <summary>The pataka from under the gajur down the front of every roof and between them, ending above the door.</summary>
        private static void Pataka(MeshData m, in Affine3 k, RoofSpec[] roofs, int tiers, double endV, int lod)
        {
            int cap = 4 * tiers + 4;
            double[] pu = new double[cap], pv = new double[cap], pw = new double[cap];
            int n = 0;
            double width = Math.Max(0.3, Math.Min(0.6, 0.03 * roofs[0].EaveU * 2));
            for (int i = tiers - 1; i >= 0; i--)
            {
                RoofSpec r = roofs[i];
                double tStart = 1;
                if (i < tiers - 1)
                {
                    // Where the strip hanging from the tier above lands on this slope.
                    double wl = roofs[i + 1].EaveW + 0.06;
                    tStart = Math.Max(0.05, Math.Min(0.95, (r.EaveW - wl) / Math.Max(1e-6, r.EaveW - r.TopW)));
                }
                for (int q = 0; q < 3; q++)
                {
                    double t = tStart * (1 - q / 2.0);
                    double u, v, w, nu, nv, nw;
                    SacredParts.RoofPoint(r, 0, 0, t, out u, out v, out w);
                    SacredParts.RoofNormal(r, 0, 0, t, out nu, out nv, out nw);
                    pu[n] = 0;
                    pv[n] = v + nv * 0.09;
                    pw[n] = w + nw * 0.09 + (q == 2 ? 0.06 : 0);
                    n++;
                }
                pu[n] = 0;
                pv[n] = i > 0 ? r.EaveV - r.FasciaH - 0.1 : Math.Max(endV, r.EaveV - 2.2);
                pw[n] = r.EaveW + 0.06;
                n++;
            }
            SacredParts.Pataka(m, k, pu, pv, pw, n, width, lod);
        }

        /// <summary>A stone pillar (Garuda column) of height h at (u, w).</summary>
        private static void Column(MeshData m, in Affine3 k, double u, double w, double h, int lod)
        {
            SacredParts.Pedestal(m, k, u, 0, w, 0.6, 0.6, 0.45, Looks.Stone, lod);
            Shapes.Cylinder(m, SacredDraw.At(k, u, 0.45, w), Looks.Stone, 0.28, h - 0.65, SacredParts.Seg(10, lod), 0.04, 1);
            Shapes.Cylinder(m, SacredDraw.At(k, u, h - 0.2, w), Looks.StoneLight, 0.42, 0.2, SacredParts.Seg(10, lod), 0.05, 1);
        }

        /// <summary>LOD3: plinth block, sanctum box and hipped roofs (≤ 200 triangles).</summary>
        private static void Lod3(in PagodaParams p, in Affine3 k, int levels, double plinthTop, double w0, double d0, double coreW, double coreD, double[] ew,
                                 double[] eh, double[] rr, double[] sw, double aspect, int tiers, double apex, double top, in ShapeBrush plinthB,
                                 in ShapeBrush wallB, uint tileCol, MeshData m)
        {
            if (levels > 0) SacredDraw.Box(m, k, -0.5 * w0, 0.5 * w0, -0.3, plinthTop, -0.5 * d0, 0.5 * d0, plinthB, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, k, -0.5 * coreW, 0.5 * coreW, plinthTop, eh[0], -0.5 * coreD, 0.5 * coreD, wallB, BoxFaces.All & ~BoxFaces.Bottom);
            for (int i = 0; i < tiers; i++)
            {
                bool gilt = p.Finish == RoofFinish.GiltAll || p.Finish == RoofFinish.GiltTop && i == tiers - 1;
                ShapeBrush roof = gilt ? Looks.Gilt : Looks.Of(tileCol, MaterialChannel.RoofTile);
                double hu = 0.5 * ew[i], hw = hu * aspect, tu = i < tiers - 1 ? 0.5 * sw[i + 1] : 0.02, tw = tu * (i < tiers - 1 ? aspect : 1);
                double v0 = eh[i], v1 = eh[i] + rr[i];
                for (int s = 0; s < 4; s++)
                {
                    double au, aw, bu, bw, cu, cw, du, dw, ou, ow;
                    SacredParts.OnRect(s, -1, hu, hw, out au, out aw);
                    SacredParts.OnRect(s, 1, hu, hw, out bu, out bw);
                    SacredParts.OnRect(s, 1, tu, tw, out cu, out cw);
                    SacredParts.OnRect(s, -1, tu, tw, out du, out dw);
                    SacredParts.Out(s, out ou, out ow);
                    SacredDraw.Quad(m, k, au, v0, aw, bu, v0, bw, cu, v1, cw, du, v1, dw, ou, 1, ow, roof);
                }
                if (i > 0) SacredDraw.Box(m, k, -0.5 * sw[i], 0.5 * sw[i], eh[i - 1] + rr[i - 1] - 0.2, eh[i], -0.5 * sw[i] * aspect, 0.5 * sw[i] * aspect, wallB,
                                          BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            }
            Shapes.Cone(m, SacredDraw.At(k, 0, apex - 0.05, 0), Looks.Gilt, Math.Max(0.2, 0.05 * ew[tiers - 1]), top - apex + 0.05, 4, 0, 0, false);
        }

        private static readonly int[] NyatapolaOrder = { 1, 2, 0, 3, 4 };

        private static int GuardianKindAt(GuardianSet g, int level)
        {
            switch (g)
            {
                case GuardianSet.Lions: return level == 0 ? 0 : -1;
                case GuardianSet.Wrestlers: return level == 0 ? 1 : -1;
                case GuardianSet.Elephants: return level == 0 ? 2 : -1;
                case GuardianSet.Nyatapola: return level < 5 ? NyatapolaOrder[level] : -1;
                default: return -1;
            }
        }

        private static void Fill(SacredStats s, int tiers, int levels, double plinthTop, double top, in GenFrame f, double coreW, double coreD,
                                 double sanctumTop, int bells, int steps)
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
