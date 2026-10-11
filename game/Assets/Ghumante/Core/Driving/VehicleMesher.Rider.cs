using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Driving
{
    public static partial class VehicleMesher
    {
        // -----------------------------------------------------------------------------------------------------------
        // Riders of the traffic two-wheelers and the rickshaw puller (docs/research/w2/ref_vehicles.md §8, photos
        // scooter_ktm ref_00/ref_01): people from the same generator as the pedestrians (HumanoidMesher), dressed for
        // the road and posed on the vehicle they ride, baked into its body mesh.
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>Rider looks per livery and model type (<see cref="RiderLookFor"/>): a cache that keys its shared
        /// bodies by look as well gets this many different people on every bike of one colour.</summary>
        public const int RiderLooks = 4;

        /// <summary>The rider look of an agent (0..<see cref="RiderLooks"/> − 1), the same at every level.</summary>
        public static byte RiderLookFor(uint agentSeed)
        {
            return (byte)(VehicleCatalogEntry.Mix(agentSeed ^ 0x4C4F4F4Bu) % RiderLooks);
        }

        /// <summary>The humanoid level a rider is built at on a vehicle level: the nearest traffic gets the near-crowd
        /// body (sculpted face, hands with a thumb), near traffic the mid-distance body, far traffic the far body.</summary>
        public static int RiderLevel(VehicleLod lod)
        {
            return lod == VehicleLod.Lod0 ? 1 : lod == VehicleLod.Lod1 ? 2 : HumanoidMesher.FarLod;
        }

        /// <summary>True for the motor two-wheelers, whose riders wear a helmet (the law in Nepal, W2_DESIGN 5.2).</summary>
        public static bool RiderWearsHelmet(BodyShape shape)
        {
            return shape == BodyShape.Scooter || shape == BodyShape.Motorbike || shape == BodyShape.Cruiser;
        }

        /// <summary>
        /// The person who rides a vehicle of <paramref name="shape"/> in look <paramref name="look"/> (a hash of the
        /// livery, model type and rider look): Kathmandu commuters in jackets, hoodies and jeans, with women on about
        /// 45% of the scooters (often in kurta suruwal) and a few on motorbikes, a dust mask on a third of the motor
        /// riders; cyclists in caps or bare-headed; rickshaw pullers in shirts or vests with rolled trousers, a cap, a
        /// dhaka topi or a sun hat. Nothing that needs a free hand (umbrella, prayer wheel) and no load on the back but
        /// a daypack. Deterministic.
        /// </summary>
        public static CharacterRecipe RiderRecipe(BodyShape shape, uint look)
        {
            uint h = VehicleCatalogEntry.Mix(look ^ 0x52494445u);
            bool pedal = shape == BodyShape.Bicycle || shape == BodyShape.Rickshaw;
            int womenPct = shape == BodyShape.Scooter ? 45 : shape == BodyShape.Motorbike ? 8 : shape == BodyShape.Bicycle ? 12 : 0;
            bool woman = (int)((h >> 3) % 100u) < womenPct;
            PedArchetype a = shape == BodyShape.Rickshaw ? PedArchetype.Farmer : PedArchetype.UrbanCasual;
            CharacterRecipe r = null;
            for (int t = 0; t < 8; t++)
            {
                r = CharacterRecipe.ForPedestrian(a, (int)((h >> 9) % 16u), VehicleCatalogEntry.Mix(h + (uint)t * 0x9E3779B9u), StreetStyle.Urban, 0);
                if ((r.Figure != 0) == woman && r.Age != AgeGroup.Child) break;
            }
            if (r.Age == AgeGroup.Child) r.Age = AgeGroup.Adult;
            // Pedalling needs the long-legged build, a grown-up's full height (the cartoon legs of the shorter builds and
            // ages do not reach the pedals of a real-size cycle).
            if (pedal)
            {
                r.Build = BodyBuild.D;
                r.Age = AgeGroup.Adult;
                r.HeightPct = (sbyte)Math.Max(4, (int)r.HeightPct);
            }
            // A sari is rare on a running scooter; riders wear a kurta suruwal.
            if (r[OutfitSlotKind.Torso].Item == OutfitItem.Sari)
            {
                OutfitSlot top = r[OutfitSlotKind.Torso];
                r[OutfitSlotKind.Torso] = new OutfitSlot(OutfitItem.Kurta, top.Colour, top.Pattern);
                r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Suruwal, (byte)((h >> 13) % 6u));
            }
            if (!pedal && (h >> 5) % 100u < 65u)
            {
                // Motor riders mostly wear a jacket against the wind and the dust (black, navy, olive, maroon, red, grey).
                OutfitItem top = r[OutfitSlotKind.Torso].Item;
                if (top == OutfitItem.TShirt || top == OutfitItem.Shirt || top == OutfitItem.Hoodie || top == OutfitItem.Fleece || top == OutfitItem.Kurta)
                    r[OutfitSlotKind.Torso] = new OutfitSlot(OutfitItem.Jacket, (byte)((h >> 14) % 6u));
                if (r[OutfitSlotKind.Legs].Item == OutfitItem.Suruwal && (h & 1u) == 0u) r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Jeans, (byte)((h >> 17) % 4u));
            }
            if (r[OutfitSlotKind.Back].Item != OutfitItem.Daypack) r[OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.None);
            r.Accents &= ~(CharacterAccents.Umbrella | CharacterAccents.PrayerWheel | CharacterAccents.Mala);
            if (!pedal && (h >> 20) % 3u == 0u) r.Accents |= CharacterAccents.Mask;
            if (pedal && r[OutfitSlotKind.Head].Item == OutfitItem.None)
            {
                uint k = (h >> 24) % 10u;
                OutfitItem hat = shape == BodyShape.Rickshaw ? (k < 4 ? OutfitItem.Cap : k < 7 ? OutfitItem.DhakaTopi : OutfitItem.None)
                    : k < 3 ? OutfitItem.Cap : OutfitItem.None;
                if (hat != OutfitItem.None) r[OutfitSlotKind.Head] = new OutfitSlot(hat, (byte)((h >> 16) % 8u), (byte)((h >> 12) % 6u));
            }
            r.HelmetColour = RiderHelmet(h >> 25);
            r.Name = "";
            return r.Validate();
        }

        // Helmet colour indices (CharacterRecipe.HelmetColour): red, blue, yellow, white, black, green, orange, pink.
        private static readonly byte[] HelmetWeights = { 15, 15, 4, 20, 34, 4, 4, 4 };

        /// <summary>Helmets on Kathmandu roads are mostly black, white, red and blue (scooter_ktm ref_00/ref_01).</summary>
        private static byte RiderHelmet(uint h)
        {
            int u = (int)(h % 100u);
            for (int i = 0; i < HelmetWeights.Length; i++)
            {
                u -= HelmetWeights[i];
                if (u < 0) return (byte)i;
            }
            return 4;
        }

        /// <summary>Scratch of the rider build (per thread).</summary>
        private sealed class RiderScratch
        {
            public readonly MeshData Mesh = new MeshData(4096, 12288);
            public readonly SkinWeights Weights = new SkinWeights(4096);
            public readonly Quat[] Local = new Quat[HumanoidSkeleton.BoneCount];
            public readonly V3[] Pos = new V3[HumanoidSkeleton.BoneCount];
            public readonly Quat[] Rot = new Quat[HumanoidSkeleton.BoneCount];
        }

        [ThreadStatic] private static RiderScratch s_riderScratch;

        /// <summary>
        /// Builds the rider of the two-wheeler (or the rickshaw's puller) just built, at the humanoid level of this
        /// vehicle level (<see cref="RiderLevel"/>), posed on it (<see cref="PoseRider"/>) and appended to the body.
        /// The look follows the livery, the model type and <see cref="Ctx.RiderLook"/>, so one agent keeps its rider
        /// across levels.
        /// </summary>
        private static void Rider(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            RiderRef k = s_rider;
            if (k.HipY <= 0) return;
            CharacterRecipe r = RiderRecipe(e.Shape, RiderHash(p.Index, c.Model, e.Shape, c.RiderLook));
            RiderScratch sc = s_riderScratch ?? (s_riderScratch = new RiderScratch());
            MeshData body = sc.Mesh;
            body.Clear();
            sc.Weights.Clear();
            HeadwearMode hw = RiderWearsHelmet(e.Shape) ? HeadwearMode.Helmet : HeadwearMode.Outfit;
            HumanoidMesher.Build(r, RiderLevel(c.Lod), body, sc.Weights, CharacterMeshOptions.For(hw));
            var sk = new HumanoidSkeleton(r);
            PoseRider(sk, k, e.Shape, c.Model, sc.Local, out V3 hips);
            sk.Solve(sc.Local, hips, 1f, sc.Pos, sc.Rot);
            BakeSkinned(body, sc.Weights, sk, sc.Pos, sc.Rot, c.M);
            _ = d;
        }

        /// <summary>The look hash of a rider: the livery, the model type, the family and the rider look.</summary>
        internal static uint RiderHash(byte livery, byte model, BodyShape shape, byte riderLook)
        {
            return VehicleCatalogEntry.Mix((uint)livery * 7919u + (uint)model * 104729u + (uint)shape * 31u + (uint)(riderLook % RiderLooks) * 2654435761u);
        }

        /// <summary>The rider of a catalogue entry, livery, model type and rider look (the person <see cref="Build(int, byte,
        /// byte, VehicleLod, MeshData, uint, bool, byte)"/> seats on it).</summary>
        internal static CharacterRecipe RiderOf(int variant, byte livery, byte model, byte riderLook)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            byte lv = (byte)(livery % Math.Max(1, (int)e.LiveryCount));
            return RiderRecipe(e.Shape, RiderHash(lv, (byte)(model % ModelCount(variant)), e.Shape, riderLook));
        }

        /// <summary>
        /// How well the rider of a two-wheeler (or the rickshaw's puller) sits on it (tests): the largest distance from
        /// a palm to its grip and from the ball of a foot to its peg, pedal or floorboard spot, and the height of the
        /// hip joints over the seat point.
        /// </summary>
        internal static void RiderFit(int variant, byte model, byte riderLook, out float hand, out float foot, out float hipOverSeat)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            Build(variant, 0, model, VehicleLod.Lod2, new MeshData(), 1u, false, false);
            RiderRef k = s_rider;
            CharacterRecipe r = RiderOf(variant, 0, model, riderLook);
            var sk = new HumanoidSkeleton(r);
            var local = new Quat[HumanoidSkeleton.BoneCount];
            var pos = new V3[HumanoidSkeleton.BoneCount];
            var rot = new Quat[HumanoidSkeleton.BoneCount];
            PoseRider(sk, k, e.Shape, model % ModelCount(variant), local, out V3 hips);
            sk.Solve(local, hips, 1f, pos, rot);
            hand = foot = 0f;
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Bone h = side == 0 ? Bone.HandL : Bone.HandR, f = side == 0 ? Bone.FootL : Bone.FootR;
                // The palm: down the hand from the wrist.
                V3 palm = pos[(int)h] + rot[(int)h] * new V3(0f, -0.06f * sk.Metrics.HandScale, 0f);
                hand = Math.Max(hand, V3.Distance(palm, new V3(s * (float)k.GripX, (float)k.GripY, (float)k.GripZ)));
                V3 ball = pos[(int)f] + rot[(int)f] * RiderBall;
                foot = Math.Max(foot, V3.Distance(ball, RiderPeg(k, side)));
            }
            hipOverSeat = pos[(int)Bone.ThighL].Y - (float)k.HipY;
        }

        /// <summary>The ball of the foot from the ankle (foot frame).</summary>
        private static readonly V3 RiderBall = new V3(0f, -0.07f, 0.10f);

        /// <summary>Where the ball of a rider's foot goes: the peg or floorboard spot, or the pedal with the crank a
        /// quarter turn from the top (the right pedal forward).</summary>
        private static V3 RiderPeg(in RiderRef k, int side)
        {
            float s = side == 0 ? -1f : 1f;
            if (!k.Pedals) return new V3(s * (float)k.PegX, (float)k.PegY, (float)k.PegZ);
            double a = side == 1 ? 1.2 : 1.2 + Math.PI;
            return new V3(s * 0.13f, (float)(k.PegY + Math.Cos(a) * 0.165), (float)(k.PegZ + Math.Sin(a) * 0.165));
        }

        /// <summary>
        /// Seats a rider on the vehicle (vehicle frame = the rider's model frame, origin under the rear axle): the hip
        /// joints just above the seat, the back leaning toward the bars (a sporty commuter 16°, a family scooter or the
        /// cruiser upright, a mountain bike 22°), the head lifted to look down the road, the hands on the grips with the
        /// palms down and the fingers wrapped round them, and the feet on the pegs, flat on the scooter's floorboard or
        /// toes down on the pedals (the crank a quarter turn from the top). Two-bone IK on the rig's bind lengths.
        /// </summary>
        internal static void PoseRider(HumanoidSkeleton sk, in RiderRef k, BodyShape shape, int model, Quat[] local, out V3 hips)
        {
            for (int i = 0; i < local.Length; i++) local[i] = Quat.Identity;
            bool pedal = k.Pedals;
            float lean = shape == BodyShape.Motorbike ? (model == 1 ? 9f : 16f) : shape == BodyShape.Cruiser ? 4f : shape == BodyShape.Scooter ? 3f
                : shape == BodyShape.Bicycle ? (model == 1 ? 10f : 22f) : 15f;
            V3 hipBind = sk[Bone.Hips], thighBind = sk[Bone.ThighL];
            // The hip joints a hand's breadth over the seat top (pedalled saddles a little less, so the feet reach).
            float sit = pedal ? 0.025f : 0.045f;
            // The hips bone sits its bind height over the root plus this offset: put the hip joints at the seat.
            hips = new V3(0f, (float)k.HipY + sit - thighBind.Y, (float)k.HipZ - hipBind.Z);
            local[(int)Bone.Hips] = Quat.Euler(0.3f * lean, 0f, 0f);
            local[(int)Bone.Spine] = Quat.Euler(0.4f * lean, 0f, 0f);
            local[(int)Bone.Chest] = Quat.Euler(0.3f * lean, 0f, 0f);
            local[(int)Bone.Head] = Quat.Euler(-0.85f * lean, 0f, 0f);
            V3[] pos = s_poseP ?? (s_poseP = new V3[HumanoidSkeleton.BoneCount]);
            Quat[] rot = s_poseR ?? (s_poseR = new Quat[HumanoidSkeleton.BoneCount]);
            sk.Solve(local, hips, 1f, pos, rot);
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                // Feet: the ball of the foot on the peg or pedal; flat feet on pegs and floorboards, toes down on pedals.
                float toeDown = pedal ? 35f : shape == BodyShape.Scooter ? 0f : 8f;
                Quat footWorld = Quat.Euler(toeDown, s * 6f, 0f);
                V3 ankle = RiderPeg(k, side) - footWorld * RiderBall;
                LegIk(sk, local, pos, rot, side, ankle, new V3(s * 0.25f, 0.35f, 1f), footWorld);
                // Hands: the palm round the grip (the wrist a palm's length behind it), the elbow out and down.
                var grip = new V3(s * (float)k.GripX, (float)k.GripY, (float)k.GripZ);
                Bone upper = side == 0 ? Bone.UpperArmL : Bone.UpperArmR;
                V3 shoulder = pos[(int)upper];
                V3 reach = (grip - shoulder).Normalized;
                V3 wrist = grip - reach * (0.06f * sk.Metrics.HandScale) + new V3(0f, 0.015f, 0f);
                ArmIk(sk, local, pos, rot, side, wrist, new V3(s * 1f, -0.7f, -0.35f));
                // Palm down (a quarter turn about the forearm) and the fingers and thumb wrapped round the grip.
                int o = side * 4;
                local[(int)Bone.HandL + o] = Quat.Euler(0f, s * -80f, 0f);
                int f = side * 4;
                local[(int)Bone.FingerL1 + f] = Quat.Euler(0f, 0f, -s * 55f);
                local[(int)Bone.FingerL2 + f] = Quat.Euler(0f, 0f, -s * 50f);
                local[(int)Bone.ThumbL1 + f] = Quat.Euler(0f, 0f, -s * 25f);
                sk.Solve(local, hips, 1f, pos, rot);
            }
        }

        [ThreadStatic] private static V3[] s_poseP;
        [ThreadStatic] private static Quat[] s_poseR;

        private static void LegIk(HumanoidSkeleton sk, Quat[] local, V3[] pos, Quat[] rot, int side, V3 ankle, V3 pole, Quat footWorld)
        {
            int o = side * 4;
            Bone thigh = (Bone)(23 + o), shin = (Bone)(24 + o), foot = (Bone)(25 + o);
            float upper = V3.Distance(sk[thigh], sk[shin]), lower = V3.Distance(sk[shin], sk[foot]);
            V3 hip = pos[(int)thigh];
            TwoBoneIk.Solve(hip, upper, lower, ankle, pole, out V3 knee, out V3 end);
            Quat hipsWorld = rot[(int)Bone.Hips];
            Quat thighLocal = TwoBoneIk.Aim(hipsWorld, sk[shin] - sk[thigh], knee - hip);
            local[(int)thigh] = thighLocal;
            Quat thighWorld = hipsWorld * thighLocal;
            Quat shinLocal = TwoBoneIk.Aim(thighWorld, sk[foot] - sk[shin], end - knee);
            local[(int)shin] = shinLocal;
            Quat shinWorld = thighWorld * shinLocal;
            local[(int)foot] = (shinWorld.Inverse * footWorld).Normalized;
        }

        private static void ArmIk(HumanoidSkeleton sk, Quat[] local, V3[] pos, Quat[] rot, int side, V3 wrist, V3 pole)
        {
            int o = side * 4;
            Bone upperArm = (Bone)(8 + o), forearm = (Bone)(9 + o), hand = (Bone)(10 + o), shoulder = (Bone)(7 + o);
            float upper = V3.Distance(sk[upperArm], sk[forearm]), lower = V3.Distance(sk[forearm], sk[hand]);
            V3 root = pos[(int)upperArm];
            TwoBoneIk.Solve(root, upper, lower, wrist, pole, out V3 elbow, out V3 end);
            Quat parentWorld = rot[(int)shoulder];
            Quat upperLocal = TwoBoneIk.Aim(parentWorld, sk[forearm] - sk[upperArm], elbow - root);
            local[(int)upperArm] = upperLocal;
            Quat upperWorld = parentWorld * upperLocal;
            local[(int)forearm] = TwoBoneIk.Aim(upperWorld, sk[hand] - sk[forearm], end - elbow);
        }

        /// <summary>Linear-blend skins <paramref name="src"/> with the solved bone transforms and appends it to
        /// <paramref name="dst"/> (colours, material channel and AO kept).</summary>
        private static void BakeSkinned(MeshData src, SkinWeights w, HumanoidSkeleton sk, V3[] pos, Quat[] rot, MeshData dst)
        {
            int baseV = dst.VertexCount;
            dst.HasUv0 = true;
            bool uv = src.HasUv0;
            float[] p = src.Positions, n = src.Normals;
            for (int v = 0; v < src.VertexCount; v++)
            {
                var pv = new V3(p[v * 3], p[v * 3 + 1], p[v * 3 + 2]);
                var nv = new V3(n[v * 3], n[v * 3 + 1], n[v * 3 + 2]);
                int b0 = w.Bone0[v], b1 = w.Bone1[v];
                float w0 = w.Weight0[v], w1 = 1f - w0;
                V3 q = pos[b0] + rot[b0] * (pv - sk.BindPosition[b0]);
                V3 m = rot[b0] * nv;
                if (w1 > 1e-4f)
                {
                    q = q * w0 + (pos[b1] + rot[b1] * (pv - sk.BindPosition[b1])) * w1;
                    m = (m * w0 + (rot[b1] * nv) * w1).Normalized;
                }
                uint rgba = (uint)(src.Colors[v * 4] << 24 | src.Colors[v * 4 + 1] << 16 | src.Colors[v * 4 + 2] << 8 | src.Colors[v * 4 + 3]);
                dst.AddVertex(q.X, q.Y, q.Z, m.X, m.Y, m.Z, rgba, uv ? src.Uv0[v * 2] : 0f, uv ? src.Uv0[v * 2 + 1] : 1f);
            }
            for (int t = 0; t < src.IndexCount; t += 3)
                dst.AddTriangle(src.Indices[t] + baseV, src.Indices[t + 1] + baseV, src.Indices[t + 2] + baseV);
        }
    }
}
