using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Instancing
{
    /// <summary>Clothing palettes of the crowd by archetype (W2_DESIGN 5.4, street_life §3.3), as instanced tints.</summary>
    public static class PersonPalette
    {
        private static readonly uint[] Casual = { 0x2F6FD6, 0xD93A2B, 0xF2F0E8, 0x3FA35C, 0xF2C230, 0x6B4E9A, 0x2E3440, 0xE07A57 };
        private static readonly uint[] Kurta = { 0xE23B2A, 0xF49AC1, 0xF6A21B, 0x8E24AA, 0x2E9E4F, 0xFFD95A, 0xC2185B };
        private static readonly uint[] Daura = { 0xF1E3BE, 0xE9E4D4, 0xD8C9A8 };
        private static readonly uint[] Haku = { 0x1E1E1E, 0x7A1F1F };
        private static readonly uint[] School = { 0x7FC8F8, 0xF7F6F0, 0x1F3A93 };
        private static readonly uint[] Porter = { 0x8B6B4A, 0x5A3E2B, 0x6E6E70 };
        private static readonly uint[] Tourist = { 0xC9B27A, 0x3CC9C0, 0xF59A3B, 0xE8483A, 0x9FE2B8 };
        private static readonly uint[] Monk = { 0x7A1F1F, 0x8E2525 };
        private static readonly uint[] Sadhu = { 0xE07B1A, 0xF0A030 };
        private static readonly uint[] Farmer = { 0x7A6A4A, 0x9C9A94, 0xB86A4A };
        private static readonly uint[] Police = { 0x1F3A93 };
        private static readonly uint[] Trousers = { 0x2E3440, 0x1F3A5F, 0x4A3B30, 0x5C5C5C, 0x26324A };

        public static uint Clothes(PedArchetype a, int tint)
        {
            uint[] p;
            switch (a)
            {
                case PedArchetype.KurtaSari: p = Kurta; break;
                case PedArchetype.DauraSuruwal: p = Daura; break;
                case PedArchetype.Hakupatasi: p = Haku; break;
                case PedArchetype.SchoolKid: p = School; break;
                case PedArchetype.Porter: p = Porter; break;
                case PedArchetype.Vendor: p = Casual; break;
                case PedArchetype.Tourist: p = Tourist; break;
                case PedArchetype.Monk: p = Monk; break;
                case PedArchetype.Sadhu: p = Sadhu; break;
                case PedArchetype.Farmer: p = Farmer; break;
                case PedArchetype.TrafficPolice: p = Police; break;
                default: p = Casual; break;
            }
            return p[(tint & 0x7FFFFFFF) % p.Length];
        }

        /// <summary>Lower-body colour: a sari, robe or daura continues the top; others wear trousers.</summary>
        public static uint Lower(PedArchetype a, int tint)
        {
            switch (a)
            {
                case PedArchetype.KurtaSari:
                case PedArchetype.DauraSuruwal:
                case PedArchetype.Monk:
                case PedArchetype.Sadhu:
                case PedArchetype.Hakupatasi:
                    return Clothes(a, tint);
                case PedArchetype.TrafficPolice:
                    return 0x1F3A93;
                default:
                    return Trousers[((tint >> 3) & 0x7FFFFFFF) % Trousers.Length];
            }
        }

        /// <summary>Headgear of the person meshes: 0 none, 1 dhaka topi, 2 police cap, 3 monk.</summary>
        public static int Headgear(PedArchetype a, int tint)
        {
            switch (a)
            {
                case PedArchetype.DauraSuruwal: return 1;
                case PedArchetype.Farmer: return (tint & 3) == 0 ? 1 : 0;
                case PedArchetype.TrafficPolice: return 2;
                case PedArchetype.Monk: return 3;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// Instanced rigid-part people for the crowd and the traffic officers (W2_DESIGN 5.4 representation: near people as
    /// animated parts at LOD0 / LOD1 with their carry prop (doko, sack, gas cylinder, baby, umbrella), far people as a
    /// single block-out with a walking bob, standing in for the VAT body until Track D's HumanoidMesher bakes one; open
    /// issue against the 10.3 contract, see World/README.md). Every part is one shared mesh drawn with
    /// <see cref="InstanceBatch"/>; clothes and trousers are per-instance tints. Call <see cref="Begin"/>, then
    /// <see cref="Add"/> per person, then <see cref="End"/>, once per frame. Main thread only.
    /// </summary>
    public sealed class PeopleRenderer : IDisposable
    {
        private const int Headgears = 4;
        private readonly InstanceBatch[,] _body = new InstanceBatch[2, Headgears];
        private readonly InstanceBatch[] _legL = new InstanceBatch[2], _legR = new InstanceBatch[2], _armL = new InstanceBatch[2], _armR = new InstanceBatch[2];
        private readonly InstanceBatch[] _far = new InstanceBatch[Headgears];
        private readonly InstanceBatch[,] _carry = new InstanceBatch[2, KitMeshes.CarryProps];
        private readonly InstanceBatch[] _all;

        public PeopleRenderer(WorldMaterialSet materials)
        {
            Material mat = materials.instancedTint;
            var m = new MeshData(512, 1536);
            for (int lod = 0; lod < 2; lod++)
            {
                for (int h = 0; h < Headgears; h++) _body[lod, h] = Make(KitMeshes.PersonPart.Body, lod, h, mat, m, lod == 0);
                _legL[lod] = Make(KitMeshes.PersonPart.LegLeft, lod, 0, mat, m, lod == 0);
                _legR[lod] = Make(KitMeshes.PersonPart.LegRight, lod, 0, mat, m, lod == 0);
                _armL[lod] = Make(KitMeshes.PersonPart.ArmLeft, lod, 0, mat, m, lod == 0);
                _armR[lod] = Make(KitMeshes.PersonPart.ArmRight, lod, 0, mat, m, lod == 0);
            }
            for (int h = 0; h < Headgears; h++) _far[h] = Make(KitMeshes.PersonPart.Body, 2, h, mat, m, false);
            for (int lod = 0; lod < 2; lod++)
                for (int c = 1; c < KitMeshes.CarryProps; c++)
                {
                    m.Clear();
                    int tris = KitMeshes.Carry(c, lod, m);
                    _carry[lod, c] = new InstanceBatch(MeshUpload.CreateWhole(m, "person_carry_" + c + "_" + lod), mat, tris, true)
                    {
                        Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                }
            _all = new InstanceBatch[2 * Headgears + 8 + Headgears + 2 * (KitMeshes.CarryProps - 1)];
            int k = 0;
            foreach (InstanceBatch b in _body) _all[k++] = b;
            for (int lod = 0; lod < 2; lod++)
            {
                _all[k++] = _legL[lod];
                _all[k++] = _legR[lod];
                _all[k++] = _armL[lod];
                _all[k++] = _armR[lod];
            }
            for (int h = 0; h < Headgears; h++) _all[k++] = _far[h];
            for (int lod = 0; lod < 2; lod++)
                for (int c = 1; c < KitMeshes.CarryProps; c++) _all[k++] = _carry[lod, c];
        }

        private static InstanceBatch Make(KitMeshes.PersonPart part, int lod, int headgear, Material mat, MeshData m, bool shadows)
        {
            m.Clear();
            int tris = KitMeshes.Person(part, lod, headgear, m);
            return new InstanceBatch(MeshUpload.CreateWhole(m, "person_" + part + "_" + lod + "_" + headgear), mat, tris, true)
            {
                Shadows = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
        }

        /// <summary>People and triangles submitted since <see cref="Begin"/>.</summary>
        public int People { get; private set; }

        public int Tris { get; private set; }

        public int Draws { get; private set; }

        public void Begin(Vector3 cameraScene)
        {
            var bounds = new Bounds(cameraScene, new Vector3(1000f, 600f, 1000f));
            foreach (InstanceBatch b in _all)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            People = 0;
        }

        /// <summary>One person at scene position <paramref name="p"/> facing <paramref name="headingDeg"/> (clockwise
        /// from north); <paramref name="lod"/> 0 / 1 animated parts, 2 the far block-out. <paramref name="carry"/> is the
        /// pose's carry prop (<see cref="KitMeshes.Carry"/>; 0 none), drawn at LOD0 and LOD1 on the torso.</summary>
        public void Add(Vector3 p, float headingDeg, in PersonPose pose, uint clothes, uint lower, int headgear, int lod, int carry = 0)
        {
            headgear = Mathf.Clamp(headgear, 0, Headgears - 1);
            Vector4 top = Tint.Hex(clothes), bottom = Tint.Hex(lower);
            Matrix4x4 root = Matrix4x4.TRS(p + new Vector3(0f, pose.BobM - pose.DropM, 0f), Quaternion.Euler(0f, headingDeg, 0f), Vector3.one);
            People++;
            if (lod >= 2)
            {
                _far[headgear].Add(root, top);
                return;
            }
            // Bow about the hip.
            Matrix4x4 body = root * Matrix4x4.Translate(new Vector3(0f, KitMeshes.HipY, 0f)) * Matrix4x4.Rotate(Quaternion.Euler(pose.LeanDeg, 0f, 0f)) *
                             Matrix4x4.Translate(new Vector3(0f, -KitMeshes.HipY, 0f));
            _body[lod, headgear].Add(body, top);
            if (carry > 0 && carry < KitMeshes.CarryProps) _carry[lod, carry].Add(body, top);
            _legL[lod].Add(Limb(root, -KitMeshes.HipX, KitMeshes.HipY, pose.LegLeft, 0f), bottom);
            _legR[lod].Add(Limb(root, KitMeshes.HipX, KitMeshes.HipY, pose.LegRight, 0f), bottom);
            _armL[lod].Add(Limb(body, -KitMeshes.ShoulderX, KitMeshes.ShoulderY, pose.ArmLeft, -pose.ArmOutLeft), top);
            _armR[lod].Add(Limb(body, KitMeshes.ShoulderX, KitMeshes.ShoulderY, pose.ArmRight, pose.ArmOutRight), top);
        }

        /// <summary>A limb pivoting at (x, y): forward swing about X, sideways raise about Z.</summary>
        private static Matrix4x4 Limb(in Matrix4x4 parent, float x, float y, float forwardDeg, float outDeg)
        {
            return parent * Matrix4x4.Translate(new Vector3(x, y, 0f)) * Matrix4x4.Rotate(Quaternion.Euler(-forwardDeg, 0f, outDeg)) *
                   Matrix4x4.Translate(new Vector3(-x, 0f, 0f));
        }

        public void End()
        {
            int tris = 0, draws = 0;
            foreach (InstanceBatch b in _all)
            {
                b.Flush();
                tris += b.Tris;
                draws += b.Draws;
            }
            Tris = tris;
            Draws = draws;
        }

        public void Dispose()
        {
            foreach (InstanceBatch b in _all) b.DestroyMesh();
        }
    }
}
