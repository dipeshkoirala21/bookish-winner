using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Save;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The character recipe and the procedural humanoid mesher (W2_DESIGN 6.1, 10.3; P §2).</summary>
    public class CharactersMeshTests
    {
        private static CharacterRecipe[] Wardrobe()
        {
            var list = new System.Collections.Generic.List<CharacterRecipe>();
            OutfitItem[] heads = { OutfitItem.None, OutfitItem.DhakaTopi, OutfitItem.BhadgaunleTopi, OutfitItem.Beanie, OutfitItem.SunHat };
            OutfitItem[] tops = { OutfitItem.TShirt, OutfitItem.Hoodie, OutfitItem.DhakaJacket, OutfitItem.Kurta, OutfitItem.Daura, OutfitItem.Sari, OutfitItem.Robe, OutfitItem.Uniform };
            OutfitItem[] legs = { OutfitItem.Jeans, OutfitItem.Joggers, OutfitItem.Shorts, OutfitItem.Suruwal };
            OutfitItem[] feet = { OutfitItem.Sneakers, OutfitItem.Chappal, OutfitItem.TrekBoots, OutfitItem.Barefoot };
            OutfitItem[] backs = { OutfitItem.None, OutfitItem.Daypack, OutfitItem.Doko };
            int i = 0;
            for (int hair = 1; hair <= CharacterRecipe.HairStyleCount; hair++)
            {
                foreach (OutfitItem top in tops)
                {
                    var r = new CharacterRecipe
                    {
                        Build = (BodyBuild)(i % 4),
                        Skin = (byte)(1 + i % 10),
                        Hair = (byte)hair,
                        HairColour = (byte)(i % 8),
                        HelmetColour = (byte)(i % 8),
                        Seed = (uint)i,
                    };
                    r[OutfitSlotKind.Head] = new OutfitSlot(heads[i % heads.Length], (byte)i, (byte)i);
                    r[OutfitSlotKind.Torso] = new OutfitSlot(top, (byte)i);
                    r[OutfitSlotKind.Legs] = new OutfitSlot(legs[i % legs.Length], (byte)i);
                    r[OutfitSlotKind.Feet] = new OutfitSlot(feet[i % feet.Length], (byte)i);
                    r[OutfitSlotKind.Back] = new OutfitSlot(backs[i % backs.Length], (byte)i);
                    list.Add(r.Validate());
                    i++;
                }
            }
            return list.ToArray();
        }

        [Test]
        public void EveryWardrobeFitsTheTriangleBudgetsAtEveryLevel()
        {
            int worst0 = 0, worst1 = 0, worst2 = 0, worstV2 = 0;
            foreach (CharacterRecipe r in Wardrobe())
            {
                foreach (HeadwearMode mode in new[] { HeadwearMode.Outfit, HeadwearMode.Helmet })
                {
                    for (int lod = 0; lod <= 2; lod++)
                    {
                        var m = new MeshData();
                        var w = new SkinWeights();
                        HumanoidMesher.Build(r, lod, m, w, mode);
                        Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(HumanoidMesher.Budget(lod)), "lod " + lod + " hair " + r.Hair + " " + r[OutfitSlotKind.Torso].Item);
                        Assert.That(m.TriangleCount, Is.GreaterThan(lod == 2 ? 150 : 600));
                        if (lod == 0) worst0 = Math.Max(worst0, m.TriangleCount);
                        if (lod == 1) worst1 = Math.Max(worst1, m.TriangleCount);
                        if (lod == 2)
                        {
                            worst2 = Math.Max(worst2, m.TriangleCount);
                            worstV2 = Math.Max(worstV2, m.VertexCount);
                        }
                    }
                }
            }
            TestContext.WriteLine("worst triangles: LOD0 " + worst0 + ", LOD1 " + worst1 + ", LOD2 " + worst2 + " (" + worstV2 + " vertices)");
        }

        [Test]
        public void MeshesAreWellFormedAndSkinnedToRealBones()
        {
            foreach (CharacterRecipe r in Wardrobe())
            {
                var m = new MeshData();
                var w = new SkinWeights();
                HumanoidMesher.Build(r, 0, m, w);
                Assert.That(w.Count, Is.EqualTo(m.VertexCount));
                for (int v = 0; v < m.VertexCount; v++)
                {
                    float x = m.Positions[v * 3], y = m.Positions[v * 3 + 1], z = m.Positions[v * 3 + 2];
                    Assert.That(float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z), Is.True);
                    float nx = m.Normals[v * 3], ny = m.Normals[v * 3 + 1], nz = m.Normals[v * 3 + 2];
                    Assert.That(Math.Abs(nx * nx + ny * ny + nz * nz - 1f), Is.LessThan(1e-3f));
                    Assert.That(w.Bone0[v], Is.LessThan(HumanoidSkeleton.BoneCount));
                    Assert.That(w.Bone1[v], Is.LessThan(HumanoidSkeleton.BoneCount));
                    Assert.That(w.Weight0[v], Is.InRange(0.5f, 1f));
                    Assert.That(m.Colors[v * 4 + 3], Is.EqualTo(255), "opaque, no alpha anywhere");
                    Assert.That(y, Is.GreaterThan(-0.01f), "nothing under the soles");
                }
                for (int t = 0; t < m.IndexCount; t += 3)
                {
                    int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                    Assert.That(a < m.VertexCount && b < m.VertexCount && c < m.VertexCount, Is.True);
                }
            }
        }

        [Test]
        public void ProportionsMatchTheDesign()
        {
            // 1.55 m barefoot on build B; the topi adds about 0.09 m, the helmet about 0.12 m (P §2.3).
            var r = new CharacterRecipe { Hair = 2 };
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            Assert.That(MaxY(r, HeadwearMode.Outfit), Is.EqualTo(1.55f).Within(0.03f));
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi);
            Assert.That(MaxY(r, HeadwearMode.Outfit), Is.EqualTo(1.64f).Within(0.035f));
            Assert.That(MaxY(r, HeadwearMode.Helmet), Is.EqualTo(1.62f).Within(0.06f));
            var tall = new CharacterRecipe { Build = BodyBuild.D, Hair = 2 };
            tall[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            Assert.That(MaxY(tall, HeadwearMode.Outfit), Is.EqualTo(1.66f).Within(0.03f));
            BodyMetrics b = BodyMetrics.For(BodyBuild.B);
            Assert.That(b.HeadH / b.HeightM, Is.EqualTo(1f / 3.9f).Within(0.01f), "3.9 heads tall");
            Assert.That(b.LegM, Is.EqualTo(0.63f + 0.075f).Within(0.08f));
            Assert.That(b.ShoulderW, Is.EqualTo(0.42f).Within(1e-4f));
            Assert.That(b.HipW, Is.EqualTo(0.34f).Within(1e-4f));
        }

        private static float MaxY(CharacterRecipe r, HeadwearMode mode)
        {
            var m = new MeshData();
            HumanoidMesher.Build(r, 0, m, null, mode);
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            return maxY;
        }

        [Test]
        public void TheTopiIsTallerAtTheFrontAndTilted()
        {
            Assert.That(HumanoidMesher.TopiFrontM, Is.EqualTo(0.11f));
            Assert.That(HumanoidMesher.TopiBackM, Is.EqualTo(0.075f));
            Assert.That(HumanoidMesher.TopiTiltDeg, Is.EqualTo(6f));
            var r = new CharacterRecipe { Hair = 2 };
            var bare = new MeshData();
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            HumanoidMesher.Build(r, 0, bare, null);
            int n0 = bare.VertexCount;
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi);
            var topi = new MeshData();
            HumanoidMesher.Build(r, 0, topi, null);
            Assert.That(topi.VertexCount, Is.GreaterThan(n0));
            // The highest topi point in front of the head centre is higher than the highest one behind it.
            float front = 0f, back = 0f;
            for (int v = 0; v < topi.VertexCount; v++)
            {
                float y = topi.Positions[v * 3 + 1], z = topi.Positions[v * 3 + 2];
                if (y < 1.5f) continue;
                if (z > 0.1f) front = Math.Max(front, y);
                if (z < -0.08f) back = Math.Max(back, y);
            }
            Assert.That(front, Is.GreaterThan(back + 0.015f));
        }

        [Test]
        public void BuildingIsDeterministic()
        {
            CharacterRecipe r = CharacterRecipe.NewPlayer(42u);
            var a = new MeshData();
            var b = new MeshData();
            var wa = new SkinWeights();
            var wb = new SkinWeights();
            HumanoidMesher.Build(r, 0, a, wa);
            HumanoidMesher.Build(r.Clone(), 0, b, wb);
            Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
            for (int i = 0; i < a.IndexCount; i++) Assert.That(a.Indices[i], Is.EqualTo(b.Indices[i]));
            for (int i = 0; i < wa.Count; i++) Assert.That(wa.Weight0[i], Is.EqualTo(wb.Weight0[i]));
        }

        [Test]
        public void NewPlayersNeverStartOnTheFirstSkinSwatch()
        {
            var seen = new bool[CharacterRecipe.SkinCount + 1];
            for (uint s = 0; s < 500; s++)
            {
                CharacterRecipe r = CharacterRecipe.NewPlayer(s);
                Assert.That(r.Skin, Is.InRange(2, 10));
                seen[r.Skin] = true;
            }
            for (int k = 2; k <= 10; k++) Assert.That(seen[k], Is.True, "swatch " + k + " reachable");
            Assert.That(CharacterPalette.SkinBase.Length, Is.EqualTo(10));
            Assert.That(CharacterPalette.SkinBase[0], Is.EqualTo(0xF3D3B5u));
            Assert.That(CharacterPalette.SkinBase[9], Is.EqualTo(0x5C3826u));
        }

        [Test]
        public void EveryPedestrianArchetypeBuildsWithTheSameGenerator()
        {
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
            {
                for (int tint = 0; tint < 6; tint++)
                {
                    CharacterRecipe r = CharacterRecipe.ForPedestrian(a, tint, (uint)(tint * 31 + (int)a));
                    for (int lod = 0; lod <= 2; lod++)
                    {
                        var m = new MeshData();
                        HumanoidMesher.BuildStatic(r, lod, m);
                        Assert.That(m.TriangleCount, Is.InRange(150, HumanoidMesher.Budget(lod)));
                    }
                }
            }
            CharacterRecipe monk = CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 1u);
            Assert.That(monk[OutfitSlotKind.Torso].Item, Is.EqualTo(OutfitItem.Robe));
            CharacterRecipe porter = CharacterRecipe.ForPedestrian(PedArchetype.Porter, 0, 1u);
            Assert.That(porter[OutfitSlotKind.Back].Item, Is.EqualTo(OutfitItem.Doko));
            CharacterRecipe daura = CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 1u);
            Assert.That(daura[OutfitSlotKind.Head].Item, Is.EqualTo(OutfitItem.DhakaTopi));
            Assert.That(daura[OutfitSlotKind.Legs].Item, Is.EqualTo(OutfitItem.Suruwal));
        }

        [Test]
        public void AppearanceRoundTripsThroughTheSaveAndClampsBadValues()
        {
            CharacterRecipe r = CharacterRecipe.NewPlayer(7u);
            r.Build = BodyBuild.C;
            r[OutfitSlotKind.Torso] = new OutfitSlot(OutfitItem.Daura, 2);
            r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Suruwal, 1);
            r.Name = "Asha";
            JsonObject o = r.ToJson();
            string text = o.ToString();
            CharacterRecipe back = CharacterRecipe.FromJson(Json.Parse(text).AsObject());
            Assert.That(back, Is.EqualTo(r));
            Assert.That(text.Length, Is.LessThan(400));

            var bad = new JsonObject().Set("build", 9L).Set("skin", 0L).Set("hair", 99L).Set("hairColour", 300L)
                .Set("outfit", new JsonArray().Add(new JsonObject().Set("item", (long)OutfitItem.Jeans)));
            CharacterRecipe fixedUp = CharacterRecipe.FromJson(bad);
            Assert.That(fixedUp.Build, Is.EqualTo(BodyBuild.B));
            Assert.That(fixedUp.Skin, Is.InRange(1, 10));
            Assert.That(fixedUp.Hair, Is.InRange(1, 12));
            Assert.That(fixedUp[OutfitSlotKind.Head].Item, Is.EqualTo(OutfitItem.None), "jeans are not a hat");
            Assert.That(CharacterRecipe.FromJson(null).Validate().Skin, Is.InRange(1, 10));
        }

        [Test]
        public void SkeletonHasThe37HumBonesWithParentsBeforeChildren()
        {
            Assert.That(HumanoidSkeleton.BoneCount, Is.EqualTo(37));
            Assert.That(HumanoidSkeleton.Parent.Length, Is.EqualTo(37));
            Assert.That(HumanoidSkeleton.Names.Length, Is.EqualTo(37));
            for (int i = 1; i < 37; i++) Assert.That(HumanoidSkeleton.Parent[i], Is.InRange(0, i - 1));
            var sk = new HumanoidSkeleton(BodyBuild.B);
            Assert.That(sk[Bone.HandL].X, Is.LessThan(0f), "left is −X");
            Assert.That(sk[Bone.Head].Y, Is.EqualTo(sk.Metrics.HeadBaseY).Within(1e-5f));
            var local = new Quat[37];
            for (int i = 0; i < 37; i++) local[i] = Quat.Identity;
            var pos = new V3[37];
            var rot = new Quat[37];
            sk.Solve(local, V3.Zero, 1f, pos, rot);
            for (int i = 0; i < 37; i++) Assert.That(V3.Distance(pos[i], sk.BindPosition[i]), Is.LessThan(1e-5f));
        }

        /// <summary>Set GHUMANTE_CHAR_DUMP to a folder to write OBJ files of sample characters for a visual check.</summary>
        [Test]
        public void DumpSamplesWhenAsked()
        {
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_CHAR_DUMP");
            if (string.IsNullOrEmpty(dir)) Assert.Pass("set GHUMANTE_CHAR_DUMP to write samples");
            Directory.CreateDirectory(dir);
            var samples = new (string, CharacterRecipe, HeadwearMode, int)[]
            {
                ("player_default", new CharacterRecipe { Skin = 6, Hair = 1 }, HeadwearMode.Outfit, 0),
                ("player_helmet", new CharacterRecipe { Skin = 6, Hair = 8 }, HeadwearMode.Helmet, 0),
                ("daura", CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 3u), HeadwearMode.Outfit, 0),
                ("sari", CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 2, 4u), HeadwearMode.Outfit, 0),
                ("porter", CharacterRecipe.ForPedestrian(PedArchetype.Porter, 1, 5u), HeadwearMode.Outfit, 0),
                ("monk_lod1", CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 6u), HeadwearMode.Outfit, 1),
                ("tourist_lod2", CharacterRecipe.ForPedestrian(PedArchetype.Tourist, 3, 7u), HeadwearMode.Outfit, 2),
            };
            foreach (var (name, recipe, mode, lod) in samples)
            {
                var m = new MeshData();
                HumanoidMesher.Build(recipe, lod, m, null, mode);
                File.WriteAllText(Path.Combine(dir, name + ".obj"), Obj(m));
            }
        }

        internal static string Obj(MeshData m)
        {
            var sb = new StringBuilder();
            CultureInfo c = CultureInfo.InvariantCulture;
            for (int v = 0; v < m.VertexCount; v++)
            {
                sb.Append("v ").Append(m.Positions[v * 3].ToString("0.#####", c)).Append(' ').Append(m.Positions[v * 3 + 1].ToString("0.#####", c)).Append(' ')
                  .Append(m.Positions[v * 3 + 2].ToString("0.#####", c)).Append(' ').Append((m.Colors[v * 4] / 255f).ToString("0.###", c)).Append(' ')
                  .Append((m.Colors[v * 4 + 1] / 255f).ToString("0.###", c)).Append(' ').Append((m.Colors[v * 4 + 2] / 255f).ToString("0.###", c)).Append('\n');
            }
            for (int v = 0; v < m.VertexCount; v++)
                sb.Append("vn ").Append(m.Normals[v * 3].ToString("0.####", c)).Append(' ').Append(m.Normals[v * 3 + 1].ToString("0.####", c)).Append(' ')
                  .Append(m.Normals[v * 3 + 2].ToString("0.####", c)).Append('\n');
            for (int t = 0; t < m.IndexCount; t += 3)
                sb.Append("f ").Append(m.Indices[t] + 1).Append(' ').Append(m.Indices[t + 1] + 1).Append(' ').Append(m.Indices[t + 2] + 1).Append('\n');
            return sb.ToString();
        }
    }
}
