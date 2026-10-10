using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Engine-free procedural animation of the fauna rigs (no keyframe data): every clip is a function of time,
    /// speed and a per-animal seed that fills a <see cref="FaunaPose"/>. Quadrupeds walk in the four-beat lateral
    /// sequence and trot in diagonal pairs (leg swing, knee and hock flex in the swing phase, body bob and a head nod),
    /// graze head-down with a step now and then, lie on the sternum chewing the cud (cows) or curl up asleep (dogs),
    /// sit, bark and scratch an ear (dogs); every idle has breathing, a swishing tail, ear flicks and slow head turns
    /// timed by the seed. Macaques walk on all fours, sit, groom and climb. Birds stand, walk with a head bob, peck,
    /// hop, take off, flap, glide, soar with a twisting tail and land; ducks paddle. Allocation-free; deterministic.
    /// <para>
    /// Flight clips (<see cref="IsFlight"/>) place the body centre at the model origin with the body level, so a
    /// presenter puts the origin at the bird's position and banks it freely; all other clips keep the feet on the
    /// ground at y = 0.
    /// </para>
    /// </summary>
    public static class FaunaAnimator
    {
        /// <summary>True for the clips that hold the bird in the air.</summary>
        public static bool IsFlight(FaunaClip c)
        {
            return c == FaunaClip.Flap || c == FaunaClip.Glide || c == FaunaClip.Soar || c == FaunaClip.TakeOff || c == FaunaClip.Land;
        }

        /// <summary>Share of the street cattle that graze when the sim has them standing (at the vegetable waste;
        /// street_life §7.1: standing 30% of the time, nearly always eating).</summary>
        public const float StreetGrazeShare = 0.7f;

        /// <summary>Dogs walking faster than this trot (m/s).</summary>
        public const float DogTrotMps = 1.3f;

        /// <summary>A sitting dog scratches for <see cref="ScratchS"/> seconds once in every <see cref="ScratchEveryS"/>.</summary>
        public const float ScratchS = 2.4f, ScratchEveryS = 22f;

        /// <summary>
        /// The generated clip a street animal of the traffic sim plays (AnimalSim knows only lie, stand, walk, sleep,
        /// sit and bark): a standing cow, calf or ox grazes (a stable share of the animals, by agent), a dog walking
        /// faster than <see cref="DogTrotMps"/> trots, and a sitting dog scratches an ear for a couple of seconds now and
        /// then (its own phase, by agent). Everything else maps across directly.
        /// </summary>
        public static FaunaClip StreetClip(FaunaSpecies species, byte simClip, float speedMps, int agentId, float timeS)
        {
            var c = (FaunaClip)simClip;
            switch (FaunaCatalog.Family(species))
            {
                case FaunaFamily.Bovine:
                    if (c == FaunaClip.Stand && FaunaRng.Unit(FaunaRng.Hash((uint)agentId, 0x6A2Eu)) < StreetGrazeShare) return FaunaClip.Graze;
                    return c;
                case FaunaFamily.Canine:
                    if (c == FaunaClip.Walk && speedMps > DogTrotMps) return FaunaClip.Trot;
                    if (c == FaunaClip.Sit)
                    {
                        float phase = timeS + ScratchEveryS * FaunaRng.Unit(FaunaRng.Hash((uint)agentId, 0x5C2Au));
                        if (phase - ScratchEveryS * (float)Math.Floor(phase / ScratchEveryS) < ScratchS) return FaunaClip.Scratch;
                    }
                    return c;
                default:
                    return c;
            }
        }

        /// <summary>Length in seconds of one cycle of a looping clip at <paramref name="speedMps"/> (for keyframe baking).</summary>
        public static float CycleSeconds(FaunaSpecies s, FaunaClip clip, float speedMps)
        {
            FaunaSpeciesInfo info = FaunaCatalog.Info(s);
            switch (clip)
            {
                case FaunaClip.Walk:
                case FaunaClip.Trot:
                case FaunaClip.Run:
                    return 1f / StepHz(info, clip, speedMps);
                case FaunaClip.Flap:
                case FaunaClip.TakeOff:
                    return 1f / Math.Max(0.5f, info.FlapHz * (clip == FaunaClip.TakeOff ? 1.3f : 1f));
                case FaunaClip.Peck:
                    return 0.6f;
                case FaunaClip.Hop:
                    return 0.45f;
                case FaunaClip.Scratch:
                    return 1f / 6f;
                default:
                    return 4f;
            }
        }

        /// <summary>Stride frequency (cycles per second) of a gait at a speed.</summary>
        public static float StepHz(FaunaSpeciesInfo info, FaunaClip clip, float speedMps)
        {
            float v = speedMps > 0.05f ? speedMps : clip == FaunaClip.Walk ? info.WalkMps : info.TrotMps;
            // Stride length grows with leg length (about 1.1 × withers height walking, 1.6 × trotting).
            float stride = info.HeightM * (clip == FaunaClip.Walk ? (info.IsAvian ? 0.9f : 1.1f) : clip == FaunaClip.Trot ? 1.6f : 2.2f);
            return FMath.Clamp(v / Math.Max(0.05f, stride), 0.3f, 8f);
        }

        /// <summary>
        /// Fills <paramref name="pose"/> for <paramref name="mesh"/>'s species (its pivots set heights) playing
        /// <paramref name="clip"/> at clip time <paramref name="t"/> (seconds) and ground speed
        /// <paramref name="speedMps"/>; <paramref name="seed"/> varies idles and timing between animals.
        /// </summary>
        public static void Evaluate(FaunaMesh mesh, FaunaClip clip, float t, float speedMps, uint seed, FaunaPose pose)
        {
            pose.Reset();
            FaunaSpeciesInfo info = FaunaCatalog.Info(mesh.Species);
            // De-phase identical animals.
            float tt = t + (seed & 0xFFFF) * (1f / 6553.6f);
            switch (info.Family)
            {
                case FaunaFamily.Fowl:
                case FaunaFamily.Bird:
                    Avian(mesh, info, clip, tt, speedMps, seed, pose);
                    break;
                case FaunaFamily.Primate:
                    Primate(mesh, info, clip, tt, speedMps, seed, pose);
                    break;
                default:
                    Quadruped(mesh, info, clip, tt, speedMps, seed, pose);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Quadrupeds (cattle, buffalo, dogs, goats)

        private static void Quadruped(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, float speed, uint seed, FaunaPose p)
        {
            bool bovine = info.Family == FaunaFamily.Bovine, dog = info.Family == FaunaFamily.Canine;
            float h = info.HeightM;
            switch (clip)
            {
                case FaunaClip.Walk:
                case FaunaClip.Trot:
                case FaunaClip.Run:
                    Gait(m, info, clip, t, speed, p);
                    break;
                case FaunaClip.Graze:
                {
                    // Head down at the waste pile or grass; a slow step now and then.
                    float neck = bovine ? 56f : 64f;
                    p.Set(FaunaBone.Neck, neck + 3f * FMath.Sin(t * 0.7f), 6f * FMath.Sin(t * 0.23f), 0f);
                    p.Set(FaunaBone.Head, bovine ? 28f : 30f, 0f, 3f * FMath.Sin(t * 0.5f));
                    p.Set(FaunaBone.Chest, 6f, 0f, 0f);
                    // Tearing and chewing.
                    float chew = FMath.Sin(t * 9f);
                    p.Set(FaunaBone.Jaw, 6f + 5f * chew, 3f * FMath.Sin(t * 4.5f), 0f);
                    // A step every ~6 s: one front leg moves forward and back.
                    float stepT = FMath.Frac(t / 6.3f);
                    if (stepT < 0.15f)
                    {
                        float s = FMath.Pulse(stepT / 0.15f);
                        p.Set(FaunaBone.FrontUpperL, -14f * s, 0f, 0f);
                        p.Set(FaunaBone.FrontLowerL, 30f * s, 0f, 0f);
                    }
                    Idle(m, info, t, seed, p, false);
                    break;
                }
                case FaunaClip.Lie:
                    if (dog) DogLie(m, t, seed, p, false);
                    else Kush(m, info, t, seed, p);
                    break;
                case FaunaClip.Sleep:
                    if (dog) DogLie(m, t, seed, p, true);
                    else
                    {
                        Kush(m, info, t, seed, p);
                        p.Set(FaunaBone.Neck, 30f, -55f, 0f);
                        p.Set(FaunaBone.Head, 25f, -30f, 15f);
                        p.Set(FaunaBone.Jaw, 0f, 0f, 0f);
                    }
                    break;
                case FaunaClip.Sit:
                    Sit(m, info, t, seed, p);
                    break;
                case FaunaClip.Scratch:
                    Sit(m, info, t, seed, p);
                    // Left hind foot up to the ear, kicking fast; head tilted into it.
                    p.Set(FaunaBone.HindUpperL, -122f, 10f, -30f);
                    p.Set(FaunaBone.HindLowerL, 55f + 20f * FMath.Sin(t * FMath.TwoPi * 6f), 0f, 0f);
                    p.Set(FaunaBone.HindFootL, 10f, 0f, 0f);
                    p.Set(FaunaBone.Neck, 30f, -28f, -30f);
                    p.Set(FaunaBone.Head, 12f, -18f, -28f);
                    p.Set(FaunaBone.EarL, 0f, 0f, -20f + 8f * FMath.Sin(t * 37f));
                    break;
                case FaunaClip.Bark:
                {
                    // Standing, head up, a bark every ~0.6 s with a jolt.
                    float ph = FMath.Frac(t / 0.6f);
                    float bark = ph < 0.25f ? FMath.Pulse(ph / 0.25f) : 0f;
                    p.Set(FaunaBone.Neck, -18f - 6f * bark, 0f, 0f);
                    p.Set(FaunaBone.Head, -8f - 10f * bark, 0f, 0f);
                    p.Set(FaunaBone.Jaw, 32f * bark, 0f, 0f);
                    p.Set(FaunaBone.Chest, -3f * bark, 0f, 0f);
                    p.RootOffset = new Fv3(0f, 0.01f * h * bark, -0.02f * h * bark);
                    p.SetPair(FaunaBone.EarL, -15f, 0f, 0f);
                    Tail(info, t, seed, p, 1.4f);
                    break;
                }
                default: // Stand
                    Idle(m, info, t, seed, p, true);
                    if (bovine)
                    {
                        // Chewing the cud while standing.
                        p.Set(FaunaBone.Jaw, 3f, 6f * FMath.Sin(t * 7.5f), 0f);
                    }
                    break;
            }
        }

        /// <summary>Breathing, tail swish, ear flicks, slow head turns (and, when <paramref name="head"/>, the head
        /// pose of an idle animal).</summary>
        private static void Idle(FaunaMesh m, FaunaSpeciesInfo info, float t, uint seed, FaunaPose p, bool head)
        {
            float breathe = FMath.Sin(t * FMath.TwoPi * (info.Family == FaunaFamily.Canine ? 0.45f : 0.25f));
            p.Add(FaunaBone.Chest, 0.8f * breathe, 0f, 0f);
            if (head)
            {
                // Look around: a slow turn picked per 5 s window.
                uint w = FaunaRng.Hash(seed, (uint)(t / 5f));
                float target = (FaunaRng.Unit(w) - 0.5f) * 50f;
                float prev = (FaunaRng.Unit(FaunaRng.Hash(seed, (uint)(t / 5f) - 1u)) - 0.5f) * 50f;
                float k = FMath.SmoothStep(FMath.Clamp01(FMath.Frac(t / 5f) / 0.4f));
                float yaw = FMath.Lerp(prev, target, k);
                p.Add(FaunaBone.Neck, 2f * breathe, 0.55f * yaw, 0f);
                p.Add(FaunaBone.Head, 0f, 0.45f * yaw, 0f);
            }
            Ears(t, seed, p);
            Tail(info, t, seed, p, 1f);
        }

        private static void Ears(float t, uint seed, FaunaPose p)
        {
            // An ear flick in some 2 s windows (one ear or both).
            uint w = FaunaRng.Hash(seed ^ 0xEA25u, (uint)(t / 2f));
            float u = FaunaRng.Unit(w);
            float ph = FMath.Frac(t / 2f);
            float flick = u < 0.35f && ph < 0.18f ? FMath.Pulse(ph / 0.18f) : 0f;
            bool left = (w & 0x100u) != 0, both = (w & 0x200u) != 0;
            p.Set(FaunaBone.EarL, -10f * (left || both ? flick : 0f), 0f, -25f * (left || both ? flick : 0f));
            p.Set(FaunaBone.EarR, -10f * (!left || both ? flick : 0f), 0f, 25f * (!left || both ? flick : 0f));
        }

        private static void Tail(FaunaSpeciesInfo info, float t, uint seed, FaunaPose p, float amount)
        {
            // Added on top of whatever the clip set (a sitting dog's tail on the ground still wags).
            switch (info.Family)
            {
                case FaunaFamily.Canine:
                {
                    // The curled tail wags gently.
                    float wag = FMath.Sin(t * FMath.TwoPi * 1.6f) * 10f * amount;
                    p.Add(FaunaBone.Tail0, 0f, wag, 0f);
                    p.Add(FaunaBone.Tail1, 0f, 0.6f * wag, 0f);
                    break;
                }
                case FaunaFamily.Caprine:
                {
                    float flick = FMath.Frac(t / 1.7f) < 0.12f ? FMath.Pulse(FMath.Frac(t / 1.7f) / 0.12f) : 0f;
                    p.Add(FaunaBone.Tail1, -15f * flick, 20f * flick * FMath.Sin(t * 40f), 0f);
                    break;
                }
                case FaunaFamily.Primate:
                    p.Add(FaunaBone.Tail1, 0f, 6f * FMath.Sin(t * 0.9f) * amount, 0f);
                    p.Add(FaunaBone.Tail2, 0f, 8f * FMath.Sin(t * 0.9f - 0.7f) * amount, 0f);
                    break;
                default:
                {
                    // Cattle: a lazy swish plus a sharp flick at flies now and then.
                    float swish = FMath.Sin(t * FMath.TwoPi * 0.32f);
                    uint w = FaunaRng.Hash(seed ^ 0x7A11u, (uint)(t / 3f));
                    float ph = FMath.Frac(t / 3f);
                    float flick = FaunaRng.Unit(w) < 0.4f && ph < 0.3f ? FMath.Pulse(ph / 0.3f) : 0f;
                    float side = (w & 1u) != 0 ? 1f : -1f;
                    p.Add(FaunaBone.Tail0, 2f, 6f * swish * amount + 18f * flick * side, 0f);
                    p.Add(FaunaBone.Tail1, 0f, 10f * FMath.Sin(t * FMath.TwoPi * 0.32f - 0.8f) * amount + 25f * flick * side, 0f);
                    p.Add(FaunaBone.Tail2, 0f, 14f * FMath.Sin(t * FMath.TwoPi * 0.32f - 1.6f) * amount + 30f * flick * side, 0f);
                    break;
                }
            }
        }

        /// <summary>Walk (lateral sequence, duty 0.65), trot (diagonal pairs, duty 0.45) and gallop-ish run.</summary>
        private static void Gait(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, float speed, FaunaPose p)
        {
            float hz = StepHz(info, clip, speed);
            float ph = t * hz;
            bool walk = clip == FaunaClip.Walk;
            float duty = walk ? 0.65f : clip == FaunaClip.Trot ? 0.45f : 0.35f;
            float swingDeg = walk ? 18f : clip == FaunaClip.Trot ? 26f : 34f;
            // Phase offsets: LH, LF, RH, RF.
            float oLH, oLF, oRH, oRF;
            if (walk)
            {
                oLH = 0f;
                oLF = 0.25f;
                oRH = 0.5f;
                oRF = 0.75f;
            }
            else if (clip == FaunaClip.Trot)
            {
                oLF = 0f;
                oRH = 0f;
                oRF = 0.5f;
                oLH = 0.5f;
            }
            else
            {
                oLF = 0f;
                oRF = 0.1f;
                oLH = 0.5f;
                oRH = 0.6f;
            }
            Leg(p, FaunaBone.FrontUpperL, true, ph - oLF, duty, swingDeg);
            Leg(p, FaunaBone.FrontUpperR, true, ph - oRF, duty, swingDeg);
            Leg(p, FaunaBone.HindUpperL, false, ph - oLH, duty, swingDeg);
            Leg(p, FaunaBone.HindUpperR, false, ph - oRH, duty, swingDeg);
            float h = info.HeightM;
            float bob = walk ? 0.012f : 0.03f;
            float b2 = FMath.Sin(ph * FMath.TwoPi * 2f);
            p.RootOffset = new Fv3(0f, bob * h * b2, 0f);
            p.Add(FaunaBone.Chest, 0f, 0f, (walk ? 2f : 3f) * FMath.Sin(ph * FMath.TwoPi));
            p.Add(FaunaBone.Pelvis, 0f, 0f, (walk ? -2.5f : -3f) * FMath.Sin(ph * FMath.TwoPi));
            // Head nod (cattle and goats nod with each front step; dogs hold the head steadier).
            float nod = info.Family == FaunaFamily.Canine ? 2f : 6f;
            p.Set(FaunaBone.Neck, (info.Family == FaunaFamily.Bovine ? 6f : -4f) + nod * FMath.Sin(ph * FMath.TwoPi * 2f + 0.8f), 0f, 0f);
            p.Set(FaunaBone.Head, nod * 0.5f * FMath.Sin(ph * FMath.TwoPi * 2f + 1.4f), 0f, 0f);
            if (info.Family == FaunaFamily.Canine && !walk) p.Set(FaunaBone.Jaw, 10f, 0f, 0f); // panting
            Ears(t, 0x5EEDu, p);
            Tail(info, t, 0x7A11u, p, walk ? 1f : 1.3f);
        }

        /// <summary>One leg at gait phase <paramref name="ph"/>: stance sweeps the leg back, swing lifts and brings it
        /// forward with the lower joints flexed.</summary>
        private static void Leg(FaunaPose p, FaunaBone upper, bool front, float ph, float duty, float swingDeg)
        {
            float u = FMath.Frac(ph);
            float angle, flex;
            if (u < duty)
            {
                // Stance: from forward (−) to back (+) linearly.
                float s = u / duty;
                angle = FMath.Lerp(-swingDeg, swingDeg, s);
                flex = 0f;
            }
            else
            {
                float s = (u - duty) / (1f - duty);
                angle = FMath.Lerp(swingDeg, -swingDeg, FMath.SmoothStep(s));
                flex = FMath.Pulse(s);
            }
            FaunaBone lower = (FaunaBone)((int)upper + 1), foot = (FaunaBone)((int)upper + 2);
            p.Set(upper, angle, 0f, 0f);
            if (front)
            {
                // Carpus folds back, hoof curls.
                p.Set(lower, 55f * flex, 0f, 0f);
                p.Set(foot, 35f * flex, 0f, 0f);
            }
            else
            {
                // Hock flexes, the cannon swings forward under the body.
                p.Set(upper, angle - 12f * flex, 0f, 0f);
                p.Set(lower, 40f * flex, 0f, 0f);
                p.Set(foot, 30f * flex, 0f, 0f);
            }
        }

        /// <summary>Cattle and goats lying on the sternum: front legs folded under the chest, hind legs tucked to one side,
        /// head up chewing the cud.</summary>
        private static void Kush(FaunaMesh m, FaunaSpeciesInfo info, float t, uint seed, FaunaPose p)
        {
            float h = info.HeightM;
            float side = (seed & 1u) != 0 ? 1f : -1f;
            RestOnBelly(m, p, 0.05f * h, 0f, 6f * side);
            // Knees forward on the ground, cannons folded back under the chest.
            p.SetPair(FaunaBone.FrontUpperL, -30f, 0f, 6f);
            p.SetPair(FaunaBone.FrontLowerL, 150f, 0f, 0f);
            p.SetPair(FaunaBone.FrontFootL, 20f, 0f, 0f);
            // Hind legs folded forward under the belly, the outer one splayed to the side the body leans away from.
            FaunaBone hu = side > 0f ? FaunaBone.HindUpperL : FaunaBone.HindUpperR;
            FaunaBone hu2 = side > 0f ? FaunaBone.HindUpperR : FaunaBone.HindUpperL;
            p.Set(hu, -62f, side * 6f, -side * 40f);
            p.Set((FaunaBone)((int)hu + 1), 150f, 0f, 0f);
            p.Set((FaunaBone)((int)hu + 2), 10f, 0f, 0f);
            p.Set(hu2, -72f, 0f, side * 4f);
            p.Set((FaunaBone)((int)hu2 + 1), 160f, 0f, 0f);
            p.Set((FaunaBone)((int)hu2 + 2), 5f, 0f, 0f);
            // Head up and level, chewing side to side.
            p.Set(FaunaBone.Neck, -6f, 8f * FMath.Sin(t * 0.21f), -side * 6f);
            p.Set(FaunaBone.Head, 4f, 0f, 0f);
            p.Set(FaunaBone.Jaw, 3f, 7f * FMath.Sin(t * 7.2f), 0f);
            // Tail hanging from the tailhead and lying along the ground beside the rump.
            p.Set(FaunaBone.Tail0, 12f, side * 28f, 0f);
            p.Set(FaunaBone.Tail1, 0f, side * 10f, 0f);
            p.Set(FaunaBone.Tail2, 75f, side * 30f, 0f);
            Ears(t, seed, p);
            float breathe = FMath.Sin(t * FMath.TwoPi * 0.22f);
            p.Add(FaunaBone.Chest, 0.8f * breathe, 0f, 0f);
        }

        /// <summary>Dogs: lying in the sphinx pose, or asleep flat on one side in the sun.</summary>
        private static void DogLie(FaunaMesh m, float t, uint seed, FaunaPose p, bool asleep)
        {
            float side = (seed & 2u) != 0 ? 1f : -1f;
            float breathe = FMath.Sin(t * FMath.TwoPi * (asleep ? 0.3f : 0.45f));
            if (!asleep)
            {
                // Sphinx: lying on the belly, front legs forward, hind legs folded, head up.
                RestOnBelly(m, p, 0.035f, 0f, 0f);
                p.SetPair(FaunaBone.FrontUpperL, -58f, 0f, 0f);
                p.SetPair(FaunaBone.FrontLowerL, -22f, 0f, 0f);
                p.SetPair(FaunaBone.FrontFootL, 80f, 0f, 0f);
                p.SetPair(FaunaBone.HindUpperL, -104f, 0f, -16f);
                p.SetPair(FaunaBone.HindLowerL, 192f, 0f, 0f);
                p.SetPair(FaunaBone.HindFootL, -8f, 0f, 0f);
                p.Set(FaunaBone.Neck, 18f, 0f, 0f);
                p.Set(FaunaBone.Head, 4f, 10f * FMath.Sin(t * 0.3f), 0f);
                p.Add(FaunaBone.Chest, 1f * breathe, 0f, 0f);
                Ears(t, seed, p);
                return;
            }
            // Asleep the way the street dogs of the valley sleep in the sun: flat on one side, legs stretched out, head
            // and tail on the ground in line with the spine. The barrel rests on its flank; the dog is moved back over
            // its place so it covers about the same ground as standing.
            float w = Math.Max(0.04f, m.BodyHalfWidthM);
            float spine = m.Pivot[(int)FaunaBone.Chest].Y;
            p.SetRoot(0f, 0f, side * 90f);
            p.RootOffset = new Fv3(side * 0.5f * spine, w + 0.008f, 0f);
            // Gravity points to -X in the dog's frame when it lies on its left flank (side > 0), to +X on the right:
            // a hanging leg droops by a roll of -side, the neck, head and tail (held up and back) by a roll of +side.
            float droop = -side;
            p.Set(FaunaBone.Chest, 0f, 0f, 0f);
            p.Set(FaunaBone.Neck, 38f, 0f, side * 14f);
            p.Set(FaunaBone.Head, 8f, 0f, side * 12f);
            // The legs: the lower pair on the ground, the upper pair resting on them, a little apart and relaxed.
            bool leftLow = side > 0f;
            LegsAside(p, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, -24f, 18f, 30f, leftLow ? 0f : droop * 10f);
            LegsAside(p, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, -6f, 34f, 40f, leftLow ? droop * 10f : 0f);
            LegsAside(p, FaunaBone.HindUpperL, FaunaBone.HindLowerL, FaunaBone.HindFootL, 22f, 12f, 24f, leftLow ? 0f : droop * 10f);
            LegsAside(p, FaunaBone.HindUpperR, FaunaBone.HindLowerR, FaunaBone.HindFootR, 4f, 30f, 30f, leftLow ? droop * 10f : 0f);
            p.Set(FaunaBone.Tail0, -40f, -side * 20f, side * 30f);
            p.Set(FaunaBone.Tail1, -28f, 0f, 0f);
            p.Set(FaunaBone.Tail2, -16f, 0f, 0f);
            p.SetPair(FaunaBone.EarL, 30f, 0f, 0f);
            // Slow deep breaths lift the ribs; now and then a twitch of a paw.
            p.Add(FaunaBone.Chest, 0f, 0f, droop * 1.6f * breathe);
            float twitch = FMath.Sin(t * 7f) * Math.Max(0f, FMath.Sin(t * 0.37f + seed % 7u) - 0.9f) * 60f;
            p.Add(leftLow ? FaunaBone.FrontLowerR : FaunaBone.FrontLowerL, twitch, 0f, 0f);
        }

        private static void LegsAside(FaunaPose p, FaunaBone upper, FaunaBone lower, FaunaBone foot, float upperPitch, float lowerPitch, float footPitch, float roll)
        {
            p.Set(upper, upperPitch, 0f, roll);
            p.Set(lower, lowerPitch, 0f, 0f);
            p.Set(foot, footPitch, 0f, 0f);
        }

        /// <summary>Sitting on the haunches: rear on the ground, front legs straight, head up.</summary>
        private static void Sit(FaunaMesh m, FaunaSpeciesInfo info, float t, uint seed, FaunaPose p)
        {
            float tilt = 42f;
            // Rotate about the hip (pelvis pivot) so the rump drops onto the ground.
            Fv3 hip = m.Pivot[(int)FaunaBone.HindUpperL];
            hip = new Fv3(0f, hip.Y, hip.Z);
            p.SetRoot(-tilt, 0f, 0f);
            Fv3 rotated = p.RootRot * hip;
            // Seat the rump (about 0.45 of the hip height below the hip) on the ground.
            float seat = hip.Y * 0.42f;
            p.RootOffset = new Fv3(0f, hip.Y - rotated.Y - (hip.Y - seat), hip.Z - rotated.Z);
            p.SetPair(FaunaBone.FrontUpperL, tilt - 6f, 0f, 0f);
            p.SetPair(FaunaBone.FrontLowerL, 0f, 0f, 0f);
            p.SetPair(FaunaBone.FrontFootL, 8f, 0f, 0f);
            p.SetPair(FaunaBone.HindUpperL, -78f + tilt * 0.2f, 0f, -12f);
            p.SetPair(FaunaBone.HindLowerL, 140f, 0f, 0f);
            p.SetPair(FaunaBone.HindFootL, -60f, 0f, 0f);
            p.Set(FaunaBone.Neck, 18f, 0f, 0f);
            p.Set(FaunaBone.Head, 14f, 0f, 0f);
            p.Set(FaunaBone.Tail0, 60f, 30f, 0f);
            Idle(m, info, t, seed, p, true);
        }

        /// <summary>
        /// Lowers and levels the body so the belly rests on the ground: a nose-up tilt that brings the higher (rear)
        /// belly point down to the front one, then a drop that puts both <paramref name="clearance"/> above the ground.
        /// </summary>
        private static void RestOnBelly(FaunaMesh m, FaunaPose p, float clearance, float extraPitchDeg, float rollDeg)
        {
            float dz = Math.Max(0.05f, m.BellyFrontZ - m.BellyRearZ);
            float tilt = (float)Math.Atan2(m.BellyRearY - m.BellyFrontY, dz) / FMath.Deg;
            p.SetRoot(-tilt + extraPitchDeg, 0f, rollDeg);
            Fv3 f = p.RootRot * new Fv3(0f, m.BellyFrontY, m.BellyFrontZ);
            Fv3 r = p.RootRot * new Fv3(0f, m.BellyRearY, m.BellyRearZ);
            p.RootOffset = new Fv3(0f, clearance - Math.Min(f.Y, r.Y), 0f);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Macaques

        private static void Primate(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, float speed, uint seed, FaunaPose p)
        {
            float s = info.HeightM / 0.42f;
            switch (clip)
            {
                case FaunaClip.Walk:
                case FaunaClip.Trot:
                    Gait(m, info, FaunaClip.Walk, t, speed, p);
                    p.Set(FaunaBone.Tail0, -10f, 0f, 0f);
                    p.Set(FaunaBone.Tail1, -20f, 10f * FMath.Sin(t * 3f), 0f);
                    break;
                case FaunaClip.Run:
                {
                    // Bounding: front pair and hind pair together.
                    float hz = StepHz(info, FaunaClip.Run, speed);
                    float ph = t * hz;
                    Leg(p, FaunaBone.FrontUpperL, true, ph, 0.35f, 38f);
                    Leg(p, FaunaBone.FrontUpperR, true, ph - 0.06f, 0.35f, 38f);
                    Leg(p, FaunaBone.HindUpperL, false, ph - 0.5f, 0.35f, 38f);
                    Leg(p, FaunaBone.HindUpperR, false, ph - 0.56f, 0.35f, 38f);
                    float arch = FMath.Sin(ph * FMath.TwoPi);
                    p.Set(FaunaBone.Chest, 10f * arch, 0f, 0f);
                    p.Set(FaunaBone.Pelvis, -8f * arch, 0f, 0f);
                    p.RootOffset = new Fv3(0f, 0.05f * s * Math.Abs(arch), 0f);
                    p.Set(FaunaBone.Tail0, -30f, 0f, 0f);
                    break;
                }
                case FaunaClip.Climb:
                    MacaqueClimb(m, info, t, seed, s, p);
                    break;
                case FaunaClip.Groom:
                case FaunaClip.Sit:
                case FaunaClip.Lie:
                case FaunaClip.Sleep:
                default:
                    MacaqueSit(m, info, clip, t, seed, s, p);
                    break;
            }
        }

        /// <summary>
        /// A macaque sitting on its haunches, hunched a little (the Swayambhu and Pashupati photos): the knees up in front,
        /// the shins upright and the feet flat, the tail lying behind. The arms are placed by two-bone reaching (elbows
        /// back), by the seed: both hands on the ground between the feet, or one hand at the mouth with a bite of food
        /// and the other on the ground, or the forearms resting on the knees. Grooming picks through the fur in front of
        /// the belly with both hands; asleep the head drops onto the chest.
        /// </summary>
        private static void MacaqueSit(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, uint seed, float s, FaunaPose p)
        {
            const float root = -68f;
            p.SetRoot(root, 0f, 0f);
            Fv3 hip = m.Pivot[(int)FaunaBone.Pelvis];
            Fv3 r = p.RootRot * hip;
            // The rump rests on the ground (the pelvis pivot about 12 cm up on an adult).
            p.RootOffset = new Fv3(0f, -r.Y + 0.122f * s, hip.Z - r.Z - 0.04f * s);
            p.Set(FaunaBone.Neck, 50f, 0f, 0f);
            p.Set(FaunaBone.Head, 16f, 0f, 0f);
            // The tail lies on the ground behind.
            p.Set(FaunaBone.Tail0, 112f, 12f, 0f);
            p.Set(FaunaBone.Tail1, 12f, 15f, 0f);
            p.Set(FaunaBone.Tail2, 6f, 15f, 0f);

            // Legs: each foot flat on the ground in front of the rump, a little apart, the knee folded up.
            Fv3 hipW = World(m, p, FaunaBone.HindUpperL);
            float ankleUp = m.Pivot[(int)FaunaBone.HindFootL].Y;
            float footZ = hipW.Z + 0.17f * s;
            float legSum = Reach(m, p, root, FaunaBone.HindUpperL, FaunaBone.HindLowerL, FaunaBone.HindFootL, ankleUp, footZ, true, -16f);
            Reach(m, p, root, FaunaBone.HindUpperR, FaunaBone.HindLowerR, FaunaBone.HindFootR, ankleUp, footZ, true, 16f);
            p.SetPair(FaunaBone.HindFootL, -legSum, 0f, 0f);

            // Arms by variant.
            Fv3 shW = World(m, p, FaunaBone.FrontUpperL);
            float wristUp = m.Pivot[(int)FaunaBone.FrontFootL].Y;
            bool groom = clip == FaunaClip.Groom, sleep = clip == FaunaClip.Sleep || clip == FaunaClip.Lie;
            int variant = groom || sleep ? 3 : (int)((seed >> 5) % 3u);
            float l, rr;
            switch (variant)
            {
                case 0:
                    // Both hands on the ground between the feet.
                    l = Reach(m, p, root, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, wristUp, footZ + 0.01f * s, false, 6f);
                    p.Set(FaunaBone.FrontFootL, -l, 0f, 0f);
                    rr = Reach(m, p, root, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, wristUp, footZ + 0.01f * s, false, -6f);
                    p.Set(FaunaBone.FrontFootR, -rr, 0f, 0f);
                    Idle(m, info, t, seed, p, true);
                    break;
                case 1:
                {
                    // Eating: the right hand brings a bite to the mouth now and then, the left rests on the ground.
                    l = Reach(m, p, root, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, wristUp, footZ, false, 6f);
                    p.Set(FaunaBone.FrontFootL, -l, 0f, 0f);
                    float bite = FMath.SmoothStep(FMath.Clamp01(1.6f * FMath.Sin(t * 1.3f) + 0.4f));
                    float headPitch = 16f + 10f * bite;
                    Fv3 mouth = Mouth(m, p, root, 50f, headPitch);
                    float ty = FMath.Lerp(shW.Y - 0.16f * s, mouth.Y - 0.035f * s, bite), tz = FMath.Lerp(shW.Z + 0.1f * s, mouth.Z - 0.005f * s, bite);
                    rr = Reach(m, p, root, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, ty, tz, false, -12f);
                    p.Set(FaunaBone.FrontFootR, FMath.Lerp(-40f, -150f, bite) - rr, 0f, 0f);
                    p.Set(FaunaBone.Head, headPitch, 0f, 0f);
                    Ears(t, seed, p);
                    Tail(info, t, seed, p, 1f);
                    break;
                }
                case 2:
                {
                    // Forearms resting on the knees, the hands hanging in front of them.
                    Fv3 kneeW = World(m, p, FaunaBone.HindLowerL);
                    l = Reach(m, p, root, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, kneeW.Y + 0.01f * s, kneeW.Z + 0.1f * s, false, 4f);
                    p.Set(FaunaBone.FrontFootL, 60f - l, 0f, 0f);
                    rr = Reach(m, p, root, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, kneeW.Y + 0.01f * s, kneeW.Z + 0.1f * s, false, -4f);
                    p.Set(FaunaBone.FrontFootR, 60f - rr, 0f, 0f);
                    Idle(m, info, t, seed, p, true);
                    break;
                }
                default:
                {
                    // Grooming (or dozing): both hands in front of the belly; grooming hands pick in turn.
                    float a = groom ? FMath.Sin(t * 5.3f) : 0f, b2 = groom ? FMath.Sin(t * 4.1f + 1f) : 0f;
                    float ty = shW.Y - 0.15f * s, tz = shW.Z + 0.11f * s;
                    l = Reach(m, p, root, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, ty + 0.02f * s * a, tz + 0.015f * s * a, false, 10f);
                    p.Set(FaunaBone.FrontFootL, -70f - l, 0f, 0f);
                    rr = Reach(m, p, root, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, ty + 0.02f * s * b2, tz + 0.015f * s * b2, false,
                               -10f);
                    p.Set(FaunaBone.FrontFootR, -70f - rr, 0f, 0f);
                    p.Set(FaunaBone.Neck, sleep ? 62f : 64f, 0f, 0f);
                    p.Set(FaunaBone.Head, sleep ? 42f : 24f, 0f, 0f);
                    Ears(t, seed, p);
                    if (!sleep) Tail(info, t, seed, p, 1f);
                    break;
                }
            }
        }

        /// <summary>
        /// Climbing a wall, a plinth or a trunk that rises in front (+Z): the body upright with the belly a hand's
        /// breadth off the wall, the hands reaching up the face in turn with the palms flat on it, the feet gripping
        /// below with the knees splayed out, the head tipped back to look up, the tail hanging. At clip time 0 the feet
        /// are at the foot of the wall (the sim lifts the whole animal as it climbs).
        /// </summary>
        private static void MacaqueClimb(FaunaMesh m, FaunaSpeciesInfo info, float t, uint seed, float s, FaunaPose p)
        {
            const float root = -86f;
            p.SetRoot(root, 0f, 0f);
            Fv3 hip = m.Pivot[(int)FaunaBone.Pelvis];
            Fv3 r = p.RootRot * hip;
            p.RootOffset = new Fv3(0f, -r.Y + 0.21f * s, -r.Z);
            float wall = World(m, p, FaunaBone.Pelvis).Z + 0.105f * s;
            float ph = t * 1.1f * FMath.TwoPi;
            float a = FMath.Sin(ph), b = FMath.Sin(ph + FMath.Pi);
            Fv3 sh = World(m, p, FaunaBone.FrontUpperL), hp = World(m, p, FaunaBone.HindUpperL);
            float l = Reach(m, p, root, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL, FaunaBone.FrontFootL, sh.Y + (0.26f + 0.05f * a) * s, wall - 0.01f * s, true, -22f);
            p.Set(FaunaBone.FrontFootL, -90f - l, 0f, 0f);
            float rr = Reach(m, p, root, FaunaBone.FrontUpperR, FaunaBone.FrontLowerR, FaunaBone.FrontFootR, sh.Y + (0.26f + 0.05f * b) * s, wall - 0.01f * s, true, 22f);
            p.Set(FaunaBone.FrontFootR, -90f - rr, 0f, 0f);
            l = Reach(m, p, root, FaunaBone.HindUpperL, FaunaBone.HindLowerL, FaunaBone.HindFootL, hp.Y - (0.13f - 0.04f * b) * s, wall, false, -34f);
            p.Set(FaunaBone.HindFootL, -90f - l, 0f, 0f);
            rr = Reach(m, p, root, FaunaBone.HindUpperR, FaunaBone.HindLowerR, FaunaBone.HindFootR, hp.Y - (0.13f - 0.04f * a) * s, wall, false, 34f);
            p.Set(FaunaBone.HindFootR, -90f - rr, 0f, 0f);
            p.Set(FaunaBone.Neck, 52f, 12f * FMath.Sin(t * 0.7f), 0f);
            p.Set(FaunaBone.Head, 8f, 0f, 0f);
            // The tail swings out behind, clear of the ground at the foot of the wall.
            p.Set(FaunaBone.Tail0, 112f, 0f, 0f);
            p.Set(FaunaBone.Tail1, -8f, 8f * a, 0f);
            Ears(t, seed, p);
        }

        /// <summary>Model-space position of the mouth (the jaw pivot) of a sitting animal whose neck and head are pitched
        /// by <paramref name="neckDeg"/> and <paramref name="headDeg"/> under a root pitch of <paramref name="rootDeg"/>
        /// (the chest unrotated).</summary>
        private static Fv3 Mouth(FaunaMesh m, FaunaPose p, float rootDeg, float neckDeg, float headDeg)
        {
            Fv3 neck = m.Pivot[(int)FaunaBone.Neck], head = m.Pivot[(int)FaunaBone.Head], jaw = m.Pivot[(int)FaunaBone.Jaw];
            Fv3 nw = p.RootRot * neck + p.RootOffset;
            Fv3 hw = nw + FRot.Euler((rootDeg + neckDeg) * FMath.Deg, 0f, 0f) * (head - neck);
            return hw + FRot.Euler((rootDeg + neckDeg + headDeg) * FMath.Deg, 0f, 0f) * (jaw - head);
        }

        /// <summary>Model-space position of a bone's pivot under the root transform alone (its ancestors up to the root
        /// unrotated, as in the sitting pose before the limbs are placed).</summary>
        private static Fv3 World(FaunaMesh m, FaunaPose p, FaunaBone b)
        {
            return p.RootRot * m.Pivot[(int)b] + p.RootOffset;
        }

        /// <summary>
        /// Two-bone reaching in the pitch plane: sets the local pitch of <paramref name="upper"/> and
        /// <paramref name="lower"/> (with <paramref name="rollDeg"/> on the upper bone) so that the pivot of
        /// <paramref name="end"/> reaches (<paramref name="ty"/>, <paramref name="tz"/>) in model space, the middle joint
        /// bending forward (<paramref name="jointForward"/>: a knee) or back (an elbow); targets out of reach are
        /// clamped to the limb's length. The upper bone's parents must be unrotated (the root alone, pitch
        /// <paramref name="rootDeg"/>). Returns the summed pitch carried into <paramref name="end"/> (root, upper, lower),
        /// so a hand or foot is laid flat with a local pitch of minus that.
        /// </summary>
        private static float Reach(FaunaMesh m, FaunaPose p, float rootDeg, FaunaBone upper, FaunaBone lower, FaunaBone end, float ty, float tz,
                                   bool jointForward, float rollDeg)
        {
            Fv3 s0 = m.Pivot[(int)upper], e0 = m.Pivot[(int)lower], w0 = m.Pivot[(int)end];
            float l1 = Plane(e0 - s0), l2 = Plane(w0 - e0);
            float b1 = Pitch(e0 - s0), b2 = Pitch(w0 - e0);
            Fv3 sw = p.RootRot * s0 + p.RootOffset;
            float dy = ty - sw.Y, dz = tz - sw.Z;
            float dist = (float)Math.Sqrt(dy * dy + dz * dz);
            float lo = Math.Abs(l1 - l2) + 1e-3f, hi = l1 + l2 - 1e-3f;
            float dc = FMath.Clamp(dist, lo, hi);
            float phi = (float)Math.Atan2(-dz, -dy);
            float cosA = FMath.Clamp((l1 * l1 + dc * dc - l2 * l2) / (2f * l1 * dc), -1f, 1f);
            float alpha = (float)Math.Acos(cosA);
            float t1 = jointForward ? phi - alpha : phi + alpha;
            // The end point actually reached (on the line to the target, at the clamped distance).
            float ey = sw.Y - l1 * (float)Math.Cos(t1), ez = sw.Z - l1 * (float)Math.Sin(t1);
            float wy = sw.Y + dy / Math.Max(1e-5f, dist) * dc, wz = sw.Z + dz / Math.Max(1e-5f, dist) * dc;
            float t2 = (float)Math.Atan2(-(wz - ez), -(wy - ey));
            float d1 = t1 / FMath.Deg - rootDeg - b1;
            float d2 = t2 / FMath.Deg - rootDeg - d1 - b2;
            p.Set(upper, d1, 0f, rollDeg);
            p.Set(lower, d2, 0f, 0f);
            return rootDeg + d1 + d2;
        }

        private static float Plane(Fv3 v)
        {
            return (float)Math.Sqrt(v.Y * v.Y + v.Z * v.Z);
        }

        /// <summary>Pitch (degrees) of a direction in the y-z plane: 0 straight down, positive swung back (−Z).</summary>
        private static float Pitch(Fv3 v)
        {
            return (float)Math.Atan2(-v.Z, -v.Y) / FMath.Deg;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Birds and fowl

        private static void Avian(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, float speed, uint seed, FaunaPose p)
        {
            bool air = IsFlight(clip);
            // Rest: the spread wings are hidden; in the air the folded wings are.
            float spread = air ? 1f : 0f;
            p.Scale[(int)FaunaBone.WingL] = p.Scale[(int)FaunaBone.WingR] = spread;
            p.Scale[(int)FaunaBone.FoldL] = p.Scale[(int)FaunaBone.FoldR] = 1f - spread;
            if (air)
            {
                Flight(m, info, clip, t, seed, p);
                return;
            }
            float h = info.HeightM;
            switch (clip)
            {
                case FaunaClip.Walk:
                case FaunaClip.Run:
                case FaunaClip.Swim:
                {
                    float hz = StepHz(info, FaunaClip.Walk, speed) * (clip == FaunaClip.Run ? 1.8f : 1f) * (clip == FaunaClip.Swim ? 0.8f : 1f);
                    float ph = t * hz;
                    float a = FMath.Sin(ph * FMath.TwoPi), b = FMath.Sin(ph * FMath.TwoPi + FMath.Pi);
                    float lift = clip == FaunaClip.Swim ? 0f : 1f;
                    p.Set(FaunaBone.HindUpperL, -25f * a, 0f, 0f);
                    p.Set(FaunaBone.HindLowerL, 35f * Math.Max(0f, -a) * lift, 0f, 0f);
                    p.Set(FaunaBone.HindUpperR, -25f * b, 0f, 0f);
                    p.Set(FaunaBone.HindLowerR, 35f * Math.Max(0f, -b) * lift, 0f, 0f);
                    // Head bob: the head holds still, then thrusts forward with each step (pigeons, chickens).
                    float thrust = FMath.Frac(ph * 2f);
                    float bob = thrust < 0.35f ? FMath.SmoothStep(thrust / 0.35f) : 1f - FMath.SmoothStep((thrust - 0.35f) / 0.65f);
                    p.Set(FaunaBone.Neck, -12f + 18f * bob, 0f, 0f);
                    p.Set(FaunaBone.Head, 10f - 18f * bob, 0f, 0f);
                    p.RootOffset = new Fv3(0f, 0.015f * h * Math.Abs(a), 0f);
                    p.SetRoot(0f, 0f, 3f * a);
                    p.Set(FaunaBone.Tail0, 0f, 6f * a, 0f);
                    break;
                }
                case FaunaClip.Peck:
                {
                    // Body tips forward, quick pecks at the ground.
                    float ph = FMath.Frac(t / 0.6f);
                    float peck = ph < 0.3f ? FMath.Pulse(ph / 0.3f) : 0f;
                    p.SetRoot(18f, 0f, 0f);
                    p.Set(FaunaBone.Neck, 40f + 25f * peck, 0f, 0f);
                    p.Set(FaunaBone.Head, 25f + 15f * peck, 0f, 0f);
                    p.SetPair(FaunaBone.HindUpperL, -18f, 0f, 0f);
                    p.Set(FaunaBone.Tail0, -12f, 0f, 0f);
                    break;
                }
                case FaunaClip.Hop:
                {
                    float ph = FMath.Frac(t / 0.45f);
                    float air2 = ph < 0.5f ? FMath.Pulse(ph / 0.5f) : 0f;
                    p.RootOffset = new Fv3(0f, 0.35f * h * air2, 0.25f * h * air2);
                    p.SetPair(FaunaBone.HindUpperL, 30f * air2, 0f, 0f);
                    p.SetPair(FaunaBone.HindLowerL, 40f * air2, 0f, 0f);
                    p.Set(FaunaBone.Tail0, -15f * air2, 0f, 0f);
                    break;
                }
                case FaunaClip.Sleep:
                case FaunaClip.Sit:
                case FaunaClip.Lie:
                    // Fluffed up, head sunk into the shoulders, sitting on the legs.
                    p.RootOffset = new Fv3(0f, -0.55f * LegHeight(m), 0f);
                    p.SetPair(FaunaBone.HindUpperL, -60f, 0f, 0f);
                    p.SetPair(FaunaBone.HindLowerL, 120f, 0f, 0f);
                    p.Set(FaunaBone.Neck, 35f, 0f, 0f);
                    p.Set(FaunaBone.Head, clip == FaunaClip.Sleep ? -10f : 0f, clip == FaunaClip.Sleep ? 150f : 0f, 0f);
                    p.Scale[(int)FaunaBone.Chest] = 1.05f;
                    break;
                default:
                {
                    // Standing: looks about with quick head turns.
                    uint w = FaunaRng.Hash(seed, (uint)(t / 1.3f));
                    float yaw = (FaunaRng.Unit(w) - 0.5f) * 70f;
                    p.Set(FaunaBone.Head, (FaunaRng.Unit(w >> 3) - 0.5f) * 20f, yaw, (FaunaRng.Unit(w >> 7) - 0.5f) * 15f);
                    p.Set(FaunaBone.Neck, 2f * FMath.Sin(t * 2.2f), 0f, 0f);
                    p.Set(FaunaBone.Tail0, 0f, 0f, 4f * FMath.Sin(t * 1.3f));
                    break;
                }
            }
        }

        private static float LegHeight(FaunaMesh m)
        {
            return m.Pivot[(int)FaunaBone.HindUpperL].Y;
        }

        /// <summary>How far an egret draws its neck in when it flies (scale of the neck about its base).</summary>
        public const float EgretNeckTuck = 0.5f;

        private static void Flight(FaunaMesh m, FaunaSpeciesInfo info, FaunaClip clip, float t, uint seed, FaunaPose p)
        {
            // Level the body (it is tilted up in the standing bind pose) and centre it on the origin.
            Fv3 pel = m.Pivot[(int)FaunaBone.Pelvis], che = m.Pivot[(int)FaunaBone.Chest];
            Fv3 axis = (che - pel).Normalized;
            float bindPitch = (float)Math.Atan2(axis.Y, axis.Z) / FMath.Deg;
            Fv3 centre = (pel + che) * 0.5f;
            float extraPitch = clip == FaunaClip.TakeOff ? -25f : clip == FaunaClip.Land ? -35f : 0f;
            p.SetRoot(bindPitch + extraPitch, 0f, 0f);
            Fv3 rc = p.RootRot * centre;
            p.RootOffset = -rc;
            // Legs: trailing straight back along the levelled body (the long-legged egret's feet stick out past the
            // tail; the small birds tuck theirs into the belly feathers), dangling forward to land, pushing off at take-off.
            // Rotations about the same axis add up, so a leg pitch of 90° minus the root pitch points the leg backwards.
            float root = bindPitch + extraPitch;
            bool egret = m.Species == FaunaSpecies.Egret;
            if (clip == FaunaClip.Land)
            {
                p.SetPair(FaunaBone.HindUpperL, -20f - root, 0f, 0f);
                p.SetPair(FaunaBone.HindLowerL, 25f, 0f, 0f);
            }
            else if (clip == FaunaClip.TakeOff)
            {
                p.SetPair(FaunaBone.HindUpperL, 45f - root, 0f, 0f);
                p.SetPair(FaunaBone.HindLowerL, 20f, 0f, 0f);
            }
            else
            {
                p.SetPair(FaunaBone.HindUpperL, 88f - root, 0f, 0f);
                p.SetPair(FaunaBone.HindLowerL, 0f, 0f, 0f);
                p.SetPair(FaunaBone.HindFootL, 25f, 0f, 0f);
                // Toes tucked away (seen only on the egret); the light bird shows no legs in the air at all.
                if (!egret) p.Scale[(int)FaunaBone.HindFootL] = p.Scale[(int)FaunaBone.HindFootR] = 0f;
                if (m.Lod >= 1) p.Scale[(int)FaunaBone.HindUpperL] = p.Scale[(int)FaunaBone.HindUpperR] = 0f;
            }
            if (egret)
            {
                // Herons and egrets fly with the neck pulled back into an S: the neck is drawn in (shortened about its
                // base and laid back) so the head rests just in front of the shoulders, the bill level and forward.
                const float neck = -70f;
                p.Set(FaunaBone.Neck, neck, 0f, 0f);
                p.Scale[(int)FaunaBone.Neck] = EgretNeckTuck;
                p.Scale[(int)FaunaBone.Head] = 1f / EgretNeckTuck;
                p.Set(FaunaBone.Head, -(root + neck) - 4f, 0f, 0f);
            }
            else
            {
                p.Set(FaunaBone.Neck, -bindPitch * 0.3f - 10f, 0f, 0f);
                p.Set(FaunaBone.Head, -6f, 0f, 0f);
            }
            switch (clip)
            {
                case FaunaClip.Glide:
                {
                    float wob = FMath.Sin(t * 1.7f);
                    p.SetPair(FaunaBone.WingL, 0f, 0f, -(8f + 2f * wob));
                    p.SetPair(FaunaBone.WingTipL, 0f, -4f, -3f);
                    break;
                }
                case FaunaClip.Soar:
                {
                    // Kite: wings flat with the hand angled back, the forked tail twisting to steer.
                    float tw = FMath.Sin(t * 0.9f + (seed & 0xFF) * 0.1f);
                    p.SetPair(FaunaBone.WingL, 0f, 2f, -(3f + 2f * FMath.Sin(t * 1.3f)));
                    p.SetPair(FaunaBone.WingTipL, 0f, -12f, 4f);
                    p.Set(FaunaBone.Tail0, 0f, 0f, 22f * tw);
                    p.Set(FaunaBone.Head, -8f, 20f * tw, 0f);
                    break;
                }
                default:
                {
                    // Flapping: the arm leads, the hand lags and flexes on the upstroke.
                    float hz = Math.Max(0.5f, info.FlapHz) * (clip == FaunaClip.TakeOff ? 1.3f : 1f);
                    float ph = t * hz * FMath.TwoPi;
                    float amp = clip == FaunaClip.Land ? 30f : 48f;
                    float arm = amp * FMath.Sin(ph);
                    float hand = amp * 0.7f * FMath.Sin(ph - 0.9f);
                    float fold = Math.Max(0f, FMath.Cos(ph)) * 18f;
                    p.SetPair(FaunaBone.WingL, 0f, 0f, -(10f + arm));
                    p.SetPair(FaunaBone.WingTipL, 0f, -fold, -hand * 0.6f);
                    p.RootOffset = p.RootOffset + new Fv3(0f, -0.02f * info.HeightM * FMath.Sin(ph), 0f);
                    p.Set(FaunaBone.Tail0, 6f * FMath.Sin(ph + 1f), 0f, 0f);
                    break;
                }
            }
        }
    }
}
