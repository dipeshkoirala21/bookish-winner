using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Sky
{
    /// <summary>
    /// Day and night (ARCHITECTURE.md 5.3 and 8): advances the time of day (default 1 game day = 48 real minutes),
    /// and from <see cref="SkyPalette"/> drives the directional light (sun or moon: rotation, colour, intensity, shadow
    /// strength), the <c>Ghumante/SkyGradient</c> skybox (through shader globals), URP fog (exponential squared valley
    /// haze tinted by the time of day) and the trilight ambient (which URP turns into the SH the toon shader samples).
    /// While enabled it owns those RenderSettings; disabling restores what was there (the menu's look).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ghumante/World Sky")]
    public sealed class WorldSky : MonoBehaviour
    {
        public const float DefaultDayLengthMinutes = 48f;

        /// <summary>The light turns once the sun moved more than 0.1 degrees (cos 0.1°).</summary>
        private const float LightStepCos = 0.9999985f;

        private static readonly int SkyZenithId = Shader.PropertyToID("_GhSkyZenith");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_GhSkyHorizon");
        private static readonly int SkyGroundId = Shader.PropertyToID("_GhSkyGround");
        private static readonly int SunDirectionId = Shader.PropertyToID("_GhSunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_GhSunColor");
        private static readonly int MoonDirectionId = Shader.PropertyToID("_GhMoonDirection");
        private static readonly int SkyParamsId = Shader.PropertyToID("_GhSkyParams");

        [Tooltip("Directional light used as sun and moon. Empty: RenderSettings.sun, then the first directional light, else one is created.")]
        [SerializeField] private Light sun;

        [Tooltip("Skybox material (Ghumante/SkyGradient).")]
        [SerializeField] private Material skyMaterial;

        [Range(0f, 24f)]
        [SerializeField] private float timeOfDayHours = 6.2f;

        [Tooltip("Real minutes per game day.")]
        [SerializeField] private float dayLengthMinutes = DefaultDayLengthMinutes;

        [Tooltip("Multiplies the passing of time (debug fast-forward).")]
        [SerializeField] private float timeScale = 1f;

        [SerializeField] private bool paused;

        [Tooltip("Day of the year for the sun's path (280 = early October).")]
        [Range(1, 366)]
        [SerializeField] private int dayOfYear = 280;

        [SerializeField] private float latitudeDeg = (float)SkyPalette.DefaultLatitudeDeg;

        [Tooltip("Scales the haze density (1 = the palette's valley haze).")]
        [SerializeField] private float fogDensityScale = 1f;

        private SkyState _state;
        private Vector3 _lightTowards;
        private bool _createdSun;
        private Saved _saved;
        private bool _hasSaved;

        private struct Saved
        {
            public bool Fog;
            public FogMode FogMode;
            public float FogDensity;
            public Color FogColor;
            public AmbientMode AmbientMode;
            public Color AmbientSky, AmbientEquator, AmbientGround;
            public Material Skybox;
            public Light Sun;
            public Light Light;
            public bool LightEnabled;
            public Quaternion LightRotation;
            public Color LightColor;
            public float LightIntensity, LightShadowStrength;
        }

        /// <summary>Local solar time in hours, [0, 24).</summary>
        public float TimeOfDayHours
        {
            get { return timeOfDayHours; }
            set
            {
                float h = value % 24f;
                timeOfDayHours = h < 0f ? h + 24f : h;
                if (isActiveAndEnabled) Apply();
            }
        }

        /// <summary>Real minutes per game day (default 48).</summary>
        public float DayLengthMinutes
        {
            get { return dayLengthMinutes; }
            set { dayLengthMinutes = Mathf.Max(0.01f, value); }
        }

        public float TimeScale
        {
            get { return timeScale; }
            set { timeScale = value; }
        }

        public bool Paused
        {
            get { return paused; }
            set { paused = value; }
        }

        public int DayOfYear
        {
            get { return dayOfYear; }
            set { dayOfYear = Mathf.Clamp(value, 1, 366); }
        }

        public Light Sun
        {
            get { return sun; }
            set { sun = value; }
        }

        public Material SkyMaterial
        {
            get { return skyMaterial; }
            set
            {
                skyMaterial = value;
                if (isActiveAndEnabled && value != null) RenderSettings.skybox = value;
            }
        }

        /// <summary>The state last applied.</summary>
        public SkyState State
        {
            get { return _state; }
        }

        private void OnEnable()
        {
            if (!_hasSaved)
            {
                _saved = new Saved
                {
                    Fog = RenderSettings.fog, FogMode = RenderSettings.fogMode, FogDensity = RenderSettings.fogDensity,
                    FogColor = RenderSettings.fogColor, AmbientMode = RenderSettings.ambientMode,
                    AmbientSky = RenderSettings.ambientSkyColor, AmbientEquator = RenderSettings.ambientEquatorColor,
                    AmbientGround = RenderSettings.ambientGroundColor, Skybox = RenderSettings.skybox, Sun = RenderSettings.sun,
                };
                _hasSaved = true;
            }
            EnsureSun();
            if (!_createdSun && sun != null)
            {
                // The scene's own light (the menu's sun): remember it so closing the world gives it back unchanged.
                _saved.Light = sun;
                _saved.LightEnabled = sun.enabled;
                _saved.LightRotation = sun.transform.rotation;
                _saved.LightColor = sun.color;
                _saved.LightIntensity = sun.intensity;
                _saved.LightShadowStrength = sun.shadowStrength;
            }
            if (_createdSun && sun != null) sun.gameObject.SetActive(true);
            _lightTowards = Vector3.zero;
            if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Apply();
        }

        private void OnDisable()
        {
            if (!_hasSaved) return;
            RenderSettings.fog = _saved.Fog;
            RenderSettings.fogMode = _saved.FogMode;
            RenderSettings.fogDensity = _saved.FogDensity;
            RenderSettings.fogColor = _saved.FogColor;
            RenderSettings.ambientMode = _saved.AmbientMode;
            RenderSettings.ambientSkyColor = _saved.AmbientSky;
            RenderSettings.ambientEquatorColor = _saved.AmbientEquator;
            RenderSettings.ambientGroundColor = _saved.AmbientGround;
            RenderSettings.skybox = _saved.Skybox;
            RenderSettings.sun = _saved.Sun;
            if (_saved.Light != null)
            {
                _saved.Light.enabled = _saved.LightEnabled;
                _saved.Light.transform.rotation = _saved.LightRotation;
                _saved.Light.color = _saved.LightColor;
                _saved.Light.intensity = _saved.LightIntensity;
                _saved.Light.shadowStrength = _saved.LightShadowStrength;
            }
            _saved = default(Saved);
            _hasSaved = false;
            if (_createdSun && sun != null) sun.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_createdSun && sun != null) Destroy(sun.gameObject);
        }

        private void Update()
        {
            if (!paused && dayLengthMinutes > 0f)
            {
                float hoursPerSecond = 24f / (dayLengthMinutes * 60f);
                float h = (timeOfDayHours + Time.deltaTime * hoursPerSecond * timeScale) % 24f;
                timeOfDayHours = h < 0f ? h + 24f : h;
            }
            Apply();
        }

        /// <summary>Push the current time of day to the light, sky, fog and ambient.</summary>
        public void Apply()
        {
            _state = SkyPalette.Evaluate(timeOfDayHours, dayOfYear, latitudeDeg);
            SkyState s = _state;

            if (sun != null)
            {
                // A directional light shines along its forward axis: from the sun (or moon) towards the ground.
                Vector3 towards = s.LightIsMoon ? new Vector3(s.MoonX, s.MoonY, s.MoonZ) : new Vector3(s.SunX, s.SunY, s.SunZ);
                if (towards.y < 0.02f) towards.y = 0.02f; // keep grazing light from shining up through the ground
                towards.Normalize();
                // Turn the light in small steps, not every frame: a shadow map that moves a hair each frame shimmers.
                if (Vector3.Dot(towards, _lightTowards) < LightStepCos)
                {
                    _lightTowards = towards;
                    sun.transform.rotation = Quaternion.LookRotation(-towards, Vector3.up);
                }
                sun.color = ToColor(s.Light);
                sun.intensity = s.LightIntensity;
                sun.shadowStrength = s.ShadowStrength;
                sun.enabled = s.LightIntensity > 0.001f;
            }

            RenderSettings.fogColor = ToColor(s.Fog);
            RenderSettings.fogDensity = s.FogDensity * fogDensityScale;
            RenderSettings.ambientSkyColor = ToColor(s.AmbientSky);
            RenderSettings.ambientEquatorColor = ToColor(s.AmbientEquator);
            RenderSettings.ambientGroundColor = ToColor(s.AmbientGround);

            Shader.SetGlobalColor(SkyZenithId, ToColor(s.Zenith));
            Shader.SetGlobalColor(SkyHorizonId, ToColor(s.Horizon));
            Shader.SetGlobalColor(SkyGroundId, ToColor(s.Ground));
            Shader.SetGlobalVector(SunDirectionId, new Vector4(s.SunX, s.SunY, s.SunZ, s.SunVisible));
            Shader.SetGlobalColor(SunColorId, ToColor(s.Light));
            Shader.SetGlobalVector(MoonDirectionId, new Vector4(s.MoonX, s.MoonY, s.MoonZ, s.Night));
            Shader.SetGlobalVector(SkyParamsId, new Vector4(s.Golden, s.Night, 0f, 0f));
        }

        /// <summary>"HH:MM" of the current time (allocates; for HUDs).</summary>
        public string ClockText()
        {
            int minutes = (int)(timeOfDayHours * 60f) % 1440;
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        private void EnsureSun()
        {
            if (sun != null) return;
            sun = RenderSettings.sun;
            if (sun == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length && sun == null; i++)
                    if (lights[i].type == LightType.Directional) sun = lights[i];
            }
            if (sun != null) return;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            _createdSun = true;
        }

        private static Color ToColor(SkyColor c)
        {
            return new Color(c.R, c.G, c.B, 1f);
        }
    }
}
