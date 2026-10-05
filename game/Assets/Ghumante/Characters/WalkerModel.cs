using Ghumante.Core.Motion;
using Ghumante.Vehicles;
using Ghumante.Vehicles.Visuals;
using UnityEngine;

namespace Ghumante.Characters
{
    /// <summary>
    /// The placeholder explorer on foot (M1 track D): a rounded body, a head with the same yellow helmet as the rider
    /// (so it reads as the same person), eyes, swinging hands and feet, built from primitives with vertex colours and
    /// drawn with the <c>Ghumante/ToonLit</c> material. <see cref="Apply"/> poses it from a <see cref="VehiclePose"/>:
    /// a walk cycle driven by the distance travelled (feet step, hands swing, the body bobs and leans into a run),
    /// plus the mount/dismount hop and a squash-and-stretch spring (<see cref="KickSquash"/>). The body is three
    /// renderers (body, feet, hands) so limbs move without skinning. Model axes: +Z forward, +Y up, origin between the feet.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class WalkerModel : MonoBehaviour
    {
        /// <summary>Metres per full step cycle (two steps) at a walk; running stretches it.</summary>
        public const float StrideM = 1.3f;

        private const float SquashLimit = 0.3f;

        private Transform _squash;
        private Transform _body;
        private Transform _leftFoot, _rightFoot, _leftHand, _rightHand;
        private Mesh[] _meshes;
        private Spring _squashSpring = new Spring(new SpringParams(3.6f, 0.3f));
        private float _gaitAmount;

        public static WalkerModel Create(Transform parent, Material material)
        {
            if (material == null) throw new System.ArgumentNullException(nameof(material));
            var go = new GameObject("Walker");
            go.transform.SetParent(parent, false);
            WalkerModel model = go.AddComponent<WalkerModel>();
            model.Build(material);
            return model;
        }

        /// <summary>
        /// Poses the walker. <paramref name="scenePosition"/> is the ground point in scene space; <paramref name="hopHeight"/>
        /// lifts the whole figure (the mount/dismount arc). With <paramref name="reducedMotion"/> the bob, lean and squash
        /// rest; the feet still step so walking reads as walking.
        /// </summary>
        public void Apply(in VehiclePose pose, Vector3 scenePosition, float hopHeight, float dt, bool reducedMotion)
        {
            const float rad2Deg = 57.2957795f;
            transform.SetPositionAndRotation(scenePosition + new Vector3(0f, hopHeight, 0f),
                                             Quaternion.Euler(0f, pose.HeadingRad * rad2Deg, 0f));
            float speed = Mathf.Abs(pose.SpeedMps);
            float target = Mathf.Clamp01(speed / 1.4f);
            _gaitAmount += (target - _gaitAmount) * (1f - Mathf.Exp(-10f * Mathf.Max(0f, dt)));
            float run = Mathf.Clamp01((speed - 1.6f) / 2.9f);
            float stride = StrideM * (1f + 0.6f * run);
            float phase = (float)(pose.OdometerM / stride % 1.0) * 2f * Mathf.PI;
            float swing = Mathf.Sin(phase) * _gaitAmount;
            float lift = 0.5f + 0.5f * Mathf.Cos(phase);
            float step = (0.16f + 0.08f * run) * swing;
            _leftFoot.localPosition = new Vector3(-0.1f, 0.06f + 0.09f * _gaitAmount * Mathf.Max(0f, Mathf.Cos(phase)), step);
            _rightFoot.localPosition = new Vector3(0.1f, 0.06f + 0.09f * _gaitAmount * Mathf.Max(0f, -Mathf.Cos(phase)), -step);
            float hand = (0.12f + 0.08f * run) * swing;
            _leftHand.localPosition = new Vector3(-0.27f, 0.78f, -hand);
            _rightHand.localPosition = new Vector3(0.27f, 0.78f, hand);

            if (reducedMotion)
            {
                _squashSpring.Snap(0f);
                _body.localPosition = Vector3.zero;
                _body.localRotation = Quaternion.identity;
            }
            else
            {
                float bob = 0.045f * _gaitAmount * Mathf.Abs(Mathf.Sin(phase)) * (1f + run) - 0.02f * _gaitAmount * lift * run;
                _body.localPosition = new Vector3(0f, bob, 0f);
                _body.localRotation = Quaternion.Euler(4f * _gaitAmount + 8f * run, 0f, 2.5f * swing);
                _squashSpring.Step(dt);
                if (_squashSpring.Value > SquashLimit) _squashSpring.Value = SquashLimit;
                else if (_squashSpring.Value < -SquashLimit) _squashSpring.Value = -SquashLimit;
            }
            float s = _squashSpring.Value;
            _squash.localScale = new Vector3(1f + 0.5f * s, 1f - s, 1f + 0.5f * s);
        }

        /// <summary>Bounces the figure: positive squashes (a landing), negative stretches (a jump).</summary>
        public void KickSquash(float strength)
        {
            _squashSpring.Kick(Mathf.Clamp(strength, -2f, 2f) * 2.6f);
        }

        private void OnDestroy()
        {
            if (_meshes == null) return;
            for (int i = 0; i < _meshes.Length; i++) ToonMeshBuilder.Destroy(_meshes[i]);
            _meshes = null;
        }

        private void Build(Material material)
        {
            _squash = Child("Squash", transform, Vector3.zero);
            _body = Child("Body", _squash, Vector3.zero);
            var b = new ToonMeshBuilder();
            // Rounded body: trousers below, shirt above, a scarf; the head with the rider's helmet, eyes and a smile line.
            b.Sphere(new Vector3(0f, 0.52f, 0f), new Vector3(0.21f, 0.2f, 0.17f), ToonPalette.Trousers, Quaternion.identity);
            b.Sphere(new Vector3(0f, 0.82f, 0f), new Vector3(0.23f, 0.3f, 0.19f), ToonPalette.Shirt, Quaternion.identity);
            b.Sphere(new Vector3(0f, 1.08f, 0.01f), new Vector3(0.12f, 0.05f, 0.11f), ToonPalette.Scarf, Quaternion.identity, 5, 12);
            b.Sphere(new Vector3(0f, 1.3f, 0.02f), 0.2f, ToonPalette.Skin);
            b.Sphere(new Vector3(0f, 1.37f, 0.0f), new Vector3(0.215f, 0.19f, 0.225f), ToonPalette.Helmet, Quaternion.identity);
            b.Sphere(new Vector3(0f, 1.39f, 0.0f), new Vector3(0.05f, 0.196f, 0.232f), ToonPalette.HelmetStripe, Quaternion.identity, 6, 10);
            b.Sphere(new Vector3(0f, 1.43f, 0.19f), new Vector3(0.16f, 0.03f, 0.08f), ToonPalette.Visor, Quaternion.identity, 4, 12);
            b.Sphere(new Vector3(-0.07f, 1.3f, 0.2f), new Vector3(0.026f, 0.034f, 0.02f), ToonPalette.Eyes, Quaternion.identity, 4, 8);
            b.Sphere(new Vector3(0.07f, 1.3f, 0.2f), new Vector3(0.026f, 0.034f, 0.02f), ToonPalette.Eyes, Quaternion.identity, 4, 8);
            Mesh body = b.ToMesh("Walker Body");
            Renderer(_body, "Body Mesh", body, material, true);

            b.Clear();
            b.Sphere(Vector3.zero, new Vector3(0.075f, 0.06f, 0.11f), ToonPalette.Shoes, Quaternion.identity, 5, 10);
            Mesh foot = b.ToMesh("Walker Foot");
            _leftFoot = Renderer(_squash, "Left Foot", foot, material, true).transform;
            _rightFoot = Renderer(_squash, "Right Foot", foot, material, true).transform;

            b.Clear();
            b.Sphere(Vector3.zero, 0.07f, ToonPalette.Skin, 5, 10);
            Mesh hand = b.ToMesh("Walker Hand");
            _leftHand = Renderer(_body, "Left Hand", hand, material, false).transform;
            _rightHand = Renderer(_body, "Right Hand", hand, material, false).transform;
            _meshes = new[] { body, foot, hand };
        }

        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static GameObject Renderer(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
