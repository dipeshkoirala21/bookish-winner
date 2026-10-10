using System;

namespace Ghumante.Core.Meshing.Shapes
{
    /// <summary>
    /// What a shape paints its vertices with: the albedo tint (<c>0xRRGGBBAA</c>, alpha = instance tint weight as
    /// everywhere in <see cref="MeshData"/>), the <see cref="MaterialChannel"/> written to <c>Uv0.u</c> and the
    /// starting ambient occlusion written to <c>Uv0.v</c> (1 = open; <see cref="ShapeAo"/> multiplies into it later).
    /// Always build one with the constructor: <c>default(ShapeBrush)</c> is transparent black and fully occluded.
    /// </summary>
    public struct ShapeBrush
    {
        public uint Color;
        public MaterialChannel Channel;
        public float Ao;

        public ShapeBrush(uint color, MaterialChannel channel = MaterialChannel.Plain, float ao = 1f)
        {
            Color = color;
            Channel = channel;
            Ao = ao;
        }

        /// <summary>An opaque brush from a <c>0xRRGGBB</c> hex value.</summary>
        public static ShapeBrush Hex(uint rgb, MaterialChannel channel = MaterialChannel.Plain)
        {
            return new ShapeBrush(MeshColor.FromHex(rgb), channel);
        }

        public ShapeBrush WithColor(uint color)
        {
            ShapeBrush b = this;
            b.Color = color;
            return b;
        }

        public ShapeBrush WithChannel(MaterialChannel channel)
        {
            ShapeBrush b = this;
            b.Channel = channel;
            return b;
        }

        public ShapeBrush WithAo(float ao)
        {
            ShapeBrush b = this;
            b.Ao = ao;
            return b;
        }
    }

    /// <summary>
    /// Level of detail for the shape emitters: every primitive takes its LOD0 segment counts and a
    /// <see cref="ShapeLod"/>, and scales them down (LOD1 halves, LOD2 quarters, LOD3 eighths) with sensible
    /// minimums, so a caller writes one recipe and asks for any level. <c>default(ShapeLod)</c> is LOD0.
    /// </summary>
    public readonly struct ShapeLod
    {
        public readonly int Level;

        public ShapeLod(int level)
        {
            Level = level < 0 ? 0 : level > 3 ? 3 : level;
        }

        public static ShapeLod Lod0
        {
            get { return new ShapeLod(0); }
        }

        public static ShapeLod Lod1
        {
            get { return new ShapeLod(1); }
        }

        public static ShapeLod Lod2
        {
            get { return new ShapeLod(2); }
        }

        /// <summary>Segment scale of this level: 1, 1/2, 1/4, 1/8.</summary>
        public double Factor
        {
            get { return 1.0 / (1 << Level); }
        }

        /// <summary>Segments around an axis (circles, lathes, tubes): at least 3; LOD2+ keeps at least 4 when the
        /// LOD0 count had 6 or more.</summary>
        public int Radial(int lod0)
        {
            if (lod0 < 3) lod0 = 3;
            int n = (int)Math.Round(lod0 * Factor);
            int min = lod0 >= 6 ? 4 : 3;
            if (Level >= 2 && lod0 >= 12) min = 6;
            return n < min ? Math.Min(min, lod0) : n;
        }

        /// <summary>Segments of a bevel or fillet arc: at least 1 (a chamfer).</summary>
        public int Bevel(int lod0)
        {
            if (lod0 < 1) return lod0 < 0 ? 0 : lod0;
            int n = (int)Math.Round(lod0 * Factor);
            return n < 1 ? 1 : n;
        }

        /// <summary>Segments along a path or a grid side: at least 1.</summary>
        public int Path(int lod0)
        {
            if (lod0 < 1) return 1;
            int n = (int)Math.Round(lod0 * Factor);
            return n < 1 ? 1 : n;
        }
    }
}
