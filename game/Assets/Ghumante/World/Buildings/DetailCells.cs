using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Meshing;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Buildings
{
    /// <summary>
    /// The B0 "kit-lite" band (W2_DESIGN 2.4; the detail pass band table, Core BuildingBandTable) in two rings around the
    /// camera: the <b>near</b> ring (out to <see cref="BandConfig.B0NearOuterM"/>) draws the full house grammar per 32 m
    /// cell at the tier's per-house cap; the <b>lite</b> ring (out to <see cref="BandConfig.B0OuterM"/>) draws every house
    /// at the tier's lite level (<see cref="BandConfig.B0LiteDrop"/>) per 64 m cell. Each ring has its own copy of the B0 material whose band range dithers the 4 m
    /// cross-fade between the rings (and into B1), so a house never pops between its two forms. A cell is built on a
    /// worker (BuildingDetailMesher.BuildCell, at most one in flight and at most one started per 100 ms, nearest first,
    /// near cells first) when it comes within its ring plus a margin, uploaded on the main thread when the tile uploads
    /// leave room, and kept in an LRU of 24 / 48 / 96 cells. A B1 block switches to its "outer" material only once every
    /// cell of both rings near the camera in it is up (<see cref="BlockReady"/>); until then B1 stands in and those cells
    /// stay hidden, so there is never a hole. Shown cells contribute their structure colliders (pikha aprons, flat roofs,
    /// stair cabins, temple plinths) to their tile, once per house. Cells keep footprints and low projections out of the
    /// road corridors (BuildingOptions.RoadGuard). Main thread only, apart from the cell job.
    /// </summary>
    internal sealed class DetailCells : IDisposable
    {
        /// <summary>Cells are prepared and shown this far beyond their ring (they overhang their centroid cell).</summary>
        public const float MarginM = 24f;

        /// <summary>Milliseconds between two cell builds (W2_DESIGN 10.4: at most one cell per 100 ms).</summary>
        public const double DispatchIntervalMs = 100.0;

        private const int NearLayer = 0, LiteLayer = 1;

        private sealed class Cell
        {
            public TileView View;
            public int Layer, CX, CZ;
            public double MinX, MinZ, MaxX, MaxZ;
            public MeshData Data;
            public GenColliders Gen;
            public StructureColliders Colliders;
            public Mesh Mesh;
            public GameObject Go;
            public MeshRenderer Renderer;
            public int Tris;
            public double LastUsedMs;
            public bool Uploaded, Shown, Failed;
            public volatile bool Done;
            public Exception Error;
        }

        private readonly BandConfig _bands;
        private readonly BuildingOptions _options;
        private readonly Material[] _materials = new Material[2];
        private readonly Dictionary<TileView, Dictionary<int, Cell>> _cells = new Dictionary<TileView, Dictionary<int, Cell>>();
        private readonly List<Cell> _all = new List<Cell>(128);
        private Cell _job;
        private double _lastDispatchMs = -1e9;
        private TileView _bestView;
        private int _bestLayer, _bestX, _bestZ;
        private double _bestScore;
        private bool _disposed;

        /// <summary>The band set and the B0 options of the streamer; <paramref name="material"/> is the B0 band material,
        /// copied once per ring with that ring's band range (the copies are owned and destroyed here).</summary>
        public DetailCells(BandConfig bands, BuildingOptions options, Material material)
        {
            _bands = bands;
            _options = options ?? new BuildingOptions { Band = BuildingBand.B0KitLite };
            for (int layer = 0; layer < 2; layer++) _materials[layer] = RingMaterial(material, layer);
        }

        private Material RingMaterial(Material source, int layer)
        {
            if (source == null) return null;
            float inner, outer;
            _bands.B0Range(layer == NearLayer, out inner, out outer);
            var m = new Material(source) { name = source.name + (layer == NearLayer ? " (near)" : " (lite)"), hideFlags = HideFlags.DontSave };
            m.SetVector(WorldShaders.BandRange, new Vector4(inner, outer, BandConfig.FadeM, 0f));
            m.EnableKeyword(WorldShaders.KeywordBandFade);
            return m;
        }

        /// <summary>The options a cell of a ring builds with, taken when the build starts (the caller's instance is shared
        /// with the other bands and its hide set may change between builds): the near ring at the tier's per-house cap and
        /// base drop level, the lite ring at the tier's lite level (flat on Low).</summary>
        private BuildingOptions OptionsFor(int layer)
        {
            if (layer == LiteLayer)
            {
                BuildingOptions lite = BuildingBandTable.LiteOptions(_options);
                if (_bands.B0LiteDrop > 0) lite.B0BaseDrop = _bands.B0LiteDrop;
                return lite;
            }
            BuildingOptions o = BuildingBandTable.Copy(_options, BuildingBand.B0KitLite);
            if (_bands.B0CapTris > 0) o.B0CapTris = _bands.B0CapTris;
            o.B0BaseDrop = Math.Max(_bands.B0BaseDrop, _options.B0BaseDrop);
            return o;
        }

        /// <summary>Triangles of the cells shown this frame.</summary>
        public int ShownTris { get; private set; }

        /// <summary>Cells shown this frame.</summary>
        public int ShownCount { get; private set; }

        /// <summary>Cells alive (built or building).</summary>
        public int Count
        {
            get { return _all.Count; }
        }

        private static int Key(int layer, int cx, int cz)
        {
            return layer << 24 | cz * 4096 + cx;
        }

        private static int Subdivision(int layer)
        {
            return layer == NearLayer ? BuildingBandTable.NearSubdivision : 1;
        }

        /// <summary>True when a cell of <paramref name="layer"/> over [x0, x1] × [z0, z1] (tile-local) may hold a house its
        /// ring draws for the camera at (cx, cz): its bounds grown by <see cref="MarginM"/> touch the ring.</summary>
        private bool Wanted(int layer, double x0, double z0, double x1, double z1, double cx, double cz)
        {
            float inner, outer;
            _bands.B0Range(layer == NearLayer, out inner, out outer);
            return BandConfig.Touches(x0 - MarginM, z0 - MarginM, x1 + MarginM, z1 + MarginM, cx, cz, inner, outer);
        }

        /// <summary>Per-frame: mark the cells near the camera, show the ready ones in range, start the next build.</summary>
        public void Update(List<TileView> views, double camX, double camZ, double nowMs)
        {
            if (_disposed) return;
            ShownTris = 0;
            ShownCount = 0;
            _bestView = null;
            _bestScore = double.MaxValue;
            float reach = _bands.B0OuterM + BandConfig.FadeM + MarginM;
            for (int v = 0; v < views.Count; v++)
            {
                TileView view = views[v];
                TileExtras e = view.Extras;
                if (e == null || e.Source == null || e.Sampler == null || !e.Sampler.HasHeights) continue;
                double cx = camX - view.Node.Area.X0, cz = camZ - view.Node.Area.Z0;
                Dictionary<int, Cell> map;
                _cells.TryGetValue(view, out map);
                for (int layer = 0; layer < 2; layer++)
                {
                    int per = BuildingDetailMesher.CellsPerSide(e.Source, Subdivision(layer));
                    double cell = view.Node.Area.Size / per;
                    int x0 = Math.Max(0, (int)Math.Floor((cx - reach) / cell)), x1 = Math.Min(per - 1, (int)Math.Floor((cx + reach) / cell));
                    int z0 = Math.Max(0, (int)Math.Floor((cz - reach) / cell)), z1 = Math.Min(per - 1, (int)Math.Floor((cz + reach) / cell));
                    for (int iz = z0; iz <= z1; iz++)
                        for (int ix = x0; ix <= x1; ix++)
                        {
                            if (!Wanted(layer, ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz)) continue;
                            Cell c;
                            if (map != null && map.TryGetValue(Key(layer, ix, iz), out c))
                            {
                                c.LastUsedMs = nowMs;
                                continue;
                            }
                            // Nearest first; the near ring's cells (the houses beside the camera) before lite cells as near.
                            double d = BandConfig.NearestDistance(ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz);
                            double score = d + (layer == NearLayer ? 0 : 8);
                            if (score < _bestScore)
                            {
                                _bestScore = score;
                                _bestView = view;
                                _bestLayer = layer;
                                _bestX = ix;
                                _bestZ = iz;
                            }
                        }
                }
            }

            // Show or hide the uploaded cells.
            for (int i = 0; i < _all.Count; i++)
            {
                Cell c = _all[i];
                if (!c.Uploaded || c.Renderer == null) continue;
                bool on = false;
                if (c.View.IsVisible)
                {
                    double cx = camX - c.View.Node.Area.X0, cz = camZ - c.View.Node.Area.Z0;
                    on = Wanted(c.Layer, c.MinX, c.MinZ, c.MaxX, c.MaxZ, cx, cz) && BlockReady(c.View, BlockOfCell(c.View, c), cx, cz);
                }
                if (on != c.Shown)
                {
                    c.Shown = on;
                    c.Renderer.enabled = on;
                    if (c.Colliders != null && c.Colliders.Count > 0) c.View.CollidersDirty = true;
                }
                if (on)
                {
                    ShownTris += c.Tris;
                    ShownCount++;
                }
            }

            Dispatch(nowMs);
            Evict(nowMs);
        }

        /// <summary>The B1 block (part index of <see cref="TileLayers.Buildings"/>) holding a cell.</summary>
        private static int BlockOfCell(TileView view, Cell c)
        {
            int per = MeshParts.BlocksPerSide(view.Node.Area.Size, BandConfig.B1BlockM);
            return MeshParts.BlockOf(0.5 * (c.MinX + c.MaxX), 0.5 * (c.MinZ + c.MaxZ), BandConfig.B1BlockM, per);
        }

        /// <summary>True when every cell of both B0 rings in B1 block <paramref name="block"/> that the camera
        /// (tile-local cx, cz) wants is uploaded: the block may then hand its inner range to B0.</summary>
        public bool BlockReady(TileView view, int block, double cx, double cz)
        {
            TileExtras e = view.Extras;
            if (e == null || e.Source == null) return false;
            int blocks = MeshParts.BlocksPerSide(view.Node.Area.Size, BandConfig.B1BlockM);
            double b0x = (block % blocks) * BandConfig.B1BlockM, b0z = (block / blocks) * BandConfig.B1BlockM;
            float reach = _bands.B0OuterM + BandConfig.FadeM + MarginM;
            if (BandConfig.NearestDistance(b0x, b0z, b0x + BandConfig.B1BlockM, b0z + BandConfig.B1BlockM, cx, cz) > reach) return true;
            Dictionary<int, Cell> map;
            _cells.TryGetValue(view, out map);
            for (int layer = 0; layer < 2; layer++)
            {
                int per = BuildingDetailMesher.CellsPerSide(e.Source, Subdivision(layer));
                double cell = view.Node.Area.Size / per;
                int ix0 = Math.Max(0, (int)Math.Floor(b0x / cell + 1e-6)), iz0 = Math.Max(0, (int)Math.Floor(b0z / cell + 1e-6));
                int ix1 = Math.Min(per - 1, (int)Math.Ceiling((b0x + BandConfig.B1BlockM) / cell - 1e-6) - 1);
                int iz1 = Math.Min(per - 1, (int)Math.Ceiling((b0z + BandConfig.B1BlockM) / cell - 1e-6) - 1);
                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        if (!Wanted(layer, ix * cell, iz * cell, (ix + 1) * cell, (iz + 1) * cell, cx, cz)) continue;
                        Cell c;
                        if (map == null || !map.TryGetValue(Key(layer, ix, iz), out c) || !c.Uploaded || c.Failed) return false;
                    }
            }
            return true;
        }

        private void Dispatch(double nowMs)
        {
            if (_job != null || _bestView == null || nowMs - _lastDispatchMs < DispatchIntervalMs) return;
            TileView view = _bestView;
            TileExtras e = view.Extras;
            Dictionary<int, Cell> map;
            if (!_cells.TryGetValue(view, out map))
            {
                map = new Dictionary<int, Cell>();
                _cells.Add(view, map);
            }
            int layer = _bestLayer, sub = Subdivision(layer);
            int per = BuildingDetailMesher.CellsPerSide(e.Source, sub);
            double cell = view.Node.Area.Size / per;
            var c = new Cell
            {
                View = view, Layer = layer, CX = _bestX, CZ = _bestZ, MinX = _bestX * cell, MinZ = _bestZ * cell, MaxX = (_bestX + 1) * cell,
                MaxZ = (_bestZ + 1) * cell, Data = new MeshData(4096, 12288), Gen = new GenColliders(), LastUsedMs = nowMs,
            };
            map.Add(Key(layer, c.CX, c.CZ), c);
            _all.Add(c);
            _job = c;
            _lastDispatchMs = nowMs;
            var src = e.Source;
            var sampler = e.Sampler;
            BuildingOptions options = OptionsFor(layer);
            Task.Run(() =>
            {
                try
                {
                    BuildingDetailMesher.BuildCell(src, sampler, c.CX, c.CZ, sub, options, c.Data, c.Gen);
                }
                catch (Exception ex)
                {
                    c.Error = ex;
                }
                c.Done = true;
            });
        }

        /// <summary>Uploads the finished cell when <paramref name="allowed"/> (the tile uploads left budget).</summary>
        public void UploadReady(bool allowed, double nowMs)
        {
            Cell c = _job;
            if (c == null || !c.Done) return;
            if (c.View == null)
            {
                _job = null; // released while building
                return;
            }
            if (!allowed) return;
            _job = null;
            if (c.Error != null)
            {
                Debug.LogWarning("DetailCells: " + (c.Layer == NearLayer ? "near" : "lite") + " cell " + c.CX + "," + c.CZ + " of " + c.View.Node + " failed: " +
                                 c.Error.Message);
                c.Data = null;
                c.Uploaded = true;
                c.Failed = true; // its B1 block keeps standing in for it
                return;
            }
            c.Tris = c.Data.TriangleCount;
            if (c.Tris > 0)
            {
                c.Mesh = MeshUpload.CreateWhole(c.Data, c.Layer == NearLayer ? "b0_near" : "b0_lite");
                c.Go = new GameObject((c.Layer == NearLayer ? "b0 near " : "b0 lite ") + c.CX + "," + c.CZ);
                c.Go.transform.SetParent(c.View.Transform, false);
                c.Go.AddComponent<MeshFilter>().sharedMesh = c.Mesh;
                c.Renderer = c.Go.AddComponent<MeshRenderer>();
                c.Renderer.sharedMaterial = _materials[c.Layer];
                c.Renderer.shadowCastingMode = ShadowCastingMode.On;
                c.Renderer.lightProbeUsage = LightProbeUsage.Off;
                c.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                c.Renderer.allowOcclusionWhenDynamic = false;
                c.Renderer.enabled = false;
            }
            if (c.Gen.Boxes.Count > 0 || c.Gen.Ramps.Count > 0)
            {
                c.Colliders = new StructureColliders();
                c.Colliders.AddFrom(c.Gen);
            }
            c.Data = null;
            c.Gen = null;
            c.Uploaded = true;
            c.LastUsedMs = nowMs;
        }

        /// <summary>Appends the colliders of the view's shown cells, once per house: a lite cell's colliders cover its near
        /// cells (the same houses), so a near cell adds its own only while its lite cell is not shown.</summary>
        public void AppendColliders(TileView view, StructureColliders into)
        {
            Dictionary<int, Cell> map;
            if (!_cells.TryGetValue(view, out map)) return;
            int sub = BuildingBandTable.NearSubdivision;
            foreach (Cell c in map.Values)
            {
                if (!c.Shown || c.Colliders == null) continue;
                if (c.Layer == NearLayer)
                {
                    Cell lite;
                    if (map.TryGetValue(Key(LiteLayer, c.CX / sub, c.CZ / sub), out lite) && lite.Shown) continue;
                }
                into.Boxes.AddRange(c.Colliders.Boxes);
                into.Ramps.AddRange(c.Colliders.Ramps);
            }
        }

        /// <summary>The view was hidden: hide its cells (they stay cached).</summary>
        public void Hide(TileView view)
        {
            Dictionary<int, Cell> map;
            if (!_cells.TryGetValue(view, out map)) return;
            foreach (Cell c in map.Values)
            {
                c.Shown = false;
                if (c.Renderer != null) c.Renderer.enabled = false;
            }
        }

        /// <summary>The view is released: destroy its cells.</summary>
        public void Release(TileView view)
        {
            Dictionary<int, Cell> map;
            if (!_cells.TryGetValue(view, out map)) return;
            foreach (Cell c in map.Values) Destroy(c);
            _cells.Remove(view);
            _all.RemoveAll(c => c.View == null);
        }

        private void Evict(double nowMs)
        {
            int over = _all.Count - _bands.CellCacheSize;
            while (over > 0)
            {
                Cell oldest = null;
                for (int i = 0; i < _all.Count; i++)
                {
                    Cell c = _all[i];
                    if (c == _job || c.LastUsedMs >= nowMs) continue; // in flight or wanted this frame
                    if (oldest == null || c.LastUsedMs < oldest.LastUsedMs) oldest = c;
                }
                if (oldest == null) break;
                Dictionary<int, Cell> map;
                if (_cells.TryGetValue(oldest.View, out map)) map.Remove(Key(oldest.Layer, oldest.CX, oldest.CZ));
                if (oldest.Shown && oldest.Colliders != null) oldest.View.CollidersDirty = true;
                Destroy(oldest);
                _all.Remove(oldest);
                over--;
            }
        }

        private void Destroy(Cell c)
        {
            if (c.Go != null) UnityEngine.Object.Destroy(c.Go);
            if (c.Mesh != null) UnityEngine.Object.Destroy(c.Mesh);
            c.Go = null;
            c.Mesh = null;
            c.Renderer = null;
            c.Shown = false;
            c.View = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _all.Count; i++) Destroy(_all[i]);
            _all.Clear();
            _cells.Clear();
            for (int layer = 0; layer < 2; layer++)
            {
                if (_materials[layer] != null) UnityEngine.Object.Destroy(_materials[layer]);
                _materials[layer] = null;
            }
        }
    }
}
