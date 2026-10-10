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
                {
                    // Body vertical against a wall or trunk, limbs reaching up in turn.
                    float hz = 1.1f;
                    float ph = t * hz;
                    p.SetRoot(-88f, 0f, 0f);
                    p.RootOffset = new Fv3(0f, 0.18f * s, -0.32f * s);
                    float a = FMath.Sin(ph * FMath.TwoPi), b = FMath.Sin(ph * FMath.TwoPi + FMath.Pi);
                    p.Set(FaunaBone.FrontUpperL, -60f - 30f * a, 0f, -10f);
                    p.Set(FaunaBone.FrontLowerL, 40f + 20f * a, 0f, 0f);
                    p.Set(FaunaBone.FrontUpperR, -60f - 30f * b, 0f, 10f);
                    p.Set(FaunaBone.FrontLowerR, 40f + 20f * b, 0f, 0f);
                    p.Set(FaunaBone.HindUpperL, -70f - 25f * b, 0f, -20f);
                    p.Set(FaunaBone.HindLowerL, 90f + 20f * b, 0f, 0f);
                    p.Set(FaunaBone.HindUpperR, -70f - 25f * a, 0f, 20f);
                    p.Set(FaunaBone.HindLowerR, 90f + 20f * a, 0f, 0f);
                    p.Set(FaunaBone.Neck, 50f, 15f * FMath.Sin(t * 0.7f), 0f);
                    p.Set(FaunaBone.Head, 25f, 0f, 0f);
                    p.Set(FaunaBone.Tail0, 40f, 0f, 0f);
                    break;
                }
                case FaunaClip.Groom:
                case FaunaClip.Sit:
                case FaunaClip.Lie:
                case FaunaClip.Sleep:
                default:
                {
                    // Sitting upright on the haunches.
                    p.SetRoot(-72f, 0f, 0f);
                    Fv3 hip = m.Pivot[(int)FaunaBone.Pelvis];
                    Fv3 r = p.RootRot * hip;
                    p.RootOffset = new Fv3(0f, -r.Y + 0.1f * s, hip.Z - r.Z - 0.05f * s);
                    // Knees up in front, feet flat on the ground.
                    p.SetPair(FaunaBone.HindUpperL, -42f, 0f, -22f);
                    p.SetPair(FaunaBone.HindLowerL, 118f, 0f, 0f);
                    p.SetPair(FaunaBone.HindFootL, -62f, 0f, 0f);
                    p.Set(FaunaBone.Neck, 52f, 0f, 0f);
                    p.Set(FaunaBone.Head, 16f, 0f, 0f);
                    // The tail lies on the ground behind.
                    p.Set(FaunaBone.Tail0, 118f, 12f, 0f);
                    p.Set(FaunaBone.Tail1, 10f, 15f, 0f);
                    p.Set(FaunaBone.Tail2, 5f, 15f, 0f);
                    bool groom = clip == FaunaClip.Groom;
                    bool sleep = clip == FaunaClip.Sleep;
                    if (groom)
                    {
                        // Hands picking through fur in front, alternating.
                        float a = FMath.Sin(t * 5.3f), b2 = FMath.Sin(t * 4.1f + 1f);
                        p.Set(FaunaBone.FrontUpperL, 30f + 12f * a, 0f, -10f);
                        p.Set(FaunaBone.FrontLowerL, -75f + 15f * a, 0f, 0f);
                        p.Set(FaunaBone.FrontUpperR, 30f + 12f * b2, 0f, 10f);
                        p.Set(FaunaBone.FrontLowerR, -75f + 15f * b2, 0f, 0f);
                        p.Set(FaunaBone.Neck, 70f, 0f, 0f);
                        p.Set(FaunaBone.Head, 25f, 0f, 0f);
                    }
                    else
                    {
                        // Hands together in front of the chest (holding a bite of food); looking about.
                        p.SetPair(FaunaBone.FrontUpperL, 60f, 0f, 8f);
                        p.SetPair(FaunaBone.FrontLowerL, -72f, 0f, 0f);
                        p.SetPair(FaunaBone.FrontFootL, 15f, 0f, 0f);
                        if (!sleep) Idle(m, info, t, seed, p, true);
                        else p.Set(FaunaBone.Head, 45f, 0f, 0f);
                    }
                    break;
                }
            }
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
            // Legs tucked back (dangling forward when landing).
            float legs = clip == FaunaClip.Land ? -50f : clip == FaunaClip.TakeOff ? 20f : 75f;
            p.SetPair(FaunaBone.HindUpperL, legs, 0f, 0f);
            p.SetPair(FaunaBone.HindLowerL, clip == FaunaClip.Land ? 30f : 60f, 0f, 0f);
            p.Set(FaunaBone.Neck, -bindPitch * 0.3f - 10f, 0f, 0f);
            p.Set(FaunaBone.Head, -6f, 0f, 0f);
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
