using System;
using System.Diagnostics;
using Ghumante.Core.Generators.Fauna;
using Ghumante.World.Life;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// W2 perf pass, owner playtest #4 ("stutters"): the fauna presenters' bind meshes are built on worker threads
    /// (<see cref="FaunaBindCache"/>, used by <c>Wildlife.FaunaLibrary</c>). A near-level bind mesh used to be built on
    /// the main thread the first time such an animal came close: 2-11 ms on a desktop (a cow at LOD0 about 10 ms), several
    /// times that on a phone.
    /// </summary>
    public class TrafficFaunaBindTests
    {
        [Test]
        public void TheFirstNearAnimalNeverBuildsItsMeshOnTheFrame()
        {
            // Before: the main thread built the bind mesh (what FaunaLibrary.Mesh did on first use).
            var sw = Stopwatch.StartNew();
            var inline = new FaunaMesh(1024, 3072);
            FaunaMesher.Build(FaunaSpecies.Cow, 0, CoatPattern.Plain, inline);
            double inlineMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            FaunaMesher.Build(FaunaSpecies.Dog, 0, CoatPattern.Patched, new FaunaMesh(1024, 3072));
            double dogMs = sw.Elapsed.TotalMilliseconds;

            // After: asking costs the frame next to nothing, the build runs on a worker.
            var cache = new FaunaBindCache();
            double worst = 0;
            FaunaMesh m;
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Cow, FaunaSpecies.Bull, FaunaSpecies.Buffalo, FaunaSpecies.Dog, FaunaSpecies.Goat, FaunaSpecies.Macaque })
            {
                sw.Restart();
                bool ready = cache.TryGet(sp, 0, CoatPattern.Plain, out m);
                worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds);
                Assert.That(ready, Is.False, sp + ": not built on the calling thread");
                Assert.That(m, Is.Null);
            }
            TestContext.WriteLine("main thread: inline cow LOD0 bind mesh " + inlineMs.ToString("0.00") + " ms, dog " + dogMs.ToString("0.00") +
                                  " ms; worst TryGet " + worst.ToString("0.000") + " ms");
            Assert.That(cache.BuiltInline, Is.EqualTo(0));
            Assert.That(cache.Pending, Is.EqualTo(6));
            Assert.That(worst, Is.LessThan(1.0), "asking for a bind mesh never stalls the frame");

            cache.WaitAll();
            Assert.That(cache.TryGet(FaunaSpecies.Cow, 0, CoatPattern.Plain, out m), Is.True);
            Assert.That(m.VertexCount, Is.EqualTo(inline.VertexCount), "the worker builds the same mesh");
            Assert.That(m.Mesh.IndexCount, Is.EqualTo(inline.Mesh.IndexCount));
            Assert.That(cache.BuiltInBackground, Is.EqualTo(1));
            Assert.That(cache.BuiltInline, Is.EqualTo(0));
        }

        [Test]
        public void SeeingASpeciesFarBuildsItsNearLevelsBeforeItComesClose()
        {
            var cache = new FaunaBindCache();
            cache.Prewarm(FaunaSpecies.Macaque, CoatPattern.Plain);
            Assert.That(cache.Pending, Is.EqualTo(3), "all three levels start building");
            cache.Prewarm(FaunaSpecies.Macaque, CoatPattern.Plain);
            Assert.That(cache.Pending, Is.EqualTo(3), "once");
            cache.WaitAll();
            for (int lod = 0; lod <= 2; lod++) Assert.That(cache.IsReady(FaunaSpecies.Macaque, lod, CoatPattern.Plain), Is.True, "LOD" + lod);
            // The near presenter's Get then takes the finished mesh, never building on the frame.
            FaunaMesh near = cache.Get(FaunaSpecies.Macaque, 0, CoatPattern.Plain);
            Assert.That(near, Is.Not.Null);
            Assert.That(cache.BuiltInline, Is.EqualTo(0));
            Assert.That(cache.BuiltInBackground, Is.EqualTo(3));
            Assert.That(cache.Count, Is.EqualTo(3));
        }

        [Test]
        public void GetWaitsForARunningBuildAndBuildsOnlyAsALastResort()
        {
            var cache = new FaunaBindCache();
            FaunaMesh m;
            Assert.That(cache.TryGet(FaunaSpecies.Pigeon, 1, CoatPattern.Plain, out m), Is.False);
            FaunaMesh got = cache.Get(FaunaSpecies.Pigeon, 1, CoatPattern.Plain);
            Assert.That(got, Is.Not.Null);
            Assert.That(cache.BuiltInline, Is.EqualTo(0), "the running worker build was waited for");
            FaunaMesh again = cache.Get(FaunaSpecies.Crow, 2, CoatPattern.Plain);
            Assert.That(again, Is.Not.Null);
            Assert.That(cache.BuiltInline, Is.EqualTo(1), "nothing built or building: built on the calling thread");
            Assert.That(cache.Get(FaunaSpecies.Crow, 2, CoatPattern.Plain), Is.SameAs(again), "kept");
        }
    }
}
