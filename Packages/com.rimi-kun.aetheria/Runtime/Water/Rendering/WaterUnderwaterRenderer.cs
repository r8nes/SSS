using Aetheria.Runtime.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterUnderwaterRenderer
    {
        internal readonly struct WaterLineParameters
        {
            internal readonly Vector4 boundsSS;
            internal readonly Vector2 upDirectionSS;
            internal readonly Vector4 screenSize;
            internal readonly int bufferStride;
            internal readonly int reductionSize;
            internal readonly float cameraHeight;
            internal readonly Vector3 upDirectionWS;

            internal WaterLineParameters(
                Vector4 boundsSS,
                Vector2 upDirectionSS,
                Vector4 screenSize,
                int bufferStride,
                int reductionSize,
                float cameraHeight,
                Vector3 upDirectionWS)
            {
                this.boundsSS = boundsSS;
                this.upDirectionSS = upDirectionSS;
                this.screenSize = screenSize;
                this.bufferStride = bufferStride;
                this.reductionSize = reductionSize;
                this.cameraHeight = cameraHeight;
                this.upDirectionWS = upDirectionWS;
            }
        }

        private Material m_EffectMaterial;
        private ComputeShader m_LineShader;
        private ComputeShader m_EvaluationShader;
        private int m_ClearLineKernel = -1;
        private int m_LineEvaluationKernel = -1;
        private int m_BoundsPropagationKernel = -1;
        private int m_EvaluationFlatKernel = -1;
        private int m_EvaluationKernel = -1;
        private int m_EvaluationMaskKernel = -1;
        private int m_EvaluationCurrentKernel = -1;
        private int m_EvaluationCurrentMaskKernel = -1;
        private Texture2D m_CausticsTexture;
        private Texture2D m_CausticsDistortionTexture;

        private readonly Vector3[] m_AmbientDirection = { Vector3.zero };
        private readonly Color[] m_AmbientLighting = new Color[1];

        internal Material effectMaterial => m_EffectMaterial;
        internal ComputeShader lineShader => m_LineShader;
        internal ComputeShader evaluationShader => m_EvaluationShader;
        internal int clearLineKernel => m_ClearLineKernel;
        internal int lineEvaluationKernel => m_LineEvaluationKernel;
        internal int boundsPropagationKernel => m_BoundsPropagationKernel;
        internal int evaluationFlatKernel => m_EvaluationFlatKernel;
        internal int evaluationKernel => m_EvaluationKernel;
        internal int evaluationMaskKernel => m_EvaluationMaskKernel;
        internal int evaluationCurrentKernel => m_EvaluationCurrentKernel;
        internal int evaluationCurrentMaskKernel => m_EvaluationCurrentMaskKernel;
        internal bool IsValid => m_EffectMaterial != null
            && m_LineShader != null
            && m_EvaluationShader != null
            && m_ClearLineKernel >= 0
            && m_LineEvaluationKernel >= 0
            && m_BoundsPropagationKernel >= 0
            && m_EvaluationFlatKernel >= 0
            && m_EvaluationKernel >= 0
            && m_EvaluationMaskKernel >= 0
            && m_EvaluationCurrentKernel >= 0
            && m_EvaluationCurrentMaskKernel >= 0;

        internal bool Initialize(WaterResources resources)
        {
            Cleanup();
            if (resources == null || resources.UnderwaterEffect == null || resources.UnderwaterLine == null || resources.Evaluation == null)
                return false;

            m_EffectMaterial = CoreUtils.CreateEngineMaterial(resources.UnderwaterEffect);
            m_LineShader = resources.UnderwaterLine;
            m_EvaluationShader = resources.Evaluation;
            m_ClearLineKernel = m_LineShader.FindKernel("ClearWaterLine");
            m_LineEvaluationKernel = m_LineShader.FindKernel("LineEvaluation1D");
            m_BoundsPropagationKernel = m_LineShader.FindKernel("BoundsPropagation");
            m_EvaluationFlatKernel = m_EvaluationShader.FindKernel("FindVerticalDisplacementFlat");
            m_EvaluationKernel = m_EvaluationShader.FindKernel("FindVerticalDisplacement");
            m_EvaluationMaskKernel = m_EvaluationShader.FindKernel("FindVerticalDisplacementMask");
            m_EvaluationCurrentKernel = m_EvaluationShader.FindKernel("FindVerticalDisplacementCurrent");
            m_EvaluationCurrentMaskKernel = m_EvaluationShader.FindKernel("FindVerticalDisplacementCurrentMask");
            m_CausticsTexture = resources.Caustics;
            m_CausticsDistortionTexture = resources.CausticsDistortion;
            return IsValid;
        }

        internal void Cleanup()
        {
            CoreUtils.Destroy(m_EffectMaterial);
            m_EffectMaterial = null;
            m_LineShader = null;
            m_EvaluationShader = null;
            m_ClearLineKernel = -1;
            m_LineEvaluationKernel = -1;
            m_BoundsPropagationKernel = -1;
            m_EvaluationFlatKernel = -1;
            m_EvaluationKernel = -1;
            m_EvaluationMaskKernel = -1;
            m_EvaluationCurrentKernel = -1;
            m_EvaluationCurrentMaskKernel = -1;
            m_CausticsTexture = null;
            m_CausticsDistortionTexture = null;
        }

        internal int FindSurface(WaterSurface[] surfaces, int surfaceCount, WaterCameraContext cameraContext)
        {
            Camera camera = cameraContext.camera;
            if (!IsValid || camera == null)
                return -1;

            int bestIndex = -1;
            float bestAbsHeight = float.PositiveInfinity;
            for (int i = 0; i < surfaceCount; ++i)
            {
                WaterSurface surface = surfaces[i];
                if (surface == null || !surface.underwaterEnabled || !surface.IsInfinite() || surface.simulation == null)
                    continue;

                float height = cameraContext.GetSignedSurfaceHeight(
                    surface,
                    camera.transform.position);
                float conservativeHeight = height - surface.simulation.renderingParameters.maxWaveHeight;
                // The underwater volume is only needed when the camera can actually enter the
                // displaced surface envelope. The old fixed 2 m guard kept Line + Composite alive
                // for ordinary above-water cameras and cost a full screen scan every frame.
                float aboveSurfaceGuard = Mathf.Max(camera.nearClipPlane * 2.0f, 0.01f);
                if (conservativeHeight > aboveSurfaceGuard)
                    continue;

                float absHeight = Mathf.Abs(height);
                if (absHeight < bestAbsHeight)
                {
                    bestAbsHeight = absHeight;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static Vector2 GetScreenSpaceBounds(Vector2 direction, int width, int height)
        {
            float c0 = Vector2.Dot(direction, Vector2.zero);
            float c1 = Vector2.Dot(direction, new Vector2(0.0f, height));
            float c2 = Vector2.Dot(direction, new Vector2(width, 0.0f));
            float c3 = Vector2.Dot(direction, new Vector2(width, height));
            return new Vector2(
                Mathf.Min(Mathf.Min(c0, c1), Mathf.Min(c2, c3)),
                Mathf.Max(Mathf.Max(c0, c1), Mathf.Max(c2, c3)));
        }

        internal WaterLineParameters BuildWaterLineParameters(
            UniversalCameraData cameraData, WaterCameraContext cameraContext, WaterSurface surface, int width, int height)
        {
            Camera camera = cameraData.camera;
            width = Mathf.Max(width, 1);
            height = Mathf.Max(height, 1);
            Vector3 surfaceUp = cameraContext.GetSurfaceUp(surface);

            // Match the actual URP render projection (jitter + target orientation) used by the water GBuffer.
            Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
            Vector3 projectedUp = viewProjection.MultiplyVector(surfaceUp);
            if (projectedUp.sqrMagnitude < 0.001f)
                projectedUp.y = -1.0f;

            Vector2 upVector = new Vector2(projectedUp.x, -projectedUp.y);
            if (upVector.sqrMagnitude < 1e-8f)
                upVector = Vector2.up;
            else
                upVector.Normalize();

            Vector2 rightVector = new Vector2(upVector.y, -upVector.x);
            Vector2 rightBounds = GetScreenSpaceBounds(rightVector, width, height);
            Vector2 upBounds = GetScreenSpaceBounds(upVector, width, height);
            Vector4 boundsSS = new Vector4(rightBounds.x, rightBounds.y, upBounds.x, upBounds.y);

            int bufferStride = 2 + Mathf.CeilToInt(Mathf.Sqrt((float)width * width + (float)height * height));
            int reductionSize = Mathf.Max(1, CoreUtils.DivRoundUp(
                Mathf.CeilToInt(boundsSS.y - boundsSS.x),
                64));
            if (boundsSS.y <= boundsSS.x)
            {
                boundsSS.z = -1.0f;
                boundsSS.w = 1.0f;
            }

            float cameraHeight = cameraContext.GetSignedSurfaceHeight(
                surface,
                camera.transform.position);

            return new WaterLineParameters(
                boundsSS,
                upVector,
                new Vector4(width, height, 1.0f / width, 1.0f / height),
                bufferStride,
                reductionSize,
                cameraHeight,
                surfaceUp);
        }

        internal MaterialPropertyBlock BuildEffectProperties(
            Camera camera,
            WaterSurface surface,
            in WaterLineParameters lineParameters)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Vector3 extinction = surface.extinction;
            float submersionDepth = Mathf.Max(0.0f, -lineParameters.cameraHeight);
            Vector3 albedo = surface.EvaluateStylizedScatteringAlbedo(submersionDepth);
            Vector3 scattering = Vector3.Scale(extinction, albedo);
            Vector3 absorption = extinction - scattering;

            float minimumFogDensity = Mathf.Max(Mathf.Min(extinction.x, Mathf.Min(extinction.y, extinction.z)) * Mathf.Max(surface.underwaterExtinctionMultiplier, 0.0f), 0.0001f);
            float volumeExtinctionLength = -Mathf.Log(0.000001f) / minimumFogDensity;

            LightProbes.GetInterpolatedProbe(camera.transform.position, null, out SphericalHarmonicsL2 probe);
            m_AmbientDirection[0] = lineParameters.upDirectionWS;
            probe.Evaluate(m_AmbientDirection, m_AmbientLighting);
            Color ambient = m_AmbientLighting[0];

            properties.SetVector(WaterShaderIDs._WaterUnderwaterAbsorption, new Vector4(absorption.x, absorption.y, absorption.z, 0f));
            properties.SetVector(WaterShaderIDs._WaterUnderwaterScattering, new Vector4(scattering.x, scattering.y, scattering.z, 0f));
            properties.SetVector(WaterShaderIDs._WaterUnderwaterAmbientLighting, new Vector4(ambient.r, ambient.g, ambient.b, 0f));
            properties.SetVector(WaterShaderIDs._WaterUnderwaterBoundsSS, lineParameters.boundsSS);
            properties.SetVector(WaterShaderIDs._WaterUnderwaterUpDirection, new Vector4(lineParameters.upDirectionSS.x, lineParameters.upDirectionSS.y, 0f, 0f));
            properties.SetInt(WaterShaderIDs._WaterUnderwaterBufferStride, lineParameters.bufferStride);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterSurfaceLevel, surface.transform.position.y);
            Vector3 surfacePosition = surface.transform.position;
            Vector3 surfaceUpWS = lineParameters.upDirectionWS.normalized;
            properties.SetVector(WaterShaderIDs._WaterUnderwaterSurfacePositionWS,
                new Vector4(surfacePosition.x, surfacePosition.y, surfacePosition.z, 1.0f));
            properties.SetVector(WaterShaderIDs._WaterUnderwaterUpDirectionWS,
                new Vector4(surfaceUpWS.x, surfaceUpWS.y, surfaceUpWS.z, 0.0f));
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterVolumeExtinctionLength, volumeExtinctionLength);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterExtinctionMultiplier, surface.underwaterExtinctionMultiplier);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterSunBoost, surface.underwaterSunBoost);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterOutScatteringFactor, surface.underwaterOutScatteringFactor);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterOutScatteringExtinctionFactor, surface.underwaterOutScatteringExtinctionFactor);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterAnisotropy, surface.underwaterAnisotropy);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterDirectLightingMultiplier, surface.underwaterDirectLightingMultiplier);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterAmbientLightingMultiplier, surface.underwaterAmbientLightingMultiplier);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterAmbientShadowInfluence, surface.underwaterAmbientShadowInfluence);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterDitheringIntensity, surface.underwaterDitheringIntensity);
            properties.SetFloat(WaterShaderIDs._WaterUnderwaterDitheringEnabled, surface.underwaterDitheringEnabled ? 1f : 0f);
            properties.SetTexture(WaterShaderIDs._WaterCausticsTexture, m_CausticsTexture);
            properties.SetTexture(WaterShaderIDs._WaterCausticsDistortionTexture, m_CausticsDistortionTexture);
            properties.SetVector(WaterShaderIDs._WaterCausticsAppearanceParameters, new Vector4(
                surface.causticsEnabled ? Mathf.Max(surface.causticsStrength, 0.0f) : 0.0f,
                Mathf.Max(surface.causticsScale, 0.01f),
                Mathf.Max(surface.causticsScrollSpeed, 0.0f),
                Mathf.Clamp01(surface.causticsTextureAverage)));
            properties.SetFloat(WaterShaderIDs._WaterCausticsShadowDimmer, Mathf.Clamp01(surface.causticsDirectionalShadowDimmer));
            properties.SetVector(WaterShaderIDs._WaterCausticsProjectionParameters, new Vector4(
                Mathf.Max(surface.causticsFocalDepth, 0.0f),
                Mathf.Max(surface.causticsDepthOfField, 0.01f),
                Mathf.Clamp(surface.causticsDistortionStrength, 0.0f, 0.25f),
                Mathf.Max(surface.causticsDistortionScale, 0.01f)));
            return properties;
        }
    }
}
