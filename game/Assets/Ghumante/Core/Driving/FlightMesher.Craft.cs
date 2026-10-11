using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    public static partial class FlightMesher
    {
        // ================================================================================================ stations

        // Fuselage sections, nose first (ref_flight.md §4: side-view proportions read off the reference photos).
        private static readonly Station[] AtrStations =
        {
            new Station(13.58, 1.72, 0.06, 0.06), new Station(13.35, 1.74, 0.52, 0.46), new Station(12.95, 1.8, 0.92, 0.84),
            new Station(12.35, 1.88, 1.22, 1.14), new Station(11.5, 1.95, 1.42, 1.36), new Station(10.4, 2.0, 1.5, 1.46),
            new Station(-5.6, 2.0, 1.5, 1.46), new Station(-7.6, 2.14, 1.38, 1.34), new Station(-9.6, 2.42, 1.1, 1.08),
            new Station(-11.6, 2.82, 0.72, 0.74), new Station(-13.05, 3.18, 0.34, 0.4), new Station(-13.58, 3.32, 0.07, 0.1),
        };

        private static readonly Station[] DashStations =
        {
            new Station(16.4, 1.82, 0.06, 0.06), new Station(16.05, 1.84, 0.45, 0.4), new Station(15.45, 1.9, 0.86, 0.8),
            new Station(14.6, 1.98, 1.2, 1.14), new Station(13.6, 2.04, 1.38, 1.36), new Station(12.6, 2.06, 1.42, 1.42),
            new Station(-8.6, 2.06, 1.42, 1.42), new Station(-10.8, 2.2, 1.28, 1.28), new Station(-12.9, 2.5, 1.0, 1.0),
            new Station(-14.9, 2.9, 0.62, 0.66), new Station(-16.0, 3.2, 0.3, 0.36), new Station(-16.4, 3.3, 0.07, 0.09),
        };

        private static readonly Station[] JetstreamStations =
        {
            new Station(9.62, 1.62, 0.05, 0.05), new Station(9.3, 1.66, 0.36, 0.32), new Station(8.7, 1.76, 0.7, 0.62),
            new Station(7.9, 1.88, 0.98, 0.9), new Station(7.0, 1.96, 1.1, 1.06), new Station(6.0, 1.98, 1.12, 1.1),
            new Station(-3.2, 1.98, 1.12, 1.1), new Station(-5.0, 2.1, 0.98, 0.98), new Station(-6.8, 2.34, 0.7, 0.72),
            new Station(-8.6, 2.66, 0.38, 0.42), new Station(-9.62, 2.86, 0.06, 0.08),
        };

        private static readonly Station[] OtterStations =
        {
            new Station(7.88, 1.42, 0.06, 0.06, 2.6), new Station(7.6, 1.44, 0.34, 0.3, 2.8), new Station(7.0, 1.5, 0.6, 0.54, 3.0),
            new Station(6.2, 1.56, 0.76, 0.72, 3.2), new Station(5.3, 1.6, 0.82, 0.86, 3.4), new Station(4.4, 1.62, 0.84, 0.92, 3.6),
            new Station(-2.6, 1.62, 0.84, 0.92, 3.6), new Station(-4.0, 1.78, 0.72, 0.78, 3.3), new Station(-5.5, 2.06, 0.5, 0.52, 2.8),
            new Station(-7.0, 2.36, 0.24, 0.28, 2.4), new Station(-7.6, 2.46, 0.06, 0.08, 2.2),
        };

        private static readonly Station[] HeliStations =
        {
            new Station(2.42, 1.08, 0.05, 0.05), new Station(2.3, 1.12, 0.42, 0.42), new Station(2.0, 1.2, 0.72, 0.68),
            new Station(1.45, 1.3, 0.9, 0.84), new Station(0.7, 1.34, 0.96, 0.9), new Station(-0.4, 1.36, 0.95, 0.92),
            new Station(-1.3, 1.44, 0.82, 0.82), new Station(-1.95, 1.62, 0.5, 0.52), new Station(-2.4, 1.72, 0.32, 0.34),
            new Station(-4.6, 1.86, 0.24, 0.24), new Station(-6.6, 1.98, 0.17, 0.17), new Station(-6.95, 2.0, 0.08, 0.08),
        };

        private static Station[] StationsOf(FlightCraft c)
        {
            switch (c)
            {
                case FlightCraft.TurbopropStretch: return DashStations;
                case FlightCraft.Commuter: return JetstreamStations;
                case FlightCraft.Stol: return OtterStations;
                case FlightCraft.Helicopter: return HeliStations;
                default: return AtrStations;
            }
        }

        // ==================================================================================================== body

        private static void BuildBody(FlightCraft c, Kit k)
        {
            FlightLiveries.Livery v = k.Liv;
            Station[] st = StationsOf(c);
            var paint = new FuselagePaint { Body = v.Body, Belly = v.Belly, Stripe = v.Stripe, Tail = v.Tail };
            ShapeBrush body = Kit.B(v.Body, MaterialChannel.Paint), belly = Kit.B(v.Belly, MaterialChannel.Paint);
            ShapeBrush dark = Kit.B(DarkMetal, MaterialChannel.Metal), door = Kit.B(Shade(v.Body, 0.9), MaterialChannel.Paint);
            ShapeBrush stripe = Kit.B(v.Stripe, MaterialChannel.Paint);
            int ring = k.Lod >= 2 ? 8 : k.Ring(28);
            switch (c)
            {
                case FlightCraft.TurbopropStretch:
                {
                    paint.TailZ = -12.6;
                    paint.StripeLow = paint.StripeHigh = 2.0;
                    Fuselage(k, st, ring, paint);
                    Band(k, st, 15.5, -13.6, -0.4, -0.14, 0.34, stripe);
                    FlightDeck(k, st, 15.25, 0.45, 14.65, 0.7, 0.6, 1.15);
                    Windows(k, st, k.Lod == 0 ? 18 : 12, 10.2, -7.6, 0.18, 0.34, 0.44, 0.13);
                    if (k.Lod == 0)
                    {
                        Pane(k, st, 11.5, -AngleAtHeight(0.05), 0.8, 1.6, 0.12, door); // front airstair door
                        Pane(k, st, -9.7, -AngleAtHeight(0.05), 0.8, 1.55, 0.12, door);
                        Antenna(k, 0, 3.47, 4.0);
                        Antenna(k, 0, 3.47, -4.5);
                    }
                    Light(k, 0xFF3030, 0, 3.5, -6.2, 0.09);
                    Light(k, 0xFF3030, 0, 0.63, 1.5, 0.08);
                    break;
                }
                case FlightCraft.Commuter:
                {
                    paint.TailZ = -6.6;
                    paint.StripeLow = paint.StripeHigh = 2.0;
                    Fuselage(k, st, ring, paint);
                    Band(k, st, 8.75, -8.2, -0.36, -0.16, 0.3, stripe);
                    FlightDeck(k, st, 8.55, 0.35, 8.0, 0.6, 0.6, 1.12);
                    // Big oval windows (the Jetstream's look).
                    Windows(k, st, k.Lod == 0 ? 10 : 7, 5.2, -2.6, 0.22, 0.42, 0.56, 0.2);
                    // Ventral fairing over the wing box under the floor.
                    Shapes.Superellipsoid(k.M, Affine3.Translation(0, 0.98, 0.0), belly, 1.0, 0.36, 2.9, 0.5, 0.5, k.Lod == 0 ? 20 : 10, k.SL);
                    if (k.Lod == 0)
                    {
                        Pane(k, st, 6.1, -AngleAtHeight(0.05), 0.72, 1.4, 0.14, door);
                        Antenna(k, 0, 3.08, 2.0);
                    }
                    Light(k, 0xFF3030, 0, 3.1, -2.4, 0.07);
                    break;
                }
                case FlightCraft.Stol:
                {
                    paint.TailZ = -5.6;
                    paint.StripeLow = paint.StripeHigh = 2.0;
                    Fuselage(k, st, ring, paint);
                    Band(k, st, 7.1, -6.4, -0.34, -0.12, 0.25, stripe);
                    // The Twin Otter's slanted windscreen and the cockpit doors' big windows.
                    FlightDeck(k, st, 5.65, 0.2, 5.1, 0.9, 0.62, 1.2);
                    // Square cabin windows and the cockpit doors' windows.
                    Windows(k, st, k.Lod == 0 ? 6 : 4, 2.9, -1.7, 0.3, 0.46, 0.44, 0.08);
                    if (k.Lod == 0)
                    {
                        Pane(k, st, -3.1, -AngleAtHeight(0.0), 1.25, 1.45, 0.08, door); // rear cargo door
                        Pane(k, st, 4.0, -AngleAtHeight(-0.1), 0.8, 1.5, 0.08, door); // cockpit door
                        Pane(k, st, 4.0, AngleAtHeight(-0.1), 0.8, 1.5, 0.08, door);
                    }
                    // The long nose baggage hatch line and the belly.
                    Light(k, 0xFF3030, 0, 2.58, -2.4, 0.07);
                    break;
                }
                case FlightCraft.Helicopter:
                {
                    // Big bubble windscreen and door windows (glass), the body colour with a belly band.
                    paint.StripeLow = paint.StripeHigh = 2.0;
                    paint.BellyBelow = -0.72;
                    Fuselage(k, st, ring, paint);
                    // The bubble canopy down to the chin, with the centre and door posts.
                    FlightDeck(k, st, 2.37, 0.04, 0.95, 0.3, 0.95, 1.95);
                    Band(k, st, 0.35, -2.2, -0.58, -0.4, 0.45, Kit.B(v.Stripe, MaterialChannel.Paint));
                    if (k.Lod <= 1)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Pane(k, st, -0.02, s * AngleAtHeight(0.3), 0.6, 0.75, 0.16, Kit.B(Glass, MaterialChannel.Glass));
                            Pane(k, st, -0.82, s * AngleAtHeight(0.3), 0.62, 0.7, 0.14, Kit.B(Glass, MaterialChannel.Glass));
                        }
                    }
                    // Engine and transmission cowling on the roof, the exhaust behind it.
                    Shapes.Superellipsoid(k.M, Affine3.Translation(0, 2.32, -0.55), body, 0.62, 0.42, 1.55, 0.45, 0.45, k.Lod == 0 ? 20 : 10, k.SL);
                    if (k.Lod <= 1)
                        Shapes.Cylinder(k.M, Affine3.Translation(0.42, 2.38, -1.75) * Affine3.RotationX(-Pi / 2 - 0.25), dark, 0.16, 0.5, k.Ring(10), 0.02, 1,
                                        true, true, k.SL);
                    Light(k, 0xFF3030, 0, 2.78, -1.2, 0.07);
                    break;
                }
                default:
                {
                    paint.TailZ = -9.8;
                    paint.StripeLow = paint.StripeHigh = 2.0;
                    Fuselage(k, st, ring, paint);
                    Band(k, st, 12.95, -11.2, -0.4, -0.14, 0.32, stripe);
                    FlightDeck(k, st, 12.75, 0.4, 12.2, 0.65, 0.6, 1.15);
                    Windows(k, st, k.Lod == 0 ? 15 : 10, 8.3, -6.6, 0.17, 0.32, 0.42, 0.12);
                    // Main-gear sponsons low on the sides (the ATR's look) and the wing-root fairing on the roof.
                    for (int s = -1; s <= 1; s += 2)
                        Shapes.Superellipsoid(k.M, Affine3.Translation(s * 1.28, 0.82, -0.25), belly, 0.55, 0.62, 2.7, 0.6, 0.55, k.Lod == 0 ? 18 : 10, k.SL);
                    if (k.Lod == 0)
                    {
                        Pane(k, st, -9.1, -AngleAtHeight(0.05), 0.76, 1.6, 0.12, door); // rear passenger door (left)
                        Pane(k, st, 9.6, -AngleAtHeight(0.0), 1.25, 1.45, 0.1, door); // forward cargo door (left)
                        Antenna(k, 0, 3.42, 6.0);
                        Antenna(k, 0, 3.42, -4.0);
                    }
                    Light(k, 0xFF3030, 0, 3.45, -3.0, 0.09);
                    Light(k, 0xFF3030, 0, 0.56, 3.5, 0.08);
                    break;
                }
            }
        }

        private static void Antenna(Kit k, double x, double y, double z)
        {
            Shapes.Superellipsoid(k.M, Affine3.Translation(x, y + 0.12, z) * Affine3.RotationX(-0.3), Kit.B(0xD8DCDF, MaterialChannel.Paint), 0.03, 0.16,
                                  0.09, 0.5, 0.5, 6, k.SL);
        }

        private static uint Shade(uint rgb, double f)
        {
            uint r = (uint)Math.Min(255, ((rgb >> 16) & 0xFF) * f), g = (uint)Math.Min(255, ((rgb >> 8) & 0xFF) * f), b = (uint)Math.Min(255, (rgb & 0xFF) * f);
            return (r << 16) | (g << 8) | b;
        }

        // =================================================================================================== frame

        private static void BuildFrame(FlightCraft c, Kit k)
        {
            FlightLiveries.Livery v = k.Liv;
            ShapeBrush wing = Kit.B(v.Body, MaterialChannel.Paint), tail = Kit.B(v.Tail, MaterialChannel.Paint), nac = Kit.B(v.Body, MaterialChannel.Paint);
            ShapeBrush grey = Kit.B(0xC9CED3, MaterialChannel.Metal), accent = Kit.B(v.Stripe, MaterialChannel.Paint);
            switch (c)
            {
                case FlightCraft.TurbopropStretch:
                {
                    // High wing: straight centre section, tapered outer panels with dihedral.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Surface(k, wing, 0, 3.35, 1.9, 3.1, 0.17, s * 4.6, 3.35, 1.9, 2.9, 0.16, 0, 1, Boot);
                        Surface(k, wing, s * 4.6, 3.35, 1.9, 2.9, 0.16, s * 14.2, 3.85, 1.2, 1.25, 0.12, 0, 1, Boot);
                        // Long nacelles that hold the main gear, reaching past the trailing edge.
                        Nacelle(k, nac, s * 4.3, 2.75, 5.95, -4.8, 0.72, 0.42, 0.18);
                        Light(k, s < 0 ? 0xFF3030u : 0x30E060u, s * 14.25, 3.86, 0.9, 0.07);
                    }
                    Shapes.Superellipsoid(k.M, Affine3.Translation(0, 3.35, 0.6), wing, 1.05, 0.32, 2.6, 0.5, 0.5, 12, k.SL);
                    // T-tail: an upright fin with a dorsal fillet, the stabiliser on top.
                    Surface(k, tail, 0, 3.0, -10.6, 5.0, 0.14, 0, 8.05, -13.35, 2.6, 0.12, 1, 0, Boot);
                    Surface(k, tail, 0, 3.3, -8.4, 2.8, 0.1, 0, 4.4, -10.5, 1.2, 0.1, 1, 0);
                    for (int s = -1; s <= 1; s += 2)
                        Surface(k, tail, 0, 8.0, -13.2, 2.2, 0.12, s * 4.65, 8.15, -14.0, 1.25, 0.1, 0, 1, Boot);
                    break;
                }
                case FlightCraft.Commuter:
                {
                    // Low wing under the fuselage with dihedral; nacelles sit on the wing, props ahead.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Surface(k, wing, 0, 1.0, 1.6, 2.5, 0.17, s * 2.75, 1.25, 1.55, 2.3, 0.16, 0, 1, Boot);
                        Surface(k, wing, s * 2.75, 1.25, 1.55, 2.3, 0.16, s * 9.15, 1.95, 0.75, 1.0, 0.12, 0, 1, Boot);
                        Nacelle(k, nac, s * 2.75, 1.95, 3.6, -2.0, 0.55, 0.36, 0.14);
                        Light(k, s < 0 ? 0xFF3030u : 0x30E060u, s * 9.2, 1.96, 0.3, 0.06);
                    }
                    // Cruciform tail: the stabiliser crosses the fin half way up.
                    Surface(k, tail, 0, 2.6, -6.4, 3.2, 0.14, 0, 5.7, -8.6, 1.6, 0.12, 1, 0, Boot);
                    Surface(k, tail, 0, 2.7, -5.0, 1.6, 0.1, 0, 3.4, -6.3, 0.8, 0.1, 1, 0);
                    for (int s = -1; s <= 1; s += 2)
                        Surface(k, tail, 0, 3.95, -7.3, 1.55, 0.12, s * 3.3, 4.05, -7.9, 0.9, 0.1, 0, 1, Boot);
                    break;
                }
                case FlightCraft.Stol:
                {
                    // High rectangular wing on the cabin roof, braced by a strut each side.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Surface(k, wing, 0, 2.55, 2.4, 1.98, 0.16, s * 9.9, 2.72, 2.4, 1.98, 0.16, 0, 1);
                        Nacelle(k, nac, s * 3.2, 2.32, 4.55, 0.1, 0.42, 0.3, 0.12);
                        Strut(k, grey, s * 0.82, 1.05, 1.15, s * 4.2, 2.48, 1.55, 0.065);
                        Light(k, s < 0 ? 0xFF3030u : 0x30E060u, s * 9.95, 2.73, 1.2, 0.06);
                    }
                    // Conventional tail: a tall fin with a long dorsal fillet, stabiliser at the tail cone.
                    Surface(k, tail, 0, 2.3, -4.9, 2.6, 0.13, 0, 5.94, -6.4, 1.5, 0.12, 1, 0);
                    Surface(k, tail, 0, 2.2, -3.0, 2.1, 0.08, 0, 3.2, -4.95, 0.9, 0.08, 1, 0);
                    for (int s = -1; s <= 1; s += 2) Surface(k, wing, 0, 2.3, -5.7, 1.65, 0.12, s * 3.2, 2.3, -5.9, 1.4, 0.11, 0, 1);
                    break;
                }
                case FlightCraft.Helicopter:
                {
                    // Rotor mast and the hub fairing; the tail boom's fins; a horizontal stabiliser with end plates.
                    ShapeBrush body = Kit.B(v.Body, MaterialChannel.Paint), dark = Kit.B(DarkMetal, MaterialChannel.Metal);
                    Shapes.Cylinder(k.M, Affine3.Translation(0, 2.6, 0), dark, 0.11, 0.55, k.Ring(12), 0.02, 1, true, true, k.SL);
                    Surface(k, body, 0, 1.9, -6.2, 1.15, 0.16, 0, 3.05, -6.85, 0.7, 0.14, 1, 0);
                    Surface(k, body, 0, 1.9, -6.35, 0.8, 0.14, 0, 1.2, -6.75, 0.5, 0.12, 1, 0);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Surface(k, body, 0, 1.82, -5.0, 0.62, 0.14, s * 1.2, 1.84, -5.05, 0.5, 0.12, 0, 1);
                        Surface(k, Kit.B(v.Stripe, MaterialChannel.Paint), s * 1.2, 1.62, -4.95, 0.62, 0.1, s * 1.2, 2.14, -5.0, 0.55, 0.1, 1, 0);
                    }
                    // Skids: two tubes turned up at the front, on cross tubes.
                    ShapeBrush skid = Kit.B(0x3B4046, MaterialChannel.Metal);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        k.Path.Clear();
                        k.Path.Add(s * 1.1, 0.07, -1.85).Add(s * 1.1, 0.07, 1.25).Add(s * 1.1, 0.12, 1.65).Add(s * 1.08, 0.3, 1.9);
                        Shapes.Tube(k.M, Affine3.Identity, skid, k.Path, 0.055, k.Ring(10), true, false, k.SL);
                        Strut(k, skid, s * 1.1, 0.07, 0.95, s * 0.55, 0.62, 0.95, 0.05);
                        Strut(k, skid, s * 1.1, 0.07, -1.05, s * 0.55, 0.65, -1.05, 0.05);
                    }
                    Strut(k, skid, -0.55, 0.62, 0.95, 0.55, 0.62, 0.95, 0.05);
                    Strut(k, skid, -0.55, 0.65, -1.05, 0.55, 0.65, -1.05, 0.05);
                    Light(k, 0xFF3030, -1.25, 1.84, -5.05, 0.05);
                    Light(k, 0x30E060, 1.25, 1.84, -5.05, 0.05);
                    break;
                }
                default:
                {
                    // ATR: straight centre section to the nacelles, tapered outer wing, the T-tail with a swept fin.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Surface(k, wing, 0, 3.3, 1.65, 2.57, 0.17, s * 4.4, 3.3, 1.65, 2.57, 0.16, 0, 1, Boot);
                        Surface(k, wing, s * 4.4, 3.3, 1.65, 2.57, 0.16, s * 13.5, 3.68, 1.2, 1.4, 0.12, 0, 1, Boot);
                        Nacelle(k, nac, s * 4.1, 2.86, 4.45, -1.9, 0.64, 0.38, 0.16);
                        // The intake scoop under the spinner.
                        if (k.Lod == 0)
                            Shapes.Superellipsoid(k.M, Affine3.Translation(s * 4.1, 2.32, 3.7), Kit.B(Shade(v.Body, 0.85), MaterialChannel.Paint), 0.22, 0.14,
                                                  0.55, 0.5, 0.5, 8, k.SL);
                        Light(k, s < 0 ? 0xFF3030u : 0x30E060u, s * 13.55, 3.69, 0.7, 0.07);
                    }
                    Shapes.Superellipsoid(k.M, Affine3.Translation(0, 3.32, 0.4), wing, 1.0, 0.3, 2.3, 0.5, 0.5, 12, k.SL);
                    Surface(k, tail, 0, 3.2, -8.9, 4.3, 0.14, 0, 7.45, -11.9, 2.15, 0.12, 1, 0, Boot);
                    Surface(k, tail, 0, 3.4, -6.9, 2.4, 0.1, 0, 4.3, -9.2, 1.0, 0.1, 1, 0);
                    for (int s = -1; s <= 1; s += 2)
                        Surface(k, tail, 0, 7.4, -11.6, 2.0, 0.12, s * 3.65, 7.55, -12.3, 1.2, 0.1, 0, 1, Boot);
                    break;
                }
            }
            // Nacelle lips in the stripe colour for a little livery accent on the engines.
            if (c != FlightCraft.Helicopter && k.Lod == 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    float px, py, pz;
                    bool cw;
                    PropMount(c, i, out px, out py, out pz, out cw);
                    Shapes.Torus(k.M, Affine3.Translation(px, py, pz - 0.42) * Affine3.RotationX(Pi / 2), accent, NacelleFront(c) * 0.86, 0.05, 16, 4, ShapeLod.Lod0);
                }
            }
        }

        private static double NacelleFront(FlightCraft c)
        {
            return c == FlightCraft.TurbopropStretch ? 0.42 : c == FlightCraft.Commuter ? 0.36 : c == FlightCraft.Stol ? 0.3 : 0.38;
        }

        // ==================================================================================================== gear

        private static void BuildGear(FlightCraft c, Kit k)
        {
            ShapeBrush leg = Kit.B(0xB8BEC4, MaterialChannel.Metal), dark = Kit.B(DarkMetal, MaterialChannel.Metal);
            switch (c)
            {
                case FlightCraft.TurbopropStretch:
                    // Nose gear (twin) and twin main wheels under each nacelle on tall legs.
                    Strut(k, leg, 0, 0.5, 12.9, 0, 1.35, 12.9, 0.1);
                    TwinWheel(k, 0, 0.42, 12.9, 0.42, 0.26, 0.22, 0);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Strut(k, leg, s * 4.3, 0.55, -1.0, s * 4.3, 2.1, -1.4, 0.14);
                        TwinWheel(k, s * 4.3, 0.55, -1.0, 0.55, 0.32, 0.3, 0);
                    }
                    break;
                case FlightCraft.Commuter:
                    Strut(k, leg, 0, 0.35, 6.7, 0, 1.0, 6.7, 0.08);
                    TwinWheel(k, 0, 0.32, 6.7, 0.32, 0.2, 0.18, 0);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Strut(k, leg, s * 2.95, 0.4, -0.4, s * 2.85, 1.3, -0.2, 0.1);
                        TwinWheel(k, s * 2.95, 0.38, -0.4, 0.38, 0.22, 0.2, 0);
                    }
                    break;
                case FlightCraft.Stol:
                    // Fixed tricycle gear: a nose leg with a fork, main legs splayed out from stubs on the cabin floor.
                    Strut(k, leg, 0, 0.3, 4.5, 0, 0.95, 4.35, 0.07);
                    Wheel(k, 0, 0.3, 4.5, 0.3, 0.2);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Shapes.Superellipsoid(k.M, Affine3.Translation(s * 0.92, 0.82, 0.0), Kit.B(k.Liv.Belly, MaterialChannel.Paint), 0.22, 0.14, 0.42, 0.5,
                                              0.5, k.Lod == 0 ? 10 : 6, k.SL);
                        Strut(k, leg, s * 1.0, 0.78, 0.0, s * 1.75, 0.42, 0.0, 0.07);
                        Wheel(k, s * 1.85, 0.42, 0.0, 0.42, 0.28);
                    }
                    break;
                case FlightCraft.Helicopter:
                    break;
                default:
                    // ATR: twin nose wheels, twin main wheels in each sponson.
                    Strut(k, leg, 0, 0.36, 10.5, 0, 1.0, 10.5, 0.09);
                    TwinWheel(k, 0, 0.34, 10.5, 0.34, 0.22, 0.2, 0);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Strut(k, dark, s * 1.75, 0.45, -0.3, s * 1.45, 0.95, -0.3, 0.1);
                        TwinWheel(k, s * 1.75, 0.45, -0.3, 0.45, 0.3, 0, 0.4);
                    }
                    break;
            }
        }

        // ============================================================================================ props, rotors

        private static void BuildProp(FlightCraft c, Kit k)
        {
            uint spinner = k.Liv.Body;
            switch (c)
            {
                case FlightCraft.TurbopropStretch:
                    Propeller(k, 6, 2.05, 0.42, 1.0, spinner);
                    break;
                case FlightCraft.Commuter:
                    Propeller(k, 5, 1.45, 0.34, 0.8, spinner);
                    break;
                case FlightCraft.Stol:
                    Propeller(k, 3, 1.3, 0.24, 0.55, spinner);
                    break;
                default:
                    Propeller(k, 6, 1.96, 0.36, 0.9, spinner);
                    break;
            }
        }

        private static void BuildRotor(Kit k)
        {
            ShapeBrush hub = Kit.B(DarkMetal, MaterialChannel.Metal), blade = Kit.B(0x2F3338, MaterialChannel.Paint);
            Shapes.Dome(k.M, Affine3.Identity, hub, 0.32, 0.22, k.Lod == 0 ? 14 : 8, true, k.SL);
            for (int i = 0; i < 3; i++)
            {
                double a = 2 * Pi * i / 3;
                // Long thin blades along local +Z rotated round the hub, a touch of droop.
                Affine3 f = Affine3.RotationY(a) * Affine3.Translation(0, 0.06, 2.85) * Affine3.RotationX(0.015);
                Shapes.Superellipsoid(k.M, f, blade, 0.17, 0.035, 2.55, 0.4, 0.5, k.Lod == 0 ? 12 : 8, k.SL);
                if (k.Lod == 0)
                    Shapes.Superellipsoid(k.M, Affine3.RotationY(a) * Affine3.Translation(0, 0.065, 5.2), Kit.B(PropTip, MaterialChannel.Paint), 0.175, 0.037,
                                          0.18, 0.4, 0.5, 8, k.SL);
            }
        }

        private static void BuildTailRotor(Kit k)
        {
            ShapeBrush blade = Kit.B(0x2F3338, MaterialChannel.Paint);
            Shapes.Sphere(k.M, Affine3.Identity, Kit.B(DarkMetal, MaterialChannel.Metal), 0.09, k.Lod == 0 ? 10 : 6, k.SL);
            for (int i = 0; i < 2; i++)
            {
                double a = Pi * i;
                Affine3 f = Affine3.RotationX(a) * Affine3.Translation(0, 0.47, 0) * Affine3.RotationY(0.25);
                Shapes.Superellipsoid(k.M, f, blade, 0.025, 0.45, 0.09, 0.4, 0.5, 8, k.SL);
            }
        }
    }
}
