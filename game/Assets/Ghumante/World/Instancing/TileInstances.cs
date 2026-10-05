using System;
using System.Collections.Generic;
using Ghumante.Core.Generators.Placement;

namespace Ghumante.World.Instancing
{
    /// <summary>One parked vehicle (W2_DESIGN 5.2 "Parked vehicles"): tile-local metres with absolute Y, yaw clockwise
    /// from north, a <see cref="Core.Driving.VehicleCatalog"/> variant and livery. About 30% carry the green
    /// community-fleet key tag (W2-O5) that gameplay lets the player borrow.</summary>
    public struct ParkedVehicle
    {
        public float X, Y, Z, YawDeg;
        public ushort Variant;
        public byte Livery;
        public bool CommunityFleet;

        /// <summary>Stable id (the road and slot), for plates, seeds and the fleet's return-home logic.</summary>
        public uint Id;
    }

    /// <summary>
    /// The instanced dressing of one detail tile, built on the worker with the tile's meshes: trees (Core
    /// TreePlacement), street props (Core PropPlacement) and parked vehicles (<see cref="ParkedPlacement"/>). Each list
    /// is sorted by its 64 m cell (row-major from the south-west), and <see cref="TreeCells"/>, <see cref="PropCells"/>
    /// and <see cref="ParkedCells"/> hold the start of every cell plus one end entry, so renderers visit only the cells
    /// near the camera. Owned by the tile's view once uploaded; read-only on the main thread. Engine-free.
    /// </summary>
    public sealed class TileInstances
    {
        /// <summary>Side of an instance cell.</summary>
        public const double CellM = 64.0;

        public readonly List<TreeInstance> Trees = new List<TreeInstance>();
        public readonly List<StreetProp> Props = new List<StreetProp>();
        public readonly List<ParkedVehicle> Parked = new List<ParkedVehicle>();

        /// <summary>Cells per side.</summary>
        public int CellsPerSide;

        public int[] TreeCells = new int[0], PropCells = new int[0], ParkedCells = new int[0];

        public bool IsEmpty
        {
            get { return Trees.Count == 0 && Props.Count == 0 && Parked.Count == 0; }
        }

        public void Clear()
        {
            Trees.Clear();
            Props.Clear();
            Parked.Clear();
            CellsPerSide = 0;
        }

        /// <summary>Sort every list by cell and fill the cell starts (call once after placement).</summary>
        public void Index(double tileSizeM)
        {
            int n = Math.Max(1, (int)Math.Ceiling(tileSizeM / CellM - 1e-9));
            CellsPerSide = n;
            TreeCells = Sort(Trees, n, t => t.X, t => t.Z);
            PropCells = Sort(Props, n, p => p.X, p => p.Z);
            ParkedCells = Sort(Parked, n, p => p.X, p => p.Z);
        }

        /// <summary>Cell index of a tile-local point.</summary>
        public int CellOf(double x, double z)
        {
            int n = CellsPerSide;
            int cx = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(x / CellM)));
            int cz = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(z / CellM)));
            return cz * n + cx;
        }

        private int[] Sort<T>(List<T> list, int n, Func<T, float> fx, Func<T, float> fz)
        {
            var starts = new int[n * n + 1];
            if (list.Count == 0) return starts;
            var cell = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                cell[i] = CellOf(fx(list[i]), fz(list[i]));
                starts[cell[i] + 1]++;
            }
            for (int c = 0; c < n * n; c++) starts[c + 1] += starts[c];
            var sorted = new T[list.Count];
            var next = (int[])starts.Clone();
            for (int i = 0; i < list.Count; i++) sorted[next[cell[i]]++] = list[i];
            list.Clear();
            list.AddRange(sorted);
            return starts;
        }
    }
}
