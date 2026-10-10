using System;
using System.IO;
using System.IO.Compression;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth.Textures;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The procedural material textures of the cartoon look (World/README.md "Look"): determinism, seamless tiling,
    /// mip chains, the tint encoding the shader relies on, and the per-channel look table. Set the environment variable
    /// <c>GHUMANTE_SWATCH_DIR</c> to also write every texture as PNG (one tile and a 2 × 2 repeat) for a visual check.
    /// </summary>
    public class SynthTextureTests
    {
        private static MaterialChannel[] AllChannels()
        {
            var all = new MaterialChannel[MaterialTextures.SliceCount];
            for (int i = 0; i < all.Length; i++) all[i] = (MaterialChannel)i;
            return all;
        }

        [Test]
        public void EveryChannelHasASliceAndALook()
        {
            int enumCount = Enum.GetValues(typeof(MaterialChannel)).Length;
            Assert.That(MaterialTextures.SliceCount, Is.EqualTo(enumCount), "one texture slice per MaterialChannel");
            Assert.That(MaterialLooks.Count, Is.EqualTo((int)MaterialChannel.Marking + 1));
            Assert.That(MaterialLooks.ArraySize, Is.GreaterThanOrEqualTo(MaterialLooks.Count));
            foreach (MaterialChannel c in AllChannels())
            {
                MaterialLook l = MaterialLooks.Of(c);
                Assert.That(l.TileM, Is.InRange(0.05f, 10f), c + " tile");
                Assert.That(l.Gloss, Is.GreaterThanOrEqualTo(1f), c + " gloss");
                Assert.That(l.Specular, Is.InRange(0f, 1f), c + " specular");
                Assert.That(l.Sparkle, Is.InRange(0f, 1f), c + " sparkle");
                Assert.That(l.Metallic, Is.InRange(0f, 1f), c + " metallic");
                Assert.That(l.Reflect, Is.InRange(0f, 1f), c + " reflect");
                Assert.That(l.Macro, Is.InRange(0f, 1f), c + " macro");
            }
            Assert.That(MaterialLooks.Of((MaterialChannel)200).TileM, Is.EqualTo(MaterialLooks.Of(MaterialChannel.Plain).TileM));
        }

        [Test]
        public void ShaderArraysCarryTheTable()
        {
            var a = new float[MaterialLooks.ArraySize * 4];
            var b = new float[MaterialLooks.ArraySize * 4];
            MaterialLooks.FillShaderArrays(a, b);
            MaterialLook brick = MaterialLooks.Of(MaterialChannel.Brick), water = MaterialLooks.Of(MaterialChannel.Water);
            int bi = (int)MaterialChannel.Brick * 4, wi = (int)MaterialChannel.Water * 4;
            Assert.That(a[bi], Is.EqualTo(1f / brick.TileM).Within(1e-6f));
            Assert.That(a[bi + 2], Is.EqualTo(brick.Gloss));
            Assert.That(b[wi + 3], Is.EqualTo(water.Flow));
            Assert.That(b[(int)MaterialChannel.Gilt * 4], Is.EqualTo(1f), "gilt highlights take the gold colour");
            // Unused entries repeat Plain.
            int last = (MaterialLooks.ArraySize - 1) * 4;
            Assert.That(a[last], Is.EqualTo(a[0]));
            Assert.Throws<ArgumentException>(() => MaterialLooks.FillShaderArrays(new float[8], b));
        }

        [Test]
        public void SeedsComeFromFnv1aOfTheChannelName()
        {
            string key = "ghumante.material.v" + MaterialTextures.Version + ".Brick";
            var bytes = new byte[key.Length];
            for (int i = 0; i < key.Length; i++) bytes[i] = (byte)key[i];
            Assert.That(MaterialTextures.SeedOf(MaterialChannel.Brick), Is.EqualTo(Hashes.Fnv1a32(bytes, 0, bytes.Length)));
            var seen = new System.Collections.Generic.HashSet<uint>();
            foreach (MaterialChannel c in AllChannels()) Assert.That(seen.Add(MaterialTextures.SeedOf(c)), c + " seed collides");
        }

        [Test]
        public void BakingIsDeterministicAndChannelsDiffer()
        {
            var hashes = new System.Collections.Generic.HashSet<uint>();
            foreach (MaterialChannel c in AllChannels())
            {
                byte[][] a = MaterialTextures.Bake(c, 32), b = MaterialTextures.Bake(c, 32);
                Assert.That(a.Length, Is.EqualTo(b.Length));
                for (int m = 0; m < a.Length; m++) Assert.That(a[m], Is.EqualTo(b[m]), c + " mip " + m);
                Assert.That(hashes.Add(Hashes.Crc32(a[0])), c + " looks like another channel");
            }
        }

        [Test]
        public void EveryChannelIsFiniteAndInRange()
        {
            const int n = 64;
            var px = new float[n * n * 4];
            foreach (MaterialChannel c in AllChannels())
            {
                Array.Clear(px, 0, px.Length);
                MaterialTextures.Render(c, n, px);
                for (int i = 0; i < px.Length; i++)
                {
                    Assert.That(float.IsNaN(px[i]) || float.IsInfinity(px[i]), Is.False, c + " NaN at " + i);
                    Assert.That(px[i], Is.InRange(0f, 1f), c + " out of range at " + i);
                }
            }
        }

        [Test]
        public void TintedTexelsKeepTheVertexColourOnAverage()
        {
            // albedo = tint × rgb × 2 where alpha = 1: the mean modulation must stay near 1 so the meshers' palette
            // colours still read (a texture can enrich a wall, never repaint it).
            const int n = 64;
            var px = new float[n * n * 4];
            foreach (MaterialChannel c in AllChannels())
            {
                MaterialTextures.Render(c, n, px);
                double sum = 0;
                int count = 0, tinted = 0;
                for (int i = 0; i < n * n; i++)
                {
                    float a = px[i * 4 + 3];
                    if (a > 0.99f)
                    {
                        sum += (px[i * 4] + px[i * 4 + 1] + px[i * 4 + 2]) / 3f * 2f;
                        count++;
                    }
                    if (a > 0.5f) tinted++;
                }
                Assert.That(tinted, Is.GreaterThan(n * n / 2), c + ": most texels follow the vertex colour");
                double mean = sum / Math.Max(1, count);
                Assert.That(mean, Is.InRange(0.7, 1.25), c + " mean modulation");
            }
        }

        [Test]
        public void TexturesTileSeamlessly()
        {
            // A texture tiles when it is periodic: the window shifted by half a tile (crossing the wrap in both
            // directions) equals the tile rolled by half a tile.
            const int n = 64, h = n / 2;
            var tile = new float[n * n * 4];
            var shifted = new float[n * n * 4];
            foreach (MaterialChannel c in AllChannels())
            {
                MaterialTextures.Render(c, n, tile, 0, 0);
                MaterialTextures.Render(c, n, shifted, h, h);
                double worst = 0;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int a = (y * n + x) * 4, b = (((y + h) % n) * n + (x + h) % n) * 4;
                    for (int k = 0; k < 4; k++) worst = Math.Max(worst, Math.Abs(shifted[a + k] - tile[b + k]));
                }
                Assert.That(worst, Is.LessThan(2e-3), c + " is not periodic over the tile");
            }
        }

        [Test]
        public void SmallerSizesShowTheSamePattern()
        {
            // The pattern is a function of (u, v), not of the texel grid: a 64 px bake matches the 128 px bake
            // box-filtered to 64 px (Low uses 128, Mid and High 256).
            var big = new float[128 * 128 * 4];
            var small = new float[64 * 64 * 4];
            foreach (MaterialChannel c in new[] { MaterialChannel.Brick, MaterialChannel.Asphalt, MaterialChannel.Grass, MaterialChannel.RoofTile })
            {
                MaterialTextures.Render(c, 128, big);
                MaterialTextures.Render(c, 64, small);
                float[] half = MaterialTextures.Downsample(big, 128);
                double err = 0;
                for (int i = 0; i < small.Length; i++) err += Math.Abs(small[i] - half[i]);
                err /= small.Length;
                Assert.That(err, Is.LessThan(0.06), c + " differs between sizes");
            }
        }

        [Test]
        public void MipChainsHalveDownToOneTexel()
        {
            Assert.That(MaterialTextures.MipCount(256), Is.EqualTo(9));
            Assert.That(MaterialTextures.MipCount(128), Is.EqualTo(8));
            Assert.That(MaterialTextures.IsValidSize(100), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => MaterialTextures.Bake(MaterialChannel.Brick, 100));
            byte[][] mips = MaterialTextures.Bake(MaterialChannel.Flagstone, 64);
            Assert.That(mips.Length, Is.EqualTo(7));
            for (int m = 0, s = 64; m < mips.Length; m++, s >>= 1) Assert.That(mips[m].Length, Is.EqualTo(s * s * 4), "mip " + m);
            // The 1 × 1 mip is the linear mean of the top level.
            var top = new float[64 * 64 * 4];
            MaterialTextures.Render(MaterialChannel.Flagstone, 64, top);
            double mean = 0;
            for (int i = 0; i < 64 * 64; i++) mean += top[i * 4];
            mean /= 64 * 64;
            Assert.That(MaterialTextures.SrgbToLinear(mips[6][0]), Is.EqualTo(mean).Within(0.01));
        }

        [Test]
        public void SrgbEncodingRoundTrips()
        {
            var px = new float[] { 0f, 0.5f, 0.214f, 1f };
            var b = new byte[4];
            MaterialTextures.EncodeSrgb(px, 1, b);
            Assert.That(b[0], Is.EqualTo(0));
            Assert.That(b[1], Is.EqualTo(188), "linear 0.5 (the neutral tint) is sRGB 188");
            Assert.That(MaterialTextures.SrgbToLinear(b[2]), Is.EqualTo(0.214f).Within(0.004f));
            Assert.That(b[3], Is.EqualTo(255), "alpha stays linear");
        }

        [Test]
        public void WritesSwatchesWhenAsked()
        {
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_SWATCH_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("set GHUMANTE_SWATCH_DIR to write the material swatches");
            Directory.CreateDirectory(dir);
            const int n = MaterialTextures.DefaultSize;
            foreach (MaterialChannel c in AllChannels())
            {
                byte[] top = MaterialTextures.Bake(c, n)[0];
                WritePng(Path.Combine(dir, (int)c + "_" + c + ".png"), top, n, n, 1);
                WritePng(Path.Combine(dir, (int)c + "_" + c + "_2x2.png"), top, n, n, 2);
            }
        }

        private static float Lum(float[] px, int n, int x, int y)
        {
            int i = (y * n + x) * 4;
            return 0.3f * px[i] + 0.59f * px[i + 1] + 0.11f * px[i + 2];
        }

        /// <summary>Writes RGBA8 rows (bottom-up) as an RGBA PNG, top row first, repeated <paramref name="repeat"/> times
        /// in both directions.</summary>
        internal static void WritePng(string path, byte[] rgba, int w, int h, int repeat)
        {
            int ow = w * repeat, oh = h * repeat;
            var raw = new byte[oh * (ow * 4 + 1)];
            int o = 0;
            for (int y = oh - 1; y >= 0; y--)
            {
                raw[o++] = 0;
                int sy = y % h;
                for (int x = 0; x < ow; x++)
                {
                    int i = (sy * w + x % w) * 4;
                    raw[o++] = rgba[i];
                    raw[o++] = rgba[i + 1];
                    raw[o++] = rgba[i + 2];
                    raw[o++] = rgba[i + 3];
                }
            }
            using (var fs = File.Create(path))
            {
                fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                BigEndian(ihdr, 0, (uint)ow);
                BigEndian(ihdr, 4, (uint)oh);
                ihdr[8] = 8;
                ihdr[9] = 6;
                Chunk(fs, "IHDR", ihdr);
                using (var ms = new MemoryStream())
                {
                    using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
                    Chunk(fs, "IDAT", ms.ToArray());
                }
                Chunk(fs, "IEND", new byte[0]);
            }
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BigEndian(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var body = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
            Array.Copy(data, 0, body, 4, data.Length);
            s.Write(body, 0, body.Length);
            var crc = new byte[4];
            BigEndian(crc, 0, Hashes.Crc32(body));
            s.Write(crc, 0, 4);
        }

        private static void BigEndian(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24);
            b[at + 1] = (byte)(v >> 16);
            b[at + 2] = (byte)(v >> 8);
            b[at + 3] = (byte)v;
        }
    }
}
