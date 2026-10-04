using System;
using System.Collections.Generic;

namespace Ghumante.Core.Services
{
    /// <summary>
    /// Typed publish/subscribe between gameplay assemblies (ARCHITECTURE.md 7.1, 7.11). Events are usually
    /// small structs such as <see cref="DiscoveryMade"/>.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Subscribe; dispose the returned token to unsubscribe.</summary>
        IDisposable Subscribe<T>(Action<T> handler);

        void Unsubscribe<T>(Action<T> handler);

        /// <summary>Invoke every handler subscribed to <typeparamref name="T"/> at the time of the call.</summary>
        void Publish<T>(T evt);
    }

    /// <summary>
    /// Main-thread event bus. Handler lists are copy-on-write arrays, so <see cref="Publish{T}"/> allocates
    /// nothing beyond the delegate invocations and handlers may (un)subscribe while an event is being
    /// delivered (the change applies from the next publish). A throwing handler does not stop the others; the
    /// exceptions are reported through <see cref="HandlerError"/> (rethrown when nobody listens).
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        /// <summary>Receives exceptions thrown by handlers.</summary>
        public event Action<Exception> HandlerError;

        private sealed class Slot<T>
        {
            public Action<T>[] Handlers = Array.Empty<Action<T>>();
        }

        private Slot<T> GetSlot<T>(bool create)
        {
            object o;
            if (_handlers.TryGetValue(typeof(T), out o)) return (Slot<T>)o;
            if (!create) return null;
            var s = new Slot<T>();
            _handlers[typeof(T)] = s;
            return s;
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Slot<T> s = GetSlot<T>(true);
            var a = new Action<T>[s.Handlers.Length + 1];
            Array.Copy(s.Handlers, a, s.Handlers.Length);
            a[a.Length - 1] = handler;
            s.Handlers = a;
            return new Subscription<T>(this, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            Slot<T> s = GetSlot<T>(false);
            if (s == null || handler == null) return;
            int i = Array.IndexOf(s.Handlers, handler);
            if (i < 0) return;
            var a = new Action<T>[s.Handlers.Length - 1];
            Array.Copy(s.Handlers, 0, a, 0, i);
            Array.Copy(s.Handlers, i + 1, a, i, a.Length - i);
            s.Handlers = a;
        }

        public void Publish<T>(T evt)
        {
            Slot<T> s = GetSlot<T>(false);
            if (s == null) return;
            Action<T>[] hs = s.Handlers;
            for (int i = 0; i < hs.Length; i++)
            {
                try
                {
                    hs[i](evt);
                }
                catch (Exception e)
                {
                    Action<Exception> onError = HandlerError;
                    if (onError == null) throw;
                    onError(e);
                }
            }
        }

        public int SubscriberCount<T>()
        {
            Slot<T> s = GetSlot<T>(false);
            return s == null ? 0 : s.Handlers.Length;
        }

        private sealed class Subscription<T> : IDisposable
        {
            private EventBus _bus;
            private readonly Action<T> _handler;

            public Subscription(EventBus bus, Action<T> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_bus == null) return;
                _bus.Unsubscribe(_handler);
                _bus = null;
            }
        }
    }

    // Events a future quest system can subscribe to (ARCHITECTURE.md 7.11).

    /// <summary>A discoverable place or POI was found for the first time.</summary>
    public struct DiscoveryMade
    {
        public string RegionId;

        /// <summary>The POI's osm_ref, as in the tile POIS chunk.</summary>
        public ulong OsmRef;

        public Geo.WorldPos Position;
    }

    public struct ActivityCompleted
    {
        public string ActivityId;
        public int Score;
        public int Stars;
    }

    public struct PlaceEntered
    {
        public string RegionId;

        /// <summary>Search index entry osm_ref (32-bit, diagnostics) of the place.</summary>
        public uint OsmRef;

        public string Name;
    }

    /// <summary>Raised by Ghumante.World after a floating-origin rebase (ARCHITECTURE.md 5.2).</summary>
    public struct OriginShifted
    {
        public Geo.WorldPos OldOrigin;
        public Geo.WorldPos NewOrigin;
        public Geo.WorldPos Delta;
    }
}
