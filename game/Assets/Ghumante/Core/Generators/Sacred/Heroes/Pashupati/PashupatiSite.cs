using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using Ghumante.Core.Streaming;

namespace Ghumante.Core.Generators.Sacred.Heroes.Pashupati
{
    /// <summary>
    /// The parts of the bespoke Pashupatinath site, each built as its own hero (so the hero LOD allocator gives the main
    /// temple LOD0 while the far side of the courtyard steps down) and all anchored in the main temple's level-10 tile.
    /// </summary>
    public enum PashupatiPart : byte
    {
        /// <summary>PT-01 main temple, PT-03 gajur and trident, PT-02 Nandi, the courtyard floor, rails, lamps and bells.</summary>
        Temple = 0,

        /// <summary>PT-04 West Gate, its flanks and the inner west buildings, PT-05 forecourt, PT-15 great trident.</summary>
        WestGate = 1,

        /// <summary>PT-11 East Gate, the east stair and the east wing.</summary>
        EastGate = 2,

        /// <summary>PT-08 Mukti Mandap on its riverside block and PT-21 south-east block.</summary>
        MuktiMandap = 3,

        /// <summary>PT-07 Bhairav temple, PT-09 octagonal temple, PT-13 South Gate range, PT-14 kings' pillars, PT-19
        /// priests' quarters.</summary>
        SouthCourt = 4,

        /// <summary>PT-06 Vasuki temple, PT-10 small shrines, PT-12 north wing, the two Araucarias.</summary>
        NorthCourt = 5,
    }

    /// <summary>
    /// Where the Pashupatinath site stands in a tile and how to build in it: the temple frame of the heritage brief
    /// (docs/research/heritage/pashupati-temple.md §0): <c>a</c> metres toward "compound east" (bearing 126.7°, the east
    /// door and the river), <c>b</c> toward "compound north" (36.7°), <c>y</c> up from the courtyard floor, origin the
    /// temple centroid. <see cref="Site"/> maps (a, y, b) to tile-local metres with absolute heights (a proper rotation:
    /// no mirroring). The collider helpers take site coordinates and write tile-local colliders.
    /// </summary>
    internal struct PashupatiFrame
    {
        /// <summary>Site (a, y, b) to tile-local (x, absolute y, z).</summary>
        public Affine3 Site;

        /// <summary>Tile-local temple centroid and the absolute courtyard level.</summary>
        public double OX, OZ, Level;

        /// <summary>Tile-plane unit vectors of the a and b axes.</summary>
        public double AX, AZ, BX, BZ;

        public int Lod;

        /// <summary>Colliders of LOD0 (null at the other LODs).</summary>
        public GenColliders C;

        /// <summary>The OSM outlines of the tile (site metres), when the build has a tile.</summary>
        public TileData Tile;

        /// <summary>The tile's terrain as streamed (after <see cref="PashupatiGround.Apply"/>; see
        /// <see cref="PashupatiGround.Prepared"/>), or null.</summary>
        public TileHeightSampler Terrain;

        /// <summary>The prepared terrain as the streamer draws it (decimated: <see cref="DrawnSteps"/>), or null.</summary>
        public TileHeightSampler[] Drawn;

        /// <summary>The frame of a build on <paramref name="tile"/>, its terrain the prepared ground.</summary>
        public static PashupatiFrame For(TileData tile, int lod, GenColliders c)
        {
            PashupatiFrame f = Plain(tile, lod, c);
            TileData ground = PashupatiGround.Prepared(tile);
            if (!TerrainGrid.HasHeights(ground)) return f;
            f.Terrain = new TileHeightSampler(ground);
            int[] steps = DrawnSteps(ground);
            f.Drawn = new TileHeightSampler[steps.Length];
            for (int i = 0; i < steps.Length; i++) f.Drawn[i] = TileHeightSampler.ForArea(ground, ground.Tile, steps[i]);
            return f;
        }

