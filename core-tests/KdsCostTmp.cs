using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsTemples;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>TEMPORARY: triangle cost of the kit's parts (mon-kds-temples budget pass).</summary>
    public class KdsCostTmp
    {
        [Test]
        public void Profile()
        {
            TileData tile = StreamingSampleRegion.Tile(new TileId(10, 516, 161));
            var all = new System.Collections.Generic.Dictionary<string, int>();
            foreach (string id in KdsTemplesSite.Ids)
            {
                HeroRecipe recipe;
                HeroCatalog.TryGet(id, out recipe);
                KdsKit.Prof = new System.Collections.Generic.Dictionary<string, int>();
                var m = new MeshData();
                int tris = KdsTemplesSite.Build(HeroCatalog.ToRecord(recipe), tile, 0, m, null, null);
                var sb = new System.Text.StringBuilder();
                int known = 0;
                var keys = new System.Collections.Generic.List<string>(KdsKit.Prof.Keys);
                keys.Sort((a, b) => KdsKit.Prof[b].CompareTo(KdsKit.Prof[a]));
                foreach (string k in keys)
                {
                    int v = KdsKit.Prof[k];
                    if (k != "valance") known += v;
                    sb.AppendFormat(" {0}={1}", k, v);
                    int a;
                    all.TryGetValue(k, out a);
                    all[k] = a + v;
                }
                TestContext.WriteLine("PROF {0,-30} {1,6} other={2}{3}", id.Substring(8), tris, tris - known, sb);
                int o;
                all.TryGetValue("other", out o);
                all["other"] = o + tris - known;
            }
            KdsKit.Prof = null;
            var ks = new System.Collections.Generic.List<string>(all.Keys);
            ks.Sort((a, b) => all[b].CompareTo(all[a]));
            foreach (string k in ks) TestContext.WriteLine("TOTAL {0,-14} {1,7}", k, all[k]);
        }

        [Test]
        public void Costs()
        {
            TileData tile = StreamingSampleRegion.Tile(new TileId(10, 516, 161));
            HeroRecipe recipe;
            HeroCatalog.TryGet("her.ktm.kds_saraswati", out recipe);
            KdsTemplesSite.Entry e;
            KdsTemplesSite.TryEntry(recipe.Id, out e);
            for (int lod = 0; lod <= 1; lod++)
            {
                Func<Action<KdsCtx>, int> cost = a =>
                {
                    var m = new MeshData();
                    m.HasUv0 = true;
                    KdsCtx x;
                    KdsTemplesSite.Locate(e, tile, lod, m, null, null, out x);
                    x.Clear = null;
                    a(x);
                    return m.TriangleCount;
                };
                TestContext.WriteLine("LOD {0}", lod);
                foreach (int q in new[] { 0, 1, 2 })
                    foreach (GuardianKind k in new[] { GuardianKind.Lion, GuardianKind.Griffin })
                        TestContext.WriteLine("  guardian {0} q{1}: {2}", k, q, cost(x => SacredFigures.Guardian(x.M, x.K, 0, 0, 0, 1.5, k, true, q, x.Lod, 0)));
                TestContext.WriteLine("  roof 4x4 no val: {0}", cost(x => KdsKit.Roof(x, 4, 4, 10, 1.5, 1.5, 12, 2.5, 2.5, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  roof 4x4 val:    {0}", cost(x => KdsKit.Roof(x, 4, 4, 10, 1.5, 1.5, 12, 2.5, 2.5, 10, KdsLooks.TileOld, 0.3)));
                TestContext.WriteLine("  roof 4x4 struts3:{0}", cost(x => KdsKit.Struts(x, KdsKit.Roof(x, 4, 4, 10, 1.5, 1.5, 12, 2.5, 2.5, 10, KdsLooks.TileOld, 0), 8, 3, 3, false, 1u)) - cost(x => KdsKit.Roof(x, 4, 4, 10, 1.5, 1.5, 12, 2.5, 2.5, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  polyroof 8 a5:   {0}", cost(x => KdsKit.PolyRoof(x, 8, 5, 10, 2, 12, 3, 10, KdsLooks.TileOld, 0.35)));
                TestContext.WriteLine("  roof 7x7 no val: {0}", cost(x => KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  roof 7x7 val:    {0}", cost(x => KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0.4)));
                TestContext.WriteLine("  struts 6x6 rich: {0}", cost(x => KdsKit.Struts(x, KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0), 7, 6, 6, true, 1u)) - cost(x => KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  struts 6x6 plain:{0}", cost(x => KdsKit.Struts(x, KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0), 7, 6, 6, false, 1u)) - cost(x => KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  bells 7x7:       {0}", cost(x => KdsKit.Bells(x, KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0), 1.0, 1.0)) - cost(x => KdsKit.Roof(x, 7, 7, 10, 3, 3, 13, 4, 4, 10, KdsLooks.TileOld, 0)));
                TestContext.WriteLine("  post 3m:         {0}", cost(x => KdsKit.Post(x, 0, 0, 0, 3, 0.25, KdsLooks.Timber)));
                TestContext.WriteLine("  blackwindow:     {0}", cost(x => KdsKit.BlackWindow(x.M, x.K, 0, 1, 1, 0.8, 0.8, x.Lod)));
                TestContext.WriteLine("  latticewindow:   {0}", cost(x => SacredParts.LatticeWindow(x.M, x.K, 0, 1, 1, 0.8, 0.8, KdsLooks.WindowBlack, x.Lod)));
                TestContext.WriteLine("  door torana:     {0}", cost(x => KdsKit.Door(x.M, x.K, 0, 0, 1, 1, 2, KdsLooks.Timber, x.Lod, true)));
                TestContext.WriteLine("  lattice door:    {0}", cost(x => KdsKit.LatticeDoor(x.M, x.K, 0, 0, 1, 1, 2, x.Lod)));
                TestContext.WriteLine("  gilt finial 2m:  {0}", cost(x => KdsKit.GiltFinial(x, 0, 2, 0.3)));
                TestContext.WriteLine("  balustrade 10m:  {0}", cost(x => KdsKit.Balustrade(x, 0, 0, 10, 0, 0, 0.8, KdsLooks.Timber)));
                TestContext.WriteLine("  shutters 10m:    {0}", cost(x => KdsKit.Shutters(x.M, x.K, 5, 1, 0, 3, 1.1, x.Lod, KdsLooks.Timber)));
                TestContext.WriteLine("  bandedwall 4x4:  {0}", cost(x => SacredParts.BandedWall(x.M, x.K, 4, 4, 0, 3, KdsLooks.Brick, KdsLooks.Timber, x.Lod, true, true)));
                TestContext.WriteLine("  stage 6x6:       {0}", cost(x => KdsKit.Stage(x, 6, 6, 0, 1, 5, 5, 0, KdsLooks.Brick, KdsLooks.Nosing, 0.1, 0.2, 2)));
                TestContext.WriteLine("  stair 13:        {0}", cost(x => KdsKit.Stair(x, 0.8, 5, 3, 0, 3, 0.3, 0.5, KdsLooks.Nosing, KdsLooks.Plaster, 13)));
                TestContext.WriteLine("  ribbed dome:     {0}", cost(x => KdsKit.RibbedDome(x, 0, 0, 0, 2, 0.6, KdsLooks.BronzeDark)));
                TestContext.WriteLine("  big bell:        {0}", cost(x => KdsKit.BigBell(x, 0, 3, 0, 1.9)));
                TestContext.WriteLine("  flame arch:      {0}", cost(x => KdsKit.FlameArch(x.M, x.K, 0, 0, 0, 1.7, 5, 0.2, 0.1, KdsLooks.Gilt, x.Lod, true, true)));
                TestContext.WriteLine("  nandi:           {0}", cost(x => SacredFigures.Nandi(x.M, x.K, 0, 0, 0, 1.6, KdsLooks.StoneGrey, x.Lod)));
                TestContext.WriteLine("  garuda:          {0}", cost(x => SacredFigures.Garuda(x.M, x.K, 0, 0, 0, 1.6, KdsLooks.StoneGrey, x.Lod)));
                TestContext.WriteLine("  picket 10m:      {0}", cost(x => KdsKit.Picket(x, 0, 0, 10, 0, 1)));
            }
        }
    }
}
