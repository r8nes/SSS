using Aetheria.Runtime.Rendering;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Water
{
    internal sealed class WaterSimulationPipeline
    {
        internal readonly RenderGraphExternalTextureCache m_TextureCache;
        private WaterResources m_Resources;
        private bool m_IsInitialized;
        private WaterSimulationResolution m_Resolution;

        internal WaterSimulationPipeline(RenderGraphExternalTextureCache textureCache)
        {
            m_TextureCache = textureCache;
        }

        internal bool Initialize(WaterResources resources, WaterSimulationResolution resolution)
        {
            m_Resources = resources;
            m_Resolution = resolution;
            m_IsInitialized = InitializeShaders();
            return m_IsInitialized;
        }

        internal void Cleanup()
        {
            m_IsInitialized = false;
            ReleaseShaders();
            m_Resources = null;
        }

        internal void Record(RenderGraph renderGraph, WaterSimulationFrame frame, WaterSurface[] surfaces, int surfaceCount,
            GraphicsBuffer currentSectorSwizzleBuffer)
        {
            frame.Initialize(surfaces, surfaceCount);
            WaterSpectralSimulationPass.Record(this, renderGraph, frame);
            WaterWaveMipGenerationPass.Record(renderGraph, frame);
            WaterFoamSimulationPass.Record(this, renderGraph, frame, currentSectorSwizzleBuffer);
            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                if (surfaceFrame.spectralUpdateScheduled && surfaceFrame.surface?.simulation != null)
                    surfaceFrame.surface.simulation.waveFieldUpdateRequired = false;
            }
        }

        internal ComputeShader m_SpectralSimulationShader;
        private int m_CollapseSpectrumTransitionKernel;
        private int m_InitializeDirectionalNormalizationKernel;
        private int m_InitializeDirectionalSpectrumKernel;
        private int m_EvaluateTotalDispersionKernel;
        private int m_UnpackWaveCascadeKernel;
        private int m_DirectionalNormalizationDispatchGroupsX;

        internal ComputeShader m_FoamSimulationShader;
        private int m_UpdateFoamKernel;
        private int m_UpdateFoamNoCurrentKernel;
        private int m_ReprojectFoamKernel;

        private enum FourierTransformBackend
        {
            SharedMemory,
            WaveOperations,
        }

        internal ComputeShader m_FourierTransformShader;
        private FourierTransformBackend m_FourierTransformBackend;
        private readonly int[] m_FftRowKernels = new int[WaterLimits.CascadeCount];
        private readonly int[] m_FftTransposeKernels = new int[WaterLimits.CascadeCount];
        private readonly int[] m_FftRowsPerGroup = new int[WaterLimits.CascadeCount];

        private bool InitializeShaders()
        {
            m_SpectralSimulationShader = m_Resources.SpectralSimulation;
            m_CollapseSpectrumTransitionKernel = m_SpectralSimulationShader.FindKernel("WaterCollapseSpectrumTransition");
            m_InitializeDirectionalNormalizationKernel = m_SpectralSimulationShader.FindKernel("WaterInitializeDirectionalNormalizationLut");
            m_InitializeDirectionalSpectrumKernel = m_SpectralSimulationShader.FindKernel("WaterInitializeDirectionalSpectrum");
            m_EvaluateTotalDispersionKernel = m_SpectralSimulationShader.FindKernel("WaterEvaluateTotalDispersion");
            m_UnpackWaveCascadeKernel = m_SpectralSimulationShader.FindKernel("WaterUnpackWaveCascade");
            m_SpectralSimulationShader.GetKernelThreadGroupSizes(
                m_InitializeDirectionalNormalizationKernel, out uint normalizationThreadsX, out _, out _);
            m_DirectionalNormalizationDispatchGroupsX = CoreUtils.DivRoundUp(
                WaterConstants.k_DirectionalNormalizationLutSize, (int)normalizationThreadsX);

            m_FoamSimulationShader = m_Resources.FoamSimulation;
            if (m_FoamSimulationShader != null)
            {
                m_UpdateFoamKernel = m_FoamSimulationShader.FindKernel("WaterUpdateFoam");
                m_UpdateFoamNoCurrentKernel = m_FoamSimulationShader.FindKernel("WaterUpdateFoamNoCurrent");
                m_ReprojectFoamKernel = m_FoamSimulationShader.FindKernel("WaterReprojectFoam");
            }

            GraphicsDeviceType graphicsDeviceType = SystemInfo.graphicsDeviceType;
            if (graphicsDeviceType == GraphicsDeviceType.Direct3D11)
            {
                m_FourierTransformBackend = FourierTransformBackend.SharedMemory;
                m_FourierTransformShader = m_Resources.FourierTransformSharedMemory;
            }
            else if (graphicsDeviceType == GraphicsDeviceType.Direct3D12 || graphicsDeviceType == GraphicsDeviceType.Vulkan)
            {
                m_FourierTransformBackend = FourierTransformBackend.WaveOperations;
                m_FourierTransformShader = m_Resources.FourierTransformWaveOperations;
            }
            else
            {
                Debug.LogError($"Water FFT supports Direct3D 11, Direct3D 12, and Vulkan. The active graphics API is {graphicsDeviceType}; water rendering has been disabled.");
                return false;
            }

            if (m_FourierTransformShader == null)
            {
                Debug.LogError($"Water FFT resource for {m_FourierTransformBackend} is missing; water rendering has been disabled.");
                return false;
            }

            try
            {
                int resolution = WaterSimulationLayout.GetCascadeResolution(m_Resolution);
                for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
                {
                    WaterSimulationLayout.GetFftKernels(m_FourierTransformShader, (WaterSimulationResolution)resolution,
                        out m_FftRowKernels[cascadeIndex], out m_FftTransposeKernels[cascadeIndex]);
                    m_FftRowsPerGroup[cascadeIndex] = m_FourierTransformBackend == FourierTransformBackend.SharedMemory
                        ? 512 / resolution
                        : resolution == 64 ? 2 : 1;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Water FFT kernels for {m_FourierTransformBackend} are unavailable: {exception.Message}. Water rendering has been disabled.");
                m_FourierTransformShader = null;
                return false;
            }

            for (int cascadeIndex = 0; cascadeIndex < WaterLimits.CascadeCount; ++cascadeIndex)
            {
                if (!m_FourierTransformShader.IsSupported(m_FftRowKernels[cascadeIndex])
                    || !m_FourierTransformShader.IsSupported(m_FftTransposeKernels[cascadeIndex]))
                {
                    Debug.LogError($"Water FFT kernels for {m_FourierTransformBackend} did not compile or are unsupported by the active graphics device; water rendering has been disabled.");
                    m_FourierTransformShader = null;
                    return false;
                }
            }

            if (m_FourierTransformBackend == FourierTransformBackend.WaveOperations
                && !WaterValidateWaveOperationsSupport(m_FourierTransformShader, out string validationFailure))
            {
                Debug.LogError(validationFailure + " Water rendering has been disabled.");
                m_FourierTransformShader = null;
                return false;
            }
            return true;
        }

        private static bool WaterValidateWaveOperationsSupport(ComputeShader fourierTransformShader, out string failureReason)
        {
            int validationKernel;
            try
            {
                validationKernel = fourierTransformShader.FindKernel("WaterValidateWaveOperations");
            }
            catch (Exception exception)
            {
                failureReason = $"The wave-operations FFT validation kernel is unavailable: {exception.Message}.";
                return false;
            }

            if (!fourierTransformShader.IsSupported(validationKernel))
            {
                failureReason = "The wave-operations FFT validation kernel did not compile or is unsupported by the active graphics device.";
                return false;
            }

            ComputeBuffer supportBuffer = null;
            try
            {
                supportBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
                uint[] supportValue = { 0u };
                supportBuffer.SetData(supportValue);
                fourierTransformShader.SetBuffer(validationKernel, WaterShaderIDs._WaveOperationsSupportResult, supportBuffer);
                fourierTransformShader.Dispatch(validationKernel, 1, 1, 1);
                supportBuffer.GetData(supportValue);
                if (supportValue[0] == 1u)
                {
                    failureReason = null;
                    return true;
                }
                failureReason = "Water FFT on Direct3D 12/Vulkan requires compute-shader subgroup operations with a native subgroup width of 32, 64, or 128 lanes. The active GPU/compiler variant does not satisfy this requirement.";
                return false;
            }
            catch (Exception exception)
            {
                failureReason = $"Water wave-operations FFT capability validation failed: {exception.Message}.";
                return false;
            }
            finally
            {
                supportBuffer?.Release();
            }
        }

        private void ReleaseShaders()
        {
            m_SpectralSimulationShader = null;
            m_FoamSimulationShader = null;
            m_FourierTransformShader = null;
        }

        internal int GetCascadeResolution() => WaterSimulationLayout.GetCascadeResolution(m_Resolution);

        private static bool HasValidWaveCascadeHandles(WaterSimulationFrame frame, int surfaceIndex, int cascadeCount)
        {
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                if (!frame.WaveField(surfaceIndex, cascadeIndex).IsValid())
                    return false;
            return true;
        }

        private static bool HasValidSpectrumHandles(WaterSimulationFrame frame, int surfaceIndex, int cascadeCount)
        {
            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                if (!frame.Spectrum(surfaceIndex, cascadeIndex).IsValid()
                    || !frame.PreviousSpectrum(surfaceIndex, cascadeIndex).IsValid())
                    return false;
            return true;
        }

        internal void DispatchSpectralSimulation(ComputeCommandBuffer cmd, WaterSpectralSimulationPass.PassData data)
        {
            if (!m_IsInitialized)
                return;
            WaterSimulationFrame frame = data.frame;
            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                WaterSurface surface = surfaceFrame.surface;
                if (surface == null || surface.simulation?.gpuResources == null)
                    continue;
                if (!surfaceFrame.spectralUpdateScheduled
                    || !HasValidWaveCascadeHandles(frame, surfaceIndex, surfaceFrame.cascadeCount)
                    || !HasValidSpectrumHandles(frame, surfaceIndex, surfaceFrame.cascadeCount)
                    || (!surface.simulation.currentSpectrumValid && !surfaceFrame.directionalNormalizationLut.IsValid()))
                    continue;
                DispatchSurfaceSpectrum(cmd, surface, data, surfaceIndex);
            }
        }

        internal void DispatchFoamSimulation(ComputeCommandBuffer cmd, WaterFoamSimulationPass.PassData data)
        {
            if (!m_IsInitialized)
                return;
            WaterSimulationFrame frame = data.frame;
            for (int surfaceIndex = 0; surfaceIndex < frame.surfaceCount; ++surfaceIndex)
            {
                ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
                WaterSurface surface = surfaceFrame.surface;
                if (!surfaceFrame.foamUpdateScheduled || surface?.simulation == null)
                    continue;
                if (!surfaceFrame.foamSource.IsValid() || !surfaceFrame.foamTarget.IsValid() || !surfaceFrame.foamCascades.IsValid())
                    continue;
                if (surfaceFrame.waveDrivenFoamUpdateRequired
                    && !HasValidWaveCascadeHandles(frame, surfaceIndex, surfaceFrame.cascadeCount))
                    continue;
                if (surfaceFrame.foamUsesCurrent
                    && (!data.currentSectorSwizzleBuffer.IsValid() || !surfaceFrame.currentMap.IsValid()))
                    continue;
                DispatchFoam(cmd, surface, data, surfaceIndex);
            }
        }

        private void DispatchInverseFFT(ComputeCommandBuffer cmd, WaterSimulationFrame frame, int cascadeIndex, int resolution)
        {
            int rowKernel = m_FftRowKernels[cascadeIndex];
            int transposeKernel = m_FftTransposeKernels[cascadeIndex];
            int rowGroupCount = resolution / m_FftRowsPerGroup[cascadeIndex];
            int transposeGroupCount = resolution / 16;
            TextureHandle spatialReal = frame.evolvedSpectrumReal[cascadeIndex];
            TextureHandle spatialImaginary = frame.evolvedSpectrumImaginary[cascadeIndex];
            TextureHandle rowReal = frame.fftRowReal[cascadeIndex];
            TextureHandle rowImaginary = frame.fftRowImaginary[cascadeIndex];

            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftRealInput, spatialReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftImaginaryInput, spatialImaginary);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftRealOutput, rowReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftImaginaryOutput, rowImaginary);
            cmd.DispatchCompute(m_FourierTransformShader, rowKernel, 1, rowGroupCount, 1);

            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftRealInput, rowReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftImaginaryInput, rowImaginary);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftRealOutput, spatialReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftImaginaryOutput, spatialImaginary);
            cmd.DispatchCompute(m_FourierTransformShader, transposeKernel, transposeGroupCount, transposeGroupCount, 1);

            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftRealInput, spatialReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftImaginaryInput, spatialImaginary);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftRealOutput, rowReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, rowKernel, WaterShaderIDs._FftImaginaryOutput, rowImaginary);
            cmd.DispatchCompute(m_FourierTransformShader, rowKernel, 1, rowGroupCount, 1);

            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftRealInput, rowReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftImaginaryInput, rowImaginary);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftRealOutput, spatialReal);
            cmd.SetComputeTextureParam(m_FourierTransformShader, transposeKernel, WaterShaderIDs._FftImaginaryOutput, spatialImaginary);
            cmd.DispatchCompute(m_FourierTransformShader, transposeKernel, transposeGroupCount, transposeGroupCount, 1);
        }

        private static void BindConstantBuffer(ComputeCommandBuffer cmd, ComputeShader shader, int id, GraphicsBuffer buffer)
        {
            cmd.SetComputeConstantBufferParam(shader, id, buffer, 0, buffer.stride);
        }

        private void DispatchSurfaceSpectrum(ComputeCommandBuffer cmd, WaterSurface surface,
            WaterSpectralSimulationPass.PassData data, int surfaceIndex)
        {
            WaterSimulationFrame frame = data.frame;
            ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
            WaterSimulationState simulation = surface.simulation;
            int cascadeCount = simulation.activeCascadeCount;
            int resolution = GetCascadeResolution();
            int dispatchGroupCount = CoreUtils.DivRoundUp(resolution, 8);
            BindConstantBuffer(cmd, m_SpectralSimulationShader, WaterShaderIDs._WaterSimulationShaderVariables,
                surface.simulationConstantBuffer);
            BindConstantBuffer(cmd, m_SpectralSimulationShader, WaterShaderIDs._WaterSpectrumShaderVariables,
                surface.spectrumConstantBuffer);
            cmd.SetComputeVectorParam(m_SpectralSimulationShader, WaterShaderIDs._CascadeWhitecap,
                surface.EvaluateFoamWhitecapParameters());
            cmd.SetComputeIntParam(m_SpectralSimulationShader, WaterShaderIDs._PreviousSpectrumValid,
                simulation.previousSpectrumValid ? 1 : 0);
            cmd.SetComputeFloatParam(m_SpectralSimulationShader, WaterShaderIDs._SpectrumUpdateDeltaTime,
                simulation.simulationDeltaTime);

            if (!simulation.currentSpectrumValid)
            {
                if (simulation.spectrumCollapsePending)
                {
                    cmd.SetComputeIntParam(m_SpectralSimulationShader, WaterShaderIDs._SpectrumCollapseRequiresNormalization,
                        simulation.spectrumCollapseRequiresNormalization ? 1 : 0);
                    cmd.SetComputeFloatParam(m_SpectralSimulationShader, WaterShaderIDs._SpectrumCollapseBlend,
                        simulation.spectrumCollapseBlend);
                    for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                    {
                        cmd.SetComputeIntParam(m_SpectralSimulationShader, WaterShaderIDs._CascadeIndex, cascadeIndex);
                        cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_CollapseSpectrumTransitionKernel,
                            WaterShaderIDs._Spectrum, frame.Spectrum(surfaceIndex, cascadeIndex));
                        cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_CollapseSpectrumTransitionKernel,
                            WaterShaderIDs._PreviousSpectrumRW, frame.PreviousSpectrum(surfaceIndex, cascadeIndex));
                        cmd.DispatchCompute(m_SpectralSimulationShader, m_CollapseSpectrumTransitionKernel,
                            dispatchGroupCount, dispatchGroupCount, 1);
                    }
                    simulation.spectrumCollapsePending = false;
                    simulation.spectrumCollapseRequiresNormalization = false;
                    simulation.spectrumCollapseBlend = 1.0f;
                }

                if (surfaceFrame.requiresDirectionalNormalizationUpdate)
                {
                    cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_InitializeDirectionalNormalizationKernel,
                        WaterShaderIDs._DirectionalNormalizationLutRW, surfaceFrame.directionalNormalizationLut);
                    cmd.DispatchCompute(m_SpectralSimulationShader, m_InitializeDirectionalNormalizationKernel,
                        m_DirectionalNormalizationDispatchGroupsX, WaterLimits.CascadeCount, 1);
                    simulation.CommitDirectionalNormalization();
                }

                for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                {
                    cmd.SetComputeIntParam(m_SpectralSimulationShader, WaterShaderIDs._CascadeIndex, cascadeIndex);
                    cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_InitializeDirectionalSpectrumKernel,
                        WaterShaderIDs._DirectionalNormalizationLut, surfaceFrame.directionalNormalizationLut);
                    cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_InitializeDirectionalSpectrumKernel,
                        WaterShaderIDs._SpectrumRW, frame.Spectrum(surfaceIndex, cascadeIndex));
                    cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_InitializeDirectionalSpectrumKernel,
                        WaterShaderIDs._PreviousSpectrum, frame.PreviousSpectrum(surfaceIndex, cascadeIndex));
                    cmd.DispatchCompute(m_SpectralSimulationShader, m_InitializeDirectionalSpectrumKernel,
                        dispatchGroupCount, dispatchGroupCount, 1);
                }
                simulation.currentSpectrumValid = true;
            }

            for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
            {
                cmd.SetComputeIntParam(m_SpectralSimulationShader, WaterShaderIDs._CascadeIndex, cascadeIndex);
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_EvaluateTotalDispersionKernel,
                    WaterShaderIDs._SpectrumRW, frame.Spectrum(surfaceIndex, cascadeIndex));
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_EvaluateTotalDispersionKernel,
                    WaterShaderIDs._PreviousSpectrumRW, frame.PreviousSpectrum(surfaceIndex, cascadeIndex));
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_EvaluateTotalDispersionKernel,
                    WaterShaderIDs._EvolvedSpectrumRealOutput, frame.evolvedSpectrumReal[cascadeIndex]);
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_EvaluateTotalDispersionKernel,
                    WaterShaderIDs._EvolvedSpectrumImaginaryOutput, frame.evolvedSpectrumImaginary[cascadeIndex]);
                cmd.DispatchCompute(m_SpectralSimulationShader, m_EvaluateTotalDispersionKernel,
                    dispatchGroupCount, dispatchGroupCount, 1);

                DispatchInverseFFT(cmd, frame, cascadeIndex, resolution);

                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_UnpackWaveCascadeKernel,
                    WaterShaderIDs._SpatialWaveFieldRealInput, frame.evolvedSpectrumReal[cascadeIndex]);
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_UnpackWaveCascadeKernel,
                    WaterShaderIDs._SpatialWaveFieldImaginaryInput, frame.evolvedSpectrumImaginary[cascadeIndex]);
                cmd.SetComputeTextureParam(m_SpectralSimulationShader, m_UnpackWaveCascadeKernel,
                    WaterShaderIDs._WaterWaveField, frame.WaveField(surfaceIndex, cascadeIndex));
                cmd.DispatchCompute(m_SpectralSimulationShader, m_UnpackWaveCascadeKernel,
                    dispatchGroupCount, dispatchGroupCount, 1);
            }
        }

        private void DispatchFoam(ComputeCommandBuffer cmd, WaterSurface surface,
            WaterFoamSimulationPass.PassData data, int surfaceIndex)
        {
            WaterSimulationFrame frame = data.frame;
            ref WaterSurfaceSimulationFrame surfaceFrame = ref frame.surfaces[surfaceIndex];
            WaterSimulationState simulation = surface.simulation;
            BindConstantBuffer(cmd, m_FoamSimulationShader, WaterShaderIDs._WaterSimulationShaderVariables,
                surface.simulationConstantBuffer);

            int dispatchGroupCount = CoreUtils.DivRoundUp(simulation.foam.resolution, 8);
            int lodCount = Mathf.Max(simulation.foam.lodCount, 1);
            int kernel = !surfaceFrame.waveDrivenFoamUpdateRequired
                ? m_ReprojectFoamKernel
                : surfaceFrame.foamUsesCurrent ? m_UpdateFoamKernel : m_UpdateFoamNoCurrentKernel;

            cmd.SetComputeBufferParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterFoamCascades,
                surfaceFrame.foamCascades);
            cmd.SetComputeVectorParam(m_FoamSimulationShader, WaterShaderIDs._CascadeWhitecap,
                surface.EvaluateFoamWhitecapParameters());
            cmd.SetComputeVectorParam(m_FoamSimulationShader, WaterShaderIDs._CascadeFoamAmount,
                surface.EvaluateFoamAmountParameters());

            if (surfaceFrame.waveDrivenFoamUpdateRequired)
            {
                cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterWaveFieldCascade0,
                    frame.WaveField(surfaceIndex, 0));
                cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterWaveFieldCascade1,
                    frame.WaveField(surfaceIndex, 1));
                cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterWaveFieldCascade2,
                    frame.WaveField(surfaceIndex, 2));
                if (surfaceFrame.foamUsesCurrent)
                {
                    cmd.SetComputeBufferParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterCurrentSectorSwizzles,
                        data.currentSectorSwizzleBuffer);
                    cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._WaterCurrentMap,
                        surfaceFrame.currentMap);
                }
            }

            int substepCount = Mathf.Max(surfaceFrame.foamSubstepCount, 1);
            for (int substep = 0; substep < substepCount; ++substep)
            {
                bool isFirstSubstep = substep == 0;
                TextureHandle source;
                TextureHandle target;
                if (isFirstSubstep)
                {
                    source = surfaceFrame.foamSource;
                    target = surfaceFrame.foamTarget;
                }
                else if ((substep & 1) != 0)
                {
                    source = surfaceFrame.foamTarget;
                    target = surfaceFrame.foamSource;
                }
                else
                {
                    source = surfaceFrame.foamSource;
                    target = surfaceFrame.foamTarget;
                }

                cmd.SetComputeIntParam(m_FoamSimulationShader, WaterShaderIDs._FoamSourceValid,
                    isFirstSubstep ? (surfaceFrame.foamSourceValid ? 1 : 0) : 1);
                cmd.SetComputeIntParam(m_FoamSimulationShader, WaterShaderIDs._FoamSourceIsCurrent,
                    isFirstSubstep ? 0 : 1);
                cmd.SetComputeIntParam(m_FoamSimulationShader, WaterShaderIDs._FoamLodScaleShift,
                    isFirstSubstep ? surfaceFrame.foamLodScaleShift : 0);
                cmd.SetComputeIntParam(m_FoamSimulationShader, WaterShaderIDs._FoamPrewarmingRequired,
                    isFirstSubstep && surfaceFrame.foamPrewarmingRequired ? 1 : 0);
                cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._FoamSource, source);
                cmd.SetComputeTextureParam(m_FoamSimulationShader, kernel, WaterShaderIDs._FoamTarget, target);
                cmd.DispatchCompute(m_FoamSimulationShader, kernel, dispatchGroupCount, dispatchGroupCount, lodCount);
            }
        }
    }
}
