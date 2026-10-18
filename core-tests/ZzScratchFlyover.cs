using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    [TestFixture]
    public class ZzScratchFlyover
    {
        [Test, Explicit]
        public void Flyover522()
        {
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            s.DrawBuildings = false;
            s.DrawInstances = false;
            var id = new TileId(10, 522, 160);
            TileData t = StreamingSampleRegion.Tile(id);
            BridgeLayout layout = BridgeLayout.For(t);
            BridgeSpan flyover = null;
            foreach (BridgeSpan sp in layout.Spans)
                if (!sp.Foot && !sp.Water && !sp.Stair && sp.Kind == RoadStructureKind.Flyover && sp.Count >= 3) flyover = sp;
            TileGroundQuery ground = StreamingScene.Ground(s);
            TileBuild b = StreamingScene.Build(id, s, ground);
            ground.Add(id, b.Sampler, b.Roads);
            IBridgeDeckQuery decks = BridgeDeckIndex.ForTile(b.Source);
            TestContext.WriteLine("flyover " + flyover.Record.OsmWayId + " road " + flyover.Road + " count " + flyover.Count);
            for (int k = 1; k + 1 < flyover.Count; k++)
            {
                double gx = id.X0 + flyover.Path.X[k], gz = id.Z0 + flyover.Path.Z[k];
                float terrain, deckY, nx, ny, nz;
                b.Sampler.TryHeightClamped(gx, gz, out terrain);
                bool d = decks.TryDeck(gx, gz, flyover.Path.Y[k] + 0.3f, out deckY, out nx, out ny, out nz);
                GroundSample under;
                ground.TrySample(gx, gz, terrain + 0.5f, out under);
                RoadHit hit;
                bool h = ground.TryNearestRoad(gx, gz, RoadSpatialIndex.MaxFootpathM, RoadFlags.None, RoadFlags.None, RoadLayer.Ground, out hit);
                float sy = float.NaN, sg;
                if (h) b.Roads.TrySurfaceHeight(in hit, out sy, out sg);
                TestContext.WriteLine("   surf " + sy.ToString("F2") + " edge " + hit.EdgeDistanceM.ToString("F2") + " lat " + hit.LateralM.ToString("F2") + " half " + hit.HalfWidthM.ToString("F2") + " shift " + hit.CentreShiftM.ToString("F2") + " seg " + hit.Segment + " piece " + hit.PieceLengthM.ToString("F1"));
                TestContext.WriteLine("k " + k + " (" + gx.ToString("F1") + "," + gz.ToString("F1") + ") terrain " + terrain.ToString("F2") + " deck " + (d ? deckY.ToString("F2") : "-")
                    + " under " + under.Height.ToString("F2") + (under.OnDeck ? " deck" : "") + " nearest " + (h ? hit.Road.OsmWayId + " " + hit.Road.RoadClass + " on " + hit.OnRoad + "/" + hit.OnFootpath + " along " + hit.AlongM.ToString("F1") + " drawn " + layout.Draws(hit.RoadIndex, hit.Segment) + " covers " + layout.CoversAlong(hit.RoadIndex, hit.AlongM) : "-"));
            }
        }
    
        [Test, Explicit]
        public void Pieces()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10, false);
            s.DrawBuildings = false;
            s.DrawInstances = false;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0 || !t.HasRoadStructures) continue;
                if (BridgeLayout.For(t).Spans.Count == 0) continue;
                TileBuild b = StreamingScene.Build(id, s);
                BridgeLayout layout = BridgeLayout.For(b.Source);
                foreach (LodPiece p in b.Pieces)
                {
                    if (p.Layer != TileLayers.Bridges || p.Span == null) continue;
                    bool trench = layout.Trenches.Contains(p.Span);
                    BridgePath path = p.Span.Path;
                    int k = p.Span.Count / 2;
                    double ox = path.X[k] + path.Nx[k] * 150, oz = path.Z[k] + path.Nz[k] * 150;
                    double d = p.DistanceTo(ox, oz), centreline = BridgeMesher.Distance(p.Span, ox, oz);
                    if (d > centreline + 1e-9)
                        TestContext.WriteLine(id + " road " + p.Span.Road + " way " + p.Span.Record.OsmWayId + " trench " + trench + " d " + d.ToString("F2") + " centreline " + centreline.ToString("F2")
                            + " circle c (" + p.CX.ToString("F1") + "," + p.CZ.ToString("F1") + ") r " + p.RadiusM.ToString("F1") + " path " + path.Start.ToString("F1") + ".." + path.End.ToString("F1")
                            + " railL " + string.Join(",", p.Span.RailRunsL.ConvertAll(v => v.ToString("F1"))) + " railR " + string.Join(",", p.Span.RailRunsR.ConvertAll(v => v.ToString("F1"))));
                }
            }
        }
    }
}
