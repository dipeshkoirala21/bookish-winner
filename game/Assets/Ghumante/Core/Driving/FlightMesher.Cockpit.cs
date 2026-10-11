using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    public static partial class FlightMesher
    {
        /// <summary>
        /// What the pilot sees from the left seat (<see cref="FlightSpec.EyeX"/>, EyeY, EyeZ): the nose ahead in the
        /// body colour, the window frame posts, the glareshield over a dark instrument panel with glowing displays, two
        /// yokes (cyclic and collective sticks in the helicopter), the centre pedestal and dark side walls. Drawn instead
        /// of the fuselage in the cockpit view (wings, engines and props stay visible through the side windows).
        /// </summary>
        private static void BuildCockpit(FlightCraft c, Kit k)
        {
            FlightSpec s = FlightSpec.For(c);
            Station[] st = StationsOf(c);
            ShapeBrush panel = Kit.B(0x2B2F34, MaterialChannel.Plain), frame = Kit.B(0x3A3F45, MaterialChannel.Paint);
            ShapeBrush wall = Kit.B(Interior, MaterialChannel.Fabric), body = Kit.B(k.Liv.Body, MaterialChannel.Paint);
            double ez = s.EyeZ, ey = s.EyeY;
            bool heli = c == FlightCraft.Helicopter;

            // The nose ahead of the windscreen (outside skin) so the view has the aircraft in it.
            var paint = new FuselagePaint { Body = k.Liv.Body, Belly = k.Liv.Belly, Stripe = k.Liv.Stripe, Tail = k.Liv.Tail };
            int n = 0;
            for (int i = 0; i < st.Length; i++)
                if (st[i].Z > ez + (heli ? 0.4 : 0.9)) n++;
            if (n >= 2)
            {
                var nose = new Station[n + 1];
                for (int i = 0; i < n; i++) nose[i] = st[i];
                Station cut = SectionAt(st, ez + (heli ? 0.3 : 0.75));
                // Cut the nose off just below the windscreen sill so it never covers the view.
                nose[n] = cut;
                for (int i = 0; i <= n; i++)
                {
                    Station a = nose[i];
                    double sill = ey - 0.45 - a.Yc;
                    if (a.B > 0 && sill < a.B) a.B = Math.Max(0.05, Math.Min(a.B, a.B * 0.5 + sill * 0.5));
                    nose[i] = a;
                }
                Fuselage(k, nose, k.Ring(20), paint);
            }

            // Instrument panel with displays, the glareshield over it, and the pedestal between the seats.
            double pz = ez + (heli ? 0.75 : 0.95), py = ey - (heli ? 0.55 : 0.42);
            double halfW = heli ? 0.8 : Math.Min(1.0, SectionAt(st, ez).A * 0.82);
            Shapes.RoundedBox(k.M, Affine3.Translation(0, py, pz) * Affine3.RotationX(-0.35), panel, halfW * 2, 0.52, 0.12, 0.05, 2, k.SL);
            Shapes.RoundedBox(k.M, Affine3.Translation(0, py + 0.3, pz + 0.05), Kit.B(0x1E2125, MaterialChannel.Fabric), halfW * 2 + 0.1, 0.07, 0.42, 0.03, 2,
                              k.SL);
            int screens = heli ? 2 : 4;
            for (int i = 0; i < screens; i++)
            {
                double x = -halfW + halfW * 2 * (i + 0.5) / screens;
                uint glow = i % 2 == 0 ? 0x2E6FA8u : 0x3C8A5Au;
                Shapes.RoundedBox(k.M, Affine3.Translation(x, py + 0.03, pz - 0.075) * Affine3.RotationX(-0.35), Kit.B(glow, MaterialChannel.Glass),
                                  halfW * 2 / screens * 0.72, 0.3, 0.02, 0.02, 1, k.SL);
            }
            if (!heli) Shapes.RoundedBox(k.M, Affine3.Translation(0, ey - 0.85, ez + 0.45), panel, 0.36, 0.5, 0.8, 0.06, 2, k.SL);

            // Yokes in front of both seats (sticks in the helicopter).
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * Math.Abs(s.EyeX);
                if (heli)
                {
                    Strut(k, frame, x, ey - 1.05, ez + 0.25, x, ey - 0.55, ez + 0.32, 0.025);
                    Shapes.Sphere(k.M, Affine3.Translation(x, ey - 0.52, ez + 0.33), frame, 0.045, 8, k.SL);
                    continue;
                }
                Strut(k, frame, x, ey - 0.95, ez + 0.75, x, ey - 0.5, ez + 0.6, 0.03);
                Shapes.Torus(k.M, Affine3.Translation(x, ey - 0.42, ez + 0.57) * Affine3.RotationX(Pi / 2 + 0.3), frame, 0.15, 0.022, 14, 6, k.SL, 200, 140, false);
            }

            // Window frame: the posts either side of the windscreen and the roof edge above it.
            double postZ = ez + (heli ? 0.35 : 0.6);
            double topY = ey + (heli ? 0.75 : 0.42), sillY = ey - 0.34;
            double posts = heli ? 0.92 : halfW + 0.05;
            for (int side = -1; side <= 1; side += 2)
            {
                Strut(k, frame, side * posts, sillY, postZ + 0.3, side * posts * 0.82, topY, postZ - 0.15, heli ? 0.03 : 0.055);
                if (!heli) Strut(k, frame, side * posts, sillY, postZ + 0.3, side * (posts + 0.12), sillY - 0.05, ez - 0.8, 0.05);
            }
            if (!heli)
            {
                Strut(k, frame, 0, sillY, postZ + 0.42, 0, topY, postZ - 0.1, 0.035);
                Strut(k, frame, -posts * 0.82, topY, postZ - 0.15, posts * 0.82, topY, postZ - 0.15, 0.05);
                // The roof over the pilot's head, dark.
                Shapes.RoundedBox(k.M, Affine3.Translation(0, topY + 0.1, ez - 0.4), wall, posts * 2 + 0.3, 0.08, 1.6, 0.04, 1, k.SL);
            }
            else
            {
                Strut(k, frame, -0.9, topY, postZ - 0.15, 0.9, topY, postZ - 0.15, 0.03);
            }
            // Side walls below the side windows and the floor.
            for (int side = -1; side <= 1; side += 2)
                Shapes.RoundedBox(k.M, Affine3.Translation(side * (posts + 0.1), ey - 0.75, ez - 0.4), wall, 0.08, 0.8, 2.0, 0.03, 1, k.SL);
            Shapes.RoundedBox(k.M, Affine3.Translation(0, ey - 1.25, ez - 0.2), wall, posts * 2, 0.06, 2.4, 0.02, 1, k.SL);
        }
    }
}
