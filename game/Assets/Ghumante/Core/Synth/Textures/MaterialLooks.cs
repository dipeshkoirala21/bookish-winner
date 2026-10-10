using Ghumante.Core.Meshing;

namespace Ghumante.Core.Synth.Textures
{
    /// <summary>
    /// How one <see cref="MaterialChannel"/> is shaded by <c>Ghumante/ToonLit</c> (World/README.md "Look"): the texture
    /// repeat and the toon highlight, glint, sky reflection, large-scale variation and drift of the surface. The Unity
    /// side uploads the table as two global vector arrays (<c>_GhChannelA</c> = 1 / tile, specular, gloss, sparkle;
    /// <c>_GhChannelB</c> = metallic, reflect, macro, flow) that the vertex shader reads once per vertex.
    /// </summary>
    public readonly struct MaterialLook
    {
        /// <summary>Metres per texture repeat (object space, triplanar).</summary>
        public readonly float TileM;

        /// <summary>Strength of the toon highlight (0 = matte).</summary>
        public readonly float Specular;

        /// <summary>Blinn-Phong exponent of the highlight (larger = smaller, sharper spot).</summary>
        public readonly float Gloss;

        /// <summary>View-dependent glints (gilt copper, sunlit water), 0..1.</summary>
        public readonly float Sparkle;

        /// <summary>0 = white highlight (paint, glass), 1 = the highlight takes the surface colour (metal, gilt).</summary>
        public readonly float Metallic;

        /// <summary>Sky reflection at grazing angles (glass, water), 0..1.</summary>
        public readonly float Reflect;

        /// <summary>Strength of the large-scale brightness variation that hides the texture repeat (ground, walls).</summary>
        public readonly float Macro;

        /// <summary>Texture drift in metres per second along object x (water).</summary>
        public readonly float Flow;

        public MaterialLook(float tileM, float specular, float gloss, float sparkle, float metallic, float reflect, float macro, float flow)
        {
            TileM = tileM;
            Specular = specular;
            Gloss = gloss;
            Sparkle = sparkle;
            Metallic = metallic;
            Reflect = reflect;
            Macro = macro;
            Flow = flow;
        }
    }

    /// <summary>The <see cref="MaterialLook"/> of every channel (engine-free; the shader's twin values).</summary>
    public static class MaterialLooks
    {
        /// <summary>Size of the shader's channel arrays (<c>_GhChannelA[32]</c>, <c>_GhChannelB[32]</c>); channels at or
        /// above it render as <see cref="MaterialChannel.Plain"/>.</summary>
        public const int ArraySize = 32;

        /// <summary>Metres per repeat of the macro-variation layer (the <see cref="MaterialChannel.Plain"/> slice, sampled
        /// a second time at this scale to break up the repeat of ground and wall textures).</summary>
        public const float MacroTileM = 23f;

