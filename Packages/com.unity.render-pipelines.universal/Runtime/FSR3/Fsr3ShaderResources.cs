using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FidelityFX.FSR3
{
    /// <summary>
    /// Compute shader set used by one FSR 3.1 context.
    /// </summary>
    internal sealed class Fsr3UpscalerShaders : IDisposable
    {
        public ComputeShader prepareInputsPass;
        public ComputeShader lumaPyramidPass;
        public ComputeShader shadingChangePyramidPass;
        public ComputeShader shadingChangePass;
        public ComputeShader prepareReactivityPass;
        public ComputeShader lumaInstabilityPass;
        public ComputeShader accumulatePass;
        public ComputeShader sharpenPass;
        public ComputeShader autoGenReactivePass;
        public ComputeShader debugViewPass;

        internal Fsr3UpscalerShaders CloneForContext()
        {
            return new Fsr3UpscalerShaders
            {
                prepareInputsPass = Object.Instantiate(prepareInputsPass),
                lumaPyramidPass = Object.Instantiate(lumaPyramidPass),
                shadingChangePyramidPass = Object.Instantiate(shadingChangePyramidPass),
                shadingChangePass = Object.Instantiate(shadingChangePass),
                prepareReactivityPass = Object.Instantiate(prepareReactivityPass),
                lumaInstabilityPass = Object.Instantiate(lumaInstabilityPass),
                accumulatePass = Object.Instantiate(accumulatePass),
                sharpenPass = Object.Instantiate(sharpenPass),
                autoGenReactivePass = Object.Instantiate(autoGenReactivePass),
                debugViewPass = debugViewPass != null ? Object.Instantiate(debugViewPass) : null,
            };
        }

        public void Dispose()
        {
            CoreUtils.Destroy(prepareInputsPass);
            CoreUtils.Destroy(lumaPyramidPass);
            CoreUtils.Destroy(shadingChangePyramidPass);
            CoreUtils.Destroy(shadingChangePass);
            CoreUtils.Destroy(prepareReactivityPass);
            CoreUtils.Destroy(lumaInstabilityPass);
            CoreUtils.Destroy(accumulatePass);
            CoreUtils.Destroy(sharpenPass);
            CoreUtils.Destroy(autoGenReactivePass);
            CoreUtils.Destroy(debugViewPass);
        }
    }
}
