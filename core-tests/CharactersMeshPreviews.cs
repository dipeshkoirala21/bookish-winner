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

        /// <summary>
        /// Close-ups for the detail fixes: the topi on the player and its weaves, the porters' namlo with and without a
        /// topi, every footwear, the schoolchildren's bag straps, the mid-distance LOD2 and far lineups, and one crowd shape
        /// in several garment colours (near recolour and far tint). Written under <c>characters/fix/</c>.
        /// </summary>
        [Test]
        public void DumpDetailFixes()
        {
            string dir = Dir;
            if (dir == null) Assert.Pass("set GHUMANTE_PREVIEW_DIR to write previews");
            string root = Path.Combine(dir, "characters", "fix");
            Directory.CreateDirectory(root);
            var player = new CharacterRecipe { Skin = 5, Hair = 1, HairColour = 0, Seed = 7u };
            float headY = new HumanoidSkeleton(player).Metrics.HeadBaseY;
            Write(Path.Combine(root, "topi_player_head.obj"), Crop(Mesh(player, 0, HeadwearMode.Outfit, FaceState.Default), headY - 0.02f, 9f));
            var weaves = new MeshData(65536, 196608) { HasUv0 = true };
            for (int wv = 0; wv <= CharacterPalette.DhakaWeaves; wv++)
            {
                CharacterRecipe r = player.Clone();
                r[OutfitSlotKind.Head] = wv < CharacterPalette.DhakaWeaves ? new OutfitSlot(OutfitItem.DhakaTopi, (byte)wv, (byte)wv) : new OutfitSlot(OutfitItem.BhadgaunleTopi);
                r.Hair = (byte)(wv % 4);
                Append(weaves, Crop(Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default), headY - 0.02f, 9f), (wv - 3f) * 0.5f);
            }
            Write(Path.Combine(root, "topi_weaves.obj"), weaves);
            var topiLods = new MeshData(65536, 196608) { HasUv0 = true };
            for (int lod = 0; lod <= HumanoidMesher.FarLod; lod++)
                Append(topiLods, Crop(Mesh(player, lod, HeadwearMode.Outfit, FaceState.Default), headY - 0.02f, 9f), (lod - 1.5f) * 0.5f);
            Write(Path.Combine(root, "topi_lods.obj"), topiLods);
            // Porters: the doko's namlo over a topi and over bare hair.
            CharacterRecipe porterTopi = FirstWith(PedArchetype.Porter, StreetStyle.OldBazaar, r => r[OutfitSlotKind.Head].Item == OutfitItem.DhakaTopi && CharacterRecipe.UsesNamlo(r[OutfitSlotKind.Back].Item), 1);
            CharacterRecipe porterBare = FirstWith(PedArchetype.Porter, StreetStyle.OldBazaar, r => r[OutfitSlotKind.Head].Item == OutfitItem.None && CharacterRecipe.UsesNamlo(r[OutfitSlotKind.Back].Item), 1);
            CharacterRecipe gas = CharacterRecipe.ForPedestrian(PedArchetype.Porter, 2, 19u, StreetStyle.Urban, 3);
            var heads = new MeshData(65536, 196608) { HasUv0 = true };
            Append(heads, Crop(Mesh(porterTopi, 0, HeadwearMode.Outfit, FaceState.Default), headY - 0.25f, 9f), -0.6f);
            Append(heads, Crop(Mesh(porterBare, 0, HeadwearMode.Outfit, FaceState.Default), headY - 0.25f, 9f), 0f);
            Append(heads, Crop(Mesh(gas, 0, HeadwearMode.Outfit, FaceState.Default), headY - 0.25f, 9f), 0.6f);
            Write(Path.Combine(root, "namlo_heads.obj"), heads);
            Write(Path.Combine(root, "porter_topi.obj"), Mesh(porterTopi, 0, HeadwearMode.Outfit, FaceState.Default));
            // Footwear close-ups (below the knee), side by side.
            OutfitItem[] feet = { OutfitItem.Chappal, OutfitItem.Barefoot, OutfitItem.Sneakers, OutfitItem.LeatherShoes, OutfitItem.TrekBoots };
            var shoes = new MeshData(65536, 196608) { HasUv0 = true };
            for (int i = 0; i < feet.Length; i++)
            {
                CharacterRecipe r = player.Clone();
                r[OutfitSlotKind.Feet] = new OutfitSlot(feet[i], 0);
                if (feet[i] == OutfitItem.Chappal || feet[i] == OutfitItem.Barefoot) r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Shorts, 1);
                Append(shoes, Crop(Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default), -1f, 0.22f), (i - 2f) * 0.45f);
            }
            Write(Path.Combine(root, "feet.obj"), shoes);
            // Schoolchildren with daypacks (the straps over the shirt, tie and pocket).
            var school = new MeshData(65536, 196608) { HasUv0 = true };
            int k = 0;
            foreach (bool girl in new[] { true, false })
                for (int lod = 0; lod <= 1; lod++)
                {
                    CharacterRecipe r = SchoolKid(girl);
                    r[OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.Daypack, 2);
                    Append(school, Mesh(r, lod, HeadwearMode.Outfit, FaceState.Default), (k++ - 1.5f) * 0.7f);
                }
            Write(Path.Combine(root, "school_straps.obj"), school);
            // The lineup at the mid-distance LOD2 and the far level.
            var line = new List<CharacterRecipe>
            {
                CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 3u, StreetStyle.OldBazaar, 0), CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u, StreetStyle.Urban, 0),
                CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 5, 21u, StreetStyle.Urban, 0), CharacterRecipe.ForPedestrian(PedArchetype.Hakupatasi, 0, 5u, StreetStyle.NewarTown, 0),
                CharacterRecipe.ForPedestrian(PedArchetype.Porter, 1, 5u, StreetStyle.OldBazaar, 1), CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 6u, StreetStyle.Buddhist, 0),
                CharacterRecipe.ForPedestrian(PedArchetype.Tourist, 3, 7u, StreetStyle.Tourist, 0), CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 0, 9u, StreetStyle.Urban, 0),
                SchoolKid(true), CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 4, 12u, StreetStyle.Urban, 0), player,
            };
            for (int lod = 1; lod <= HumanoidMesher.FarLod; lod++)
            {
                var all = new MeshData(65536, 196608) { HasUv0 = true };
                for (int i = 0; i < line.Count; i++) Append(all, Mesh(line[i], lod, HeadwearMode.Outfit, FaceState.Default), (i - line.Count * 0.5f) * 0.9f);
                Write(Path.Combine(root, "lineup_lod" + lod + ".obj"), all);
            }
            // Pashupati: pilgrims and the sadhus among them.
            var pash = new MeshData(65536, 196608) { HasUv0 = true };
            int placed = 0;
            for (uint seed = 1; seed < 80 && placed < 8; seed++)
            {
                PedArchetype a = seed % 3 == 0 ? PedArchetype.KurtaSari : seed % 3 == 1 ? PedArchetype.DauraSuruwal : PedArchetype.UrbanCasual;
                CharacterRecipe r = CharacterRecipe.ForPedestrian(a, (int)seed, seed, StreetStyle.Pashupati, 0);
                bool sadhu = r.Hair == CharacterRecipe.HairDreadlocks;
                if (placed < 3 ? !sadhu : sadhu && placed > 4) continue;
                Append(pash, Mesh(r, 0, HeadwearMode.Outfit, FaceState.Default), (placed++ - 3.5f) * 0.9f);
            }
            Write(Path.Combine(root, "pashupati.obj"), pash);
            // One crowd shape in six garment colours: near (LOD1 recoloured) in front, far (baked, shader tint) behind.
            int shape = CrowdVariants.Key(PedArchetype.KurtaSari, StreetStyle.Urban, 0, 4);
            CharacterRecipe sr = CrowdVariants.Recipe(shape);
            var looks = new MeshData(65536, 196608) { HasUv0 = true };
            var near = new MeshData(16384, 49152);
            var nearW = new SkinWeights(16384);
            HumanoidMesher.Build(sr, 1, near, nearW, CrowdVariants.Options(sr));
            var far = new MeshData(4096, 12288);
            var farW = new SkinWeights(4096);
            HumanoidMesher.Build(sr, HumanoidMesher.FarLod, far, farW, CrowdVariants.Options(sr));
            var frame = new MeshData(4096, 12288);
            new CrowdBaker().Bake(far, farW, new HumanoidSkeleton(sr), CrowdVariants.FramePose(FarFrame.Stand, CrowdVariants.HoldOf(sr)), frame);
            for (int c = 0; c < 6; c++)
            {
                uint rgb = CrowdVariants.GarmentColour(sr, c * 3);
                Append(looks, Recoloured(near, rgb), (c - 2.5f) * 0.8f);
                Append(looks, Recoloured(frame, rgb), (c - 2.5f) * 0.8f, -1.6f);
            }
            Write(Path.Combine(root, "looks_near_far.obj"), looks);
        }

        /// <summary>A copy of <paramref name="m"/> with its tint mask applied in <paramref name="rgb"/> (the crowd's look).</summary>
        private static MeshData Recoloured(MeshData m, uint rgb)
        {
            var c = new MeshData(m.VertexCount + 4, m.IndexCount + 4) { HasUv0 = true };
            Append(c, m, 0f);
            var rgba = new byte[m.VertexCount * 4];
            CrowdVariants.Recolour(m, rgb, rgba);
            for (int i = 0; i < m.VertexCount; i++)
            {
                c.Colors[i * 4] = rgba[i * 4];
                c.Colors[i * 4 + 1] = rgba[i * 4 + 1];
                c.Colors[i * 4 + 2] = rgba[i * 4 + 2];
                c.Colors[i * 4 + 3] = 255;
            }
            return c;
        }

        /// <summary>The triangles of <paramref name="m"/> whose centroid lies between two heights.</summary>
        internal static MeshData Crop(MeshData m, float y0, float y1)
        {
            var c = new MeshData(m.VertexCount + 4, m.IndexCount + 4) { HasUv0 = true };
            var map = new int[m.VertexCount];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            for (int t = 0; t < m.IndexCount; t += 3)
            {
                int a = m.Indices[t], b = m.Indices[t + 1], d = m.Indices[t + 2];
                float y = (m.Positions[a * 3 + 1] + m.Positions[b * 3 + 1] + m.Positions[d * 3 + 1]) / 3f;
                if (y < y0 || y > y1) continue;
                int[] tri = { a, b, d };
                for (int q = 0; q < 3; q++)
                {
                    int v = tri[q];
                    if (map[v] < 0)
                    {
                        uint rgba = (uint)(m.Colors[v * 4] << 24 | m.Colors[v * 4 + 1] << 16 | m.Colors[v * 4 + 2] << 8 | m.Colors[v * 4 + 3]);
                        map[v] = c.AddVertex(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2], m.Normals[v * 3], m.Normals[v * 3 + 1],
                                             m.Normals[v * 3 + 2], rgba, m.Uv0[v * 2], m.Uv0[v * 2 + 1]);
                    }
                    tri[q] = map[v];
                }
                c.AddTriangle(tri[0], tri[1], tri[2]);
            }
            return c;
        }

        /// <summary>The first pedestrian of an archetype (seeds 1, 2, ...) that matches <paramref name="want"/>.</summary>
        private static CharacterRecipe FirstWith(PedArchetype a, StreetStyle style, Func<CharacterRecipe, bool> want, int carry = 0)
        {
            for (uint seed = 1; seed < 400; seed++)
            {
                CharacterRecipe r = CharacterRecipe.ForPedestrian(a, (int)seed, seed, style, carry);
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
