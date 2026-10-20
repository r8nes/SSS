using System;
using System.Collections.Generic;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal sealed class EnvironmentCubemapConditioner : IDisposable
    {
        // The first two mips retain separate desktop and mobile sample sequences.
        private const int SamplesPerMip = 68;
        private const int MobileSampleOffset = 34;

        private static readonly AetheriaProfiler.Marker PyramidProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Build Radiance Pyramid");

        private static readonly AetheriaProfiler.Marker PrefilterProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Prefilter Cubemap");

        private static readonly AetheriaProfiler.Marker PublishProfile =
            AetheriaProfiler.CreateMarker("Environment/Lighting", "Publish Cubemap");

        private sealed class PyramidPassData
        {
            public TextureHandle Capture;
            public TextureHandle Radiance;
        }

        private sealed class PrefilterPassData
        {
            public TextureHandle Radiance;
            public TextureHandle Filtered;
            public BufferHandle Samples;
            public ComputeShader Shader;
            public int Kernel;
            public int Resolution;
        }

        private sealed class PublishPassData
        {
            public TextureHandle Radiance;
            public TextureHandle Filtered;
            public TextureHandle Lighting;
        }

        private readonly ComputeShader _shader;
        private readonly int _kernel;
        private readonly Dictionary<int, GraphicsBuffer> _sampleBuffers = new();

        public EnvironmentCubemapConditioner(ComputeShader shader)
        {
            _shader = shader;
            _kernel = shader.FindKernel("ConvolveCubemap");
        }

        public void Dispose()
        {
            foreach (GraphicsBuffer buffer in _sampleBuffers.Values)
                buffer.Dispose();
            _sampleBuffers.Clear();
        }

        public bool Prepare(
            TextureHandle radiance,
            int resolution,
            GraphicsFormat format,
            int mipCount)
        {
            if (!radiance.IsValid() || resolution <= 0)
                return false;

            int requiredMipCount = CoreUtils.GetMipCount(resolution);
            if (mipCount < requiredMipCount)
            {
                Debug.LogWarning(
                    $"Aetheria environment conditioning requires a complete radiance mip chain " +
                    $"({requiredMipCount} levels for {resolution}x{resolution}), but the resource has {mipCount}.");
                return false;
            }

            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore))
            {
                Debug.LogWarning(
                    $"Aetheria environment conditioning cannot write format {format} from compute.");
                return false;
            }

            return true;
        }

        public void RecordPrepared(
            RenderGraph graph,
            TextureHandle capture,
            TextureHandle radiance,
            TextureHandle lighting,
            int resolution,
            GraphicsFormat format)
        {
            Debug.Assert(capture.IsValid());
            Debug.Assert(lighting.IsValid());
            Debug.Assert(radiance.IsValid());

            var filtered = CreateFilteredTarget(graph, resolution, format);

            RecordRadiancePyramid(graph, capture, radiance);
            RecordPrefilter(graph, radiance, filtered, resolution);
            RecordPublish(graph, radiance, filtered, lighting);
        }

        private static void RecordRadiancePyramid(
            RenderGraph graph,
            TextureHandle capture,
            TextureHandle radiance)
        {
            using var profiledPass = PyramidProfile.AddUnsafePass<PyramidPassData>(
                graph,
                out var pass);

            var builder = profiledPass.Builder;
            pass.Capture = capture;
            pass.Radiance = radiance;
            builder.UseTexture(pass.Capture);
            builder.UseTexture(pass.Radiance, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (PyramidPassData data, UnsafeGraphContext context) =>
            {
                RTHandle captureHandle = data.Capture;
                RTHandle radianceHandle = data.Radiance;
                var captureTexture = captureHandle.rt;
                var radianceTexture = radianceHandle.rt;
                var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                for (int face = 0; face < 6; ++face)
                {
                    command.CopyTexture(
                        captureTexture,
                        face,
                        0,
                        radianceTexture,
                        face,
                        0);
                }

                command.GenerateMips(radianceTexture);
            });
        }

        private void RecordPrefilter(
            RenderGraph graph,
            TextureHandle radiance,
            TextureHandle filtered,
            int resolution)
        {
            using var profiledPass = PrefilterProfile.AddComputePass<PrefilterPassData>(
                graph,
                out var pass);

            var builder = profiledPass.Builder;
            pass.Radiance = radiance;
            pass.Filtered = filtered;
            pass.Samples = graph.ImportBuffer(GetSamples(resolution));
            pass.Shader = _shader;
            pass.Kernel = _kernel;
            pass.Resolution = resolution;

            builder.UseTexture(pass.Radiance);
            builder.UseTexture(pass.Filtered, AccessFlags.Write);
            builder.UseBuffer(pass.Samples);

            builder.SetRenderFunc(static (PrefilterPassData data, ComputeGraphContext context) =>
            {
                var writer = ShaderValueWriter.ForCompute(context.cmd, data.Shader);
                context.cmd.SetComputeBufferParam(
                    data.Shader,
                    data.Kernel,
                    ShaderProperties.EnvironmentFilter.Samples,
                    data.Samples);

                for (int mip = 1; mip < EnvironmentConstants.ConvolutionMipCount; ++mip)
                {
                    int kernel = data.Kernel;
                    int mipResolution = Mathf.Max(1, data.Resolution >> mip);

                    context.cmd.SetComputeTextureParam(
                        data.Shader,
                        kernel,
                        ShaderProperties.EnvironmentFilter.MainTexture,
                        data.Radiance);
                    writer.SetInt(ShaderProperties.EnvironmentFilter.MipLevel, mip);
                    writer.SetInt(
                        ShaderProperties.EnvironmentFilter.MipResolution,
                        mipResolution);
                    context.cmd.SetComputeTextureParam(
                        data.Shader,
                        kernel,
                        ShaderProperties.EnvironmentFilter.Output,
                        data.Filtered,
                        mip - 1);
                    context.cmd.DispatchCompute(
                        data.Shader,
                        kernel,
                        CoreUtils.DivRoundUp(mipResolution, 8),
                        CoreUtils.DivRoundUp(mipResolution, 8),
                        6);
                }
            });
        }

        private GraphicsBuffer GetSamples(int resolution)
        {
            if (_sampleBuffers.TryGetValue(resolution, out GraphicsBuffer buffer))
                return buffer;

            var data = new Vector4[
                EnvironmentConstants.ConvolutionMipCountMinusOne * SamplesPerMip * 2];
            float invTexelSolidAngle = 6.0f * resolution * resolution / (4.0f * Mathf.PI);

            for (int mip = 1; mip < EnvironmentConstants.ConvolutionMipCount; ++mip)
            {
                int offset = (mip - 1) * SamplesPerMip;
                WriteSamples(data, offset, mip, GetDesktopSampleCount(mip), invTexelSolidAngle);
                if (mip <= 2)
                    WriteSamples(
                        data,
                        offset + MobileSampleOffset,
                        mip,
                        mip == 1 ? 21 : 34,
                        invTexelSolidAngle);
            }

            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, 4 * sizeof(float));
            buffer.SetData(data);
            _sampleBuffers.Add(resolution, buffer);
            return buffer;
        }

        private static int GetDesktopSampleCount(int mip) => mip switch
        {
            1 => 13,
            2 => 21,
            _ => 34
        };

        private static int GetPreviousFibonacciNumber(int sampleCount) => sampleCount switch
        {
            13 => 8,
            21 => 13,
            _ => 21
        };

        private static void WriteSamples(
            Vector4[] data,
            int offset,
            int mip,
            int sampleCount,
            float invTexelSolidAngle)
        {
            float perceptualRoughness = Mathf.Clamp01(
                1.7f / 1.4f - Mathf.Sqrt(
                    2.89f / 1.96f - (2.8f / 1.96f) *
                    (float)mip / EnvironmentConstants.ConvolutionMipCountMinusOne));
            float roughness = perceptualRoughness * perceptualRoughness;
            float roughnessSquared = roughness * roughness;
            float sequenceStep = (float)GetPreviousFibonacciNumber(sampleCount) / sampleCount;
            float weightSum = 0.0f;

            for (int i = 0; i < sampleCount; ++i)
            {
                float u = (i + 0.5f) / sampleCount;
                float cosTheta = Mathf.Sqrt(
                    (1.0f - u) / (1.0f + (roughnessSquared - 1.0f) * u));
                float sinTheta = Mathf.Sqrt(Mathf.Clamp01(1.0f - cosTheta * cosTheta));
                float phi = 2.0f * Mathf.PI * Mathf.Repeat(i * sequenceStep, 1.0f);
                float nDotL = 2.0f * cosTheta * cosTheta - 1.0f;
                if (nDotL <= 0.0f)
                    continue;

                float denominator = 1.0f + (roughnessSquared - 1.0f) * cosTheta * cosTheta;
                float distribution = roughnessSquared /
                                     (Mathf.PI * denominator * denominator);
                float pdf = 0.25f * distribution;
                float omegaS = 1.0f / (sampleCount * pdf);
                float sourceMip = 0.5f * Mathf.Log(omegaS * invTexelSolidAngle, 2.0f) +
                                  roughness;
                float lambdaL = Mathf.Sqrt(
                    (nDotL * (1.0f - roughnessSquared)) * nDotL + roughnessSquared);
                float weight = 0.5f * nDotL / (nDotL + lambdaL);

                int index = 2 * (offset + i);
                float reflectedSin = 2.0f * cosTheta * sinTheta;
                data[index] = new Vector4(
                    reflectedSin * Mathf.Cos(phi),
                    reflectedSin * Mathf.Sin(phi),
                    nDotL,
                    sourceMip);
                data[index + 1] = new Vector4(weight, 0.0f, 0.0f, 0.0f);
                weightSum += weight;
            }

            for (int i = 0; i < sampleCount; ++i)
            {
                int index = 2 * (offset + i) + 1;
                Vector4 sampleWeight = data[index];
                sampleWeight.x /= weightSum;
                data[index] = sampleWeight;
            }
        }

        private static void RecordPublish(
            RenderGraph graph,
            TextureHandle radiance,
            TextureHandle filtered,
            TextureHandle lighting)
        {
            using var profiledPass = PublishProfile.AddUnsafePass<PublishPassData>(
                graph,
                out var pass);

            var builder = profiledPass.Builder;
            pass.Radiance = radiance;
            pass.Filtered = filtered;
            pass.Lighting = lighting;
            builder.UseTexture(pass.Radiance);
            builder.UseTexture(pass.Filtered);
            builder.UseTexture(pass.Lighting, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PublishPassData data, UnsafeGraphContext context) =>
            {
                RTHandle radianceHandle = data.Radiance;
                RTHandle filteredHandle = data.Filtered;
                RTHandle lightingHandle = data.Lighting;
                var radianceTexture = radianceHandle.rt;
                var filteredTexture = filteredHandle.rt;
                var lightingTexture = lightingHandle.rt;
                var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                for (int face = 0; face < 6; ++face)
                {
                    command.CopyTexture(
                        radianceTexture,
                        face,
                        0,
                        lightingTexture,
                        face,
                        0);
                }

                for (int mip = 1; mip < EnvironmentConstants.ConvolutionMipCount; ++mip)
                {
                    int filteredMip = mip - 1;
                    for (int face = 0; face < 6; ++face)
                    {
                        command.CopyTexture(
                            filteredTexture,
                            face,
                            filteredMip,
                            lightingTexture,
                            face,
                            mip);
                    }
                }
            });
        }

        private static TextureHandle CreateFilteredTarget(
            RenderGraph graph,
            int resolution,
            GraphicsFormat format)
        {
            int filteredResolution = Mathf.Max(1, resolution >> 1);
            return graph.CreateTexture(new TextureDesc(filteredResolution, filteredResolution)
            {
                name = "Aetheria Lighting Sky Prefilter",
                dimension = TextureDimension.Tex2DArray,
                slices = 6,
                format = format,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = false,
                discardBuffer = true
            });
        }

    }
}
