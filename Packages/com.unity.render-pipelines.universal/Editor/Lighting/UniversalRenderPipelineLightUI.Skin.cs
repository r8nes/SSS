using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    partial class UniversalRenderPipelineLightUI
    {
        private static class Styles
        {
            public static readonly GUIContent Type = EditorGUIUtility.TrTextContent("Type", "Specifies the current type of light. Possible types are Directional, Spot, Point, and Area lights.");

            public static readonly GUIContent AreaLightShapeContent = EditorGUIUtility.TrTextContent("Shape", "Specifies the shape of the Area light. Possible types are Rectangle and Disc.");
            public static readonly GUIContent[] LightTypeTitles = { EditorGUIUtility.TrTextContent("Spot"), EditorGUIUtility.TrTextContent("Directional"), EditorGUIUtility.TrTextContent("Point"), EditorGUIUtility.TrTextContent("Area (baked only)") };
            public static readonly int[] LightTypeValues = { (int)LightType.Spot, (int)LightType.Directional, (int)LightType.Point, (int)LightType.Rectangle };

            public static readonly GUIContent[] AreaLightShapeTitles = { EditorGUIUtility.TrTextContent("Rectangle"), EditorGUIUtility.TrTextContent("Disc") };
            public static readonly int[] AreaLightShapeValues = { (int)LightType.Rectangle, (int)LightType.Disc };

            public static readonly GUIContent SpotAngle = EditorGUIUtility.TrTextContent("Spot Angle", "Controls the angle in degrees at the base of a Spot light's cone.");

            public static readonly GUIContent BakingWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. Enable Baked Global Illumination to use Mixed or Baked light modes.");
            public static readonly GUIContent DisabledLightWarning = EditorGUIUtility.TrTextContent("Lighting has been disabled in at least one Scene view. Any changes applied to lights in the Scene will not be updated in these views until Lighting has been enabled again.");
            public static readonly GUIContent SunSourceWarning = EditorGUIUtility.TrTextContent("This light is set as the current Sun Source, which requires a directional light. Go to the Lighting Window's Environment settings to edit the Sun Source.");
            public static readonly GUIContent CullingMask = EditorGUIUtility.TrTextContent("Culling Mask", "Specifies which lights are culled per camera. To control exclude certain lights affecting certain objects, use Rendering Layers instead, which is supported across all rendering paths.");
            public static readonly GUIContent CullingMaskWarning = EditorGUIUtility.TrTextContent("Culling Mask should be used to control which lights are culled per camera. If you want to exclude certain lights from affecting certain objects, use Rendering Layers on the Light, and Rendering Layer Mask on the Mesh Renderer.");

            public static readonly GUIContent ShadowRealtimeSettings = EditorGUIUtility.TrTextContent("Realtime Shadows", "Settings for realtime direct shadows.");
            public static readonly GUIContent ShadowStrength = EditorGUIUtility.TrTextContent("Strength", "Controls how dark the shadows cast by the light will be.");
            public static readonly GUIContent ScreenSpaceShadows = EditorGUIUtility.TrTextContent("Screen Space Shadows", "Resolve this main Directional Light's realtime shadow map once per opaque screen pixel. Transparent objects continue to sample the shadow atlas.");
            public static readonly GUIContent ShadowNearPlane = EditorGUIUtility.TrTextContent("Near Plane", "Controls the value for the near clip plane when rendering shadows. Currently clamped to 0.1 units or 1% of the lights range property, whichever is lower.");
            public static readonly GUIContent ShadowNormalBias = EditorGUIUtility.TrTextContent("Normal", "Determines the bias this Light applies along the normal of surfaces it illuminates. This is ignored for point lights.");
            public static readonly GUIContent ShadowDepthBias = EditorGUIUtility.TrTextContent("Depth", "Determines the bias at which shadows are pushed away from the shadow-casting Game Object along the line from the Light.");
            public static readonly GUIContent ShadowInfo = EditorGUIUtility.TrTextContent("Unity might reduce the Light's shadow resolution to ensure that shadow maps fit in the shadow atlas. Consider this when selecting the the size of the shadow atlas, the shadow resolution of Lights, the number of Lights in your scene and whether you use soft shadows.");

            // Resolution (default or custom)
            public static readonly GUIContent ShadowResolution = EditorGUIUtility.TrTextContent("Resolution", $"Sets the rendered resolution of the shadow maps. A higher resolution increases the fidelity of shadows at the cost of GPU performance and memory usage. Rounded to the next power of two, and clamped to be at least {UniversalAdditionalLightData.AdditionalLightsShadowMinimumResolution}.");
            public static readonly int[] ShadowResolutionDefaultValues =
            {
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh
            };
            public static readonly GUIContent[] ShadowResolutionDefaultOptions =
            {
                EditorGUIUtility.TrTextContent("Custom"),
                UniversalRenderPipelineAssetUI.Styles.additionalLightsShadowResolutionTierNames[0],
                UniversalRenderPipelineAssetUI.Styles.additionalLightsShadowResolutionTierNames[1],
                UniversalRenderPipelineAssetUI.Styles.additionalLightsShadowResolutionTierNames[2],
            };

            public static GUIContent SoftShadowQuality = EditorGUIUtility.TrTextContent("Soft Shadows Quality", "Controls the filtering quality of soft shadows. Higher quality has lower performance.");

            // Bias (default or custom)
            public static GUIContent shadowBias = EditorGUIUtility.TrTextContent("Bias", "Select if the Bias should use the settings from the Pipeline Asset or Custom settings.");
            public static int[] optionDefaultValues = { 0, 1 };
            public static GUIContent[] displayedDefaultOptions =
            {
                EditorGUIUtility.TrTextContent("Custom"),
                EditorGUIUtility.TrTextContent("Use settings from Render Pipeline Asset")
            };

            public static readonly GUIContent customShadowLayers = EditorGUIUtility.TrTextContent("Custom Shadow Layers", "When enabled, you can use the Layer property below to specify the layers for shadows seperately to lighting. When disabled, the Light Layer property in the General section specifies the layers for both lighting and for shadows.");
            public static readonly GUIContent ShadowLayer = EditorGUIUtility.TrTextContent("Layer", "Specifies the light layer to use for shadows.");
            public static readonly GUIContent HighQualitySettings = EditorGUIUtility.TrTextContent("High Quality Settings");
            public static readonly GUIContent DirectionalLightAngularDiameter = EditorGUIUtility.TrTextContent("Angular Diameter", "Angular diameter of the directional light source in degrees.");
            public static readonly GUIContent DirectionalLightMaxPenumbraSize = EditorGUIUtility.TrTextContent("Max Penumbra Size", "Maximum size (in world space) of contact-hardening shadow penumbra limiting blur filter kernel size, larger kernels may require more samples to avoid quality degradation.");
            public static readonly GUIContent DirectionalLightMaxSamplingDistance = EditorGUIUtility.TrTextContent("Max Sampling Distance", "Maximum distance (in world space) from the receiver shadow sampling occurs, lower to avoid light bleeding but may cause self-shadowing.");
            public static readonly GUIContent DirectionalLightMinFilterSizeTexels = EditorGUIUtility.TrTextContent("Min Filter", "Minimum filter size (in shadowmap texels) to avoid aliasing close to caster.");
            public static readonly GUIContent DirectionalLightBlockerTapCount = EditorGUIUtility.TrTextContent("Blocker Search Taps", "Controls the blocker-search kernel size. On gather-capable APIs each tap evaluates a 2x2 raw-depth footprint; other APIs use one point sample per tap. Increasing the count raises quality at the expense of performance.");
            public static readonly GUIContent DirectionalLightFilterSampleCount = EditorGUIUtility.TrTextContent("Filter Sample Count", "Controls the number of samples that blur shadows over the kernel; increasing it raises quality at the expense of performance.");
            public static readonly GUIContent DirectionalLightMinFilterMaxAngularDiameter = EditorGUIUtility.TrTextContent("Min Filter Max Angular Diameter", "Maximum angular diameter to reach minimum filter size, lower to avoid self-shadowing but may cause light bleeding.");
            public static readonly GUIContent DirectionalLightBlockerSearchAngularDiameter = EditorGUIUtility.TrTextContent("Blocker Search Angular Diameter", "Angular diameter to use for blocker search, increase to avoid missing hidden close blockers but may cause self-shadowing.");
            public static readonly GUIContent DirectionalLightBlockerSamplingClumpExponent = EditorGUIUtility.TrTextContent("Blocker Sampling Clump Exponent", "Affects how blocker search samples are distributed. Samples distance to center is elevated to this power.");
            public static readonly GUIContent RadiusScaleForSoftness = EditorGUIUtility.TrTextContent("Radius Scale for Softness", "Scale the angular diameter for the sake of softness calculation. Higher scales result in higher softness.");
            public static readonly GUIContent ShadowTint = EditorGUIUtility.TrTextContent("Tint", "Specifies the color that URP tints this directional main light's dynamic shadows to.");
            public static readonly GUIContent PenumbraTint = EditorGUIUtility.TrTextContent("Penumbra Tint", "When enabled, the tint only affects the shadow's penumbra.");

            public static readonly GUIContent LightCookieSize = EditorGUIUtility.TrTextContent("Cookie Size", "Controls the size of the cookie mask currently assigned to the light.");
            public static readonly GUIContent LightCookieOffset = EditorGUIUtility.TrTextContent("Cookie Offset", "Controls the offset of the cookie mask currently assigned to the light.");
            /// <summary>Title with "Rendering Layer"</summary>
            public static readonly GUIContent RenderingLayers = EditorGUIUtility.TrTextContent("Rendering Layers", "Select the Rendering Layers that the Light affects. This Light affects objects where at least one Rendering Layer value matches.");
            public static readonly GUIContent RenderingLayersDisabled = EditorGUIUtility.TrTextContent("Rendering Layers", "Rendering Layers are disabled because they have a small GPU performance cost. To enable this setting, go to the active Universal Render Pipeline Asset, and enable Lighting -> Use Rendering Layers.");
        }
    }
}
