using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;

namespace Ghumante.App.Explore
{
    /// <summary>
    /// "Where am I?" for the HUD (M1 track D): the name of the nearest landmark-like POI within <see cref="PoiRadiusM"/>
    /// from the loaded level-10 tiles around the explorer (temples, stupas, squares, museums, parks...; never businesses
    /// or brands, ADR-010), else the place (neighbourhood, village, town, city) whose own radius covers the explorer
    /// best (distance / radius; places come from the region's search index, else from the tiles). <see cref="Update"/> adds a little
    /// hysteresis: a new name must win twice in a row, so the label does not flicker on a boundary. Engine-free, no
    /// allocation per update.
    /// </summary>
    public sealed class PlaceNamer
    {
        /// <summary>A POI names the spot within this distance.</summary>
        public const double PoiRadiusM = 60.0;

        private struct Place
        {
            public double X, Z;
            public double Radius;
            public NameRecord Name;
        }

        private readonly Place[] _places;
        private NameRecord _pending;
        private bool _hasPending;

        /// <param name="index">The region's search index (null: places come from the loaded tiles only).</param>
        public PlaceNamer(SearchIndexData index)
        {
            var places = new List<Place>();
            if (index != null && index.Entries != null)
            {
                foreach (SearchEntry e in index.Entries)
                {
                    if (!e.IsPlace || e.Name == null || e.Name.IsEmpty) continue;
                    double r = PlaceRadius(e.PlaceKind);
                    if (r > 0) places.Add(new Place { X = e.X, Z = e.Z, Radius = r, Name = e.Name });
                }
            }
            _places = places.ToArray();
            HasIndexPlaces = _places.Length > 0;
        }

        /// <summary>True when places come from the search index.</summary>
        public bool HasIndexPlaces { get; private set; }

        /// <summary>The name shown now (null: none).</summary>
        public NameRecord Current { get; private set; }

        /// <summary>Re-evaluates at (x, z); returns true when <see cref="Current"/> changed.</summary>
        public bool Update(TileGroundQuery ground, double x, double z)
        {
            NameRecord candidate = Resolve(ground, x, z);
            if (Same(candidate, Current))
            {
                _hasPending = false;
                return false;
            }
            if (Current == null || _hasPending && Same(candidate, _pending))
            {
                Current = candidate;
                _hasPending = false;
                _pending = null;
                return true;
            }
            _pending = candidate;
            _hasPending = true;
            return false;
        }

        /// <summary>Forgets the current name (after a teleport).</summary>
        public void Reset()
        {
            Current = null;
            _pending = null;
            _hasPending = false;
        }

        /// <summary>The best name for (x, z) right now, without hysteresis (null: none): the nearest landmark-like POI
        /// within <see cref="PoiRadiusM"/>, else the place whose radius covers the point most (distance / radius).</summary>
        public NameRecord Resolve(TileGroundQuery ground, double x, double z)
        {
            NameRecord poi = null, place = null;
            double poiDistance = PoiRadiusM, placeScore = 1.0;
            if (ground != null)
            {
                TileId centre = TileId.At(10, x, z);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        RoadSpatialIndex roads = ground.RoadIndexOf(new TileId(10, centre.Tx + dx, centre.Ty + dy));
                        TileData tile = roads != null ? roads.Tile : null;
                        if (tile == null) continue;
                        ScoreTile(tile, x, z, ref poi, ref poiDistance, ref place, ref placeScore);
                    }
                }
            }
            for (int i = 0; i < _places.Length; i++)
            {
                double ddx = _places[i].X - x, ddz = _places[i].Z - z;
                double score = Math.Sqrt(ddx * ddx + ddz * ddz) / _places[i].Radius;
                if (score <= placeScore)
                {
                    placeScore = score;
                    place = _places[i].Name;
                }
            }
            return poi ?? place;
        }

        private void ScoreTile(TileData tile, double x, double z, ref NameRecord poi, ref double poiDistance,
                               ref NameRecord place, ref double placeScore)
        {
            List<PoiRecord> pois = tile.Pois;
            for (int i = 0; i < pois.Count; i++)
            {
                PoiRecord p = pois[i];
                if (p.NameRef <= 0) continue;
                int kind = (int)p.Kind;
                bool isPlace = kind >= SearchEntry.PlaceKindOffset;
                double radius = 0.0;
                if (isPlace)
                {
                    if (HasIndexPlaces) continue; // the index has them, with merged names
                    radius = PlaceRadius((PlaceKind)(kind - SearchEntry.PlaceKindOffset));
                    if (!(radius > 0)) continue;
                }
                else if (!Landmarkish(p.Kind))
                {
                    continue;
                }
                double px, pz;
                tile.LocalToGame(p.XCm, p.ZCm, out px, out pz);
                double ddx = px - x, ddz = pz - z;
                double d = Math.Sqrt(ddx * ddx + ddz * ddz);
                if (isPlace ? d / radius > placeScore : d > poiDistance) continue;
                NameRecord name = tile.Name(p.NameRef);
                if (name == null || name.IsEmpty) continue;
                if (isPlace)
                {
                    placeScore = d / radius;
                    place = name;
                }
                else
                {
                    poiDistance = d;
                    poi = name;
                }
            }
        }

        /// <summary>How far a place's name reaches (0: not used as a "you are here" name).</summary>
        public static double PlaceRadius(PlaceKind kind)
        {
            switch (kind)
            {
                case PlaceKind.City: return 6000.0;
                case PlaceKind.Town: return 3000.0;
                case PlaceKind.Village: return 1500.0;
                case PlaceKind.Suburb: return 1500.0;
                case PlaceKind.Island: return 1000.0;
                case PlaceKind.Hamlet: return 700.0;
                case PlaceKind.Neighbourhood: return 700.0;
                case PlaceKind.Quarter: return 700.0;
                case PlaceKind.Locality: return 500.0;
                case PlaceKind.Farm: return 300.0;
                case PlaceKind.IsolatedDwelling: return 200.0;
                case PlaceKind.Square: return 150.0;
                default: return 0.0; // admin areas: too big to say "you are here"
            }
        }

        /// <summary>POIs worth naming the spot after: public places, never businesses or lodging (ADR-010).</summary>
        public static bool Landmarkish(PoiKind kind)
        {
            int k = (int)kind;
            if (k >= 100 && k <= 126) return true;   // religious and heritage
            if (k >= 200 && k <= 213) return true;   // nature
            switch (kind)
            {
                case PoiKind.Airport:
                case PoiKind.CableCarStation:
                case PoiKind.RailwayStation:
                case PoiKind.Bridge:
                case PoiKind.Attraction:
                case PoiKind.Marketplace:
                case PoiKind.PicnicSite:
                case PoiKind.ThemePark:
                case PoiKind.Zoo:
                case PoiKind.School:
                case PoiKind.Hospital:
                    return true;
                default:
                    return k >= 500 && k < 600;      // adventure
            }
        }

        private static bool Same(NameRecord a, NameRecord b)
        {
            if (a == null || a.IsEmpty) return b == null || b.IsEmpty;
            return a.Equals(b);
        }
    }
}
