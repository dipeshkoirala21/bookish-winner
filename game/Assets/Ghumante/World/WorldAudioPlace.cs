using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ghumante.Audio;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.World
{
    /// <summary>
    /// The open world's side of the sound (W2_DESIGN 7.1 occlusion, 7.2 zoning): keeps a
    /// <see cref="PlaceAudioIndex"/> of the visible detail tiles, feeds <see cref="AudioDirector.SetPlace"/> four
    /// times a second at the streaming focus (Ring Road / arterial roar, the TIA aerodrome + 3 km, parks, rivers and
    /// the galli width for the slapback snapshot), and answers the director's occlusion rays against the building
    /// footprints (the procedural world has no physics colliders). Main thread only; owned by <see cref="WorldRoot"/>.
    /// Perf pass (perf-roads): a shown tile is indexed on a worker thread (<see cref="PlaceAudioIndex.Prepare"/>, 1 to 2
    /// ms per dense tile on a desktop) and joins the index on the main thread at the next query.
    /// </summary>
    internal sealed class WorldAudioPlace : IOcclusionQuery
    {
        /// <summary>Place inputs refresh period (s).</summary>
        public const float UpdateS = 0.25f;

        /// <summary>Nearest-road search radius for the galli test (m).</summary>
        public const double LaneSearchM = 4.0;

        private readonly PlaceAudioIndex _index = new PlaceAudioIndex();
        private readonly WorldRoot _world;
        private float _accum = UpdateS;

        // Tiles asked for (ticket per tile; a newer ask or a removal makes older worker results stale) and the worker
        // results waiting for the main thread.
        private readonly Dictionary<TileId, int> _wanted = new Dictionary<TileId, int>();
        private readonly ConcurrentQueue<KeyValuePair<int, PlaceAudioIndex.PreparedTile>> _ready =
            new ConcurrentQueue<KeyValuePair<int, PlaceAudioIndex.PreparedTile>>();
        private int _ticket;

        public WorldAudioPlace(WorldRoot world)
        {
            _world = world;
        }

        public PlaceAudioIndex Index
        {
            get
            {
                Join();
                return _index;
            }
        }

        /// <summary>A detail tile showed: indexed on a worker, it joins at the next query.</summary>
        public void AddTile(TileId id, TileData t)
        {
            if (t == null) return;
            int ticket = ++_ticket;
            _wanted[id] = ticket;
            ConcurrentQueue<KeyValuePair<int, PlaceAudioIndex.PreparedTile>> ready = _ready;
            Task.Run(() =>
            {
                try
                {
                    ready.Enqueue(new KeyValuePair<int, PlaceAudioIndex.PreparedTile>(ticket, PlaceAudioIndex.Prepare(id, t)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("WorldAudioPlace: tile " + id + " not indexed: " + e.Message);
                }
            });
        }

        public void RemoveTile(TileId id)
        {
            _wanted.Remove(id);
            _index.RemoveTile(id);
        }

        public void Clear()
        {
            _wanted.Clear();
            _index.Clear();
        }

        /// <summary>Main thread: the prepared tiles still wanted join the index (stale ones are dropped).</summary>
        private void Join()
        {
            KeyValuePair<int, PlaceAudioIndex.PreparedTile> r;
            while (_ready.TryDequeue(out r))
            {
                int ticket;
                if (_wanted.TryGetValue(r.Value.Id, out ticket) && ticket == r.Key) _index.AddTile(r.Value);
            }
        }

        /// <summary>Recomputes the place overlays at <paramref name="at"/> every <see cref="UpdateS"/>.</summary>
        public void Tick(float dt, AudioDirector audio, WorldPos at, TileGroundQuery ground, AviationConfig aviation)
        {
            _accum += dt;
            if (_accum < UpdateS) return;
            _accum = 0f;
            Join();
            PlaceSample p = _index.Sample(at.X, at.Z);
            float airport = 0f;
            if (aviation != null)
                airport = PlaceAudioIndex.AirportNearness(at.X, at.Z, aviation.PavementSouthX, aviation.PavementSouthZ,
                                                          aviation.PavementNorthX, aviation.PavementNorthZ);
            float lane = 0f;
            RoadHit hit;
            if (ground != null && ground.TryNearestRoad(at.X, at.Z, LaneSearchM, out hit) && hit.Road != null && (hit.OnRoad || hit.OnFootpath))
                lane = _index.GalliWidth(hit.X, hit.Z, hit.DirX, hit.DirZ, hit.HalfWidthM);
            audio.SetPlace(p.RingRoad01, airport, p.Park01, p.Water01, lane);
        }

        /// <summary>Scene positions to game metres, then the footprint test.</summary>
        public bool Occluded(Vector3 listener, Vector3 source)
        {
            Join();
            WorldPos o = _world.Origin;
            return _index.Occluded(o.X + listener.x, o.Y + listener.y, o.Z + listener.z, o.X + source.x, o.Y + source.y, o.Z + source.z);
        }
    }
}
