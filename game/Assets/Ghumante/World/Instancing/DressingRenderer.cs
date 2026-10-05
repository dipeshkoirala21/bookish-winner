using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;
using TreeInstance = Ghumante.Core.Generators.Placement.TreeInstance;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Draws the instanced dressing of the visible detail tiles every frame (W2_DESIGN 5.8 trees, 4.5 / 4.7 / 2.6 street
    /// props): trees in three shape families at three LODs (LOD0 and LOD1 nearest-first under the tier caps, a cheap far
    /// LOD to the vegetation radius, also nearest first: the LOD1 overflow, then whole 64 m cells by distance, so the
    /// far cap never leaves a bald patch around the camera) with vertex wind and a per-instance crown colour by species
    /// and month, chautari platforms under the OSM trees that carry one, and every street prop kind within the prop
    /// radius. Instance matrices and tints are built per tile in scene space, progressively under a per-frame budget
    /// (nearest tile first; a tile is drawn once its cache is complete), shifted on origin rebases (three adds each),
    /// re-tinted progressively on a month change, and kept a few seconds after the tile is hidden. So a frame only
    /// selects and copies. One <see cref="InstanceBatch"/> per mesh: about a dozen instanced draw calls. Main thread only.
    /// </summary>
    public sealed class DressingRenderer : IDisposable
    {
        private const uint PurposeTint = 0x54494E54;

        /// <summary>Cache work per frame, in instances (a matrix or a tint each): spreads a forest tile's
        /// ~16 k matrices and tints over a few frames instead of one multi-millisecond spike.</summary>
        public const int BuildUnitsPerFrame = 6000;

        /// <summary>Frames a hidden tile's cache is kept (a tile shown again soon needs no rebuild).</summary>
        public const int KeepFrames = 300;

        private const int FineCell = 0x40000000;

        private sealed class TileCache
        {
            public TileInstances Inst;
            public double X0, Z0, NearM;
            public Matrix4x4[] Trees, Props, Chautari;
            public Vector4[] TreeTint, PropTint;
            public int[] ChautariTree;
            public WorldPos Origin;
            public int TintMonth, TreesDone, PropsDone, TintDone, LastUsed;
            public bool ChautariDone, Ready;
        }

        private readonly DressingConfig _config;
        private readonly InstanceBatch[,] _trees = new InstanceBatch[3, KitMeshes.TreeLods];
        private readonly InstanceBatch _chautari;
        private readonly InstanceBatch[] _props = new InstanceBatch[KitMeshes.PropKinds];
        private readonly Dictionary<TileInstances, TileCache> _caches = new Dictionary<TileInstances, TileCache>();
        private readonly List<TileInstances> _drop = new List<TileInstances>();
        private float[] _near = new float[256];
        private int[] _nearIndex = new int[256];
        private TileCache[] _nearTile = new TileCache[256];
        private float[] _mid = new float[1024];
        private int[] _midIndex = new int[1024];
        private TileCache[] _midTile = new TileCache[1024];
        private float[] _farKey = new float[1024];
        private int[] _farCell = new int[1024];
        private TileCache[] _farTile = new TileCache[1024];
        private TileCache[] _visible = new TileCache[32], _pending = new TileCache[32];
        private int _frame;

        /// <summary>Month for the seasonal colours (1-12; W2 acceptance runs in October).</summary>
        public int Month = 10;

        public DressingRenderer(DressingConfig config, WorldMaterialSet materials)
        {
            _config = config ?? DressingConfig.ForTier(1);
            var m = new MeshData(1024, 3072);
            for (int s = 0; s < 3; s++)
                for (int lod = 0; lod < KitMeshes.TreeLods; lod++)
                {
                    m.Clear();
                    int tris = KitMeshes.Tree((TreeShape)s, lod, m);
                    _trees[s, lod] = new InstanceBatch(MeshUpload.CreateWhole(m, "tree_" + (TreeShape)s + "_" + lod), materials.trees, tris, true)
                    {
                        Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                }
            m.Clear();
            int ct = KitMeshes.Chautari(m);
            _chautari = new InstanceBatch(MeshUpload.CreateWhole(m, "chautari"), materials.instancedTint, ct, true);
            for (int k = 0; k < KitMeshes.PropKinds; k++)
            {
                m.Clear();
                int tris = KitMeshes.Prop((StreetPropKind)k, m);
                if (tris == 0) continue;
                _props[k] = new InstanceBatch(MeshUpload.CreateWhole(m, "prop_" + (StreetPropKind)k), materials.instancedTint, tris, true)
                {
                    Shadows = k == (int)StreetPropKind.Helipad || k == (int)StreetPropKind.ParkingPosition ? ShadowCastingMode.Off : ShadowCastingMode.On,
                };
            }
        }

        /// <summary>Selects and submits this frame's instances. <paramref name="cameraScene"/>: camera in scene space.</summary>
        public void Draw(IReadOnlyList<TileView> views, WorldPos origin, Vector3 cameraScene, out int treeTris, out int trees, out int propTris,
                         out int props, out int draws)
        {
            treeTris = trees = propTris = props = draws = 0;
            _frame++;
            var bounds = new Bounds(cameraScene, new Vector3(4000f, 3000f, 4000f));
            foreach (InstanceBatch b in _trees)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            _chautari.ResetCounters();
            _chautari.WorldBounds = bounds;
            for (int k = 0; k < _props.Length; k++)
            {
                if (_props[k] == null) continue;
                _props[k].ResetCounters();
                _props[k].WorldBounds = bounds;
            }

            double camX = origin.X + cameraScene.x, camZ = origin.Z + cameraScene.z;

            // The visible tiles' caches; unfinished ones advance under the frame's budget, nearest tile first.
            int nVis = 0, nPending = 0;
            for (int v = 0; v < views.Count; v++)
            {
                TileView view = views[v];
                TileInstances inst = view.Extras != null ? view.Extras.Instances : null;
                if (inst == null || inst.IsEmpty) continue;
                TileCache c = Cache(inst, view, origin);
                c.LastUsed = _frame;
                double size = inst.CellsPerSide * TileInstances.CellM;
                c.NearM = Buildings.BandConfig.NearestDistance(0, 0, size, size, camX - c.X0, camZ - c.Z0);
                if (nVis == _visible.Length) Array.Resize(ref _visible, nVis * 2);
                _visible[nVis++] = c;
                if (c.Ready && c.TintMonth == Month && c.TintDone >= inst.Trees.Count) continue;
                if (nPending == _pending.Length) Array.Resize(ref _pending, nPending * 2);
                int at = nPending++;
                while (at > 0 && _pending[at - 1].NearM > c.NearM)
                {
                    _pending[at] = _pending[at - 1];
                    at--;
                }
                _pending[at] = c;
            }
            int budget = BuildUnitsPerFrame;
            for (int i = 0; i < nPending; i++)
            {
                if (budget > 0) Advance(_pending[i], ref budget);
                _pending[i] = null;
            }

            int nNear = 0, nMid = 0, nFar = 0, far = 0, propCount = 0;
            for (int v = 0; v < nVis; v++)
            {
                TileCache c = _visible[v];
                _visible[v] = null;
                if (!c.Ready) continue;
                TileInstances inst = c.Inst;
                double cx = camX - c.X0, cz = camZ - c.Z0;
                int n = inst.CellsPerSide;
                for (int cell = 0; cell < n * n; cell++)
                {
                    double x0 = (cell % n) * TileInstances.CellM, z0 = (cell / n) * TileInstances.CellM;
                    double dn = Buildings.BandConfig.NearestDistance(x0, z0, x0 + TileInstances.CellM, z0 + TileInstances.CellM, cx, cz);
                    // Trees: cells beyond the LOD1 radius are far candidates as a whole (O(cells), sorted below); nearer
                    // cells sort their trees into LOD0 / LOD1 and leave their far trees to the same far pass.
                    if (dn <= _config.TreeLod2M && inst.TreeCells[cell + 1] > inst.TreeCells[cell])
                    {
                        if (dn > _config.TreeLod1M) AddFar((float)dn, cell, c, ref nFar);
                        else
                        {
                            bool hasFar = false;
                            int a = inst.TreeCells[cell], e = inst.TreeCells[cell + 1];
                            for (int i = a; i < e; i++)
                            {
                                TreeInstance t = inst.Trees[i];
                                double dx = t.X - cx, dz = t.Z - cz;
                                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                                int lod = _config.TreeLodAt(d);
                                if (lod == 0)
                                {
                                    Grow(ref _near, ref _nearIndex, ref _nearTile, nNear);
                                    _near[nNear] = d;
                                    _nearIndex[nNear] = i;
                                    _nearTile[nNear++] = c;
                                }
                                else if (lod == 1)
                                {
                                    Grow(ref _mid, ref _midIndex, ref _midTile, nMid);
                                    _mid[nMid] = d;
                                    _midIndex[nMid] = i;
                                    _midTile[nMid++] = c;
                                }
                                else hasFar = true;
                            }
                            if (hasFar) AddFar(_config.TreeLod1M, cell | FineCell, c, ref nFar);
                        }
                    }
                    // Props.
                    if (dn <= _config.PropM && propCount < _config.PropCap)
                    {
                        int a = inst.PropCells[cell], e = inst.PropCells[cell + 1];
                        for (int i = a; i < e && propCount < _config.PropCap; i++)
                        {
                            StreetProp p = inst.Props[i];
                            InstanceBatch b = (int)p.Kind < _props.Length ? _props[(int)p.Kind] : null;
                            if (b == null) continue;
                            double dx = p.X - cx, dz = p.Z - cz;
                            if (dx * dx + dz * dz > _config.PropM * _config.PropM) continue;
                            b.Add(c.Props[i], c.PropTint[i]);
                            propCount++;
                        }
                    }
                }
                // Chautari platforms follow their trees' visibility radius (LOD1).
                for (int k = 0; k < c.Chautari.Length; k++)
                {
                    TreeInstance t = inst.Trees[c.ChautariTree[k]];
                    double dx = t.X - cx, dz = t.Z - cz;
                    if (dx * dx + dz * dz <= _config.TreeLod1M * _config.TreeLod1M) _chautari.Add(c.Chautari[k], Vector4.one);
                }
            }

            // LOD0 nearest-first under its cap; the rest step down to LOD1, then LOD1 likewise to the far LOD.
            SortTiles(_near, _nearIndex, _nearTile, nNear);
            for (int i = 0; i < nNear; i++)
            {
                TileCache c = _nearTile[i];
                int idx = _nearIndex[i];
                TreeInstance t = c.Inst.Trees[idx];
                if (i < _config.TreeLod0Cap) _trees[(int)t.Shape, 0].Add(c.Trees[idx], c.TreeTint[idx]);
                else
                {
                    Grow(ref _mid, ref _midIndex, ref _midTile, nMid);
                    _mid[nMid] = _near[i];
                    _midIndex[nMid] = idx;
                    _midTile[nMid++] = c;
                }
                _nearTile[i] = null;
            }
            if (nMid > 1) SortTiles(_mid, _midIndex, _midTile, nMid);
            for (int i = 0; i < nMid; i++)
            {
                TileCache c = _midTile[i];
                int idx = _midIndex[i];
                TreeInstance t = c.Inst.Trees[idx];
                if (i < _config.TreeLod1Cap) _trees[(int)t.Shape, 1].Add(c.Trees[idx], c.TreeTint[idx]);
                else if (far < _config.TreeLod2Cap)
                {
                    _trees[(int)t.Shape, 2].Add(c.Trees[idx], c.TreeTint[idx]);
                    far++;
                }
                _midTile[i] = null;
            }
            // The far LOD: the LOD1 overflow above (all within the LOD1 radius), then whole cells nearest first.
            if (nFar > 1) SortTiles(_farKey, _farCell, _farTile, nFar);
            for (int k = 0; k < nFar; k++)
            {
                TileCache c = _farTile[k];
                _farTile[k] = null;
                if (far >= _config.TreeLod2Cap) continue;
                bool fine = (_farCell[k] & FineCell) != 0;
                int cell = _farCell[k] & ~FineCell;
                double cx = camX - c.X0, cz = camZ - c.Z0;
                TileInstances inst = c.Inst;
                int a = inst.TreeCells[cell], e = inst.TreeCells[cell + 1];
                for (int i = a; i < e && far < _config.TreeLod2Cap; i++)
                {
                    TreeInstance t = inst.Trees[i];
                    if (fine)
                    {
                        double dx = t.X - cx, dz = t.Z - cz;
                        if (dx * dx + dz * dz <= _config.TreeLod1M * _config.TreeLod1M) continue; // drawn at LOD0 / LOD1 above
                    }
                    _trees[(int)t.Shape, 2].Add(c.Trees[i], c.TreeTint[i]);
                    far++;
                }
            }

            foreach (InstanceBatch b in _trees)
            {
                b.Flush();
                treeTris += b.Tris;
                trees += b.Instances;
                draws += b.Draws;
            }
            _chautari.Flush();
            propTris += _chautari.Tris;
            draws += _chautari.Draws;
            for (int k = 0; k < _props.Length; k++)
            {
                if (_props[k] == null) continue;
                _props[k].Flush();
                propTris += _props[k].Tris;
                props += _props[k].Instances;
                draws += _props[k].Draws;
            }

            // Forget the caches of tiles hidden for a while.
            _drop.Clear();
            foreach (KeyValuePair<TileInstances, TileCache> kv in _caches)
                if (_frame - kv.Value.LastUsed > KeepFrames) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) _caches.Remove(_drop[i]);
        }

        private void AddFar(float key, int cell, TileCache c, ref int n)
        {
            Grow(ref _farKey, ref _farCell, ref _farTile, n);
            _farKey[n] = key;
            _farCell[n] = cell;
            _farTile[n++] = c;
        }

        /// <summary>Sorts parallel arrays by distance (keys first, then the tile array follows the same permutation).</summary>
        private static void SortTiles(float[] keys, int[] index, TileCache[] tiles, int n)
        {
            if (n < 2) return;
            // Pack the tile into the index array's order with a stable secondary sort: sort an index permutation.
            var perm = _perm;
            if (perm == null || perm.Length < n) perm = _perm = new int[Math.Max(n, 256)];
            for (int i = 0; i < n; i++) perm[i] = i;
            var k2 = _keys2;
            if (k2 == null || k2.Length < n) k2 = _keys2 = new float[Math.Max(n, 256)];
            Array.Copy(keys, k2, n);
            Array.Sort(k2, perm, 0, n);
            var it = _idxTmp;
            if (it == null || it.Length < n)
            {
                it = _idxTmp = new int[Math.Max(n, 256)];
                _tileTmp = new TileCache[it.Length];
            }
            for (int i = 0; i < n; i++)
            {
                it[i] = index[perm[i]];
                _tileTmp[i] = tiles[perm[i]];
            }
            for (int i = 0; i < n; i++)
            {
                keys[i] = k2[i];
                index[i] = it[i];
                tiles[i] = _tileTmp[i];
                _tileTmp[i] = null;
            }
        }

        [ThreadStatic] private static int[] _perm, _idxTmp;
        [ThreadStatic] private static float[] _keys2;
        [ThreadStatic] private static TileCache[] _tileTmp;

        private static void Grow(ref float[] d, ref int[] idx, ref TileCache[] tiles, int n)
        {
            if (n < d.Length) return;
            Array.Resize(ref d, d.Length * 2);
            Array.Resize(ref idx, idx.Length * 2);
            Array.Resize(ref tiles, tiles.Length * 2);
        }

        /// <summary>The tile's cache (created empty on first sight; <see cref="Advance"/> fills it), shifted to the
        /// current origin.</summary>
        private TileCache Cache(TileInstances inst, TileView view, WorldPos origin)
        {
            TileCache c;
            if (_caches.TryGetValue(inst, out c))
            {
                if (c.Origin != origin) Shift(c, origin);
                return c;
            }
            c = new TileCache
            {
                Inst = inst, X0 = view.Node.Area.X0, Z0 = view.Node.Area.Z0, Origin = origin, TintMonth = Month,
                Trees = new Matrix4x4[inst.Trees.Count], TreeTint = new Vector4[inst.Trees.Count],
                Props = new Matrix4x4[inst.Props.Count], PropTint = new Vector4[inst.Props.Count],
            };
            _caches.Add(inst, c);
            return c;
        }

        /// <summary>Builds up to <paramref name="budget"/> more matrices and tints of a cache: trees (matrix and tint),
        /// then chautari, then props; a ready cache re-tints after a month change. Entries are built at the cache's
        /// current origin (a rebase mid-build shifts what is built; the rest follows the new origin).</summary>
        private void Advance(TileCache c, ref int budget)
        {
            TileInstances inst = c.Inst;
            int nTrees = inst.Trees.Count;
            if (c.TintMonth != Month)
            {
                c.TintMonth = Month;
                c.TintDone = 0;
            }
            float ox = (float)(c.X0 - c.Origin.X), oz = (float)(c.Z0 - c.Origin.Z), oy = c.Origin.Y;
            while (c.TreesDone < nTrees && budget > 0)
            {
                TreeInstance t = inst.Trees[c.TreesDone];
                c.Trees[c.TreesDone++] = Matrix4x4.TRS(new Vector3(ox + t.X, t.Y - oy - 0.1f, oz + t.Z), Quaternion.Euler(0f, t.YawDeg, 0f),
                                                       new Vector3(t.CrownM, t.HeightM, t.CrownM));
                budget--;
            }
            while (c.TintDone < c.TreesDone && budget > 0)
            {
                c.TreeTint[c.TintDone] = TreeTint(inst.Trees[c.TintDone]);
                c.TintDone++;
                budget--;
            }
            if (c.TreesDone < nTrees) return;
            if (!c.ChautariDone)
            {
                int chautari = 0;
                for (int i = 0; i < nTrees; i++)
                    if (inst.Trees[i].Chautari) chautari++;
                c.Chautari = new Matrix4x4[chautari];
                c.ChautariTree = new int[chautari];
                chautari = 0;
                for (int i = 0; i < nTrees; i++)
                {
                    TreeInstance t = inst.Trees[i];
                    if (!t.Chautari) continue;
                    float w = Mathf.Clamp(t.CrownM * 0.45f, 3f, 9f);
                    float h = Mathf.Lerp(0.45f, 0.9f, WorldHash.Unit(t.OsmRef, 0x43484154));
                    c.ChautariTree[chautari] = i;
                    c.Chautari[chautari++] = Matrix4x4.TRS(new Vector3(ox + t.X, t.Y - oy, oz + t.Z), Quaternion.Euler(0f, t.YawDeg, 0f), new Vector3(w, h, w));
                }
                c.ChautariDone = true;
                budget -= chautari;
            }
            while (c.PropsDone < inst.Props.Count && budget > 0)
            {
                int i = c.PropsDone++;
                StreetProp p = inst.Props[i];
                float def = KitMeshes.DefaultHeight(p.Kind);
                float s = p.HeightM > 0f && def > 0.2f ? Mathf.Clamp(p.HeightM / def, 0.5f, 2f) : 1f;
                c.Props[i] = Matrix4x4.TRS(new Vector3(ox + p.X, p.Y - oy, oz + p.Z), Quaternion.Euler(0f, p.YawDeg, 0f), new Vector3(s, s, s));
                ulong key = p.OsmRef != 0 ? p.OsmRef : (ulong)(uint)(p.X * 100f) << 32 | (uint)(p.Z * 100f);
                c.PropTint[i] = p.Kind == StreetPropKind.StorageTank ? Tint.Hex(DressingConfig.TankColour(WorldHash.Unit(key, PurposeTint))) : Vector4.one;
                budget--;
            }
            // Drawn once complete; a later month change re-tints in place while the tile stays drawn.
            if (c.PropsDone >= inst.Props.Count && c.TintDone >= nTrees) c.Ready = true;
        }

        private Vector4 TreeTint(in TreeInstance t)
        {
            ulong key = t.OsmRef != 0 ? t.OsmRef : (ulong)(uint)(t.X * 100f) << 32 | (uint)(t.Z * 100f);
            return Tint.Vary(Tint.Hex(DressingConfig.CrownColour(t.Species, Month)), WorldHash.Unit(key, PurposeTint), 0.1f);
        }

        private static void Shift(TileCache c, WorldPos origin)
        {
            float dx = (float)(origin.X - c.Origin.X), dy = origin.Y - c.Origin.Y, dz = (float)(origin.Z - c.Origin.Z);
            ShiftAll(c.Trees, dx, dy, dz);
            ShiftAll(c.Props, dx, dy, dz);
            ShiftAll(c.Chautari, dx, dy, dz);
            c.Origin = origin;
        }

        private static void ShiftAll(Matrix4x4[] m, float dx, float dy, float dz)
        {
            if (m == null) return; // not built yet
            for (int i = 0; i < m.Length; i++)
            {
                m[i].m03 -= dx;
                m[i].m13 -= dy;
                m[i].m23 -= dz;
            }
        }

        public void Dispose()
        {
            foreach (InstanceBatch b in _trees)
                if (b != null) b.DestroyMesh();
            _chautari.DestroyMesh();
            for (int k = 0; k < _props.Length; k++)
                if (_props[k] != null) _props[k].DestroyMesh();
            _caches.Clear();
        }
    }
}