        /// <summary>The distinct terrain decimation steps the streamer draws <paramref name="tile"/> with on the three
        /// tiers (the tile as its own area: <see cref="StreamingConfig.TerrainStep"/>).</summary>
        public static int[] DrawnSteps(TileData tile)
        {
            int n = 0;
            var steps = new int[3];
            for (int tier = StreamingConfig.TierLow; tier <= StreamingConfig.TierHigh; tier++)
            {
                int s = StreamingConfig.ForTier(tier).TerrainStep(tile.Tile, tile.Tile, tile.HeightsN);
                if (Array.IndexOf(steps, s, 0, n) < 0) steps[n++] = s;
            }
            Array.Resize(ref steps, n);
            return steps;
        }

        /// <summary>The highest of the terrain surfaces at site (a, b) (every drawn decimation and the full grid) relative
        /// to the courtyard level, or <paramref name="fallback"/> without heights: what a level floor must stay above to
        /// keep the terrain from pushing through it on any tier.</summary>
        public double TopGroundAt(double a, double b, double fallback)
        {
            if (Terrain == null) return fallback;
            double top = GroundAt(a, b, fallback), x, z;
            ToTile(a, b, out x, out z);
            for (int i = 0; Drawn != null && i < Drawn.Length; i++)
            {
                float h;
                if (Drawn[i].TryHeightClamped(Tile.Tile.X0 + x, Tile.Tile.Z0 + z, out h)) top = Math.Max(top, h - Level);
            }
            return top;
        }

        /// <summary>The frame without a terrain sampler (the transform only).</summary>
        public static PashupatiFrame Plain(TileData tile, int lod, GenColliders c)
        {
            var f = new PashupatiFrame { Lod = lod, C = c, Tile = tile, Level = PashupatiSite.CourtyardLevelM };
            double r = PashupatiSite.AxisBearingDeg * Math.PI / 180.0;
            f.AX = Math.Sin(r);
            f.AZ = Math.Cos(r);
            // b: bearing 36.7° = 126.7° − 90°.
            f.BX = -f.AZ;
            f.BZ = f.AX;
            f.OX = PashupatiSite.CentreX - tile.Tile.X0;
            f.OZ = PashupatiSite.CentreZ - tile.Tile.Z0;
            f.Site = Affine3.FromBasis(f.AX, 0, f.AZ, 0, 1, 0, f.BX, 0, f.BZ, f.OX, f.Level, f.OZ);
            return f;
        }

        /// <summary>The terrain at site (a, b) relative to the courtyard level, or <paramref name="fallback"/> without
        /// heights (the east stair's foot on the ghat walk).</summary>
        public double GroundAt(double a, double b, double fallback)
        {
            if (Terrain == null) return fallback;
            double x, z;
            ToTile(a, b, out x, out z);
            float h;
            return Terrain.TryHeightClamped(Tile.Tile.X0 + x, Tile.Tile.Z0 + z, out h) ? h - Level : fallback;
        }

        /// <summary>Tile-local (x, z) of site (a, b).</summary>
        public void ToTile(double a, double b, out double x, out double z)
        {
            x = OX + a * AX + b * BX;
            z = OZ + a * AZ + b * BZ;
        }

        /// <summary>Site (a, b) of tile-local (x, z).</summary>
        public void FromTile(double x, double z, out double a, out double b)
        {
            double dx = x - OX, dz = z - OZ;
            a = dx * AX + dz * AZ;
            b = dx * BX + dz * BZ;
        }

        /// <summary>A translation to site (a, y, b).</summary>
        public Affine3 At(double a, double y, double b)
        {
            return Affine3.Translation(a, y, b).Then(Site);
        }

        /// <summary>A local frame at site (a, y, b) whose +z points along the site direction (da, db) (unit), y up and
        /// x = (db, 0, −da): a face frame (x along the face, z outward) when (da, db) is the face's outward normal.</summary>
        public Affine3 Facing(double a, double y, double b, double da, double db)
        {
            return Affine3.FromBasis(db, 0, -da, 0, 1, 0, da, 0, db, a, y, b).Then(Site);
        }

