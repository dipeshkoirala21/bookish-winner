using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred
{
    /// <summary>
    /// Builds the hero replicas (W2_DESIGN 3.1, 3.4, 10.3) from heritage records: the anchor is found in the anchor
    /// tile (the BLDG way or relation, a POI node, or the curated lon/lat), the plan is the OSM outline itself when there
    /// is one (V4: within 0.1 m), the height, tiers, plinth levels and door yaw come from the record (or the
    /// <see cref="HeroCatalog"/> recipe) and are met exactly; cartoon latitude stays inside that envelope. Mesh positions
    /// are relative to the anchor tile's south-west corner with absolute Y, like every mesher. LOD3 is a box with a
    /// pyramid roof (a dome for stupas), ≤ 200 triangles. Sanctums are never entered or modelled inside.
    /// </summary>
    public static class HeroBuilder
    {
        public static int Build(HeritageRecord r, TileData anchorTile, int lod, MeshData m, GenColliders c)
        {
            return Build(r, anchorTile, lod, m, c, null);
        }

        /// <summary>Build a hero; returns its triangles, 0 when the anchor is not in this tile.</summary>
        public static int Build(HeritageRecord r, TileData anchorTile, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (anchorTile == null) throw new ArgumentNullException(nameof(anchorTile));
            if (m == null) throw new ArgumentNullException(nameof(m));
            HeroRecipe recipe = Merge(r);
            double x, z;
            int building;
            if (!TryLocate(r, recipe, anchorTile, out x, out z, out building)) return 0;
            stats?.Clear();
            int t0 = m.TriangleCount;
            IHeightSampler h = anchorTile.HeightsQ != null ? new TileHeightSampler(anchorTile) : (IHeightSampler)new FlatSampler();
            var g = new RoadSurface(anchorTile, h);
            int[] ring = building >= 0 ? anchorTile.Buildings[building].Rings[0] : null;
            double yaw = recipe.YawDeg;
            double w = recipe.PlanW, d = recipe.PlanD > 0 ? recipe.PlanD : recipe.PlanW;
            double[] ox = null, oz = null;
            if (ring != null)
            {
                double cx, cz, fyaw, fw, fd;
                BuildingFrontRecord front = BuildingFronts.For(anchorTile).Front(building);
                SacredSelector.Frame(ring, front.HasFront ? front.FrontEdge : -1, out cx, out cz, out fyaw, out fw, out fd);
                // The door is square to the measured walls: a recipe yaw (often a cardinal estimate) snaps to the
                // nearest axis of the OSM outline when it is within 30° of one.
                yaw = float.IsNaN(recipe.YawDeg) ? fyaw : SnapYaw(recipe.YawDeg, fyaw, 30);
                SacredSelector.Extents(ring, x, z, yaw, out w, out d);
                ox = new double[ring.Length / 2];
                oz = new double[ring.Length / 2];
                for (int i = 0; i < ox.Length; i++)
                {
                    ox[i] = ring[2 * i] / 100.0;
                    oz[i] = ring[2 * i + 1] / 100.0;
                }
                if (Polygon.SignedArea(ox, oz, ox.Length) < 0)
                {
                    Array.Reverse(ox);
                    Array.Reverse(oz);
                }
            }
            if (double.IsNaN(yaw)) yaw = 180;
            double ground = GroundUnder(ref g, x, z, yaw, w, d, ring);
            var f = new GenFrame(x, z, (float)ground, (float)yaw);

            if (lod >= 3 && recipe.Form != HeroForm.Stupa)
            {
                Lod3(recipe, f, w, d, m);
                if (stats != null)
                {
                    stats.TopM = recipe.HeightM;
                    stats.DoorYawDeg = (float)yaw;
                    stats.Tiers = recipe.Tiers;
                    stats.PlinthLevels = recipe.PlinthLevels;
                }
                return m.TriangleCount - t0;
            }

            switch (recipe.Form)
            {
                case HeroForm.Pagoda:
                case HeroForm.Mandapa:
                {
                    PagodaParams p = PagodaFor(recipe, w, d, ox, oz);
                    PagodaGenerator.Build(p, f, lod, m, c, stats);
                    if (recipe.CompoundWall && lod <= 2) CompoundWall(recipe, f, w, d, lod, m);
                    break;
                }
                case HeroForm.Stupa:
                    BuildStupa(recipe, f, lod, ref g, m, c, stats);
                    break;
                case HeroForm.ShikharaStone:
                case HeroForm.ShikharaPlaster:
                {
                    ShikharaGenerator.Build(ShikharaFor(recipe, w, d, ox, oz), f, lod, m, c, stats);
                    break;
                }
                case HeroForm.HouseTemple:
                    HeroForms.HouseTemple(new HouseTempleParams
                    {
                        W = (float)w, D = (float)d, TotalHeightM = recipe.HeightM, Storeys = Math.Max(2, recipe.Storeys), PlinthLevels = recipe.PlinthLevels,
                        PlinthRiseM = recipe.StepRiseM, GiltBalcony = recipe.GiltBalcony, Figures = recipe.Figures, Lions = true, RoofColour = recipe.RoofColour,
                    }, f, lod, m, c, stats);
                    break;
                case HeroForm.Relief:
                    HeroForms.Relief((float)w, (float)d, recipe.HeightM - 0.45f, f, lod, m, c, stats);
                    break;
                case HeroForm.Column:
                    HeroForms.Column(recipe.HeightM, false, f, lod, m, c, stats);
                    break;
                case HeroForm.PalaceTower:
                    HeroForms.PalaceTower((float)w, (float)d, Math.Max(1, recipe.Storeys), Math.Max(0, recipe.Tiers), recipe.HeightM, f, lod, m, c, stats);
                    break;
                case HeroForm.Dharahara:
                    HeroForms.Dharahara(recipe.HeightM, f, lod, m, c, stats);
                    break;
                case HeroForm.GateHanuman:
                case HeroForm.GateGolden:
                    HeroForms.Gate(recipe.Form == HeroForm.GateGolden, (float)w, recipe.HeightM, f, lod, m, c, stats);
                    break;
                case HeroForm.PalaceRana:
                case HeroForm.PalaceWindows:
                    HeroForms.Palace(recipe.Form == HeroForm.PalaceRana, (float)w, (float)d, recipe.HeightM, Math.Max(1, recipe.Storeys), recipe.Windows, f, lod, m, c, stats);
                    break;
                case HeroForm.BellPavilion:
                    HeroForms.BellPavilion((float)w, (float)d, recipe.HeightM, f, lod, m, c);
                    if (stats != null)
                    {
                        stats.TopM = recipe.HeightM;
                        stats.DoorYawDeg = (float)yaw;
                    }
                    break;
                case HeroForm.Hiti:
                    HeroForms.Hiti((float)w, (float)d, Math.Max(1, recipe.Spouts), true, f, lod, m, c);
                    if (stats != null)
                    {
                        stats.TopM = 0.5f;
                        stats.DoorYawDeg = (float)yaw;
                    }
                    break;
                case HeroForm.KumariGhar:
                    KumariGhar(recipe, f, w, d, lod, m, c, stats);
                    break;
                case HeroForm.GoldenTemple:
                    GoldenTemple(recipe, f, w, d, lod, m, c, stats);
                    break;
                case HeroForm.PeacockWindow:
                    PeacockWindow(f, lod, m);
                    if (stats != null)
                    {
                        stats.TopM = recipe.HeightM;
                        stats.DoorYawDeg = (float)yaw;
                    }
                    break;
            }
            return m.TriangleCount - t0;
        }

        /// <summary>The bearing among <paramref name="axisYaw"/> + k·90° closest to <paramref name="yaw"/>, when within
        /// <paramref name="maxDeg"/>; otherwise <paramref name="yaw"/> itself.</summary>
        public static double SnapYaw(double yaw, double axisYaw, double maxDeg)
        {
            double best = yaw, bestD = double.MaxValue;
            for (int q = 0; q < 4; q++)
            {
                double c = (axisYaw + 90 * q) % 360, dd = Math.Abs(c - yaw) % 360;
                if (dd > 180) dd = 360 - dd;
                if (dd < bestD)
                {
                    bestD = dd;
                    best = c;
                }
            }
            return bestD <= maxDeg ? best : yaw;
        }

        /// <summary>The catalog recipe of a record (or one synthesised from its kind) with the record's curated values
        /// applied.</summary>
        public static HeroRecipe Merge(HeritageRecord r)
        {
            HeroRecipe baseRecipe;
            HeroRecipe x;
            if (HeroCatalog.TryGet(r.Id, out baseRecipe))
            {
                x = baseRecipe.Clone();
            }
            else
            {
                x = new HeroRecipe { Id = r.Id, Name = r.Name, Osm = r.Osm, Kind = r.Kind, Form = FormOf(r.Kind), Tiers = 1, HeightM = 10f, PlanW = 8f, PlanD = 8f };
                if (x.Form == HeroForm.Stupa)
                {
                    x.DomeDiameterM = 10f;
                    x.Terraces = 1;
                }
            }
            if (!string.IsNullOrEmpty(r.Osm)) x.Osm = r.Osm;
            if (r.Tiers > 0) x.Tiers = r.Tiers;
            if (r.PlinthLevels > 0) x.PlinthLevels = r.PlinthLevels;
            if (r.Doors > 0) x.Doors = r.Doors;
            if (!float.IsNaN(r.HeightM) && r.HeightM > 0) x.HeightM = r.HeightM;
            if (!float.IsNaN(r.YawDeg)) x.YawDeg = r.YawDeg;
            if (!float.IsNaN(r.PlanWM) && r.PlanWM > 0) x.PlanW = r.PlanWM;
            if (!float.IsNaN(r.PlanDM) && r.PlanDM > 0) x.PlanD = r.PlanDM;
            if (!double.IsNaN(r.Lat) && !double.IsNaN(r.Lon))
            {
                x.Lat = r.Lat;
                x.Lon = r.Lon;
            }
            float[] tw = r.AttrFloats("terrace_widths_m"), tt = r.AttrFloats("terrace_tops_m");
            if (tw != null) x.TerraceWidths = tw;
            if (tt != null) x.TerraceTops = tt;
            float v = r.AttrFloat("dome_m", float.NaN);
            if (!float.IsNaN(v)) x.DomeDiameterM = v;
            v = r.AttrFloat("drum_m", float.NaN);
            if (!float.IsNaN(v)) x.DrumDiameterM = v;
            v = r.AttrFloat("harmika_m", float.NaN);
            if (!float.IsNaN(v)) x.HarmikaWM = v;
            v = r.AttrFloat("step_rise_m", float.NaN);
            if (!float.IsNaN(v)) x.StepRiseM = v;
            switch (r.Finish)
            {
                case HeritageFinish.GiltAll: x.Finish = RoofFinish.GiltAll; break;
                case HeritageFinish.GiltTop: x.Finish = RoofFinish.GiltTop; break;
                case HeritageFinish.Tile: x.Finish = RoofFinish.Tile; break;
            }
            return x;
        }

        private static HeroForm FormOf(HeritageKind k)
        {
            switch (k)
            {
                case HeritageKind.Stupa: return HeroForm.Stupa;
                case HeritageKind.ShikharaStone: return HeroForm.ShikharaStone;
                case HeritageKind.ShikharaPlaster: return HeroForm.ShikharaPlaster;
                case HeritageKind.HouseTemple: return HeroForm.HouseTemple;
                case HeritageKind.Mandapa: return HeroForm.Mandapa;
                case HeritageKind.Relief: return HeroForm.Relief;
                case HeritageKind.Column: return HeroForm.Column;
                case HeritageKind.Gate: return HeroForm.GateHanuman;
                case HeritageKind.Palace: return HeroForm.PalaceRana;
                case HeritageKind.Tower: return HeroForm.PalaceTower;
                case HeritageKind.Hiti: return HeroForm.Hiti;
                case HeritageKind.Bahal: return HeroForm.KumariGhar;
                default: return HeroForm.Pagoda;
            }
        }

        /// <summary>Find the hero's anchor in a tile (tile-local metres): its building (way or relation), its POI node,
        /// or the curated lon/lat when it falls inside the tile.</summary>
        public static bool TryLocate(HeritageRecord r, HeroRecipe recipe, TileData t, out double x, out double z, out int buildingIndex)
        {
            x = z = 0;
            buildingIndex = -1;
            string osm = !string.IsNullOrEmpty(r.Osm) ? r.Osm : recipe != null ? recipe.Osm : null;
            ulong wr = r.FootprintRef != 0 ? r.FootprintRef : HeritageRecord.WayRelationRef(osm);
            if (wr != 0)
            {
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    if (t.Buildings[i].OsmRef != wr) continue;
                    double cx, cz, yaw, w, d;
                    SacredSelector.Frame(t.Buildings[i].Rings[0], -1, out cx, out cz, out yaw, out w, out d);
                    x = cx;
                    z = cz;
                    buildingIndex = i;
                    return true;
                }
            }
            ulong nwr = r.AnchorRef != 0 ? r.AnchorRef : HeritageRecord.NwrRef(osm);
            if (nwr != 0)
            {
                foreach (PoiRecord p in t.Pois)
                {
                    if (p.OsmRef != nwr) continue;
                    x = p.XCm / 100.0;
                    z = p.ZCm / 100.0;
                    return true;
                }
            }
            if (!double.IsNaN(r.X) && !double.IsNaN(r.Z))
            {
                x = r.X - t.Tile.X0;
                z = r.Z - t.Tile.Z0;
                return x >= 0 && z >= 0 && x < t.Tile.Size && z < t.Tile.Size;
            }
            double lat = recipe != null ? recipe.Lat : r.Lat, lon = recipe != null ? recipe.Lon : r.Lon;
            if (double.IsNaN(lat) || double.IsNaN(lon)) return false;
            double gx, gz;
            WorldFrame.LonLatToGame(lon, lat, out gx, out gz);
            x = gx - t.Tile.X0;
            z = gz - t.Tile.Z0;
            return x >= 0 && z >= 0 && x < t.Tile.Size && z < t.Tile.Size;
        }

        private static double GroundUnder(ref RoadSurface g, double x, double z, double yaw, double w, double d, int[] ring)
        {
            double ground = g.Height(x, z);
            if (ring != null)
            {
                for (int i = 0; i < ring.Length / 2; i++) ground = Math.Min(ground, g.Height(ring[2 * i] / 100.0, ring[2 * i + 1] / 100.0));
                return ground;
            }
            KitFrame k = KitFrame.FromYaw(x, 0, z, yaw);
            for (int q = 0; q < 4; q++)
            {
                double px, py, pz;
                k.ToWorld((q % 2 == 0 ? -0.5 : 0.5) * w, 0, (q < 2 ? -0.5 : 0.5) * d, out px, out py, out pz);
                ground = Math.Min(ground, g.Height(px, pz));
            }
            return ground;
        }

        internal static PagodaParams PagodaFor(HeroRecipe r, double w, double d, double[] ox, double[] oz)
        {
            PagodaParams p = PagodaParams.Defaults((float)w, (float)d, Math.Max(1, r.Tiers));
            p.PlinthLevels = r.PlinthLevels;
            if (r.StepRiseM > 0) p.StepRiseM = r.StepRiseM;
            p.TotalHeightM = r.HeightM;
            p.Finish = r.Finish;
            p.Doors = (byte)Math.Max(1, r.Doors);
            p.Pataka = r.Pataka;
            p.Guardians = r.Guardians;
            p.NoStair = r.NoStair;
            p.Open = r.Form == HeroForm.Mandapa;
            if (r.CoreFrac > 0) p.CoreFrac = r.CoreFrac;
            if (r.TierShrink > 0) p.TierShrink = r.TierShrink;
            p.PlinthWidths = r.PlinthWidths;
            p.EaveWidths = r.EaveWidths;
            p.EaveHeights = r.EaveHeights;
            p.RoofColour = r.RoofColour;
            p.PlinthColour = r.PlinthColour;
            p.VahanaDistM = r.VahanaDistM;
            p.VahanaGaruda = r.VahanaGaruda;
            p.Hero = true;
            p.Rich = r.Centrepiece;
            p.Ambulatory = r.Ambulatory;
            p.Fringe = r.Fringe;
            p.GajurBell = r.GajurBell;
            p.PlasterUpper = r.PlasterUpper;
            p.Balcony = r.Balcony;
            p.PaintedGuardians = r.PaintedGuardians;
            p.WhiteStair = r.WhiteStair;
            p.FrontBell = r.FrontBell;
            p.LampPillars = r.LampPillars;
            if (r.StrutPitchM > 0) p.StrutPitchM = r.StrutPitchM;
            if (r.PitchBottomDeg > 0) p.PitchBottomDeg = r.PitchBottomDeg;
            if (r.PitchTopDeg > 0) p.PitchTopDeg = r.PitchTopDeg;
            if (r.GajurFrac > 0) p.GajurFrac = r.GajurFrac;
            p.UpperWallFrac = r.UpperWallFrac;
            if (r.PlinthLevels > 0)
            {
                p.OutlineX = ox;
                p.OutlineZ = oz;
            }
            return p;
        }

        /// <summary>The shikhara parameters of a recipe on a plan of w × d (and its OSM outline, if any).</summary>
        internal static ShikharaParams ShikharaFor(HeroRecipe recipe, double w, double d, double[] ox, double[] oz)
        {
            bool stone = recipe.Form == HeroForm.ShikharaStone;
            ShikharaParams p = stone ? ShikharaParams.StoneDefaults((float)w, (float)d) : ShikharaParams.PlasterDefaults((float)w, (float)d);
            p.TotalHeightM = recipe.HeightM;
            if (recipe.PlinthLevels > 0) p.PlinthLevels = recipe.PlinthLevels;
            if (recipe.StepRiseM > 0) p.StepRiseM = recipe.StepRiseM;
            if (stone)
            {
                p.PavilionsStorey1 = recipe.Pavilions1;
                p.PavilionsStorey2 = recipe.Pavilions2;
            }
            p.OutlineX = ox;
            p.OutlineZ = oz;
            return p;
        }

        /// <summary>The stupa parameters of a recipe.</summary>
        internal static StupaParams StupaFor(HeroRecipe r)
        {
            StupaParams p = StupaParams.Defaults(r.DomeDiameterM > 0 ? r.DomeDiameterM : 0.6f * r.PlanW, r.Terraces);
            p.TerraceWidths = r.TerraceWidths;
            p.TerraceTops = r.TerraceTops;
            if (r.DrumDiameterM > 0) p.DrumDiameterM = r.DrumDiameterM;
            p.DomeRiseM = r.DomeRiseM;
            p.HarmikaHM = r.HarmikaHM;
            if (r.HarmikaWM > 0) p.HarmikaWFrac = r.HarmikaWM / p.DomeDiameterM;
            p.SpireHM = r.SpireHM;
            p.ParasolHM = r.ParasolHM;
            p.FinialHM = r.FinialHM;
            p.FlagLines = r.FlagLines;
            p.WheelNiches = r.WheelNiches;
            p.BuddhaNiches = r.BuddhaNiches;
            p.TotalHeightM = r.HeightM;
            p.Style = r.StupaStyle;
            p.GateDistM = r.GateDistM;
            return p;
        }

        private static void BuildStupa(HeroRecipe r, in GenFrame f, int lod, ref RoadSurface g, MeshData m, GenColliders c, SacredStats stats)
        {
            StupaParams p = StupaFor(r);
            if (lod >= 3)
            {
                // Box terraces, a coarse dome and a cone spire (≤ 200 triangles).
                int v0 = m.VertexCount, i0 = m.IndexCount;
                Affine3 k = SacredDraw.Xf(f.Kit);
                double top = 0;
                if (r.TerraceWidths != null)
                    for (int i = 0; i < r.TerraceWidths.Length; i++)
                    {
                        double hw = 0.5 * r.TerraceWidths[i], t1 = r.TerraceTops != null && i < r.TerraceTops.Length ? r.TerraceTops[i] : top + 1;
                        SacredDraw.Box(m, k, -hw, hw, top, t1, -hw, hw, Looks.Whitewash, BoxFaces.All & ~BoxFaces.Bottom);
                        top = t1;
                    }
                double rd = 0.5 * p.DomeDiameterM, rise = p.DomeRiseM > 0 ? p.DomeRiseM : 0.37 * p.DomeDiameterM;
                Shapes.Dome(m, SacredDraw.At(k, 0, top, 0), Looks.Whitewash, rd, rise, 8);
                Shapes.Cone(m, SacredDraw.At(k, 0, top + rise, 0), Looks.Gilt, 0.12 * p.DomeDiameterM, r.HeightM - top - rise, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.GroundY);
                if (stats != null)
                {
                    stats.TopM = r.HeightM;
                    stats.Terraces = r.Terraces;
                    stats.DoorYawDeg = f.YawDeg;
                }
                return;
            }
            StupaGenerator.Build(p, f, lod, m, c, stats);
            if (r.EastStair365) SwayambhuStair(r, f, lod, ref g, m, c, stats);
        }

        /// <summary>
        /// Swayambhu's eastern stairway of 365 steps: from the platform edge it runs 260 m east down the hill; the
        /// terrain is sampled every 10 m and each 10 m flight gets the share of the 365 steps its drop needs, so the
        /// constant rise is computed from the real hill (≈ 0.19 m) and the stair always lands on the hilltop. The gilt
        /// vajra stands at its top.
        /// </summary>
        private static void SwayambhuStair(HeroRecipe r, in GenFrame f, int lod, ref RoadSurface g, MeshData m, GenColliders c, SacredStats stats)
        {
            const int Steps = 365;
            const double Length = 260.0, Seg = 10.0;
            KitFrame k = KitFrame.FromYaw(f.X, f.GroundY, f.Z, 90);
            double start = 0.5 * r.DomeDiameterM + 12.0;
            int segs = (int)(Length / Seg);
            var heights = new double[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                double x, y, z;
                k.ToWorld(0, 0, start + i * Seg, out x, out y, out z);
                heights[i] = i == 0 ? f.GroundY : g.Height(x, z);
            }
            double drop = heights[0] - heights[segs];
            if (drop < 5) return;
            double rise = drop / Steps;
            int placed = 0;
            for (int i = 0; i < segs; i++)
            {
                double hi = heights[i], lo = Math.Min(hi, heights[i + 1]);
                int n = i == segs - 1 ? Steps - placed : (int)Math.Round((heights[0] - lo) / rise) - placed;
                if (n <= 0) continue;
                double v1 = heights[0] - placed * rise - f.GroundY, v0 = v1 - n * rise;
                SacredParts.Stair(m, c, k, SacredDraw.Xf(k), 2.0, start + i * Seg, Seg, v0, v1, lod <= 1 ? 0.5 : 0, 0.6, Looks.Stone, Looks.StoneLight, lod, GenColliders.Stone);
                placed += n;
            }
            double vx, vy, vz;
            k.ToWorld(0, 0, start - 5.0, out vx, out vy, out vz);
            HeroForms.Vajra(new GenFrame(vx, vz, f.GroundY, 90), lod, m);
            if (stats != null) stats.Steps = placed;
        }

        private static void CompoundWall(HeroRecipe r, in GenFrame f, double w, double d, int lod, MeshData m)
        {
            // Taleju (Kathmandu): the red walled compound on the top plinth step with its white cornice, the gilt torana
            // gate flanked by painted lions, and the small single-roof shrines at its corners (ref_temples 0).
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            double top = r.PlinthLevels * r.StepRiseM;
            double inset = Math.Min(0.07 * w, Math.Max(0, (w - (r.CoreFrac * w + 1.2)) / (2.0 * Math.Max(1, r.PlinthLevels - 1))));
            double topW = r.PlinthWidths != null && r.PlinthWidths.Length >= r.PlinthLevels ? r.PlinthWidths[r.PlinthLevels - 1] : w - 2 * (r.PlinthLevels - 1) * inset;
            double hw = 0.5 * topW - 0.6, hd = hw * d / w, wt = 0.45, wh = 2.6;
            ShapeBrush red = Looks.Of(MeshColor.FromHex(0xB0352A), MaterialChannel.Plaster), white = Looks.Whitewash;
            // Four wall runs (the front split by the gate).
            double gate = 1.4;
            WallRun(m, k, -hw, -gate, hd - wt, hd, top, wh, red, white, lod);
            WallRun(m, k, gate, hw, hd - wt, hd, top, wh, red, white, lod);
            WallRun(m, k, -hw, hw, -hd, -hd + wt, top, wh, red, white, lod);
            WallRun(m, k, -hw, -hw + wt, -hd + wt, hd - wt, top, wh, red, white, lod);
            WallRun(m, k, hw - wt, hw, -hd + wt, hd - wt, top, wh, red, white, lod);
            SacredDraw.Box(m, k, -gate, gate, top + 2.3, top + wh + 0.5, hd - wt, hd, white, BoxFaces.All & ~BoxFaces.Bottom);
            SacredParts.Torana(m, k, 0, top + 2.3, hd + 0.01, 2.6, Looks.Gilt, lod);
            if (lod <= 1)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    SacredParts.Pedestal(m, k, sgn * (gate + 0.8), top, hd + 0.6, 0.35, 0.42, 0.55, Looks.Stone, lod);
                    SacredFigures.Guardian(m, k, sgn * (gate + 0.8), top + 0.55, hd + 0.6, 1.3, GuardianKind.Lion, true, true, lod);
                }
                for (int q = 0; q < 4; q++)
                {
                    double cu = (q % 2 == 0 ? 1 : -1) * (hw - 1.4), cw = (q < 2 ? 1 : -1) * (hd - 1.4), x, y, z;
                    f.Kit.ToWorld(cu, top, cw, out x, out y, out z);
                    ShrineGenerator.Build(new ShrineParams { Kind = ShrineKind.Generic, Form = ShrineForm.MiniPagoda, LongSideM = 2.4f, ShortSideM = 2.4f },
                                          new GenFrame(x, z, (float)y, f.YawDeg), 1, m, null);
                }
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
        }

        private static void WallRun(MeshData m, in Affine3 k, double u0, double u1, double w0, double w1, double v, double h, in ShapeBrush wall, in ShapeBrush cap, int lod)
        {
            double cu = 0.5 * (u0 + u1), cw = 0.5 * (w0 + w1), hu = 0.5 * (u1 - u0), hw = 0.5 * (w1 - w0);
            Mould p = SacredDraw.M;
            p.Add(0, v, wall).Add(0, v + h - 0.35).Add(0.08, v + h - 0.35, cap).Add(0.14, v + h - 0.12).Add(0.14, v + h);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(cu, cw, hu, hw, pu, pw);
            SacredDraw.Ring(m, k, pu, pw, n, p);
            SacredDraw.CapRect(m, k, cu, cw, hu + 0.14, hw + 0.14, v + h, true, cap);
        }

        private static void KumariGhar(HeroRecipe r, in GenFrame f, double w, double d, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double iu, iw;
            HeroForms.Courtyard((float)w, (float)d, 5.5f, r.HeightM - 1.6f, true, SacredPalette.BrickDachi, f, lod, m, c, out iu, out iw);
            // The richly carved front windows, closed: the Kumari is never shown.
            Affine3 k = SacredDraw.Xf(f.Kit);
            int storeys = Math.Max(2, r.Storeys), windows = 0;
            double sH = (r.HeightM - 1.6) / storeys;
            for (int s = 1; s < storeys && lod <= 1; s++)
            {
                for (int b = 0; b < 5; b++)
                {
                    double u = -0.5 * w + w * (b + 0.5) / 5;
                    if (Math.Abs(u) < 1.3) continue;
                    SacredParts.LatticeWindow(m, k, u, s * sH + 1.0, 0.5 * d + 0.02, 1.1, 1.2, Looks.WoodDark, lod, 3);
                    windows++;
                }
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
            if (stats != null)
            {
                stats.TopM = r.HeightM;
                stats.Windows = windows;
                stats.DoorYawDeg = f.YawDeg;
            }
        }

        private static void GoldenTemple(HeroRecipe r, in GenFrame f, double w, double d, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            double iu, iw;
            HeroForms.Courtyard((float)w, (float)d, 5f, 6.5f, true, SacredPalette.BrickDachi, f, lod, m, c, out iu, out iw);
            // The three-tier gilt shrine rises from the back (west) range and faces the gate across the court.
            KitFrame k = f.Kit;
            double sx, sy, sz;
            k.ToWorld(0, 0, -iw - 1.5, out sx, out sy, out sz);
            PagodaParams p = PagodaParams.Defaults(8f, 8f, Math.Max(1, r.Tiers));
            p.TotalHeightM = r.HeightM;
            p.Finish = r.Finish == RoofFinish.Tile ? RoofFinish.GiltAll : r.Finish;
            p.PlinthLevels = 1;
            p.StepRiseM = 0.5f;
            p.Pataka = true;
            p.Guardians = GuardianSet.Elephants;
            p.CoreFrac = 0.6f;
            p.Ambulatory = false;
            p.Hero = true;
            var sf = new GenFrame(sx, sz, f.GroundY, f.YawDeg);
            PagodaGenerator.Build(p, sf, lod, m, c, stats);
            if (lod <= 1) ChaityaGenerator.Build(ChaityaParams.Defaults(2.5f), new GenFrame(f.X, f.Z, f.GroundY + 0.05f, f.YawDeg), lod, m, c);
        }

        private static void PeacockWindow(in GenFrame f, int lod, MeshData m)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            SacredParts.BandedWall(m, SacredDraw.At(k, 0, 0, -0.25), 2.0, 0.25, -0.3, 4.0, Looks.Of(MeshColor.FromHex(0xB4432F), MaterialChannel.Brick), Looks.WoodDark, lod, false, true);
            SacredDraw.CapRect(m, k, 0, -0.25, 2.0, 0.25, 4.0, true, Looks.PlinthCoping);
            SacredParts.LatticeWindow(m, k, 0, 2.0, 0.0, 1.2, 1.1, Looks.WoodDark, 2, 0);
            // The fanned peacock: a fan of carved feathers and the bird's body and head.
            int feathers = lod <= 1 ? 11 : 5;
            for (int q = 0; q < feathers; q++)
            {
                double a0 = Math.PI * q / feathers, a1 = Math.PI * (q + 1) / feathers;
                ShapeBrush b = q % 2 == 0 ? Looks.Of(MeshColor.FromHex(0x6B4129), MaterialChannel.WoodCarved) : Looks.WoodDark;
                SacredDraw.Tri(m, k, 0, 1.6, 0.06, 0.55 * Math.Cos(a0), 1.6 + 0.75 * Math.Sin(a0), 0.04, 0.55 * Math.Cos(a1), 1.6 + 0.75 * Math.Sin(a1), 0.04, 0, 0, 1, b);
                if (lod <= 1) Shapes.Sphere(m, SacredDraw.At(k, 0.42 * Math.Cos(0.5 * (a0 + a1)), 1.6 + 0.58 * Math.Sin(0.5 * (a0 + a1)), 0.07), Looks.WoodMid, 0.04, 4);
            }
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 1.75, 0.1), Looks.WoodMid, 0.1, 0.18, 0.07, 6, false);
            Shapes.Ellipsoid(m, SacredDraw.At(k, 0, 2.02, 0.12), Looks.WoodMid, 0.05, 0.06, 0.05, 4, false);
            SacredDraw.Bake(m, v0, i0, f.GroundY);
        }

        private static void Lod3(HeroRecipe r, in GenFrame f, double w, double d, MeshData m)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = SacredDraw.Xf(f.Kit);
            double h = Math.Max(1.0, r.HeightM), wall = 0.55 * h;
            bool white = r.Form == HeroForm.Dharahara || r.Form == HeroForm.ShikharaPlaster || r.Form == HeroForm.PalaceRana;
            ShapeBrush body = white ? Looks.Whitewash : r.Form == HeroForm.ShikharaStone ? Looks.StoneBuff : Looks.WallGlazed;
            SacredDraw.Box(m, k, -0.5 * w, 0.5 * w, -0.3, wall, -0.5 * d, 0.5 * d, body, BoxFaces.All & ~BoxFaces.Bottom);
            ShapeBrush roof = r.Finish == RoofFinish.GiltAll ? Looks.Gilt : white || r.Form == HeroForm.ShikharaStone ? body : Looks.Tile;
            double hu = 0.6 * w, hw = 0.6 * d;
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, ou, ow;
                SacredParts.OnRect(s, -1, hu, hw, out au, out aw);
                SacredParts.OnRect(s, 1, hu, hw, out bu, out bw);
                SacredParts.Out(s, out ou, out ow);
                SacredDraw.Tri(m, k, au, wall, aw, bu, wall, bw, 0, h, 0, ou, 1, ow, roof);
            }
            SacredDraw.Bake(m, v0, i0, f.GroundY);
        }

        private sealed class FlatSampler : IHeightSampler
        {
            public bool TryHeight(double x, double z, out float h)
            {
                h = 0f;
                return true;
            }
        }
    }
}
