using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Junction caps, roundabout and chowk islands, police podiums and junction markings (W2_DESIGN 4.7) from the
    /// tile's <see cref="RoadLayout"/>. Surfaces: each cap is fanned from its node and draped on the terrain just above
    /// its highest arm's ribbon (whose surface colour it takes); islands are raised discs with a mountable kerb, a
    /// 1 m apron on roundabouts and grass inside; police chowks get a white podium with an umbrella. Decals: stop lines
    /// and zebras on the arms of signalised and police chowks, a painted ring for mini roundabouts.
    /// <para><see cref="RoadMesher"/> calls this with <c>decals = null</c> when <see cref="RoadOptions.JunctionCaps"/> is on,
    /// and <see cref="MarkingMesher"/> with <c>surface = null</c>, so callers normally use those two. Returns the number
    /// of caps drawn. Thread-safe for distinct meshes.</para>
    /// </summary>
    public static class JunctionMesher
    {
        /// <summary>Lift of a cap over its highest arm's ribbon.</summary>
        public const float CapExtraLiftM = 0.003f;

        public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData surface, MeshData decals)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (o == null) o = new RoadOptions();
            if (t.Roads.Count == 0) return 0;
            RoadLayout layout = RoadLayout.For(t);
            var g = new RoadSurface(t, h);
            int drawn = 0;
            foreach (JunctionCap cap in layout.Caps)
            {
                RoadRecord top = t.Roads[cap.TopRoad];
                float lift = RoadMesher.LiftOf(top, o) + CapExtraLiftM;
                if (surface != null)
                {
                    uint c = RoadStyle.SurfaceRgba(top.Surface);
                    for (int k = 0; k < cap.Count; k++)
                    {
                        int q = k + 1 == cap.Count ? 0 : k + 1;
                        g.Tri(cap.X, cap.Z, cap.PolyX[k], cap.PolyZ[k], cap.PolyX[q], cap.PolyZ[q], lift, c, surface);
                    }
                }
                if (decals != null && Controlled(cap)) ChowkMarkings(t, layout, cap, ref g, o, decals);
                drawn++;
            }
            foreach (RoadIsland isl in layout.Islands)
            {
                float lift = isl.TopRoad >= 0 ? RoadMesher.LiftOf(t.Roads[isl.TopRoad], o) + CapExtraLiftM : o.LiftM;
                if (isl.Kind == IslandKind.Mini)
                {
                    if (decals != null)
                    {
                        g.Disc(isl.X, isl.Z, Math.Max(0, isl.RadiusM - 0.3), isl.RadiusM, 16, lift + MarkingMesher.DecalLiftM, MarkingMesher.White, decals);
                        g.Disc(isl.X, isl.Z, 0, 0.6, 8, lift + MarkingMesher.DecalLiftM, MarkingMesher.White, decals);
                    }
                    continue;
                }
                if (surface == null) continue;
                float top = lift + o.MedianHeightM;
                int sides = isl.RadiusM > 10 ? 32 : 20;
                g.Wall(isl.X, isl.Z, isl.RadiusM, sides, lift - 0.1f, top, RoadStyle.Kerb, surface);
                double apron = Math.Min(isl.ApronM, 0.5 * isl.RadiusM);
                if (apron > 0)
                {
                    g.Disc(isl.X, isl.Z, isl.RadiusM - apron, isl.RadiusM, sides, top, RoadStyle.SurfaceRgba(Surface.Cobble), surface);
                    g.Wall(isl.X, isl.Z, isl.RadiusM - apron, sides, top - 0.05f, top + 0.15f, RoadStyle.Kerb, surface);
                    g.Disc(isl.X, isl.Z, 0, isl.RadiusM - apron, sides, top + 0.15f, RoadStyle.IslandGrass, surface);
                }
                else
                {
                    g.Disc(isl.X, isl.Z, 0, isl.RadiusM, sides, top, RoadStyle.IslandGrass, surface);
                }
                if (isl.PolicePodium) Podium(ref g, isl.X, isl.Z, top + (apron > 0 ? 0.15f : 0f), surface);
            }
            if (surface != null)
            {
                // Police chowks without an island: the podium stands at the junction centre.
                foreach (JunctionCap cap in layout.Caps)
                    if ((cap.Kind == JunctionKind.Police || (cap.Flags & (byte)JunctionFlags.HasPolice) != 0) && !HasIsland(layout, cap))
                        Podium(ref g, cap.X, cap.Z, RoadMesher.LiftOf(t.Roads[cap.TopRoad], o) + CapExtraLiftM, surface);
            }
            return drawn;
        }

        private static bool HasIsland(RoadLayout layout, JunctionCap cap)
        {
            foreach (RoadIsland i in layout.Islands)
                if ((i.X - cap.X) * (i.X - cap.X) + (i.Z - cap.Z) * (i.Z - cap.Z) < (i.RadiusM + 10) * (i.RadiusM + 10)) return true;
            return false;
        }

        private static bool Controlled(JunctionCap cap)
        {
            return cap.Kind == JunctionKind.Signals || cap.Kind == JunctionKind.Police ||
                   (cap.Flags & (byte)(JunctionFlags.HasPolice | JunctionFlags.HasSignals | JunctionFlags.CrossingsMarked)) != 0;
        }

        /// <summary>Police podium (W2_DESIGN 4.7): Ø 1.4 m, 0.35 m high, white, with a 2.2 m red umbrella.</summary>
        private static void Podium(ref RoadSurface g, double x, double z, float baseLift, MeshData m)
        {
            float y = g.Height(x, z) + baseLift;
            MeshKit.Cylinder(m, x, z, 0.7, y - 0.1, y + 0.35, 10, true, RoadStyle.Podium);
            MeshKit.Cylinder(m, x, z, 0.04, y + 0.35, y + 2.2, 4, false, MeshColor.FromHex(0x5A5A5A));
            MeshKit.Frustum(m, x, z, 1.1, y + 2.0, 0.0, y + 2.45, 8, false, MeshColor.FromHex(0xC9433A));
            MeshKit.Frustum(m, x, z, 1.1, y + 2.0, 0.9, y + 1.98, 8, false, MeshColor.FromHex(0xC9433A));
        }

        /// <summary>Zebras and stop lines on every arm at least 6 m wide of a signalised or police chowk.</summary>
        private static void ChowkMarkings(TileData t, RoadLayout layout, JunctionCap cap, ref RoadSurface g, RoadOptions o, MeshData decals)
        {
            for (int k = 0; k < cap.ArmRoads.Length; k++)
            {
                int ri = cap.ArmRoads[k];
                RoadRecord r = t.Roads[ri];
                // The arm's node along value: the cut sits at node ± setback; find the node from the cut list.
                RoadCut[] cuts = layout.Cuts[ri];
                if (cuts == null) continue;
                double sEdge = double.NaN;
                for (int c = 0; c + 1 < cuts.Length; c += 2)
                {
                    double mid = 0.5 * (cuts[c].S + cuts[c + 1].S);
                    if (!PointNear(layout, t, ri, mid, cap.X, cap.Z, cap.ArmSetback[k] + 2)) continue;
                    sEdge = cap.ArmForward[k] ? cuts[c + 1].S : cuts[c].S;
                    break;
                }
                if (double.IsNaN(sEdge)) continue;
                RoadProfile p = layout.ProfileAt(ri, sEdge);
                if (p.CarriagewayM < 6f) continue;
                double dir = cap.ArmForward[k] ? 1 : -1;
                double depth = Math.Max(2.0, Math.Min(4.0, Math.Max(p.FootpathLeftM, p.FootpathRightM)));
                double z0 = sEdge + dir * 0.5, z1 = sEdge + dir * (0.5 + depth);
                if (z1 < 0 || z1 > layout.Profiles[ri].LengthM) continue;
                float lift = RoadMesher.LiftOf(r, o) + MarkingMesher.DecalLiftM;
                MarkingMesher.Zebra(t, layout, ri, Math.Min(z0, z1), Math.Max(z0, z1), lift, ref g, decals);
                // Stop line for traffic arriving toward the junction: on an outward arm that is the half on the arm's
                // right (left-hand traffic keeps left when driving in, which is the arm's right seen outward).
                double sStop = sEdge + dir * (0.5 + depth + 2.5);
                if (sStop < 0 || sStop > layout.Profiles[ri].LengthM) continue;
                RoadProfile ps = layout.ProfileAt(ri, sStop);
                bool oneway = (r.Flags & RoadFlags.Oneway) != 0;
                double half = 0.5 * ps.CarriagewayM, sh = ps.CentreShiftM;
                double a, b;
                if (oneway)
                {
                    a = sh - half;
                    b = sh + half;
                }
                else if (cap.ArmForward[k])
                {
                    a = sh - half; // inbound traffic on a forward arm drives against the point order: piece's right half
                    b = sh;
                }
                else
                {
                    a = sh;
                    b = sh + half;
                }
                MarkingMesher.CrossLine(t, layout, ri, sStop, 0.2, a, b, lift, MarkingMesher.White, ref g, decals);
            }
        }

        private static bool PointNear(RoadLayout layout, TileData t, int ri, double s, double x, double z, double radius)
        {
            RoadCut c = layout.CutAt(t, ri, s);
            return (c.CX - x) * (c.CX - x) + (c.CZ - z) * (c.CZ - z) <= radius * radius;
        }
    }
}
