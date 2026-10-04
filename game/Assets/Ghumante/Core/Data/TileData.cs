using System;
using System.Collections.Generic;

namespace Ghumante.Core.Data
{
    // Decoded GHT1 records (DATA_FORMATS.md section 1; tile_format.py). Point lists are interleaved
    // local centimetres relative to the tile's south-west corner: {x0, z0, x1, z1, ...}.

    public sealed class RoadRecord
    {
        public ulong OsmWayId;
        public RoadClass RoadClass;
        public Surface Surface;
        public SurfaceSource SurfaceSource;
        public RoadFlags Flags;
        public byte Lanes;
        public SacScale SacScale;
        public byte TrailVisibility;
        public sbyte Layer;
        public ulong WidthCm;
        public Travel Access;
        public int NameRef;
        public int RefRef;
        public int[] Points;

        public int PointCount
        {
            get { return Points.Length / 2; }
        }

        public bool HasPrevContext
        {
            get { return (Flags & RoadFlags.HasPrevCtx) != 0; }
        }

        public bool HasNextContext
        {
            get { return (Flags & RoadFlags.HasNextCtx) != 0; }
        }
    }

    [Flags]
    public enum LineFlags : byte
    {
        None = 0,
        HasPrevCtx = 1,
        HasNextCtx = 2,
        Intermittent = 4,
        Tunnel = 8,
    }

    public sealed class LineRecord
    {
        public ulong OsmWayId;
        public LineKind Kind;
        public LineFlags Flags;
        public ulong WidthCm;
        public int NameRef;
        public int[] Points;

        public int PointCount
        {
            get { return Points.Length / 2; }
        }
    }

    public sealed class BuildingRecord
    {
        public ulong OsmRef;
        public BuildingArchetype Archetype;
        public BuildingUse Use;
        public byte Levels;
        public BuildingFlags Flags;
        public ulong HeightCm;
        public ulong MinHeightCm;
        public RoofShape RoofShape;
        public RoofMaterial RoofMaterial;
        public WallMaterial WallMaterial;
        public uint Seed;
        public int NameRef;

        /// <summary>Ring 0 is the outer ring (counter-clockwise), the rest holes (clockwise); not closed.</summary>
        public int[][] Rings;
    }

    [Flags]
    public enum AreaFlags : byte
    {
        None = 0,
        ClippedByTile = 1,
    }

    public sealed class AreaRecord
    {
        public ulong OsmRef;
        public AreaKind Kind;
        public AreaFlags Flags;
        public int NameRef;
        public int[] Vertices;

        /// <summary>Triangle vertex indices, counter-clockwise, a multiple of 3.</summary>
        public int[] Indices;

        /// <summary>Boundary rings as (start, count) pairs into <see cref="Vertices"/>, interleaved.</summary>
        public int[] Rings;
    }

    public sealed class PoiRecord
    {
        public ulong OsmRef;
        public PoiKind Kind;
        public PoiFlags Flags;
        public byte Importance;
        public int XCm;
        public int ZCm;
        public int EleDm;
        public int NameRef;

        /// <summary>Entry index in the region search index + 1, 0 = not searchable.</summary>
        public ulong SearchId;
    }

    public struct ChunkInfo
    {
        public uint FourCC;
        public byte Codec;
        public uint Offset;
        public uint StoredSize;

        public string FourCCString
        {
            get { return Ght.FourCCToString(FourCC); }
        }
    }

    /// <summary>A decoded GHT1 tile. Absent chunks leave empty lists or null grids.</summary>
    public sealed class TileData
    {
        public TileId Tile;
        public ushort Version;
        public ushort Flags;
        public uint DataVersion;
        public uint PayloadCrc32;
        public ChunkInfo[] Chunks = new ChunkInfo[0];

        public int HeightsN;

        /// <summary>Global u16 height codes, row-major, row 0 south (<see cref="HeightAt"/> dequantises).</summary>
        public ushort[] HeightsQ;

        public int BiomesN;
        public Biome[] Biomes;

        public readonly List<NameRecord> Names = new List<NameRecord>();
        public readonly List<RoadRecord> Roads = new List<RoadRecord>();
        public readonly List<LineRecord> Lines = new List<LineRecord>();
        public readonly List<BuildingRecord> Buildings = new List<BuildingRecord>();
        public readonly List<AreaRecord> Areas = new List<AreaRecord>();
        public readonly List<PoiRecord> Pois = new List<PoiRecord>();

        public bool HasSeed;
        public ulong TileSeed;
        public ushort ScatterRuleset;

        /// <summary>The META chunk's JSON text, or null.</summary>
        public string MetaJson;

        public bool HasDetail
        {
            get { return (Flags & Ght.FlagHasDetail) != 0; }
        }

        /// <summary>Resolve a name_ref (0 = no name).</summary>
        public NameRecord Name(int nameRef)
        {
            return nameRef == 0 ? null : Names[nameRef - 1];
        }

        /// <summary>Height in metres of sample (row j from the south, column i from the west).</summary>
        public float HeightAt(int j, int i)
        {
            return Ght.Dequantize(HeightsQ[j * HeightsN + i]);
        }

        /// <summary>All heights in metres (row-major).</summary>
        public float[] HeightsMetres()
        {
            if (HeightsQ == null) return null;
            var h = new float[HeightsQ.Length];
            for (int k = 0; k < h.Length; k++) h[k] = Ght.Dequantize(HeightsQ[k]);
            return h;
        }

        /// <summary>Game X/Z of a local centimetre point.</summary>
        public void LocalToGame(int xCm, int zCm, out double x, out double z)
        {
            x = Tile.X0 + xCm / 100.0;
            z = Tile.Z0 + zCm / 100.0;
        }
    }
}
