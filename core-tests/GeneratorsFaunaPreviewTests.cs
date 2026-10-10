using System;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Preview dumps of the fauna (visual self-check, docs/W2_DETAIL_CONTRACT.md §6): every species at every level,
    /// the posed clips and a pigeon flock, as OBJ files under <c>GHUMANTE_PREVIEW_DIR/animals</c>. Writes nothing
    /// unless the variable is set.
    /// </summary>
    [TestFixture]
    public class GeneratorsFaunaPreviewTests
    {
        private static string Dir
        {
            get { return ObjDump.Dir; }
        }

        [Test]
        public void DumpSpecies()
        {
            if (Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            var tint = new MeshData(4096, 12288);
            for (int s = 0; s < FaunaCatalog.SpeciesCount; s++)
            {
                var sp = (FaunaSpecies)s;
                for (int lod = 0; lod < FaunaMesher.LodCount; lod++)
                {
                    CoatPattern[] patterns = FaunaPalette.Patterns(sp);
                    for (int p = 0; p < (lod == 0 ? patterns.Length : 1); p++)
                    {
                        FaunaMesh m = FaunaMesher.Create(sp, lod, patterns[p]);
                        if (m.VertexCount == 0) continue;
                        FaunaPalette.CoatFor(sp, DemoTint(sp, patterns[p]), out _, out uint rgb);
                        ApplyTint(Pose(m, FaunaClip.Stand, 0.5f), rgb, tint);
                        Write(tint, "species/" + sp.ToString().ToLowerInvariant() + "_lod" + lod + "_" + patterns[p].ToString().ToLowerInvariant() + ".obj");
                    }
                }
            }
        }

        /// <summary>Every species in its typical clips (cow grazing, lying; dog asleep, scratching; monkeys climbing...).</summary>
        [Test]
        public void DumpClips()
        {
            if (Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            var tint = new MeshData(4096, 12288);
            (FaunaSpecies, CoatPattern, FaunaClip, float)[] list =
            {
                (FaunaSpecies.Cow, CoatPattern.Plain, FaunaClip.Walk, 0.3f), (FaunaSpecies.Cow, CoatPattern.Plain, FaunaClip.Graze, 1f),
                (FaunaSpecies.Cow, CoatPattern.Patched, FaunaClip.Lie, 1f), (FaunaSpecies.Bull, CoatPattern.Saddle, FaunaClip.Trot, 0.2f),
                (FaunaSpecies.Dog, CoatPattern.Plain, FaunaClip.Sleep, 1f), (FaunaSpecies.Dog, CoatPattern.Saddle, FaunaClip.Trot, 0.15f),
                (FaunaSpecies.Dog, CoatPattern.Socks, FaunaClip.Sit, 1f), (FaunaSpecies.Dog, CoatPattern.Plain, FaunaClip.Scratch, 0.05f),
                (FaunaSpecies.Dog, CoatPattern.Patched, FaunaClip.Lie, 1f), (FaunaSpecies.Goat, CoatPattern.Plain, FaunaClip.Graze, 1f),
                (FaunaSpecies.Goat, CoatPattern.Plain, FaunaClip.Lie, 1f), (FaunaSpecies.Buffalo, CoatPattern.Plain, FaunaClip.Lie, 1f),
                (FaunaSpecies.Macaque, CoatPattern.Plain, FaunaClip.Sit, 1f), (FaunaSpecies.Macaque, CoatPattern.Plain, FaunaClip.Walk, 0.4f),
                (FaunaSpecies.Macaque, CoatPattern.Plain, FaunaClip.Climb, 0.3f), (FaunaSpecies.Macaque, CoatPattern.Plain, FaunaClip.Groom, 1f),
                (FaunaSpecies.MacaqueBaby, CoatPattern.Plain, FaunaClip.Sit, 1f), (FaunaSpecies.Pigeon, CoatPattern.Plain, FaunaClip.Peck, 0.1f),
                (FaunaSpecies.Pigeon, CoatPattern.Plain, FaunaClip.Flap, 0.05f), (FaunaSpecies.Pigeon, CoatPattern.Plain, FaunaClip.Glide, 0f),
                (FaunaSpecies.BlackKite, CoatPattern.Plain, FaunaClip.Soar, 0f), (FaunaSpecies.Crow, CoatPattern.Plain, FaunaClip.Flap, 0.1f),
                (FaunaSpecies.Egret, CoatPattern.Plain, FaunaClip.Flap, 0.2f), (FaunaSpecies.Hen, CoatPattern.Plain, FaunaClip.Peck, 0.1f),
                (FaunaSpecies.Rooster, CoatPattern.Plain, FaunaClip.Walk, 0.2f), (FaunaSpecies.Duck, CoatPattern.Plain, FaunaClip.Walk, 0.2f),
                (FaunaSpecies.Swallow, CoatPattern.Plain, FaunaClip.Glide, 0f), (FaunaSpecies.Myna, CoatPattern.Plain, FaunaClip.Flap, 0.05f),
            };
            foreach ((FaunaSpecies sp, CoatPattern pat, FaunaClip clip, float t) in list)
            {
                FaunaMesh m = FaunaMesher.Create(sp, 0, pat);
                FaunaPalette.CoatFor(sp, DemoTint(sp, pat), out _, out uint rgb);
                ApplyTint(Pose(m, clip, t), rgb, tint);
                Write(tint, "clips/" + sp.ToString().ToLowerInvariant() + "_" + clip.ToString().ToLowerInvariant() + ".obj");
            }
        }

        /// <summary>Animals posed and tinted like the reference photos (docs/research/w2/ref_animals.md).</summary>
        [Test]
        public void DumpCompare()
        {
            if (Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            var tint = new MeshData(4096, 12288);
            (string, FaunaSpecies, CoatPattern, uint, FaunaClip, float)[] list =
            {
                ("cow_brown_stand", FaunaSpecies.Cow, CoatPattern.Plain, 0x7C4819u, FaunaClip.Stand, 0.2f),
                ("cow_fawn_lie", FaunaSpecies.Cow, CoatPattern.Plain, 0xC2AC85u, FaunaClip.Lie, 1f),
                ("cow_white_lie", FaunaSpecies.Cow, CoatPattern.Plain, 0xEDE7DAu, FaunaClip.Lie, 2f),
                ("bull_white_stand", FaunaSpecies.Bull, CoatPattern.Saddle, 0xEDE7DAu, FaunaClip.Stand, 0.2f),
                ("buffalo_walk", FaunaSpecies.Buffalo, CoatPattern.Plain, 0x2E2C2Cu, FaunaClip.Walk, 0.3f),
                ("dog_ginger_lie", FaunaSpecies.Dog, CoatPattern.Plain, 0xB8702Fu, FaunaClip.Lie, 1f),
                ("dog_ginger_sit", FaunaSpecies.Dog, CoatPattern.Plain, 0xB8702Fu, FaunaClip.Sit, 1f),
                ("dog_black_sit", FaunaSpecies.Dog, CoatPattern.Socks, 0x2A2A2Au, FaunaClip.Sit, 2f),
                ("dog_tricolour_sleep", FaunaSpecies.Dog, CoatPattern.Saddle, 0xA8683Au, FaunaClip.Sleep, 1f),
                ("goat_black", FaunaSpecies.Goat, CoatPattern.Plain, 0x2A2624u, FaunaClip.Stand, 0.3f),
                ("macaque_sit", FaunaSpecies.Macaque, CoatPattern.Plain, 0x927963u, FaunaClip.Sit, 1f),
                ("pigeon_stand", FaunaSpecies.Pigeon, CoatPattern.Plain, BirdMesher.PigeonBaseTint, FaunaClip.Stand, 0.4f),
                ("crow_stand", FaunaSpecies.Crow, CoatPattern.Plain, 0xFFFFFFu, FaunaClip.Stand, 0.4f),
                ("kite_soar", FaunaSpecies.BlackKite, CoatPattern.Plain, 0xFFFFFFu, FaunaClip.Soar, 0f),
                ("myna_stand", FaunaSpecies.Myna, CoatPattern.Plain, 0xFFFFFFu, FaunaClip.Stand, 0.4f),
                ("sparrow_stand", FaunaSpecies.Sparrow, CoatPattern.Plain, 0xFFFFFFu, FaunaClip.Stand, 0.4f),
                ("egret_stand", FaunaSpecies.Egret, CoatPattern.Saddle, 0xFFFFFFu, FaunaClip.Stand, 0.4f),
                ("rooster_stand", FaunaSpecies.Rooster, CoatPattern.Plain, 0xB0552Au, FaunaClip.Stand, 0.4f),
                ("hen_stand", FaunaSpecies.Hen, CoatPattern.Plain, 0x9C5A2Eu, FaunaClip.Stand, 0.4f),
                ("duck_stand", FaunaSpecies.Duck, CoatPattern.Plain, 0xF4F2ECu, FaunaClip.Stand, 0.4f),
                ("swallow_stand", FaunaSpecies.Swallow, CoatPattern.Plain, 0xFFFFFFu, FaunaClip.Stand, 0.4f),
            };
            foreach ((string name, FaunaSpecies sp, CoatPattern pat, uint rgb, FaunaClip clip, float t) in list)
            {
                FaunaMesh m = FaunaMesher.Create(sp, 0, pat);
                ApplyTint(Pose(m, clip, t), rgb, tint);
                Write(tint, "compare/" + name + ".obj");
            }
        }

        /// <summary>Every species side by side at true size (LOD0, typical coat, standing), plus their LOD1 and LOD2.</summary>
        [Test]
        public void DumpLineup()
        {
            if (Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            for (int lod = 0; lod < 3; lod++)
            {
                var all = new MeshData(65536, 196608);
                var tint = new MeshData(4096, 12288);
                float x = 0f;
                for (int s = 0; s < FaunaCatalog.SpeciesCount; s++)
                {
                    var sp = (FaunaSpecies)s;
                    CoatPattern pat = FaunaPalette.Patterns(sp)[0];
                    FaunaMesh m = FaunaMesher.Create(sp, lod, pat);
                    uint rgb = LineupTint(sp);
                    ApplyTint(Pose(m, FaunaClip.Stand, 0.3f), rgb, tint);
                    float w = Math.Max(0.25f, FaunaCatalog.Info(sp).IsAvian ? 0.35f : FaunaCatalog.Info(sp).LengthM * 0.55f);
                    x += w;
                    Append(all, tint, new Fv3(x, 0f, 0f), FRot.Euler(0f, -FMath.Pi * 0.5f, 0f));
                    x += w;
                }
                Write(all, "lineup_lod" + lod + ".obj");
            }
        }

        /// <summary>A pigeon flock on Basantapur: pecking in its disc, and two seconds after a burst.</summary>
        [Test]
        public void DumpPigeonFlock()
        {
            if (Dir == null) Assert.Ignore("GHUMANTE_PREVIEW_DIR not set");
            var sim = new FlockSim(FlockKind.Pigeons, 140, 0.0, 0.0, 0f, 0xBA5Au);
            for (int i = 0; i < 200; i++) sim.Step(0.05f, 100f, 100f, false);
            Write(FlockMesh(sim), "flock/pigeons_ground.obj");
            sim.Step(0.05f, 0f, 0f, true);
            for (int i = 0; i < 50; i++) sim.Step(0.05f, 100f, 100f, false);
            Write(FlockMesh(sim), "flock/pigeons_burst.obj");
            for (int i = 0; i < 120; i++) sim.Step(0.05f, 100f, 100f, false);
            Write(FlockMesh(sim), "flock/pigeons_circuit.obj");
        }

        private static MeshData FlockMesh(FlockSim sim)
        {
            var all = new MeshData(65536, 196608);
            var tint = new MeshData(4096, 12288);
            FaunaMesh lod0 = FaunaMesher.Create(sim.P.Species, 0), lod1 = FaunaMesher.Create(sim.P.Species, 1);
            for (int i = 0; i < sim.Count; i++)
            {
                FaunaMesh m = i % 3 == 0 ? lod0 : lod1;
                FaunaAnimator.Evaluate(m, sim.Clip[i], sim.ClipTime[i], 0f, (uint)i, PoseScratch);
                Skinner.Bake(m, PoseScratch, Posed);
                FaunaPalette.CoatFor(sim.P.Species, i * 7 + 3, out _, out uint rgb);
                ApplyTint(Posed, rgb, tint);
                FRot r = FRot.Euler(-sim.Pitch[i], sim.Heading[i], -sim.Bank[i]);
                Append(all, tint, new Fv3(sim.X[i], sim.Y[i], sim.Z[i]), r);
            }
            return all;
        }

        private static uint LineupTint(FaunaSpecies sp)
        {
            switch (sp)
            {
                case FaunaSpecies.Cow: return 0xEDE7DAu;
                case FaunaSpecies.Calf: return 0xC2AC85u;
                case FaunaSpecies.Bull: return 0xD8D2C6u;
                case FaunaSpecies.Buffalo: return 0x2E2C2Cu;
                case FaunaSpecies.Dog: return 0xB8702Fu;
                case FaunaSpecies.Goat: return 0x2A2624u;
                case FaunaSpecies.Macaque:
                case FaunaSpecies.MacaqueBaby: return 0x927963u;
                case FaunaSpecies.Hen: return 0x9C5A2Eu;
                case FaunaSpecies.Rooster: return 0xB0552Au;
                case FaunaSpecies.Duck: return 0xF4F2ECu;
                case FaunaSpecies.Pigeon: return BirdMesher.PigeonBaseTint;
                default: return 0xFFFFFFu;
            }
        }

        /// <summary>Appends <paramref name="src"/> rotated by <paramref name="r"/> and moved to <paramref name="at"/>.</summary>
        internal static void Append(MeshData dst, MeshData src, Fv3 at, FRot r)
        {
            int v0 = dst.VertexCount;
            dst.Reserve(src.VertexCount, src.IndexCount);
            for (int v = 0; v < src.VertexCount; v++)
            {
                int p = 3 * v, c = 4 * v;
                Fv3 q = r * new Fv3(src.Positions[p], src.Positions[p + 1], src.Positions[p + 2]) + at;
                Fv3 n = r * new Fv3(src.Normals[p], src.Normals[p + 1], src.Normals[p + 2]);
                uint col = ((uint)src.Colors[c] << 24) | ((uint)src.Colors[c + 1] << 16) | ((uint)src.Colors[c + 2] << 8) | 0xFFu;
                dst.AddVertex(q.X, q.Y, q.Z, n.X, n.Y, n.Z, col, src.Uv0[2 * v], src.Uv0[2 * v + 1]);
            }
            for (int i = 0; i < src.IndexCount; i += 3) dst.AddTriangle(v0 + src.Indices[i], v0 + src.Indices[i + 1], v0 + src.Indices[i + 2]);
        }

        private static readonly FaunaPose PoseScratch = new FaunaPose();
        private static readonly FaunaSkinner Skinner = new FaunaSkinner();
        private static readonly MeshData Posed = new MeshData(4096, 12288);

        /// <summary>The mesh posed in a clip at time t (seed 7).</summary>
        internal static MeshData Pose(FaunaMesh m, FaunaClip clip, float t)
        {
            FaunaAnimator.Evaluate(m, clip, t, 0f, 7u, PoseScratch);
            Skinner.Bake(m, PoseScratch, Posed);
            return Posed;
        }

        /// <summary>A tint index whose coat matches the pattern (for the previews).</summary>
        internal static int DemoTint(FaunaSpecies s, CoatPattern p)
        {
            for (int t = 0; t < 64; t++)
            {
                FaunaPalette.CoatFor(s, t, out CoatPattern q, out _);
                if (q == p) return t;
            }
            return 0;
        }

        /// <summary>Multiplies the tinted vertices (alpha 255) by <paramref name="rgb"/> as the instanced shader does.</summary>
        internal static void ApplyTint(MeshData src, uint rgb, MeshData dst)
        {
            dst.Clear();
            dst.Reserve(src.VertexCount, src.IndexCount);
            Array.Copy(src.Positions, dst.Positions, src.VertexCount * 3);
            Array.Copy(src.Normals, dst.Normals, src.VertexCount * 3);
            Array.Copy(src.Uv0, dst.Uv0, src.VertexCount * 2);
            Array.Copy(src.Indices, dst.Indices, src.IndexCount);
            dst.VertexCount = src.VertexCount;
            dst.IndexCount = src.IndexCount;
            dst.HasUv0 = src.HasUv0;
            float tr = ((rgb >> 16) & 0xFF) / 255f, tg = ((rgb >> 8) & 0xFF) / 255f, tb = (rgb & 0xFF) / 255f;
            for (int v = 0; v < src.VertexCount; v++)
            {
                int c = 4 * v;
                float a = src.Colors[c + 3] / 255f;
                dst.Colors[c] = (byte)(src.Colors[c] * (1f - a + a * tr));
                dst.Colors[c + 1] = (byte)(src.Colors[c + 1] * (1f - a + a * tg));
                dst.Colors[c + 2] = (byte)(src.Colors[c + 2] * (1f - a + a * tb));
                dst.Colors[c + 3] = 255;
            }
        }

        /// <summary>Writes an OBJ under <c>GHUMANTE_PREVIEW_DIR/animals</c> with <see cref="ObjDump"/>.</summary>
        internal static string Write(MeshData m, string rel)
        {
            return ObjDump.Write(m, "animals/" + rel);
        }
    }
}
