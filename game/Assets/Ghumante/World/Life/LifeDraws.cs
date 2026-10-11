using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;

namespace Ghumante.World.Life
{
    /// <summary>
    /// The frame's draw-call budget per tier (W2_DESIGN 10.4 "SRP batches ≤ 120 / 200 / 300") split into slices, and the
    /// caps the life presenters hold (W2 perf pass): <see cref="MovingDraws"/> and <see cref="ParkedDraws"/> for the
    /// traffic presenter, <see cref="CrowdDraws"/> for the crowd presenter. A draw is one instanced or skinned submission
    /// of one pass: an outlined material (Mid and High, <see cref="Outlines"/>) costs two, the forward pass and the
    /// inverted-hull outline; shadow casters are not counted (like the V11 counter's batches). The other slices are what
    /// the rest of the frame is planned at (the static tile layers, the near building cells and heroes, dressing,
    /// wildlife, aircraft and the player); <see cref="Sum"/> of all slices equals <see cref="Total"/>. Engine-free; the
    /// presenters and the budget test share it.
    /// </summary>
    public sealed class LifeDrawBudget
    {
        /// <summary>The whole frame (10.4).</summary>
        public int Total;

        /// <summary>The tier draws the outline pass (Mid and High; <c>ToonLookTier.Outlines</c>).</summary>
        public bool Outlines;

        /// <summary>Moving traffic: plated LOD0 bodies, shared LOD1 and LOD2 bodies and the wheel batches.</summary>
        public int MovingDraws;

        /// <summary>Parked vehicles: near shared bodies, tinted block-outs and boxes, fleet tags.</summary>
        public int ParkedDraws;

        /// <summary>People: skinned near and mid NPCs (outlined where the tier outlines) and the far frame batches,
        /// officers included.</summary>
        public int CrowdDraws;

        /// <summary>Slices of the rest of the frame (other packages hold them): terrain, roads, decals, areas, bridges,
        /// ornaments and the B1/B2/B3 bands; B0 cells and hero replicas; trees, plants and props; animals and birds;
        /// aircraft; the player and their vehicle.</summary>
        public int StaticWorld, NearBuildings, Dressing, Wildlife, Aircraft, Player;

        /// <summary>The traffic presenter's cap (moving and parked).</summary>
        public int TrafficDraws
        {
            get { return MovingDraws + ParkedDraws; }
        }

        /// <summary>Every slice.</summary>
        public int Sum
        {
            get { return StaticWorld + NearBuildings + Dressing + Wildlife + Aircraft + Player + MovingDraws + ParkedDraws + CrowdDraws; }
        }

        /// <summary>Draws of one submission of an outlined (or plain) material on this tier.</summary>
        public int Cost(bool outlined)
        {
            return outlined && Outlines ? 2 : 1;
        }

