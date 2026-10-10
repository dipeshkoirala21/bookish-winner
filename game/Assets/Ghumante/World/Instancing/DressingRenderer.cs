using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Flora;
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
    /// Draws the instanced dressing of the visible detail tiles every frame (W2_DESIGN 5.8 trees and the nature kit's
    /// plants, 4.5 / 4.7 / 2.6 street props). Trees draw at four levels: each species' own detailed model (LOD0) and
    /// simplified model (LOD1) near the camera, then the far range as the shape family's volume (LOD2) out to
    /// <see cref="DressingConfig.TreeVolumeM"/> and its impostor (LOD3, a rounded crown on a trunk) out to the
    /// vegetation radius. Selection follows the nature kit's shared rules (<see cref="FloraLodPlan"/> under the tier's
    /// <see cref="FloraBudget"/>, the same code the preview scenes run): only what the camera's view cone can see
    /// (<see cref="FloraView"/>: the horizontal field of view plus a margin, everything within a few metres; street
    /// props are culled by the same cone, cell by cell), every
    /// level filled nearest first under its instance cap and its triangle budget, what overflows stepping down a level;
    /// the far pass takes the LOD1 overflow first, then whole 64 m cells by distance, so the far cap never leaves a
    /// bald patch around the camera. Plants (flowers, shrubs, hedges, pots, ground cover, rocks, straw stacks) draw at
    /// LOD0 then LOD1 within their own small radius, the large kinds (<see cref="FloraCatalog.DrawsFar"/>) at LOD1 on
    /// to <see cref="DressingConfig.PlantFarM"/>; out of season kinds are skipped (straw stacks after the harvest,
    /// sunflowers in summer). The near models carry their own colours (bloom, flush and harvest are baked per month and
    /// rebuilt progressively on a month change) with a small per-instance brightness variation; the grey far models
    /// take the species' crown colour of the month (<see cref="FloraCatalog.FoliageColour"/>). Swaying kinds use the
    /// wind material, rigid ones (pots, tulsi math, rocks, straw stacks) the plain tinted one. Chautari platforms stand
    /// under the OSM trees that carry one, at the size and turn placement fitted beside the road
    /// (<see cref="TreeInstance.PlatformM"/>). Instance matrices and tints are built per tile in scene space,
    /// progressively under a per-frame budget (nearest tile first; a tile is drawn once its cache is complete),
    /// shifted on origin rebases, re-tinted progressively on a month change, and kept a few seconds after the tile is
    /// hidden. So a frame only selects and copies (no allocation). One <see cref="InstanceBatch"/> per mesh. Main
    /// thread only.
    /// </summary>
    public sealed class DressingRenderer : IDisposable
    {
        private const uint PurposeTint = 0x54494E54;

        /// <summary>Cache work per frame, in instances (a matrix or a tint each): spreads a forest tile's
        /// ~16 k matrices and tints over a few frames instead of one multi-millisecond spike.</summary>
        public const int BuildUnitsPerFrame = 6000;

        /// <summary>Frames a hidden tile's cache is kept (a tile shown again soon needs no rebuild).</summary>
        public const int KeepFrames = 300;

        /// <summary>Species whose near models are rebuilt per frame after a month change.</summary>
        public const int MeshRebuildsPerFrame = 2;

        /// <summary>Far shape families (every <see cref="TreeShape"/> but <see cref="TreeShape.Low"/>).</summary>
        private const int FarFamilies = (int)TreeShape.Low;

        private const int FineCell = 0x40000000;

        private sealed class TileCache
        {
            public TileInstances Inst;
            public double X0, Z0, NearM;
            public Matrix4x4[] Trees, Props, Chautari;
            public Vector4[] TreeTint, NearTint, PropTint;
            public int[] ChautariTree;

            /// <summary>Indices into <see cref="TileInstances.Trees"/> per 64 m cell: trees (far-eligible) and plants
            /// apart, with their cell starts (cells + 1 entries); in each cell the plants of the far-drawn kinds come
            /// first, ending at <see cref="PlantFarEnd"/>[cell].</summary>
            public int[] TreeIdx, TreeIdxCells, PlantIdx, PlantIdxCells, PlantFarEnd;

            public WorldPos Origin;
            public int TintMonth, TreesDone, PropsDone, TintDone, LastUsed;
            public bool ChautariDone, Ready;
        }

        private readonly DressingConfig _config;
        private readonly WorldMaterialSet _materials;
        private readonly InstanceBatch[,] _species = new InstanceBatch[FloraCatalog.Count, 2];
        private readonly int[,] _speciesTris = new int[FloraCatalog.Count, 2];
        private readonly int[] _meshMonth = new int[FloraCatalog.Count];
        private readonly bool[] _isTree = new bool[FloraCatalog.Count], _inSeason = new bool[FloraCatalog.Count], _drawsFar = new bool[FloraCatalog.Count];
        private readonly InstanceBatch[,] _family = new InstanceBatch[FarFamilies, 2];
        private readonly int[,] _familyTris = new int[FarFamilies, 2];
        private readonly InstanceBatch[] _chautari = new InstanceBatch[2];
        private readonly InstanceBatch[] _props = new InstanceBatch[KitMeshes.PropKinds];
        private readonly Dictionary<TileInstances, TileCache> _caches = new Dictionary<TileInstances, TileCache>();
        private readonly List<TileInstances> _drop = new List<TileInstances>();
        private readonly MeshData _scratch = new MeshData(4096, 12288);
        private float[] _near = new float[256];
        private int[] _nearIndex = new int[256];
        private TileCache[] _nearTile = new TileCache[256];
        private float[] _farKey = new float[1024];
        private FloraLodCounters _count;
        private int[] _farCell = new int[1024];
        private TileCache[] _farTile = new TileCache[1024];
        private float[] _plant = new float[1024];
        private int[] _plantIndex = new int[1024];
        private TileCache[] _plantTile = new TileCache[1024];
        private TileCache[] _visible = new TileCache[32], _pending = new TileCache[32];
        private int _frame, _seasonMonth;

        /// <summary>Month for the seasonal colours and models (1-12; W2 acceptance runs in October).</summary>
        public int Month = 10;

        /// <summary>Triangles drawn last frame per level: tree LOD0, LOD1, volume, impostor, and plants (for the
        /// budget overlay and tests of the tier split).</summary>
        public readonly int[] LevelTris = new int[5];

        /// <summary>The camera whose view cone selects the dressing (<see cref="FloraView"/>); null: the main camera,
        /// and without one everything round the camera position is a candidate.</summary>
        public Camera ViewCamera;

        /// <summary>Select only what is in the camera's view cone (off: everything round the camera, as before).</summary>
        public bool ViewCulling = true;

        public DressingRenderer(DressingConfig config, WorldMaterialSet materials)
        {
            _config = config ?? DressingConfig.ForTier(1);
            _materials = materials;
            MeshData m = _scratch;
            for (int s = 0; s < FloraCatalog.Count; s++)
            {
                _isTree[s] = FloraCatalog.IsTree((TreeSpecies)s);
                _drawsFar[s] = FloraCatalog.DrawsFar((TreeSpecies)s);
                BuildSpecies(s, Month);
            }
            for (int f = 0; f < FarFamilies; f++)
                for (int k = 0; k < 2; k++)
                {
                    m.Clear();
                    int tris = FloraMesher.Family((TreeShape)f, 2 + k, m);
                    _familyTris[f, k] = tris;
                    _family[f, k] = new InstanceBatch(MeshUpload.CreateWhole(m, "tree_" + (TreeShape)f + (k == 0 ? "_volume" : "_impostor")), materials.trees, tris, true)
                    {
                        Shadows = ShadowCastingMode.Off,
                    };
                }
            for (int lod = 0; lod < 2; lod++)
            {
                m.Clear();
                int ct = KitMeshes.Chautari(m, lod);
                _chautari[lod] = new InstanceBatch(MeshUpload.CreateWhole(m, "chautari_" + lod), materials.instancedTint, ct, true)
                {
                    Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };
            }
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

        /// <summary>(Re)builds a species' LOD0 and LOD1 for a month: swaying kinds with the wind material, rigid ones
        /// with the plain tinted one; only tree LOD0 casts shadows.</summary>
        private void BuildSpecies(int s, int month)
        {
            var sp = (TreeSpecies)s;
            Material mat = FloraCatalog.Sways(sp) ? _materials.trees : _materials.instancedTint;
            for (int lod = 0; lod < 2; lod++)
            {
                _scratch.Clear();
                int tris = KitMeshes.Plant(sp, lod, month, _scratch);
                Mesh mesh = MeshUpload.CreateWhole(_scratch, "flora_" + sp + "_" + lod);
                InstanceBatch b = _species[s, lod];
                if (b == null)
                {
                    _species[s, lod] = new InstanceBatch(mesh, mat, tris, true)
                    {
                        Shadows = lod == 0 && _isTree[s] ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                }
                else
                {
                    b.DestroyMesh();
                    b.Mesh = mesh;
                    b.TrisPerInstance = tris;
                }
                _speciesTris[s, lod] = tris;
            }
            _meshMonth[s] = month;
        }

        /// <summary>After a month change: the season flags at once, the near models a few species per frame.</summary>
        private void UpdateSeason()
        {
            if (_seasonMonth != Month)
            {
                _seasonMonth = Month;
                for (int s = 0; s < FloraCatalog.Count; s++) _inSeason[s] = FloraCatalog.InSeason((TreeSpecies)s, Month);
            }
            int rebuilt = 0;
            for (int s = 0; s < FloraCatalog.Count && rebuilt < MeshRebuildsPerFrame; s++)
            {
                if (_meshMonth[s] == Month) continue;
                BuildSpecies(s, Month);
                rebuilt++;
            }
        }

        /// <summary>Selects and submits this frame's instances. <paramref name="cameraScene"/>: camera in scene space.
        /// Tree counters include the plants.</summary>
        public void Draw(IReadOnlyList<TileView> views, WorldPos origin, Vector3 cameraScene, out int treeTris, out int trees, out int propTris,
                         out int props, out int draws)
        {
            treeTris = trees = propTris = props = draws = 0;
            _frame++;
            UpdateSeason();
            var bounds = new Bounds(cameraScene, new Vector3(4000f, 3000f, 4000f));
            foreach (InstanceBatch b in _species) Reset(b, bounds);
            foreach (InstanceBatch b in _family) Reset(b, bounds);
            foreach (InstanceBatch b in _chautari) Reset(b, bounds);
            foreach (InstanceBatch b in _props) Reset(b, bounds);
            Array.Clear(LevelTris, 0, LevelTris.Length);

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

            DressingConfig cfg = _config;
            FloraBudget fb = cfg.Flora;
            FloraView cone = View(camX, camZ, fb);
            int nNear = 0, nFar = 0, nPlant = 0, propCount = 0;
            _count = default(FloraLodCounters);
            const double cellHalfDiag = 0.7072 * TileInstances.CellM;
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
                    if (dn > fb.TreeFarM && dn > fb.PlantFarM && dn > cfg.PropM) continue;
                    if (!cone.Sees(c.X0 + x0 + 0.5 * TileInstances.CellM, c.Z0 + z0 + 0.5 * TileInstances.CellM, cellHalfDiag)) continue;
                    // Trees: cells beyond the LOD1 radius are far candidates as a whole (O(cells), sorted below); nearer
                    // cells sort their trees by distance and leave their far trees to the same far pass.
                    if (dn <= fb.TreeFarM && c.TreeIdxCells[cell + 1] > c.TreeIdxCells[cell])
                    {
                        if (dn > fb.TreeLod1M) AddFar((float)dn, cell, c, ref nFar);
                        else
                        {
                            bool hasFar = false;
                            int a = c.TreeIdxCells[cell], e = c.TreeIdxCells[cell + 1];
                            for (int k = a; k < e; k++)
                            {
                                int i = c.TreeIdx[k];
                                TreeInstance t = inst.Trees[i];
                                double dx = t.X - cx, dz = t.Z - cz;
                                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                                if (d > fb.TreeLod1M) hasFar = true;
                                else if (cone.Sees(c.X0 + t.X, c.Z0 + t.Z, 0.5 * t.CrownM)) Push(ref _near, ref _nearIndex, ref _nearTile, ref nNear, d, i, c);
                            }
                            if (hasFar) AddFar(fb.TreeLod1M, cell | FineCell, c, ref nFar);
                        }
                    }
                    // Plants within their radius, in season: the far-drawn kinds (first in each cell) to the far radius.
                    if (dn <= fb.PlantFarM)
                    {
                        int a = c.PlantIdxCells[cell], e = dn <= fb.PlantM ? c.PlantIdxCells[cell + 1] : c.PlantFarEnd[cell];
                        for (int k = a; k < e; k++)
                        {
                            int i = c.PlantIdx[k];
                            TreeInstance t = inst.Trees[i];
                            int s = (int)t.Species;
                            if (!_inSeason[s]) continue;
                            double dx = t.X - cx, dz = t.Z - cz;
                            float d = (float)Math.Sqrt(dx * dx + dz * dz);
                            if (d <= (_drawsFar[s] ? fb.PlantFarM : fb.PlantM) && cone.Sees(c.X0 + t.X, c.Z0 + t.Z, 0.5 * t.CrownM))
                                Push(ref _plant, ref _plantIndex, ref _plantTile, ref nPlant, d, i, c);
                        }
                    }
                    // Props.
                    if (dn <= cfg.PropM && propCount < cfg.PropCap)
                    {
                        int a = inst.PropCells[cell], e = inst.PropCells[cell + 1];
                        for (int i = a; i < e && propCount < cfg.PropCap; i++)
                        {
                            StreetProp p = inst.Props[i];
                            InstanceBatch b = (int)p.Kind < _props.Length ? _props[(int)p.Kind] : null;
                            if (b == null) continue;
                            double dx = p.X - cx, dz = p.Z - cz;
                            if (dx * dx + dz * dz > cfg.PropM * cfg.PropM) continue;
                            b.Add(c.Props[i], c.PropTint[i]);
                            propCount++;
                        }
                    }
                }
                // Chautari platforms follow their trees' visibility radius (LOD1): detailed near, simple beyond.
                float nearChautari = 1.5f * fb.TreeLod0M;
                for (int k = 0; k < c.Chautari.Length; k++)
                {
                    TreeInstance t = inst.Trees[c.ChautariTree[k]];
                    double dx = t.X - cx, dz = t.Z - cz, d2 = dx * dx + dz * dz;
                    if (d2 <= nearChautari * nearChautari) _chautari[0].Add(c.Chautari[k], Vector4.one);
                    else if (d2 <= fb.TreeLod1M * fb.TreeLod1M) _chautari[1].Add(c.Chautari[k], Vector4.one);
                }
            }

            // Trees within the LOD1 radius, nearest first: LOD0, else LOD1, else a far level (FloraLodPlan).
            SortTiles(_near, _nearIndex, _nearTile, nNear);
            for (int i = 0; i < nNear; i++)
            {
                AddTree(_nearTile[i], _nearIndex[i], _near[i]);
                _nearTile[i] = null;
            }
            // The far levels: whole cells nearest first after the LOD1 overflow above.
            if (nFar > 1) SortTiles(_farKey, _farCell, _farTile, nFar);
            for (int k = 0; k < nFar; k++)
            {
                TileCache c = _farTile[k];
                _farTile[k] = null;
                if (_count.Far >= fb.TreeFarCap) continue;
                bool fine = (_farCell[k] & FineCell) != 0;
                int cell = _farCell[k] & ~FineCell;
                double cx = camX - c.X0, cz = camZ - c.Z0;
                // A whole cell beyond the volume radius is all impostors: no distance per tree.
                bool allImpostors = !fine && _farKey[k] > fb.TreeVolumeM;
                int a = c.TreeIdxCells[cell], e = c.TreeIdxCells[cell + 1];
                for (int j = a; j < e && _count.Far < fb.TreeFarCap; j++)
                {
                    int i = c.TreeIdx[j];
                    float d = _farKey[k];
                    if (!allImpostors)
                    {
                        TreeInstance t = c.Inst.Trees[i];
                        double dx = t.X - cx, dz = t.Z - cz;
                        double d2 = dx * dx + dz * dz;
                        if (d2 <= (double)fb.TreeLod1M * fb.TreeLod1M) continue; // drawn above
                        d = (float)Math.Sqrt(d2);
                    }
                    AddTree(c, i, d);
                }
            }

            // Plants: LOD0 near, LOD1 beyond, nearest first under the cap and the triangle budget.
            if (nPlant > 1) SortTiles(_plant, _plantIndex, _plantTile, nPlant);
            for (int i = 0; i < nPlant && _count.Plants < fb.PlantCap; i++)
            {
                TileCache c = _plantTile[i];
                int idx = _plantIndex[i];
                int s = (int)c.Inst.Trees[idx].Species;
                int lod = FloraLodPlan.PickPlant(fb, ref _count, _plant[i], _drawsFar[s], _speciesTris[s, 0], _speciesTris[s, 1]);
                if (lod >= 0) _species[s, lod].Add(c.Trees[idx], c.NearTint[idx]);
            }
            Array.Clear(_plantTile, 0, nPlant);
            LevelTris[0] = _count.Lod0Tris;
            LevelTris[1] = _count.Lod1Tris;
            LevelTris[2] = _count.VolumeTris;
            LevelTris[3] = _count.ImpostorTris;
            LevelTris[4] = _count.PlantTris;

            foreach (InstanceBatch b in _species) Flush(b, ref treeTris, ref trees, ref draws);
            foreach (InstanceBatch b in _family) Flush(b, ref treeTris, ref trees, ref draws);
            int chautariCount = 0;
            foreach (InstanceBatch b in _chautari) Flush(b, ref propTris, ref chautariCount, ref draws);
            foreach (InstanceBatch b in _props) Flush(b, ref propTris, ref props, ref draws);

            // Forget the caches of tiles hidden for a while.
            _drop.Clear();
            foreach (KeyValuePair<TileInstances, TileCache> kv in _caches)
                if (_frame - kv.Value.LastUsed > KeepFrames) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) _caches.Remove(_drop[i]);
        }

        private static void Reset(InstanceBatch b, Bounds bounds)
        {
            if (b == null) return;
            b.ResetCounters();
            b.WorldBounds = bounds;
        }

        private static void Flush(InstanceBatch b, ref int tris, ref int instances, ref int draws)
        {
            if (b == null) return;
            b.Flush();
            tris += b.Tris;
            instances += b.Instances;
            draws += b.Draws;
        }

        /// <summary>One tree at distance <paramref name="d"/>: the level <see cref="FloraLodPlan.PickTree"/> gives it
        /// (its species' LOD0 or LOD1, its family's volume or impostor, or nothing).</summary>
        private void AddTree(TileCache c, int idx, float d)
        {
            TreeInstance t = c.Inst.Trees[idx];
            int s = (int)t.Species, f = (int)t.Shape;
            if (f >= FarFamilies) f = 0;
            int lvl = FloraLodPlan.PickTree(_config.Flora, ref _count, d, _speciesTris[s, 0], _speciesTris[s, 1], _familyTris[f, 0], _familyTris[f, 1]);
            if (lvl == 0 || lvl == 1) _species[s, lvl].Add(c.Trees[idx], c.NearTint[idx]);
            else if (lvl >= 2) _family[f, lvl - 2].Add(c.Trees[idx], c.TreeTint[idx]);
        }

        /// <summary>
        /// The view cone of this frame in world metres, from the camera position the frame is drawn for: the view
        /// camera's (or the main camera's) horizontal field of view plus the budget's margin, along its forward vector;
        /// everything round the camera without a camera or with <see cref="ViewCulling"/> off.
        /// </summary>
        private FloraView View(double camX, double camZ, FloraBudget fb)
        {
            Camera cam = ViewCamera != null ? ViewCamera : Camera.main;
            if (!ViewCulling || cam == null || cam.orthographic) return FloraView.All(camX, camZ);
            Vector3 f = cam.transform.forward;
            return FloraView.Cone(camX, camZ, f.x, f.y, f.z, FloraView.HorizontalFov(cam.fieldOfView, cam.aspect), fb.ViewMarginDeg, fb.ViewNearM);
        }

        private void AddFar(float key, int cell, TileCache c, ref int n)
        {
            Grow(ref _farKey, ref _farCell, ref _farTile, n);
            _farKey[n] = key;
            _farCell[n] = cell;
            _farTile[n++] = c;
        }

        private static void Push(ref float[] d, ref int[] idx, ref TileCache[] tiles, ref int n, float dist, int i, TileCache c)
        {
            Grow(ref d, ref idx, ref tiles, n);
            d[n] = dist;
            idx[n] = i;
            tiles[n++] = c;
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
                Trees = new Matrix4x4[inst.Trees.Count], TreeTint = new Vector4[inst.Trees.Count], NearTint = new Vector4[inst.Trees.Count],
                Props = new Matrix4x4[inst.Props.Count], PropTint = new Vector4[inst.Props.Count],
            };
            SplitCells(c);
            _caches.Add(inst, c);
            return c;
        }

        /// <summary>Per-cell index lists of the trees and of the plants of a tile (the instance list is sorted by cell,
        /// so each cell's entries stay together and in order); in each cell the plants of the far-drawn kinds come
        /// first, so a cell beyond the small-plant radius scans only those.</summary>
        private void SplitCells(TileCache c)
        {
            TileInstances inst = c.Inst;
            int cells = inst.CellsPerSide * inst.CellsPerSide, nTrees = 0;
            for (int i = 0; i < inst.Trees.Count; i++)
                if (_isTree[(int)inst.Trees[i].Species]) nTrees++;
            c.TreeIdx = new int[nTrees];
            c.PlantIdx = new int[inst.Trees.Count - nTrees];
            c.TreeIdxCells = new int[cells + 1];
            c.PlantIdxCells = new int[cells + 1];
            c.PlantFarEnd = new int[cells];
            int ti = 0, pi = 0;
            for (int cell = 0; cell < cells; cell++)
            {
                c.TreeIdxCells[cell] = ti;
                c.PlantIdxCells[cell] = pi;
                for (int i = inst.TreeCells[cell]; i < inst.TreeCells[cell + 1]; i++)
                {
                    int s = (int)inst.Trees[i].Species;
                    if (_isTree[s]) c.TreeIdx[ti++] = i;
                    else if (_drawsFar[s]) c.PlantIdx[pi++] = i;
                }
                c.PlantFarEnd[cell] = pi;
                for (int i = inst.TreeCells[cell]; i < inst.TreeCells[cell + 1]; i++)
                {
                    int s = (int)inst.Trees[i].Species;
                    if (!_isTree[s] && !_drawsFar[s]) c.PlantIdx[pi++] = i;
                }
            }
            c.TreeIdxCells[cells] = ti;
            c.PlantIdxCells[cells] = pi;
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
                int i = c.TreesDone++;
                TreeInstance t = inst.Trees[i];
                // Trunks sink 10 cm into slopes; plants a little; rigid kinds (pots, rocks) sit on the ground.
                FloraClass cls = FloraCatalog.Info(t.Species).Class;
                float sink = cls == FloraClass.Tree ? 0.1f : cls == FloraClass.Plant ? 0.05f : 0.02f;
                c.Trees[i] = Matrix4x4.TRS(new Vector3(ox + t.X, t.Y - oy - sink, oz + t.Z), Quaternion.Euler(0f, t.YawDeg, 0f),
                                           new Vector3(t.CrownM, t.HeightM, t.CrownM));
                c.NearTint[i] = Tint.Vary(Vector4.one, WorldHash.Unit(Key(t), PurposeTint ^ 0x4E), cls == FloraClass.Rigid ? 0.05f : 0.08f);
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
                    float w = t.PlatformM > 0f ? t.PlatformM : TreePlacement.DefaultPlatformM(t.CrownM);
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

        /// <summary>The far tint of a tree: the species' crown colour of the month with a small variation.</summary>
        private Vector4 TreeTint(in TreeInstance t)
        {
            return Tint.Vary(Tint.Hex(FloraCatalog.FoliageColour(t.Species, Month)), WorldHash.Unit(Key(t), PurposeTint), 0.1f);
        }

        private static ulong Key(in TreeInstance t)
        {
            return t.OsmRef != 0 ? t.OsmRef : (ulong)(uint)(t.X * 100f) << 32 | (uint)(t.Z * 100f);
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
            foreach (InstanceBatch b in _species)
                if (b != null) b.DestroyMesh();
            foreach (InstanceBatch b in _family)
                if (b != null) b.DestroyMesh();
            foreach (InstanceBatch b in _chautari)
                if (b != null) b.DestroyMesh();
            for (int k = 0; k < _props.Length; k++)
                if (_props[k] != null) _props[k].DestroyMesh();
            _caches.Clear();
        }
    }
}
