using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>Detail levels of a vehicle model (W2_DESIGN 5.2 and 10.4).</summary>
    public enum VehicleLod : byte
    {
        /// <summary>The player's vehicle and the nearest traffic: every part, numbered plates.</summary>
        Lod0 = 0,

        /// <summary>Near traffic: the same silhouette with the small parts dropped, blank plates.</summary>
        Lod1 = 1,

        /// <summary>Far traffic and near parked vehicles: rounded silhouette, wheels baked in.</summary>
        Lod2 = 2,

        /// <summary>Parked block-out (≤ 80 triangles): body, cabin or seat, wheel boxes.</summary>
        Block = 3,

        /// <summary>A single palette-tinted box (12 triangles), parked 80–150 m.</summary>
        Box = 4,
    }

    /// <summary>The look of a wheel (tyre profile and rim), set per socket by <see cref="VehicleMesher.Wheels"/>.
    /// Append-only.</summary>
    public enum WheelStyle : byte
    {
        /// <summary>Steel wheel with a hubcap (small cars, taxis, vans, the tempo).</summary>
        Car = 0,

        /// <summary>Five-spoke alloy (EVs, SUVs, pickups).</summary>
        CarAlloy = 1,

        /// <summary>Motorbike: round-section tyre, five-spoke alloy, disc brake.</summary>
        MotoAlloy = 2,

        /// <summary>Wire-spoked motorbike wheel (the retro cruiser).</summary>
        MotoSpoked = 3,

        /// <summary>Small scooter wheel: fat tyre, five-spoke alloy.</summary>
        Scooter = 4,

        /// <summary>Bicycle and cycle rickshaw: thin tyre, wire spokes.</summary>
        Bicycle = 5,

        /// <summary>Bus and truck: tall tyre, deep steel disc with holes, hub boss and nuts.</summary>
        Truck = 6,

        /// <summary>Tractor drive wheel: chevron lugs, yellow rim.</summary>
        TractorRear = 7,

        /// <summary>Tractor steering wheel: ribbed tyre, yellow rim.</summary>
        TractorFront = 8,
    }

    /// <summary>Where a wheel sits in the vehicle frame (origin on the ground under the rear axle, +X right, +Z forward).</summary>
    public struct WheelSocket
    {
        public float X, Y, Z, Radius, Width;

        /// <summary>A steered (front) wheel.</summary>
        public bool Steers;

        /// <summary>Tyre and rim look.</summary>
        public WheelStyle Style;
    }

    /// <summary>
    /// Procedural cartoon vehicles of the Kathmandu Valley as they drive today (W2_DESIGN 5.2,
    /// docs/research/w2/ref_vehicles.md): every catalogue entry is built from the rounded-geometry library
    /// (<see cref="Shapes"/>: superellipsoid tanks and seats, side-profile bodies with rounded edges, lathe wheels with real
    /// tyre and rim profiles, tube frames, rounded lamps and panels) as one of a few **model types** per entry (the
    /// commuter motorbike comes as a Pulsar-, Shine- or streetfighter-type bike, the hatchback as a Swift-, i10- or
    /// Alto-type car, and so on), picked per agent by <see cref="ModelFor"/> with the street weights. Each vertex carries
    /// its material channel and baked AO in UV0 (docs/W2_DETAIL_CONTRACT.md §5). No brand, badge, wordmark, logo,
    /// operator livery or emblem (the ambulance has no Red Cross, the police jeep no weapons); plates are fictional
    /// Devanagari plates in the legacy colour code (<see cref="VehiclePlates"/>, <see cref="PlateGlyphs"/>). Frame:
    /// origin on the ground under the rear axle, +X right, +Y up, +Z forward (the <see cref="ArcadeVehicle"/> and
    /// <see cref="Traffic.AgentPose"/> reference point). Wheels are separate meshes (<see cref="BuildWheel(in WheelSocket,
    /// VehicleLod, MeshData)"/> at <see cref="Wheels"/>) at LOD0 and LOD1 so they spin and steer; LOD2 and the parked
    /// levels bake them in. Deterministic; allocation only through <see cref="MeshData"/> growth and per-thread scratch.
    /// </summary>
    public static partial class VehicleMesher
    {
        /// <summary>The largest triangle cap per level over all families (<see cref="Budget(VehicleLod, BodyShape)"/>):
        /// W2_DESIGN 10.4 with the LOD0 cap raised for buses and trucks (they fill the screen; at most one is at LOD0),
        /// LOD1 and LOD2 raised slightly for the rounded silhouettes; block-out 80, box 12.</summary>
        public static int Budget(VehicleLod lod)
        {
            switch (lod)
            {
                case VehicleLod.Lod0: return 9000;
                case VehicleLod.Lod1: return 2600;
                case VehicleLod.Lod2: return 900;
                case VehicleLod.Block: return 80;
                default: return 12;
            }
        }

        /// <summary>Triangle cap of a family at a level, wheels and rider included (ref_vehicles.md §7): two-wheelers 7 k
        /// (the player's own bike fills the screen all game), cars and vans 7.5 k, buses, trucks and tractors 9 k at
        /// LOD0; LOD1 2.2 k / 2.4 k / 2.6 k; LOD2 700 / 900.</summary>
        public static int Budget(VehicleLod lod, BodyShape shape)
        {
            bool two = IsTwo(shape) || shape == BodyShape.Rickshaw;
            bool heavy = shape == BodyShape.Bus || shape == BodyShape.Truck || shape == BodyShape.Tipper || shape == BodyShape.Tanker ||
                         shape == BodyShape.Tractor;
            switch (lod)
            {
                case VehicleLod.Lod0: return two ? 7000 : heavy ? 9000 : 7500;
                case VehicleLod.Lod1: return two ? 2200 : heavy ? 2600 : 2400;
                case VehicleLod.Lod2: return two ? 700 : 900;
                default: return Budget(lod);
            }
        }

        public const float WheelScale = 1.15f, CabScale = 1.1f, LengthScale = 0.95f;

        /// <summary>Offset of painted panels (windows, stripes, displays) off the body, as the road decal lift.</summary>
        public const float DecalLift = 0.012f;

        /// <summary>Body measurements of an entry in the cartoon overlay.</summary>
        public struct Dims
        {
            public float Length, Width, Height, Wheelbase, WheelR, Rear, Front;

            /// <summary>Half width of the body.</summary>
            public float Half
            {
                get { return 0.5f * Width; }
            }
        }

        public static Dims DimsOf(in VehicleCatalogEntry e)
        {
            float l = e.LengthM, wb = e.WheelbaseM, ro = e.RearOverhangM;
            float fo = Math.Max(0.05f, l - wb - ro);
            float k = l - wb > 0.05f ? (LengthScale * l - wb) / (l - wb) : 1f;
            k = Math.Max(0.6f, Math.Min(1f, k));
            return new Dims
            {
                Length = wb + (ro + fo) * k, Width = e.WidthM, Height = e.HeightM, Wheelbase = wb, WheelR = 0.5f * e.WheelDiaM * WheelScale,
                Rear = ro * k, Front = fo * k,
            };
        }

        /// <summary>The wheels of an entry as its first model type draws them (<see cref="Wheels(in VehicleCatalogEntry, int)"/>).
        /// Prefer the overload with the vehicle's own model type: car model types differ in track and wheel look.</summary>
        public static WheelSocket[] Wheels(in VehicleCatalogEntry e)
        {
            return Wheels(e, 0);
        }

        /// <summary>The wheels of an entry drawn as model type <paramref name="model"/> (rear axle at z = 0, front at the
        /// wheelbase; tandem rear on the tipper; three wheels on the tempo and rickshaw; big rear wheels on the tractor;
        /// wide dual rear wheels on buses and trucks; cars at their model type's track with alloys or steel wheels).
        /// Deterministic; allocates the returned array.</summary>
        public static WheelSocket[] Wheels(in VehicleCatalogEntry e, int model)
        {
            Dims d = DimsOf(e);
            float r = d.WheelR;
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                    return Pair(r, d.Wheelbase, 0.115f, 0.105f, WheelStyle.Scooter, WheelStyle.Scooter);
                case BodyShape.Motorbike:
                    return Pair(r, d.Wheelbase, 0.125f, 0.10f, WheelStyle.MotoAlloy, WheelStyle.MotoAlloy);
                case BodyShape.Cruiser:
                    return Pair(r, d.Wheelbase, 0.125f, 0.10f, WheelStyle.MotoSpoked, WheelStyle.MotoSpoked);
                case BodyShape.Bicycle:
                    return Pair(r, d.Wheelbase, 0.05f, 0.05f, WheelStyle.Bicycle, WheelStyle.Bicycle);
                case BodyShape.Tempo:
                {
                    float w = 0.17f, x = d.Half - 0.5f * w - 0.02f;
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = 0f, Radius = r, Width = w, Style = WheelStyle.Car },
                        new WheelSocket { X = x, Y = r, Z = 0f, Radius = r, Width = w, Style = WheelStyle.Car },
                        new WheelSocket { X = 0f, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true, Style = WheelStyle.Car },
                    };
                }
                case BodyShape.Rickshaw:
                {
                    float w = 0.05f, x = d.Half - 0.08f;
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = 0f, Radius = r, Width = w, Style = WheelStyle.Bicycle },
                        new WheelSocket { X = x, Y = r, Z = 0f, Radius = r, Width = w, Style = WheelStyle.Bicycle },
                        new WheelSocket { X = 0f, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true, Style = WheelStyle.Bicycle },
                    };
                }
                case BodyShape.Tractor:
                {
                    float rr = 0.5f * 1.40f * 1.05f, rf = 0.5f * 0.80f * 1.1f, wr = 0.38f, wf = 0.17f;
                    return new[]
                    {
                        new WheelSocket { X = -(d.Half - 0.5f * wr), Y = rr, Z = 0f, Radius = rr, Width = wr, Style = WheelStyle.TractorRear },
                        new WheelSocket { X = d.Half - 0.5f * wr, Y = rr, Z = 0f, Radius = rr, Width = wr, Style = WheelStyle.TractorRear },
                        new WheelSocket { X = -(d.Half - 0.33f), Y = rf, Z = d.Wheelbase, Radius = rf, Width = wf, Steers = true, Style = WheelStyle.TractorFront },
                        new WheelSocket { X = d.Half - 0.33f, Y = rf, Z = d.Wheelbase, Radius = rf, Width = wf, Steers = true, Style = WheelStyle.TractorFront },
                    };
                }
                case BodyShape.Bus:
                case BodyShape.Truck:
                case BodyShape.Tanker:
                case BodyShape.Tipper:
                {
                    float wf = 0.30f, wr = 0.52f, xf = d.Half - 0.5f * wf - 0.08f, xr = d.Half - 0.5f * wr - 0.04f;
                    if (e.Shape == BodyShape.Tipper)
                        return new[]
                        {
                            new WheelSocket { X = -xr, Y = r, Z = -0.675f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                            new WheelSocket { X = xr, Y = r, Z = -0.675f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                            new WheelSocket { X = -xr, Y = r, Z = 0.675f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                            new WheelSocket { X = xr, Y = r, Z = 0.675f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                            new WheelSocket { X = -xf, Y = r, Z = d.Wheelbase, Radius = r, Width = wf, Steers = true, Style = WheelStyle.Truck },
                            new WheelSocket { X = xf, Y = r, Z = d.Wheelbase, Radius = r, Width = wf, Steers = true, Style = WheelStyle.Truck },
                        };
                    return new[]
                    {
                        new WheelSocket { X = -xr, Y = r, Z = 0f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                        new WheelSocket { X = xr, Y = r, Z = 0f, Radius = r, Width = wr, Style = WheelStyle.Truck },
                        new WheelSocket { X = -xf, Y = r, Z = d.Wheelbase, Radius = r, Width = wf, Steers = true, Style = WheelStyle.Truck },
                        new WheelSocket { X = xf, Y = r, Z = d.Wheelbase, Radius = r, Width = wf, Steers = true, Style = WheelStyle.Truck },
                    };
                }
                default:
                {
                    // Cars and vans: tyre face flush with the body side of the model type, width on a 2 cm grid with the
                    // alloys on the odd centimetre (so StyleFor reads the style back from the size).
                    double bw, fe, re;
                    WheelStyle st;
                    CarProportions(e, model, out bw, out fe, out re, out st);
                    float w = CarWheelWidth(bw, st == WheelStyle.CarAlloy), x = (float)(0.5 * bw) - 0.5f * w - 0.015f;
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = 0f, Radius = r, Width = w, Style = st },
                        new WheelSocket { X = x, Y = r, Z = 0f, Radius = r, Width = w, Style = st },
                        new WheelSocket { X = -x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true, Style = st },
                        new WheelSocket { X = x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true, Style = st },
                    };
                }
            }
        }

        private static WheelSocket[] Pair(float r, float wb, float rearW, float frontW, WheelStyle rear, WheelStyle front)
        {
            return new[]
            {
                new WheelSocket { X = 0f, Y = r, Z = 0f, Radius = r, Width = rearW, Style = rear },
                new WheelSocket { X = 0f, Y = r, Z = wb, Radius = r, Width = frontW, Steers = true, Style = front },
            };
        }

        internal static bool IsTwo(BodyShape s)
        {
            return s == BodyShape.Scooter || s == BodyShape.Motorbike || s == BodyShape.Cruiser || s == BodyShape.Bicycle;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Model types per catalogue entry
        // -----------------------------------------------------------------------------------------------------------

        // Street weights of the model types of each catalogue entry (VehicleCatalog order; ref_vehicles.md §0).
        private static readonly byte[][] ModelWeights =
        {
            new byte[] { 40, 30, 30 }, // scooter: Dio type, Activa type, Ntorq type
            new byte[] { 40, 25, 35 }, // commuter: Pulsar type, Shine/SP type, streetfighter (FZ/Apache/N)
            new byte[] { 1 }, // cruiser: Classic type
            new byte[] { 55, 45 }, // e-scooter: NIU type, Ather type
            new byte[] { 60, 40 }, // bicycle: MTB, roadster
            new byte[] { 1 }, // cycle rickshaw
            new byte[] { 1 }, // Safa tempo
            new byte[] { 55, 45 }, // microbus: Hiace type, electric micro
            new byte[] { 50, 50 }, // city bus: diesel, electric low-floor
            new byte[] { 1 }, // minibus (Tata 709 type)
            new byte[] { 1 }, // long-distance coach
            new byte[] { 1 }, // school bus
            new byte[] { 60, 40 }, // taxi: 800/Alto type, tall-boy (Santro/i10 type)
            new byte[] { 40, 35, 25 }, // hatchback: Swift type, i10 type, Alto/Tiago type
            new byte[] { 40, 30, 30 }, // EV: Dolphin type, Nexon/Punch type, Atto 3 type
            new byte[] { 40, 35, 25 }, // SUV: Scorpio type, Creta type, Prado type
            new byte[] { 65, 35 }, // pickup: Bolero single cab, Hilux double cab
            new byte[] { 1 }, // decorated truck
            new byte[] { 1 }, // tipper
            new byte[] { 1 }, // tanker
            new byte[] { 1 }, // tractor
            new byte[] { 1 }, // police jeep
            new byte[] { 1 }, // ambulance
        };

        private static readonly string[][] ModelNames =
        {
            new[] { "dio", "activa", "ntorq" },
            new[] { "pulsar", "shine", "streetfighter" },
            new[] { "classic" },
            new[] { "niu", "ather" },
            new[] { "mtb", "roadster" },
            new[] { "rickshaw" },
            new[] { "safa" },
            new[] { "hiace", "emicro" },
            new[] { "diesel", "electric" },
            new[] { "tata709" },
            new[] { "coach" },
            new[] { "school" },
            new[] { "alto800", "tallboy" },
            new[] { "swift", "i10", "alto" },
            new[] { "dolphin", "nexon", "atto3" },
            new[] { "scorpio", "creta", "prado" },
            new[] { "bolero", "hilux" },
            new[] { "lpt" },
            new[] { "tipper" },
            new[] { "tanker" },
            new[] { "tractor" },
            new[] { "police" },
            new[] { "ambulance" },
        };

        /// <summary>Number of model types of catalogue entry <paramref name="variant"/> (1 or more).</summary>
        public static int ModelCount(int variant)
        {
            return variant >= 0 && variant < ModelWeights.Length ? ModelWeights[variant].Length : 1;
        }

        /// <summary>A short generic name of a model type ("pulsar" = a Pulsar-type commuter), for previews and logs.</summary>
        public static string ModelName(int variant, int model)
        {
            if (variant < 0 || variant >= ModelNames.Length) return "model" + model;
            string[] n = ModelNames[variant];
            return n[((model % n.Length) + n.Length) % n.Length];
        }

        /// <summary>The model type an agent (or parked vehicle) of this seed is drawn as: the street weights of
        /// ref_vehicles.md, deterministic per seed.</summary>
        public static byte ModelFor(int variant, uint seed)
        {
            if (variant < 0 || variant >= ModelWeights.Length) return 0;
            byte[] w = ModelWeights[variant];
            if (w.Length == 1) return 0;
            int total = 0;
            for (int i = 0; i < w.Length; i++) total += w[i];
            uint h = VehicleCatalogEntry.Mix(seed ^ 0x4D4F444Cu);
            int u = (int)(h % (uint)total);
            for (int i = 0; i < w.Length; i++)
            {
                u -= w[i];
                if (u < 0) return (byte)i;
            }
            return (byte)(w.Length - 1);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Build
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds catalogue entry <paramref name="variant"/> with livery <paramref name="livery"/> at <paramref name="lod"/>
        /// into <paramref name="m"/> (appending), as the model type <see cref="ModelFor"/> picks for
        /// <paramref name="plateSeed"/>. See <see cref="Build(int, byte, byte, VehicleLod, MeshData, uint, bool)"/>.
        /// </summary>
        public static int Build(int variant, byte livery, VehicleLod lod, MeshData m, uint plateSeed = 0, bool rider = false)
        {
            return Build(variant, livery, ModelFor(variant, plateSeed), lod, m, plateSeed, rider);
        }

        /// <summary>
        /// Builds catalogue entry <paramref name="variant"/>, model type <paramref name="model"/> (wrapped), livery
        /// <paramref name="livery"/> at <paramref name="lod"/> into <paramref name="m"/> (appending). LOD0 and LOD1 leave
        /// the wheels out (draw them with <see cref="BuildWheel(in WheelSocket, VehicleLod, MeshData)"/>); LOD0 numbers
        /// the plates from <paramref name="plateSeed"/>. With <paramref name="rider"/> a two-wheeler gets a helmeted
        /// rider and the rickshaw its puller (traffic; the player's own character comes from Track D). Returns the
        /// triangles added.
        /// </summary>
        public static int Build(int variant, byte livery, byte model, VehicleLod lod, MeshData m, uint plateSeed = 0, bool rider = false)
        {
            return Build(variant, livery, model, lod, m, plateSeed, rider, true);
        }

        /// <summary>As <see cref="Build(int, byte, byte, VehicleLod, MeshData, uint, bool)"/>; without
        /// <paramref name="bakeWheels"/> LOD2 leaves its wheels out too (tests check the body on its own).</summary>
        internal static int Build(int variant, byte livery, byte model, VehicleLod lod, MeshData m, uint plateSeed, bool rider, bool bakeWheels)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            VehicleLivery lv = e.LiveryAt(livery);
            int before = m.IndexCount / 3;
            Dims d = DimsOf(e);
            var pal = new Livery
            {
                Body = MeshColor.FromHex(lv.Body), Accent = MeshColor.FromHex(lv.Accent), Roof = MeshColor.FromHex(lv.Roof),
                Trim = MeshColor.FromHex(lv.Trim), Sign = MeshColor.FromHex(lv.Sign), Index = livery,
            };
            if (lod == VehicleLod.Box)
            {
                BuildBox(e, d, pal, m);
                return m.IndexCount / 3 - before;
            }
            if (lod == VehicleLod.Block)
            {
                BuildBlock(e, d, pal, m);
                return m.IndexCount / 3 - before;
            }
            int count = ModelCount(variant);
            var c = new Ctx
            {
                M = m, Lod = lod, S = new ShapeLod(lod == VehicleLod.Lod0 ? 0 : lod == VehicleLod.Lod1 ? 1 : 2), Model = (byte)(model % count),
                Seed = plateSeed,
            };
            int v0 = m.VertexCount, i0 = m.IndexCount;
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                    if (e.Engine == EngineSound.EScooter) EScooter(ref c, e, d, pal);
                    else Scooter(ref c, e, d, pal);
                    if (rider) Rider(ref c, e, d, pal);
                    break;
                case BodyShape.Motorbike:
                case BodyShape.Cruiser:
                    Motorbike(ref c, e, d, pal);
                    if (rider) Rider(ref c, e, d, pal);
                    break;
                case BodyShape.Bicycle:
                    Bicycle(ref c, e, d, pal);
                    if (rider) Rider(ref c, e, d, pal);
                    break;
                case BodyShape.Rickshaw:
                    Rickshaw(ref c, e, d, pal, rider);
                    break;
                case BodyShape.Tempo:
                    Tempo(ref c, e, d, pal);
                    break;
                case BodyShape.Bus:
                    Bus(ref c, e, d, pal);
                    break;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker:
                    Truck(ref c, e, d, pal);
                    break;
                case BodyShape.Tractor:
                    Tractor(ref c, e, d, pal);
                    break;
                case BodyShape.Van:
                    Van(ref c, e, d, pal);
                    break;
                default:
                    Car(ref c, e, d, pal);
                    break;
            }
            if (lod == VehicleLod.Lod2 && bakeWheels)
                foreach (WheelSocket w in Wheels(e, c.Model))
                    WheelAt(ref c, w);
            if (lod <= VehicleLod.Lod1) Plates(ref c, e, d, plateSeed);
            BakeAo(m, v0, i0, true);
            return m.IndexCount / 3 - before;
        }

        /// <summary>The colours of one livery, packed (0xRRGGBBAA).</summary>
        internal struct Livery
        {
            public uint Body, Accent, Roof, Trim, Sign;
            public byte Index;
        }

        /// <summary>One wheel at the origin (axis along X) in the style the catalogue gives a wheel of this size (to the
        /// centimetre; <see cref="StyleFor"/>). Kept for callers that only know the size.</summary>
        public static int BuildWheel(float radius, float width, VehicleLod lod, MeshData m)
        {
            return BuildWheel(StyleFor(radius, width), radius, width, lod, m);
        }

        /// <summary>The wheel of a socket at the origin (axis along X, symmetric so it reads from either side).</summary>
        public static int BuildWheel(in WheelSocket w, VehicleLod lod, MeshData m)
        {
            return BuildWheel(w.Style, w.Radius, w.Width, lod, m);
        }

        /// <summary>A wheel of <paramref name="style"/> at the origin (axis along X). Returns the triangles added.</summary>
        public static int BuildWheel(WheelStyle style, float radius, float width, VehicleLod lod, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int before = m.IndexCount / 3;
            if (lod > VehicleLod.Lod2) lod = VehicleLod.Lod2;
            var c = new Ctx { M = m, Lod = lod, S = new ShapeLod(lod == VehicleLod.Lod0 ? 0 : lod == VehicleLod.Lod1 ? 1 : 2) };
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Wheel(ref c, style, radius, width, Affine3.Identity);
            BakeAo(m, v0, i0, false);
            return m.IndexCount / 3 - before;
        }

        /// <summary>The style of the catalogue wheel with this radius and width (to the centimetre) over every entry and
        /// model type, or <see cref="WheelStyle.Car"/> for a size the catalogue does not use. Unambiguous: no two sockets
        /// of one size have different styles (car wheels sit on a 2 cm width grid with the alloys on the odd centimetre;
        /// VehicleMeshTests checks every socket). Prefer <see cref="BuildWheel(in WheelSocket, VehicleLod, MeshData)"/>
        /// with the socket itself.</summary>
        public static WheelStyle StyleFor(float radius, float width)
        {
            int r = (int)Math.Round(radius * 100f), w = (int)Math.Round(width * 100f);
            WheelSocket[] all = AllSockets();
            for (int i = 0; i < all.Length; i++)
                if ((int)Math.Round(all[i].Radius * 100f) == r && (int)Math.Round(all[i].Width * 100f) == w)
                    return all[i].Style;
            return WheelStyle.Car;
        }

        private static WheelSocket[] _allSockets;

        /// <summary>Every wheel socket of every catalogue entry and model type (built once).</summary>
        internal static WheelSocket[] AllSockets()
        {
            WheelSocket[] a = _allSockets;
            if (a != null) return a;
            var list = new System.Collections.Generic.List<WheelSocket>();
            for (int v = 0; v < VehicleCatalog.Count; v++)
                for (int k = 0; k < ModelCount(v); k++)
                    list.AddRange(Wheels(VehicleCatalog.At(v), k));
            a = list.ToArray();
            _allSockets = a;
            return a;
        }

        /// <summary>A wheel baked into the body at its socket (LOD2).</summary>
        private static void WheelAt(ref Ctx c, in WheelSocket w)
        {
            Wheel(ref c, w.Style, w.Radius, w.Width, T(w.X, w.Y, w.Z));
        }
    }
}