        /// <summary>A site-aligned box collider (site extents, heights above the courtyard).</summary>
        public void Box(double a0, double a1, double b0, double b1, double y0, double y1, GenColliderFlags flags, byte material)
        {
            if (C == null) return;
            double cx, cz;
            ToTile(0.5 * (a0 + a1), 0.5 * (b0 + b1), out cx, out cz);
            C.AddBox(cx, cz, Level + y0, Level + y1, 0.5 * Math.Abs(a1 - a0), 0.5 * Math.Abs(b1 - b0), AX, AZ, flags, material);
        }

        /// <summary>
        /// A site-aligned solid box collider kept off the tile's drawn roads (<see cref="SacredDrawnRoads"/>): where a drawn
        /// footway runs into the footprint (the two OSM footways that end inside the West Gate's side bays, mapped 3.5 m
        /// either side of the gate the photos put on the temple axis; open issue for roads and data), the box is cut into
        /// strips across b (0.5 m) and each strip starts behind the road's end, so no solid stands on a walkway the road
        /// mesher draws. Strips with the same start merge.
        /// </summary>
        public void BoxClear(double a0, double a1, double b0, double b1, double y0, double y1, GenColliderFlags flags, byte material)
        {
            if (C == null) return;
            if (Tile == null || Tile.Roads.Count == 0)
            {
                Box(a0, a1, b0, b1, y0, y1, flags, material);
                return;
            }
            SacredDrawnRoads drawn = SacredDrawnRoads.For(Tile);
            const double Strip = 0.5, Step = 0.25;
            int strips = Math.Max(1, (int)Math.Ceiling((b1 - b0) / Strip));
            double runStart = double.NaN, runB0 = b0;
            for (int s = 0; s <= strips; s++)
            {
                double start = double.NaN;
                if (s < strips)
                {
                    double sb0 = b0 + (b1 - b0) * s / strips, sb1 = b0 + (b1 - b0) * (s + 1) / strips;
                    // The first a from which the rest of the strip is off every drawn road (scanning back from a1).
                    start = a0;
                    for (double a = a1; a >= a0 - 1e-9; a -= Step)
                    {
                        if (!OnDrawn(drawn, a, sb0, sb1)) continue;
                        start = a + Step;
                        break;
                    }
                    if (start > a1 - 0.2) start = double.NaN;
                }
                if (!double.IsNaN(runStart) && !double.IsNaN(start) && Math.Abs(start - runStart) < 1e-9) continue;
                double edge = b0 + (b1 - b0) * s / strips;
                if (!double.IsNaN(runStart)) Box(runStart, a1, runB0, edge, y0, y1, flags, material);
                runStart = start;
                runB0 = edge;
            }
        }

        /// <summary>A solid vertical cylinder at site (a, b), left out when any of it stands on a drawn road.</summary>
        public void CylinderClear(double a, double b, double y0, double y1, double radius)
        {
            if (C == null) return;
            if (Tile != null && Tile.Roads.Count > 0)
            {
                SacredDrawnRoads drawn = SacredDrawnRoads.For(Tile);
                for (int i = -2; i <= 2; i++)
                {
                    for (int j = -2; j <= 2; j++)
                    {
                        double x, z;
                        ToTile(a + 0.5 * radius * i, b + 0.5 * radius * j, out x, out z);
                        if (drawn.SignedDistance(x, z) < -0.05) return;
                    }
                }
            }
            Cylinder(a, b, y0, y1, radius);
        }

        /// <summary>A thin wall from site (a0, b0) to (a1, b1), shortened at either end where it runs onto a drawn road
        /// (left out when less than half a metre is clear).</summary>
        public void WallClear(double a0, double b0, double a1, double b1, double y0, double y1, double halfThickness)
        {
            if (C == null) return;
            if (Tile == null || Tile.Roads.Count == 0)
            {
                Wall(a0, b0, a1, b1, y0, y1, halfThickness);
                return;
            }
            SacredDrawnRoads drawn = SacredDrawnRoads.For(Tile);
            double len = Math.Sqrt((a1 - a0) * (a1 - a0) + (b1 - b0) * (b1 - b0));
            int n = Math.Max(2, (int)Math.Ceiling(len / 0.25));
            int first = -1, last = -1;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n, x, z;
                ToTile(a0 + (a1 - a0) * t, b0 + (b1 - b0) * t, out x, out z);
                if (drawn.SignedDistance(x, z) < halfThickness + 0.05) continue;
                if (first < 0) first = i;
                last = i;
            }
            if (first < 0 || last - first < 2) return;
            double t0 = (double)first / n, t1 = (double)last / n;
            Wall(a0 + (a1 - a0) * t0, b0 + (b1 - b0) * t0, a0 + (a1 - a0) * t1, b0 + (b1 - b0) * t1, y0, y1, halfThickness);
        }

