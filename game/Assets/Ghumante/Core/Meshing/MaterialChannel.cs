namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Surface material of a vertex for the procedural-texture toon shader (docs/W2_DETAIL_CONTRACT.md §5).
    /// Encoded in <see cref="MeshData.Uv0"/>: u = (float)channel, v = baked ambient occlusion (1 = open, 0 = fully
    /// occluded). Meshes without UV0 render as <see cref="Plain"/> with no AO (the W1/W2 stage 1 look). The vertex
    /// colour stays the albedo tint; its alpha keeps its existing meaning (instance tint weight).
    /// </summary>
    public enum MaterialChannel : byte
    {
        Plain = 0,
        Plaster = 1,
        Brick = 2,
        BrickGlazed = 3,
        Wood = 4,
        WoodCarved = 5,
        RoofTile = 6,
        Metal = 7,
        Gilt = 8,
        Stone = 9,
        Asphalt = 10,
        Concrete = 11,
        Grass = 12,
        Foliage = 13,
        Bark = 14,
        Fabric = 15,
        Skin = 16,
        Glass = 17,
        Water = 18,
        Paint = 19,
        Rubber = 20,
        Dirt = 21,
        Flagstone = 22,
        Hair = 23,
        Leather = 24,
        Marking = 25,
    }
}
