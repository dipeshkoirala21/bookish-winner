using System;
using Ghumante.Core.Driving;
using Ghumante.Vehicles;
using Ghumante.Vehicles.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Characters.Rides
{
    /// <summary>
    /// The look of a vehicle the player can use (garage, community fleet): Track B's procedural body at LOD0 with its own
    /// plate number (<see cref="VehicleMeshCache.CreateUnique"/>), separate wheels that spin with the odometer and steer at
    /// the front, the visual roll and pitch of four-wheelers and the lean of two-wheelers (physics stays flat), and a
    /// squash spring for bumps and the honk bounce. A small green key tag hangs on community-fleet vehicles (W2-O5).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DrivenVehicleView : MonoBehaviour
    {
        private Transform _body;
        private Transform[] _wheels;
        private WheelSocket[] _sockets;
        private Mesh _unique;
        private GameObject _tag;
        private Mesh _tagMesh;
        private Core.Motion.Spring _squash = new Core.Motion.Spring(new Core.Motion.SpringParams(3.2f, 0.32f));
        private VehicleCatalogEntry _entry;

        public int Variant { get; private set; }
        public byte Livery { get; private set; }

        public static DrivenVehicleView Create(Transform parent, Material material, VehicleMeshCache cache, int variant, byte livery, uint plateSeed,
                                               bool fleetTag)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            var go = new GameObject("Vehicle " + VehicleCatalog.At(variant).AssetId);
            go.transform.SetParent(parent, false);
            DrivenVehicleView v = go.AddComponent<DrivenVehicleView>();
            v.Build(material, cache, variant, livery, plateSeed, fleetTag);
            return v;
        }

        public VehicleCatalogEntry Entry
        {
            get { return _entry; }
        }

        /// <summary>Shows or hides the green community-fleet key tag.</summary>
        public bool FleetTag
        {
            set
            {
                if (_tag != null && _tag.activeSelf != value) _tag.SetActive(value);
            }
        }

        /// <summary>Bounces the body (positive squashes).</summary>
        public void Kick(float strength)
        {
            _squash.Kick(Mathf.Clamp(strength, -2f, 2f) * 2.2f);
        }

        /// <summary>
        /// The body's drawn pitch (nose up) and roll (Unity z) for a pose, radians: the ground pose plus the visual roll
        /// and pitch of four-wheelers (not with Reduce motion), a two-wheeler's lean. The body is drawn at
        /// <c>Quaternion.Euler(-pitch, heading, roll)</c>; the mounted cameras use the same angles.
        /// </summary>
        public static void BodyAngles(in VehiclePose pose, BodyShape shape, bool reducedMotion, out float pitchRad, out float rollRad)
        {
            bool two = Core.Characters.VehicleRoles.IsTwoWheeler(shape);
            rollRad = two ? -pose.Lean : -pose.Roll - pose.VisualRoll;
            pitchRad = pose.Pitch + pose.VisualPitch;
            if (reducedMotion && !two)
            {
                rollRad = -pose.Roll;
                pitchRad = pose.Pitch;
            }
        }

        /// <summary>Poses the vehicle at <paramref name="scenePosition"/> from its interpolated pose.</summary>
        public void Apply(in VehiclePose pose, Vector3 scenePosition, float dt, bool reducedMotion)
        {
            const float rad2Deg = 57.2957795f;
            float pitch, roll;
            BodyAngles(pose, _entry.Shape, reducedMotion, out pitch, out roll);
            transform.SetPositionAndRotation(scenePosition, Quaternion.Euler(-pitch * rad2Deg, pose.HeadingRad * rad2Deg, roll * rad2Deg));
            if (reducedMotion) _squash.Snap(0f);
            else _squash.Step(dt);
            float s = Mathf.Clamp(_squash.Value, -0.25f, 0.25f);
            _body.localScale = new Vector3(1f + 0.4f * s, 1f - s, 1f + 0.4f * s);
            for (int i = 0; i < _wheels.Length; i++)
            {
                WheelSocket w = _sockets[i];
                float spin = (float)(pose.OdometerM / Math.Max(0.1f, w.Radius)) * rad2Deg;
                float steer = w.Steers ? pose.SteerRad * rad2Deg : 0f;
                _wheels[i].localRotation = Quaternion.Euler(0f, steer, 0f) * Quaternion.Euler(spin % 360f, 0f, 0f);
            }
        }

        private void Build(Material material, VehicleMeshCache cache, int variant, byte livery, uint plateSeed, bool fleetTag)
        {
            Variant = variant;
            Livery = livery;
            _entry = VehicleCatalog.At(variant);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _unique = cache.CreateUnique(variant, livery, VehicleLod.Lod0, plateSeed);
            Renderer(_body, "Shell", _unique, material, true);
            _sockets = VehicleMesher.Wheels(_entry);
            _wheels = new Transform[_sockets.Length];
            for (int i = 0; i < _sockets.Length; i++)
            {
                WheelSocket w = _sockets[i];
                Mesh wheel = cache.Wheel(w.Radius, w.Width, VehicleLod.Lod0);
                var pivot = new GameObject("Wheel " + i).transform;
                pivot.SetParent(_body, false);
                pivot.localPosition = new Vector3(w.X, w.Y, w.Z);
                Renderer(pivot, "Tyre", wheel, material, true);
                _wheels[i] = pivot;
            }
            BuildTag(material);
            FleetTag = fleetTag;
        }

        /// <summary>The green key tag: a small diamond hanging by the handlebar or the driver's mirror.</summary>
        private void BuildTag(Material material)
        {
            VehicleMesher.Dims d = VehicleMesher.DimsOf(_entry);
            float size = 0.09f;
            var v = new[]
            {
                new Vector3(0f, size, 0f), new Vector3(size * 0.7f, 0f, 0f), new Vector3(0f, -size, 0f), new Vector3(-size * 0.7f, 0f, 0f),
            };
            var n = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            var green = new Color32(0x3F, 0xB9, 0x4F, 0xFF);
            var c = new[] { green, green, green, green };
            // Both faces (no culling assumptions on the toon shader).
            var t = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            _tagMesh = new Mesh { name = "Fleet tag", vertices = v, normals = n, colors32 = c, triangles = t };
            _tag = new GameObject("Fleet Tag");
            _tag.transform.SetParent(_body, false);
            bool two = Core.Characters.VehicleRoles.IsTwoWheeler(_entry.Shape);
            _tag.transform.localPosition = two ? new Vector3(-0.3f, 1.05f, d.Wheelbase + 0.05f) : new Vector3(-(d.Half + 0.05f), Mathf.Min(1.4f, d.Height * 0.7f), d.Wheelbase * 0.8f);
            _tag.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _tag.AddComponent<MeshFilter>().sharedMesh = _tagMesh;
            var r = _tag.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Renderer(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            Kill(_unique);
            Kill(_tagMesh);
            _unique = null;
            _tagMesh = null;
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
