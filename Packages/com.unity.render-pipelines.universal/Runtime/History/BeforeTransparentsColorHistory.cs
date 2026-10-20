namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Color history before rendering transparent objects. Raw rendered geometry. No UI overlay.
    /// Color space is linear RGB.
    /// Includes a consumer-requested Gaussian mip chain when required.
    /// MSAA is not supported and is resolved for the history.
    /// XR is supported.
    /// </summary>
    public sealed class BeforeTransparentsColorHistory : ColorHistory
    {
        /// <inheritdoc />
        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            m_Names[0] = "BeforeTransparentsColorHistory0";
            m_Names[1] = "BeforeTransparentsColorHistory1";
            base.OnCreate(owner, typeId);
        }
    }
}
