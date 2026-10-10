using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Material channel and baked ambient occlusion for kit geometry (docs/W2_DETAIL_CONTRACT.md §5), applied as a
    /// post-pass over the vertices a builder just emitted: <c>int v0 = m.VertexCount; MeshKit.Box(...);
    /// paint.Apply(m, v0);</c>. It writes <see cref="MeshData.Uv0"/> = (channel, ao) and leaves positions, normals and
    /// colours alone, so the <see cref="MeshKit"/> emitters stay exactly as they were for every other caller.
    /// <para>
    /// The AO of a vertex is <see cref="Ao"/> multiplied by up to four cheap terms, each 1 when unused: a ground ramp
    /// (dark at the ground contact, open <see cref="GroundFade"/> above it), a ceiling ramp (dark just under an eave
    /// or a slab at <see cref="TopY"/>), a contact plane (dark where an element meets the wall it is fixed to, or at
    /// the back of a deep opening) and a sky term (faces pointing down are darker by <see cref="Under"/>). Values stay
    /// in [0, 1]. A struct with no references: copy it freely, no allocation.
    /// </para>
    /// </summary>
    public struct KitPaint
    {
        public MaterialChannel Channel;

        /// <summary>Base AO (1 = open).</summary>
        public float Ao;

        /// <summary>Darkening of faces that point down: ao *= 1 - Under * max(0, -ny).</summary>
        public float Under;

        public bool Ground;
        public double GroundY;
        public float GroundAo, GroundFade;

        public bool Top;
        public double TopY;
        public float TopAo, TopFade;

        public bool Plane;
        public double PX, PY, PZ, NX, NY, NZ;
        public float PlaneAo, PlaneFade;

        /// <summary>A brush for a channel with a base AO and the default sky term (downward faces 30% darker).</summary>
        public static KitPaint Of(MaterialChannel channel, float ao = 1f)
        {
            return new KitPaint { Channel = channel, Ao = ao, Under = 0.3f };
        }

        public KitPaint WithChannel(MaterialChannel channel)
        {
            KitPaint p = this;
            p.Channel = channel;
            return p;
        }

        public KitPaint WithAo(float ao)
        {
            KitPaint p = this;
            p.Ao = ao;
            return p;
        }

        /// <summary>Ground contact: <paramref name="ao"/> at absolute height <paramref name="y"/> and below, open at
        /// <paramref name="y"/> + <paramref name="fade"/>.</summary>
        public KitPaint WithGround(double y, float ao, float fade)
        {
            KitPaint p = this;
            p.Ground = true;
            p.GroundY = y;
            p.GroundAo = ao;
            p.GroundFade = Math.Max(1e-3f, fade);
            return p;
        }

        /// <summary>Ceiling shade: <paramref name="ao"/> at absolute height <paramref name="y"/> and above, open at
        /// <paramref name="y"/> − <paramref name="fade"/>.</summary>
        public KitPaint WithTop(double y, float ao, float fade)
        {
            KitPaint p = this;
            p.Top = true;
            p.TopY = y;
            p.TopAo = ao;
            p.TopFade = Math.Max(1e-3f, fade);
            return p;
        }

        /// <summary>Contact plane through (x, y, z) with unit normal (nx, ny, nz): <paramref name="ao"/> on the plane
        /// (and behind it), open <paramref name="fade"/> in front of it.</summary>
        public KitPaint WithPlane(double x, double y, double z, double nx, double ny, double nz, float ao, float fade)
        {
            KitPaint p = this;
            p.Plane = true;
            p.PX = x;
            p.PY = y;
            p.PZ = z;
            p.NX = nx;
            p.NY = ny;
            p.NZ = nz;
            p.PlaneAo = ao;
            p.PlaneFade = Math.Max(1e-3f, fade);
            return p;
        }

        /// <summary>The contact plane w = <paramref name="w"/> of a facade frame (normal +W).</summary>
        public KitPaint WithWall(in KitFrame f, double w, float ao, float fade)
        {
            double x, y, z;
            f.ToWorld(0, 0, w, out x, out y, out z);
            return WithPlane(x, y, z, f.WX, 0, f.WZ, ao, fade);
        }

        public KitPaint WithoutPlane()
        {
            KitPaint p = this;
            p.Plane = false;
            return p;
        }

        /// <summary>The AO of one vertex.</summary>
        public float AoAt(double x, double y, double z, double nx, double ny, double nz)
        {
            double ao = Ao;
            if (ny < 0) ao *= 1.0 - Under * -ny;
            if (Ground) ao *= Lerp(GroundAo, (y - GroundY) / GroundFade);
            if (Top) ao *= Lerp(TopAo, (TopY - y) / TopFade);
            if (Plane) ao *= Lerp(PlaneAo, ((x - PX) * NX + (y - PY) * NY + (z - PZ) * NZ) / PlaneFade);
            return ao < 0 ? 0f : ao > 1 ? 1f : (float)ao;
        }

        private static double Lerp(float at0, double t)
        {
            if (t <= 0) return at0;
            if (t >= 1) return 1.0;
            return at0 + (1.0 - at0) * t;
        }

        /// <summary>Paint vertices <paramref name="v0"/>..end: Uv0 = (channel, AO). Turns <see cref="MeshData.HasUv0"/>
        /// on first (older vertices then read Plain and open, see <see cref="FillUnset"/>).</summary>
        public void Apply(MeshData m, int v0)
        {
            Begin(m);
            float ch = (float)Channel;
            float[] p = m.Positions, n = m.Normals, uv = m.Uv0;
            for (int v = v0 < 0 ? 0 : v0; v < m.VertexCount; v++)
            {
                int i = v * 3;
                uv[v * 2] = ch;
                uv[v * 2 + 1] = AoAt(p[i], p[i + 1], p[i + 2], n[i], n[i + 1], n[i + 2]);
            }
        }

        /// <summary>Multiply the AO of vertices <paramref name="v0"/>..end by this brush's terms (its channel is not
        /// written): for a second occluder over geometry that is already painted.</summary>
        public void Darken(MeshData m, int v0)
        {
            Begin(m);
            float[] p = m.Positions, n = m.Normals, uv = m.Uv0;
            for (int v = v0 < 0 ? 0 : v0; v < m.VertexCount; v++)
            {
                int i = v * 3;
                uv[v * 2 + 1] *= AoAt(p[i], p[i + 1], p[i + 2], n[i], n[i + 1], n[i + 2]);
            }
        }

        /// <summary>Make sure <paramref name="m"/> carries UV0; vertices added before read Plain and open (0, 1),
        /// not the fully occluded (0, 0) the buffer would otherwise hold.</summary>
        public static void Begin(MeshData m)
        {
            if (m.HasUv0) return;
            float[] uv = m.Uv0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                uv[v * 2] = 0f;
                uv[v * 2 + 1] = 1f;
            }
            m.HasUv0 = true;
        }

        /// <summary>After a generator that does not write UV0 appended to a painted mesh: its vertices from
        /// <paramref name="v0"/> read (0, 0), fully occluded; set them to Plain and open. Vertices that already carry a
        /// channel or an AO are left alone.</summary>
        public static void FillUnset(MeshData m, int v0)
        {
            if (!m.HasUv0) return;
            float[] uv = m.Uv0;
            for (int v = v0 < 0 ? 0 : v0; v < m.VertexCount; v++)
                if (uv[v * 2] == 0f && uv[v * 2 + 1] == 0f) uv[v * 2 + 1] = 1f;
        }
    }
}
