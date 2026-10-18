using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The drawn ground of a mesh (cut/fill package, owner playtest: "the dog looks like it is sleeping in the air; half of
    /// the motorcycle is in the air"): the up-facing triangles of meshes as drawn (tile-local x/z, absolute y) in a 4 m
    /// grid, so the probes measure how far an object stands off what the player sees, independent of the samplers.
    /// </summary>
    internal sealed class DrawnGround
    {
        private const double CellM = 4.0;
        private readonly List<float> _p = new List<float>();
        private readonly List<int> _tri = new List<int>();
        private readonly List<int> _layer = new List<int>();
        private int _layers;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

        /// <summary>Adds the up-facing triangles (geometric normal y at least <paramref name="minUp"/>) of a mesh.</summary>
        public void Add(MeshData m, float minUp = 0.2f)
        {
            int layer = _layers++;
            int baseV = _p.Count / 3;
            for (int v = 0; v < m.VertexCount; v++)
            {
                _p.Add(m.Positions[3 * v]);
                _p.Add(m.Positions[3 * v + 1]);
                _p.Add(m.Positions[3 * v + 2]);
            }
            for (int t = 0; t + 2 < m.IndexCount; t += 3)
            {
                int a = m.Indices[t] + baseV, b = m.Indices[t + 1] + baseV, c = m.Indices[t + 2] + baseV;
                double ax = _p[3 * a], ay = _p[3 * a + 1], az = _p[3 * a + 2];
                double ux = _p[3 * b] - ax, uy = _p[3 * b + 1] - ay, uz = _p[3 * b + 2] - az;
                double vx = _p[3 * c] - ax, vy = _p[3 * c + 1] - ay, vz = _p[3 * c + 2] - az;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-9 || Math.Abs(ny) / len < minUp) continue;
                int id = _tri.Count / 3;
                _tri.Add(a);
                _tri.Add(b);
                _tri.Add(c);
                _layer.Add(layer);
                double x0 = Math.Min(ax, Math.Min(_p[3 * b], _p[3 * c])), x1 = Math.Max(ax, Math.Max(_p[3 * b], _p[3 * c]));
                double z0 = Math.Min(az, Math.Min(_p[3 * b + 2], _p[3 * c + 2])), z1 = Math.Max(az, Math.Max(_p[3 * b + 2], _p[3 * c + 2]));
                for (long j = (long)Math.Floor(z0 / CellM); j <= (long)Math.Floor(z1 / CellM); j++)
                for (long i = (long)Math.Floor(x0 / CellM); i <= (long)Math.Floor(x1 / CellM); i++)
                {
                    long key = j * 1000003L + i;
                    List<int> l;
                    if (!_cells.TryGetValue(key, out l)) _cells[key] = l = new List<int>(4);
                    l.Add(id);
                }
            }
        }

        /// <summary>
        /// The drawn surface under tile-local (x, z) for a body at <paramref name="nearY"/>: the highest triangle at most
        /// <paramref name="stepUp"/> above it, else the lowest. False where nothing is drawn.
        /// </summary>
        public bool TryHeight(double x, double z, float nearY, out float y, float stepUp = 0.6f)
        {
            y = 0f;
            List<int> l;
            if (!_cells.TryGetValue((long)Math.Floor(z / CellM) * 1000003L + (long)Math.Floor(x / CellM), out l)) return false;
            bool found = false, foundOk = false;
            float best = 0f;
            foreach (int id in l)
            {
                double h;
                if (!Height(id, x, z, out h)) continue;
                float hy = (float)h;
                bool ok = hy <= nearY + stepUp;
                if (!found || ok && !foundOk || ok == foundOk && (ok ? hy > best : hy < best))
                {
                    best = hy;
                    found = true;
                    foundOk = ok;
                }
            }
            y = best;
            return found;
        }

        /// <summary>Diagnostics: every drawn surface under tile-local (x, z) as "layer:height" pairs.</summary>
        public string All(double x, double z)
        {
            List<int> l;
            if (!_cells.TryGetValue((long)Math.Floor(z / CellM) * 1000003L + (long)Math.Floor(x / CellM), out l)) return "-";
            var sb = new System.Text.StringBuilder();
            foreach (int id in l)
            {
                double h;
                if (!Height(id, x, z, out h)) continue;
                sb.Append(' ').Append(_layer[id]).Append(':').Append(h.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private bool Height(int id, double px, double pz, out double h)
        {
            h = 0;
            int a = _tri[3 * id], b = _tri[3 * id + 1], c = _tri[3 * id + 2];
            double ax = _p[3 * a], az = _p[3 * a + 2], bx = _p[3 * b], bz = _p[3 * b + 2], cx = _p[3 * c], cz = _p[3 * c + 2];
            double d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
            if (Math.Abs(d) < 1e-12) return false;
            double w0 = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / d;
            double w1 = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / d;
            double w2 = 1 - w0 - w1;
            const double e = -1e-5;
            if (w0 < e || w1 < e || w2 < e) return false;
            h = w0 * _p[3 * a + 1] + w1 * _p[3 * b + 1] + w2 * _p[3 * c + 1];
            return true;
        }
    }

    /// <summary>Hill places of the kathmandu_core sample the cut/fill probes look at (lon, lat, radius in metres).</summary>
    internal static class HillPlaces
    {
        public struct Place
        {
            public string Name;
            public double Lon, Lat, RadiusM;

            public void Game(out double x, out double z)
            {
                WorldFrame.LonLatToGame(Lon, Lat, out x, out z);
            }

            public TileId Tile
            {
                get
                {
                    double x, z;
                    Game(out x, out z);
                    return TileId.At(10, x, z);
                }
            }
        }

        /// <summary>The owner's screenshot (Gaurighat Hanuman Mandir on the Bagmati by Pashupati) and other hill roads.</summary>
        public static readonly Place[] All =
        {
            new Place { Name = "gaurighat", Lon = 85.3496, Lat = 27.7127, RadiusM = 260 },
            new Place { Name = "pashupati_bank", Lon = 85.3486, Lat = 27.7100, RadiusM = 200 },
            new Place { Name = "chabahil_gaurighat", Lon = 85.3470, Lat = 27.7155, RadiusM = 260 },
            new Place { Name = "swayambhu", Lon = 85.2904, Lat = 27.7149, RadiusM = 420 },
            new Place { Name = "balaju_nagarjun", Lon = 85.2985, Lat = 27.7335, RadiusM = 350 },
        };

        /// <summary>The runtime terrain step of a level-10 detail tile on the High tier (1 024 m over 64 quads).</summary>
        public const int RuntimeStep = 2;
    }
}
