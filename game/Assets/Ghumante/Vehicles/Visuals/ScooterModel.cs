using Ghumante.Core.Motion;
using UnityEngine;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>
    /// The placeholder cartoon scooter with its rider (M1 track D; the real art comes later, ASSET_MANIFEST.md): body,
    /// seat, leg shield, two wheels, handlebar with grips and mirrors, headlamp and tail light, built from primitives
    /// with vertex colours and drawn with one <c>Ghumante/ToonLit</c> material (five renderers, SRP-Batcher friendly).
    /// <para><see cref="Apply"/> poses it from a <see cref="VehiclePose"/> each frame: position and heading, pitch and roll
    /// from the ground, lean into turns (pivoting on the tyres' contact line), the handlebar and front wheel steer, both
    /// wheels roll with the distance travelled, and a squash-and-stretch spring (<see cref="KickSquash"/>) bounces it on
    /// landings and bumps. Model axes: +Z forward, +Y up, origin on the ground between the wheels.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ScooterModel : MonoBehaviour
    {
        public const float WheelRadiusM = VehicleTuning.ScooterWheelRadiusM;
        public const float RearAxleZ = -0.62f;
        public const float FrontAxleZ = 0.68f;

        /// <summary>Where the rider sits (model space): the target of the mount hop.</summary>
        public static readonly Vector3 SeatLocal = new Vector3(0f, 0.84f, -0.34f);

        /// <summary>Largest drawn handlebar angle (radians); the model's steering is exaggerated a little.</summary>
        public const float MaxVisualSteerRad = 0.6f;

        private const float SquashLimit = 0.28f;

        private Transform _lean;
        private Transform _squash;
        private Transform _steer;
        private Transform _frontWheel;
        private Transform _rearWheel;
        private GameObject _rider;
        private Mesh[] _meshes;
        private Spring _squashSpring = new Spring(new SpringParams(3.2f, 0.32f));
        private bool _riderVisible = true;

        /// <summary>Builds the scooter under <paramref name="parent"/> (keep the parent at the identity).</summary>
        public static ScooterModel Create(Transform parent, Material material)
        {
            ToonMeshBuilder.RequireMaterial(material);
            var go = new GameObject("Scooter");
            go.transform.SetParent(parent, false);
            ScooterModel model = go.AddComponent<ScooterModel>();
            model.Build(material);
            return model;
        }

        /// <summary>The rider on the seat (hidden while the explorer walks).</summary>
        public bool RiderVisible
        {
            get { return _riderVisible; }
            set
            {
                if (_riderVisible == value) return;
                _riderVisible = value;
                if (_rider != null) _rider.SetActive(value);
            }
        }

        /// <summary>The seat in scene space (where a mounting walker lands).</summary>
        public Vector3 SeatPosition
        {
            get { return _squash != null ? _squash.TransformPoint(SeatLocal) : transform.position; }
        }

        /// <summary>The ground point under the rear wheel in scene space (where dust puffs start).</summary>
        public Vector3 RearContact
        {
            get { return transform.TransformPoint(new Vector3(0f, 0.05f, RearAxleZ - 0.1f)); }
        }

        /// <summary>
        /// Poses the model. <paramref name="scenePosition"/> is the pose's ground point in scene space (world minus the
        /// floating origin). With <paramref name="reducedMotion"/> the squash spring rests.
        /// </summary>
        public void Apply(in VehiclePose pose, Vector3 scenePosition, float dt, bool reducedMotion)
        {
            const float rad2Deg = 57.2957795f;
            Quaternion body = Quaternion.Euler(-pose.Pitch * rad2Deg, pose.HeadingRad * rad2Deg, -pose.Roll * rad2Deg);
            transform.SetPositionAndRotation(scenePosition, body);
            _lean.localRotation = Quaternion.Euler(0f, 0f, -pose.Lean * rad2Deg);
            float steer = Mathf.Clamp(pose.SteerRad * 0.8f, -MaxVisualSteerRad, MaxVisualSteerRad);
            _steer.localRotation = Quaternion.Euler(0f, steer * rad2Deg, 0f);
            Quaternion spin = Quaternion.Euler(pose.WheelAngleRad * rad2Deg, 0f, 0f);
            _frontWheel.localRotation = spin;
            _rearWheel.localRotation = spin;

            if (reducedMotion)
            {
                _squashSpring.Snap(0f);
            }
            else
            {
                _squashSpring.Step(dt);
                if (_squashSpring.Value > SquashLimit) _squashSpring.Value = SquashLimit;
                else if (_squashSpring.Value < -SquashLimit) _squashSpring.Value = -SquashLimit;
            }
            float s = _squashSpring.Value;
            _squash.localScale = new Vector3(1f + 0.5f * s, 1f - s, 1f + 0.5f * s);
        }

        /// <summary>Bounces the model: positive squashes it onto its wheels (a landing), negative stretches it.
        /// About 1 is a firm landing.</summary>
        public void KickSquash(float strength)
        {
            _squashSpring.Kick(Mathf.Clamp(strength, -2f, 2f) * 2.4f);
        }

        private void OnDestroy()
        {
            if (_meshes == null) return;
            for (int i = 0; i < _meshes.Length; i++) ToonMeshBuilder.Destroy(_meshes[i]);
            _meshes = null;
        }

        private void Build(Material material)
        {
            var b = new ToonMeshBuilder();
            _lean = Child("Lean", transform, Vector3.zero);
            _squash = Child("Squash", _lean, Vector3.zero);

            // ---- body (does not steer) ----
            b.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.42f, 0.12f, 0.92f), ToonPalette.ScooterBody);
            b.Box(new Vector3(0f, 0.336f, 0.06f), new Vector3(0.34f, 0.012f, 0.56f), ToonPalette.Black);
            b.Sphere(new Vector3(0f, 0.52f, -0.46f), new Vector3(0.24f, 0.25f, 0.43f), ToonPalette.ScooterBody, Quaternion.identity);
            b.Box(new Vector3(0.232f, 0.5f, -0.46f), new Vector3(0.03f, 0.06f, 0.56f), ToonPalette.ScooterAccent);
            b.Box(new Vector3(-0.232f, 0.5f, -0.46f), new Vector3(0.03f, 0.06f, 0.56f), ToonPalette.ScooterAccent);
            b.Sphere(new Vector3(0f, 0.78f, -0.40f), new Vector3(0.19f, 0.075f, 0.35f), ToonPalette.Seat, Quaternion.identity);
            b.Box(new Vector3(0f, 0.58f, -0.88f), new Vector3(0.17f, 0.07f, 0.05f), ToonPalette.Taillight);
            b.Sphere(new Vector3(0f, 0.42f, RearAxleZ), new Vector3(0.11f, 0.06f, 0.29f), ToonPalette.ScooterTrim, Quaternion.identity);
            Quaternion shieldTilt = Quaternion.Euler(-12f, 0f, 0f);
            b.Box(new Vector3(0f, 0.62f, 0.40f), new Vector3(0.46f, 0.66f, 0.10f), ToonPalette.ScooterBody, shieldTilt);
            b.Box(new Vector3(0f, 0.68f, 0.455f), new Vector3(0.30f, 0.38f, 0.02f), ToonPalette.ScooterAccent, shieldTilt);
            Mesh bodyMesh = b.ToMesh("Scooter Body");
            Renderer(_squash, "Body", bodyMesh, material);

            // ---- steering assembly: pivots on the front axle's vertical line ----
            _steer = Child("Steer", _squash, new Vector3(0f, 0f, FrontAxleZ));
            b.Clear();
            b.Cylinder(new Vector3(0f, 0.42f, -0.06f), new Vector3(0f, 1.0f, -0.18f), 0.035f, ToonPalette.Chrome, 10);
            b.Sphere(new Vector3(0f, 1.0f, -0.16f), new Vector3(0.16f, 0.085f, 0.12f), ToonPalette.ScooterBody, Quaternion.identity);
            b.Sphere(new Vector3(0f, 1.0f, -0.045f), 0.066f, ToonPalette.Headlamp, 6, 12);
            b.Cylinder(new Vector3(-0.34f, 1.02f, -0.20f), new Vector3(0.34f, 1.02f, -0.20f), 0.022f, ToonPalette.Chrome, 8);
            b.Cylinder(new Vector3(-0.37f, 1.02f, -0.20f), new Vector3(-0.25f, 1.02f, -0.20f), 0.032f, ToonPalette.Black, 8);
            b.Cylinder(new Vector3(0.25f, 1.02f, -0.20f), new Vector3(0.37f, 1.02f, -0.20f), 0.032f, ToonPalette.Black, 8);
            b.Cylinder(new Vector3(-0.22f, 1.03f, -0.20f), new Vector3(-0.27f, 1.17f, -0.22f), 0.012f, ToonPalette.Chrome, 6);
            b.Cylinder(new Vector3(0.22f, 1.03f, -0.20f), new Vector3(0.27f, 1.17f, -0.22f), 0.012f, ToonPalette.Chrome, 6);
            b.Sphere(new Vector3(-0.28f, 1.19f, -0.22f), new Vector3(0.05f, 0.035f, 0.016f), ToonPalette.Black, Quaternion.identity, 5, 10);
            b.Sphere(new Vector3(0.28f, 1.19f, -0.22f), new Vector3(0.05f, 0.035f, 0.016f), ToonPalette.Black, Quaternion.identity, 5, 10);
            b.Cylinder(new Vector3(0.075f, WheelRadiusM, 0f), new Vector3(0.06f, 0.46f, -0.06f), 0.018f, ToonPalette.Chrome, 6);
            b.Cylinder(new Vector3(-0.075f, WheelRadiusM, 0f), new Vector3(-0.06f, 0.46f, -0.06f), 0.018f, ToonPalette.Chrome, 6);
            b.Sphere(new Vector3(0f, 0.46f, 0f), new Vector3(0.1f, 0.05f, 0.25f), ToonPalette.ScooterBody, Quaternion.identity);
            Mesh barMesh = b.ToMesh("Scooter Handlebar");
            Renderer(_steer, "Handlebar", barMesh, material);

            // ---- wheels: tyre, hub and a cross so the roll is visible ----
            b.Clear();
            b.Cylinder(new Vector3(-0.06f, 0f, 0f), new Vector3(0.06f, 0f, 0f), WheelRadiusM, ToonPalette.Tyre, 18);
            b.Cylinder(new Vector3(-0.066f, 0f, 0f), new Vector3(0.066f, 0f, 0f), 0.13f, ToonPalette.Hub, 14);
            b.Box(Vector3.zero, new Vector3(0.14f, 0.045f, 0.21f), ToonPalette.ScooterTrim);
            b.Box(Vector3.zero, new Vector3(0.14f, 0.21f, 0.045f), ToonPalette.ScooterTrim);
            Mesh wheel = b.ToMesh("Scooter Wheel");
            _frontWheel = Renderer(_steer, "Front Wheel", wheel, material).transform;
            _frontWheel.localPosition = new Vector3(0f, WheelRadiusM, 0f);
            _rearWheel = Renderer(_squash, "Rear Wheel", wheel, material).transform;
            _rearWheel.localPosition = new Vector3(0f, WheelRadiusM, RearAxleZ);

            // ---- rider, seated ----
            b.Clear();
            BuildRider(b);
            Mesh riderMesh = b.ToMesh("Scooter Rider");
            _rider = Renderer(_squash, "Rider", riderMesh, material);
            _rider.SetActive(_riderVisible);
            _meshes = new[] { bodyMesh, barMesh, wheel, riderMesh };
        }

        private static void BuildRider(ToonMeshBuilder b)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side;
                b.Capsule(new Vector3(0.12f * x, 0.84f, -0.32f), new Vector3(0.14f * x, 0.80f, 0.03f), 0.078f, ToonPalette.Trousers);
                b.Capsule(new Vector3(0.14f * x, 0.80f, 0.03f), new Vector3(0.13f * x, 0.43f, 0.13f), 0.066f, ToonPalette.Trousers);
                b.Sphere(new Vector3(0.13f * x, 0.39f, 0.19f), new Vector3(0.062f, 0.05f, 0.105f), ToonPalette.Shoes, Quaternion.identity, 5, 10);
                b.Capsule(new Vector3(0.21f * x, 1.30f, -0.22f), new Vector3(0.26f * x, 1.12f, -0.01f), 0.056f, ToonPalette.Shirt);
                b.Capsule(new Vector3(0.26f * x, 1.12f, -0.01f), new Vector3(0.30f * x, 1.03f, 0.45f), 0.047f, ToonPalette.Shirt);
                b.Sphere(new Vector3(0.30f * x, 1.03f, 0.48f), 0.052f, ToonPalette.Skin, 5, 10);
                b.Sphere(new Vector3(0.05f * x, 1.52f, -0.045f), 0.019f, ToonPalette.Eyes, 4, 8);
            }
            b.Sphere(new Vector3(0f, 1.12f, -0.27f), new Vector3(0.20f, 0.27f, 0.15f), ToonPalette.Shirt, Quaternion.Euler(12f, 0f, 0f));
            b.Sphere(new Vector3(0f, 1.36f, -0.21f), new Vector3(0.115f, 0.045f, 0.10f), ToonPalette.Scarf, Quaternion.identity, 5, 12);
            b.Sphere(new Vector3(0f, 1.53f, -0.18f), 0.15f, ToonPalette.Skin);
            b.Sphere(new Vector3(0f, 1.59f, -0.19f), new Vector3(0.17f, 0.15f, 0.18f), ToonPalette.Helmet, Quaternion.identity);
            b.Sphere(new Vector3(0f, 1.61f, -0.19f), new Vector3(0.04f, 0.156f, 0.186f), ToonPalette.HelmetStripe, Quaternion.identity, 6, 10);
            b.Sphere(new Vector3(0f, 1.63f, -0.05f), new Vector3(0.13f, 0.025f, 0.07f), ToonPalette.Visor, Quaternion.identity, 4, 12);
        }

        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static GameObject Renderer(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }
    }
}
