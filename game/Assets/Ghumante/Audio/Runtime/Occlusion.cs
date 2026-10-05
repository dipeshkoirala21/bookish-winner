using UnityEngine;

namespace Ghumante.Audio
{
    /// <summary>
    /// Answers "is a building between the listener and this source?" for the occlusion probe (W2_DESIGN 7.1
    /// "Occlusion"). Implementations must be cheap (one call per voice every 250 ms) and main-thread only.
    /// </summary>
    public interface IOcclusionQuery
    {
        bool Occluded(Vector3 listener, Vector3 source);
    }

    /// <summary>
    /// Occlusion by physics: one <c>Physics.Linecast</c> against <paramref name="mask"/> (the B1/B2 building
    /// collider layer once the world builds one). A zero mask disables it.
    /// </summary>
    public sealed class PhysicsOcclusion : IOcclusionQuery
    {
        private readonly int _mask;

        public PhysicsOcclusion(int mask)
        {
            _mask = mask;
        }

        public bool Occluded(Vector3 listener, Vector3 source)
        {
            if (_mask == 0) return false;
            return Physics.Linecast(listener, source, _mask, QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>
    /// Round-robin occlusion rays (W2_DESIGN 7.1): up to 8 tracked voices, each re-tested every 250 ms (so at most
    /// 32 queries per second, spread over frames). Hits set the voice's occlusion target (−6 dB and a 1.5 kHz
    /// low-pass, ramped over 200 ms by the voice).
    /// </summary>
    internal sealed class OcclusionProbe
    {
        public const int MaxVoices = 8;
        public const float PeriodS = 0.25f;

        private float _accum;
        private int _cursor;

        public IOcclusionQuery Query { get; set; }

        /// <summary>How many rays to cast this frame for <paramref name="tracked"/> voices.</summary>
        public int RaysThisFrame(float dt, int tracked)
        {
            if (Query == null || tracked <= 0)
            {
                _accum = 0f;
                return 0;
            }
            int n = tracked < MaxVoices ? tracked : MaxVoices;
            _accum += dt;
            float step = PeriodS / n;
            int rays = (int)(_accum / step);
            _accum -= rays * step;
            return rays > n ? n : rays;
        }

        /// <summary>Next index in [0, count) in round-robin order.</summary>
        public int Next(int count)
        {
            if (count <= 0) return -1;
            _cursor++;
            if (_cursor >= count) _cursor = 0;
            return _cursor;
        }
    }
}
