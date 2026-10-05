using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;

namespace Ghumante.World.Sacred
{
    /// <summary>
    /// The hero replicas of a region (W2_DESIGN 3.4) indexed by the level-10 tile that anchors them, plus the D5 hide
    /// zones: every BLDG reference a hero replaces (its curated <c>hidden</c> list, its footprint and the <c>hide</c>
    /// anchors), which the building meshers skip (<c>BuildingOptions.HiddenRefs</c>), so no unhidden building ever
    /// intersects a hero (G7). Built once when the region opens, from the region's curated DB (<c>.ghcd</c>) or, when
    /// the region has none, from <see cref="HeroCatalog.S1"/>. Read-only afterwards, so worker builds share it.
    /// Engine-free.
    /// </summary>
    public sealed class HeroSet
    {
        /// <summary>Level of the tiles heroes are built with (the exact detail level).</summary>
        public const int AnchorLevel = 10;

        private readonly Dictionary<ulong, List<HeritageRecord>> _byTile = new Dictionary<ulong, List<HeritageRecord>>();
        private readonly HashSet<ulong> _hidden = new HashSet<ulong>();
        private static readonly List<HeritageRecord> None = new List<HeritageRecord>(0);
        private int _count;

        /// <summary>BLDG references hidden under heroes.</summary>
        public ISet<ulong> HiddenRefs
        {
            get { return _hidden; }
        }

        /// <summary>Heroes with an anchor tile.</summary>
        public int Count
        {
            get { return _count; }
        }

        /// <summary>The heroes anchored in level-10 tile <paramref name="tileKey"/> (empty when none), in id order.</summary>
        public IReadOnlyList<HeritageRecord> InTile(ulong tileKey)
        {
            List<HeritageRecord> l;
            return _byTile.TryGetValue(tileKey, out l) ? l : None;
        }

        /// <summary>Heroes from a curated DB (null: the S1 catalogue).</summary>
        public static HeroSet From(CuratedDb db)
        {
            var set = new HeroSet();
            if (db != null && db.Count > 0)
            {
                foreach (HeritageRecord r in db.Heritage) set.Add(r);
            }
            else
            {
                foreach (HeroRecipe r in HeroCatalog.S1) set.Add(HeroCatalog.ToRecord(r));
            }
            foreach (List<HeritageRecord> l in set._byTile.Values) l.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return set;
        }

        /// <summary>Adds one hero (anchor tile from the record, its position or its lon/lat; skipped without any).</summary>
        public void Add(HeritageRecord r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            ulong key;
            if (!TryAnchorTile(r, out key)) return;
            List<HeritageRecord> l;
            if (!_byTile.TryGetValue(key, out l))
            {
                l = new List<HeritageRecord>(2);
                _byTile.Add(key, l);
            }
            l.Add(r);
            _count++;
            if (r.Hidden != null)
                for (int i = 0; i < r.Hidden.Length; i++)
                    if (r.Hidden[i] != 0) _hidden.Add(r.Hidden[i]);
            ulong footprint = r.AnchorWayRelationRef;
            if (footprint != 0) _hidden.Add(footprint);
            if (r.Hide != null)
                for (int i = 0; i < r.Hide.Length; i++)
                {
                    ulong h = HeritageRecord.WayRelationRef(r.Hide[i]);
                    if (h != 0) _hidden.Add(h);
                }
        }

        /// <summary>The level-10 tile key a hero is built in.</summary>
        public static bool TryAnchorTile(HeritageRecord r, out ulong key)
        {
            key = 0;
            if (r.AnchorTileKey != 0)
            {
                TileId t = TileId.FromKey(r.AnchorTileKey);
                key = t.Level == AnchorLevel ? r.AnchorTileKey : TileId.At(AnchorLevel, t.X0 + 1, t.Z0 + 1).Key;
                if (t.Level == AnchorLevel) return true;
            }
            double x = r.X, z = r.Z;
            if (double.IsNaN(x) || double.IsNaN(z))
            {
                if (double.IsNaN(r.Lat) || double.IsNaN(r.Lon)) return key != 0;
                WorldFrame.LonLatToGame(r.Lon, r.Lat, out x, out z);
            }
            key = TileId.At(AnchorLevel, x, z).Key;
            return true;
        }
    }
}