        /// <summary>Whether the cross line of a box strip at a (b0..b1) touches a drawn road.</summary>
        private bool OnDrawn(SacredDrawnRoads drawn, double a, double b0, double b1)
        {
            for (int k = 0; k <= 2; k++)
            {
                double x, z;
                ToTile(a, b0 + (b1 - b0) * k / 2, out x, out z);
                if (drawn.SignedDistance(x, z) < -0.05) return true;
            }
            return false;
        }

        /// <summary>A box collider centred at site (ca, cb), its length along the site direction (da, db).</summary>
        public void BoxAlong(double ca, double cb, double halfAlong, double halfAcross, double da, double db, double y0, double y1, GenColliderFlags flags,
                             byte material)
        {
            if (C == null) return;
            double cx, cz, ux = da * AX + db * BX, uz = da * AZ + db * BZ;
            ToTile(ca, cb, out cx, out cz);
            C.AddBox(cx, cz, Level + y0, Level + y1, halfAlong, halfAcross, ux, uz, flags, material);
        }

        /// <summary>A walkable ramp (a stair) from site (a0, b0, y0) to (a1, b1, y1).</summary>
        public void Ramp(double a0, double b0, double y0, double a1, double b1, double y1, double halfWidth, byte material)
        {
            if (C == null) return;
            double x0, z0, x1, z1;
            ToTile(a0, b0, out x0, out z0);
            ToTile(a1, b1, out x1, out z1);
            C.AddRamp(new GenRamp
            {
                X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Y0 = (float)(Level + y0), Y1 = (float)(Level + y1), HalfWidth = (float)halfWidth, Material = material,
            });
        }

        /// <summary>A solid vertical cylinder at site (a, b).</summary>
        public void Cylinder(double a, double b, double y0, double y1, double radius, bool cameraPasses = false)
        {
            if (C == null) return;
            double x, z;
            ToTile(a, b, out x, out z);
            C.AddCylinder(x, z, Level + y0, Level + y1, radius, cameraPasses);
        }

        /// <summary>A thin wall from site (a0, b0) to (a1, b1).</summary>
        public void Wall(double a0, double b0, double a1, double b1, double y0, double y1, double halfThickness)
        {
            if (C == null) return;
            double x0, z0, x1, z1;
            ToTile(a0, b0, out x0, out z0);
            ToTile(a1, b1, out x1, out z1);
            C.AddWall(x0, z0, x1, z1, Level + y0, Level + y1, halfThickness);
        }

