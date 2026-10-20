using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Utils
{
    internal interface IShaderValueSource
    {
        void WriteTo(ref ShaderValueWriter writer);
    }

    internal struct ShaderValueWriter
    {
        private enum Target
        {
            Compute,
            Material,
            MaterialPropertyBlock,
            Globals
        }

        private readonly Target _target;
        private readonly ComputeCommandBuffer _computeCommand;
        private readonly RasterCommandBuffer _rasterCommand;
        private readonly ComputeShader _shader;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties;

        private ShaderValueWriter(ComputeCommandBuffer command, ComputeShader shader)
        {
            _target = Target.Compute;
            _computeCommand = command;
            _rasterCommand = null;
            _shader = shader;
            _material = null;
            _properties = null;
        }

        private ShaderValueWriter(Material material)
        {
            _target = Target.Material;
            _computeCommand = null;
            _rasterCommand = null;
            _shader = null;
            _material = material;
            _properties = null;
        }

        private ShaderValueWriter(MaterialPropertyBlock properties)
        {
            _target = Target.MaterialPropertyBlock;
            _computeCommand = null;
            _rasterCommand = null;
            _shader = null;
            _material = null;
            _properties = properties;
        }

        private ShaderValueWriter(RasterCommandBuffer command)
        {
            _target = Target.Globals;
            _computeCommand = null;
            _rasterCommand = command;
            _shader = null;
            _material = null;
            _properties = null;
        }

        public static ShaderValueWriter ForCompute(ComputeCommandBuffer command, ComputeShader shader) =>
            new(command, shader);

        public static ShaderValueWriter ForMaterial(Material material) => new(material);
        public static ShaderValueWriter ForMaterialPropertyBlock(MaterialPropertyBlock properties) => new(properties);
        public static ShaderValueWriter ForGlobals(RasterCommandBuffer command) => new(command);

        public void Write<T>(T source) where T : struct, IShaderValueSource => source.WriteTo(ref this);

        public void SetFloat(int nameId, float value)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeFloatParam(_shader, nameId, value); break;
                case Target.Material: _material.SetFloat(nameId, value); break;
                case Target.MaterialPropertyBlock: _properties.SetFloat(nameId, value); break;
                default: _rasterCommand.SetGlobalFloat(nameId, value); break;
            }
        }

        public void SetInt(int nameId, int value)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeIntParam(_shader, nameId, value); break;
                case Target.Material: _material.SetInt(nameId, value); break;
                case Target.MaterialPropertyBlock: _properties.SetInt(nameId, value); break;
                default: _rasterCommand.SetGlobalInt(nameId, value); break;
            }
        }

        public void SetColor(int nameId, Color value)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeVectorParam(_shader, nameId, value); break;
                case Target.Material: _material.SetColor(nameId, value); break;
                case Target.MaterialPropertyBlock: _properties.SetColor(nameId, value); break;
                default: _rasterCommand.SetGlobalColor(nameId, value); break;
            }
        }

        public void SetVector(int nameId, Vector4 value)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeVectorParam(_shader, nameId, value); break;
                case Target.Material: _material.SetVector(nameId, value); break;
                case Target.MaterialPropertyBlock: _properties.SetVector(nameId, value); break;
                default: _rasterCommand.SetGlobalVector(nameId, value); break;
            }
        }

        public void SetMatrix(int nameId, Matrix4x4 value)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeMatrixParam(_shader, nameId, value); break;
                case Target.Material: _material.SetMatrix(nameId, value); break;
                case Target.MaterialPropertyBlock: _properties.SetMatrix(nameId, value); break;
                default: _rasterCommand.SetGlobalMatrix(nameId, value); break;
            }
        }

        public void SetVectorArray(int nameId, Vector4[] values)
        {
            switch (_target)
            {
                case Target.Compute: _computeCommand.SetComputeVectorArrayParam(_shader, nameId, values); break;
                case Target.Material: _material.SetVectorArray(nameId, values); break;
                case Target.MaterialPropertyBlock: _properties.SetVectorArray(nameId, values); break;
                default: _rasterCommand.SetGlobalVectorArray(nameId, values); break;
            }
        }
    }
}