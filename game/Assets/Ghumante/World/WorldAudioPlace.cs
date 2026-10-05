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

        public WorldAudioPlace(WorldRoot world)
        {
            _world = world;
        }

        public PlaceAudioIndex Index
        {
            get { return _index; }
        }

        public void AddTile(TileId id, TileData t)
        {
            _index.AddTile(id, t);
        }

        public void RemoveTile(TileId id)
        {
            _index.RemoveTile(id);
        }

        public void Clear()
        {
            _index.Clear();
        }

        /// <summary>Recomputes the place overlays at <paramref name="at"/> every <see cref="UpdateS"/>.</summary>
        public void Tick(float dt, AudioDirector audio, WorldPos at, TileGroundQuery ground, AviationConfig aviation)
        {
            _accum += dt;
            if (_accum < UpdateS) return;
            _accum = 0f;
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
            WorldPos o = _world.Origin;
            return _index.Occluded(o.X + listener.x, o.Y + listener.y, o.Z + listener.z, o.X + source.x, o.Y + source.y, o.Z + source.z);
        }
    }
}
