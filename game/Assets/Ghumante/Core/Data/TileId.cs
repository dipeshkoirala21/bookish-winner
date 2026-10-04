using System;
using System.Globalization;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// Quadtree tile address (DATA_FORMATS.md section 0, projection.py): a tile at level <c>L</c> has side
    /// <c>2^(20-L)</c> m and covers <c>[tx·S, (tx+1)·S) × [ty·S, (ty+1)·S)</c> in game metres, <c>ty</c>
    /// growing north. The 64-bit key is <c>(L &lt;&lt; 58) | morton(tx, ty)</c>.
    /// </summary>
    public readonly struct TileId : IEquatable<TileId>, IComparable<TileId>
    {
        public const int RootLevelBits = 20;
        public const int MaxLevel = 16;
        public const double RootSizeM = 1048576.0;

        public readonly int Level;
        public readonly int Tx;
        public readonly int Ty;

        public TileId(int level, int tx, int ty)
        {
            if (level < 0 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            int n = 1 << level;
            if (tx < 0 || tx >= n || ty < 0 || ty >= n)
                throw new ArgumentOutOfRangeException(nameof(tx), "tile " + level + "/" + tx + "/" + ty + " outside quadtree");
            Level = level;
            Tx = tx;
            Ty = ty;
        }

        public static double SizeAt(int level)
        {
            if (level < 0 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            return RootSizeM / (1 << level);
        }

        /// <summary>Side length in metres.</summary>
        public double Size
        {
            get { return SizeAt(Level); }
        }

        /// <summary>West edge in game metres.</summary>
        public double X0
        {
            get { return Tx * Size; }
        }

        /// <summary>South edge in game metres.</summary>
        public double Z0
        {
            get { return Ty * Size; }
        }

        public ulong Key
        {
            get { return (ulong)Level << 58 | Morton(Tx, Ty); }
        }

        /// <summary>Interleave the low 29 bits: tx on even bits, ty on odd bits.</summary>
        public static ulong Morton(int tx, int ty)
        {
            return Part1By1((uint)tx) | Part1By1((uint)ty) << 1;
        }

        public static TileId FromKey(ulong key)
        {
            int level = (int)(key >> 58);
            ulong m = key & ((1UL << 58) - 1);
            return new TileId(level, (int)Compact1By1(m), (int)Compact1By1(m >> 1));
        }

        /// <summary>The tile at <paramref name="level"/> containing game point (x, z).</summary>
        public static TileId At(int level, double x, double z)
        {
            double s = SizeAt(level);
            return new TileId(level, (int)Math.Floor(x / s), (int)Math.Floor(z / s));
        }

        public TileId Parent()
        {
            if (Level == 0) throw new InvalidOperationException("root has no parent");
            return new TileId(Level - 1, Tx >> 1, Ty >> 1);
        }

        /// <summary>Children in the order SW, SE, NW, NE (as projection.TileId.children).</summary>
        public TileId[] Children()
        {
            return new[]
            {
                new TileId(Level + 1, Tx * 2, Ty * 2), new TileId(Level + 1, Tx * 2 + 1, Ty * 2),
                new TileId(Level + 1, Tx * 2, Ty * 2 + 1), new TileId(Level + 1, Tx * 2 + 1, Ty * 2 + 1),
            };
        }

        public bool Contains(double x, double z)
        {
            double s = Size, x0 = Tx * s, z0 = Ty * s;
            return x >= x0 && x < x0 + s && z >= z0 && z < z0 + s;
        }

        private static ulong Part1By1(uint v)
        {
            ulong x = v & ((1u << 29) - 1);
            x = (x | x << 16) & 0x0000FFFF0000FFFFUL;
            x = (x | x << 8) & 0x00FF00FF00FF00FFUL;
            x = (x | x << 4) & 0x0F0F0F0F0F0F0F0FUL;
            x = (x | x << 2) & 0x3333333333333333UL;
            x = (x | x << 1) & 0x5555555555555555UL;
            return x;
        }

        private static ulong Compact1By1(ulong x)
        {
            x &= 0x5555555555555555UL;
            x = (x | x >> 1) & 0x3333333333333333UL;
            x = (x | x >> 2) & 0x0F0F0F0F0F0F0F0FUL;
            x = (x | x >> 4) & 0x00FF00FF00FF00FFUL;
            x = (x | x >> 8) & 0x0000FFFF0000FFFFUL;
            x = (x | x >> 16) & 0x00000000FFFFFFFFUL;
            return x & ((1UL << 29) - 1);
        }

        public bool Equals(TileId other)
        {
            return Level == other.Level && Tx == other.Tx && Ty == other.Ty;
        }

        public override bool Equals(object obj)
        {
            return obj is TileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public int CompareTo(TileId other)
        {
            return Key.CompareTo(other.Key);
        }

        public static bool operator ==(TileId a, TileId b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TileId a, TileId b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}", Level, Tx, Ty);
        }
    }
}
