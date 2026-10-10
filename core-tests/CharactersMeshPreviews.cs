using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check dumps of the characters (docs/W2_DETAIL_CONTRACT.md §6): set GHUMANTE_PREVIEW_DIR and run
    /// <c>--filter CharactersMeshPreviews</c>; the OBJ files (Unity Z negated, winding reversed, like
    /// core-tests/Support/ObjDump.cs) render with tools/mesh-preview/render.py. Ordinary runs write nothing.
    /// </summary>
    public class CharactersMeshPreviews
    {
        private static string Dir
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_DIR");
                return string.IsNullOrWhiteSpace(d) ? null : d;
            }
        }

        [Test]
        public void DumpPlayerAndPedestrians()
        {
            string dir = Dir;
            if (dir == null) Assert.Pass("set GHUMANTE_PREVIEW_DIR to write previews");
            string root = Path.Combine(dir, "characters");
            Directory.CreateDirectory(root);
            var player = new CharacterRecipe { Skin = 5, Hair = 1, HairColour = 0, Seed = 7u };
            Write(Path.Combine(root, "player.obj"), Mesh(player, 0, HeadwearMode.Outfit, FaceState.Default));
            Write(Path.Combine(root, "player_helmet.obj"), Mesh(player, 0, HeadwearMode.Helmet, FaceState.Default));
            Write(Path.Combine(root, "player_lod1.obj"), Mesh(player, 1, HeadwearMode.Outfit, FaceState.Default));
            Write(Path.Combine(root, "player_lod2.obj"), Mesh(player, 2, HeadwearMode.Outfit, FaceState.Default));
            foreach (FaceExpression e in Enum.GetValues(typeof(FaceExpression)))
                Write(Path.Combine(root, "face_" + e.ToString().ToLowerInvariant() + ".obj"), Mesh(player, 0, HeadwearMode.Outfit, new FaceState(e)));
            Write(Path.Combine(root, "face_blink.obj"), Mesh(player, 0, HeadwearMode.Outfit, new FaceState(FaceExpression.Smile, 1f)));
            // Topi weaves.
            for (int w = 0; w < CharacterPalette.DhakaWeaves; w++)
            {
                CharacterRecipe r = player.Clone();
                r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi, (byte)w, (byte)w);
                Write(Path.Combine(root, "topi_" + w + ".obj"), Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default));
            }
            CharacterRecipe kalo = player.Clone();
            kalo[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.BhadgaunleTopi);
            Write(Path.Combine(root, "topi_bhadgaunle.obj"), Mesh(kalo, 0, HeadwearMode.Outfit, FaceState.Default));
            // Footwear.
            OutfitItem[] feet = { OutfitItem.Sneakers, OutfitItem.Chappal, OutfitItem.LeatherShoes, OutfitItem.TrekBoots, OutfitItem.Barefoot };
            foreach (OutfitItem f in feet)
            {
                CharacterRecipe r = player.Clone();
                r[OutfitSlotKind.Feet] = new OutfitSlot(f, 0);
                Write(Path.Combine(root, "feet_" + f.ToString().ToLowerInvariant() + ".obj"), Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default));
            }
            // Archetypes in their places.
            var line = new List<(string, CharacterRecipe)>
            {
                ("daura_topi", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 3u, StreetStyle.OldBazaar, 0)),
                ("daura_kalo_bhaktapur", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 1, 11u, StreetStyle.NewarTown, 0)),
                ("sari", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u, StreetStyle.Urban, 0)),
                ("kurta", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 5, 21u, StreetStyle.Urban, 0)),
                ("haku_patasi", CharacterRecipe.ForPedestrian(PedArchetype.Hakupatasi, 0, 5u, StreetStyle.NewarTown, 0)),
                ("porter_doko", CharacterRecipe.ForPedestrian(PedArchetype.Porter, 1, 5u, StreetStyle.OldBazaar, 1)),
                ("monk", CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 6u, StreetStyle.Buddhist, 0)),
                ("sadhu", CharacterRecipe.ForPedestrian(PedArchetype.Sadhu, 0, 8u, StreetStyle.Urban, 0)),
                ("tourist", CharacterRecipe.ForPedestrian(PedArchetype.Tourist, 3, 7u, StreetStyle.Tourist, 0)),
                ("police", CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 0, 9u, StreetStyle.Urban, 0)),
                ("school_girl", SchoolKid(true)),
                ("school_boy", SchoolKid(false)),
                ("chuba_boudha", Chuba()),
                ("casual_youth", CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 4, 12u, StreetStyle.Urban, 0)),
                ("office", CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 2, 31u, StreetStyle.Office, 0)),
                ("umbrella", CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 6, 15u, StreetStyle.Urban, 5)),
                ("baby_sling", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 3, 17u, StreetStyle.Village, 4)),
                ("gas_porter", CharacterRecipe.ForPedestrian(PedArchetype.Porter, 2, 19u, StreetStyle.Urban, 3)),
                ("daura_plain", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 2, 7u, StreetStyle.NewarTown, 0)),
                ("haku_drape", FirstWith(PedArchetype.Hakupatasi, StreetStyle.NewarTown, r => r[OutfitSlotKind.Torso].Pattern % 2 == 1)),
                ("police_shirt", FirstWith(PedArchetype.TrafficPolice, StreetStyle.Urban, r => !HumanoidMesher.WearsPoliceVest(r))),
            };
            foreach (var (name, r) in line) Write(Path.Combine(root, "ped_" + name + ".obj"), Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default));
            // The lineup of twelve walking by (one OBJ, 0.9 m apart).
            var lineup = new MeshData(65536, 196608) { HasUv0 = true };
            var lineupLod1 = new MeshData(65536, 196608) { HasUv0 = true };
            var lineupLod2 = new MeshData(65536, 196608) { HasUv0 = true };
            for (int i = 0; i < 12; i++)
            {
                CharacterRecipe r = line[i].Item2;
                Append(lineup, Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default), (i - 5.5f) * 0.9f);
                Append(lineupLod1, Mesh(r, 1, HeadwearMode.Outfit, FaceState.Default), (i - 5.5f) * 0.9f);
                Append(lineupLod2, Mesh(r, 2, HeadwearMode.Outfit, FaceState.Default), (i - 5.5f) * 0.9f);
            }
            Write(Path.Combine(root, "lineup.obj"), lineup);
            Write(Path.Combine(root, "lineup_lod1.obj"), lineupLod1);
            Write(Path.Combine(root, "lineup_lod2.obj"), lineupLod2);
        }

        /// <summary>The first pedestrian of an archetype (seeds 1, 2, ...) that matches <paramref name="want"/>.</summary>
        private static CharacterRecipe FirstWith(PedArchetype a, StreetStyle style, Func<CharacterRecipe, bool> want)
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                CharacterRecipe r = CharacterRecipe.ForPedestrian(a, (int)seed, seed, style, 0);
                if (want(r)) return r;
            }
            throw new InvalidOperationException("no " + a + " matches");
        }

        [Test]
        public void DumpPoses()
        {
            string dir = Dir;
            if (dir == null) Assert.Pass("set GHUMANTE_PREVIEW_DIR to write previews");
            string root = Path.Combine(dir, "characters");
            Directory.CreateDirectory(root);
            var baker = new CrowdBaker();
            var people = new[]
            {
                CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 4, 12u, StreetStyle.Urban, 0),
                CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u, StreetStyle.Urban, 0),
                CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 6, 15u, StreetStyle.Urban, 5),
                CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 0, 9u, StreetStyle.Urban, 0),
            };
            var clips = new (PedClip, float)[] { (PedClip.Walk, 0.1f), (PedClip.Walk, 0.35f), (PedClip.Run, 0.2f), (PedClip.Sit, 0f), (PedClip.Namaste, 1f), (PedClip.Chat, 0.7f), (PedClip.Carry, 0.3f) };
            foreach (int lod in new[] { 0, 2 })
            {
                var all = new MeshData(65536, 196608) { HasUv0 = true };
                for (int i = 0; i < people.Length; i++)
                {
                    var m = new MeshData(16384, 49152);
                    var w = new SkinWeights(16384);
                    HumanoidMesher.Build(people[i], lod, m, w, CharacterMeshOptions.For(HeadwearMode.Outfit));
                    var sk = new HumanoidSkeleton(people[i]);
                    CrowdHold hold = CrowdVariants.HoldOf(people[i]);
                    for (int c = 0; c < clips.Length; c++)
                    {
                        CrowdPose pose = i == 3 && c >= 3 ? CrowdAnimation.Officer(c - 3, 0.3f)
                                                          : CrowdAnimation.Pose(clips[c].Item1, clips[c].Item2, clips[c].Item2, clips[c].Item1 == PedClip.Run ? 3f : 1.4f, 1, hold);
                        var posed = new MeshData(16384, 49152);
                        baker.Bake(m, w, sk, pose, posed);
                        Append(all, posed, c * 0.9f, i * 1.2f);
                    }
                }
                Write(Path.Combine(root, "poses_lod" + lod + ".obj"), all);
            }
        }

        [Test]
        public void PrintTriangleBreakdown()
        {
            var player = new CharacterRecipe { Skin = 5, Hair = 1, Seed = 7u };
            var list = new List<(string, CharacterRecipe)>
            {
                ("player", player),
                ("daura", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 3u, StreetStyle.OldBazaar, 0)),
                ("sari", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u, StreetStyle.Urban, 0)),
                ("porter", CharacterRecipe.ForPedestrian(PedArchetype.Porter, 1, 5u, StreetStyle.OldBazaar, 1)),
                ("police", CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 0, 9u, StreetStyle.Urban, 0)),
            };
            foreach (var (name, r) in list)
                for (int lod = 0; lod <= 2; lod++)
                {
                    int[] parts = HumanoidMesher.Breakdown(r, lod, CharacterMeshOptions.For(HeadwearMode.Outfit));
                    var sb = new StringBuilder();
                    int sum = 0;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        sb.Append(HumanoidMesher.PartNames[i]).Append(' ').Append(parts[i]).Append(", ");
                        sum += parts[i];
                    }
                    TestContext.WriteLine(name + " LOD" + lod + " = " + sum + ": " + sb);
                }
        }

        internal static CharacterRecipe SchoolKid(bool girl)
        {
            for (uint s = 1; s < 200; s++)
            {
                CharacterRecipe r = CharacterRecipe.ForPedestrian(PedArchetype.SchoolKid, (int)s, s, StreetStyle.Urban, 0);
                if ((r[OutfitSlotKind.Legs].Item == OutfitItem.Skirt) == girl) return r;
            }
            return CharacterRecipe.ForPedestrian(PedArchetype.SchoolKid, 0, 1u);
        }

        internal static CharacterRecipe Chuba()
        {
            for (uint s = 1; s < 400; s++)
            {
                CharacterRecipe r = CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, (int)s, s, StreetStyle.Buddhist, 0);
                if (r[OutfitSlotKind.Torso].Item == OutfitItem.Chuba) return r;
            }
            return CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 0, 1u);
        }

        internal static MeshData Mesh(CharacterRecipe r, int lod, HeadwearMode mode, FaceState face)
        {
            var m = new MeshData(16384, 49152);
            var o = CharacterMeshOptions.For(mode);
            o.Face = face;
            HumanoidMesher.Build(r, lod, m, null, o);
            return m;
        }

        internal static void Append(MeshData dst, MeshData src, float dx, float dz = 0f)
        {
            int baseV = dst.VertexCount;
            for (int v = 0; v < src.VertexCount; v++)
            {
                uint rgba = (uint)(src.Colors[v * 4] << 24 | src.Colors[v * 4 + 1] << 16 | src.Colors[v * 4 + 2] << 8 | src.Colors[v * 4 + 3]);
                dst.AddVertex(src.Positions[v * 3] + dx, src.Positions[v * 3 + 1], src.Positions[v * 3 + 2] + dz, src.Normals[v * 3], src.Normals[v * 3 + 1],
                              src.Normals[v * 3 + 2], rgba, src.Uv0[v * 2], src.Uv0[v * 2 + 1]);
            }
            for (int t = 0; t < src.IndexCount; t += 3) dst.AddTriangle(src.Indices[t] + baseV, src.Indices[t + 1] + baseV, src.Indices[t + 2] + baseV);
        }

        /// <summary>Writes <paramref name="m"/> as OBJ in the ObjDump convention (Z negated, winding reversed, colours,
        /// normals, UV0) to an absolute <paramref name="path"/>.</summary>
        internal static void Write(string path, MeshData m)
        {
            File.WriteAllText(path, ObjDump.Format(null, new ObjPart(Path.GetFileNameWithoutExtension(path), m)));
        }
    }
}
