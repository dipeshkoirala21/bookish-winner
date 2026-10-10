using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Save;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The character recipe and the procedural humanoid mesher (W2_DESIGN 6.1, 10.3; P §2; ref_characters.md).</summary>
    public class CharactersMeshTests
    {
        private static readonly OutfitItem[] Heads =
        {
            OutfitItem.None, OutfitItem.DhakaTopi, OutfitItem.BhadgaunleTopi, OutfitItem.Beanie, OutfitItem.SunHat, OutfitItem.Cap, OutfitItem.PoliceCap,
            OutfitItem.Headscarf,
        };

        private static readonly OutfitItem[] Tops =
        {
            OutfitItem.None, OutfitItem.TShirt, OutfitItem.Hoodie, OutfitItem.DhakaJacket, OutfitItem.Kurta, OutfitItem.Daura, OutfitItem.Sari, OutfitItem.Robe,
            OutfitItem.Uniform, OutfitItem.Shirt, OutfitItem.PoliceUniform, OutfitItem.Fleece, OutfitItem.Vest, OutfitItem.SchoolShirt, OutfitItem.Chuba,
            OutfitItem.Jacket, OutfitItem.Blazer,
        };

        private static readonly OutfitItem[] Legs =
        {
            OutfitItem.None, OutfitItem.Jeans, OutfitItem.Joggers, OutfitItem.Shorts, OutfitItem.Suruwal, OutfitItem.Trousers, OutfitItem.Skirt,
            OutfitItem.TrekTrousers, OutfitItem.RolledTrousers,
        };

        private static readonly OutfitItem[] Feet =
        {
            OutfitItem.Sneakers, OutfitItem.Chappal, OutfitItem.TrekBoots, OutfitItem.Barefoot, OutfitItem.LeatherShoes, OutfitItem.None,
        };

        private static readonly OutfitItem[] Backs =
        {
            OutfitItem.None, OutfitItem.Daypack, OutfitItem.Doko, OutfitItem.Sack, OutfitItem.GasCylinder, OutfitItem.BabySling,
        };

        /// <summary>A wardrobe touching every item, hairstyle, beard, accent, build, figure and age group.</summary>
        internal static CharacterRecipe[] Wardrobe()
        {
            var list = new List<CharacterRecipe>();
            int i = 0;
            for (int hair = 1; hair <= CharacterRecipe.HairStyleCount; hair++)
            {
                foreach (OutfitItem top in Tops)
                {
                    var r = new CharacterRecipe
                    {
                        Build = (BodyBuild)(i % 4),
                        Figure = (byte)(i % 2),
                        Age = (AgeGroup)(i % 4),
                        HeightPct = (sbyte)(i % 13 - 6),
                        Skin = (byte)(1 + i % 10),
                        Hair = (byte)hair,
                        HairColour = (byte)(i % CharacterRecipe.HairColourCount),
                        Beard = (FacialHair)(i % 7),
                        EyeColour = (byte)(i % CharacterRecipe.EyeColourCount),
                        Accents = (CharacterAccents)((i * 2654435761u) & 0x7FFF),
                        HelmetColour = (byte)(i % 8),
                        Seed = (uint)i,
                    };
                    r[OutfitSlotKind.Head] = new OutfitSlot(Heads[i % Heads.Length], (byte)i, (byte)i);
                    r[OutfitSlotKind.Torso] = new OutfitSlot(top, (byte)i, (byte)(i / 3));
                    r[OutfitSlotKind.Legs] = new OutfitSlot(Legs[i % Legs.Length], (byte)i);
                    r[OutfitSlotKind.Feet] = new OutfitSlot(Feet[i % Feet.Length], (byte)i);
                    r[OutfitSlotKind.Back] = new OutfitSlot(Backs[i % Backs.Length], (byte)i, (byte)i);
                    list.Add(r.Validate());
                    i++;
                }
            }
            return list.ToArray();
        }

        [Test]
        public void EveryWardrobeFitsTheTriangleBudgetsAtEveryLevel()
        {
            int[] worst = new int[3];
            foreach (CharacterRecipe r in Wardrobe())
            {
                foreach (HeadwearMode mode in new[] { HeadwearMode.Outfit, HeadwearMode.Helmet })
                {
                    for (int lod = 0; lod <= 2; lod++)
                    {
                        var m = new MeshData();
                        var w = new SkinWeights();
                        HumanoidMesher.Build(r, lod, m, w, mode);
                        Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(HumanoidMesher.Budget(lod)),
                                    "lod " + lod + " hair " + r.Hair + " " + r[OutfitSlotKind.Torso].Item + " " + r[OutfitSlotKind.Back].Item);
                        Assert.That(m.TriangleCount, Is.GreaterThan(lod == 2 ? 250 : lod == 1 ? 1200 : 5000));
                        worst[lod] = Math.Max(worst[lod], m.TriangleCount);
                    }
                }
            }
            TestContext.WriteLine("worst triangles: LOD0 " + worst[0] + ", LOD1 " + worst[1] + ", LOD2 " + worst[2]);
        }

        [Test]
        public void MeshesAreWellFormedSkinnedAndCarryMaterialChannels()
        {
            foreach (CharacterRecipe r in Wardrobe())
            {
                for (int lod = 0; lod <= 2; lod += 2)
                {
                    var m = new MeshData();
                    var w = new SkinWeights();
                    HumanoidMesher.Build(r, lod, m, w);
                    Assert.That(w.Count, Is.EqualTo(m.VertexCount));
                    Assert.That(m.HasUv0, Is.True, "UV0 = (material channel, AO) on every vertex (contract §5)");
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
                        float ch = m.Uv0[v * 2], ao = m.Uv0[v * 2 + 1];
                        Assert.That(Enum.IsDefined(typeof(MaterialChannel), (byte)ch) && ch == MathF.Floor(ch), Is.True, "channel " + ch);
                        Assert.That(ao, Is.InRange(CharacterAo.Floor * 0.5f, 1f));
                    }
                    for (int t = 0; t < m.IndexCount; t += 3)
                    {
                        int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                        Assert.That(a < m.VertexCount && b < m.VertexCount && c < m.VertexCount, Is.True);
                    }
                }
            }
        }

        [Test]
        public void SkinIsInTheSkinChannelAndClothInFabric()
        {
            var r = new CharacterRecipe { Hair = 1, Seed = 3u };
            var m = new MeshData();
            HumanoidMesher.BuildStatic(r, 0, m);
            int skin = 0, fabric = 0, hair = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                var ch = (MaterialChannel)(int)m.Uv0[v * 2];
                if (ch == MaterialChannel.Skin) skin++;
                if (ch == MaterialChannel.Fabric) fabric++;
                if (ch == MaterialChannel.Hair) hair++;
            }
            Assert.That(skin, Is.GreaterThan(500));
            Assert.That(fabric, Is.GreaterThan(1000));
            Assert.That(hair, Is.GreaterThan(100));
        }

        [Test]
        public void TheBodyAoDarkensTheArmpitsAndUnderTheChin()
        {
            var r = new CharacterRecipe { Hair = 2, Seed = 3u };
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            var m = new MeshData();
            HumanoidMesher.BuildStatic(r, 0, m);
            BodyMetrics b = BodyMetrics.For(r);
            float minArmpit = 1f, open = 0f;
            int n = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                float x = m.Positions[v * 3], y = m.Positions[v * 3 + 1], ao = m.Uv0[v * 2 + 1];
                float nx = m.Normals[v * 3];
                if (y > b.ShoulderY - 0.25f && y < b.ShoulderY - 0.05f && Math.Abs(x) > 0.18f && Math.Abs(x) < 0.26f && nx * x < 0f) minArmpit = Math.Min(minArmpit, ao);
                if (y > b.HeadBaseY + 0.3f)
                {
                    open += ao;
                    n++;
                }
            }
            Assert.That(minArmpit, Is.LessThan(0.86f), "inner arms face the torso");
            Assert.That(open / Math.Max(1, n), Is.GreaterThan(0.9f), "the crown is open");
            Assert.That(open / Math.Max(1, n) - minArmpit, Is.GreaterThan(0.08f), "the armpits are darker than the crown");
        }

        [Test]
        public void ProportionsMatchTheDesign()
        {
            // 1.55 m barefoot on build B (P §2.3); the topi stands above the crown, the helmet a little less.
            var r = new CharacterRecipe { Hair = 2 };
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            float bare = MaxY(r, HeadwearMode.Outfit);
            Assert.That(bare, Is.EqualTo(1.55f).Within(0.03f));
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi);
            float topi = MaxY(r, HeadwearMode.Outfit);
            Assert.That(topi - bare, Is.InRange(0.03f, 0.1f));
            Assert.That(MaxY(r, HeadwearMode.Helmet) - bare, Is.InRange(0.02f, 0.08f));
            var tall = new CharacterRecipe { Build = BodyBuild.D, Hair = 2 };
            tall[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            Assert.That(MaxY(tall, HeadwearMode.Outfit), Is.EqualTo(1.66f).Within(0.03f));
            var child = new CharacterRecipe { Age = AgeGroup.Child, Hair = 2 };
            child[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.None);
            child[OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.None);
            Assert.That(MaxY(child, HeadwearMode.Outfit), Is.EqualTo(1.22f).Within(0.04f));
            BodyMetrics b = BodyMetrics.For(BodyBuild.B);
            Assert.That(b.HeadH / b.HeightM, Is.EqualTo(1f / 3.9f).Within(0.01f), "3.9 heads tall");
            Assert.That(b.ShoulderW, Is.EqualTo(0.42f).Within(1e-4f));
            Assert.That(b.HipW, Is.EqualTo(0.34f).Within(1e-4f));
            BodyMetrics soft = BodyMetrics.For(BodyBuild.B, AgeGroup.Adult, 0, 1);
            Assert.That(soft.ShoulderW, Is.LessThan(b.ShoulderW));
            Assert.That(soft.HipW, Is.GreaterThan(b.HipW));
        }

        private static float MaxY(CharacterRecipe r, HeadwearMode mode)
        {
            var m = new MeshData();
            HumanoidMesher.Build(r, 0, m, null, mode);
            m.GetBounds(out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ);
            return maxY;
        }

        /// <summary>Topi vertices: the ones bound to head_attach above <paramref name="aboveY"/>.</summary>
        private static List<int> TopiVertices(MeshData m, SkinWeights w, float aboveY)
        {
            var list = new List<int>();
            for (int v = 0; v < m.VertexCount; v++)
                if (w.Bone0[v] == (byte)Bone.HeadAttach && m.Positions[v * 3 + 1] > aboveY) list.Add(v);
            return list;
        }

        [Test]
        public void TheTopiIsTallerAtTheFrontTiltedLeftAndWovenInSeveralColours()
        {
            Assert.That(HumanoidMesher.TopiFrontM / HumanoidMesher.TopiBackM, Is.EqualTo(1.43f).Within(0.08f), "3 : 2 front to back");
            Assert.That(HumanoidMesher.TopiTiltDeg, Is.EqualTo(6f));
            var r = new CharacterRecipe { Hair = 2 };
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi, 0, 0);
            var m = new MeshData();
            var w = new SkinWeights();
            HumanoidMesher.Build(r, 0, m, w);
            HumanoidMesher.HeadShape(BodyMetrics.For(r), out V3 hc, out V3 hr);
            List<int> topi = TopiVertices(m, w, hc.Y);
            Assert.That(topi.Count, Is.GreaterThan(400));
            float front = 0f, back = 0f, right = 0f, left = 0f;
            var colours = new HashSet<uint>();
            foreach (int v in topi)
            {
                float x = m.Positions[v * 3], y = m.Positions[v * 3 + 1], z = m.Positions[v * 3 + 2];
                if (z > hc.Z + 0.08f) front = Math.Max(front, y);
                if (z < hc.Z - 0.08f) back = Math.Max(back, y);
                if (x > 0.08f) right = Math.Max(right, y);
                if (x < -0.08f) left = Math.Max(left, y);
                colours.Add((uint)(m.Colors[v * 4] << 16 | m.Colors[v * 4 + 1] << 8 | m.Colors[v * 4 + 2]));
            }
            Assert.That(front, Is.GreaterThan(back + 0.03f), "the front stands higher (the mountain peak)");
            Assert.That(right, Is.GreaterThan(left), "tilted toward the left forehead");
            Assert.That(front, Is.GreaterThan(hc.Y + hr.Y + 0.02f), "the cap stands above the crown");
            Assert.That(colours.Count, Is.GreaterThanOrEqualTo(5), "ground, lining and the weave's motif colours");
            // The Bhadgaunle (kalo) topi is black.
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.BhadgaunleTopi);
            var k = new MeshData();
            var kw = new SkinWeights();
            HumanoidMesher.Build(r, 0, k, kw);
            foreach (int v in TopiVertices(k, kw, hc.Y + 0.1f))
                Assert.That(k.Colors[v * 4] + k.Colors[v * 4 + 1] + k.Colors[v * 4 + 2], Is.LessThan(3 * 70));
        }

        [Test]
        public void EveryDhakaWeaveUsesItsMotifColours()
        {
            for (int weave = 0; weave < CharacterPalette.DhakaWeaves; weave++)
            {
                var seen = new bool[5];
                for (int col = 0; col < 48; col++)
                    for (int row = 0; row < 10; row++) seen[HumanoidMesher.DhakaCell(weave, col, row)] = true;
                for (int c = 0; c < 5; c++) Assert.That(seen[c], Is.True, "weave " + weave + " cell " + c);
            }
        }

        [Test]
        public void HandsHaveAThumbAndFourFingersAtLod0AndMittensAtLod1()
        {
            var r = new CharacterRecipe { Seed = 2u };
            int Count(int lod, Bone b)
            {
                var m = new MeshData();
                var w = new SkinWeights();
                HumanoidMesher.Build(r, lod, m, w);
                int n = 0;
                for (int v = 0; v < w.Count; v++)
                    if (w.Bone0[v] == (byte)b) n++;
                return n;
            }
            Assert.That(HumanoidMesher.FingersLod0, Is.EqualTo(5));
            foreach (Bone b in new[] { Bone.FingerL2, Bone.FingerR2, Bone.ThumbL2, Bone.ThumbR2, Bone.FingerL1, Bone.ThumbR1 })
                Assert.That(Count(0, b), Is.GreaterThan(10), b + " at LOD0");
            Assert.That(Count(1, Bone.FingerL2), Is.EqualTo(0), "LOD1 is a mitten");
            Assert.That(Count(1, Bone.ThumbL1), Is.GreaterThan(0), "with a thumb");
        }

        [Test]
        public void EveryFaceStateHasTheSameTopology()
        {
            var r = new CharacterRecipe { Seed = 9u, Figure = 1 };
            MeshData basis = null;
            foreach (FaceExpression e in Enum.GetValues(typeof(FaceExpression)))
            {
                foreach (float blink in new[] { 0f, 1f })
                {
                    var o = CharacterMeshOptions.For(HeadwearMode.Outfit);
                    o.Face = new FaceState(e, blink, 0.5f, -0.3f);
                    var m = new MeshData();
                    HumanoidMesher.Build(r, 0, m, null, o);
                    if (basis == null)
                    {
                        basis = m;
                        continue;
                    }
                    Assert.That(m.VertexCount, Is.EqualTo(basis.VertexCount), e + " blink " + blink);
                    Assert.That(m.IndexCount, Is.EqualTo(basis.IndexCount));
                    for (int i = 0; i < m.IndexCount; i++)
                        if (m.Indices[i] != basis.Indices[i]) Assert.Fail(e + " blink " + blink + ": index " + i + " differs from the basis");
                }
            }
            // Joy opens the mouth: some vertices move.
            var joy = CharacterMeshOptions.For(HeadwearMode.Outfit);
            joy.Face = new FaceState(FaceExpression.Joy);
            var j = new MeshData();
            HumanoidMesher.Build(r, 0, j, null, joy);
            int moved = 0;
            for (int i = 0; i < j.VertexCount * 3; i++)
                if (Math.Abs(j.Positions[i] - basis.Positions[i]) > 1e-4f) moved++;
            Assert.That(moved, Is.GreaterThan(50));
        }

        [Test]
        public void TheDauraHasEightTiesAndFivePleats()
        {
            Assert.That(HumanoidMesher.DauraTies, Is.EqualTo(8));
            Assert.That(HumanoidMesher.DauraPleats, Is.EqualTo(5));
            var r = CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, 0, 1u);
            Assert.That(r[OutfitSlotKind.Torso].Item, Is.EqualTo(OutfitItem.Daura));
            Assert.That(r[OutfitSlotKind.Legs].Item, Is.EqualTo(OutfitItem.Suruwal));
            Assert.That(r[OutfitSlotKind.Head].Item, Is.AnyOf(OutfitItem.DhakaTopi, OutfitItem.BhadgaunleTopi));
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
            for (int i = 0; i < a.VertexCount * 2; i++) Assert.That(a.Uv0[i], Is.EqualTo(b.Uv0[i]));
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
                foreach (StreetStyle style in Enum.GetValues(typeof(StreetStyle)))
                {
                    for (int tint = 0; tint < 3; tint++)
                    {
                        CharacterRecipe r = CharacterRecipe.ForPedestrian(a, tint, (uint)(tint * 31 + (int)a), style, tint % 6);
                        for (int lod = 0; lod <= 2; lod++)
                        {
                            var m = new MeshData();
                            HumanoidMesher.BuildStatic(r, lod, m);
                            Assert.That(m.TriangleCount, Is.InRange(250, HumanoidMesher.Budget(lod)), a + " " + style + " lod " + lod);
                        }
                    }
                }
            }
            CharacterRecipe monk = CharacterRecipe.ForPedestrian(PedArchetype.Monk, 0, 1u);
            Assert.That(monk[OutfitSlotKind.Torso].Item, Is.EqualTo(OutfitItem.Robe));
            Assert.That(monk.Hair, Is.EqualTo(CharacterRecipe.HairShaved));
            CharacterRecipe porter = CharacterRecipe.ForPedestrian(PedArchetype.Porter, 0, 1u);
            Assert.That(porter[OutfitSlotKind.Back].Item, Is.EqualTo(OutfitItem.Doko));
            CharacterRecipe sadhu = CharacterRecipe.ForPedestrian(PedArchetype.Sadhu, 0, 1u);
            Assert.That(sadhu.Hair, Is.EqualTo(CharacterRecipe.HairDreadlocks));
            Assert.That(sadhu.Has(CharacterAccents.Tilak), Is.True);
            CharacterRecipe police = CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 0, 1u);
            Assert.That(police[OutfitSlotKind.Torso].Item, Is.EqualTo(OutfitItem.PoliceUniform));
            Assert.That(police.Has(CharacterAccents.Gloves), Is.True);
            CharacterRecipe haku = CharacterRecipe.ForPedestrian(PedArchetype.Hakupatasi, 0, 1u, StreetStyle.NewarTown, 0);
            Assert.That(haku[OutfitSlotKind.Torso].Item, Is.EqualTo(OutfitItem.Sari));
            Assert.That(haku[OutfitSlotKind.Torso].Colour, Is.EqualTo(CharacterPalette.HakuSari));
            CharacterRecipe umbrella = CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 0, 1u, StreetStyle.Urban, 5);
            Assert.That(umbrella.Has(CharacterAccents.Umbrella), Is.True);
            CharacterRecipe baby = CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, 0, 1u, StreetStyle.Village, 4);
            Assert.That(baby[OutfitSlotKind.Back].Item, Is.EqualTo(OutfitItem.BabySling));
        }

        [Test]
        public void TheWardrobeFollowsThePlace()
        {
            int kaloNewar = 0, kaloUrban = 0, chuba = 0, trek = 0, office = 0, officeShoes = 0;
            for (uint s = 0; s < 400; s++)
            {
                if (CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, (int)s, s, StreetStyle.NewarTown, 0)[OutfitSlotKind.Head].Item == OutfitItem.BhadgaunleTopi) kaloNewar++;
                if (CharacterRecipe.ForPedestrian(PedArchetype.DauraSuruwal, (int)s, s, StreetStyle.Urban, 0)[OutfitSlotKind.Head].Item == OutfitItem.BhadgaunleTopi) kaloUrban++;
                if (CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, (int)s, s, StreetStyle.Buddhist, 0)[OutfitSlotKind.Torso].Item == OutfitItem.Chuba) chuba++;
                CharacterRecipe t = CharacterRecipe.ForPedestrian(PedArchetype.Tourist, (int)s, s, StreetStyle.Tourist, 0);
                if (t[OutfitSlotKind.Legs].Item == OutfitItem.TrekTrousers || t[OutfitSlotKind.Feet].Item == OutfitItem.TrekBoots) trek++;
                CharacterRecipe o = CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, (int)s, s, StreetStyle.Office, 0);
                OutfitItem top = o[OutfitSlotKind.Torso].Item;
                if (top == OutfitItem.Shirt || top == OutfitItem.Blazer || top == OutfitItem.Kurta || top == OutfitItem.Sari) office++;
                if (o[OutfitSlotKind.Feet].Item == OutfitItem.LeatherShoes) officeShoes++;
            }
            Assert.That(kaloNewar, Is.GreaterThan(kaloUrban * 2), "the black Bhadgaunle topi is the Bhaktapur cap");
            Assert.That(chuba, Is.GreaterThan(100), "chuba at Boudha and Swayambhu");
            Assert.That(trek, Is.GreaterThan(250), "trekking gear in Thamel");
            Assert.That(office, Is.GreaterThan(200), "office wear in the office districts");
            Assert.That(officeShoes, Is.GreaterThan(120));
        }

        [Test]
        public void NoBeardsOnTheYoungAndRecipesComeBackValid()
        {
            for (uint s = 0; s < 300; s++)
            {
                foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
                {
                    CharacterRecipe r = CharacterRecipe.ForPedestrian(a, (int)s, s, (StreetStyle)(s % 8), (int)(s % 6));
                    if (r.Age == AgeGroup.Child || r.Age == AgeGroup.Teen || r.Figure != 0) Assert.That(r.Beard, Is.EqualTo(FacialHair.None));
                    Assert.That(r.Clone().Validate().Equals(r), Is.True, "ForPedestrian returns valid recipes");
                }
            }
        }

        [Test]
        public void TheTintMaskReplacesOnlyTheMainGarment()
        {
            CharacterRecipe r = CharacterRecipe.ForPedestrian(PedArchetype.UrbanCasual, 3, 7u);
            var o = CharacterMeshOptions.For(HeadwearMode.Outfit);
            o.TintMask = true;
            var m = new MeshData();
            HumanoidMesher.Build(r, 2, m, null, o);
            int tinted = 0, plain = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                byte a = m.Colors[v * 4 + 3];
                Assert.That(a == 0 || a == 255, Is.True);
                if (a == 255)
                {
                    tinted++;
                    Assert.That(m.Colors[v * 4], Is.EqualTo(m.Colors[v * 4 + 1]), "tinted vertices are grey");
                }
                else plain++;
            }
            Assert.That(tinted, Is.GreaterThan(30));
            Assert.That(plain, Is.GreaterThan(tinted / 4));
            Assert.That(BodyKit.ShadeOf(0x808080u, 0xFFFFFFu), Is.EqualTo(128f / 255f).Within(0.01f));
            Assert.That(BodyKit.ShadeOf(0xFF0000u, 0x00FF00u), Is.EqualTo(-1f));
        }

        [Test]
        public void AppearanceRoundTripsThroughTheSaveAndClampsBadValues()
        {
            CharacterRecipe r = CharacterRecipe.NewPlayer(7u);
            r.Build = BodyBuild.C;
            r.Figure = 1;
            r.Age = AgeGroup.Elder;
            r.HeightPct = -3;
            r.Beard = FacialHair.ShortBeard;
            r.EyeColour = 2;
            r.Accents = CharacterAccents.Glasses | CharacterAccents.Shawl | CharacterAccents.NoseStud;
            r[OutfitSlotKind.Torso] = new OutfitSlot(OutfitItem.Daura, 2, 1);
            r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Suruwal, 1);
            r.Name = "Asha";
            JsonObject o = r.ToJson();
            string text = o.ToString();
            CharacterRecipe back = CharacterRecipe.FromJson(Json.Parse(text).AsObject());
            Assert.That(back, Is.EqualTo(r));
            Assert.That(text.Length, Is.LessThan(520));
            // A version 1 save (no detail-pass keys) reads with the defaults.
            var v1 = new JsonObject().Set("version", 1L).Set("build", 2L).Set("skin", 4L).Set("hair", 3L);
            CharacterRecipe old = CharacterRecipe.FromJson(v1);
            Assert.That(old.Age, Is.EqualTo(AgeGroup.Adult));
            Assert.That(old.Figure, Is.EqualTo(0));
            Assert.That(old.Beard, Is.EqualTo(FacialHair.None));

            var bad = new JsonObject().Set("build", 9L).Set("skin", 0L).Set("hair", 99L).Set("hairColour", 300L).Set("figure", 7L).Set("age", 9L)
                .Set("height", 40L).Set("beard", 50L).Set("accents", 1L << 20)
                .Set("outfit", new JsonArray().Add(new JsonObject().Set("item", (long)OutfitItem.Jeans)));
            CharacterRecipe fixedUp = CharacterRecipe.FromJson(bad);
            Assert.That(fixedUp.Build, Is.EqualTo(BodyBuild.B));
            Assert.That(fixedUp.Skin, Is.InRange(1, 10));
            Assert.That(fixedUp.Hair, Is.InRange(1, CharacterRecipe.HairStyleCount));
            Assert.That(fixedUp.Figure, Is.LessThanOrEqualTo(1));
            Assert.That(fixedUp.Age, Is.EqualTo(AgeGroup.Adult));
            Assert.That(fixedUp.HeightPct, Is.EqualTo(0));
            Assert.That(fixedUp.Beard, Is.EqualTo(FacialHair.None));
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
            var child = new HumanoidSkeleton(new CharacterRecipe { Age = AgeGroup.Child });
            Assert.That(child[Bone.Head].Y, Is.LessThan(sk[Bone.Head].Y - 0.2f));
            Assert.That(new CharacterRecipe { Figure = 1 }.SameSkeleton(new CharacterRecipe()), Is.False);
        }
    }
}
