using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    internal class UniversalRenderPipelineSerializedLight : ISerializedLight
    {
        /// <summary>The base settings of the light</summary>
        public LightEditor.Settings settings { get; }
        /// <summary>The light serialized</summary>
        public SerializedObject serializedObject { get; }
        /// <summary>The additional light data serialized</summary>
        public SerializedObject serializedAdditionalDataObject { get; private set; }

        public UniversalAdditionalLightData additionalLightData => lightsAdditionalData[0];
        public UniversalAdditionalLightData[] lightsAdditionalData { get; private set; }

        // Common SRP's Lights properties
        public SerializedProperty intensity { get; }

        // URP Light Properties
        public SerializedProperty useAdditionalDataProp { get; }                     // Does light use shadow bias settings defined in UniversalRP asset file?
        public SerializedProperty additionalLightsShadowResolutionTierProp { get; }  // Index of the AdditionalLights ShadowResolution Tier
        public SerializedProperty softShadowQualityProp { get; }                     // Per light soft shadow filtering quality.
        public SerializedProperty lightCookieSizeProp { get; }                       // Multi dimensional light cookie size replacing `cookieSize` in legacy light.
        public SerializedProperty lightCookieOffsetProp { get; }                     // Multi dimensional light cookie offset.
        public SerializedProperty angularDiameterProp { get; }
        public SerializedProperty softnessScaleProp { get; }
        public SerializedProperty dirLightMaxPenumbraSizeProp { get; }
        public SerializedProperty dirLightMaxSamplingDistanceProp { get; }
        public SerializedProperty dirLightMinFilterSizeTexelsProp { get; }
        public SerializedProperty dirLightBlockerTapCountProp { get; }
        public SerializedProperty dirLightFilterSampleCountProp { get; }
        public SerializedProperty dirLightMinFilterMaxAngularDiameterProp { get; }
        public SerializedProperty dirLightBlockerSearchAngularDiameterProp { get; }
        public SerializedProperty dirLightBlockerSamplingClumpExponentProp { get; }
        public SerializedProperty useScreenSpaceShadowsProp { get; }
        public SerializedProperty shadowTintProp { get; }
        public SerializedProperty penumbraTintProp { get; }

        // Light layers related
        public SerializedProperty renderingLayers { get; }
        public SerializedProperty customShadowLayers { get; }
        public SerializedProperty shadowRenderingLayers { get; }

        /// <summary>Method that updates the <see cref="SerializedObject"/> of the Light and the Additional Light Data</summary>
        public void Update()
        {
            serializedObject.Update();
            serializedAdditionalDataObject.Update();
            settings.Update();
        }

        /// <summary>Method that applies the modified properties the <see cref="SerializedObject"/> of the Light and the Light Camera Data</summary>
        public void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            serializedAdditionalDataObject.ApplyModifiedProperties();
            settings.ApplyModifiedProperties();
        }

        /// <summary>Constructor</summary>
        /// <param name="serializedObject"><see cref="SerializedObject"/> with the light</param>
        /// <param name="settings"><see cref="LightEditor.Settings"/>with the settings</param>
        public UniversalRenderPipelineSerializedLight(SerializedObject serializedObject, LightEditor.Settings settings)
        {
            this.settings = settings;
            settings.OnEnable();

            this.serializedObject = serializedObject;

            lightsAdditionalData = CoreEditorUtils
                .GetAdditionalData<UniversalAdditionalLightData>(serializedObject.targetObjects);
            serializedAdditionalDataObject = new SerializedObject(lightsAdditionalData);

            intensity = serializedObject.FindProperty("m_Intensity");

            useAdditionalDataProp = serializedAdditionalDataObject.FindProperty("m_UsePipelineSettings");
            additionalLightsShadowResolutionTierProp = serializedAdditionalDataObject.FindProperty("m_AdditionalLightsShadowResolutionTier");
            softShadowQualityProp = serializedAdditionalDataObject.FindProperty("m_SoftShadowQuality");
            lightCookieSizeProp = serializedAdditionalDataObject.FindProperty("m_LightCookieSize");
            lightCookieOffsetProp = serializedAdditionalDataObject.FindProperty("m_LightCookieOffset");
            angularDiameterProp = serializedAdditionalDataObject.FindProperty("m_AngularDiameter");
            softnessScaleProp = serializedAdditionalDataObject.FindProperty("m_SoftnessScale");
            dirLightMaxPenumbraSizeProp = serializedAdditionalDataObject.FindProperty("m_DirLightMaxPenumbraSize");
            dirLightMaxSamplingDistanceProp = serializedAdditionalDataObject.FindProperty("m_DirLightMaxSamplingDistance");
            dirLightMinFilterSizeTexelsProp = serializedAdditionalDataObject.FindProperty("m_DirLightMinFilterSizeTexels");
            dirLightBlockerTapCountProp = serializedAdditionalDataObject.FindProperty("m_DirLightBlockerTapCount");
            dirLightFilterSampleCountProp = serializedAdditionalDataObject.FindProperty("m_DirLightFilterSampleCount");
            dirLightMinFilterMaxAngularDiameterProp = serializedAdditionalDataObject.FindProperty("m_DirLightMinFilterMaxAngularDiameter");
            dirLightBlockerSearchAngularDiameterProp = serializedAdditionalDataObject.FindProperty("m_DirLightBlockerSearchAngularDiameter");
            dirLightBlockerSamplingClumpExponentProp = serializedAdditionalDataObject.FindProperty("m_DirLightBlockerSamplingClumpExponent");
            useScreenSpaceShadowsProp = serializedAdditionalDataObject.FindProperty("m_UseScreenSpaceShadows");
            shadowTintProp = serializedAdditionalDataObject.FindProperty("m_ShadowTint");
            penumbraTintProp = serializedAdditionalDataObject.FindProperty("m_PenumbraTint");

            renderingLayers = serializedAdditionalDataObject.FindProperty("m_RenderingLayersMask");
            customShadowLayers = serializedAdditionalDataObject.FindProperty("m_CustomShadowLayers");
            shadowRenderingLayers = serializedAdditionalDataObject.FindProperty("m_ShadowRenderingLayersMask");

            settings.ApplyModifiedProperties();
        }
    }
}
