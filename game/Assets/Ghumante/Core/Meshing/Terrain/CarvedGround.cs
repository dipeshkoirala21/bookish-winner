using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// What a body standing at a point stands on (cut/fill package, owner playtest at Gaurighat: "the dog looks like it is
    /// sleeping in the air"): the drawn road surface where a road is drawn (carriageway, footpath top, shoulder, junction
    /// cap or ring, a deck within reach), else the carved terrain (<see cref="TerrainCarve"/>). Implementations are
    /// thread-safe and allocation-free per query, so the sims can call them from their worker step.
    /// </summary>
    public interface IStandSurface
    {
        /// <summary>
        /// The surface a body near height <paramref name="nearY"/> stands on at game (<paramref name="x"/>,
        /// <paramref name="z"/>); <paramref name="onRoad"/> tells a drawn road surface from the terrain. False where no
        /// ground is known.
        /// </summary>
        bool TryStand(double x, double z, float nearY, out float y, out bool onRoad);
    }

    /// <summary>
    /// <see cref="IStandSurface"/> over a set of detail tiles: per tile the carved sampler at the tile's drawn terrain step
    /// and the drawn road surface on it (<see cref="RoadSurfaceQuery"/>), so what the sims place (street animals beside the
    /// kerb, anything standing off a lane) stands exactly on the one carved surface the terrain mesh, the road mesh and the
    /// ground query share. Tiles are prepared on any thread (<see cref="Prepare"/>: the carve and the road query are built
    /// there, once per tile and step, and shared with every other sampler of that tile and step), then added and removed by
    /// the owner; queries read an immutable snapshot of the tile set, so adding and removing never disturbs a query
    /// running on another thread.
    /// </summary>
    public sealed class CarvedGround : IStandSurface
    {
        /// <summary>One prepared tile.</summary>
        public sealed class Entry
        {
            public readonly TileId Id;
            public readonly TileHeightSampler Sampler;
            public readonly RoadSurfaceQuery Roads;

            internal Entry(TileId id, TileHeightSampler sampler, RoadSurfaceQuery roads)
            {
                Id = id;
                Sampler = sampler;
                Roads = roads;
            }
        }

        private Entry[] _tiles = new Entry[0];
        private readonly object _gate = new object();

        /// <summary>Tiles in the set.</summary>
        public int Count
        {
            get { return _tiles.Length; }
        }

        /// <summary>
        /// Builds the entry of detail tile <paramref name="t"/> drawn at terrain step <paramref name="step"/> (the carve and
        /// the road surface query; heavy, so on a worker), or null when the tile has no heights.
        /// </summary>
        public static Entry Prepare(TileData t, int step)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (!TerrainGrid.HasHeights(t)) return null;
            TileHeightSampler h = TileHeightSampler.ForArea(t, t.Tile, Math.Max(1, step), true);
            TerrainCarve unused = h.Carve; // built now, on this thread
            RoadSurfaceQuery q = t.Roads.Count > 0 ? RoadSurfaceQuery.For(t, h, new RoadOptions()) : null;
            return new Entry(t.Tile, h, q);
        }

        /// <summary>Adds (or replaces) a prepared tile.</summary>
        public void Add(Entry e)
        {
            if (e == null) return;
            lock (_gate)
            {
                Entry[] old = _tiles;
                int n = old.Length, at = -1;
                for (int i = 0; i < n; i++)
                    if (old[i].Id == e.Id) at = i;
                var next = new Entry[at >= 0 ? n : n + 1];
                Array.Copy(old, next, n);
                next[at >= 0 ? at : n] = e;
                _tiles = next;
            }
        }

        /// <summary>Removes a tile (no-op when it is not in the set).</summary>
        public void Remove(TileId id)
        {
            lock (_gate)
            {
                Entry[] old = _tiles;
                int at = -1;
                for (int i = 0; i < old.Length; i++)
                    if (old[i].Id == id) at = i;
                if (at < 0) return;
                var next = new Entry[old.Length - 1];
                Array.Copy(old, 0, next, 0, at);
                Array.Copy(old, at + 1, next, at, old.Length - at - 1);
                _tiles = next;
            }
        }

        public bool TryStand(double x, double z, float nearY, out float y, out bool onRoad)
        {
            Entry[] tiles = _tiles;
            for (int i = 0; i < tiles.Length; i++)
            {
                Entry e = tiles[i];
                if (!e.Sampler.Covers(x, z)) continue;
                RoadSurfaceHit hit;
                // The road surface where a road's drawn extent covers the point (the carve's extents are the ribbons, caps
                // and rings as drawn); the query's edge and bend tolerances beyond them stand on the drawn terrain.
                if (e.Roads != null && e.Roads.TrySample(x, z, nearY, out hit) && OnExtent(e, x, z, hit))
                {
                    y = hit.Y;
                    onRoad = true;
                    return true;
                }
                onRoad = false;
                return e.Sampler.TryHeight(x, z, out y);
            }
            y = 0f;
            onRoad = false;
            return false;
        }

        private static bool OnExtent(Entry e, double x, double z, in RoadSurfaceHit hit)
        {
            TerrainCarve c = e.Sampler.Carve;
            if (c == null || hit.OnDeck) return true; // no carve (gentle tile) or a deck: the query alone
            float bench;
            double gx, gz;
            return c.ExtentAt(x - e.Id.X0, z - e.Id.Z0, out bench, out gx, out gz);
        }
    }
}
