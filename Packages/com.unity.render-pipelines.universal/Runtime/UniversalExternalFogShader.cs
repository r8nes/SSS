using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    public static class UniversalExternalFogShader
    {
        public static readonly int Enabled = Shader.PropertyToID("_UniversalExternalFogEnabled");
        public static readonly int VolumeEnabled = Shader.PropertyToID("_UniversalExternalFogVolumeEnabled");
        public static readonly int Texture = Shader.PropertyToID("_UniversalExternalFogTexture");
        public static readonly int LastSliceDistance = Shader.PropertyToID("_UniversalExternalFogLastSliceDistance");
        public static readonly int GridParams = Shader.PropertyToID("_UniversalExternalFogGridParams");
        public static readonly int DepthEncodingParams = Shader.PropertyToID("_UniversalExternalFogDepthEncodingParams");
        public static readonly int VBufferViewportSize = Shader.PropertyToID("_UniversalExternalFogVBufferViewportSize");
        public static readonly int UseBiquadraticReconstruction = Shader.PropertyToID("_UniversalExternalFogUseBiquadraticReconstruction");
        public static readonly int Color = Shader.PropertyToID("_UniversalExternalFogColor");
        public static readonly int MipParameters = Shader.PropertyToID("_UniversalExternalFogMipParameters");
        public static readonly int ConvolutionMipCount = Shader.PropertyToID("_UniversalExternalFogConvolutionMipCount");
        public static readonly int SkyColorEnabled = Shader.PropertyToID("_UniversalExternalFogSkyColorEnabled");
        public static readonly int BaseExtinction = Shader.PropertyToID("_UniversalExternalFogBaseExtinction");
        public static readonly int BaseScattering = Shader.PropertyToID("_UniversalExternalFogBaseScattering");
        public static readonly int BaseHeight = Shader.PropertyToID("_UniversalExternalFogBaseHeight");
        public static readonly int HeightExponents = Shader.PropertyToID("_UniversalExternalFogHeightExponents");
        public static readonly int PlanetUpAltitude = Shader.PropertyToID("_UniversalExternalFogPlanetUpAltitude");
        public static readonly int FilteredSky = Shader.PropertyToID("_UniversalExternalFogFilteredSky");
        public static readonly int MediumEnabled = Shader.PropertyToID("_UniversalExternalMediumEnabled");
        public static readonly int MediumCameraInside = Shader.PropertyToID("_UniversalExternalMediumCameraInside");
        public static readonly int MediumExtinction = Shader.PropertyToID("_UniversalExternalMediumExtinction");
        public static readonly int MediumScatteringAlbedo = Shader.PropertyToID("_UniversalExternalMediumScatteringAlbedo");
    }
}
