using System;
using System.IO;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Gait, IK, springs, jump, fidgets and the poser (W2_DESIGN 6.1; P §4–5 suggested tests).</summary>
    public class CharactersMotionTests
    {
        [Test]
        public void GaitCadenceAndStepMatchTheSpec()
        {
            var g = new GaitSolver(0.63f);
            Assert.That(g.StepLength(1.0f), Is.EqualTo(0.42f * 0.63f + 0.18f).Within(1e-4f));
            Assert.That(g.StepLength(1.6f), Is.EqualTo(0.42f * 0.63f + 0.18f * 1.6f).Within(1e-4f));
            Assert.That(g.CadenceFor(1.6f), Is.EqualTo(2.9f).Within(0.15f), "2.9 steps/s at 1.6 m/s");
            Assert.That(g.StepLength(4.5f), Is.EqualTo(0.55f * 0.63f + 0.16f * 4.5f).Within(1e-4f));
            // The Froude walk–run switch for a 0.63 m leg sits inside the blend band.
            float froude = MathF.Sqrt(0.5f * 9.81f * 0.63f);
            Assert.That(froude, Is.InRange(GaitSolver.RunBlendStart, GaitSolver.RunBlendEnd));
        }

        [TestCase(0.8f)]
        [TestCase(1.6f)]
        [TestCase(3.0f)]
        [TestCase(4.5f)]
        [TestCase(6.0f)]
        public void PlantedFeetDoNotSlide(float speed)
        {
            var g = new GaitSolver(0.63f);
            const float dt = 1f / 60f;
            double body = 0;
            for (int i = 0; i < 120; i++)
            {
                g.Step(dt, speed);
                body += speed * dt;
            }
            double plantedAt = double.NaN;
            float worst = 0f;
            for (int i = 0; i < 600; i++)
            {
                g.Step(dt, speed);
                body += speed * dt;
                double world = body + g.Left.Forward;
                if (g.Left.Planted)
                {
                    if (double.IsNaN(plantedAt)) plantedAt = world;
                    worst = Math.Max(worst, (float)Math.Abs(world - plantedAt));
                }
                else
                {
                    plantedAt = double.NaN;
                }
            }
            Assert.That(worst, Is.LessThan(0.02f), "foot slide per stance");
        }

        [Test]
        public void HeelStrikesComeTwicePerCycle()
        {
            var g = new GaitSolver(0.63f);
            int left = 0, right = 0;
            for (int i = 0; i < 600; i++)
            {
                g.Step(1f / 60f, 1.6f);
                if (g.LeftStrike) left++;
                if (g.RightStrike) right++;
            }
            // 10 s at ~2.9 steps/s ≈ 29 steps.
            Assert.That(left + right, Is.InRange(25, 31));
            Assert.That(Math.Abs(left - right), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TwoBoneIkReachesAndKeepsLengths()
        {
            var root = new V3(0f, 1f, 0f);
            var target = new V3(0.1f, 0.6f, 0.2f);
            V3 joint, end;
            float err = TwoBoneIk.Solve(root, 0.3f, 0.27f, target, V3.Forward, out joint, out end);
            Assert.That(err, Is.LessThan(1e-4f));
            Assert.That(V3.Distance(root, joint), Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(V3.Distance(joint, end), Is.EqualTo(0.27f).Within(1e-4f));
            Assert.That(joint.Z, Is.GreaterThan(V3.Lerp(root, target, 0.5f).Z), "bends towards the pole");
            float far = TwoBoneIk.Solve(root, 0.3f, 0.27f, new V3(0f, -1f, 0f), V3.Forward, out joint, out end);
            Assert.That(far, Is.GreaterThan(1f));
            Assert.That(V3.Distance(root, end), Is.LessThanOrEqualTo(0.57f));
        }

        [Test]
        public void SpringsAreStableAtLowFrameRatesAndFollow()
        {
            var presets = new[] { SecondOrder.Topi(), SecondOrder.Braid(), SecondOrder.Backpack(), SecondOrder.Hem(), SecondOrder.LookAt(), SecondOrder.BodyLean(), SecondOrder.Helmet() };
            for (int k = 0; k < presets.Length; k++)
            {
                SecondOrder s = presets[k];
                float max = 0f;
                for (int i = 0; i < 400; i++)
                {
                    float x = i < 200 ? 1f : 0f;
                    float y = s.Update(1f / 20f, x);
                    Assert.That(float.IsFinite(y), Is.True);
                    max = Math.Max(max, Math.Abs(y));
                }
                Assert.That(max, Is.LessThan(2.5f), "preset " + k + " stays bounded at 20 fps");
                Assert.That(Math.Abs(s.Value), Is.LessThan(0.05f), "settles back");
            }
            var look = SecondOrder.LookAt();
            for (int i = 0; i < 120; i++) look.Update(1f / 60f, 1f);
            Assert.That(look.Value, Is.EqualTo(1f).Within(0.05f), "the look-at reaches its target within 2 s");
        }

        [Test]
        public void JumpReachesOneMetreInAThirdOfASecondWithCoyoteAndBuffer()
        {
            var j = new JumpModel();
            const float dt = 1f / 120f;
            j.Update(dt, true, true);
            Assert.That(j.TookOff, Is.True);
            float apex = 0f, apexT = 0f, t = 0f;
            while (j.Airborne && t < 2f)
            {
                j.Update(dt, false, true);
                t += dt;
                if (j.Height > apex)
                {
                    apex = j.Height;
                    apexT = t;
                }
            }
            Assert.That(apex, Is.EqualTo(1.0f).Within(0.03f));
            Assert.That(apexT, Is.EqualTo(0.32f).Within(0.02f));
            Assert.That(j.Landed || !j.Airborne, Is.True);
            Assert.That(JumpModel.LandSquash(4f), Is.EqualTo(0.76f).Within(1e-4f));
            Assert.That(JumpModel.LandSquash(20f), Is.EqualTo(0.75f));

            // Coyote: a press 0.08 s after walking off a ledge still jumps; 0.2 s after does not.
            var c = new JumpModel();
            c.Update(dt, false, true);
            for (float s = 0f; s < 0.08f; s += dt) c.Update(dt, false, false);
            c.Update(dt, true, false);
            Assert.That(c.TookOff, Is.True);
            var late = new JumpModel();
            late.Update(dt, false, true);
            for (float s = 0f; s < 0.2f; s += dt) late.Update(dt, false, false);
            late.Update(dt, true, false);
            Assert.That(late.TookOff, Is.False);
            // Buffer: a press 0.1 s before the ground arrives jumps on arrival.
            var b = new JumpModel();
            b.Update(dt, true, false);
            for (float s = 0f; s < 0.09f; s += dt) b.Update(dt, false, false);
            b.Update(dt, false, true);
            Assert.That(b.TookOff, Is.True);
        }

        [Test]
        public void FidgetsWaitSixSecondsAvoidRepeatsAndStayCalmInSacredAreas()
        {
            var f = new IdleFidgets(5u);
            var street = new FidgetContext { HasTopi = true, Urban = true, Hour = 10f };
            Fidget first = Fidget.None;
            float t = 0f;
            while (first == Fidget.None && t < 20f)
            {
                first = f.Update(0.1f, false, street);
                t += 0.1f;
            }
            Assert.That(t, Is.GreaterThanOrEqualTo(IdleFidgets.IdleDelayS - 0.11f));
            var seen = new System.Collections.Generic.List<Fidget> { first };
            Fidget last = first;
            for (int i = 0; i < 4000 && seen.Count < 12; i++)
            {
                Fidget now = f.Update(0.1f, false, street);
                if (now != Fidget.None && now != last) seen.Add(now);
                last = now;
            }
            for (int i = 1; i < seen.Count; i++)
                for (int k = Math.Max(0, i - 3); k < i; k++)
                    Assert.That(seen[i], Is.Not.EqualTo(seen[k]), "no repeat within three picks");
            Assert.That(f.Update(0.1f, true, street), Is.EqualTo(Fidget.None), "input stops a fidget");

            var sacred = new FidgetContext { Sacred = true, HasTopi = true, Urban = true, Hour = 21f };
            var g = new IdleFidgets(9u);
            for (int i = 0; i < 6000; i++)
            {
                Fidget now = g.Update(0.1f, false, sacred);
                Assert.That(now == Fidget.None || now == Fidget.LookAround || now == Fidget.ShiftWeight || now == Fidget.HandsTogetherRest, Is.True,
                            "calm mode: " + now);
            }
        }

        [Test]
        public void NamastePressesThePalmsTogetherAtTheChestAndBows()
        {
            var p = new CharacterPoser(BodyBuild.B);
            var input = new PoseInput { Dt = 1f / 30f, Emote = Emote.Namaste };
            for (int i = 0; i < 20; i++)
            {
                input.EmoteTime = i / 30f;
                p.Update(input);
            }
            V3 l, r, chest, head;
            Quat q, headRot;
            p.ModelOf(Bone.HandL, out l, out q);
            p.ModelOf(Bone.HandR, out r, out q);
            p.ModelOf(Bone.Chest, out chest, out q);
            p.ModelOf(Bone.Head, out head, out headRot);
            Assert.That(V3.Distance(l, r), Is.LessThan(0.06f), "palms together");
            Assert.That(l.Z, Is.GreaterThan(chest.Z + 0.1f), "in front of the chest");
            Assert.That(l.Y, Is.InRange(chest.Y - 0.1f, p.Skeleton.Metrics.ShoulderY + 0.02f), "at the sternum");
            V3 face = headRot * V3.Forward;
            float bow = MathF.Asin(-face.Y) * Quat.Rad2Deg;
            Assert.That(bow, Is.GreaterThan(NamasteMinBow()), "head bows");
        }

        private static float NamasteMinBow()
        {
            return CharacterPoser.NamasteBowDeg;
        }

        [Test]
        public void EverySeatPoseReachesItsTargetsOnEveryBuild()
        {
            foreach (BodyBuild b in Enum.GetValues(typeof(BodyBuild)))
            {
                var p = new CharacterPoser(b);
                foreach (SeatPose seat in Enum.GetValues(typeof(SeatPose)))
                {
                    if (seat == SeatPose.None) continue;
                    var input = new PoseInput { Dt = 1f / 30f, Seat = seat, Seated01 = 1f, Steer = 0.3f, CrankRad = 1f };
                    for (int i = 0; i < 10; i++) p.Update(input);
                    for (int i = 0; i < p.Local.Length; i++) Assert.That(p.Local[i].IsFinite, Is.True, seat + " bone " + i);
                    V3 foot, knee, hip;
                    Quat q;
                    p.ModelOf(Bone.FootL, out foot, out q);
                    p.ModelOf(Bone.ShinL, out knee, out q);
                    p.ModelOf(Bone.ThighL, out hip, out q);
                    if (seat == SeatPose.BusStanding)
                    {
                        Assert.That(foot.Y, Is.EqualTo(p.Skeleton.Metrics.AnkleY).Within(0.03f));
                        continue;
                    }
                    Assert.That(foot.Y, Is.LessThan(hip.Y - 0.25f), seat + ": feet below the hips");
                    Assert.That(knee.Z, Is.GreaterThan(hip.Z - 0.05f), seat + ": knees forward");
                }
            }
        }

        [Test]
        public void WalkingKeepsTheFeetOnTheGroundAndTheBodyUpright()
        {
            var p = new CharacterPoser(BodyBuild.B);
            float lowest = 9f, highest = -9f;
            for (int i = 0; i < 240; i++)
            {
                p.Update(new PoseInput { Dt = 1f / 60f, SpeedMps = 1.6f });
                V3 l, r;
                Quat q;
                p.ModelOf(Bone.FootL, out l, out q);
                p.ModelOf(Bone.FootR, out r, out q);
                lowest = Math.Min(lowest, Math.Min(l.Y, r.Y));
                highest = Math.Max(highest, Math.Max(l.Y, r.Y));
                Assert.That(p.Squash, Is.InRange(CharacterPoser.SquashMin, CharacterPoser.SquashMax));
            }
            float ankle = p.Skeleton.Metrics.AnkleY;
            Assert.That(lowest, Is.EqualTo(ankle).Within(0.02f), "stance foot on the ground");
            Assert.That(highest, Is.InRange(ankle + 0.04f, ankle + 0.1f), "swing lift about 0.07 m");
        }

        /// <summary>Set GHUMANTE_CHAR_DUMP to a folder to write posed OBJ files (CPU skinning) for a visual check.</summary>
        [Test]
        public void DumpPosesWhenAsked()
        {
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_CHAR_DUMP");
            if (string.IsNullOrEmpty(dir)) Assert.Pass("set GHUMANTE_CHAR_DUMP to write samples");
            Directory.CreateDirectory(dir);
            var r = new CharacterRecipe { Skin = 6, Hair = 1 };
            var cases = new (string, PoseInput, int)[]
            {
                ("pose_walk", new PoseInput { Dt = 1f / 60f, SpeedMps = 1.6f }, 37),
                ("pose_run", new PoseInput { Dt = 1f / 60f, SpeedMps = 4.5f }, 41),
                ("pose_namaste", new PoseInput { Dt = 1f / 30f, Emote = Emote.Namaste }, 20),
                ("pose_wave", new PoseInput { Dt = 1f / 30f, Emote = Emote.Wave }, 12),
                ("pose_scooter", new PoseInput { Dt = 1f / 30f, Seat = SeatPose.Scooter, Seated01 = 1f, Steer = 0.4f }, 5),
                ("pose_motorbike", new PoseInput { Dt = 1f / 30f, Seat = SeatPose.Motorbike, Seated01 = 1f }, 5),
                ("pose_bicycle", new PoseInput { Dt = 1f / 30f, Seat = SeatPose.Bicycle, Seated01 = 1f, CrankRad = 0.8f }, 5),
                ("pose_car", new PoseInput { Dt = 1f / 30f, Seat = SeatPose.CarDriver, Seated01 = 1f, Steer = 0.2f }, 5),
                ("pose_bus_standing", new PoseInput { Dt = 1f / 30f, Seat = SeatPose.BusStanding, Seated01 = 1f }, 5),
                ("pose_air", new PoseInput { Dt = 1f / 30f, Airborne = true }, 5),
            };
            foreach (var (name, input, frames) in cases)
            {
                var poser = new CharacterPoser(r.Build);
                PoseInput pi = input;
                for (int i = 0; i < frames; i++)
                {
                    pi.EmoteTime = i * pi.Dt;
                    poser.Update(pi);
                }
                var m = new MeshData();
                var w = new SkinWeights();
                HumanoidMesher.Build(r, 0, m, w, input.Seat == SeatPose.Scooter || input.Seat == SeatPose.Motorbike || input.Seat == SeatPose.Bicycle ? HeadwearMode.Helmet : HeadwearMode.Outfit);
                Skin(poser, m, w);
                CharactersMeshPreviews.Write(Path.Combine(dir, name + ".obj"), m);
            }
        }

        /// <summary>Linear blend skinning on the CPU (what the SkinnedMeshRenderer does).</summary>
        internal static void Skin(CharacterPoser poser, MeshData m, SkinWeights w)
        {
            var pos = new V3[HumanoidSkeleton.BoneCount];
            var rot = new Quat[HumanoidSkeleton.BoneCount];
            poser.Skeleton.Solve(poser.Local, poser.HipsOffset, poser.Squash, pos, rot);
            V3[] bind = poser.Skeleton.BindPosition;
            for (int v = 0; v < m.VertexCount; v++)
            {
                var p = new V3(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2]);
                var n = new V3(m.Normals[v * 3], m.Normals[v * 3 + 1], m.Normals[v * 3 + 2]);
                int b0 = w.Bone0[v], b1 = w.Bone1[v];
                float w0 = w.Weight0[v];
                V3 p0 = pos[b0] + rot[b0] * (p - bind[b0]), p1 = pos[b1] + rot[b1] * (p - bind[b1]);
                V3 n0 = rot[b0] * n, n1 = rot[b1] * n;
                V3 pp = p0 * w0 + p1 * (1f - w0), nn = (n0 * w0 + n1 * (1f - w0)).Normalized;
                m.Positions[v * 3] = pp.X;
                m.Positions[v * 3 + 1] = pp.Y;
                m.Positions[v * 3 + 2] = pp.Z;
                m.Normals[v * 3] = nn.X;
                m.Normals[v * 3 + 1] = nn.Y;
                m.Normals[v * 3 + 2] = nn.Z;
            }
        }
    }
}
