using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Data
{
    internal readonly struct AmbientProbeShaderData
    {
        // GPU projection returns canonical orthonormal SH coefficients. Unity's
        // SphericalHarmonicsL2 stores the basis-premultiplied polynomial coefficients
        // consumed by unity_SH*. Clamped-cosine coefficients are already divided by PI.
        private static readonly float[] s_UnityShBasis =
        {
            0.28209479177387814f,
           -0.4886025119029199f,
            0.4886025119029199f,
           -0.4886025119029199f,
            1.0925484305920792f,
           -1.0925484305920792f,
            0.31539156525252005f,
           -1.0925484305920792f,
            0.5462742152960396f
        };

        private static readonly float[] s_ClampedCosineConvolution =
        {
            1.0f,
            2.0f / 3.0f,
            2.0f / 3.0f,
            2.0f / 3.0f,
            1.0f / 4.0f,
            1.0f / 4.0f,
            1.0f / 4.0f,
            1.0f / 4.0f,
            1.0f / 4.0f
        };

        public readonly Vector4 SHAr;
        public readonly Vector4 SHAg;
        public readonly Vector4 SHAb;
        public readonly Vector4 SHBr;
        public readonly Vector4 SHBg;
        public readonly Vector4 SHBb;
        public readonly Vector4 SHC;

        internal AmbientProbeShaderData(
            Vector4 shAr,
            Vector4 shAg,
            Vector4 shAb,
            Vector4 shBr,
            Vector4 shBg,
            Vector4 shBb,
            Vector4 shC)
        {
            SHAr = shAr;
            SHAg = shAg;
            SHAb = shAb;
            SHBr = shBr;
            SHBg = shBg;
            SHBb = shBb;
            SHC = shC;
        }

        public static AmbientProbeShaderData From(SphericalHarmonicsL2 sh)
        {
            return new AmbientProbeShaderData(
                new Vector4(sh[0, 3], sh[0, 1], sh[0, 2], sh[0, 0] - sh[0, 6]),
                new Vector4(sh[1, 3], sh[1, 1], sh[1, 2], sh[1, 0] - sh[1, 6]),
                new Vector4(sh[2, 3], sh[2, 1], sh[2, 2], sh[2, 0] - sh[2, 6]),
                new Vector4(sh[0, 4], sh[0, 5], sh[0, 6] * 3.0f, sh[0, 7]),
                new Vector4(sh[1, 4], sh[1, 5], sh[1, 6] * 3.0f, sh[1, 7]),
                new Vector4(sh[2, 4], sh[2, 5], sh[2, 6] * 3.0f, sh[2, 7]),
                new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1.0f));
        }

        public static SphericalHarmonicsL2 ConvertRadianceToUnityDiffuseProbe(
            SphericalHarmonicsL2 radianceProbe)
        {
            var diffuseProbe = radianceProbe;

            for (int channel = 0; channel < 3; ++channel)
            for (int coefficient = 0; coefficient < 9; ++coefficient)
            {
                diffuseProbe[channel, coefficient] *=
                    s_UnityShBasis[coefficient] *
                    s_ClampedCosineConvolution[coefficient];
            }

            return diffuseProbe;
        }

        public Vector3 Evaluate(Vector3 normal)
        {
            var normalL0L1 = new Vector4(normal.x, normal.y, normal.z, 1.0f);

            var normalL2 = new Vector4(
                normal.x * normal.y,
                normal.y * normal.z,
                normal.z * normal.z,
                normal.z * normal.x);

            float l2C = normal.x * normal.x - normal.y * normal.y;

            var value = new Vector3(
                Vector4.Dot(SHAr, normalL0L1) + Vector4.Dot(SHBr, normalL2) + SHC.x * l2C,
                Vector4.Dot(SHAg, normalL0L1) + Vector4.Dot(SHBg, normalL2) + SHC.y * l2C,
                Vector4.Dot(SHAb, normalL0L1) + Vector4.Dot(SHBb, normalL2) + SHC.z * l2C);

            return Vector3.Max(Vector3.zero, value);
        }

        public static AmbientProbeShaderData FromApproximateMiePhase(
            SphericalHarmonicsL2 unityDiffuseProbe,
            float diameterMicrometers)
        {
            float d = Mathf.Clamp(diameterMicrometers, 5.0f, 50.0f);
            float hgG = Mathf.Exp(-0.0990567f * Mathf.Pow(d, -1.67154f));
            float draineG = Mathf.Exp(-2.20679f / (d + 3.91029f) - 0.428934f);
            float draineAlpha = Mathf.Exp(3.62489f - 8.29288f / (d + 5.52825f));
            float draineWeight = Mathf.Exp(-0.599085f * Mathf.Pow(d, -0.641583f) - 0.665888f);

            float draineG2 = draineG * draineG;
            float draineG3 = draineG2 * draineG;
            float draineG4 = draineG2 * draineG2;
            float draineNormalization = 1.0f + draineAlpha * (1.0f + 2.0f * draineG2) / 3.0f;
            float draineBand1 =
                (draineG + draineAlpha * (3.0f * draineG + 2.0f * draineG3) / 5.0f) /
                draineNormalization;
            float draineBand2 =
                (draineG2 + draineAlpha *
                    (2.0f / 15.0f + 11.0f * draineG2 / 21.0f + 12.0f * draineG4 / 35.0f)) /
                draineNormalization;

            float phaseBand1 = Mathf.Lerp(hgG, draineBand1, draineWeight);
            float phaseBand2 = Mathf.Lerp(hgG * hgG, draineBand2, draineWeight);

            float band1 = phaseBand1 / (2.0f / 3.0f);
            float band2 = phaseBand2 / 0.25f;
            var sh = unityDiffuseProbe;

            for (int channel = 0; channel < 3; ++channel)
            {
                for (int coefficient = 1; coefficient <= 3; ++coefficient)
                    sh[channel, coefficient] *= band1;
                for (int coefficient = 4; coefficient <= 8; ++coefficient)
                    sh[channel, coefficient] *= band2;
            }

            return From(sh);
        }
    }
}
