using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;

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
                if (float.IsNaN(recipe.YawDeg)) yaw = fyaw;
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
                    if (recipe.CompoundWall && lod <= 2) CompoundWall(recipe, f, w, d, m);
                    break;
                }
                case HeroForm.Stupa:
                    BuildStupa(recipe, f, lod, ref g, m, c, stats);
                    break;
                case HeroForm.ShikharaStone:
                case HeroForm.ShikharaPlaster:
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
                    ShikharaGenerator.Build(p, f, lod, m, c, stats);
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
            if (r.PlinthWidths == null && r.PlinthLevels > 0)
            {
                p.OutlineX = ox;
                p.OutlineZ = oz;
            }
            return p;
        }

        private static void BuildStupa(HeroRecipe r, in GenFrame f, int lod, ref RoadSurface g, MeshData m, GenColliders c, SacredStats stats)
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
            if (lod >= 3)
            {
                // Box terraces, a coarse dome and a frustum spire (≤ 200 triangles).
                KitFrame k = f.Kit;
                double top = 0;
                if (r.TerraceWidths != null)
                    for (int i = 0; i < r.TerraceWidths.Length; i++)
                    {
                        double hw = 0.5 * r.TerraceWidths[i], t1 = r.TerraceTops != null && i < r.TerraceTops.Length ? r.TerraceTops[i] : top + 1;
                        MeshKit.Box(m, k, -hw, hw, top, t1, -hw, hw, SacredPalette.Whitewash, BoxFaces.All & ~BoxFaces.Bottom);
                        top = t1;
                    }
                double rd = 0.5 * p.DomeDiameterM;
                MeshKit.Dome(m, f.X, f.Z, rd, f.GroundY + top, p.DomeRiseM > 0 ? p.DomeRiseM : 0.37 * p.DomeDiameterM, 8, 2, SacredPalette.Whitewash);
                double hb = top + (p.DomeRiseM > 0 ? p.DomeRiseM : 0.37 * p.DomeDiameterM);
                MeshKit.Frustum(m, f.X, f.Z, 0.12 * p.DomeDiameterM, f.GroundY + hb, 0.0, f.GroundY + r.HeightM, 4, false, SacredPalette.Gilt);
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
                SacredKit.Stair(m, c, k, -2.0, 2.0, start + i * Seg, Seg, v0, v1, lod, SacredPalette.StoneGrey, GenColliders.Stone);
                placed += n;
            }
            double vx, vy, vz;
            k.ToWorld(0, 0, start - 5.0, out vx, out vy, out vz);
            HeroForms.Vajra(new GenFrame(vx, vz, f.GroundY, 90), m);
            if (stats != null) stats.Steps = placed;
        }

        private static void CompoundWall(HeroRecipe r, in GenFrame f, double w, double d, MeshData m)
        {
            // Taleju (Kathmandu): the walled compound on the top plinth step, with a gate on the door side.
            KitFrame k = f.Kit;
            double top = r.PlinthLevels * r.StepRiseM;
            double inset = Math.Min(0.07 * w, Math.Max(0, (w - (r.CoreFrac * w + 1.2)) / (2.0 * Math.Max(1, r.PlinthLevels - 1))));
            double hw = 0.5 * w - (r.PlinthLevels - 1) * inset - 0.3, hd = hw * d / w;
            uint col = SacredPalette.BrickDachi;
            MeshKit.Box(m, k, -hw, -1.2, top, top + 2.2, hd - 0.4, hd, col, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, 1.2, hw, top, top + 2.2, hd - 0.4, hd, col, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, -hw, hw, top, top + 2.2, -hd, -hd + 0.4, col, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, -hw, -hw + 0.4, top, top + 2.2, -hd + 0.4, hd - 0.4, col, BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, hw - 0.4, hw, top, top + 2.2, -hd + 0.4, hd - 0.4, col, BoxFaces.All & ~BoxFaces.Bottom);
            SacredKit.Torana(m, k, 0, top + 2.2, hd + 0.01, 2.4, SacredPalette.Gilt, 1);
        }

        private static void KumariGhar(HeroRecipe r, in GenFrame f, double w, double d, int lod, MeshData m, GenColliders c, SacredStats stats)
        {
            double iu, iw;
            HeroForms.Courtyard((float)w, (float)d, 5.5f, r.HeightM, true, SacredPalette.BrickDachi, f, lod, m, c, out iu, out iw);
            // Carved windows on the front, closed: the Kumari is never shown.
            KitFrame k = f.Kit;
            int storeys = Math.Max(2, r.Storeys), windows = 0;
            double sH = r.HeightM / storeys;
            for (int s = 1; s < storeys; s++)
            {
                for (int b = 0; b < 5; b++)
                {
                    double u = -0.5 * w + w * (b + 0.5) / 5;
                    MeshKit.Box(m, k, u - 0.6, u + 0.6, s * sH + 0.4, s * sH + 1.6, 0.5 * d, 0.5 * d + 0.1, SacredPalette.WoodCarved, BoxFaces.Wall);
                    MeshKit.Panel(m, k, u - 0.45, s * sH + 0.5, u + 0.45, s * sH + 1.5, 0.5 * d + 0.105, MeshColor.Scale(SacredPalette.WoodCarved, 1.3f));
                    windows++;
                }
            }
            if (stats != null)
            {
                stats.TopM = r.HeightM + 0.15f;
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
            p.Guardians = GuardianSet.None;
            p.CoreFrac = 0.6f;
            var sf = new GenFrame(sx, sz, f.GroundY, f.YawDeg);
            PagodaGenerator.Build(p, sf, lod, m, c, stats);
            if (lod <= 2) ChaityaGenerator.Build(ChaityaParams.Defaults(2.5f), new GenFrame(f.X, f.Z, f.GroundY + 0.05f, f.YawDeg), lod, m, c);
        }

        private static void PeacockWindow(in GenFrame f, int lod, MeshData m)
        {
            KitFrame k = f.Kit;
            MeshKit.Box(m, k, -2.0, 2.0, -0.3, 4.0, -0.5, 0, MeshColor.FromHex(0xB4432F), BoxFaces.All & ~BoxFaces.Bottom);
            MeshKit.Box(m, k, -0.75, 0.75, 1.4, 2.6, 0, 0.12, SacredPalette.WoodCarved, BoxFaces.Wall);
            MeshKit.Panel(m, k, -0.6, 1.5, 0.6, 2.5, 0.125, MeshColor.Scale(SacredPalette.WoodCarved, 1.4f));
            int feathers = lod <= 1 ? 9 : 4;
            for (int q = 0; q < feathers; q++)
            {
                double a0 = Math.PI * q / feathers, a1 = Math.PI * (q + 1) / feathers;
                MeshKit.TriLocal(m, k, 0, 1.6, 0.13, 0.55 * Math.Cos(a0), 1.6 + 0.8 * Math.Sin(a0), 0.13, 0.55 * Math.Cos(a1), 1.6 + 0.8 * Math.Sin(a1), 0.13,
                                 0, 0, 1, q % 2 == 0 ? SacredPalette.Gilt : MeshColor.FromHex(0x2E6F5A));
            }
        }

        private static void Lod3(HeroRecipe r, in GenFrame f, double w, double d, MeshData m)
        {
            KitFrame k = f.Kit;
            double h = Math.Max(1.0, r.HeightM), wall = 0.55 * h;
            MeshKit.Box(m, k, -0.5 * w, 0.5 * w, -0.3, wall, -0.5 * d, 0.5 * d, SacredPalette.BrickDachi, BoxFaces.All & ~BoxFaces.Bottom);
            uint roof = r.Finish == RoofFinish.GiltAll ? SacredPalette.Gilt : r.Form == HeroForm.Dharahara ? SacredPalette.Whitewash : SacredPalette.Tile;
            MeshKit.Frustum(m, f.X, f.Z, 0.55 * Math.Sqrt(w * w + d * d), f.GroundY + wall, 0.0, f.GroundY + h, 4, false, roof);
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
