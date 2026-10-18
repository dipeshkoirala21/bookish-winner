using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Boudha;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class BoudhaScratchTmp
    {
        [Test]
        public void MeasureParts()
        {
            if (Environment.GetEnvironmentVariable("BOUDHA_MEASURE") == null) Assert.Pass();
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 521, 163));
            HeroBuilder.PrepareGround(t);
            HeroRecipe recipe;
            HeroCatalog.TryGet(BoudhaSite.HeroId, out recipe);
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(HeroCatalog.ToRecord(recipe), t, out f), Is.True);
            var sb = new StringBuilder();
            for (int lod = 0; lod < 3; lod++)
            {
                sb.Append("LOD" + lod + "\n");
                var m = new MeshData();
                int a = m.TriangleCount;
                BoudhaStupa.Build(m, null, f, lod, null);
                sb.Append(" stupa core+terraces " + (m.TriangleCount - a) + "\n");
                a = m.TriangleCount;
                BoudhaTerraces.Build(m, null, f, lod);
                sb.Append(" terrace dressing " + (m.TriangleCount - a) + "\n");
                foreach (BoudhaPart p in new[] { BoudhaPart.North, BoudhaPart.East, BoudhaPart.South, BoudhaPart.West })
                {
                    a = m.TriangleCount;
                    BoudhaWall.Build(m, null, f, p, lod);
                    sb.Append(" wall " + p + " " + (m.TriangleCount - a) + "\n");
                    a = m.TriangleCount;
                    BoudhaGompas.Build(m, null, t, f, p, lod);
                    sb.Append(" gompas " + p + " " + (m.TriangleCount - a) + "\n");
                    for (int i = 0; i < BoudhaRing.Houses.Length; i++)
                    {
                        RingHouse h = BoudhaRing.Houses[i];
                        if (h.Part != p || h.Look >= HouseLook.GuruLhakhang) continue;
                        double[] pu, pv;
                        int n = BoudhaRing.Footprint(t, f, h.Way, out pu, out pv);
                        if (n < 3) { sb.Append("  house " + h.Way + " MISSING\n"); continue; }
                        a = m.TriangleCount;
                        BoudhaRing.House(m, null, t, f, h, pu, pv, n, lod, i);
                        sb.Append("  house " + h.Way + " n" + n + " st" + h.Storeys + " " + (m.TriangleCount - a) + "\n");
                    }
                }
                a = m.TriangleCount;
                BoudhaNorth.Build(m, null, f, lod);
                sb.Append(" north entrance " + (m.TriangleCount - a) + "\n");
                a = m.TriangleCount;
                BoudhaGate.Build(m, null, t, f, lod);
                sb.Append(" gate " + (m.TriangleCount - a) + "\n");
            }
            sb.Append("PARTS\n");
            foreach (HeroRecipe pr in BoudhaSite.Parts())
            {
                HeritageRecord rr = HeroCatalog.ToRecord(pr);
                sb.Append(" " + pr.Id);
                for (int lod = 0; lod < 4; lod++)
                {
                    var mm = new MeshData();
                    int n = BoudhaSite.Build(rr, t, lod, mm, null, null);
                    sb.Append(" " + n + "/" + pr.Budget(lod));
                }
                sb.Append("\n");
            }
            {
                sb.Append(" " + recipe.Id);
                for (int lod = 0; lod < 4; lod++)
                {
                    var mm = new MeshData();
                    int n = BoudhaSite.Build(HeroCatalog.ToRecord(recipe), t, lod, mm, null, null);
                    sb.Append(" " + n + "/" + recipe.Budget(lod));
                }
                sb.Append("\n");
            }
            File.WriteAllText(Environment.GetEnvironmentVariable("BOUDHA_MEASURE"), sb.ToString());
        }

        private static int Call(Type t, string name, object[] args)
        {
            var mi = t.GetMethod(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi == null) return -1;
            var m = (MeshData)args[0];
            int a = m.TriangleCount;
            mi.Invoke(null, args);
            return m.TriangleCount - a;
        }

        [Test]
        public void MeasureSub()
        {
            if (Environment.GetEnvironmentVariable("BOUDHA_SUB") == null) Assert.Pass();
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 521, 163));
            HeroBuilder.PrepareGround(t);
            HeroRecipe recipe;
            HeroCatalog.TryGet(BoudhaSite.HeroId, out recipe);
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(HeroCatalog.ToRecord(recipe), t, out f), Is.True);
            Affine3 k = f.Site, I = Affine3.Identity;
            Type st = typeof(BoudhaStupa), te = typeof(BoudhaTerraces), no = typeof(BoudhaNorth), go = typeof(BoudhaGompas), qu = typeof(BoudhaQuarter),
                 wa = typeof(BoudhaWall), ki = typeof(BoudhaKit), ga = typeof(BoudhaGate);
            ShapeBrush red = new ShapeBrush(MeshColor.FromHex(0xB3261E), MaterialChannel.Paint, 1f);
            var rows = new (string, Type, string, Func<MeshData, int, object[]>)[]
            {
                ("stupa.Terraces", st, "Terraces", (m, l) => new object[] { m, null, f, l }),
                ("stupa.Stairs", st, "Stairs", (m, l) => new object[] { m, null, f, l }),
                ("stupa.Ring", st, "Ring", (m, l) => new object[] { m, k, l }),
                ("stupa.Dome", st, "Dome", (m, l) => new object[] { m, k, l }),
                ("stupa.Harmika", st, "Harmika", (m, l) => new object[] { m, k, l }),
                ("stupa.Spire", st, "Spire", (m, l) => new object[] { m, k, l }),
                ("stupa.Crown", st, "Crown", (m, l) => new object[] { m, k, l }),
                ("stupa.Flags", st, "Flags", (m, l) => new object[] { m, k, l }),
                ("terr.CompoundStairs", te, "CompoundStairs", (m, l) => new object[] { m, null, f, l }),
                ("terr.MiniBoudha x1", te, "MiniBoudha", (m, l) => new object[] { m, I, l }),
                ("terr.Elephant x1", te, "Elephant", (m, l) => new object[] { m, null, f, I, 1, l }),
                ("terr.Buddha x1", te, "Buddha", (m, l) => new object[] { m, null, f, I, l }),
                ("terr.StoneShrine", te, "StoneShrine", (m, l) => new object[] { m, null, f, I, l }),
                ("kit.BellChorten x1", ki, "BellChorten", (m, l) => new object[] { m, I, 5.6, l, false }),
                ("north.Ajima", no, "Ajima", (m, l) => new object[] { m, null, f, I, l }),
                ("north.WheelLantern x1", no, "WheelLantern", (m, l) => new object[] { m, I, 0.0, 0.0, l }),
                ("north.NeHouse", no, "NeHouse", (m, l) => new object[] { m, null, f, l }),
                ("wall.Pavilion x1", wa, "Pavilion", (m, l) => new object[] { m, null, f, I, 4.2, 2.4, 3.6, true, l }),
                ("wall.Niche x1", wa, "Niche", (m, l) => new object[] { m, I, l, 1 }),
                ("gompa.Guru", go, "Guru", (m, l) => new object[] { m, I, 8.0, 6.0, l }),
                ("gompa.Portico", go, "Portico", (m, l) => new object[] { m, I, 7.0, 6.0, 0.45, 4.05, 4, red, l, false }),
                ("gompa.Mirrors", go, "Mirrors", (m, l) => new object[] { m, I, 8.0, 6.05, 10.0, l }),
                ("gompa.GiltRoof", go, "GiltRoof", (m, l) => new object[] { m, I, -3.8, 3.8, -4.0, 1.0, 12.0, 1.6, l, true }),
                ("gompa.Banner", go, "Banner", (m, l) => new object[] { m, I, 0.0, 14.0, 0.0, 0.7, l }),
                ("gompa.DharmaWheel", go, "DharmaWheel", (m, l) => new object[] { m, I, 0.0, 11.0, 5.0, 1.4, l }),
                ("gompa.BigBell", go, "BigBell", (m, l) => new object[] { m, I, -2.0, 8.6, l }),
                ("gompa.Jamchen", go, "Jamchen", (m, l) => new object[] { m, I, 6.0, 5.0, l }),
                ("gompa.Dhabzang", go, "Dhabzang", (m, l) => new object[] { m, I, 5.0, 4.0, l }),
                ("quarter.LampHouse x1", qu, "LampHouse", (m, l) => new object[] { m, null, f, 33.5, 25.0, -40.0, 4.2, 3.0, l }),
                ("quarter.Tree x1", qu, "Tree", (m, l) => new object[] { m, null, f, 0.0, 0.0, 6.0, l, 1 }),
                ("quarter.Garden", qu, "Garden", (m, l) => new object[] { m, f, -24.0, 33.5, 9.0, 4.0, 40.0, l }),
                ("quarter.Paving N", qu, "Paving", (m, l) => new object[] { m, null, t, f, BoudhaPart.North, l }),
                ("gate.ArchWall", ga, "ArchWall", (m, l) => new object[] { m, I, red, red, l }),
                ("gate.Roof", ga, "Roof", (m, l) => new object[] { m, I, 3.0, 1.5, red, l }),
                ("gate.Dragon x1", ga, "Dragon", (m, l) => new object[] { m, I, 1, l }),
            };
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                sb.Append(r.Item1.PadRight(26));
                for (int lod = 0; lod < 3; lod++)
                {
                    var m = new MeshData();
                    m.HasUv0 = true;
                    int n;
                    try { n = Call(r.Item2, r.Item3, r.Item4(m, lod)); }
                    catch (Exception e) { n = -2; sb.Append(" [" + (e.InnerException ?? e).GetType().Name + "]"); }
                    sb.Append(" " + n.ToString().PadLeft(6));
                }
                sb.Append("\n");
            }
            File.WriteAllText(Environment.GetEnvironmentVariable("BOUDHA_SUB"), sb.ToString());
        }
    }
}
