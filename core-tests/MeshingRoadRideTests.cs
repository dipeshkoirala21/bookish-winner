using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The rideability fix round of the roads (owner: smooth wide roads, nothing blocks you, underpasses work): junction
    /// caps at acute Y-junctions keep off the houses the pipeline trimmed to the corridors, footpaths pinned at tile-border
    /// cuts stay inside the final corridor, a lowered carriageway has no false hard edge at its polyline joints, and the
    /// retaining walls of a cutting never stand across a road that joins it.
    /// </summary>
    public class MeshingRoadRideTests
    {
        private static IEnumerable<TileData> LeafTiles()
        {
            foreach (TileId id in StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t != null && t.HasDetail && t.Roads.Count > 0) yield return t;
            }
        }

        /// <summary>Area (m², 0.5 m cells) of junction cap <paramref name="cap"/> lying inside a building footprint of the
        /// tile (the pipeline-trimmed outlines) by more than 0.1 m.</summary>
        private static double CapAreaOverHouses(TileData t, JunctionCap cap)
        {
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            for (int i = 0; i < cap.Count; i++)
            {
                minX = Math.Min(minX, cap.PolyX[i]);
                maxX = Math.Max(maxX, cap.PolyX[i]);
                minZ = Math.Min(minZ, cap.PolyZ[i]);
                maxZ = Math.Max(maxZ, cap.PolyZ[i]);
            }
            var near = new List<int[]>();
            foreach (BuildingRecord b in t.Buildings)
            {
                if (b.Rings == null || b.Rings.Length == 0 || b.Rings[0] == null || b.Rings[0].Length < 6) continue;
                int[] r = b.Rings[0];
                double bx0 = double.MaxValue, bz0 = double.MaxValue, bx1 = double.MinValue, bz1 = double.MinValue;
                for (int k = 0; k + 1 < r.Length; k += 2)
                {
                    bx0 = Math.Min(bx0, r[k] / 100.0);
                    bx1 = Math.Max(bx1, r[k] / 100.0);
                    bz0 = Math.Min(bz0, r[k + 1] / 100.0);
                    bz1 = Math.Max(bz1, r[k + 1] / 100.0);
                }
                if (bx1 < minX || bx0 > maxX || bz1 < minZ || bz0 > maxZ) continue;
                near.Add(r);
            }
            if (near.Count == 0) return 0;
            double area = 0;
            for (double x = minX + 0.25; x < maxX; x += 0.5)
            for (double z = minZ + 0.25; z < maxZ; z += 0.5)
            {
                if (!InPolygon(cap.PolyX, cap.PolyZ, cap.Count, x, z)) continue;
                foreach (int[] r in near)
                {
                    if (!InRing(r, x, z) || RingDistance(r, x, z) < 0.1) continue;
                    area += 0.25;
                    break;
                }
            }
            return area;
        }

        private static bool InPolygon(double[] px, double[] pz, int n, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((pz[i] > z) != (pz[j] > z) && x < (px[j] - px[i]) * (z - pz[i]) / (pz[j] - pz[i]) + px[i]) inside = !inside;
            return inside;
        }

        private static bool InRing(int[] r, double x, double z)
        {
            bool inside = false;
            int n = r.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = r[2 * i] / 100.0, zi = r[2 * i + 1] / 100.0, xj = r[2 * j] / 100.0, zj = r[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        private static double RingDistance(int[] r, double x, double z)
        {
            double best = double.MaxValue;
            int n = r.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ax = r[2 * j] / 100.0, az = r[2 * j + 1] / 100.0, bx = r[2 * i] / 100.0, bz = r[2 * i + 1] / 100.0;
                double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                double f = l2 > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                best = Math.Min(best, Math.Sqrt((ax + dx * f - x) * (ax + dx * f - x) + (az + dz * f - z) * (az + dz * f - z)));
            }
            return best;
        }

        /// <summary>The smallest angle (degrees) between two consecutive arms of a cap, from its node to each arm's cut.</summary>
        private static double MinArmAngle(TileData t, RoadLayout layout, JunctionCap cap)
        {
            int n = cap.ArmRoads.Length;
            var ang = new double[n];
            for (int k = 0; k < n; k++)
            {
                RoadCut c = layout.DrawnCutAt(t, cap.ArmRoads[k], cap.ArmCutS[k]);
                ang[k] = Math.Atan2(c.CZ - cap.Z, c.CX - cap.X) * 180.0 / Math.PI;
            }
            Array.Sort(ang);
            double min = 360;
            for (int k = 0; k < n; k++)
            {
                double d = (k + 1 < n ? ang[k + 1] : ang[0] + 360) - ang[k];
                min = Math.Min(min, d);
            }
            return min;
        }

        /// <summary>
        /// Junction caps never pave over the houses the pipeline trimmed to the corridors (finding: 269 caps over at least
        /// 2 m² of houses, 2 153 m² in all, up to 30 m from the node at acute Y-junctions of 4.8 m lanes): a kerb-return
        /// fillet only uses the corridor room beside the carriageways at its corner, and edges whose cuts lie beyond the
        /// corner meet at it instead of a chord across the wedge between the arms.
        /// </summary>
        [Test]
        public void JunctionCapsKeepOffTheTrimmedHouses()
        {
            int caps = 0, acute = 0, over1 = 0;
            double total = 0, worst = 0, worstAcute = 0;
            string where = "";
            foreach (TileData t in LeafTiles())
            {
                RoadLayout layout = RoadLayout.For(t);
                foreach (JunctionCap cap in layout.Caps)
                {
                    caps++;
                    double a = CapAreaOverHouses(t, cap);
                    total += a;
                    if (a >= 1.0) over1++;
                    if (a > worst)
                    {
                        worst = a;
                        where = t.Tile + " node " + cap.X.ToString("0.0") + "," + cap.Z.ToString("0.0");
                    }
                    if (MinArmAngle(t, layout, cap) < 45.0)
                    {
                        acute++;
                        worstAcute = Math.Max(worstAcute, a);
                    }
                }
            }
            TestContext.WriteLine("caps " + caps + " (" + acute + " with an arm pair under 45°); over houses " + total.ToString("0") + " m² in all, " +
                                  over1 + " caps over at least 1 m², worst " + worst.ToString("0.0") + " m² at " + where + ", worst acute " +
                                  worstAcute.ToString("0.0") + " m²");
            Assert.That(caps, Is.GreaterThan(5000));
            Assert.That(total, Is.LessThan(250.0), "all caps together over the trimmed houses (2 153 m² before)");
            Assert.That(over1, Is.LessThan(40), "caps over at least 1 m² of houses (703 before)");
            Assert.That(worst, Is.LessThan(6.0), "no cap paves a house (65.8 m² before)");
            Assert.That(worstAcute, Is.LessThan(5.0), "acute Y-junctions close at their corner, not across the wedge");
        }

        /// <summary>
        /// Footpaths never reach beyond the final corridor the houses were trimmed to, also where a tile-border cut pins them
        /// to the border cross-section (finding: 3.6 m footpaths beside a 14 m carriageway in a 14 m corridor at every tile
        /// crossing, 3 398 samples over by more than 0.3 m).
        /// </summary>
        [Test]
        public void FootpathsStayInsideTheFinalCorridor()
        {
            int samples = 0, over = 0, border = 0;
            double worst = 0;
            string where = "";
            foreach (TileData t in LeafTiles())
            {
                RoadLayout layout = RoadLayout.For(t);
                if (!layout.FinalCorridor) continue;
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    if (!RoadMesher.IsDrawn(t, i, null) || layout.IsPassage(i)) continue;
                    RoadRecord r = t.Roads[i];
                    RoadWidthProfile p = layout.Profiles[i];
                    for (int k = 0; k < p.Count; k++)
                    {
                        float lim = p.Limit[k];
                        if (float.IsPositiveInfinity(lim)) continue;
                        double s = k * (double)p.StepM;
                        bool nearCut = r.HasPrevContext && s < 80 || r.HasNextContext && s > p.LengthM - 80;
                        float shift = p.ShiftAt(s), cs = p.CorridorShiftAt(s), half = 0.5f * p.Drawn[k];
                        for (int side = 0; side < 2; side++)
                        {
                            float foot = side == 0 ? p.FootLeft[k] : p.FootRight[k];
                            if (foot <= 0f) continue;
                            samples++;
                            if (nearCut) border++;
                            // The footpath's outer edge against the corridor edge on its side (both from the centreline).
                            double edge = side == 0 ? shift + half + foot : -shift + half + foot;
                            double corridor = side == 0 ? cs + 0.5 * lim : -cs + 0.5 * lim;
                            double excess = edge - corridor;
                            if (excess <= 0.05) continue;
                            over++;
                            if (excess > worst)
                            {
                                worst = excess;
                                where = r.RoadClass + " way " + r.OsmWayId + " at " + s.ToString("0") + "/" + p.LengthM.ToString("0") + " m in " + t.Tile;
                            }
                        }
                    }
                }
            }
            TestContext.WriteLine("footpath samples " + samples + " (" + border + " within 80 m of a tile-border cut), beyond the corridor " + over +
                                  ", worst " + worst.ToString("0.00") + " m " + where);
            Assert.That(samples, Is.GreaterThan(10000));
            Assert.That(border, Is.GreaterThan(500), "the border-pinned footpaths are among them");
            Assert.That(over, Is.EqualTo(0), "no footpath beyond its corridor; worst " + worst.ToString("0.00") + " m " + where);
        }

        /// <summary>
        /// A lowered road (structure heights 6 m under the terrain) with a 40° bend: on the outside of the bend, inside the
        /// carriageway, the ground is the road and not the terrain beside it (finding: a 6.6 m wall at every joint because
        /// a point past a segment end counted as beside the road).
        /// </summary>
        [Test]
        public void LoweredRoadsHaveNoHardEdgeAtTheirJoints()
        {
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 516, 161), (x, z) => 1300.0, 129);
            // From (400, 400) east 60 m, then 40° to the north-east for 60 m (centimetres, tile-local).
            double bend = 40.0 * Math.PI / 180.0;
            int[] pts = { 40000, 40000, 46000, 40000, 46000 + (int)(6000 * Math.Cos(bend)), 40000 + (int)(6000 * Math.Sin(bend)) };
            t.Roads.Add(new RoadRecord { OsmWayId = 77, RoadClass = RoadClass.Residential, Surface = Surface.Asphalt, Points = pts });
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Urban });
            t.RoadStructures.Add(new RoadStructureRecord
            {
                Kind = RoadStructureKind.Underpass, Flags = RoadStructureFlags.Lowered | RoadStructureFlags.CarAccessible,
                DeckY = new[] { 1294f, 1294f, 1294f }, DeckRole = new[] { DeckPointRole.Ramp, DeckPointRole.Ramp, DeckPointRole.Ramp },
            });
            var g = new TileGroundQuery();
            g.Add(t);
            double vx = t.Tile.X0 + 460, vz = t.Tile.Z0 + 400;
            // The outside of the bend is to the south-east of the vertex: the bisector of the two right normals.
            double nx = Math.Sin(0) + Math.Sin(bend), nz = -Math.Cos(0) - Math.Cos(bend);
            double nl = Math.Sqrt(nx * nx + nz * nz);
            nx /= nl;
            nz /= nl;
            RoadSpatialIndex idx = g.RoadIndexOf(t.Tile);
            float half = idx.HalfWidthM(0);
            int checkedPoints = 0;
            for (double f = 0.2; f <= 0.9; f += 0.1)
            {
                double x = vx + nx * half * f, z = vz + nz * half * f;
                RoadHit hit;
                Assert.That(idx.TryNearest(x, z, 0.0, out hit), Is.True, "on the carriageway at " + f.ToString("0.0") + " of the half width");
                Assert.That(hit.EdgeDistanceM, Is.LessThanOrEqualTo(0f));
                GroundSample s;
                Assert.That(g.TrySample(x, z, 1294.1f, out s), Is.True);
                Assert.That(s.Height, Is.EqualTo(1294f).Within(0.3f), "the lowered road, not the terrain 6 m above, at " + f.ToString("0.0"));
                checkedPoints++;
            }
            Assert.That(checkedPoints, Is.GreaterThanOrEqualTo(7));
            // Past the piece's own end it still ends square.
            RoadHit end;
            Assert.That(idx.TryNearest(t.Tile.X0 + 400 - 1.0, t.Tile.Z0 + 400, 0.0, out end), Is.False, "the free end stays square");
        }
    }
}
