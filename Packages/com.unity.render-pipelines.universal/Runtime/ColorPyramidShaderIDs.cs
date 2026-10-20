namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Global shader properties for the renderer-wide Gaussian color pyramid.
    /// The pyramid is published immediately before the regular transparent forward pass.
    /// </summary>
    public static class ColorPyramidShaderIDs
    {
        public static readonly int CameraColorPyramidTexture = Shader.PropertyToID("_CameraColorPyramidTexture");
        public static readonly int CameraColorPyramidMipCount = Shader.PropertyToID("_CameraColorPyramidMipCount");
        public static readonly int CameraColorPyramidMaxMip = Shader.PropertyToID("_CameraColorPyramidMaxMip");
        public static readonly int CameraColorPyramidMip0Size = Shader.PropertyToID("_CameraColorPyramidMip0Size");
        public static readonly int CameraColorPyramidUvScaleAndLimit = Shader.PropertyToID("_CameraColorPyramidUvScaleAndLimit");
        public static readonly int SSRefractionInvScreenWeightDistance = Shader.PropertyToID("_SSRefractionInvScreenWeightDistance");
        public static readonly int EnableSSRefraction = Shader.PropertyToID("_EnableSSRefraction");
    }
}