        /// <summary>The outer ring of an OSM building of the tile in site metres (counter-clockwise), or 0 points.</summary>
        public int Outline(ulong osmWay, double[] pa, double[] pb)
        {
            if (Tile == null) return 0;
            ulong key = osmWay << 1;
            for (int i = 0; i < Tile.Buildings.Count; i++)
            {
                if (Tile.Buildings[i].OsmRef != key) continue;
                int[] r = Tile.Buildings[i].Rings[0];
                int n = Math.Min(r.Length / 2, pa.Length);
                for (int k = 0; k < n; k++) FromTile(r[2 * k] / 100.0, r[2 * k + 1] / 100.0, out pa[k], out pb[k]);
                // Drop a closing duplicate, then make it counter-clockwise in (a, b).
                if (n > 1 && Math.Abs(pa[0] - pa[n - 1]) < 1e-3 && Math.Abs(pb[0] - pb[n - 1]) < 1e-3) n--;
                double s = 0;
                for (int k = 0, j = n - 1; k < n; j = k++) s += pa[j] * pb[k] - pa[k] * pb[j];
                if (s < 0)
                {
                    Array.Reverse(pa, 0, n);
                    Array.Reverse(pb, 0, n);
                }
                return n;
            }
            return 0;
        }
    }

    /// <summary>
    /// The bespoke Pashupatinath site (W2 monuments round; docs/research/heritage/pashupati-temple.md): every structure of
    /// the main temple, its courtyard, gates, shrines and the buildings round it modelled to its measured plan, section and
    /// colours instead of the parametric pagoda template that made it "look like some random temple". Six heroes build
    /// it (<see cref="PashupatiPart"/>): the curated record <see cref="HeroId"/> (the temple, its gajur and Nandi) and five
    /// catalogue parts anchored in the same tile (<see cref="HeroRecipe.SiteOf"/>). Each part has four LODs: LOD0 the full
    /// detail, LOD1 about 30 % and LOD2 about 8 % (both keep the silhouette: the gilt roofs and their black lattice bands,
    /// the gajur, the gate lanterns, the grey Mukti Mandap roof), LOD3 a few boxes (≤ 200 triangles). The courtyard is
    /// one flat level (<see cref="PashupatiGround"/>), walkable on every plinth, step and floor; the sanctum is never
    /// entered (doors closed, W2-O1).
    /// </summary>
    public static class PashupatiSite
    {
        /// <summary>The curated hero id (OSM w913170315).</summary>
        public const string HeroId = "her.ktm.pashupatinath";

        public const string WestGateId = HeroId + ".west_gate";
        public const string EastGateId = HeroId + ".east_gate";
        public const string MuktiMandapId = HeroId + ".mukti_mandap";
        public const string SouthCourtId = HeroId + ".south_court";
        public const string NorthCourtId = HeroId + ".north_court";

        /// <summary>Game position of the temple centroid (the centre of the OSM lower-roof part w1032558820) [O].</summary>
        public const double CentreX = 532963.93, CentreZ = 165857.51;

        /// <summary>Bearing of the site's a axis ("compound east", the east door): the OSM outline's edges run at 36.7° and
        /// 126.7° [O]; the main (west) door faces 306.7°.</summary>
        public const double AxisBearingDeg = 126.7;

        /// <summary>The main door's bearing (the west door, to the Nandi and the West Gate).</summary>
        public const double MainDoorBearingDeg = 306.7;

        /// <summary>The courtyard floor (absolute metres): the region pack's terrain (Copernicus GLO-30) under the temple
        /// centroid (1310.2 m) on the height grid's 0.15 m quantum. The courtyard is level [P]; <see cref="PashupatiGround"/>
        /// flattens every tile's height grid to it.</summary>
        public const double CourtyardLevelM = 1310.15;

        /// <summary>The west forecourt and the West Gate passage stand three steps (0.2 m) above the courtyard
        /// [P: c_westgate/c_26, nandi/ref_02], on the height quantum.</summary>
        public const double ForecourtRiseM = 0.6;

        /// <summary>Main temple: the finial tip 23.6 m above the courtyard [S: PADT, Wikipedia; O: roof 19.0 + gajur 4.6].</summary>
        public const float TempleTopM = 23.6f;

        /// <summary>Per-part LOD ceilings (LOD0..3; LOD1 about 30 %, LOD2 about 8 % of LOD0, LOD3 at most 200). Together
        /// 150,000 triangles at LOD0, the monuments round's allowance for a site (a major temple up to about 60,000).</summary>
        private static readonly int[][] Budgets =
        {
            new[] { 55500, 15500, 2400, 200 }, // temple, gajur, Nandi, floor, furniture
            new[] { 24800, 11000, 2200, 200 }, // West Gate, flanks, forecourt, trident
            new[] { 7500, 2900, 1000, 200 }, // East Gate, stair, east wing
            new[] { 8300, 3200, 800, 200 }, // Mukti Mandap, south-east block
            new[] { 30200, 10500, 1800, 200 }, // south court
            new[] { 23500, 8000, 2000, 200 }, // north court
        };

        /// <summary>The LOD0..3 triangle ceilings of a part (a fresh copy).</summary>
        public static int[] BudgetOf(PashupatiPart part)
        {
            return (int[])Budgets[(int)part].Clone();
        }

        /// <summary>OSM ways the parts rebuild, hidden from the house bands (<see cref="HeroRecipe.Hide"/>).</summary>
        public static string[] HideRefs(PashupatiPart part)
        {
            switch (part)
            {
                case PashupatiPart.Temple:
                    return new[] { "w913170315", "w1032558820", "w1032559974", "w1032559975", "w1033128464" };
                case PashupatiPart.WestGate:
                    return new[] { "w913170307", "r3442318", "w913170311", "w913170313", "w913170308", "w913170310", "w913170312" };
                case PashupatiPart.EastGate:
                    return new[] { "w913217416", "w1033128669" };
                case PashupatiPart.MuktiMandap:
                    return new[] { "w913217412", "w913217411" };
                case PashupatiPart.SouthCourt:
                    return new[]
                    {
                        "w913170316", "w256799892", "w256799893", "w256799894", "w256799895", "w256799896", "w256931055", "w913170317", "w913170301",
                        "w913170302", "w913170306",
                    };
                default:
                    return new[] { "w913170309", "w1094633491", "w1094633492", "w1094633493", "w1094633496", "w1368994740", "w1368994741", "w913217414", "w913217415" };
            }
        }

        /// <summary>The site's hero ids, by part.</summary>
        public static string IdOf(PashupatiPart part)
        {
            switch (part)
            {
                case PashupatiPart.Temple: return HeroId;
                case PashupatiPart.WestGate: return WestGateId;
                case PashupatiPart.EastGate: return EastGateId;
                case PashupatiPart.MuktiMandap: return MuktiMandapId;
                case PashupatiPart.SouthCourt: return SouthCourtId;
                default: return NorthCourtId;
            }
        }

        /// <summary>True for the site's hero ids.</summary>
        public static bool Handles(string id)
        {
            PashupatiPart p;
            return TryPart(id, out p);
        }

        public static bool TryPart(string id, out PashupatiPart part)
        {
            part = PashupatiPart.Temple;
            if (id == null) return false;
            for (int i = 0; i <= (int)PashupatiPart.NorthCourt; i++)
            {
                if (!string.Equals(id, IdOf((PashupatiPart)i), StringComparison.Ordinal)) continue;
                part = (PashupatiPart)i;
                return true;
            }
            return false;
        }

        /// <summary>The height of each part's highest point above the courtyard (its catalogue height).</summary>
        public static float TopOf(PashupatiPart part)
        {
            switch (part)
            {
                case PashupatiPart.Temple: return TempleTopM;
                case PashupatiPart.WestGate: return PashupatiGates.WestGateTopM;
                case PashupatiPart.EastGate: return PashupatiRing.EastWingTopM;
                case PashupatiPart.MuktiMandap: return PashupatiRing.MuktiTopM;
                case PashupatiPart.SouthCourt: return PashupatiShrines.OctagonTopM;
                default: return PashupatiShrines.AraucariaTopM;
            }
        }

        /// <summary>The bearing (degrees) a part's main door faces: the temple's west door, the West Gate's forecourt face,
        /// the East Gate's river face, the red shrine by the Mukti Mandap, the Bhairav temple's north door and the Vasuki
        /// temple's west door (both turned with their OSM squares).</summary>
        public static float DoorBearingOf(PashupatiPart part)
        {
            switch (part)
            {
                case PashupatiPart.Temple: return (float)MainDoorBearingDeg;
                case PashupatiPart.WestGate: return (float)MainDoorBearingDeg;
                case PashupatiPart.EastGate: return (float)AxisBearingDeg;
                case PashupatiPart.MuktiMandap: return (float)MainDoorBearingDeg;
                case PashupatiPart.SouthCourt: return (float)(AxisBearingDeg - 90 - PashupatiShrines.BhairavYaw * 180 / Math.PI + 360) % 360f;
                default: return (float)(MainDoorBearingDeg - PashupatiShrines.VasukiYaw * 180 / Math.PI);
            }
        }

        /// <summary>The temple centroid's longitude and latitude (the parts' anchor).</summary>
        public static void CentreLonLat(out double lon, out double lat)
        {
            WorldFrame.GameToLonLat(CentreX, CentreZ, out lon, out lat);
        }

        /// <summary>True when the temple centroid lies in the tile (the parts are built in the main temple's tile).</summary>
        public static bool InTile(TileData tile)
        {
            if (tile == null) return false;
            double x = CentreX - tile.Tile.X0, z = CentreZ - tile.Tile.Z0;
            return x >= 0 && z >= 0 && x < tile.Tile.Size && z < tile.Tile.Size;
        }

        /// <summary>
        /// Build one part of the site at a LOD into <paramref name="m"/> (tile-local, absolute heights) with its LOD0
        /// colliders; returns its triangles, 0 when the site is not in <paramref name="tile"/>.
        /// </summary>
        public static int Build(HeritageRecord r, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            PashupatiPart part;
            if (!TryPart(r.Id, out part) || !InTile(tile)) return 0;
            return Build(part, tile, lod, m, c, stats);
        }

        /// <summary>Build one part (see the other overload).</summary>
        public static int Build(PashupatiPart part, TileData tile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            lod = Math.Max(0, Math.Min(3, lod));
            stats?.Clear();
            int t0 = m.TriangleCount;
            ShapeAo.EnsureUv(m);
            m.HasUv0 = true;
            // The site reads its levelled ground from the frame (PashupatiGround.Prepared): the tile is never altered.
            PashupatiFrame f = PashupatiFrame.For(tile, lod, lod == 0 ? c : null);
            switch (part)
            {
                case PashupatiPart.Temple:
                    PashupatiTemple.Build(ref f, m, stats);
                    PashupatiNandi.Build(ref f, m);
                    PashupatiGround.CourtyardFloor(ref f, m);
                    PashupatiShrines.CourtyardFurniture(ref f, m);
                    break;
                case PashupatiPart.WestGate:
                    PashupatiGates.West(ref f, m);
                    PashupatiRing.WestBuildings(ref f, m);
                    PashupatiShrines.GreatTrident(ref f, m);
                    PashupatiGround.Forecourt(ref f, m);
                    break;
                case PashupatiPart.EastGate:
                    PashupatiGates.East(ref f, m);
                    PashupatiRing.EastWing(ref f, m);
                    break;
                case PashupatiPart.MuktiMandap:
                    PashupatiRing.MuktiMandap(ref f, m);
                    PashupatiRing.SouthEastBlock(ref f, m);
                    break;
                case PashupatiPart.SouthCourt:
                    PashupatiShrines.Bhairav(ref f, m);
                    PashupatiShrines.Octagon(ref f, m);
                    PashupatiShrines.KingsPillars(ref f, m);
                    PashupatiShrines.SouthShrines(ref f, m);
                    PashupatiRing.SouthRange(ref f, m);
                    PashupatiRing.PriestsQuarters(ref f, m);
                    break;
                default:
                    PashupatiShrines.Vasuki(ref f, m);
                    PashupatiShrines.SmallShrines(ref f, m);
                    PashupatiRing.NorthWing(ref f, m);
                    PashupatiShrines.Araucarias(ref f, m);
                    break;
            }
            if (stats != null)
            {
                stats.TopM = TopOf(part);
                stats.DoorYawDeg = DoorBearingOf(part);
                if (part == PashupatiPart.Temple)
                {
                    stats.Tiers = 2;
                    stats.PlinthLevels = 1;
                    stats.PlinthTopM = (float)PashupatiTemple.PlinthM;
                    // The sanctum core (OSM w1032559975, 8.7 × 8.3 m), never entered: across the main door (along b) and along it.
                    stats.SanctumHalfU = 4.35f;
                    stats.SanctumHalfW = 4.15f;
                    stats.SanctumV0 = (float)PashupatiTemple.PlinthM;
                    stats.SanctumV1 = 6.4f;
                }
            }
            return m.TriangleCount - t0;
        }
    }
}
