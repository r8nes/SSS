using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Rendering.Passes
{
    internal sealed class CelestialDiskRenderer : IDisposable
    {
        private readonly Material _material;

        internal Material Material => _material;

        internal CelestialDiskRenderer(Shader shader)
        {
            _material = CoreUtils.CreateEngineMaterial(shader);
        }

        public void Dispose() => CoreUtils.Destroy(_material);
    }
}
