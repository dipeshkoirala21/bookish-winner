using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    // Engine-free helpers for the World and region tests (no UnityEngine here, so these tests also run under plain
    // `dotnet test` when compiled with the engine-free sources).

    /// <summary>The committed sample region, shared/sample-regions/kathmandu_core.</summary>
    internal static class SampleRegion
    {
        public const string Id = "kathmandu_core";

        // Thamel and Boudhanath in game metres (from the region's search index).
        public const double ThamelX = 529410.6, ThamelZ = 166505.3;
        public const double BoudhaX = 534266.4, BoudhaZ = 167087.2;

        private static PackReader _pack;

        /// <summary>The repository's shared/sample-regions folder: found by walking up from the working directory
        /// (Unity runs EditMode tests in game/) or from GHUMANTE_REPO.</summary>
        public static string SamplesFolder
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("GHUMANTE_REPO");
                if (!string.IsNullOrEmpty(env))
                {
                    string p = Path.Combine(env, "shared", "sample-regions");
                    if (Directory.Exists(p)) return p;
                }
                foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        string p = Path.Combine(dir.FullName, "shared", "sample-regions");
                        if (Directory.Exists(Path.Combine(p, Id))) return p;
                        dir = dir.Parent;
                    }
                }
                Assert.Ignore("shared/sample-regions not found (set GHUMANTE_REPO)");
                return null;
            }
        }

        public static string Folder
        {
            get { return Path.Combine(SamplesFolder, Id); }
        }

        public static string FilePath(string suffix)
        {
            return Path.Combine(Folder, Id + suffix);
        }

        /// <summary>A pack reader over the sample (opened once, kept for the test run).</summary>
        public static PackReader Pack
        {
            get
            {
                if (_pack == null) _pack = new PackReader(File.ReadAllBytes(FilePath(".ghpk")));
                return _pack;
            }
        }
    }

    /// <summary>An <see cref="ITileSink"/> that records views and advances a fake clock per upload.</summary>
    internal sealed class FakeTileSink : ITileSink
    {
        public readonly Dictionary<SelectedNode, bool> Views = new Dictionary<SelectedNode, bool>();
        public readonly HashSet<SelectedNode> Released = new HashSet<SelectedNode>();
        public double Now;
        public double MsPerUpload = 1.0;
        public int Uploads;
        public int UploadsThisTick;

        public double Clock()
        {
            return Now;
        }

        /// <summary>Uploads of this node throw (after creating its view), like a broken mesh would.</summary>
        public SelectedNode? ThrowFor;

        /// <summary>Bytes uploaded since the counter was last reset, and the largest chunk seen.</summary>
        public long BytesThisTick;

        public long LargestChunkBytes;

        public long LastChunkBytes;

        public void Upload(TileBuild build, int chunk)
        {
            Assert.That(chunk, Is.InRange(0, build.Chunks.Count - 1));
            UploadChunk c = build.Chunks[chunk];
            Assert.IsTrue(build.HasLayer(c.Layer), "only non-empty layers are uploaded");
            Assert.Greater(c.IndexCount, 0, "only non-empty chunks are uploaded");
            Assert.LessOrEqual(c.VertexCount, TileBuild.UploadChunkVertices);
            long bytes = build.ChunkBytes(chunk);
            BytesThisTick += bytes;
            LastChunkBytes = bytes;
            if (bytes > LargestChunkBytes) LargestChunkBytes = bytes;
            if (!Views.ContainsKey(build.Node)) Views[build.Node] = false;
            if (ThrowFor.HasValue && ThrowFor.Value == build.Node) throw new InvalidOperationException("broken mesh");
            Uploads++;
            UploadsThisTick++;
            Now += MsPerUpload;
        }

        public void SetVisible(SelectedNode node, bool visible)
        {
            Assert.IsTrue(Views.ContainsKey(node), "visibility of a node without a view: " + node);
            Views[node] = visible;
        }

        public void Release(SelectedNode node)
        {
            Assert.IsTrue(Views.Remove(node), "released a node without a view: " + node);
            Released.Add(node);
            Extras.Remove(node);
        }

        /// <summary>Extras handed over by ready nodes (W2: instances, heroes).</summary>
        public readonly Dictionary<SelectedNode, TileExtras> Extras = new Dictionary<SelectedNode, TileExtras>();

        public void Ready(SelectedNode node, TileExtras extras)
        {
            Assert.IsTrue(Views.ContainsKey(node), "ready without a view: " + node);
            Assert.IsNotNull(extras);
            Extras[node] = extras;
        }

        public List<SelectedNode> Visible()
        {
            var list = new List<SelectedNode>();
            foreach (KeyValuePair<SelectedNode, bool> kv in Views)
                if (kv.Value) list.Add(kv.Key);
            return list;
        }

        /// <summary>Fails when two visible areas overlap (the swap rule must prevent it).</summary>
        public void AssertNoOverlap()
        {
            List<SelectedNode> v = Visible();
            for (int i = 0; i < v.Count; i++)
                for (int j = i + 1; j < v.Count; j++)
                    Assert.IsFalse(TileArea.Overlaps(v[i].Area, v[j].Area), "visible " + v[i] + " overlaps " + v[j]);
        }
    }
}
