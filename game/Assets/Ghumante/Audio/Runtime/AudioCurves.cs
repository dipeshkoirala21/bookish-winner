using Ghumante.Core.Synth;
using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// Custom rolloff curves per <see cref="SoundClass"/> (W2_DESIGN 7.1 "Distance"): 1/r from min to 0.7 × max,
    /// then a fade to exactly 0 at max, sampled from <see cref="SoundSpace.Rolloff(float, float, float)"/> into an
    /// AnimationCurve over Unity's normalised [0, maxDistance] axis. Built once per class; shared by all sources.
    /// </summary>
    internal static class AudioCurves
    {
        private const int Keys = 24;
        private static AnimationCurve[] _curves;

        public static AnimationCurve For(SoundClass c)
        {
            if (_curves == null) _curves = new AnimationCurve[SoundClasses.Count];
            int i = (int)c;
            if (i < 0 || i >= _curves.Length) i = (int)SoundClass.Foley;
            if (_curves[i] == null) _curves[i] = Build(SoundClasses.Get((SoundClass)i));
            return _curves[i];
        }

        private static AnimationCurve Build(in SoundClassInfo info)
        {
            var keys = new Keyframe[Keys + 1];
            float max = info.MaxM;
            for (int k = 0; k <= Keys; k++)
            {
                // Denser keys near the source, where 1/r bends fastest.
                float u = (float)k / Keys;
                float x = u * u;
                float d = x * max;
                keys[k] = new Keyframe(x, SoundSpace.Rolloff(d, info.MinM, info.MaxM));
            }
            var curve = new AnimationCurve(keys);
            for (int k = 0; k <= Keys; k++) curve.SmoothTangents(k, 0f);
            return curve;
        }

        /// <summary>Applies a class's spatial settings to a source (custom rolloff, no Unity Doppler).</summary>
        public static void Apply(AudioSource s, SoundClass c)
        {
            SoundClassInfo info = SoundClasses.Get(c);
            s.dopplerLevel = 0f;
            s.spatialBlend = info.SpatialBlend;
            s.spread = info.SpreadDeg;
            s.priority = info.Priority > 256 ? 256 : info.Priority;
            if (info.SpatialBlend > 0f)
            {
                s.rolloffMode = AudioRolloffMode.Custom;
                s.minDistance = info.MinM;
                s.maxDistance = info.MaxM;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, For(c));
            }
            bool twoD = info.SpatialBlend <= 0f;
            s.bypassListenerEffects = twoD;
            s.bypassReverbZones = twoD;
        }
    }
}
