using System;
using Aetheria.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal struct CloudLayerShaderData : IShaderValueSource
    {
        public Vector4 GlobalParams;
        public Vector4 SunDirection;
        public Vector4 LightParam;
        public Vector4 LightParamB;
        public Vector4 AmbientColor;
        public Vector4 AmbientColorB;
        public Vector4 AmbientProbeDimmers;
        public Vector4 AnimationParamA;
        public Vector4 AnimationParamB;

        public void WriteTo(ref ShaderValueWriter writer)
        {
            writer.SetVector(ShaderProperties.CloudLayer.GlobalParams, GlobalParams);
            writer.SetVector(ShaderProperties.CloudLayer.SunDirection, SunDirection);
            writer.SetVector(ShaderProperties.CloudLayer.LightParam, LightParam);
            writer.SetVector(ShaderProperties.CloudLayer.LightParamB, LightParamB);
            writer.SetVector(ShaderProperties.CloudLayer.AmbientColor, AmbientColor);
            writer.SetVector(ShaderProperties.CloudLayer.AmbientColorB, AmbientColorB);
            writer.SetVector(ShaderProperties.CloudLayer.AmbientProbeDimmers, AmbientProbeDimmers);
            writer.SetVector(ShaderProperties.CloudLayer.AnimationParamA, AnimationParamA);
            writer.SetVector(ShaderProperties.CloudLayer.AnimationParamB, AnimationParamB);
        }

        public void AppendHash(ref HashCode hash)
        {
            hash.Add(GlobalParams);
            hash.Add(SunDirection);
            hash.Add(LightParam);
            hash.Add(LightParamB);
            hash.Add(AmbientColor);
            hash.Add(AmbientColorB);
            hash.Add(AmbientProbeDimmers);
            hash.Add(AnimationParamA);
            hash.Add(AnimationParamB);
        }
    }

    internal sealed class CloudLayerFrameData : ContextItem
    {
        public bool IsActive;
        public TextureHandle Texture = TextureHandle.nullHandle;
        public RenderTexture TextureResource;
        public CloudLayerShaderData ShaderData;
        public int SettingsHash;

        public override void Reset()
        {
            IsActive = false;
            Texture = TextureHandle.nullHandle;
            TextureResource = null;
            ShaderData = default;
            SettingsHash = 0;
        }
    }
}