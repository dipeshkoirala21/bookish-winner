using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Rendering;
using Ghumante.World.Sacred;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Ghumante.World.Streaming
{
    /// <summary>
    /// The Unity side of world streaming (ARCHITECTURE.md 7.2): owns a <see cref="StreamingScheduler"/> (selection,
    /// worker builds, budgets, swap rule, ground query) and implements its <see cref="ITileSink"/> with pooled
    /// <see cref="TileView"/> GameObjects. Uploads go chunk by chunk (<see cref="UploadChunk"/>, at most
    /// <see cref="TileBuild.UploadChunkVertices"/> vertices, so a dense city layer spreads over several frames) through
    /// the advanced Mesh API straight from the builds' arrays (no per-frame garbage; 16-bit indices) and the CPU copies
    /// are released after upload. Tile views sit at <c>area south-west corner - floating origin</c>; meshes are never
    /// rebuilt when the origin moves. Owned by <see cref="WorldRoot"/>; main thread only.
    /// <para>
    /// W2 (<see cref="UpdateView"/>, once per frame after <see cref="Tick"/>): the building bands of W2_DESIGN 2.4 (B1
    /// blocks, B2 blocks and the B3 layer switched by their distance range around the band centre
    /// (<see cref="BandAnchor"/>: the player while the camera chases it), the band materials dithering the exact 4 m
    /// cross-fade; B0 cells from <see cref="DetailCells"/>, with a B1 block standing in only where B0 does not draw a
    /// house), the hero LODs (<see cref="HeroLodBudget"/>), the structure colliders of heroes and B0 cells (registered
    /// with the ground query while their tile is visible), and the per-frame render counters.
    /// </para>
    /// <para>
    /// Detail pass: the bridge and roundabout pieces (<see cref="LodPiece"/>: one LOD per span or site by the camera's
    /// distance, <see cref="PieceLod"/>), the colliders of the ornaments and the instanced dressing (registered with the
    /// heroes' and the B0 cells'), and in-place layer refreshes (<see cref="ITileRefreshSink"/>: the season's terrain
    /// and areas swap in without a hole).
    /// </para>
    /// </summary>
    public sealed class WorldStreamer : ITileSink, ITileRefreshSink, IDisposable
    {
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        private readonly StreamingScheduler _scheduler;
        private readonly Transform _root;
        private readonly WorldMaterialSet _materials;
        private readonly TileGroundQuery _ground;
        private readonly Dictionary<SelectedNode, TileView> _views = new Dictionary<SelectedNode, TileView>(512);
        private readonly Stack<TileView> _pool = new Stack<TileView>(64);
        private readonly List<TileView> _detailViews = new List<TileView>(32);
        private readonly BandConfig _bands;
        private readonly HeroLodBudget _heroBudget;
        private readonly DetailCells _cells;
        private readonly Material _b0, _b1, _b1Full, _b2, _b3;
        private HeroLodBudget.Entry[] _heroEntries = new HeroLodBudget.Entry[32];
        private TileView[] _heroViews = new TileView[32];
        private int[] _heroIndex = new int[32];
        private WorldPos _origin;
        private RenderStats _stats;
        private bool _disposed;
        private readonly int _tier;

        /// <summary>A detail tile became visible (its decoded tile and extras) or was hidden. Raised on the main thread.</summary>
        public event Action<TileId, TileExtras> DetailShown;

        public event Action<TileId> DetailHidden;

        /// <summary>Starts streaming <paramref name="pack"/> (the streamer owns it from now on and closes it after the
        /// last running build when disposed). <paramref name="tier"/>: 0 Low, 1 Mid, 2 High (band radii, caches).</summary>
        public WorldStreamer(Transform parent, WorldMaterialSet materials, PackReader pack, StreamingConfig config, TileSelector selector,
                             TileGroundQuery ground, MeshingSettings meshing, int tier = 1)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            _materials = materials;
            _ground = ground;
            _tier = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            _bands = BandConfig.ForTier(tier);
            _heroBudget = new HeroLodBudget(_bands.HeroBudgetTris, _bands.HeroLod0Allowed);
            _b0 = BandMaterial(materials.bandB0, BuildingBandLayer.B0, false);
            _b1 = BandMaterial(materials.bandB1, BuildingBandLayer.B1, false);
            _b1Full = BandMaterial(materials.bandB1Full, BuildingBandLayer.B1, true);
            _b2 = BandMaterial(materials.bandB2, BuildingBandLayer.B2, false);
            _b3 = BandMaterial(materials.bandB3, BuildingBandLayer.B3, false);
            var go = new GameObject("Tiles");
            _root = go.transform;
            if (parent != null) _root.SetParent(parent, false);
            _scheduler = new StreamingScheduler(pack, config, selector, ground, this, new ThreadPoolJobRunner(), NowMs, meshing, true)
            {
                Warning = Debug.LogWarning,
            };
            _cells = new DetailCells(_bands, meshing != null ? meshing.DetailBuildings : new BuildingOptions { Band = BuildingBand.B0KitLite }, _b0, ground, _root);
            _uploadCapBytes = _scheduler.UploadBudgetBytes;
        }

        public StreamingScheduler Scheduler
        {
            get { return _scheduler; }
        }

        /// <summary>Band radii and caches of the tier.</summary>
        public BandConfig Bands
        {
            get { return _bands; }
        }

        /// <summary>Tile views alive (visible, hidden or uploading).</summary>
        public int ViewCount
        {
            get { return _views.Count; }
        }

        /// <summary>Visible level-10 detail views (their extras carry instances and heroes), for the instanced renderers.</summary>
        public IReadOnlyList<TileView> DetailViews
        {
            get { return _detailViews; }
        }

        /// <summary>The floating origin the views are placed for.</summary>
        public WorldPos Origin
        {
            get { return _origin; }
        }

        /// <summary>Counters of the last <see cref="UpdateView"/> (life presenters add theirs on top).</summary>
        public RenderStats RenderStats
        {
            get { return _stats; }
        }

        /// <summary>
        /// One frame: stream around <paramref name="focus"/>. The upload budget follows the frame rate target (1.5 ms at
        /// 60 fps, 2 ms at 30 fps). Tiles and B0 cells share the scheduler's per-frame byte cap (<see cref="B0UploadShare"/>):
        /// B0 chunks (about half a megabyte) take what the tile uploads left, and go first (up to half the cap) when a
        /// build the band needs now is waiting (a house riding brings in) or when they found no room for
        /// <see cref="B0UploadShare.StarvedTicks"/> frames in a row, so neither starves and a tile streaming in never
        /// makes a house late.
        /// </summary>
        public void Tick(WorldPos focus)
        {
            if (_disposed) return;
            _focusX = focus.X;
            _focusZ = focus.Z;
            _hasFocus = true;
            double budget = Application.targetFrameRate >= 50 ? 1.5 : 2.0;
            _scheduler.UploadBudgetMs = budget;
            long cap = _uploadCapBytes, b0 = 0;
            bool b0Waiting = _cells.NextUploadBytes > 0;
            double start = NowMs();
            long first = _b0Share.First(cap, b0Waiting, b0Waiting && _cells.UrgentUploadWaiting);
            if (first > 0) b0 = _cells.Upload(first, true);
            double b0Ms = NowMs() - start;
            _scheduler.UploadBudgetBytes = Math.Max(1, cap - b0);
            _scheduler.UploadBudgetMs = Math.Max(0.1, budget - b0Ms);
            _tickUploadBytes = 0;
            _scheduler.Tick(focus.X, focus.Z);
            StreamingStats st = _scheduler.Stats;
            long after = _b0Share.After(cap, b0 + _tickUploadBytes, b0Waiting, first > 0, budget - b0Ms - st.UploadMs);
            if (after > 0) b0 += _cells.Upload(after, false);
            _b0Share.EndFrame(b0Waiting, b0);
            _scheduler.UploadBudgetBytes = cap;
        }

        /// <summary>How tiles and B0 cells share the frame's upload cap.</summary>
        private readonly B0UploadShare _b0Share = new B0UploadShare();

        /// <summary>Where the building bands are measured from (the player while the camera chases it; owner playtest #4).</summary>
        private readonly BandAnchor _bandAnchor = new BandAnchor();

        private double _focusX, _focusZ;
        private bool _hasFocus;

        private readonly long _uploadCapBytes;
        private long _tickUploadBytes;

        /// <summary>Re-place every view for a new floating origin (exact: computed from the doubles each time).</summary>
        public void Rebase(WorldPos origin)
        {
            _origin = origin;
            foreach (KeyValuePair<SelectedNode, TileView> kv in _views) kv.Value.Place(_origin);
        }

        /// <summary>
        /// Per-frame view work: band switching around the band centre (<see cref="BandAnchor"/>: the streaming focus of the
        /// last <see cref="Tick"/>, the player, while the camera chases it, so a swaying or swinging chase camera never
        /// sweeps a band edge across the houses; the camera otherwise), B0 cells, hero LODs, colliders and counters.
        /// <paramref name="cameraScene"/> is the camera's scene position; <paramref name="verticalFovDeg"/> its vertical
        /// field of view (hero screen heights).
        /// </summary>
        public void UpdateView(Vector3 cameraScene, float verticalFovDeg, float nowS)
        {
            if (_disposed) return;
            double camX = _origin.X + cameraScene.x, camZ = _origin.Z + cameraScene.z;
            float camY = _origin.Y + cameraScene.y;
            _bandAnchor.Follow(camX, camZ, _focusX, _focusZ, _hasFocus);
            double bandX = _bandAnchor.X, bandZ = _bandAnchor.Z;
            // The shader's band centre and the bands' switching below use the same point (the shader reads only x and z).
            Shader.SetGlobalVector(WorldShaders.BandCentre, new Vector4((float)(bandX - _origin.X), cameraScene.y, (float)(bandZ - _origin.Z), 0f));
            _stats = default(RenderStats); // the instanced renderers and life presenters add theirs after this call

            _cells.Update(_detailViews, bandX, bandZ, NowMs());
            for (int v = 0; v < _detailViews.Count; v++)
            {
                UpdateBands(_detailViews[v], bandX, bandZ);
                UpdatePieces(_detailViews[v], camX, camZ);
            }
            UpdateHeroes(camX, camY, camZ, verticalFovDeg, nowS);
            for (int v = 0; v < _detailViews.Count; v++)
            {
                TileView view = _detailViews[v];
                if (view.CollidersDirty) RegisterColliders(view);
            }
            foreach (KeyValuePair<SelectedNode, TileView> kv in _views)
            {
                TileView view = kv.Value;
                if (!view.IsVisible) continue;
                _stats.TerrainTris += view.LayerTris(TileLayers.Terrain);
                _stats.RoadTris += view.LayerTris(TileLayers.Roads) + view.EnabledTris(TileLayers.Bridges); // bridges share the roads slice
                _stats.HeroTris += view.EnabledTris(TileLayers.Ornaments); // roundabout statues count with the landmarks
                _stats.DecalTris += view.LayerTris(TileLayers.RoadDecals);
                _stats.AreaTris += view.LayerTris(TileLayers.Areas);
                _stats.B1Tris += view.EnabledTris(TileLayers.Buildings);
                _stats.B2Tris += view.EnabledTris(TileLayers.BuildingsFar);
                _stats.B3Tris += view.EnabledTris(TileLayers.BuildingsBlock);
            }
            _stats.B0Tris = _cells.ShownTris;
            _stats.Cells = _cells.ShownCount;
        }

        /// <summary>Adds the instanced renderers' counts to this frame's statistics.</summary>
        internal void AddInstanceStats(int treeTris, int trees, int propTris, int props, int draws)
        {
            _stats.TreeTris += treeTris;
            _stats.Trees += trees;
            _stats.PropTris += propTris;
            _stats.Props += props;
            _stats.InstancedDraws += draws;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Bands

        private Material BandMaterial(Material source, BuildingBandLayer band, bool b1Full)
        {
            if (source == null) source = _materials.buildings;
            if (source == null) return null;
            var m = new Material(source) { name = source.name + " (" + band + (b1Full ? " full" : "") + ")", hideFlags = HideFlags.DontSave };
            float inner, outer;
            _bands.Range(band, b1Full, out inner, out outer);
            m.SetVector(WorldShaders.BandRange, new Vector4(inner, outer, BandConfig.FadeM, 0f));
            m.EnableKeyword(WorldShaders.KeywordBandFade);
            return m;
        }

        private void UpdateBands(TileView view, double camX, double camZ)
        {
            double cx = camX - view.Node.Area.X0, cz = camZ - view.Node.Area.Z0;
            float i1, o1, i2, o2, i3, o3;
            _bands.Range(BuildingBandLayer.B2, false, out i2, out o2);
            _bands.Range(BuildingBandLayer.B3, false, out i3, out o3);
            List<TileView.ChunkRef> chunks = view.Chunks;
            for (int k = 0; k < chunks.Count; k++)
            {
                TileView.ChunkRef c = chunks[k];
                bool on;
                switch (c.Layer)
                {
                    case TileLayers.Buildings:
                    {
                        bool ready = _cells.BlockReady(view, c.Part, cx, cz);
                        _bands.Range(BuildingBandLayer.B1, !ready, out i1, out o1);
                        on = BandConfig.Touches(c.MinX, c.MinZ, c.MaxX, c.MaxZ, cx, cz, i1, o1);
                        Material want = ready ? _b1 : _b1Full;
                        if (on && want != null && c.Renderer.sharedMaterial != want) c.Renderer.sharedMaterial = want;
                        break;
                    }
                    case TileLayers.BuildingsFar:
                        on = BandConfig.Touches(c.MinX, c.MinZ, c.MaxX, c.MaxZ, cx, cz, i2, o2);
                        break;
                    case TileLayers.BuildingsBlock:
                        on = BandConfig.Touches(c.MinX, c.MinZ, c.MaxX, c.MaxZ, cx, cz, i3, o3);
                        break;
                    default:
                        continue;
                }
                if (c.Renderer.enabled != on) c.Renderer.enabled = on;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Bridge spans and decorated roundabouts: one LOD per piece by the camera's plan distance

        private void UpdatePieces(TileView view, double camX, double camZ)
        {
            LodPiece[] pieces = view.Extras != null ? view.Extras.Pieces : null;
            if (pieces == null || pieces.Length == 0) return;
            double cx = camX - view.Node.Area.X0, cz = camZ - view.Node.Area.Z0;
            for (int i = 0; i < pieces.Length; i++)
            {
                int current = view.PieceLod[i];
                int lod = PieceLod.Choose(pieces[i], _tier, pieces[i].DistanceTo(cx, cz), current);
                if (lod == current) continue;
                view.ShowPieceLod(pieces[i].Layer, pieces[i].Index, lod);
                view.PieceLod[i] = lod;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Heroes

        private void UpdateHeroes(double camX, float camY, double camZ, float fovDeg, float nowS)
        {
            int n = 0;
            double tanHalf = Math.Tan(Math.Max(1f, fovDeg) * 0.5 * Math.PI / 180.0);
            for (int v = 0; v < _detailViews.Count; v++)
            {
                TileView view = _detailViews[v];
                HeroPiece[] heroes = view.Extras != null ? view.Extras.Heroes : null;
                if (heroes == null) continue;
                for (int h = 0; h < heroes.Length; h++)
                {
                    if (n == _heroEntries.Length)
                    {
                        Array.Resize(ref _heroEntries, n * 2);
                        Array.Resize(ref _heroViews, n * 2);
                        Array.Resize(ref _heroIndex, n * 2);
                    }
                    HeroPiece p = heroes[h];
                    double dx = view.Node.Area.X0 + p.CX - camX, dz = view.Node.Area.Z0 + p.CZ - camZ;
                    double dy = 0.5 * (p.GroundY + p.TopY) - camY;
                    double d = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                    float screen = (float)((p.TopY - p.GroundY) / (2.0 * d * tanHalf));
                    _heroEntries[n] = new HeroLodBudget.Entry
                    {
                        ScreenHeight = screen, Tris0 = p.Tris0, Tris1 = p.Tris1, Tris2 = p.Tris2, Tris3 = p.Tris3,
                        CurrentLod = view.HeroLod[h], LastChangeS = view.HeroChanged[h],
                    };
                    _heroViews[n] = view;
                    _heroIndex[n] = h;
                    n++;
                }
            }
            _stats.HeroTris = n > 0 ? _heroBudget.Allocate(_heroEntries, n, nowS) : 0;
            _stats.Heroes = n;
            for (int i = 0; i < n; i++)
            {
                TileView view = _heroViews[i];
                int h = _heroIndex[i];
                int lod = _heroEntries[i].Lod;
                if (view.HeroLod[h] != lod || !view.HeroShown[h])
                {
                    view.ShowHeroLod(h, lod);
                    view.HeroChanged[h] = _heroEntries[i].LastChangeS;
                }
                view.HeroLod[h] = lod;
                _heroViews[i] = null;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Structure colliders, registered while the tile is visible: the heroes under the tile key and the static
        // dressing (ornaments, generated trunks, poles, parked vehicles: hundreds to a few thousand colliders) under
        // tileKey | TileGroundQuery.StaticSetKeyBit, each built once when the tile becomes visible and dropped when it
        // hides. The B0 cells register their own small sets, one per shown cell (B0CellSet), so a cell showing or hiding
        // never rebuilds these.

        private void RegisterColliders(TileView view)
        {
            view.CollidersDirty = false;
            if (_ground == null) return;
            ulong key = view.Node.Area.Key;
            if (!view.IsVisible)
            {
                if (view.CollidersRegistered) _ground.Unregister(key);
                view.CollidersRegistered = false;
                UnregisterStatic(view);
                return;
            }
            RegisterStatic(view);
            // Every kind (boxes, ramps, and the cylinders and walls of statues, piers and parapets); the ground copies them.
            StructureColliders heroes = view.Extras != null ? view.Extras.HeroColliders : null;
            if (heroes == null || heroes.Count == 0)
            {
                if (view.CollidersRegistered) _ground.Unregister(key);
                view.CollidersRegistered = false;
                return;
            }
            _ground.Register(key, heroes);
            view.CollidersRegistered = true;
        }

        /// <summary>The tile's static dressing colliders, once per visible spell (no-op when already registered).</summary>
        private void RegisterStatic(TileView view)
        {
            if (view.StaticRegistered || _ground == null) return;
            StructureColliders dressing = view.Extras != null ? view.Extras.Colliders : null;
            if (dressing == null || dressing.Count == 0) return;
            _ground.Register(view.Node.Area.Key | TileGroundQuery.StaticSetKeyBit, dressing);
            view.StaticRegistered = true;
        }

        private void UnregisterStatic(TileView view)
        {
            if (view.StaticRegistered && _ground != null) _ground.Unregister(view.Node.Area.Key | TileGroundQuery.StaticSetKeyBit);
            view.StaticRegistered = false;
        }

        // ---------------------------------------------------------------------------------------------------------
        // ITileSink

        public void Upload(TileBuild build, int chunk)
        {
            TileView view;
            if (!_views.TryGetValue(build.Node, out view))
            {
                view = _pool.Count > 0 ? _pool.Pop() : new TileView(_root, _materials);
                view.Bind(build.Node, _origin);
                _views.Add(build.Node, view);
            }
            UploadChunk c = build.Chunks[chunk];
            view.AddChunk(c, MeshUpload.Create(build, chunk), MaterialFor(c.Layer));
            _tickUploadBytes += build.ChunkBytes(chunk); // the B0 cells take what is left of the frame's cap
        }

        private Material MaterialFor(int layer)
        {
            switch (layer)
            {
                case TileLayers.Buildings: return _b1Full != null ? _b1Full : _materials.buildings;
                case TileLayers.BuildingsFar: return _b2 != null ? _b2 : _materials.buildings;
                case TileLayers.BuildingsBlock: return _b3 != null ? _b3 : _materials.buildings;
                // Decks, railings, piers and trench walls: their own material, which fades whole between the camera
                // and the player (World/README "Look"); the driving surface on the deck stays on the road layer.
                case TileLayers.Bridges: return _materials.ForLayer(layer);
                // Statues, gates and garden furniture are landmarks: the hero material (outlined, occluder fade).
                case TileLayers.Ornaments: return _materials.heroes != null ? _materials.heroes : _materials.buildings;
                default: return _materials.ForLayer(layer);
            }
        }

        // ITileRefreshSink

        public void UploadRefresh(TileBuild build, int chunk)
        {
            TileView view;
            if (!_views.TryGetValue(build.Node, out view)) return;
            view.Stage(build.Chunks[chunk], MeshUpload.Create(build, chunk));
            _tickUploadBytes += build.ChunkBytes(chunk);
        }

        public void CommitRefresh(SelectedNode node, int layerMask)
        {
            TileView view;
            if (!_views.TryGetValue(node, out view)) return;
            view.CommitStaged(layerMask, this);
        }

        public void AbortRefresh(SelectedNode node)
        {
            TileView view;
            if (_views.TryGetValue(node, out view)) view.AbortStaged();
        }

        internal Material LayerMaterial(int layer)
        {
            return MaterialFor(layer);
        }

        public void Ready(SelectedNode node, TileExtras extras)
        {
            TileView view;
            if (!_views.TryGetValue(node, out view)) return;
            // New extras for a view: its static set (if any) is dropped and registered from them on the next view update.
            UnregisterStatic(view);
            view.SetExtras(extras);
            if (view.IsVisible) view.CollidersDirty = true;
        }

        public void SetVisible(SelectedNode node, bool visible)
        {
            TileView view;
            if (!_views.TryGetValue(node, out view)) return;
            if (view.IsVisible == visible) return;
            view.SetVisible(visible);
            bool detail = view.Extras != null && view.Extras.Source != null && node.DrawsDetail;
            if (!detail) return;
            if (visible)
            {
                _detailViews.Add(view);
                view.CollidersDirty = true;
                RegisterColliders(view);
                Raise(DetailShown, node.Area, view.Extras);
            }
            else
            {
                _detailViews.Remove(view);
                _cells.Hide(view);
                view.CollidersDirty = true;
                RegisterColliders(view);
                Raise(DetailHidden, node.Area);
            }
        }

        public void Release(SelectedNode node)
        {
            TileView view;
            if (!_views.TryGetValue(node, out view)) return;
            if (view.IsVisible) SetVisible(node, false);
            if (view.CollidersRegistered && _ground != null) _ground.Unregister(node.Area.Key);
            view.CollidersRegistered = false;
            UnregisterStatic(view);
            _cells.Release(view);
            _views.Remove(node);
            view.Clear();
            _pool.Push(view);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scheduler.Dispose(); // releases every view through Release
            foreach (KeyValuePair<SelectedNode, TileView> kv in _views) kv.Value.Clear();
            _views.Clear();
            _pool.Clear();
            _detailViews.Clear();
            _cells.Dispose();
            DestroyMaterial(_b0);
            DestroyMaterial(_b1);
            DestroyMaterial(_b1Full);
            DestroyMaterial(_b2);
            DestroyMaterial(_b3);
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }

        private static void DestroyMaterial(Material m)
        {
            if (m != null) UnityEngine.Object.Destroy(m);
        }

        private static void Raise(Action<TileId, TileExtras> handler, TileId id, TileExtras e)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<TileId, TileExtras>)d)(id, e);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private static void Raise(Action<TileId> handler, TileId id)
        {
            if (handler == null) return;
            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<TileId>)d)(id);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        internal static double NowMs()
        {
            return Stopwatch.GetTimestamp() * TicksToMs;
        }
    }

    /// <summary>
    /// A pooled GameObject drawing one selected node: a child per layer holding one renderer per upload chunk
    /// (<see cref="UploadChunk"/>) with the layer's material. Terrain, buildings and heroes cast shadows; road, decal
    /// and area overlays do not. Light probes are off, so the renderers get the scene's ambient probe (the SH the toon
    /// shader samples). The chunk GameObjects stay with the view when it goes back to the pool; meshes are GPU-only (not
    /// readable, so they cannot be refilled) and are destroyed with <see cref="Clear"/>. W2: every chunk remembers its
    /// part and bounds (<see cref="Chunks"/>) so the streamer can switch band blocks and hero LODs, and a detail view
    /// holds its <see cref="TileExtras"/>.
    /// </summary>
    public sealed class TileView
    {
        /// <summary>One uploaded chunk: its layer, part, renderer, tile-local horizontal bounds and triangles.</summary>
        public sealed class ChunkRef
        {
            public int Layer, Part, Tris;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public float MinX, MinZ, MaxX, MaxZ;
        }

        private readonly GameObject _root;
        private readonly Transform _transform;
        private readonly WorldMaterialSet _materials;
        private readonly GameObject[] _layers = new GameObject[TileLayers.Count];
        private readonly List<ChunkRef>[] _parts = new List<ChunkRef>[TileLayers.Count];
        private readonly int[] _used = new int[TileLayers.Count];
        private readonly int[] _layerTris = new int[TileLayers.Count];
        private readonly List<ChunkRef> _chunks = new List<ChunkRef>(16);
        private SelectedNode _node;

        /// <summary>Hero LOD state (parallel to <see cref="TileExtras.Heroes"/>).</summary>
        internal int[] HeroLod = new int[0];

        internal float[] HeroChanged = new float[0];
        internal bool[] HeroShown = new bool[0];
        internal bool CollidersDirty, CollidersRegistered;

        /// <summary>True while the tile's static dressing colliders are registered (key | <see cref="TileGroundQuery.StaticSetKeyBit"/>).</summary>
        internal bool StaticRegistered;

        /// <summary>Shown LOD of each bridge and ornament piece (parallel to <see cref="TileExtras.Pieces"/>; -1 none).</summary>
        internal int[] PieceLod = new int[0];

        private readonly List<UploadChunk> _stagedChunks = new List<UploadChunk>(4);
        private readonly List<Mesh> _stagedMeshes = new List<Mesh>(4);

        public TileView(Transform parent, WorldMaterialSet materials)
        {
            _materials = materials;
            _root = new GameObject("tile");
            _root.SetActive(false);
            _transform = _root.transform;
            _transform.SetParent(parent, false);
            for (int i = 0; i < TileLayers.Count; i++)
            {
                var go = new GameObject(TileLayers.Name(i));
                go.transform.SetParent(_transform, false);
                go.SetActive(false);
                _layers[i] = go;
                _parts[i] = new List<ChunkRef>(1);
            }
        }

        public SelectedNode Node
        {
            get { return _node; }
        }

        /// <summary>The view's transform (at the area's south-west corner): B0 cells are parented here.</summary>
        public Transform Transform
        {
            get { return _transform; }
        }

        public bool IsVisible { get; private set; }

        /// <summary>Decoded tile, sampler, instances and heroes (null until ready; null for terrain-only nodes' extras
        /// fields).</summary>
        public TileExtras Extras { get; private set; }

        /// <summary>Every live chunk of the view.</summary>
        public List<ChunkRef> Chunks
        {
            get { return _chunks; }
        }

        internal void Bind(SelectedNode node, WorldPos origin)
        {
            _node = node;
#if UNITY_EDITOR
            _root.name = node.ToString();
#endif
            Place(origin);
        }

        /// <summary>Put the view at its area's south-west corner relative to the floating origin.</summary>
        public void Place(WorldPos origin)
        {
            _transform.localPosition = new Vector3((float)(_node.Area.X0 - origin.X), 0f, (float)(_node.Area.Z0 - origin.Z));
        }

        /// <summary>Triangles of a layer (all its chunks).</summary>
        public int LayerTris(int layer)
        {
            return _layerTris[layer];
        }

        /// <summary>Triangles of a layer's enabled chunks.</summary>
        public int EnabledTris(int layer)
        {
            List<ChunkRef> parts = _parts[layer];
            int t = 0;
            for (int k = 0; k < _used[layer]; k++)
                if (parts[k].Renderer.enabled) t += parts[k].Tris;
            return t;
        }

        /// <summary>Show one more chunk mesh (the view takes ownership of the mesh).</summary>
        internal void AddChunk(UploadChunk c, Mesh mesh, Material material)
        {
            int layer = c.Layer;
            List<ChunkRef> parts = _parts[layer];
            ChunkRef chunk;
            if (_used[layer] < parts.Count)
            {
                chunk = parts[_used[layer]];
            }
            else
            {
                var go = new GameObject(TileLayers.Name(layer));
                go.transform.SetParent(_layers[layer].transform, false);
                chunk = new ChunkRef { Filter = go.AddComponent<MeshFilter>(), Renderer = go.AddComponent<MeshRenderer>() };
                MeshRenderer r = chunk.Renderer;
                bool overlay = layer == TileLayers.Roads || layer == TileLayers.Areas || layer == TileLayers.RoadDecals;
                r.shadowCastingMode = overlay ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                r.allowOcclusionWhenDynamic = false;
                parts.Add(chunk);
            }
            _used[layer]++;
            chunk.Layer = layer;
            chunk.Part = c.Part;
            chunk.Tris = c.IndexCount / 3;
            chunk.MinX = c.MinX;
            chunk.MinZ = c.MinZ;
            chunk.MaxX = c.MaxX;
            chunk.MaxZ = c.MaxZ;
            chunk.Renderer.sharedMaterial = material != null ? material : _materials.ForLayer(layer);
            // Hero, bridge and ornament LODs start hidden (the streamer shows one per piece); everything else starts on.
            chunk.Renderer.enabled = layer != TileLayers.Heroes && layer != TileLayers.Bridges && layer != TileLayers.Ornaments;
            chunk.Filter.sharedMesh = mesh;
            chunk.Filter.gameObject.SetActive(true);
            _layers[layer].SetActive(true);
            _layerTris[layer] += chunk.Tris;
            _chunks.Add(chunk);
        }

        internal void SetExtras(TileExtras extras)
        {
            Extras = extras;
            int n = extras != null && extras.Heroes != null ? extras.Heroes.Length : 0;
            if (HeroLod.Length < n)
            {
                HeroLod = new int[n];
                HeroChanged = new float[n];
                HeroShown = new bool[n];
            }
            for (int i = 0; i < HeroLod.Length; i++)
            {
                HeroLod[i] = -1;
                HeroChanged[i] = -1000f;
                HeroShown[i] = false;
            }
            int np = extras != null && extras.Pieces != null ? extras.Pieces.Length : 0;
            if (PieceLod.Length < np) PieceLod = new int[np];
            for (int i = 0; i < PieceLod.Length; i++) PieceLod[i] = -1;
        }

        /// <summary>Enable exactly the chunks of piece <paramref name="index"/>'s <paramref name="lod"/> in a layer
        /// (<see cref="TileLayers.Bridges"/> or <see cref="TileLayers.Ornaments"/>; -1 hides the piece).</summary>
        internal void ShowPieceLod(int layer, int index, int lod)
        {
            List<ChunkRef> parts = _parts[layer];
            int lo = index * 4, hi = lo + 4, want = lod < 0 ? -1 : lo + lod;
            for (int k = 0; k < _used[layer]; k++)
            {
                ChunkRef c = parts[k];
                if (c.Part < lo || c.Part >= hi) continue;
                bool on = c.Part == want;
                if (c.Renderer.enabled != on) c.Renderer.enabled = on;
            }
        }

        /// <summary>Keep a refresh chunk hidden until <see cref="CommitStaged"/> (the view owns the mesh).</summary>
        internal void Stage(UploadChunk c, Mesh mesh)
        {
            _stagedChunks.Add(c);
            _stagedMeshes.Add(mesh);
        }

        /// <summary>Replace the layers of <paramref name="layerMask"/> by the staged chunks (the old meshes are destroyed in
        /// the same frame, so nothing flickers).</summary>
        internal void CommitStaged(int layerMask, WorldStreamer streamer)
        {
            for (int layer = 0; layer < TileLayers.Count; layer++)
                if ((layerMask & (1 << layer)) != 0) ClearLayer(layer);
            for (int i = 0; i < _stagedChunks.Count; i++)
            {
                UploadChunk c = _stagedChunks[i];
                AddChunk(c, _stagedMeshes[i], streamer.LayerMaterial(c.Layer));
            }
            _stagedChunks.Clear();
            _stagedMeshes.Clear();
        }

        /// <summary>Destroy the staged chunks (refresh abandoned).</summary>
        internal void AbortStaged()
        {
            for (int i = 0; i < _stagedMeshes.Count; i++)
                if (_stagedMeshes[i] != null) UnityEngine.Object.Destroy(_stagedMeshes[i]);
            _stagedChunks.Clear();
            _stagedMeshes.Clear();
        }

        private void ClearLayer(int layer)
        {
            List<ChunkRef> parts = _parts[layer];
            for (int k = 0; k < _used[layer]; k++)
            {
                MeshFilter f = parts[k].Filter;
                if (f == null) continue;
                Mesh mesh = f.sharedMesh;
                f.sharedMesh = null;
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
                f.gameObject.SetActive(false);
            }
            _used[layer] = 0;
            _layerTris[layer] = 0;
            for (int i = _chunks.Count - 1; i >= 0; i--)
                if (_chunks[i].Layer == layer) _chunks.RemoveAt(i);
            if (_layers[layer] != null) _layers[layer].SetActive(false);
        }

        /// <summary>Enable exactly the chunks of hero <paramref name="hero"/>'s <paramref name="lod"/>.</summary>
        internal void ShowHeroLod(int hero, int lod)
        {
            List<ChunkRef> parts = _parts[TileLayers.Heroes];
            int want = hero * HeroLodBudget.LodCount + lod;
            int lo = hero * HeroLodBudget.LodCount, hi = lo + HeroLodBudget.LodCount;
            for (int k = 0; k < _used[TileLayers.Heroes]; k++)
            {
                ChunkRef c = parts[k];
                if (c.Part < lo || c.Part >= hi) continue;
                bool on = c.Part == want;
                if (c.Renderer.enabled != on) c.Renderer.enabled = on;
            }
            HeroShown[hero] = true;
        }

        internal void SetVisible(bool visible)
        {
            IsVisible = visible;
            _root.SetActive(visible);
        }

        /// <summary>Destroy the meshes and hide (back to the pool; the chunk GameObjects are kept for reuse).</summary>
        internal void Clear()
        {
            AbortStaged();
            for (int i = 0; i < TileLayers.Count; i++)
            {
                List<ChunkRef> parts = _parts[i];
                for (int k = 0; k < _used[i]; k++)
                {
                    MeshFilter f = parts[k].Filter;
                    if (f == null) continue;
                    Mesh mesh = f.sharedMesh;
                    f.sharedMesh = null;
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
                    f.gameObject.SetActive(false);
                }
                _used[i] = 0;
                _layerTris[i] = 0;
                if (_layers[i] != null) _layers[i].SetActive(false);
            }
            _chunks.Clear();
            Extras = null;
            IsVisible = false;
            CollidersDirty = false;
            CollidersRegistered = false;
            StaticRegistered = false;
            if (_root != null) _root.SetActive(false);
        }
    }

    /// <summary>One <see cref="UploadChunk"/> to a Unity Mesh through the advanced Mesh API (multi-stream, straight from
    /// the build's arrays, no intermediate copies).</summary>
    public static class MeshUpload
    {
        // Stream 0 positions, 1 normals, 2 colours (RGBA8, sRGB values: the shader linearises them), 3 UV0 (roads).
        private static readonly VertexAttributeDescriptor[] Plain =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
        };

        private static readonly VertexAttributeDescriptor[] WithUv =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        [ThreadStatic] private static ushort[] _shorts;

        /// <summary>A whole <see cref="Core.Meshing.MeshData"/> (B0 cells, instanced kit meshes) to a GPU-only mesh;
        /// 16-bit indices when it fits. Main thread.</summary>
        public static Mesh CreateWhole(Core.Meshing.MeshData m, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int vc = m.VertexCount, ic = m.IndexCount;
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? WithUv : Plain);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, 0, 0, vc * 2, 3, Flags);
            if (vc <= 65535)
            {
                if (_shorts == null || _shorts.Length < ic) _shorts = new ushort[Math.Max(ic, 4096)];
                for (int i = 0; i < ic; i++) _shorts[i] = (ushort)m.Indices[i];
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
                mesh.SetIndexBufferData(_shorts, 0, 0, ic, Flags);
            }
            else
            {
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                mesh.SetIndexBufferData(m.Indices, 0, 0, ic, Flags);
            }
            float x0, y0, z0, x1, y1, z1;
            m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
            var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles) { firstVertex = 0, vertexCount = vc, bounds = bounds }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static Mesh Create(TileBuild b, int chunk)
        {
            UploadChunk c = b.Chunks[chunk];
            Core.Meshing.MeshData m = b.Layers[c.Layer];
            int vc = c.VertexCount, ic = c.IndexCount, v0 = c.FirstVertex;
            var mesh = new Mesh();
            mesh.name = TileLayers.Name(c.Layer);
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? WithUv : Plain);
            mesh.SetVertexBufferData(m.Positions, v0 * 3, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, v0 * 3, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, v0 * 4, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, v0 * 2, 0, vc * 2, 3, Flags);
            bool shortIndices = c.UsesShortIndices;
            mesh.SetIndexBufferParams(ic, shortIndices ? IndexFormat.UInt16 : IndexFormat.UInt32);
            if (shortIndices) mesh.SetIndexBufferData(b.ShortIndices[c.Layer], c.FirstIndex, 0, ic, Flags);
            else mesh.SetIndexBufferData(m.Indices, c.FirstIndex, 0, ic, Flags);

            // Earth curvature lowers far vertices in the shader; keep them inside the culling bounds.
            float y0 = c.MinY - b.CurvatureMarginM;
            var bounds = new Bounds(new Vector3((c.MinX + c.MaxX) * 0.5f, (y0 + c.MaxY) * 0.5f, (c.MinZ + c.MaxZ) * 0.5f),
                                    new Vector3(c.MaxX - c.MinX, c.MaxY - y0, c.MaxZ - c.MinZ));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vc,
                bounds = bounds,
            }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true); // frees the CPU copy (ARCHITECTURE 10: meshes are GPU-only)
            return mesh;
        }
    }
}