        // Indexed by MaterialChannel. Tile sizes follow the real objects with a cartoon enlargement: a 0.3 m brick with
        // 75 mm courses, 0.15 m jhingati tiles in 0.1 m rows, 0.5 m flagstones, 4 m of asphalt per repeat.
        private static readonly MaterialLook[] Table =
        {
            new MaterialLook(1.0f, 0f, 8f, 0f, 0f, 0f, 0f, 0f),        // Plain (macro slice)
            new MaterialLook(2.0f, 0f, 8f, 0f, 0f, 0f, 0.6f, 0f),       // Plaster
            new MaterialLook(0.6f, 0.05f, 12f, 0f, 0f, 0f, 0.5f, 0f),   // Brick
            new MaterialLook(0.6f, 0.45f, 40f, 0f, 0f, 0.15f, 0.3f, 0f), // BrickGlazed (dachi apa)
            new MaterialLook(1.0f, 0.08f, 16f, 0f, 0f, 0f, 0.3f, 0f),   // Wood
            new MaterialLook(0.4f, 0.10f, 16f, 0f, 0f, 0f, 0.2f, 0f),   // WoodCarved
            new MaterialLook(0.6f, 0.05f, 12f, 0f, 0f, 0f, 0.6f, 0f),   // RoofTile (jhingati)
            new MaterialLook(1.0f, 0.60f, 24f, 0f, 0.6f, 0.25f, 0.4f, 0f), // Metal
            new MaterialLook(0.5f, 0.90f, 32f, 1f, 1f, 0.3f, 0.2f, 0f), // Gilt
            new MaterialLook(1.2f, 0.05f, 10f, 0f, 0f, 0f, 0.6f, 0f),   // Stone
            new MaterialLook(4.0f, 0.03f, 10f, 0f, 0f, 0f, 1f, 0f),     // Asphalt
            new MaterialLook(2.0f, 0.03f, 10f, 0f, 0f, 0f, 0.8f, 0f),   // Concrete
            new MaterialLook(1.5f, 0f, 8f, 0f, 0f, 0f, 1f, 0f),         // Grass
            new MaterialLook(1.0f, 0.06f, 10f, 0f, 0f, 0f, 0.5f, 0f),   // Foliage
            new MaterialLook(1.0f, 0f, 8f, 0f, 0f, 0f, 0.3f, 0f),       // Bark
            new MaterialLook(0.15f, 0f, 8f, 0f, 0f, 0f, 0f, 0f),        // Fabric
            new MaterialLook(0.3f, 0.12f, 14f, 0f, 0f, 0f, 0f, 0f),     // Skin
            new MaterialLook(1.2f, 0.90f, 64f, 0f, 0f, 0.6f, 0f, 0f),   // Glass
            new MaterialLook(3.0f, 0.80f, 48f, 0.3f, 0f, 0.5f, 0.5f, 0.25f), // Water
            new MaterialLook(1.0f, 0.35f, 28f, 0f, 0f, 0.1f, 0.3f, 0f), // Paint
            new MaterialLook(0.25f, 0.05f, 8f, 0f, 0f, 0f, 0f, 0f),     // Rubber
            new MaterialLook(2.0f, 0f, 8f, 0f, 0f, 0f, 1f, 0f),         // Dirt
            new MaterialLook(2.0f, 0.05f, 12f, 0f, 0f, 0f, 0.8f, 0f),   // Flagstone
            new MaterialLook(0.2f, 0.25f, 20f, 0f, 0f, 0f, 0f, 0f),     // Hair
            new MaterialLook(0.3f, 0.20f, 18f, 0f, 0f, 0f, 0f, 0f),     // Leather
            new MaterialLook(2.0f, 0.05f, 12f, 0f, 0f, 0f, 0.5f, 0f),   // Marking
        };

        /// <summary>Number of channels with a look (and a texture slice): <see cref="MaterialChannel.Marking"/> + 1.</summary>
        public static int Count
        {
            get { return Table.Length; }
        }

        /// <summary>The look of a channel; unknown channels get the <see cref="MaterialChannel.Plain"/> look.</summary>
        public static MaterialLook Of(MaterialChannel channel)
        {
            int i = (int)channel;
            return i >= 0 && i < Table.Length ? Table[i] : Table[0];
        }

        /// <summary>
        /// The shader arrays as flat xyzw quadruples (<paramref name="a"/> and <paramref name="b"/> hold
        /// <see cref="ArraySize"/> × 4 floats): a = (1 / tile, specular, gloss, sparkle), b = (metallic, reflect, macro,
        /// flow). Entries past <see cref="Count"/> repeat the Plain look.
        /// </summary>
        public static void FillShaderArrays(float[] a, float[] b)
        {
            if (a == null || a.Length < ArraySize * 4) throw new System.ArgumentException("a needs ArraySize * 4 floats", nameof(a));
            if (b == null || b.Length < ArraySize * 4) throw new System.ArgumentException("b needs ArraySize * 4 floats", nameof(b));
            for (int i = 0; i < ArraySize; i++)
            {
                MaterialLook l = Of((MaterialChannel)i);
                a[i * 4] = 1f / l.TileM;
                a[i * 4 + 1] = l.Specular;
                a[i * 4 + 2] = l.Gloss;
                a[i * 4 + 3] = l.Sparkle;
                b[i * 4] = l.Metallic;
                b[i * 4 + 1] = l.Reflect;
                b[i * 4 + 2] = l.Macro;
                b[i * 4 + 3] = l.Flow;
            }
        }
    }
}
