using System;
using System.Collections.Generic;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// A tiny procedural stroke font for number plates (W2_DESIGN 5.2): the Devanagari digits ०–९, the zone code बा (ब +
    /// the ा sign) and the class letters क ख ग च ज झ प फ ब, drawn as straight strokes on a 4 × 6 grid (x right, y up)
    /// with the headline (shirorekha) on the letters. Cartoon shapes, readable at plate scale; no font asset ships.
    /// </summary>
    public static class PlateGlyphs
    {
        /// <summary>Glyph cell: width 4, height 6 units; advance 4.6 units; a space advances 2.4.</summary>
        public const float CellW = 4f, CellH = 6f, Advance = 4.6f, SpaceAdvance = 2.4f;

        private static readonly Dictionary<char, float[]> Strokes = Build();

        /// <summary>True when the font has the character (spaces count).</summary>
        public static bool Has(char c)
        {
            return c == ' ' || Strokes.ContainsKey(c);
        }

        /// <summary>The strokes of a character as (x0, y0, x1, y1) quadruples, or an empty array.</summary>
        public static float[] Of(char c)
        {
            float[] s;
            return Strokes.TryGetValue(c, out s) ? s : new float[0];
        }

        /// <summary>Width of a text line in glyph units (the ा sign joins its consonant, half an advance).</summary>
        public static float Width(string text)
        {
            float w = 0f;
            foreach (char c in text) w += AdvanceOf(c);
            return Math.Max(0f, w - (Advance - CellW));
        }

        public static float AdvanceOf(char c)
        {
            if (c == ' ') return SpaceAdvance;
            if (c == 'ा') return 2.2f;
            return Advance;
        }

        private static Dictionary<char, float[]> Build()
        {
            var d = new Dictionary<char, float[]>();
            // Digits (no headline).
            d['०'] = Poly(2, 0.6f, 3.3f, 1.6f, 3.3f, 3.6f, 2, 4.6f, 0.7f, 3.6f, 0.7f, 1.6f, 2, 0.6f);
            d['१'] = Poly(2.6f, 5, 1.2f, 4.4f, 1.2f, 3.4f, 2.4f, 3.0f, 3.1f, 2.2f, 2.0f, 0.4f);
            d['२'] = Poly(1, 4.2f, 2, 5, 3, 4.2f, 1.2f, 1.6f, 3.2f, 0.6f);
            d['३'] = Concat(Poly(1, 5, 3, 4.4f, 2, 3), Poly(2, 3, 3.1f, 1.7f, 1, 0.6f));
            d['४'] = Concat(Poly(1, 5, 3, 2.2f, 2, 0.6f, 1, 2.2f, 3, 5));
            d['५'] = Concat(Seg(0.8f, 5, 3.2f, 5), Poly(2, 5, 2, 3, 3, 2, 2, 0.6f, 1, 1.5f));
            d['६'] = Poly(3, 5, 1.2f, 4, 2.2f, 3, 3, 1.9f, 2, 0.6f, 1, 1.3f);
            d['७'] = Poly(1, 4.2f, 3.2f, 5, 2.1f, 0.6f);
            d['८'] = Poly(0.9f, 0.6f, 2, 4.6f, 3.1f, 0.6f);
            d['९'] = Concat(Poly(3, 3.2f, 2, 4.6f, 1, 3.6f, 2, 2.6f, 3, 3.2f), Seg(3, 3.2f, 2.4f, 0.4f));
            // Letters with the headline and (most) a right stem.
            float[] head = Seg(0, 5.6f, 4, 5.6f), stem = Seg(3.3f, 5.6f, 3.3f, 0.4f);
            // ब: the loop with its inner diagonal (प below has no diagonal).
            d['ब'] = Concat(head, stem, Poly(0.8f, 5.6f, 0.8f, 3, 1.6f, 2.1f, 3.3f, 2.1f), Seg(0.9f, 4.6f, 2.5f, 2.4f));
            d['ा'] = Concat(Seg(0, 5.6f, 1.6f, 5.6f), Seg(1.2f, 5.6f, 1.2f, 0.4f)); // ा
            d['प'] = Concat(head, stem, Poly(0.8f, 5.6f, 0.8f, 2.6f, 3.3f, 2.6f));
            d['क'] = Concat(head, Seg(2, 5.6f, 2, 0.4f), Poly(0.7f, 3.6f, 2, 2.6f, 3.3f, 3.4f, 2.6f, 4.2f, 2, 3.2f));
            d['च'] = Concat(head, stem, Poly(0.7f, 4.2f, 2.6f, 4.2f, 1.1f, 3, 3.3f, 2.4f));
            d['ख'] = Concat(head, stem, Poly(0.8f, 5.6f, 0.8f, 3, 1.6f, 2.4f, 2.2f, 3.4f, 3.3f, 3.4f));
            d['ज'] = Concat(head, stem, Poly(2, 5.6f, 2, 3, 1, 2, 2, 1));
            d['फ'] = Concat(head, stem, Poly(0.8f, 5.6f, 0.8f, 2.6f, 3.3f, 2.6f), Seg(3.3f, 3.6f, 4, 4.4f));
            d['ग'] = Concat(head, stem, Seg(1.5f, 5.6f, 1.5f, 1.2f));
            d['झ'] = Concat(head, stem, Poly(0.8f, 4.6f, 2.4f, 4.6f, 1, 3, 2.4f, 2, 1.6f, 0.6f));
            return d;
        }

        private static float[] Seg(float x0, float y0, float x1, float y1)
        {
            return new[] { x0, y0, x1, y1 };
        }

        /// <summary>A polyline through (x, y) pairs as consecutive strokes.</summary>
        private static float[] Poly(params float[] xy)
        {
            int n = xy.Length / 2;
            var s = new float[(n - 1) * 4];
            for (int i = 0; i + 1 < n; i++)
            {
                s[4 * i] = xy[2 * i];
                s[4 * i + 1] = xy[2 * i + 1];
                s[4 * i + 2] = xy[2 * i + 2];
                s[4 * i + 3] = xy[2 * i + 3];
            }
            return s;
        }

        private static float[] Concat(params float[][] parts)
        {
            int n = 0;
            foreach (float[] p in parts) n += p.Length;
            var r = new float[n];
            int o = 0;
            foreach (float[] p in parts)
            {
                Array.Copy(p, 0, r, o, p.Length);
                o += p.Length;
            }
            return r;
        }
    }
}
