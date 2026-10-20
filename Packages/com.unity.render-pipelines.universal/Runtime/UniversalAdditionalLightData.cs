using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Contains extension methods for Light class.
    /// </summary>
    public static class LightExtensions
    {
        /// <summary>
        /// Universal Render Pipeline exposes additional light data in a separate component.
        /// This method returns the additional data component for the given light or create one if it doesn't exist yet.
        /// </summary>
        /// <param name="light"></param>
        /// <returns>The <c>UniversalAdditionalLightData</c> for this light.</returns>
        /// <see cref="UniversalAdditionalLightData"/>
        public static UniversalAdditionalLightData GetUniversalAdditionalLightData(this Light light)
        {
            var gameObject = light.gameObject;
            bool componentExists = gameObject.TryGetComponent<UniversalAdditionalLightData>(out var lightData);
            if (!componentExists)
                lightData = gameObject.AddComponent<UniversalAdditionalLightData>();

            return lightData;
        }
    }

    /// <summary>
    /// Class containing various additional light data used by URP.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [URPHelpURL("universal-additional-light-data")]
    public partial class UniversalAdditionalLightData : MonoBehaviour, ISerializationCallbackReceiver, IAdditionalData
    {
        [Tooltip("Controls if light Shadow Bias parameters use pipeline settings.")]
        [SerializeField] bool m_UsePipelineSettings = true;

        /// <summary>
        /// Controls if light Shadow Bias parameters use pipeline settings or not.
        /// </summary>
        public bool usePipelineSettings
        {
            get { return m_UsePipelineSettings; }
            set { m_UsePipelineSettings = value; }
        }

        /// <summary>
        /// Value used to indicate custom shadow resolution tier for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowResolutionTierCustom = -1;

        /// <summary>
        /// Value used to indicate low shadow resolution tier for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowResolutionTierLow = 0;

        /// <summary>
        /// Value used to indicate medium shadow resolution tier for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowResolutionTierMedium = 1;

        /// <summary>
        /// Value used to indicate high shadow resolution tier for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowResolutionTierHigh = 2;

        /// <summary>
        /// The default shadow resolution tier for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowDefaultResolutionTier = AdditionalLightsShadowResolutionTierHigh;

        /// <summary>
        /// The default custom shadow resolution for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowDefaultCustomResolution = 128;

        [NonSerialized] private Light m_Light;

        /// <summary>
        /// Returns the cached light component associated with the game object that owns this light data.
        /// </summary>
#if UNITY_EDITOR
        internal new Light light
#else
        internal Light light
#endif
        {
            get
            {
                if (!m_Light)
                    TryGetComponent(out m_Light);
                return m_Light;
            }
        }

        /// <summary>
        /// The minimum shadow resolution for additional lights.
        /// </summary>
        public static readonly int AdditionalLightsShadowMinimumResolution = 128;

        [Tooltip("Controls if light shadow resolution uses pipeline settings.")]
        [SerializeField] int m_AdditionalLightsShadowResolutionTier = AdditionalLightsShadowDefaultResolutionTier;

        /// <summary>
        /// Returns the selected shadow resolution tier.
        /// </summary>
        public int additionalLightsShadowResolutionTier
        {
            get { return m_AdditionalLightsShadowResolutionTier; }
        }

        [SerializeField] bool m_CustomShadowLayers = false;

        /// <summary>
        /// Indicates whether shadows need custom layers.
        /// If not, then it uses the same settings as lightLayerMask.
        /// </summary>
        public bool customShadowLayers
        {
            get
            {
                return m_CustomShadowLayers;
            }
            set
            {
                if (m_CustomShadowLayers != value)
                {
                    m_CustomShadowLayers = value;
                    SyncLightAndShadowLayers();
                }
            }
        }

        /// <summary>
        /// Controls the size of the cookie mask currently assigned to the light.
        /// </summary>
        [Tooltip("Controls the size of the cookie mask currently assigned to the light.")]
        public Vector2 lightCookieSize
        {
            get => m_LightCookieSize;
            set => m_LightCookieSize = value;
        }
        [SerializeField] Vector2 m_LightCookieSize = Vector2.one;

        /// <summary>
        /// Controls the offset of the cookie mask currently assigned to the light.
        /// </summary>
        [Tooltip("Controls the offset of the cookie mask currently assigned to the light.")]
        public Vector2 lightCookieOffset
        {
            get => m_LightCookieOffset;
            set => m_LightCookieOffset = value;
        }
        [SerializeField] Vector2 m_LightCookieOffset = Vector2.zero;

        /// <summary>
        /// Light soft shadow filtering quality.
        /// </summary>
        [Tooltip("Controls the filtering quality of soft shadows. Higher quality has lower performance.")]
        public SoftShadowQuality softShadowQuality
        {
            get => m_SoftShadowQuality;
            set => m_SoftShadowQuality = value;
        }
        [SerializeField] SoftShadowQuality m_SoftShadowQuality = SoftShadowQuality.UsePipelineSettings;

        [SerializeField, FormerlySerializedAs("m_DirectionalLightAngularDiameter")]
        float m_AngularDiameter = 0.5f;

        [SerializeField] float m_SoftnessScale = 1.0f;

        [SerializeField]
        float m_DirLightMaxPenumbraSize = 0.56f;
        [SerializeField]
        float m_DirLightMaxSamplingDistance = 0.5f;
        [SerializeField]
        float m_DirLightMinFilterSizeTexels = 1.5f;
        [SerializeField, Range(1, 64)]
        int m_DirLightBlockerTapCount = 16;
        [SerializeField, Range(1, 64)]
        int m_DirLightFilterSampleCount = 16;
        [SerializeField]
        float m_DirLightMinFilterMaxAngularDiameter = 10.0f;
        [SerializeField]
        float m_DirLightBlockerSearchAngularDiameter = 12.0f;
        [SerializeField, Range(1.0f, 6.0f)]
        float m_DirLightBlockerSamplingClumpExponent = 2.0f;

        /// <summary>Angular diameter of the directional light source in degrees.</summary>
        public float angularDiameter
        {
            get => m_AngularDiameter;
            set => m_AngularDiameter = Mathf.Max(value, 0.0f);
        }

        /// <summary>Scale factor applied to the angular diameter while calculating shadow softness.</summary>
        public float softnessScale
        {
            get => m_SoftnessScale;
            set => m_SoftnessScale = Mathf.Max(value, 0.0f);
        }

        /// <summary>Number of blocker-search taps used by directional contact-hardening shadows. Gather-capable APIs evaluate a 2x2 raw-depth footprint per tap.</summary>
        public int dirLightBlockerTapCount
        {
            get => m_DirLightBlockerTapCount;
            set => m_DirLightBlockerTapCount = Mathf.Clamp(value, 1, 64);
        }

        public int dirLightFilterSampleCount
        {
            get => m_DirLightFilterSampleCount;
            set => m_DirLightFilterSampleCount = Mathf.Clamp(value, 1, 64);
        }

        public float dirLightMaxPenumbraSize
        {
            get => m_DirLightMaxPenumbraSize;
            set => m_DirLightMaxPenumbraSize = Mathf.Max(value, 0.0f);
        }

        public float dirLightMaxSamplingDistance
        {
            get => m_DirLightMaxSamplingDistance;
            set => m_DirLightMaxSamplingDistance = Mathf.Max(value, 0.0f);
        }

        public float dirLightMinFilterSizeTexels
        {
            get => m_DirLightMinFilterSizeTexels;
            set => m_DirLightMinFilterSizeTexels = Mathf.Max(value, 0.0f);
        }

        public float dirLightMinFilterMaxAngularDiameter
        {
            get => m_DirLightMinFilterMaxAngularDiameter;
            set => m_DirLightMinFilterMaxAngularDiameter = Mathf.Max(value, 0.0f);
        }

        public float dirLightBlockerSearchAngularDiameter
        {
            get => m_DirLightBlockerSearchAngularDiameter;
            set => m_DirLightBlockerSearchAngularDiameter = Mathf.Max(value, 0.0f);
        }

        public float dirLightBlockerSamplingClumpExponent
        {
            get => m_DirLightBlockerSamplingClumpExponent;
            set => m_DirLightBlockerSamplingClumpExponent = Mathf.Max(value, 0.0f);
        }

        [SerializeField] bool m_UseScreenSpaceShadows = false;

        /// <summary>
        /// Controls whether this Directional Light resolves its realtime shadow map into a screen-space texture.
        /// The setting is effective only when this light is selected as the URP main light.
        /// </summary>
        public bool useScreenSpaceShadows
        {
            get => m_UseScreenSpaceShadows;
            set => m_UseScreenSpaceShadows = value;
        }

        [SerializeField] Color m_ShadowTint = Color.black;
        [SerializeField, FormerlySerializedAs("m_MainLightPenumbraTintEnabled")] bool m_PenumbraTint = false;

        /// <summary>Color used to tint this directional main light's shadows.</summary>
        public Color shadowTint
        {
            get => m_ShadowTint;
            set => m_ShadowTint = value;
        }

        /// <summary>When enabled, applies <see cref="shadowTint"/> to the shadow penumbra only.</summary>
        public bool penumbraTint
        {
            get => m_PenumbraTint;
            set => m_PenumbraTint = value;
        }
        
        [SerializeField] RenderingLayerMask m_RenderingLayersMask = RenderingLayerMask.defaultRenderingLayerMask;

        /// <summary>
        /// Specifies which rendering layers this light will affect.
        /// </summary>
        public RenderingLayerMask renderingLayers
        {
            get => m_RenderingLayersMask;
            set
            {
                if (m_RenderingLayersMask == value) return;
                m_RenderingLayersMask = value;
                SyncLightAndShadowLayers();
            }
        }
        
        [SerializeField] RenderingLayerMask m_ShadowRenderingLayersMask = RenderingLayerMask.defaultRenderingLayerMask;
        
        /// <summary>
        /// Specifies which rendering layers this light shadows will affect.
        /// </summary>
        public RenderingLayerMask shadowRenderingLayers
        {
            get => m_ShadowRenderingLayersMask;
            set
            {
                if (value == m_ShadowRenderingLayersMask) return;
                m_ShadowRenderingLayersMask = value;
                SyncLightAndShadowLayers();
            }
        }

        void SyncLightAndShadowLayers()
        {
            if (light)
                light.renderingLayerMask = m_CustomShadowLayers ? m_ShadowRenderingLayersMask : m_RenderingLayersMask;
        }
        
        enum Version
        {
            Initial = 0,
            RenderingLayers = 2,
            SoftShadowQuality = 3,
            RenderingLayersMask = 4,
            // Preserve the serialized version value used by previous releases.
            Count = 7
        }
        
        [SerializeField] Version m_Version = Version.Count;

        // This piece of code is needed because some objects could have been created before existence of Version enum
        /// <summary>OnBeforeSerialize needed to handle migration before the versioning system was in place.</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (m_Version == Version.Count) // serializing a newly created object
                m_Version = Version.Count - 1; // mark as up to date
        }

        /// <summary>OnAfterDeserialize needed to handle migration before the versioning system was in place.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_Version == Version.Count) // deserializing and object without version
                m_Version = Version.Initial; // reset to run the migration
            
            if (m_Version < Version.RenderingLayers)
            {
#pragma warning disable 618 // Obsolete warning
                m_RenderingLayers = (uint)m_LightLayerMask;
                m_ShadowRenderingLayers = (uint)m_ShadowLayerMask;
#pragma warning restore 618 // Obsolete warning
                m_Version = Version.RenderingLayers;
            }

            if (m_Version < Version.SoftShadowQuality)
            {
                // SoftShadowQuality.UsePipelineSettings added at index 0. Bump existing serialized values by 1. e.g. Low(0) -> Low(1).
                m_SoftShadowQuality = (SoftShadowQuality)(Math.Clamp((int)m_SoftShadowQuality + 1, 0, (int)SoftShadowQuality.High));
                m_Version = Version.SoftShadowQuality;
            }
            
            if (m_Version < Version.RenderingLayersMask)
            {
#pragma warning disable 618 // Obsolete warning
                m_RenderingLayersMask = m_RenderingLayers;
                m_ShadowRenderingLayersMask = m_ShadowRenderingLayers;
#pragma warning restore 618 // Obsolete warning
                m_Version = Version.RenderingLayersMask;
            }

        }
    }
    
    
}
