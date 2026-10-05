using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using UnityEngine;
using UnityEngine.Rendering;
using SkinWeights = Ghumante.Core.Characters.SkinWeights;

namespace Ghumante.Characters.Avatar
{
    /// <summary>
    /// The player's cartoon body (W2_DESIGN 6.1): the <see cref="HumanoidMesher"/> mesh of a <see cref="CharacterRecipe"/>
    /// skinned to the 37-bone <c>hum</c> rig in one <see cref="SkinnedMeshRenderer"/> (one draw call), drawn with the
    /// <c>Ghumante/ToonLit</c> material (vertex colours, no alpha). Two meshes are built once per wardrobe: one with the
    /// recipe's headwear and one with the helmet, swapped on every two-wheeler mount (<see cref="Helmet"/>). Each frame
    /// <see cref="Apply"/> copies the engine-free <see cref="CharacterPoser"/>'s local rotations, hips offset and squash
    /// onto the bone transforms; nothing allocates per frame. A soft cream ground ring (opaque, Ø 0.9 m) shows under the
    /// player while standing, so the player reads apart from the crowd. Model axes: +Z forward, +Y up, origin on the
    /// ground between the feet.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        /// <summary>The ground ring's colour (#FFF3D6) and size.</summary>
        public const float RingDiameterM = 0.9f;

        private Transform[] _bones;
        private Transform _squash;
        private SkinnedMeshRenderer _renderer;
        private Mesh _outfitMesh, _helmetMesh;
        private GameObject _ring;
        private Mesh _ringMesh;
        private HumanoidSkeleton _skeleton;
        private CharacterRecipe _recipe;
        private bool _helmet;

