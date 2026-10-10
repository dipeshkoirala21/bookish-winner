using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Synth;
using Ghumante.Core.Traffic;
using Ghumante.World;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Streaming;
using UnityEngine;

namespace Ghumante.Traffic
{
    /// <summary>
    /// Draws the people of the open world (W2_DESIGN 5.4) with the player's own generator: every <see cref="PedPose"/> of
    /// <see cref="LifeHost.People"/> nearest first under the tier caps (1 / 4 / 13, 3 / 10 / 39, 6 / 20 / 78 in the near
    /// ≤ 15 m, mid ≤ 40 m and far ≤ 90 m bands) as one of the shared variant bodies of its archetype, the place it walks
    /// in (<see cref="StreetStyles"/>: Bhadgaunle topis in Bhaktapur, trek gear in Thamel, chuba at Boudha, office wear
    /// on Durbar Marg) and its carry prop (<see cref="CrowdVariants"/>), skinned near and mid, baked far
    /// (<see cref="PeopleRenderer"/>), animated by its clip (<see cref="PersonAnimation"/>: walk, run, carry, sit, pray,
    /// namaste, chat, umbrella...); NPC footsteps near the camera on the surface under the foot
    /// (<see cref="GroundSample.Foot"/>); and the traffic police officers of the visible chowks (<see cref="OfficerPosts"/>)
    /// on their podiums, cycling the five hand signals with the police phases. Attached to every <see cref="WorldRoot"/> by
    /// <see cref="WorldRoot.AnyReady"/>. Main thread only.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/Crowd Presenter")]
    public sealed class CrowdPresenter : MonoBehaviour
    {
        /// <summary>Officers are drawn to this distance (parts to 60 m, block-out beyond).</summary>
        public const float OfficerRangeM = 200f;

        private WorldRoot _world;
        private PeopleRenderer _people;
        private LifeLod _lod;
        private readonly Dictionary<int, float> _prevClip = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _seen = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _bodyKey = new Dictionary<int, int>();
        private readonly List<int> _drop = new List<int>();
        private readonly Dictionary<TileId, List<OfficerPost>> _posts = new Dictionary<TileId, List<OfficerPost>>();
        private float[] _dist = new float[128], _keys = new float[128];
        private int[] _level = new int[128], _order = new int[128], _rank = new int[128];
        private float _clearAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            WorldRoot.AnyReady += w => Attach(w);
        }

        public static CrowdPresenter Attach(WorldRoot world)
        {
            if (world == null) return null;
            CrowdPresenter p = world.GetComponent<CrowdPresenter>();
            if (p == null) p = world.gameObject.AddComponent<CrowdPresenter>();
            p.Bind(world);
            return p;
        }

        private void Bind(WorldRoot world)
        {
            if (_world != null)
            {
                _world.DetailTileShown -= OnShown;
                _world.DetailTileHidden -= OnHidden;
            }
            _world = world;
            _lod = LifeLod.ForTier((int)world.Tier);
            if (_people != null) _people.Dispose();
            _people = world.Materials != null ? new PeopleRenderer(world.Materials, world.transform, (int)world.Tier) : null;
            _posts.Clear();
            _prevClip.Clear();
            _bodyKey.Clear();
            world.DetailTileShown += OnShown;
            world.DetailTileHidden += OnHidden;
            // Tiles already visible before the presenter attached.
            WorldStreamer s = world.Streamer;
            if (s != null)
                for (int i = 0; i < s.DetailViews.Count; i++)
                    if (s.DetailViews[i].Extras != null) OnShown(s.DetailViews[i].Node.Area, s.DetailViews[i].Extras);
        }

        private void OnShown(TileId id, TileExtras e)
        {
            if (e == null || e.Source == null || _posts.ContainsKey(id)) return;
            try
            {
                List<OfficerPost> posts = OfficerPosts.For(e.Source);
                if (posts.Count > 0) _posts.Add(id, posts);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CrowdPresenter: officer posts of " + id + " failed: " + ex.Message);
            }
        }

        private void OnHidden(TileId id)
        {
            _posts.Remove(id);
        }

        private void LateUpdate()
        {
            if (_world == null || !_world.IsOpen || _world.Life == null || _people == null) return;
            Camera cam = _world.ViewCamera;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            WorldPos origin = _world.Origin;
            LifeHost life = _world.Life;
            ISoundService sound = _world.Sound;
            IGroundQuery ground = _world.Ground;
            _people.Begin(camPos);

            int n = life.PeopleCount;
            PedPose[] poses = life.People;
            if (_dist.Length < n)
            {
                int cap = Math.Max(n, _dist.Length * 2);
                _dist = new float[cap];
                _keys = new float[cap];
                _level = new int[cap];
                _order = new int[cap];
                _rank = new int[cap];
            }
            float age = life.SnapshotAgeS;
            for (int i = 0; i < n; i++) _dist[i] = (At(poses[i], origin, age) - camPos).magnitude;
            LifeLod.Assign(_dist, n, _lod.PeopleCaps, _lod.PeopleRadii, _level, _order, _keys);
            // Rank within each band, nearest first (the plan gives the nearest few the finest meshes).
            int r0 = 0, r1 = 0, r2 = 0;
            for (int k = 0; k < n; k++)
            {
                int i = _order[k];
                int l = _level[i];
                _rank[i] = l == 0 ? r0++ : l == 1 ? r1++ : l == 2 ? r2++ : 0;
            }
            AreaTypeGrid areas = _world.AreaTypes;
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                PedPose pp = poses[i];
                int level = _level[i];
                // A held snapshot (slow step) keeps moving: clip time and position run on by the snapshot's age.
                float clipTime = pp.ClipTime + age;
                float prev;
                if (!_prevClip.TryGetValue(pp.AgentId, out prev)) prev = clipTime;
                _prevClip[pp.AgentId] = clipTime;
                _seen[pp.AgentId] = now;
                if (level < 0) continue;
                int key = BodyKey(pp, areas);
                var clip = (PedClip)pp.ClipId;
                CrowdHold hold = pp.CarryProp == 5 ? CrowdHold.Umbrella : CrowdHold.None;
                PersonPose pose = PersonAnimation.Pose(clip, clipTime, prev, pp.SpeedMps, pp.AgentId, hold);
                Vector3 at = At(pp, origin, age);
                double phase = CrowdAnimation.WalkPhase(clipTime, pp.SpeedMps, clip == PedClip.Run);
                _people.Add(at, pp.HeadingRad * Mathf.Rad2Deg, pose, key, level, _rank[i], clip, phase);
                if (pose.Strike != 0 && _dist[i] <= _lod.FootstepRadiusM && sound != null) Footstep(pp, at, pose, ground, sound);
            }

