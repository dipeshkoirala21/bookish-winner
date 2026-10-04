using System;
using System.IO;
using System.Linq;
using Ghumante.Core.Services;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class ServicesTests
    {
        [Test]
        public void EventBusDeliversTypedEvents()
        {
            var bus = new EventBus();
            int discoveries = 0, places = 0;
            ulong last = 0;
            IDisposable sub = bus.Subscribe<DiscoveryMade>(e =>
            {
                discoveries++;
                last = e.OsmRef;
            });
            bus.Subscribe<PlaceEntered>(e => places++);
            bus.Publish(new DiscoveryMade { OsmRef = 42 });
            bus.Publish(new ActivityCompleted { ActivityId = "nobody listens" });
            Assert.That(discoveries, Is.EqualTo(1));
            Assert.That(last, Is.EqualTo(42UL));
            Assert.That(places, Is.EqualTo(0));
            Assert.That(bus.SubscriberCount<DiscoveryMade>(), Is.EqualTo(1));
            sub.Dispose();
            sub.Dispose();
            bus.Publish(new DiscoveryMade { OsmRef = 7 });
            Assert.That(discoveries, Is.EqualTo(1));
            Assert.That(bus.SubscriberCount<DiscoveryMade>(), Is.EqualTo(0));
        }

        [Test]
        public void HandlersMayUnsubscribeDuringPublish()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            Action<PlaceEntered> ha = null;
            ha = e =>
            {
                a++;
                bus.Unsubscribe(ha);
            };
            bus.Subscribe(ha);
            bus.Subscribe<PlaceEntered>(e => b++);
            bus.Publish(new PlaceEntered());
            bus.Publish(new PlaceEntered());
            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(2));
        }

        [Test]
        public void HandlerErrorsDoNotStopOtherHandlers()
        {
            var bus = new EventBus();
            int ok = 0;
            bus.Subscribe<ActivityCompleted>(e => throw new InvalidOperationException("boom"));
            bus.Subscribe<ActivityCompleted>(e => ok++);
            Assert.Throws<InvalidOperationException>(() => bus.Publish(new ActivityCompleted()));
            Exception seen = null;
            bus.HandlerError += ex => seen = ex;
            bus.Publish(new ActivityCompleted());
            Assert.That(seen, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ok, Is.EqualTo(1));
        }

        [Test]
        public void PublishDoesNotAllocate()
        {
            var bus = new EventBus();
            long sum = 0;
            bus.Subscribe<DiscoveryMade>(e => sum += (long)e.OsmRef);
            var evt = new DiscoveryMade { OsmRef = 1 };
            for (int i = 0; i < 1000; i++) bus.Publish(evt); // warm up (JIT tiering)
            // Any per-publish garbage would be at least 24 bytes x 10 000; the runtime itself may allocate a
            // few bytes once (on-stack replacement of the loop), so the bound is per publish.
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10100; i++) bus.Publish(evt);
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(a1 - a0, Is.LessThan(1000), "bytes allocated by 10 100 publishes");
            Assert.That(sum, Is.EqualTo(11100));
        }

        [Test]
        public void NullServicesDoNothing()
        {
            IAnalytics a = new NullAnalytics();
            a.SetConsent(true);
            a.TrackEvent("x");
            Assert.That(a.Enabled, Is.False);
            new NullCrashReporter().RecordException(new Exception());
            ICloudSave cloud = new NullCloudSave();
            Assert.That(cloud.IsAvailableAsync().Result, Is.False);
            Assert.That(cloud.LoadAsync("slot").Result, Is.Null);
            Assert.That(cloud.SaveAsync("slot", "{}").Result, Is.False);
            Assert.That(new NullQuestService().ActiveQuests, Is.Empty);
            Assert.That(new NullDialogueService().TryStart("didi"), Is.False);
            var flags = new NullWorldStateFlags();
            flags.SetBool("met_didi", true);
            flags.SetInt("momos", 3);
            Assert.That(flags.GetBool("met_didi") && flags.GetInt("momos") == 3 && !flags.GetBool("other"), Is.True);
            Geo.WorldPos p;
            Assert.That(new NullNpcRegistry().TryGetPosition("x", out p), Is.False);

            IRegionPackSource src = new NullRegionPackSource();
            Assert.That(src.ListRegionsAsync().Result, Is.Empty);
            Assert.That(src.IsAvailableAsync("kathmandu_valley").Result, Is.False);
            Assert.That(src.RequestAsync("kathmandu_valley").Result, Is.False);
            var ex = Assert.Throws<AggregateException>(() => src.OpenPackAsync("kathmandu_valley").Wait());
            Assert.That(ex.InnerExceptions.Single(), Is.InstanceOf<FileNotFoundException>());
        }

        [Test]
        public void CoreHasNoEngineReferences()
        {
            var refs = typeof(EventBus).Assembly.GetReferencedAssemblies().Select(r => r.Name).ToList();
            Assert.That(refs.Any(r => r.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
            string core = Path.Combine(GoldenFiles.RepoRoot, "game", "Assets", "Ghumante", "Core");
            foreach (string f in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(f);
                Assert.That(text.Contains("using UnityEngine"), Is.False, f);
                Assert.That(text.Contains("using System.Text.Json") || text.Contains("using Newtonsoft"), Is.False, f);
            }
        }
    }
}
