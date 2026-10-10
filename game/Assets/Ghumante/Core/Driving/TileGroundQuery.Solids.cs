using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Bridges;

namespace Ghumante.Core.Driving
{
    /// <summary>Solids, swept bodies and camera casts of the ground query (docs/W2_DETAIL_CONTRACT.md §1, §3).</summary>
    public sealed partial class TileGroundQuery
    {
        /// <summary>Distance kept between a swept body and the solid it stops at (numerical skin).</summary>
        public const float SkinM = 0.02f;

        /// <summary>Camera casts march the terrain and decks in steps of at most this many metres.</summary>
        public const float ViewStepM = 0.5f;

        // Solid sets of exact areas (key = area key) and of registered structure colliders (key | StructureKeyBit), kept
        // sorted by key so every query visits them in the same order.
        private const ulong StructureKeyBit = 1UL << 63;
        private readonly List<ulong> _solidKeys = new List<ulong>();
        private readonly List<SolidSet> _solidSets = new List<SolidSet>();
        private readonly double[] _cx = new double[CollisionBody.MaxCircles], _cz = new double[CollisionBody.MaxCircles];
        private readonly float[] _cy = new float[CollisionBody.MaxCircles];

        /// <summary>Longest step of the march that finds the ground under each circle of a body (<see cref="CarryAlongBody"/>).</summary>
        public const float CarryStepM = 2f;

        private void AddSolids(ulong key, SolidSet set)
        {
            int i = _solidKeys.BinarySearch(key);
            if (set == null || set.Count == 0)
            {
                if (i >= 0)
                {
                    _solidKeys.RemoveAt(i);
                    _solidSets.RemoveAt(i);
                }
                return;
            }
            if (i >= 0)
            {
                _solidSets[i] = set;
                return;
            }
            i = ~i;
            _solidKeys.Insert(i, key);
            _solidSets.Insert(i, set);
        }

        private void RemoveSolids(ulong key)
        {
            int i = _solidKeys.BinarySearch(key);
            if (i < 0) return;
            _solidKeys.RemoveAt(i);
            _solidSets.RemoveAt(i);
        }

        /// <summary>Number of solid sets (exact tiles with solids plus registered collider tiles).</summary>
        public int SolidSetCount
        {
            get { return _solidSets.Count; }
        }

