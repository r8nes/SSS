using System;
using UnityEngine;

namespace Aetheria.Runtime.Water
{
    internal static class WaterSpectrumMath
    {
        private const double k_SpectralDepth = 20.0;
        private const double k_TwoPi = 2.0 * Math.PI;

        internal static Vector3 EstimateCascadeVariance(WaterSpectrumParameters spectrum, int resolution)
        {
            return new Vector3(
                EstimateCascadeVariance(spectrum.cascade0, resolution),
                EstimateCascadeVariance(spectrum.cascade1, resolution),
                EstimateCascadeVariance(spectrum.cascade2, resolution));
        }

        private static float EstimateCascadeVariance(WaterCascadeSpectrumParameters cascade, int resolution)
        {
            if (resolution <= 0)
                return 0.0f;

            double variance = 0.0;
            int half = resolution / 2;
            double deltaKx = cascade.deltaK.x;
            double deltaKz = cascade.deltaK.y;
            double directionalDensity = 1.0 / k_TwoPi;
            double detailWidth = 1.0 - cascade.detail;
            for (int y = 0; y < resolution; ++y)
            {
                int nz = y - half;
                double kz = nz * deltaKz;
                for (int x = 0; x < resolution; ++x)
                {
                    int nx = x - half;
                    double kx = nx * deltaKx;
                    double k = Math.Sqrt(kx * kx + kz * kz);
                    if (k <= 1e-9)
                        continue;

                    EvaluateDispersion(k, out double omega, out double dOmegaDk);
                    double density = EvaluateJonswap(cascade, omega) * EvaluateTma(omega, k);
                    double detail = Math.Exp(-detailWidth * detailWidth * k * k);
                    variance += density * directionalDensity * detail * dOmegaDk / k * deltaKx * deltaKz;
                }
            }
            return (float)Math.Max(variance, 0.0);
        }

        private static double EvaluateJonswap(WaterCascadeSpectrumParameters cascade, double omega)
        {
            if (omega <= 1e-9 || cascade.omegaPeak <= 1e-9f)
                return 0.0;
            double sigma = omega <= cascade.omegaPeak ? 0.07 : 0.09;
            double offset = (omega - cascade.omegaPeak) / (sigma * cascade.omegaPeak);
            double peak = Math.Pow(3.3, Math.Exp(-0.5 * offset * offset));
            double ratio = cascade.omegaPeak / omega;
            double g = WaterConstants.k_GravityConstant;
            return cascade.alpha * g * g / Math.Pow(omega, 5.0)
                * Math.Exp(-1.25 * Math.Pow(ratio, 4.0)) * peak;
        }

        private static double EvaluateTma(double omega, double k)
        {
            double kh = k * k_SpectralDepth;
            if (kh > 10.0)
                return 1.0;
            double tanhKh = Math.Tanh(kh);
            double sinh2Kh = Math.Sinh(2.0 * kh);
            double denominator = 1.0 + (Math.Abs(sinh2Kh) > 1e-9 ? 2.0 * kh / sinh2Kh : 1.0);
            return tanhKh * tanhKh / Math.Max(denominator, 1e-9);
        }

        private static void EvaluateDispersion(double k, out double omega, out double dOmegaDk)
        {
            double g = WaterConstants.k_GravityConstant;
            double kh = Math.Min(k * k_SpectralDepth, 20.0);
            double tanhKh = Math.Tanh(kh);
            double coshKh = Math.Cosh(kh);
            double sechKh = 1.0 / coshKh;
            omega = Math.Sqrt(g * k * tanhKh);
            dOmegaDk = g * (tanhKh + kh * sechKh * sechKh) / Math.Max(2.0 * omega, 1e-9);
        }
    }
}