        /// <summary>The slices of a tier. The life slices are sized to what the sample's busiest anchors draw with every
        /// fallback taken (Low: moving 6-9, parked 5-11 with Asan's 80 parked motorbikes and scooters within 150 m, crowd
        /// 7-15 with one far person at Boudha standing in for a sibling; TrafficDrawBudgetTests), so nothing goes past
        /// them there; elsewhere the plans draw past a slice rather than hide a vehicle or a person.</summary>
        public static LifeDrawBudget ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new LifeDrawBudget
                    {
                        Total = 120, Outlines = false, StaticWorld = 54, NearBuildings = 12, Dressing = 8, Wildlife = 6, Aircraft = 2, Player = 4,
                        MovingDraws = 10, ParkedDraws = 10, CrowdDraws = 14,
                    };
                case 1:
                    return new LifeDrawBudget
                    {
                        Total = 200, Outlines = true, StaticWorld = 70, NearBuildings = 16, Dressing = 14, Wildlife = 10, Aircraft = 4, Player = 6,
                        MovingDraws = 24, ParkedDraws = 14, CrowdDraws = 42,
                    };
                default:
                    return new LifeDrawBudget
                    {
                        Total = 300, Outlines = true, StaticWorld = 92, NearBuildings = 24, Dressing = 24, Wildlife = 16, Aircraft = 6, Player = 8,
                        MovingDraws = 30, ParkedDraws = 14, CrowdDraws = 86,
                    };
            }
        }
    }

    /// <summary>
    /// The distinct batch keys submitted this frame and the draws they cost, with a cap: a presenter asks
    /// <see cref="TryUse"/> before it adds an instance to a batch and falls back to a coarser shared batch when a new
    /// batch would break its cap; its last fallback goes through <see cref="Force"/>, which draws past the cap (counted in
    /// <see cref="Overflow"/>), because a cap may coarsen what the street shows but never hide a vehicle or a person
    /// (owner playtest #6, "absolutely no traffic", and #4, flicker). Fixed capacity (open addressing), cleared per frame;
    /// allocation free. Engine-free.
    /// </summary>
    public sealed class DrawKeySet
    {
        private readonly ulong[] _keys;
        private readonly bool[] _full;
        private readonly int[] _slots;
        private readonly int _mask;
        private int _count;

        /// <param name="capacity">Most distinct keys per frame (rounded up to a power of two, twice over for the
        /// table).</param>
        public DrawKeySet(int capacity = 256)
        {
            int size = 16;
            while (size < capacity * 2) size <<= 1;
            _keys = new ulong[size];
            _full = new bool[size];
            _slots = new int[size];
            _mask = size - 1;
        }

        /// <summary>Distinct keys used since <see cref="Clear"/>.</summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>Draws the used keys cost.</summary>
        public int Draws { get; private set; }

        /// <summary>Keys refused since <see cref="Clear"/> (a new batch would have broken the cap).</summary>
        public int Refused { get; private set; }

        /// <summary>New batches drawn past the cap by <see cref="Force"/> since <see cref="Clear"/>.</summary>
        public int Overflow { get; private set; }

        public void Clear()
        {
            for (int i = 0; i < _count; i++) _full[_slots[i]] = false;
            _count = 0;
            Draws = 0;
            Refused = 0;
            Overflow = 0;
        }

        /// <summary>True when <paramref name="key"/> is already in use this frame (adding to it costs no draw).</summary>
        public bool Has(ulong key)
        {
            return _full[Find(key)];
        }

        /// <summary>Draws a new batch of <paramref name="cost"/> would leave within <paramref name="cap"/>.</summary>
        public bool Fits(int cost, int cap)
        {
            return Draws + cost <= cap;
        }

        /// <summary>
        /// True when <paramref name="key"/> may be drawn: it is in use already, or it is new and its
        /// <paramref name="cost"/> fits under <paramref name="cap"/> (it is then counted). False when a new batch would
        /// break the cap (counted in <see cref="Refused"/>) or the table is full.
        /// </summary>
        public bool TryUse(ulong key, int cost, int cap)
        {
            int slot = Find(key);
            if (_full[slot]) return true;
            if (Draws + cost > cap || _count >= _slots.Length / 2)
            {
                Refused++;
                return false;
            }
            _full[slot] = true;
            _keys[slot] = key;
            _slots[_count++] = slot;
            Draws += cost;
            return true;
        }

        /// <summary>
        /// The last fallback: <paramref name="key"/> is drawn whatever the cap. In use already, it costs nothing; new, it is
        /// counted (draws and <see cref="Overflow"/>); with the table full it is still drawn, counted in
        /// <see cref="Overflow"/> only.
        /// </summary>
        public void Force(ulong key, int cost)
        {
            int slot = Find(key);
            if (_full[slot]) return;
            Overflow++;
            if (_count >= _slots.Length / 2) return;
            _full[slot] = true;
            _keys[slot] = key;
            _slots[_count++] = slot;
            Draws += cost;
        }

        private int Find(ulong key)
        {
            ulong h = key * 0x9E3779B97F4A7C15UL;
            int i = (int)(h >> 40) & _mask;
            while (_full[i] && _keys[i] != key) i = (i + 1) & _mask;
            return i;
        }
    }

    /// <summary>
    /// Batch keys of the traffic presenter (one place, so the presenter and the budget test agree) and which of its
    /// batches draw the outline pass: only the near moving vehicles (plated LOD0 and shared LOD1 bodies, LOD0 wheels)
    /// are outlined; far traffic (LOD2), LOD1 wheels, parked vehicles, the tinted block-outs and boxes and the fleet tags are not
    /// (W2 perf pass: the outline there was a second draw for every batch and is not visible at their distances).
    /// </summary>
    public static class TrafficDrawKeys
    {
        /// <summary>A shared body (moving or parked) of a variant, livery, model type and level; parked LOD2 bodies share
        /// the moving LOD2 batch of the same look (no rider).</summary>
        public static ulong Body(int variant, byte livery, byte model, VehicleLod lod, bool rider)
        {
            if (lod >= VehicleLod.Block) return Tinted(variant, lod);
            return (1UL << 60) | ((ulong)(uint)variant << 40) | ((ulong)livery << 32) | ((ulong)model << 16) | ((ulong)lod << 8) | (rider ? 1UL : 0UL);
        }

        /// <summary>The livery-neutral tinted block-out or box of a variant (<see cref="ParkedLook"/>): every livery and
        /// model type shares it, the livery colour rides on the instance tint.</summary>
        public static ulong Tinted(int variant, VehicleLod lod)
        {
            return (2UL << 60) | ((ulong)(uint)variant << 40) | ((ulong)lod << 8);
        }

        /// <summary>The plated LOD0 body of one agent (its own mesh, one draw).</summary>
        public static ulong Unique(int agentId)
        {
            return (3UL << 60) | (uint)agentId;
        }

        /// <summary>A wheel batch: style, radius and width to the centimetre, level.</summary>
        public static ulong Wheel(WheelStyle style, float radius, float width, VehicleLod lod)
        {
            uint r = (uint)Math.Max(1, Math.Min(0xFFFF, (int)Math.Round(radius * 100f)));
            uint w = (uint)Math.Max(1, Math.Min(0xFFFF, (int)Math.Round(width * 100f)));
            return (4UL << 60) | ((ulong)(byte)style << 48) | ((ulong)r << 32) | ((ulong)w << 8) | (byte)lod;
        }

        /// <summary>The green key tags of the community-fleet vehicles (one batch).</summary>
        public const ulong FleetTag = 5UL << 60;

        /// <summary>Traffic of this class is drawn with its rider (two-wheelers) or puller (rickshaws).</summary>
        public static bool CarriesRider(VehicleClass c)
        {
            return VehicleClasses.IsTwoWheel(c) || c == VehicleClass.Rickshaw;
        }

        /// <summary>True for the bodies drawn with the outlined material: moving plated LOD0 and shared LOD1 bodies. Far
        /// moving bodies (LOD2), every parked batch, the tinted block-outs and boxes are outline-free.</summary>
        public static bool Outlined(VehicleLod lod, bool parked)
        {
            return !parked && lod <= VehicleLod.Lod1;
        }

        /// <summary>True for the wheel batches drawn outlined: the LOD0 wheels only (≤ 25 m). A LOD1 wheel (25-80 m, where
        /// the outline fades out) is a dark tyre whose outline is the tyre's own colour darkened: a second draw that
        /// shows nothing.</summary>
        public static bool WheelOutlined(VehicleLod lod)
        {
            return lod == VehicleLod.Lod0;
        }
    }

    /// <summary>What a vehicle is drawn as this frame (<see cref="TrafficDrawPlan"/>).</summary>
    public enum TrafficDraw : byte
    {
        None = 0,
        Unique = 1,
        Lod1 = 2,
        Lod2 = 3,
        Block = 4,
        Box = 5,
    }

    /// <summary>
    /// How the traffic presenter spends its draw cap (<see cref="LifeDrawBudget.TrafficDraws"/>) each frame: moving
    /// vehicles nearest first under <see cref="LifeDrawBudget.MovingDraws"/>, then parked ones nearest first under the
    /// whole cap (the parked slice plus what traffic left). A vehicle whose batch is in use already costs nothing; a new
    /// batch costs its draws (two for the outlined near bodies and wheels where the tier outlines). Over the cap a vehicle
    /// falls back to a coarser shared batch: plated LOD0 → shared LOD1 → shared LOD2 → the tinted block-out of its
    /// variant for moving traffic (two-wheelers and rickshaws skip the block-out, which has no rider, and take the LOD2
    /// look of a sibling of their catalogue entry already drawn, <see cref="StoodIn"/>); LOD2 → block-out → box for
    /// parked. The last fallback is drawn even past the cap (<see cref="Overflow"/>): the cap coarsens the far end of
    /// the street, it never hides a vehicle (owner playtest #6, "absolutely no traffic") nor lets a parked one blink as
    /// moving traffic takes its draws (#4). What goes past it is bounded: one batch per catalogue entry and kind (moving
    /// LOD2 of a rider class, block-out, box) and the fleet tags. Allocation free per frame; engine-free (the presenter
    /// and the budget test run the same plan).
    /// </summary>
    public sealed class TrafficDrawPlan
    {
        private readonly DrawKeySet _keys = new DrawKeySet(512);
        private LifeDrawBudget _budget = LifeDrawBudget.ForTier(1);

        /// <summary>Per catalogue entry: the frame (<see cref="_frame"/>) and the look (livery, model type) of the last
        /// rider LOD2 batch drawn, the zero-cost stand-in of a far two-wheeler over the cap.</summary>
        private int[] _riderFrame = new int[VehicleCatalog.Count];
        private byte[] _riderLivery = new byte[VehicleCatalog.Count], _riderModel = new byte[VehicleCatalog.Count];
        private int _frame;

        /// <summary>Draws planned since <see cref="Begin"/> (outline passes included).</summary>
        public int Draws
        {
            get { return _keys.Draws; }
        }

        /// <summary>Distinct batches planned since <see cref="Begin"/>.</summary>
        public int Batches
        {
            get { return _keys.Count; }
        }

        /// <summary>New batches refused by the cap since <see cref="Begin"/> (each refusal fell back to a coarser one).</summary>
        public int Refused
        {
            get { return _keys.Refused; }
        }

        /// <summary>Last-fallback batches drawn past the cap since <see cref="Begin"/> (zero on the sample's anchors).</summary>
        public int Overflow
        {
            get { return _keys.Overflow; }
        }

        public LifeDrawBudget Budget
        {
            get { return _budget; }
        }

        /// <summary>Starts a frame under <paramref name="budget"/>.</summary>
        public void Begin(LifeDrawBudget budget)
        {
            _budget = budget ?? _budget;
            _keys.Clear();
            _frame++;
            StoodIn = 0;
        }

        /// <summary>Far two-wheelers and rickshaws over the cap drawn with the LOD2 look of a sibling of their catalogue
        /// entry already drawn this frame (another livery or model type, its rider kept) since <see cref="Begin"/>.</summary>
        public int StoodIn { get; private set; }

        /// <summary>A moving vehicle's own plated LOD0 body and its LOD0 wheels: true when they fit (and are counted).</summary>
        public bool TryUnique(int agentId, WheelSocket[] wheels)
        {
            return Reserve(TrafficDrawKeys.Unique(agentId), wheels, VehicleLod.Lod0, _budget.MovingDraws);
        }

        /// <summary>A moving vehicle's shared LOD1 body and its LOD1 wheels: true when they fit (and are counted).</summary>
        public bool TryNear(int variant, byte livery, byte model, bool rider, WheelSocket[] wheels)
        {
            return Reserve(TrafficDrawKeys.Body(variant, livery, model, VehicleLod.Lod1, rider), wheels, VehicleLod.Lod1, _budget.MovingDraws);
        }

        /// <summary>A far moving vehicle (or a near one over the cap): its shared LOD2 body, else the tinted block-out of
        /// its variant (not for a class drawn with its rider: the block-out has none), and past the cap still one of the two
        /// (<see cref="DrawKeySet.Force"/>). Never <see cref="TrafficDraw.None"/>.</summary>
        public TrafficDraw TryFar(int variant, byte livery, byte model, bool rider)
        {
            return TryFar(variant, ref livery, ref model, rider);
        }

        /// <summary>
        /// <see cref="TryFar(int, byte, byte, bool)"/> for a presenter that can draw a stand-in look: a two-wheeler or
        /// rickshaw over the cap takes the LOD2 look (<paramref name="livery"/>, <paramref name="model"/>) of a sibling of
        /// its catalogue entry already drawn this frame, rider included (a far bike in another colour, at no cost), before
        /// its own look goes past the cap. Draw the returned level with the returned look.
        /// </summary>
        public TrafficDraw TryFar(int variant, ref byte livery, ref byte model, bool rider)
        {
            int cap = _budget.MovingDraws, plain = _budget.Cost(false);
            ulong body = TrafficDrawKeys.Body(variant, livery, model, VehicleLod.Lod2, rider);
            if (_keys.TryUse(body, plain, cap))
            {
                if (rider) NoteRider(variant, livery, model);
                return TrafficDraw.Lod2;
            }
            if (rider)
            {
                // The block-out has no rider: a sibling's look drawn already, else its own past the cap.
                if (variant >= 0 && variant < _riderFrame.Length && _riderFrame[variant] == _frame)
                {
                    livery = _riderLivery[variant];
                    model = _riderModel[variant];
                    StoodIn++;
                    return TrafficDraw.Lod2;
                }
                _keys.Force(body, plain);
                NoteRider(variant, livery, model);
                return TrafficDraw.Lod2;
            }
            ulong block = TrafficDrawKeys.Tinted(variant, VehicleLod.Block);
            if (!_keys.TryUse(block, plain, cap)) _keys.Force(block, plain); // never hide traffic
            return TrafficDraw.Block;
        }

        private void NoteRider(int variant, byte livery, byte model)
        {
            if (variant < 0) return;
            if (variant >= _riderFrame.Length)
            {
                int n = Math.Max(variant + 1, VehicleCatalog.Count);
                Array.Resize(ref _riderFrame, n);
                Array.Resize(ref _riderLivery, n);
                Array.Resize(ref _riderModel, n);
            }
            _riderFrame[variant] = _frame;
            _riderLivery[variant] = livery;
            _riderModel[variant] = model;
        }

        /// <summary>
        /// A moving vehicle at render level <paramref name="level"/> (0, 1, 2; −1 = not drawn) in one call, given whether
        /// its plated body and its shared LOD1 body are built: what the presenter does step by step.
        /// </summary>
        public TrafficDraw PlaceMoving(int level, int agentId, int variant, byte livery, byte model, bool rider, WheelSocket[] wheels, bool uniqueReady,
                                       bool lod1Ready)
        {
            if (level < 0) return TrafficDraw.None;
            if (level == 0 && uniqueReady && TryUnique(agentId, wheels)) return TrafficDraw.Unique;
            if (level <= 1 && lod1Ready && TryNear(variant, livery, model, rider, wheels)) return TrafficDraw.Lod1;
            return TryFar(variant, ref livery, ref model, rider);
        }

        /// <summary>A parked vehicle at parked level <paramref name="level"/> (0 near LOD2, 1 block-out, 2 box): the level
        /// it is drawn at after the cap's fallbacks, or <see cref="TrafficDraw.None"/> for a level below 0 only. Over the
        /// cap a block-out or a box takes whichever of its variant's two tinted batches is drawn already (a block-out may
        /// come back a box, a box a block-out), and only when neither is does its own go past the cap
        /// (<see cref="DrawKeySet.Force"/>): at most one batch per catalogue entry.</summary>
        public TrafficDraw PlaceParked(int level, int variant, byte livery, byte model)
        {
            int cap = _budget.TrafficDraws, plain = _budget.Cost(false);
            if (level < 0) return TrafficDraw.None;
            if (level == 0)
            {
                if (_keys.TryUse(TrafficDrawKeys.Body(variant, livery, model, VehicleLod.Lod2, false), plain, cap)) return TrafficDraw.Lod2;
                level = 1;
            }
            ulong block = TrafficDrawKeys.Tinted(variant, VehicleLod.Block), box = TrafficDrawKeys.Tinted(variant, VehicleLod.Box);
            if (level == 1)
            {
                if (_keys.TryUse(block, plain, cap)) return TrafficDraw.Block;
                if (_keys.Has(box)) return TrafficDraw.Box; // its variant's box is drawn already: no new batch
                if (_keys.TryUse(box, plain, cap)) return TrafficDraw.Box;
                _keys.Force(block, plain); // never hidden: no parked vehicle blinks as traffic takes the draws
                return TrafficDraw.Block;
            }
            if (_keys.TryUse(box, plain, cap)) return TrafficDraw.Box;
            if (_keys.Has(block)) return TrafficDraw.Block; // its variant's block-out is drawn already: no new batch
            _keys.Force(box, plain);
            return TrafficDraw.Box;
        }

        /// <summary>The fleet tags' batch (one for every community-fleet vehicle): always drawn (past the cap if need be:
        /// the tag tells the player the vehicle can be borrowed), so always true.</summary>
        public bool TryFleetTag()
        {
            int plain = _budget.Cost(false);
            if (!_keys.TryUse(TrafficDrawKeys.FleetTag, plain, _budget.TrafficDraws)) _keys.Force(TrafficDrawKeys.FleetTag, plain);
            return true;
        }

        /// <summary>Reserves an outlined near body and its wheels together: all or nothing.</summary>
        private bool Reserve(ulong body, WheelSocket[] wheels, VehicleLod wheelLod, int cap)
        {
            int outlined = _budget.Cost(true), wheel = _budget.Cost(TrafficDrawKeys.WheelOutlined(wheelLod));
            int cost = _keys.Has(body) ? 0 : outlined;
            int n = wheels != null ? wheels.Length : 0;
            for (int w = 0; w < n; w++)
            {
                ulong k = TrafficDrawKeys.Wheel(wheels[w].Style, wheels[w].Radius, wheels[w].Width, wheelLod);
                if (_keys.Has(k) || SameBefore(wheels, w, wheelLod, k)) continue;
                cost += wheel;
            }
            if (!_keys.Fits(cost, cap)) return false;
            _keys.TryUse(body, outlined, cap);
            for (int w = 0; w < n; w++) _keys.TryUse(TrafficDrawKeys.Wheel(wheels[w].Style, wheels[w].Radius, wheels[w].Width, wheelLod), wheel, cap);
            return true;
        }

        private static bool SameBefore(WheelSocket[] wheels, int w, VehicleLod lod, ulong key)
        {
            for (int j = 0; j < w; j++)
                if (TrafficDrawKeys.Wheel(wheels[j].Style, wheels[j].Radius, wheels[j].Width, lod) == key) return true;
            return false;
        }
    }

    /// <summary>
    /// The parked block-out and box levels without a livery baked in (W2 perf pass): the body paint is white with a full
    /// tint mask (vertex alpha 255) and every other part keeps its colour with no mask (alpha 0), so the
    /// <c>instancedTint</c> material paints each instance in its livery's body colour (<see cref="TintOf"/>) and one
    /// batch per variant and level serves every livery. Drawn this way a vehicle looks exactly like its baked livery
    /// build (the shader multiplies the linear white by the linear tint). Engine-free; build at load time.
    /// </summary>
    public static class ParkedLook
    {
        [ThreadStatic] private static MeshData s_other;

        /// <summary>The body colour of a livery as <c>0xRRGGBB</c> (sRGB), the instance tint of the neutral levels.</summary>
        public static uint TintOf(int variant, byte livery)
        {
            return VehicleCatalog.At(variant).LiveryAt(livery).Body & 0xFFFFFFu;
        }

        /// <summary>True for the levels drawn livery-neutral with a tint (block-out and box).</summary>
        public static bool IsTinted(VehicleLod lod)
        {
            return lod >= VehicleLod.Block;
        }

        /// <summary>
        /// Appends the livery-neutral <paramref name="lod"/> (block-out or box) of catalogue entry
        /// <paramref name="variant"/> to <paramref name="m"/>: a vertex is body paint when it carries the body colour in
        /// the build of every livery of the entry. Returns the triangles added.
        /// </summary>
        public static int BuildNeutral(int variant, VehicleLod lod, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (!IsTinted(lod)) throw new ArgumentOutOfRangeException(nameof(lod), "only the block-out and box levels are tinted");
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            int v0 = m.VertexCount;
            int tris = VehicleMesher.Build(variant, 0, 0, lod, m);
            int n = m.VertexCount - v0;
            MeshData other = s_other ?? (s_other = new MeshData(256, 768));
            // Mask: body colour in livery 0 ...
            uint body0 = MeshColor.FromHex(TintOf(variant, 0));
            var paint = new bool[n];
            for (int v = 0; v < n; v++) paint[v] = ColourAt(m, v0 + v) == body0;
            // ... and in every other livery (the geometry does not depend on the livery).
            int liveries = Math.Max(1, (int)e.LiveryCount);
            for (int l = 1; l < liveries; l++)
            {
                other.Clear();
                VehicleMesher.Build(variant, (byte)l, 0, lod, other);
                if (other.VertexCount != n) throw new InvalidOperationException("livery " + l + " of " + e.AssetId + " changes the geometry");
                uint body = MeshColor.FromHex(TintOf(variant, (byte)l));
                for (int v = 0; v < n; v++)
                    if (paint[v] && ColourAt(other, v) != body) paint[v] = false;
            }
            for (int v = 0; v < n; v++)
            {
                int c = (v0 + v) * 4;
                if (paint[v])
                {
                    m.Colors[c] = 255;
                    m.Colors[c + 1] = 255;
                    m.Colors[c + 2] = 255;
                    m.Colors[c + 3] = 255;
                }
                else m.Colors[c + 3] = 0;
            }
            return tris;
        }

        /// <summary>The colour a vertex of a neutral mesh shows with tint <paramref name="tintRgb"/> (sRGB
        /// <c>0xRRGGBBAA</c>, alpha 255): the tint on masked paint, its own colour elsewhere (tests).</summary>
        public static uint Shown(MeshData neutral, int vertex, uint tintRgb)
        {
            int c = vertex * 4;
            if (neutral.Colors[c + 3] == 255) return MeshColor.FromHex(tintRgb);
            return (uint)(neutral.Colors[c] << 24 | neutral.Colors[c + 1] << 16 | neutral.Colors[c + 2] << 8 | 0xFF);
        }

        private static uint ColourAt(MeshData m, int v)
        {
            int c = v * 4;
            return (uint)(m.Colors[c] << 24 | m.Colors[c + 1] << 16 | m.Colors[c + 2] << 8 | m.Colors[c + 3]);
        }
    }

    /// <summary>
    /// How the crowd presenter spends its draw cap (<see cref="LifeDrawBudget.CrowdDraws"/>), person by person, nearest
    /// first: a near or mid person is a skinned NPC (one draw, two where the tier outlines characters); a far person
    /// joins the batch of their body shape and baked frame (one outline-free draw per distinct pair, garment colours
    /// ride on the instance tint). When a new draw would break the cap, a near or mid person is drawn far instead, a
    /// far walker takes the nearest walk frame of their shape already drawn this frame (the stride phase moves by a
    /// sixth of a step at most, at 40-90 m), then a drawn frame of a sibling shape (same archetype, place and carry prop:
    /// the colour stays theirs), then any drawn shape of their archetype doing the same thing, and a person with none
    /// of those drawn gets their own far batch past the cap (<see cref="Overflow"/>): nobody blinks out (owner playtest
    /// #4). The caps leave every person drawn at the sample's busiest places (Asan, Boudha, Basantapur: 7-40 people
    /// within 90 m) with nothing past them; the fallbacks only bite on a street fuller than the simulations make. Allocation free per frame; engine-free (the budget test runs it on
    /// the simulated crowd).
    /// </summary>
    public sealed class CrowdDrawPlan
    {
        private readonly DrawKeySet _keys = new DrawKeySet(512);
        private LifeDrawBudget _budget;
        private int _skinned;

        /// <summary>The far batches drawn this frame (shape, frame), in order.</summary>
        private readonly int[] _farShape = new int[512];
        private readonly byte[] _farFrame = new byte[512];
        private int _farCount;

        /// <summary>Draws planned since <see cref="Begin"/> (outline passes included).</summary>
        public int Draws
        {
            get { return _keys.Draws; }
        }

        /// <summary>People drawn far although their band is skinned, far walkers moved to a drawn frame of their shape, far
        /// people drawn as a sibling shape, and far batches drawn past the cap (people with nothing of their look drawn),
        /// since <see cref="Begin"/>.</summary>
        public int Demoted { get; private set; }

        public int Snapped { get; private set; }

        public int StoodIn { get; private set; }

        public int Overflow { get; private set; }

        /// <summary>Starts a frame under <paramref name="budget"/>.</summary>
        public void Begin(LifeDrawBudget budget)
        {
            _budget = budget;
            _keys.Clear();
            _skinned = 0;
            _farCount = 0;
            Demoted = Snapped = StoodIn = Overflow = 0;
        }

        /// <summary>
        /// Plans one person of body shape <paramref name="shapeKey"/> (<see cref="CrowdVariants.ShapeKey"/>) in
        /// <paramref name="band"/> (0 near, 1 mid, 2 far) playing <paramref name="clip"/> at
        /// <paramref name="walkPhase"/>: returns the band to draw them in (a near or mid one may come back far; never −1,
        /// everybody is drawn); a far walker's phase may move to a frame already drawn, and a far person's shape may become
        /// a drawn sibling shape (draw the look with the returned shape and the person's colour).
        /// </summary>
        public int Place(ref int shapeKey, int band, PedClip clip, ref double walkPhase)
        {
            int cap = _budget != null ? _budget.CrowdDraws : int.MaxValue;
            int skinnedCost = _budget != null ? _budget.Cost(true) : 1;
            if (CrowdLodPlan.Skinned(band))
            {
                if (_keys.TryUse((6UL << 60) | (uint)_skinned, skinnedCost, cap))
                {
                    _skinned++;
                    return band;
                }
                Demoted++;
            }
            FarFrame f = CrowdVariants.FrameOf(clip, walkPhase);
            if (_keys.TryUse(FarKey(shapeKey, f), 1, cap))
            {
                if (_farCount < _farShape.Length)
                {
                    _farShape[_farCount] = shapeKey;
                    _farFrame[_farCount++] = (byte)f;
                }
                return 2;
            }
            int shape = shapeKey;
            if (f <= FarFrame.Walk5)
            {
                // The nearest walk frame of the same shape drawn this frame (either way round the stride).
                int g = NearestWalk(shape, (int)f);
                if (g >= 0)
                {
                    walkPhase = (g + 0.5) / 6.0 * 2.0 * Math.PI;
                    Snapped++;
                    return 2;
                }
            }
            // A sibling shape (same archetype, place and carry prop) drawn in this frame, or in the nearest walk frame.
            int family = shape & 0x000FFFFF;
            for (int v = 0; v < CrowdVariants.Variants; v++)
            {
                int sibling = family | (v << 20);
                if (sibling == shape) continue;
                if (_keys.Has(FarKey(sibling, f)))
                {
                    shapeKey = sibling;
                    StoodIn++;
                    return 2;
                }
                if (f > FarFrame.Walk5) continue;
                int g = NearestWalk(sibling, (int)f);
                if (g < 0) continue;
                shapeKey = sibling;
                walkPhase = (g + 0.5) / 6.0 * 2.0 * Math.PI;
                StoodIn++;
                return 2;
            }
            // Any drawn shape of the same archetype doing the same thing (a walker for a walker, a sitter for a sitter).
            bool walking = f <= FarFrame.Walk5;
            for (int i = 0; i < _farCount; i++)
            {
                if ((_farShape[i] & 0xFF) != (shape & 0xFF)) continue;
                var g = (FarFrame)_farFrame[i];
                if (walking ? g > FarFrame.Walk5 : g != f) continue;
                shapeKey = _farShape[i];
                if (walking) walkPhase = ((int)g + 0.5) / 6.0 * 2.0 * Math.PI;
                StoodIn++;
                return 2;
            }
            // Nothing of their look is drawn: their own far batch, past the cap (never hide a person).
            _keys.Force(FarKey(shape, f), 1);
            if (_farCount < _farShape.Length)
            {
                _farShape[_farCount] = shape;
                _farFrame[_farCount++] = (byte)f;
            }
            Overflow++;
            return 2;
        }

        /// <summary>The walk frame of <paramref name="shape"/> nearest <paramref name="w"/> drawn this frame, or −1.</summary>
        private int NearestWalk(int shape, int w)
        {
            for (int d = 1; d <= 3; d++)
                for (int s = 1; s >= -1; s -= 2)
                {
                    int g = ((w + s * d) % 6 + 6) % 6;
                    if (_keys.Has(FarKey(shape, (FarFrame)g))) return g;
                }
            return -1;
        }

        /// <summary>The far batch of a body shape and baked frame (as <c>PeopleRenderer</c> keeps them).</summary>
        public static ulong FarKey(int shapeKey, FarFrame frame)
        {
            return (7UL << 60) | ((ulong)(uint)shapeKey << 8) | (byte)frame;
        }
    }
}
