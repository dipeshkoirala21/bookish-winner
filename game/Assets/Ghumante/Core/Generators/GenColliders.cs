using System;
using System.Collections.Generic;

namespace Ghumante.Core.Generators
{
    /// <summary>Behaviour of a generated collider (mirrors the contract's ColliderFlags of StructureColliders).</summary>
    [Flags]
    public enum GenColliderFlags : byte
    {
        None = 0,

        /// <summary>The top is walkable ground (plinths, pikha aprons, terraces, stairs).</summary>
        Walkable = 1,

        /// <summary>Not climbable: blocks (roofs, domes, spires, temple walls).</summary>
        NoClimb = 2,

        /// <summary>Gentle contact margin (0.8 m around temple walls, W2_DESIGN 3.3).</summary>
        SoftMargin = 4,
    }

    /// <summary>An oriented box in tile-local metres (centre X/Z, centre Y absolute), yaw in radians counter-clockwise
    /// seen from above from +X. <see cref="Material"/> is a FootSurface code (W2_DESIGN 10.3: Stone 3, Brick 2, Wood 8).</summary>
    public struct GenBox
    {
        public double CX, CZ;
        public float CY, HalfX, HalfY, HalfZ, YawRad;
        public GenColliderFlags Flags;
        public byte Material;
    }

    /// <summary>A walkable ramp (a real stair drawn with visual steps) from (X0, Z0, Y0) to (X1, Z1, Y1), tile-local
    /// metres with absolute Y.</summary>
    public struct GenRamp
    {
        public double X0, Z0, X1, Z1;
        public float Y0, Y1, HalfWidth;
        public byte Material;
    }

    /// <summary>A solid vertical cylinder (statue, column, bollard, pier, trunk) in tile-local metres with absolute Y0..Y1.
    /// Never walkable.</summary>
    public struct GenCylinder
    {
        public double CX, CZ;
        public float Radius, Y0, Y1;
    }

    /// <summary>A solid thin wall (railing, parapet, fence, compound wall) from (X0, Z0) to (X1, Z1) in tile-local metres,
    /// <see cref="HalfThickness"/> to each side of its axis, with absolute Y0..Y1. Never walkable.</summary>
    public struct GenWall
    {
        public double X0, Z0, X1, Z1;
        public float HalfThickness, Y0, Y1;
    }

    /// <summary>
    /// Colliders produced together with generated meshes (sacred structures, house plinths). This is Track A's output
    /// list with exactly the fields of the W2_DESIGN 10.3 <c>StructureColliders</c> contract (owned by Track B in
    /// Core/Driving); B registers them with its ground query by copying each box and ramp. Reusable: <see cref="Clear"/>
    /// keeps the lists.
    /// </summary>
    public sealed class GenColliders
    {
        public readonly List<GenBox> Boxes = new List<GenBox>();
        public readonly List<GenRamp> Ramps = new List<GenRamp>();

        /// <summary>Solid cylinders and walls (detail pass): statues, columns, piers, railings, parapets.</summary>
        public readonly List<GenCylinder> Cylinders = new List<GenCylinder>();

        public readonly List<GenWall> Walls = new List<GenWall>();

        // FootSurface codes (W2_DESIGN 10.3 enum order).
        public const byte Asphalt = 0, Concrete = 1, Brick = 2, Stone = 3, Gravel = 4, Dirt = 5, Mud = 6, Grass = 7, Wood = 8, Metal = 9, Water = 10;

        public void Clear()
        {
            Boxes.Clear();
            Ramps.Clear();
            Cylinders.Clear();
            Walls.Clear();
        }

        /// <summary>A solid vertical cylinder (tile-local centre, absolute heights).</summary>
        public void AddCylinder(double cx, double cz, double y0, double y1, double radius)
        {
            if (!(radius > 0) || !(y1 > y0)) return;
            Cylinders.Add(new GenCylinder { CX = cx, CZ = cz, Radius = (float)radius, Y0 = (float)y0, Y1 = (float)y1 });
        }

        /// <summary>A solid thin wall from (x0, z0) to (x1, z1) (tile-local), absolute heights.</summary>
        public void AddWall(double x0, double z0, double x1, double z1, double y0, double y1, double halfThickness)
        {
            if (!(halfThickness >= 0) || !(y1 > y0)) return;
            Walls.Add(new GenWall { X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, HalfThickness = (float)halfThickness, Y0 = (float)y0, Y1 = (float)y1 });
        }

        public void AddBox(in GenBox b)
        {
            Boxes.Add(b);
        }

        public void AddRamp(in GenRamp r)
        {
            Ramps.Add(r);
        }

        /// <summary>An oriented box from a frame-aligned extent: centre (cx, cz) tile-local, U axis (ux, uz).</summary>
        public void AddBox(double cx, double cz, double y0, double y1, double halfU, double halfW, double ux, double uz,
                           GenColliderFlags flags, byte material)
        {
            Boxes.Add(new GenBox
            {
                CX = cx, CZ = cz, CY = (float)(0.5 * (y0 + y1)), HalfX = (float)halfU, HalfY = (float)(0.5 * (y1 - y0)),
                HalfZ = (float)halfW, YawRad = (float)Math.Atan2(uz, ux), Flags = flags, Material = material,
            });
        }
    }
}
