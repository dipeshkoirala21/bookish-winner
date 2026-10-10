using System.Globalization;
using System.Text;

namespace Ghumante.World.Rendering
{
    /// <summary>
    /// Per-frame triangle and instance counters for the debug HUD (W2_DESIGN 10.4 slices, V11): what the streamer, the
    /// instanced dressing and the life presenters submitted this frame (before frustum culling: an upper bound of what
    /// the GPU draws). Presenters in other assemblies add their share through <see cref="WorldRoot.ReportLife"/>.
    /// Engine-free.
    /// </summary>
    public struct RenderStats
    {
        public int TerrainTris, RoadTris, DecalTris, AreaTris;
        public int B0Tris, B1Tris, B2Tris, B3Tris, HeroTris;
        public int TreeTris, PropTris, ParkedTris;
        public int VehicleTris, PeopleTris, AnimalTris, AircraftTris;

        public int Trees, Props, Parked, Vehicles, People, Animals, Aircraft, Heroes, Cells;

        /// <summary>Instanced draw calls issued this frame (each up to 1,023 instances).</summary>
        public int InstancedDraws;

        public int BuildingTris
        {
            get { return B0Tris + B1Tris + B2Tris + B3Tris; }
        }

        public int TotalTris
        {
            get
            {
                return TerrainTris + RoadTris + DecalTris + AreaTris + BuildingTris + HeroTris + TreeTris + PropTris + ParkedTris + VehicleTris +
                       PeopleTris + AnimalTris + AircraftTris;
            }
        }

        /// <summary>
        /// Triangles the outline pass adds this frame on a tier (an upper bound, like the other counters: instances beyond
        /// the outline range are dropped whole in the outline's vertex shader, and nothing is frustum-culled here): the
        /// outlined categories of <see cref="WorldMaterialDefaults.RoleOf"/> — vehicles (moving and parked), people,
        /// animals, aircraft, heroes, plus props and the B0 band on High. Props are counted on every outlined tier while
        /// the dressing still draws them with the people's material (World/README.md "Look", open issues). The explorer is
        /// not counted.
        /// </summary>
        public int OutlinedTris(in ToonLookTier tier)
        {
            if (!tier.Outlines) return 0;
            int tris = VehicleTris + ParkedTris + PeopleTris + AnimalTris + AircraftTris + HeroTris + PropTris;
            if (tier.OutlineNearBuildings) tris += B0Tris;
            return tris;
        }

        /// <summary>Clears the life counters (presenters report them every frame).</summary>
        public void ClearLife()
        {
            VehicleTris = PeopleTris = AnimalTris = AircraftTris = ParkedTris = 0;
            Vehicles = People = Animals = Aircraft = Parked = 0;
        }

        /// <summary>Two lines for the debug HUD (allocates; a few times a second).</summary>
        public string Format()
        {
            var sb = new StringBuilder(200);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                            "tris {0}k  terr {1}k road {2}k bld {3}k (B0 {4}k/{5} cells B1 {6}k B2 {7}k B3 {8}k) hero {9}k/{10}",
                            TotalTris / 1000, TerrainTris / 1000, (RoadTris + DecalTris) / 1000, BuildingTris / 1000, B0Tris / 1000, Cells,
                            B1Tris / 1000, B2Tris / 1000, B3Tris / 1000, HeroTris / 1000, Heroes);
            sb.Append('\n');
            sb.AppendFormat(CultureInfo.InvariantCulture,
                            "veg {0}k/{1} props {2}k/{3} parked {4}k/{5} veh {6}k/{7} ppl {8}k/{9} anim {10}k/{11} air {12}k/{13} draws {14}",
                            TreeTris / 1000, Trees, PropTris / 1000, Props, ParkedTris / 1000, Parked, VehicleTris / 1000, Vehicles,
                            PeopleTris / 1000, People, AnimalTris / 1000, Animals, AircraftTris / 1000, Aircraft, InstancedDraws);
            return sb.ToString();
        }
    }
}
