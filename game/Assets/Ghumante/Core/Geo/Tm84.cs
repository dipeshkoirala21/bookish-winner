using System;

namespace Ghumante.Core.Geo
{
    /// <summary>
    /// NPL-TM84, the canonical frame (ARCHITECTURE.md 5.1): WGS84 Transverse Mercator with
    /// <c>lon0 = 84°E, lat0 = 0, k0 = 0.9996, FE = 500 000 m, FN = 0</c>.
    ///
    /// Uses the Krüger series to sixth order in the third flattening <c>n</c>, as formulated by
    /// Karney (2011, "Transverse Mercator with an accuracy of a few nanometers") and implemented by PROJ's
    /// default <c>tmerc</c>/<c>etmerc</c>. Inside Nepal the series error is on the order of nanometres, so
    /// results agree with pyproj (projection.py) far below a millimetre; the golden test checks this.
    /// </summary>
    public static class Tm84
    {
        public const double SemiMajorAxis = 6378137.0;
        public const double InverseFlattening = 298.257223563;
        public const double CentralMeridianDeg = 84.0;
        public const double ScaleFactor = 0.9996;
        public const double FalseEasting = 500000.0;
        public const double FalseNorthing = 0.0;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        private static readonly double E; // first eccentricity
        private static readonly double E2;
        private static readonly double Ka; // k0 * A, A the rectifying radius
        private static readonly double[] Alpha = new double[7]; // 1-based
        private static readonly double[] Beta = new double[7];

        static Tm84()
        {
            double f = 1.0 / InverseFlattening;
            E2 = f * (2.0 - f);
            E = Math.Sqrt(E2);
            double n = f / (2.0 - f);
            double n2 = n * n, n3 = n2 * n, n4 = n3 * n, n5 = n4 * n, n6 = n5 * n;
            double a = SemiMajorAxis / (1.0 + n) * (1.0 + n2 / 4.0 + n4 / 64.0 + n6 / 256.0);
            Ka = ScaleFactor * a;

            Alpha[1] = n / 2.0 - 2.0 * n2 / 3.0 + 5.0 * n3 / 16.0 + 41.0 * n4 / 180.0 - 127.0 * n5 / 288.0
                       + 7891.0 * n6 / 37800.0;
            Alpha[2] = 13.0 * n2 / 48.0 - 3.0 * n3 / 5.0 + 557.0 * n4 / 1440.0 + 281.0 * n5 / 630.0
                       - 1983433.0 * n6 / 1935360.0;
            Alpha[3] = 61.0 * n3 / 240.0 - 103.0 * n4 / 140.0 + 15061.0 * n5 / 26880.0 + 167603.0 * n6 / 181440.0;
            Alpha[4] = 49561.0 * n4 / 161280.0 - 179.0 * n5 / 168.0 + 6601661.0 * n6 / 7257600.0;
            Alpha[5] = 34729.0 * n5 / 80640.0 - 3418889.0 * n6 / 1995840.0;
            Alpha[6] = 212378941.0 * n6 / 319334400.0;

            Beta[1] = n / 2.0 - 2.0 * n2 / 3.0 + 37.0 * n3 / 96.0 - n4 / 360.0 - 81.0 * n5 / 512.0
                      + 96199.0 * n6 / 604800.0;
            Beta[2] = n2 / 48.0 + n3 / 15.0 - 437.0 * n4 / 1440.0 + 46.0 * n5 / 105.0 - 1118711.0 * n6 / 3870720.0;
            Beta[3] = 17.0 * n3 / 480.0 - 37.0 * n4 / 840.0 - 209.0 * n5 / 4480.0 + 5569.0 * n6 / 90720.0;
            Beta[4] = 4397.0 * n4 / 161280.0 - 11.0 * n5 / 504.0 - 830251.0 * n6 / 7257600.0;
            Beta[5] = 4583.0 * n5 / 161280.0 - 108847.0 * n6 / 3991680.0;
            Beta[6] = 20648693.0 * n6 / 638668800.0;
        }