        public static PlayerAvatar Create(Transform parent, Material material, CharacterRecipe recipe)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            var go = new GameObject("Player Avatar");
            go.transform.SetParent(parent, false);
            PlayerAvatar a = go.AddComponent<PlayerAvatar>();
            a.Build(material, recipe ?? CharacterRecipe.NewPlayer(1u));
            return a;
        }

        public CharacterRecipe Recipe
        {
            get { return _recipe; }
        }

        public HumanoidSkeleton Skeleton
        {
            get { return _skeleton; }
        }

        /// <summary>The head bone (the audio listener's ear point).</summary>
        public Transform Head
        {
            get { return _bones != null ? _bones[(int)Bone.Head] : transform; }
        }

        /// <summary>Wears the helmet (two-wheelers) instead of the recipe's headwear.</summary>
        public bool Helmet
        {
            get { return _helmet; }
            set
            {
                if (_helmet == value || _renderer == null) return;
                _helmet = value;
                _renderer.sharedMesh = value ? _helmetMesh : _outfitMesh;
            }
        }

        /// <summary>Shows the cream ground ring (standing still in a crowd).</summary>
        public bool RingVisible
        {
            set
            {
                if (_ring != null && _ring.activeSelf != value) _ring.SetActive(value);
            }
        }

        /// <summary>Rebuilds the meshes for a new wardrobe (the bones stay when the build is the same).</summary>
        public void SetRecipe(CharacterRecipe recipe, Material material)
        {
            if (recipe == null) return;
            if (_recipe != null && _recipe.Equals(recipe)) return;
            bool rebuildBones = _recipe == null || _recipe.Build != recipe.Build;
            _recipe = recipe.Clone().Validate();
            if (rebuildBones)
            {
                for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
                _renderer = null;
                _bones = null;
                Build(material, _recipe);
                return;
            }
            DestroyMeshes();
            BuildMeshes();
        }

        /// <summary>
        /// Poses the avatar: the root goes to <paramref name="scenePosition"/> facing <paramref name="rotation"/>; the
        /// bones take the poser's local rotations (relative to the bind pose), the hips its offset, the squash bone its
        /// volume-preserving scale.
        /// </summary>
        public void Apply(CharacterPoser poser, Vector3 scenePosition, Quaternion rotation)
        {
            if (poser == null || _bones == null) return;
            transform.SetPositionAndRotation(scenePosition, rotation);
            Quat[] local = poser.Local;
            V3[] bind = _skeleton.BindLocal;
            V3 hips = poser.HipsOffset;
            for (int i = 1; i < _bones.Length; i++)
            {
                Transform b = _bones[i];
                Quat q = local[i];
                b.localRotation = new Quaternion(q.X, q.Y, q.Z, q.W);
                if (i == (int)Bone.Hips)
                {
                    V3 p = bind[i] + hips;
                    b.localPosition = new Vector3(p.X, p.Y, p.Z);
                }
            }
            float s = poser.Squash;
            float side = 1f / Mathf.Sqrt(Mathf.Max(0.1f, s));
            _squash.localScale = new Vector3(side, s, side);
        }

        private void Build(Material material, CharacterRecipe recipe)
        {
            _recipe = recipe.Clone().Validate();
            _skeleton = new HumanoidSkeleton(_recipe.Build);
            _bones = new Transform[HumanoidSkeleton.BoneCount];
            _bones[0] = transform;
            for (int i = 1; i < HumanoidSkeleton.BoneCount; i++)
            {
                var go = new GameObject(HumanoidSkeleton.Names[i]);
                Transform parent = _bones[HumanoidSkeleton.Parent[i]];
                go.transform.SetParent(parent, false);
                V3 p = _skeleton.BindLocal[i];
                go.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                _bones[i] = go.transform;
            }
            _squash = _bones[(int)Bone.Squash];
            var holder = new GameObject("Body");
            holder.transform.SetParent(transform, false);
            _renderer = holder.AddComponent<SkinnedMeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.bones = _bones;
            _renderer.rootBone = _bones[(int)Bone.Hips];
            _renderer.updateWhenOffscreen = false;
            _renderer.shadowCastingMode = ShadowCastingMode.On;
            _renderer.quality = SkinQuality.Bone2;
            // Loose bounds that hold every pose (arms up, seated, a jump), so culling never clips the player.
            _renderer.localBounds = new Bounds(new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 2.6f, 2.4f));
            BuildMeshes();
            BuildRing(material);
        }

        private void BuildMeshes()
        {
            var m = new MeshData(8192, 24576);
            var w = new SkinWeights(8192);
            HumanoidMesher.Build(_recipe, 0, m, w, HeadwearMode.Outfit);
            _outfitMesh = Upload(m, w, _skeleton, "Player " + _recipe.Name);
            m.Clear();
            w.Clear();
            HumanoidMesher.Build(_recipe, 0, m, w, HeadwearMode.Helmet);
            _helmetMesh = Upload(m, w, _skeleton, "Player " + _recipe.Name + " (helmet)");
            _renderer.sharedMesh = _helmet ? _helmetMesh : _outfitMesh;
        }

        /// <summary>A skinned mesh from MeshData and SkinWeights; bind poses are the inverse bone translations (the rig's
        /// bind rotations are identity).</summary>
        public static Mesh Upload(MeshData m, SkinWeights w, HumanoidSkeleton skeleton, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int n = m.VertexCount;
            var vertices = new Vector3[n];
            var normals = new Vector3[n];
            var colours = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                normals[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                colours[i] = new Color32(m.Colors[i * 4], m.Colors[i * 4 + 1], m.Colors[i * 4 + 2], m.Colors[i * 4 + 3]);
            }
            var triangles = new int[m.IndexCount];
            Array.Copy(m.Indices, triangles, m.IndexCount);
            var mesh = new Mesh { name = name };
            mesh.indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colours;
            mesh.triangles = triangles;
            if (w != null && skeleton != null)
            {
                var weights = new BoneWeight[n];
                for (int i = 0; i < n && i < w.Count; i++)
                {
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = w.Bone0[i], weight0 = w.Weight0[i], boneIndex1 = w.Bone1[i], weight1 = 1f - w.Weight0[i],
                    };
                }
                mesh.boneWeights = weights;
                var bindposes = new Matrix4x4[HumanoidSkeleton.BoneCount];
                for (int b = 0; b < bindposes.Length; b++)
                {
                    V3 p = skeleton.BindPosition[b];
                    bindposes[b] = Matrix4x4.Translate(new Vector3(-p.X, -p.Y, -p.Z));
                }
                mesh.bindposes = bindposes;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildRing(Material material)
        {
            // An opaque cream annulus just above the ground (no alpha, ASSET_MANIFEST 5).
            const int seg = 32;
            float outer = 0.5f * RingDiameterM, inner = outer - 0.07f;
            var v = new Vector3[seg * 2];
            var n = new Vector3[seg * 2];
            var c = new Color32[seg * 2];
            var t = new int[seg * 6];
            var cream = new Color32(0xFF, 0xF3, 0xD6, 0xFF);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float x = Mathf.Sin(a), z = Mathf.Cos(a);
                v[i * 2] = new Vector3(x * inner, 0.03f, z * inner);
                v[i * 2 + 1] = new Vector3(x * outer, 0.03f, z * outer);
                n[i * 2] = n[i * 2 + 1] = Vector3.up;
                c[i * 2] = c[i * 2 + 1] = cream;
                int j = (i + 1) % seg;
                t[i * 6] = i * 2;
                t[i * 6 + 1] = j * 2;
                t[i * 6 + 2] = i * 2 + 1;
                t[i * 6 + 3] = i * 2 + 1;
                t[i * 6 + 4] = j * 2;
                t[i * 6 + 5] = j * 2 + 1;
            }
            _ringMesh = new Mesh { name = "Player ring", vertices = v, normals = n, colors32 = c, triangles = t };
            _ring = new GameObject("Ground Ring");
            _ring.transform.SetParent(transform, false);
            _ring.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            var r = _ring.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            _ring.SetActive(false);
        }

        private void DestroyMeshes()
        {
            Kill(_outfitMesh);
            Kill(_helmetMesh);
            _outfitMesh = _helmetMesh = null;
        }

        private void OnDestroy()
        {
            DestroyMeshes();
            Kill(_ringMesh);
            _ringMesh = null;
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
