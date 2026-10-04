using System;
using System.Text.Json;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class GeoTests
    {
        [Test]
        public void Tm84MatchesPyprojBelowAMillimetre()
        {
            JsonElement g = GoldenFiles.Json("tm84_points.json");
            Assert.That(g.GetProperty("origin")[0].GetDouble(), Is.EqualTo(WorldFrame.OriginE));
            Assert.That(g.GetProperty("origin")[1].GetDouble(), Is.EqualTo(WorldFrame.OriginN));
            int n = 0;
            double worst = 0;
            foreach (JsonElement p in g.GetProperty("points").EnumerateArray())
            {
                double lon = p.GetProperty("lon").GetDouble(), lat = p.GetProperty("lat").GetDouble();
                double e, nn, x, z;
                Tm84.Forward(lon, lat, out e, out nn);
                WorldFrame.LonLatToGame(lon, lat, out x, out z);
                double err = Math.Max(Math.Max(Math.Abs(e - p.GetProperty("e").GetDouble()), Math.Abs(nn - p.GetProperty("n").GetDouble())),
                                      Math.Max(Math.Abs(x - p.GetProperty("x").GetDouble()), Math.Abs(z - p.GetProperty("z").GetDouble())));
                worst = Math.Max(worst, err);
                Assert.That(err, Is.LessThan(1e-3), lon + "," + lat);

                double lon2, lat2;
                WorldFrame.GameToLonLat(p.GetProperty("x").GetDouble(), p.GetProperty("z").GetDouble(), out lon2, out lat2);
                Assert.That(lon2, Is.EqualTo(lon).Within(1e-9), "inverse lon");
                Assert.That(lat2, Is.EqualTo(lat).Within(1e-9), "inverse lat");
                n++;
            }
            Assert.That(n, Is.GreaterThanOrEqualTo(50));
            TestContext.WriteLine("worst TM84 error vs pyproj: " + worst + " m");
        }

        [Test]
        public void CentralMeridianAndRoundTrip()
        {
            double e, n;
            Tm84.Forward(84.0, 0.0, out e, out n);
            Assert.That(e, Is.EqualTo(500000.0).Within(1e-9));
            Assert.That(n, Is.EqualTo(0.0).Within(1e-9));
            for (double lon = 80.06; lon <= 88.2; lon += 0.37)
                for (double lat = 26.35; lat <= 30.45; lat += 0.41)
                {
                    WorldPos p = WorldFrame.LonLatToWorldPos(lon, lat);
                    double lo, la;
                    p.ToLonLat(out lo, out la);
                    Assert.That(lo, Is.EqualTo(lon).Within(1e-10));
                    Assert.That(la, Is.EqualTo(lat).Within(1e-10));
                    Assert.That(p.X, Is.InRange(0.0, WorldFrame.RootSizeM));
                    Assert.That(p.Z, Is.InRange(0.0, WorldFrame.RootSizeM));
                }
        }

        [Test]
        public void WorldPosArithmetic()
        {
            var a = new WorldPos(100.0, 2f, 200.0);
            var b = new WorldPos(103.0, 6f, 204.0);
            Assert.That(a.DistanceXZ(b), Is.EqualTo(5.0));
            Assert.That(a.DistanceXZSquared(b), Is.EqualTo(25.0));
            Assert.That(a.Distance(b), Is.EqualTo(Math.Sqrt(41.0)).Within(1e-12));
            Assert.That(b - a, Is.EqualTo(new WorldPos(3.0, 4f, 4.0)));
            Assert.That(a + new WorldPos(1, 1, 1), Is.EqualTo(new WorldPos(101.0, 3f, 201.0)));
            Assert.That(WorldPos.Lerp(a, b, 0.5), Is.EqualTo(new WorldPos(101.5, 4f, 202.0)));
            Assert.That(a.WithY(9f).Y, Is.EqualTo(9f));
            Assert.That(a == new WorldPos(100.0, 2f, 200.0) && a != b, Is.True);
            Assert.That(a.ToString(), Is.EqualTo("(100.000, 2.00, 200.000)"));
        }

        [Test]
        public void FloatingOriginRebase()
        {
            var origin = new WorldPos(400000.0, 0f, 300000.0);
            var near = new WorldPos(401999.0, 1500f, 300000.0);
            var far = new WorldPos(401500.0, 10f, 301500.25);
            Assert.That(FloatingOrigin.ShouldRebase(near, origin), Is.False);
            Assert.That(FloatingOrigin.ShouldRebase(far, origin), Is.True);
            Assert.That(FloatingOrigin.ShouldRebase(far, origin, 3000.0), Is.False);

            WorldPos delta;
            WorldPos o2 = FloatingOrigin.Rebase(far, origin, out delta);
            Assert.That(o2, Is.EqualTo(new WorldPos(401500.0, 0f, 301500.0)));
            Assert.That(delta, Is.EqualTo(new WorldPos(1500.0, 0f, 1500.0)));
            Assert.That(FloatingOrigin.ShouldRebase(far, o2), Is.False);

            float x, y, z;
            FloatingOrigin.ToLocal(far, o2, out x, out y, out z);
            Assert.That(x, Is.EqualTo(0f));
            Assert.That(z, Is.EqualTo(0.25f));
            Assert.That(FloatingOrigin.FromLocal(x, y, z, o2), Is.EqualTo(far));
            Assert.That(FloatingOrigin.Rebase(far, origin, out delta, 1024.0), Is.EqualTo(new WorldPos(401408.0, 0f, 301056.0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => FloatingOrigin.Rebase(far, origin, out delta, 0.0));
        }

        [Test]
        public void TileIdKeysAndGeometry()
        {
            var t = new TileId(10, 266, 200);
            Assert.That(t.Size, Is.EqualTo(1024.0));
            Assert.That(t.X0, Is.EqualTo(266 * 1024.0));
            Assert.That(t.Z0, Is.EqualTo(200 * 1024.0));
            Assert.That(TileId.FromKey(t.Key), Is.EqualTo(t));
            Assert.That(TileId.Morton(1, 0), Is.EqualTo(1UL));
            Assert.That(TileId.Morton(0, 1), Is.EqualTo(2UL));
            Assert.That(TileId.Morton(3, 5), Is.EqualTo(0b100111UL));
            int max = (1 << 16) - 1;
            var big = new TileId(16, max, 12345);
            Assert.That(TileId.FromKey(big.Key), Is.EqualTo(big));
            Assert.That(new TileId(0, 0, 0).Key, Is.EqualTo(0UL));
            Assert.That(new TileId(5, 0, 0).Key, Is.EqualTo(5UL << 58));
            Assert.That(t.Parent(), Is.EqualTo(new TileId(9, 133, 100)));
            Assert.That(t.Children()[3], Is.EqualTo(new TileId(11, 533, 401)));
            Assert.That(TileId.At(10, t.X0 + 0.5, t.Z0 + 1023.9), Is.EqualTo(t));
            Assert.That(t.Contains(t.X0, t.Z0) && !t.Contains(t.X0 + 1024.0, t.Z0), Is.True);
            Assert.That(t.ToString(), Is.EqualTo("10/266/200"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileId(3, 8, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileId(17, 0, 0));
        }

        [Test]
        public void TileKeysMatchThePackGolden()
        {
            foreach (JsonElement e in GoldenFiles.Json("golden_pack.json").GetProperty("entries").EnumerateArray())
            {
                var t = new TileId(e.GetProperty("level").GetInt32(), e.GetProperty("tx").GetInt32(), e.GetProperty("ty").GetInt32());
                Assert.That(t.Key, Is.EqualTo(e.GetProperty("key").GetUInt64()));
            }
        }
    }
}