        /// <summary>Geographic degrees to canonical metres (easting, northing).</summary>
        public static void Forward(double lonDeg, double latDeg, out double easting, out double northing)
        {
            double lam = (lonDeg - CentralMeridianDeg) * DegToRad;
            double phi = latDeg * DegToRad;
            double tau = Math.Tan(phi);
            double tauP = TauPrime(tau);
            double cosLam = Math.Cos(lam);
            double xiP = Math.Atan2(tauP, cosLam);
            double etaP = Asinh(Math.Sin(lam) / Math.Sqrt(tauP * tauP + cosLam * cosLam));

            double xi = xiP, eta = etaP;
            for (int j = 1; j <= 6; j++)
            {
                double s = 2.0 * j;
                xi += Alpha[j] * Math.Sin(s * xiP) * Math.Cosh(s * etaP);
                eta += Alpha[j] * Math.Cos(s * xiP) * Math.Sinh(s * etaP);
            }
            easting = FalseEasting + Ka * eta;
            northing = FalseNorthing + Ka * xi;
        }

        /// <summary>Canonical metres to geographic degrees (longitude, latitude).</summary>
        public static void Inverse(double easting, double northing, out double lonDeg, out double latDeg)
        {
            double xi = (northing - FalseNorthing) / Ka;
            double eta = (easting - FalseEasting) / Ka;
            double xiP = xi, etaP = eta;
            for (int j = 1; j <= 6; j++)
            {
                double s = 2.0 * j;
                xiP -= Beta[j] * Math.Sin(s * xi) * Math.Cosh(s * eta);
                etaP -= Beta[j] * Math.Cos(s * xi) * Math.Sinh(s * eta);
            }
            double sinhEta = Math.Sinh(etaP);
            double cosXi = Math.Cos(xiP);
            double tauP = Math.Sin(xiP) / Math.Sqrt(sinhEta * sinhEta + cosXi * cosXi);
            double lam = Math.Atan2(sinhEta, cosXi);
            double tau = TauFromTauPrime(tauP);
            latDeg = Math.Atan(tau) * RadToDeg;
            lonDeg = CentralMeridianDeg + lam * RadToDeg;
        }

        /// <summary>tan of the conformal latitude from tan of the geodetic latitude.</summary>
        private static double TauPrime(double tau)
        {
            double tau1 = Math.Sqrt(1.0 + tau * tau);
            double sig = Math.Sinh(E * Atanh(E * tau / tau1));
            return tau * Math.Sqrt(1.0 + sig * sig) - sig * tau1;
        }

        /// <summary>Newton iteration inverting <see cref="TauPrime"/> (Karney 2011, eqs. 19-21).</summary>
        private static double TauFromTauPrime(double tauP)
        {
            double tau = tauP;
            for (int i = 0; i < 10; i++)
            {
                double tauPi = TauPrime(tau);
                double tau1 = Math.Sqrt(1.0 + tau * tau);
                double d = (tauP - tauPi) / Math.Sqrt(1.0 + tauPi * tauPi)
                           * (1.0 + (1.0 - E2) * tau * tau) / ((1.0 - E2) * tau1);
                tau += d;
                if (Math.Abs(d) <= 1e-15 * Math.Max(1.0, Math.Abs(tau))) break;
            }
            return tau;
        }

        private static double Asinh(double x)
        {
            double ax = Math.Abs(x);
            double r = Log1p(ax + ax * ax / (1.0 + Math.Sqrt(1.0 + ax * ax)));
            return x < 0 ? -r : r;
        }

        private static double Atanh(double x)
        {
            double ax = Math.Abs(x);
            double r = 0.5 * Log1p(2.0 * ax / (1.0 - ax));
            return x < 0 ? -r : r;
        }

        /// <summary>log(1 + x) accurate for small x (Goldberg's trick; no Math.Log1p in netstandard2.1).</summary>
        private static double Log1p(double x)
        {
            double u = 1.0 + x;
            double d = u - 1.0;
            return d == 0.0 ? x : Math.Log(u) * x / d;
        }
    }
}
