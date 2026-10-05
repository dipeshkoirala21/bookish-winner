using Ghumante.Core.Data;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// What the foot (or tyre) touches, for footsteps and tyre sounds (W2_DESIGN 10.3; Track C maps each value to a
    /// 7.3 footstep set). Append-only: the byte values match <c>GenColliders</c>' material codes.
    /// </summary>
    public enum FootSurface : byte
    {
        Asphalt = 0,
        Concrete = 1,
        Brick = 2,
        Stone = 3,
        Gravel = 4,
        Dirt = 5,
        Mud = 6,
        Grass = 7,
        Wood = 8,
        Metal = 9,
        Water = 10,
    }

    /// <summary>
    /// The footstep surface rules of W2_DESIGN 10.3, in the order the ground query applies them: (1) a structure box or
    /// ramp's own material, (2) an AREA with a paving surface, (3) the road's surface, (4) the biome; then wetness
    /// from 0.5 turns DIRT into MUD (<see cref="Effective"/>, applied by whoever knows the weather).
    /// </summary>
    public static class FootSurfaces
    {
        public const int Count = 11;

        /// <summary>Footstep surface of a road: Asphalt, Concrete, Brick, Cobble→Stone, Gravel, Compacted→Gravel,
        /// Dirt, Mud, Wood, Metal; sand reads as dirt, rock as stone, grass as grass, snow and ice as gravel. An
        /// untagged surface is asphalt on motor roads, stone on steps and dirt on tracks and paths.</summary>
        public static FootSurface OfRoad(Surface s, RoadClass c)
        {
            switch (s)
            {
                case Surface.Asphalt: return FootSurface.Asphalt;
                case Surface.Concrete: return FootSurface.Concrete;
                case Surface.Brick: return FootSurface.Brick;
                case Surface.Cobble: return FootSurface.Stone;
                case Surface.Gravel:
                case Surface.Compacted:
                case Surface.SnowIce:
                    return FootSurface.Gravel;
                case Surface.Dirt:
                case Surface.Sand:
                    return FootSurface.Dirt;
                case Surface.Mud: return FootSurface.Mud;
                case Surface.Grass: return FootSurface.Grass;
                case Surface.Rock: return FootSurface.Stone;
                case Surface.Wood: return FootSurface.Wood;
                case Surface.Metal: return FootSurface.Metal;
            }
            switch (c)
            {
                case RoadClass.Steps: return FootSurface.Stone;
                case RoadClass.Track:
                case RoadClass.Path:
                case RoadClass.Footway:
                case RoadClass.Bridleway:
                    return FootSurface.Dirt;
                default:
                    return FootSurface.Asphalt;
            }
        }

        /// <summary>Footstep surface off the road network (BiomeGround order): built-up → Concrete, gardens and grass →
        /// Grass, fields and forest floors → Dirt, wetland and paddy → Mud, water → Water, riverbeds, scree and moraine →
        /// Gravel or Stone, snow and glacier → Gravel.</summary>
        public static FootSurface OfBiome(Biome b)
        {
            switch (b)
            {
                case Biome.UrbanDense: return FootSurface.Concrete;
                case Biome.UrbanGreen:
                case Biome.TeraiGrassland:
                case Biome.HillGrassland:
                case Biome.AlpineMeadow:
                    return FootSurface.Grass;
                case Biome.Water: return FootSurface.Water;
                case Biome.TeraiPaddy:
                case Biome.Wetland:
                    return FootSurface.Mud;
                case Biome.RiverbedGravel:
                case Biome.Moraine:
                case Biome.TransHimalayanSteppe:
                case Biome.Glacier:
                case Biome.Snow:
                    return FootSurface.Gravel;
                case Biome.ScreeRock: return FootSurface.Stone;
                default: return FootSurface.Dirt;
            }
        }

        /// <summary>True for an AREA whose ground is paved, with its footstep surface: heritage squares and pedestrian
        /// areas, compounds and markets are stone, courtyards (bahals and chowks) brick, car parks, bus parks and aprons
        /// concrete.</summary>
        public static bool TryOfArea(AreaKind k, out FootSurface f)
        {
            switch (k)
            {
                case AreaKind.Pedestrian:
                case AreaKind.Religious:
                case AreaKind.Marketplace:
                    f = FootSurface.Stone;
                    return true;
                case AreaKind.Courtyard:
                    f = FootSurface.Brick;
                    return true;
                case AreaKind.Parking:
                case AreaKind.BusPark:
                case AreaKind.Apron:
                    f = FootSurface.Concrete;
                    return true;
                default:
                    f = FootSurface.Asphalt;
                    return false;
            }
        }

        /// <summary>The surface after wetness: DIRT reads as MUD from 0.5 (as <see cref="VehicleSpec.Effective"/>).</summary>
        public static FootSurface Effective(FootSurface f, float wetness01)
        {
            return f == FootSurface.Dirt && wetness01 >= 0.5f ? FootSurface.Mud : f;
        }

        /// <summary>The physics surface group a footstep surface behaves like (for structures and paved areas).</summary>
        public static SurfaceGroup GroupOf(FootSurface f)
        {
            switch (f)
            {
                case FootSurface.Gravel: return SurfaceGroup.Gravel;
                case FootSurface.Dirt:
                case FootSurface.Grass:
                    return SurfaceGroup.Dirt;
                case FootSurface.Mud:
                case FootSurface.Water:
                    return SurfaceGroup.Mud;
                default: return SurfaceGroup.Paved;
            }
        }
    }
}
