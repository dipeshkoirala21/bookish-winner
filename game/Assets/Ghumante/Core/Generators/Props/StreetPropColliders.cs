using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;

namespace Ghumante.Core.Generators.Props
{
    /// <summary>
    /// The colliders of the placed street props (package props; owner playtest #7: "traffic poles, road signs and
    /// electric poles in the middle of the roads"), built from the very list the instanced kit draws
    /// (<see cref="PropPlacement"/>), so a prop is solid exactly where it is drawn and nowhere else: the mapped OSM
    /// point a prop was moved away from (a signal on the junction node, a lamp on the centreline) stays free. Tile-local
    /// metres like the props. Shapes follow the kit (<see cref="StreetPropMesher"/>) scaled by HeightM / DefaultHeight:
    /// the pole of every lamp, signal and utility pole (a signal's mast arm and head hang above the 4.5 m overhead
    /// clearance and are not solid), both poles of a transformer H-frame, the posts of a falcha shelter and its back
    /// bench, the pillars of a gate (traffic passes between them), the stand of a tank, a well's parapet, a statue's
    /// pedestal, the legs of a pylon, masts and chimneys. Flat aprons (helipad, stand marking) are not solid. Thin
    /// poles and posts never stop the chase camera. Deterministic; allocation-free apart from the collider lists.
    /// </summary>
    public static class StreetPropColliders
    {
        /// <summary>Appends the colliders of every prop in <paramref name="props"/>; returns how many were added.</summary>
        public static int AppendAll(List<StreetProp> props, StructureColliders into)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            if (into == null) throw new ArgumentNullException(nameof(into));
            int n = 0;
            for (int i = 0; i < props.Count; i++) n += Append(props[i], into);
            return n;
        }