        /// <summary>Number of solids over every set (diagnostics).</summary>
        public int SolidCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _solidSets.Count; i++) n += _solidSets[i].Count;
                return n;
            }
        }

        /// <summary>The solids of a loaded exact area (tests and debug views): count, and each as a capsule prism.</summary>
        public int SolidsOf(TileId area, List<SolidPrim> into)
        {
            Entry e;
            if (!_areas.TryGetValue(area, out e) || e.Solids == null) return 0;
            if (into != null)
                for (int i = 0; i < e.Solids.Count; i++)
                    into.Add(e.Solids[i]);
            return e.Solids.Count;
        }

        /// <summary>True when (x, z) lies inside a solid (a footprint outline or within a solid's radius) that spans a body
        /// with feet at <paramref name="feetY"/> (tests, spawn checks).</summary>
        public bool InsideSolid(double x, double z, float feetY)
        {
            for (int i = 0; i < _solidSets.Count; i++)
            {
                SolidSet set = _solidSets[i];
                if (set.Overlaps(x, z, x, z) && set.Contains(x, z, feetY, BodyHeightM)) return true;
            }
            return false;
        }

        /// <summary>True when a deck slab (bridges package decks or structure heights) overlaps the body between the
        /// step-up over the feet and the head at (x, z).</summary>
        private bool CeilingBlocks(double x, double z, float feetY, float bodyH)
        {
            float low = feetY + StepUpM, high = feetY + bodyH;
            NearRoadEntries(x, z, MaxRoadHalfWidthM);
            for (int i = 0; i < _near.Count; i++)
            {
                RoadSpatialIndex idx = _near[i].Roads;
                if (idx.HasDecks && idx.SlabOverlaps(x, z, low, high)) return true;
                if (DeckBlocks(_near[i].Decks, x, z, low, high)) return true;
            }
            return DeckBlocks(Decks, x, z, low, high);
        }

        private static bool DeckBlocks(IBridgeDeckQuery d, double x, double z, float low, float high)
        {
            if (d == null) return false;
            float y, nx, ny, nz, under;
            if (d.TryDeck(x, z, high, out y, out nx, out ny, out nz) && y > low && y - RoadSpatialIndex.DeckThicknessM < high) return true;
            return d.TryCeiling(x, z, low, out under) && under < high;
        }

        // ---------------------------------------------------------------------------------------------------------
        // ISolidQuery

        public bool SweepBody(in CollisionBody body, double x, double z, float headingRad, float feetY, double dx, double dz,
                              out float t, out float nx, out float nz)
        {
            t = 1f;
            nx = 0f;
            nz = 0f;
            if (double.IsNaN(dx) || double.IsNaN(dz) || dx * dx + dz * dz < 1e-14) return false;
            int n = body.Circles;
            float r = body.Radius;
            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int k = 0; k < n; k++)
            {
                body.Centre(k, x, z, headingRad, out _cx[k], out _cz[k]);
                minX = Math.Min(minX, Math.Min(_cx[k], _cx[k] + dx));
                maxX = Math.Max(maxX, Math.Max(_cx[k], _cx[k] + dx));
                minZ = Math.Min(minZ, Math.Min(_cz[k], _cz[k] + dz));
                maxZ = Math.Max(maxZ, Math.Max(_cz[k], _cz[k] + dz));
            }
            bool hit = false;
            for (int i = 0; i < _solidSets.Count; i++)
            {
                SolidSet set = _solidSets[i];
                if (!set.Overlaps(minX - r, minZ - r, maxX + r, maxZ + r)) continue;
                hit |= set.Sweep(_cx, _cz, n, r, dx, dz, feetY, StepUpM, body.HeightM, ref t, ref nx, ref nz);
            }
            // Deck slabs too low for the body (the side of a ramp, a low span): checked where the move ends, for each
            // circle from the ground it rides on (a long body on a ramp has its nose over higher deck than its feet).
            if (!float.IsInfinity(feetY))
            {
                bool carried = false;
                for (int k = 0; k < n; k++)
                {
                    double ex = _cx[k] + dx * t, ez = _cz[k] + dz * t;
                    if (!CeilingBlocks(ex, ez, feetY, body.HeightM)) continue;
                    if (!carried)
                    {
                        CarryAlongBody(in body, x, z, headingRad, feetY, dx * t, dz * t);
                        carried = true;
                    }
                    if (!CeilingBlocks(ex, ez, _cy[k], body.HeightM)) continue;
                    double len = Math.Sqrt(dx * dx + dz * dz);
                    t = 0f;
                    nx = (float)(-dx / len);
                    nz = (float)(-dz / len);
                    return true;
                }
            }
            return hit;
        }

        /// <summary>
        /// The ground each circle of a body rides on (into <see cref="_cy"/>) after the body moved by (mx, mz): marching
        /// from the origin (feet at <paramref name="feetY"/>) to the moved origin and then along the body's axis in steps of
        /// at most <see cref="CarryStepM"/>, the layered ground is followed while each step changes its height by at most
        /// <see cref="StructureDeckStepUpM"/> (a ramp or deck continuing under the body); a larger step (the side of a
        /// ramp, a drop) ends the march there, so beyond it the circle keeps the last height.
        /// </summary>
        private void CarryAlongBody(in CollisionBody body, double x, double z, float headingRad, float feetY, double mx, double mz)
        {
            double fx = Math.Sin(headingRad), fz = Math.Cos(headingRad);
            float y = Carry(x, z, feetY, x + mx, z + mz);
            double ox = x + mx, oz = z + mz;
            // Forward from the origin to the front circle, and back to the rear one; circles in order of their along.
            float yFwd = y, yBack = y;
            double aFwd = 0, aBack = 0;
            for (int k = 0; k < body.Circles; k++)
            {
                float along = body.Circles <= 1 ? body.FirstM : body.FirstM + (body.LastM - body.FirstM) * k / (body.Circles - 1);
                if (along >= 0)
                {
                    yFwd = Carry(ox + fx * aFwd, oz + fz * aFwd, yFwd, ox + fx * along, oz + fz * along);
                    aFwd = along;
                    _cy[k] = yFwd;
                }
                else
                {
                    _cy[k] = float.NaN;
                }
            }
            for (int k = body.Circles - 1; k >= 0; k--)
            {
                if (!float.IsNaN(_cy[k])) continue;
                float along = body.Circles <= 1 ? body.FirstM : body.FirstM + (body.LastM - body.FirstM) * k / (body.Circles - 1);
                yBack = Carry(ox + fx * aBack, oz + fz * aBack, yBack, ox + fx * along, oz + fz * along);
                aBack = along;
                _cy[k] = yBack;
            }
        }

        /// <summary>Follows the layered ground from (ax, az) at height <paramref name="y"/> to (bx, bz); see
        /// <see cref="CarryAlongBody"/>.</summary>
        private float Carry(double ax, double az, float y, double bx, double bz)
        {
            double dx = bx - ax, dz = bz - az;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return y;
            int steps = Math.Max(1, (int)Math.Ceiling(len / CarryStepM));
            for (int k = 1; k <= steps; k++)
            {
                double f = k / (double)steps;
                GroundSample s;
                if (!SampleCore(ax + dx * f, az + dz * f, y, out s) || Math.Abs(s.Height - y) > StructureDeckStepUpM) break;
                y = s.Height;
            }
            return y;
        }

        public bool Penetration(in CollisionBody body, double x, double z, float headingRad, float feetY, out double pushX, out double pushZ)
        {
            pushX = 0;
            pushZ = 0;
            int n = body.Circles;
            float r = body.Radius;
            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            for (int k = 0; k < n; k++)
            {
                body.Centre(k, x, z, headingRad, out _cx[k], out _cz[k]);
                minX = Math.Min(minX, _cx[k]);
                maxX = Math.Max(maxX, _cx[k]);
                minZ = Math.Min(minZ, _cz[k]);
                maxZ = Math.Max(maxZ, _cz[k]);
            }
            bool any = false;
            double depth = 0;
            for (int i = 0; i < _solidSets.Count; i++)
            {
                SolidSet set = _solidSets[i];
                if (!set.Overlaps(minX - r, minZ - r, maxX + r, maxZ + r)) continue;
                any |= set.Penetration(_cx, _cz, n, r, feetY, StepUpM, body.HeightM, ref pushX, ref pushZ, ref depth);
            }
            return any && depth > 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // IViewObstacleQuery

        /// <summary>
        /// Sweeps a sphere from (ox, oy, oz) along the unit direction (dx, dy, dz) up to <paramref name="maxDist"/>
        /// against every solid (footprints, structures, statues, piers, railings; not trunks, poles and other thin
        /// cylinders), the deck slabs of bridges and flyovers, and the rendered terrain. An outline the start lies inside
        /// is ignored, and so is the terrain until the sphere is above it, so a camera never locks onto what its target
        /// stands in; a wall the sphere already touches at the start (a pivot close to it) stops it at once when the cast
        /// heads into it (hit at 0) and is passed when it heads along it or away. Allocation free.
        /// </summary>
        public bool SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, double radius, double maxDist,
                               out double hitDist)
        {
            hitDist = maxDist;
            if (!(maxDist > 0) || double.IsNaN(ox + oy + oz + dx + dy + dz)) return false;
            double l = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (l < 1e-9) return false;
            dx /= l;
            dy /= l;
            dz /= l;
            float r = (float)Math.Max(0.0, radius);
            double best = maxDist;
            for (int i = 0; i < _solidSets.Count; i++)
            {
                SolidSet set = _solidSets[i];
                double ex = ox + dx * best, ez = oz + dz * best;
                if (!set.Overlaps(Math.Min(ox, ex) - r, Math.Min(oz, ez) - r, Math.Max(ox, ex) + r, Math.Max(oz, ez) + r)) continue;
                set.SphereCast(ox, oy, oz, dx, dy, dz, r, maxDist, ref best);
            }
            // March the terrain and the deck slabs up to the nearest solid hit, then refine by bisection.
            int steps = (int)Math.Ceiling(best / ViewStepM);
            if (steps > 0)
            {
                bool armed = !ViewBlocked(ox, oy, oz, r); // starting inside the ground: wait until clear of it
                double prev = 0;
                for (int k = 1; k <= steps; k++)
                {
                    double sd = Math.Min(best, k * (best / steps));
                    bool blocked = ViewBlocked(ox + dx * sd, oy + dy * sd, oz + dz * sd, r);
                    if (!armed)
                    {
                        if (!blocked) armed = true;
                        prev = sd;
                        continue;
                    }
                    if (blocked)
                    {
                        double lo = prev, hi = sd;
                        for (int it = 0; it < 6; it++)
                        {
                            double mid = 0.5 * (lo + hi);
                            if (ViewBlocked(ox + dx * mid, oy + dy * mid, oz + dz * mid, r)) hi = mid;
                            else lo = mid;
                        }
                        best = lo;
                        break;
                    }
                    prev = sd;
                }
            }
            hitDist = best;
            return best < maxDist;
        }

        /// <summary>A sphere at (x, y, z) of radius r is in the terrain or a deck slab.</summary>
        private bool ViewBlocked(double x, double y, double z, float r)
        {
            float h;
            if (TryTerrainHeight(x, z, out h) && y - r < h) return true;
            float low = (float)(y - r), high = (float)(y + r);
            NearRoadEntries(x, z, MaxRoadHalfWidthM);
            for (int i = 0; i < _near.Count; i++)
            {
                RoadSpatialIndex idx = _near[i].Roads;
                if (idx.HasDecks && idx.SlabOverlaps(x, z, low, high, r)) return true;
                if (DeckBlocks(_near[i].Decks, x, z, low, high)) return true;
            }
            return DeckBlocks(Decks, x, z, low, high);
        }
    }
}