            Officers(origin, camPos, now);
            _people.End();
            _world.ReportLife(0, 0, _people.Tris, _people.People, 0, 0, 0, 0, _people.Draws);

            // Forget agents not seen for a while (twice a second).
            if (now >= _clearAt)
            {
                _clearAt = now + 0.5f;
                _drop.Clear();
                foreach (KeyValuePair<int, float> kv in _seen)
                    if (now - kv.Value > 2f) _drop.Add(kv.Key);
                for (int i = 0; i < _drop.Count; i++)
                {
                    _seen.Remove(_drop[i]);
                    _prevClip.Remove(_drop[i]);
                    _bodyKey.Remove(_drop[i]);
                }
            }
        }

        /// <summary>The variant body of an agent: its archetype, the place it first showed in, its carry prop and its variant
        /// number (cached per agent, so a person keeps their look while walking across district edges).</summary>
        private int BodyKey(in PedPose pp, AreaTypeGrid areas)
        {
            if (_bodyKey.TryGetValue(pp.AgentId, out int key)) return key;
            AreaType area = areas != null ? areas.At(pp.X, pp.Z) : AreaType.Unknown;
            StreetStyle style = StreetStyles.At(pp.X, pp.Z, area);
            int carry = pp.CarryProp <= 5 ? pp.CarryProp : 0;
            key = CrowdVariants.Key((PedArchetype)pp.Archetype, style, carry, CrowdVariants.VariantOf(pp.AgentId));
            _bodyKey[pp.AgentId] = key;
            return key;
        }

        /// <summary>Scene position of a pose, moved <paramref name="ageS"/> along its heading (LifeHost.SnapshotAgeS).</summary>
        private static Vector3 At(in PedPose pp, WorldPos origin, float ageS)
        {
            float ahead = pp.SpeedMps * ageS;
            return new Vector3((float)(pp.X - origin.X) + Mathf.Sin(pp.HeadingRad) * ahead, pp.Y - origin.Y,
                               (float)(pp.Z - origin.Z) + Mathf.Cos(pp.HeadingRad) * ahead);
        }

        private static void Footstep(in PedPose pp, Vector3 at, in PersonPose pose, IGroundQuery ground, ISoundService sound)
        {
            byte foot = (byte)FootSurface.Concrete;
            GroundSample g;
            if (ground != null && ground.TrySample(pp.X, pp.Z, out g)) foot = (byte)g.Foot;
            float side = pose.Strike == 1 ? -0.1f : 0.1f;
            float h = pp.HeadingRad;
            var p = at + new Vector3(Mathf.Cos(h) * side, 0f, -Mathf.Sin(h) * side);
            sound.PlayFootstep(foot, pp.SpeedMps > 2.2f ? FootstepGait.Run : FootstepGait.Walk, p.x, p.y, p.z, false);
        }

        private void Officers(WorldPos origin, Vector3 camPos, float now)
        {
            if (_posts.Count == 0) return;
            IGroundQuery ground = _world.Ground;
            foreach (KeyValuePair<TileId, List<OfficerPost>> kv in _posts)
            {
                TileId id = kv.Key;
                List<OfficerPost> posts = kv.Value;
                for (int i = 0; i < posts.Count; i++)
                {
                    OfficerPost o = posts[i];
                    double gx = id.X0 + o.X, gz = id.Z0 + o.Z;
                    float dx = (float)(gx - origin.X) - camPos.x, dz = (float)(gz - origin.Z) - camPos.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > OfficerRangeM) continue;
                    float y = 0f;
                    GroundSample g;
                    if (ground != null && ground.TrySample(gx, gz, out g)) y = g.Height;
                    if (o.OnPodium) y += 0.35f;
                    double start;
                    int signal = OfficerPosts.SignalAt(o.Seed, now, out start);
                    // Face the arm being waved through: a quarter turn per phase.
                    float heading = (o.Seed % 360) + signal * 90f;
                    PersonPose pose = PersonAnimation.Officer(signal, now - (float)start);
                    var at = new Vector3((float)(gx - origin.X), y - origin.Y, (float)(gz - origin.Z));
                    int band = d <= 15f ? 0 : d <= 40f ? 1 : 2;
                    int key = CrowdVariants.Key(PedArchetype.TrafficPolice, StreetStyle.Urban, 0, (int)(o.Seed % CrowdVariants.Variants));
                    // Officers rank after the crowd in their band (the plan's finest meshes go to the nearest people).
                    _people.Add(at, heading, pose, key, band, 99, signal == 0 ? PedClip.Cheer : PedClip.Idle, 0.0);
                }
            }
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.DetailTileShown -= OnShown;
                _world.DetailTileHidden -= OnHidden;
            }
            if (_people != null) _people.Dispose();
            _people = null;
        }
    }
}