        /// <summary>Appends the colliders of one prop; returns how many were added.</summary>
        public static int Append(in StreetProp p, StructureColliders into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            float s = p.HeightM > 0f ? p.HeightM / StreetPropMesher.DefaultHeight(p.Kind) : 1f;
            double yaw = p.YawDeg * (Math.PI / 180.0);
            // Prop frame: +Z forward (sin, cos), +X right (cos, -sin) in (X, Z).
            var f = new Frame { P = p, Fx = Math.Sin(yaw), Fz = Math.Cos(yaw), Rx = Math.Cos(yaw), Rz = -Math.Sin(yaw) };
            int before = into.Count;
            switch (p.Kind)
            {
                case StreetPropKind.StreetLamp:
                case StreetPropKind.StreetLampSolar:
                case StreetPropKind.StreetLampHeritage:
                    Pole(into, f, 0, 0, 0.13f * s, p.HeightM);
                    break;
                case StreetPropKind.TrafficSignal:
                case StreetPropKind.TrafficSignalLong:
                case StreetPropKind.TrafficSignalMirrored:
                case StreetPropKind.TrafficSignalLongMirrored:
                    // The pole only: the arm and the head hang above the overhead clearance (StreetPropMesher.SignalHead).
                    Pole(into, f, 0, 0, 0.14f * s, p.HeightM);
                    break;
                case StreetPropKind.BusStop:
                case StreetPropKind.TaxiStand:
                case StreetPropKind.AerowayGate:
                case StreetPropKind.Windsock:
                    Pole(into, f, 0, 0, 0.08f * s, p.HeightM);
                    break;
                case StreetPropKind.PowerPole:
                    Pole(into, f, 0, 0, 0.16f * s, p.HeightM);
                    break;
                case StreetPropKind.TransformerPole:
                    // The H-frame's two poles 2.4 m apart along the prop's X (along the road); the platform is overhead.
                    Pole(into, f, -1.2 * s, 0, 0.16f * s, p.HeightM);
                    Pole(into, f, 1.2 * s, 0, 0.16f * s, p.HeightM);
                    break;
                case StreetPropKind.Mast:
                    Solid(into, f, 0, 0, 0.55f * s, p.HeightM);
                    break;
                case StreetPropKind.Chimney:
                    Solid(into, f, 0, 0, 1.4f * s, p.HeightM);
                    break;
                case StreetPropKind.StorageTank:
                    Solid(into, f, 0, 0, 0.62f * s, p.HeightM);
                    break;
                case StreetPropKind.Well:
                    Solid(into, f, 0, 0, 0.85f * s, 0.8f * s);
                    break;
                case StreetPropKind.WaterTap:
                    Pole(into, f, 0, 0, 0.18f * s, 1.0f * s);
                    break;
                case StreetPropKind.Artwork:
                    Solid(into, f, 0, 0, 0.6f * s, p.HeightM);
                    break;
                case StreetPropKind.PowerTower:
                    // Four legs at the corners of the 6.8 m base; walking under the tower is free.
                    for (int c = 0; c < 4; c++) Solid(into, f, (c % 2 == 0 ? -3.3 : 3.3) * s, (c < 2 ? -3.3 : 3.3) * s, 0.3f * s, 8f * s);
                    break;
                case StreetPropKind.Gate:
                    // Two pillars 0.55 m square at ±2.925 m across the road: the opening between them stays free.
                    for (int side = -1; side <= 1; side += 2) Box(into, f, side * 2.925 * s, 0, 0.275 * s, 0.275 * s, 2.7f * s);
                    break;
                case StreetPropKind.BusShelter:
                    // The falcha's ten posts (two rows of five on a 6.0 × 3.6 m plinth) and the bench along the back row.
                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 5; c++)
                            Pole(into, f, (-2.7 + 1.35 * c) * s, (r == 0 ? -1.5 : 1.5) * s, 0.13f * s, 2.6f * s);
                    Box(into, f, 0, -1.25 * s, 2.6 * s, 0.25 * s, 0.75f * s);
                    break;
                case StreetPropKind.Bench:
                    Box(into, f, 0, 0, 0.8 * s, 0.25 * s, 0.85f * s);
                    break;
                case StreetPropKind.SolarPanel:
                    Box(into, f, 0, 0, 1.05 * s, 0.65 * s, 1.5f * s);
                    break;
                default:
                    // Helipad, stand marking: flat, walkable, not solid.
                    break;
            }
            return into.Count - before;
        }

        /// <summary>A prop and its frame: +Z forward (<see cref="Fx"/>, <see cref="Fz"/>), +X right (<see cref="Rx"/>,
        /// <see cref="Rz"/>) in (X, Z).</summary>
        private struct Frame
        {
            public StreetProp P;
            public double Fx, Fz, Rx, Rz;

            public double X(double right, double forward)
            {
                return P.X + Rx * right + Fx * forward;
            }

            public double Z(double right, double forward)
            {
                return P.Z + Rz * right + Fz * forward;
            }
        }

        /// <summary>A thin vertical post at (right, forward) in the prop frame; the chase camera passes it.</summary>
        private static void Pole(StructureColliders into, in Frame f, double right, double forward, float r, float height)
        {
            into.Cylinders.Add(new ColliderCylinder
            {
                CX = f.X(right, forward), CZ = f.Z(right, forward), Radius = r, Bottom = f.P.Y - 0.3f, Top = f.P.Y + Math.Max(1f, height), CameraPasses = true,
            });
        }

        /// <summary>A solid vertical cylinder (statue, tank, chimney) that stops the camera unless it is thin.</summary>
        private static void Solid(StructureColliders into, in Frame f, double right, double forward, float r, float height)
        {
            into.Cylinders.Add(new ColliderCylinder
            {
                CX = f.X(right, forward), CZ = f.Z(right, forward), Radius = r, Bottom = f.P.Y - 0.3f, Top = f.P.Y + Math.Max(0.4f, height),
            });
        }

        /// <summary>An upright no-climb box centred at (right, forward) in the prop frame, half extents along the prop's
        /// X and Z, from just under the base to <paramref name="height"/>.</summary>
        private static void Box(StructureColliders into, in Frame f, double right, double forward, double halfX, double halfZ, float height)
        {
            float half = 0.5f * (height + 0.15f);
            into.AddBox(new OrientedBox
            {
                CX = f.X(right, forward), CZ = f.Z(right, forward), CY = f.P.Y - 0.15f + half, HalfX = (float)halfX, HalfY = half, HalfZ = (float)halfZ,
                // OrientedBox: local X along (cos a, sin a), counter-clockwise from +X; the prop's +X is (Rx, Rz).
                YawRad = (float)Math.Atan2(f.Rz, f.Rx), Flags = ColliderFlags.NoClimb, Material = FootSurface.Stone,
            });
        }
    }
}
