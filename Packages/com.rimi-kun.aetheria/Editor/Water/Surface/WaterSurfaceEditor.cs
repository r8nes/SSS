using UnityEditor.Rendering.Universal;
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Aetheria.Runtime.Water;
using CoreRenderPipelinePreferences = UnityEditor.Rendering.CoreRenderPipelinePreferences;

namespace Aetheria.Editor.Water
{
    using CED = CoreEditorDrawer<WaterSurfaceEditor>;

    sealed class WaterPairedPropertyDrawer
    {
        const int k_DoubleFieldMargin = 10;

        public static void DrawFloatPair(GUIContent label, GUIContent subLabel0, SerializedProperty subContent0, GUIContent subLabel1, SerializedProperty subContent1)
        {
            var prefixLabel = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), label);

            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float previousLabelWidth = EditorGUIUtility.labelWidth;

            var firstRect = prefixLabel;
            firstRect.width = firstRect.width / 2f - k_DoubleFieldMargin;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(subLabel0).x + 4f;
            using (new EditorGUI.MixedValueScope(subContent0.hasMultipleDifferentValues))
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(firstRect, subLabel0, subContent0.floatValue);
                if (EditorGUI.EndChangeCheck())
                    subContent0.floatValue = value;
            }

            var secondRect = prefixLabel;
            secondRect.x += firstRect.width + 2f + k_DoubleFieldMargin * 2f;
            secondRect.width = secondRect.width / 2f - 2f - k_DoubleFieldMargin;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(subLabel1).x + 2f;
            using (new EditorGUI.MixedValueScope(subContent1.hasMultipleDifferentValues))
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(secondRect, subLabel1, subContent1.floatValue);
                if (EditorGUI.EndChangeCheck())
                    subContent1.floatValue = value;
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUI.indentLevel = previousIndentLevel;
        }
    }

    readonly struct BoldLabelScope : IDisposable
    {
        readonly FontStyle m_PreviousFontStyle;

        public BoldLabelScope(bool enabled = true)
        {
            m_PreviousFontStyle = EditorStyles.label.fontStyle;
            if (enabled)
                EditorStyles.label.fontStyle = FontStyle.Bold;
        }

        public void Dispose()
        {
            EditorStyles.label.fontStyle = m_PreviousFontStyle;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(WaterSurface))]
    sealed class WaterSurfaceEditor : UnityEditor.Editor
    {
        SerializedProperty m_SurfaceType;
        SerializedProperty m_GeometryType;
        SerializedProperty m_MeshRenderers;
        SerializedProperty m_LodScaleRange;
        SerializedProperty m_LodWaveHeightDetailBias;
        SerializedProperty m_LodLevels;
        SerializedProperty m_LodResolution;
        SerializedProperty m_LodGeometryDownsampleFactor;
        SerializedProperty m_LodHorizonExtensionMultiplier;
        SerializedProperty m_SimulationEnabled;
        SerializedProperty m_SimulationTimeScale;

        void OnEnable()
        {
            var fetcher = new PropertyFetcher<WaterSurface>(serializedObject);

            m_SurfaceType = fetcher.Find(x => x.surfaceType);
            m_GeometryType = fetcher.Find(x => x.geometryType);
            m_MeshRenderers = fetcher.Find(x => x.meshRenderers);
            m_LodScaleRange = fetcher.Find(x => x.lodScaleRange);
            m_LodWaveHeightDetailBias = fetcher.Find(x => x.lodWaveHeightDetailBias);
            m_LodLevels = fetcher.Find(x => x.lodLevels);
            m_LodResolution = fetcher.Find(x => x.lodResolution);
            m_LodGeometryDownsampleFactor = fetcher.Find(x => x.lodGeometryDownsampleFactor);
            m_LodHorizonExtensionMultiplier = fetcher.Find(x => x.lodHorizonExtensionMultiplier);
            m_SimulationEnabled = fetcher.Find(x => x.simulationEnabled);
            m_SimulationTimeScale = fetcher.Find(x => x.simulationTimeScale);

            FetchSimulationProperties(fetcher);
            FetchAppearanceProperties(fetcher);
            FetchCausticsProperties(fetcher);
            FetchFoamProperties(fetcher);
            FetchUnderwaterProperties(fetcher);
            FetchMiscellaneousProperties(fetcher);
        }

        static internal void DrawGeneralSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            WaterSurfaceType previousSurfaceType = (WaterSurfaceType)serialized.m_SurfaceType.enumValueIndex;
            EditorGUILayout.PropertyField(serialized.m_SurfaceType, k_SurfaceType);
            WaterSurfaceType currentSurfaceType = (WaterSurfaceType)serialized.m_SurfaceType.enumValueIndex;

            switch (currentSurfaceType)
            {
                case WaterSurfaceType.OceanSeaLake:
                    if (previousSurfaceType != currentSurfaceType)
                        serialized.m_GeometryType.enumValueIndex = (int)WaterGeometryType.Infinite;

                    EditorGUILayout.PropertyField(serialized.m_GeometryType, k_GeometryType);
                    break;

                case WaterSurfaceType.Pool:
                    if ((WaterGeometryType)serialized.m_GeometryType.enumValueIndex == WaterGeometryType.Infinite)
                        serialized.m_GeometryType.enumValueIndex = (int)WaterGeometryType.Quad;

                    EditorGUI.BeginChangeCheck();
                    Rect rect = EditorGUILayout.GetControlRect();
                    EditorGUI.BeginProperty(rect, k_GeometryType, serialized.m_GeometryType);
                    int value = EditorGUI.Popup(rect, k_GeometryType, serialized.m_GeometryType.enumValueIndex, k_GeometryTypeEnum);
                    EditorGUI.EndProperty();
                    if (EditorGUI.EndChangeCheck())
                        serialized.m_GeometryType.enumValueIndex = value;
                    break;
            }

            WaterGeometryType geometryType = (WaterGeometryType)serialized.m_GeometryType.enumValueIndex;
            using (new EditorGUI.IndentLevelScope())
            {
                if (geometryType == WaterGeometryType.CustomMesh)
                    EditorGUILayout.PropertyField(serialized.m_MeshRenderers, k_MeshRenderers);
            }

            if (geometryType == WaterGeometryType.InstancedQuads || geometryType == WaterGeometryType.Infinite)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Level of Detail", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(serialized.m_LodScaleRange, k_LodScaleRange);
                EditorGUILayout.PropertyField(serialized.m_LodWaveHeightDetailBias, k_LodWaveHeightDetailBias);
                EditorGUILayout.PropertyField(serialized.m_LodLevels, k_LodLevels);
                EditorGUILayout.PropertyField(serialized.m_LodResolution, k_LodResolution);
                EditorGUILayout.PropertyField(serialized.m_LodGeometryDownsampleFactor, k_LodGeometryDownsampleFactor);
                EditorGUILayout.PropertyField(serialized.m_LodHorizonExtensionMultiplier, k_LodExtentsSizeMultiplier);
            }

            EditorGUILayout.PropertyField(serialized.m_SimulationEnabled, k_SimulationEnabled);
            if (serialized.m_SimulationEnabled.hasMultipleDifferentValues || serialized.m_SimulationEnabled.boolValue)
                EditorGUILayout.PropertyField(serialized.m_SimulationTimeScale, k_SimulationTimeScale);

        }

        static void MapWithExtent(SerializedProperty maskProp, GUIContent content, SerializedProperty extentProp)
        {
            bool wasEmpty = maskProp.objectReferenceValue == null;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(maskProp, content);
            if (EditorGUI.EndChangeCheck() && wasEmpty && maskProp.objectReferenceValue != null)
            {
                var surface = maskProp.serializedObject.targetObject as WaterSurface;
                if (surface != null && !surface.IsInfinite())
                {
                    Vector3 scale = surface.transform.lossyScale;
                    extentProp.vector2Value = new Vector2(scale.x, scale.z);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            EditorStyles.label.fontStyle = FontStyle.Normal;

            serializedObject.Update();

            if (target is WaterSurface surface && surface.surfaceIndex == -1)
            {
                EditorGUILayout.HelpBox("Only up to 16 water surfaces are supported simultaneously. This surface will not be rendered.", MessageType.Warning);
                EditorGUILayout.Space();
            }

            WaterSurfaceUI.Inspector.Draw(this, this);
            serializedObject.ApplyModifiedProperties();
        }

        static void SanitizeExtentsVector2(SerializedProperty prop)
        {
            if (prop == null || prop.hasMultipleDifferentValues)
                return;

            Vector2 value = prop.vector2Value;
            value.x = Mathf.Max(value.x, 0.1f);
            value.y = Mathf.Max(value.y, 0.1f);
            prop.vector2Value = value;
        }

        static public readonly GUIContent k_SurfaceType = EditorGUIUtility.TrTextContent("Surface Type", "Specifies the nature of the water body that the water system needs to simulate.");
        static public readonly GUIContent k_GeometryType = EditorGUIUtility.TrTextContent("Geometry Type", "Specifies the type of geometry used to render the water surface. Quad stretches a unique grid over the size of the water surface. Custom allows you to specify a set of mesh renderers used to render the water surface for better culling. Instanced Quads creates a finite water surface with multiple instanced grids to keep a higher vertex density. Infinite generates a surface that extends to the far plane.");
        static public GUIContent[] k_GeometryTypeEnum = { new GUIContent(WaterGeometryType.Quad.ToString()), new GUIContent(WaterGeometryType.CustomMesh.ToString()), new GUIContent(WaterGeometryType.InstancedQuads.ToString()) };
        static public readonly GUIContent k_MeshRenderers = EditorGUIUtility.TrTextContent("Mesh Renderers", "Sets the geometries to use when rendering in custom geometry type mode.");
        static public readonly GUIContent k_LodScaleRange = EditorGUIUtility.TrTextContent("Scale", "Minimum and maximum horizontal scale of the water LOD mesh.");
        static public readonly GUIContent k_LodWaveHeightDetailBias = EditorGUIUtility.TrTextContent("Drop Detail Height Based On Waves");
        static public readonly GUIContent k_LodLevels = EditorGUIUtility.TrTextContent("Levels");
        static public readonly GUIContent k_LodResolution = EditorGUIUtility.TrTextContent("Resolution");
        static public readonly GUIContent k_LodGeometryDownsampleFactor = EditorGUIUtility.TrTextContent("Geometry Down Sample Factor");
        static public readonly GUIContent k_LodExtentsSizeMultiplier = EditorGUIUtility.TrTextContent("Extents Size Multiplier");

        static public readonly GUIContent k_SimulationEnabled = EditorGUIUtility.TrTextContent("Simulation", "Enables the animated wave simulation.");
        static public readonly GUIContent k_SimulationTimeScale = EditorGUIUtility.TrTextContent("Simulation Time Scale", "Sets the speed of the water simulation. This allows you to slow down the waves or accelerate them.");
        static public readonly GUIContent k_SimulationMask = EditorGUIUtility.TrTextContent("Simulation Mask", "Sets the texture used to attenuate or suppress simulated displacement across the surface.");
        static public readonly GUIContent k_SimulationMaskExtent = EditorGUIUtility.TrTextContent("Extent", "Sets the extent of the simulation mask in meters.");
        static public readonly GUIContent k_SimulationMaskOffset = EditorGUIUtility.TrTextContent("Offset", "Sets the offset of the simulation mask in meters.");
        static public readonly GUIContent k_SimulationMaskRemap = EditorGUIUtility.TrTextContent("Remapping", "Sets the remapped simulation mask value.");

        static public readonly GUIContent k_CurrentDirection = EditorGUIUtility.TrTextContent("Direction", "Global water-current direction in degrees.");
        static public readonly GUIContent k_CurrentSpeed = EditorGUIUtility.TrTextContent("Current Speed", "Uniform current speed in kilometres per hour.");
        static public readonly GUIContent k_CurrentMap = EditorGUIUtility.TrTextContent("Current Map", "Sets the texture used to modify the local water-current direction. Red and Green store direction, Blue stores influence.");
        static public readonly GUIContent k_CurrentMapInfluence = EditorGUIUtility.TrTextContent("Influence", "Sets the influence of the current map over the global current.");
        static public readonly GUIContent k_CurrentRegionExtent = EditorGUIUtility.TrTextContent("Extent", "Sets the extent of the current region in meters.");
        static public readonly GUIContent k_CurrentRegionOffset = EditorGUIUtility.TrTextContent("Offset", "Sets the offset of the current region in meters.");

        static public readonly GUIContent k_MaterialOverride = EditorGUIUtility.TrTextContent("Material Override", "Sets a custom material used to render the water surface. If left empty, the default water material is used.");

        static public readonly GUIContent k_RefractionColor = EditorGUIUtility.TrTextContent("Color", "Sets the color used to tint water refraction.");
        static public readonly GUIContent k_MaxRefractionDistance = EditorGUIUtility.TrTextContent("Maximum Distance", "Controls the maximum distance in meters used to clamp refraction depth.");
        static public readonly GUIContent k_AbsorptionDistance = EditorGUIUtility.TrTextContent("Absorption Distance", "Controls the approximate distance in meters the camera can perceive through a water surface.");

        static public readonly GUIContent k_ScatteringColor = EditorGUIUtility.TrTextContent("Color", "Sets the color used to simulate water light scattering.");
        static public readonly GUIContent k_AmbientScattering = EditorGUIUtility.TrTextContent("Ambient Term", "Controls the intensity of the ambient scattering term.");
        static public readonly GUIContent k_ProbeLightingFadeStart = EditorGUIUtility.TrTextContent("Fade Start", "Distance in meters from the water surface center where probe lighting starts fading to the ambient probe.");
        static public readonly GUIContent k_ProbeLightingFadeEnd = EditorGUIUtility.TrTextContent("Fade End", "Distance in meters from the water surface center where probe lighting is fully replaced by the ambient probe. Values not greater than Fade Start disable the fade.");
        static public readonly GUIContent k_HeightScattering = EditorGUIUtility.TrTextContent("Height Term", "Controls height-based scattering. Higher vertical displacement receives more scattering.");
        static public readonly GUIContent k_DisplacementScattering = EditorGUIUtility.TrTextContent("Displacement Term", "Controls displacement-based scattering. Larger horizontal displacement receives more scattering.");
        static public readonly GUIContent k_DirectLightTipScattering = EditorGUIUtility.TrTextContent("Direct Light Tip Term", "Controls additional directional scattering through thin wave tips.");
        static public readonly GUIContent k_DirectLightBodyScattering = EditorGUIUtility.TrTextContent("Direct Light Body Term", "Controls additional directional scattering through thicker wave bodies.");

        static public readonly GUIContent k_SmoothnessRange = EditorGUIUtility.TrTextContent("Value Range", "Controls the smoothness value range over the Fade region.");
        static public readonly GUIContent k_SmoothnessFadeRange = EditorGUIUtility.TrTextContent("Fade Range", "Specifies the range over which the smoothness is interpolated from close to distant.");
        static public readonly GUIContent k_SmoothnessFadeStart = EditorGUIUtility.TrTextContent("Start", "Sets the distance in meters at which the smoothness interpolation starts.");
        static public readonly GUIContent k_SmoothnessFadeDistance = EditorGUIUtility.TrTextContent("Distance", "Sets the length in meters over which the smoothness is interpolated from close to distant.");
        static public readonly GUIContent k_NormalMap = EditorGUIUtility.TrTextContent("Normal Map");
        static public readonly GUIContent k_NormalMapScale = EditorGUIUtility.TrTextContent("Scale", "Base scale of the normal map detail.");
        static public readonly GUIContent k_NormalMapScrollSpeed = EditorGUIUtility.TrTextContent("Scroll Speed", "Scroll speed of the normal map detail.");
        static public readonly GUIContent k_NormalMapStrength = EditorGUIUtility.TrTextContent("Strength");
        static public readonly GUIContent k_DepthTintStrength = EditorGUIUtility.TrTextContent("Strength", "Blends the physical water body toward the artistic shallow/deep palette without changing extinction or fog density.");
        static public readonly GUIContent k_ShallowDepthTint = EditorGUIUtility.TrTextContent("Shallow", "Artistic target color for shallow surface water.");
        static public readonly GUIContent k_DeepDepthTint = EditorGUIUtility.TrTextContent("Deep", "Artistic target color for deep surface water.");
        static public readonly GUIContent k_DepthTintDistance = EditorGUIUtility.TrTextContent("Distance", "Optical depth in meters over which the artistic surface color transitions from shallow to deep.");
        static public readonly GUIContent k_DepthTintFalloff = EditorGUIUtility.TrTextContent("Falloff", "Shapes the shallow-to-deep artistic color transition.");
        static public readonly GUIContent k_ScreenSpaceReflections = EditorGUIUtility.TrTextContent("Screen Space Reflections", "Enables screen space reflections for this water surface.");
        static public readonly GUIContent k_ShorelineSmoothness = EditorGUIUtility.TrTextContent("Shoreline Smoothness");
        static public readonly GUIContent k_ShorelineSmoothnessInfluence = EditorGUIUtility.TrTextContent("Shoreline Influence", "Controls how strongly shallow depth changes canonical smoothness.");
        static public readonly GUIContent k_ShorelineSmoothnessDepthRange = EditorGUIUtility.TrTextContent("Shoreline Depth Range", "Optical depth in meters over which shoreline smoothness fades out.");
        static public readonly GUIContent k_FoamSmoothness = EditorGUIUtility.TrTextContent("Smoothness", "Controls the broad dielectric reflection lobe of the foam layer.");
        static public readonly GUIContent k_FoamTexture = EditorGUIUtility.TrTextContent("Foam Texture", "Overrides the default foam texture for this water surface.");
        static public readonly GUIContent k_FoamTextureTiling = EditorGUIUtility.TrTextContent("Texture Tiling", "Sets the per meter tiling for the foam texture.");
        static public readonly GUIContent k_FoamColor = EditorGUIUtility.TrTextContent("Color", "Sets the foam color.");

        static public readonly GUIContent k_DebugMode = EditorGUIUtility.TrTextContent("Debug Mode", "Sets the debug mode for the water surface.");
        static public readonly GUIContent k_SimulationMaskDebugMode = EditorGUIUtility.TrTextContent("Simulation Mask Mode", "Sets the simulation mask debug mode for this surface.");
        static public readonly GUIContent k_CurrentDebugArrowDensity = EditorGUIUtility.TrTextContent("Arrow Density Multiplier", "Sets a multiplier for arrow density in current debug mode.");

        SerializedProperty m_SimulationMask;
        SerializedProperty m_SimulationMaskRemap;
        SerializedProperty m_SimulationMaskExtent;
        SerializedProperty m_SimulationMaskOffset;

        SerializedProperty m_Cascade0;
        SerializedProperty m_Cascade1;
        SerializedProperty m_Cascade2;

        SerializedProperty m_CurrentDirection;
        SerializedProperty m_CurrentSpeed;
        SerializedProperty m_CurrentMap;
        SerializedProperty m_CurrentRegionExtent;
        SerializedProperty m_CurrentRegionOffset;
        SerializedProperty m_CurrentMapInfluence;

        void FetchSimulationProperties(PropertyFetcher<WaterSurface> o)
        {
            m_SimulationMask = o.Find(x => x.simulationMask);
            m_SimulationMaskRemap = o.Find(x => x.simulationMaskRemap);
            m_SimulationMaskExtent = o.Find(x => x.simulationMaskExtent);
            m_SimulationMaskOffset = o.Find(x => x.simulationMaskOffset);
            m_Cascade0 = o.Find(x => x.cascade0);
            m_Cascade1 = o.Find(x => x.cascade1);
            m_Cascade2 = o.Find(x => x.cascade2);
            m_CurrentDirection = o.Find(x => x.currentDirection);
            m_CurrentSpeed = o.Find(x => x.currentSpeed);
            m_CurrentMap = o.Find(x => x.currentMap);
            m_CurrentRegionExtent = o.Find(x => x.currentRegionExtent);
            m_CurrentRegionOffset = o.Find(x => x.currentRegionOffset);
            m_CurrentMapInfluence = o.Find(x => x.currentMapInfluence);
        }

        static void DrawSectionTitle(string label)
        {
            EditorGUILayout.Space(3.0f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        static void DrawCurrentSection(WaterSurfaceEditor serialized)
        {
            DrawSectionTitle("Current");
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_CurrentDirection, k_CurrentDirection);
                EditorGUILayout.PropertyField(serialized.m_CurrentSpeed, k_CurrentSpeed);
                MapWithExtent(serialized.m_CurrentMap, k_CurrentMap, serialized.m_CurrentRegionExtent);
                if (serialized.m_CurrentMap.objectReferenceValue != null)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serialized.m_CurrentRegionExtent, k_CurrentRegionExtent);
                        SanitizeExtentsVector2(serialized.m_CurrentRegionExtent);
                        EditorGUILayout.PropertyField(serialized.m_CurrentRegionOffset, k_CurrentRegionOffset);
                        EditorGUILayout.PropertyField(serialized.m_CurrentMapInfluence, k_CurrentMapInfluence);
                    }
                }
            }
        }

        static void DrawSurfaceMask(WaterSurfaceEditor serialized)
        {
            DrawSectionTitle("Simulation Mask");
            using (new EditorGUI.IndentLevelScope())
            {
                MapWithExtent(serialized.m_SimulationMask, k_SimulationMask, serialized.m_SimulationMaskExtent);
                if (serialized.m_SimulationMask.objectReferenceValue != null)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(serialized.m_SimulationMaskExtent, k_SimulationMaskExtent);
                        EditorGUILayout.PropertyField(serialized.m_SimulationMaskOffset, k_SimulationMaskOffset);
                        Vector2 remap = serialized.m_SimulationMaskRemap.vector2Value;
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.MinMaxSlider(k_SimulationMaskRemap, ref remap.x, ref remap.y, 0.0f, 1.0f);
                        if (EditorGUI.EndChangeCheck())
                            serialized.m_SimulationMaskRemap.vector2Value = remap;
                    }
                }
            }
        }

        static readonly string[] k_CascadeTabs = { "Cascade 1", "Cascade 2", "Cascade 3" };

        int m_SelectedCascade;
        bool m_ShowCascadeSpectrum;
        bool m_ShowCascadeBreaking;

        static void DrawCascadeSlider(SerializedProperty cascade, string propertyName, GUIContent label, float minimum, float maximum)
        {
            SerializedProperty property = cascade.FindPropertyRelative(propertyName);
            EditorGUILayout.Slider(property, minimum, maximum, label);
        }

        static void DrawCascades(WaterSurfaceEditor serialized)
        {
            DrawSectionTitle("Waves");
            serialized.m_SelectedCascade = GUILayout.Toolbar(serialized.m_SelectedCascade, k_CascadeTabs);
            SerializedProperty cascade = serialized.m_SelectedCascade == 0
                ? serialized.m_Cascade0
                : serialized.m_SelectedCascade == 1 ? serialized.m_Cascade1 : serialized.m_Cascade2;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty tileLength = cascade.FindPropertyRelative("tileLength");
                EditorGUILayout.PropertyField(tileLength, EditorGUIUtility.TrTextContent("Tile Length", "Physical repetition size of this independent spectrum in metres."));
                if (!tileLength.hasMultipleDifferentValues)
                {
                    Vector2 tile = tileLength.vector2Value;
                    tileLength.vector2Value = new Vector2(Mathf.Max(tile.x, 0.001f), Mathf.Max(tile.y, 0.001f));
                }

                DrawCascadeSlider(cascade, "windSpeed", EditorGUIUtility.TrTextContent("Wind Speed", "Ten-metre wind speed in metres per second."), 0.1f, 40.0f);
                DrawCascadeSlider(cascade, "windDirection", EditorGUIUtility.TrTextContent("Direction", "Mean wave travel direction in degrees."), -180.0f, 180.0f);
                DrawCascadeSlider(cascade, "displacementScale", EditorGUIUtility.TrTextContent("Displacement", "Scales the spatial displacement generated by this cascade."), 0.0f, 2.0f);
                DrawCascadeSlider(cascade, "normalScale", EditorGUIUtility.TrTextContent("Normal", "Scales this cascade's spectral normal contribution independently from displacement."), 0.0f, 2.0f);

                serialized.m_ShowCascadeSpectrum = EditorGUILayout.Foldout(
                    serialized.m_ShowCascadeSpectrum,
                    "Spectrum",
                    true);
                if (serialized.m_ShowCascadeSpectrum)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawCascadeSlider(cascade, "fetchLength", EditorGUIUtility.TrTextContent("Fetch", "Sea development distance in kilometres."), 1.0f, 1000.0f);
                        DrawCascadeSlider(cascade, "swell", EditorGUIUtility.TrTextContent("Swell", "Narrows and organizes the directional distribution without adding a separate swell spectrum."), 0.0f, 2.0f);
                        DrawCascadeSlider(cascade, "spread", EditorGUIUtility.TrTextContent("Spread", "Blends the directional distribution toward isotropic."), 0.0f, 1.0f);
                        DrawCascadeSlider(cascade, "detail", EditorGUIUtility.TrTextContent("Detail", "Controls attenuation of high-frequency spectral energy."), 0.0f, 1.0f);
                    }
                }

                serialized.m_ShowCascadeBreaking = EditorGUILayout.Foldout(
                    serialized.m_ShowCascadeBreaking,
                    "Breaking",
                    true);
                if (serialized.m_ShowCascadeBreaking)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawCascadeSlider(cascade, "whitecap", EditorGUIUtility.TrTextContent("Whitecap", "Controls the deformation threshold where this cascade begins generating whitecaps."), 0.0f, 2.0f);
                        DrawCascadeSlider(cascade, "foamAmount", EditorGUIUtility.TrTextContent("Foam", "Controls whitecap growth and persistence."), 0.0f, 10.0f);
                    }
                }
            }
        }

        static internal void DrawOceanSimulationSection(WaterSurfaceEditor serialized)
        {
            DrawSurfaceMask(serialized);
            DrawCascades(serialized);
            DrawCurrentSection(serialized);
        }

        static internal void DrawPoolSimulationSection(WaterSurfaceEditor serialized)
        {
            DrawSurfaceMask(serialized);
            DrawCascades(serialized);
            DrawCurrentSection(serialized);
        }

        static internal void DrawSimulationSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            if (serialized.m_SurfaceType.hasMultipleDifferentValues)
                return;

            if (serialized.m_SimulationEnabled.hasMultipleDifferentValues || serialized.m_SimulationEnabled.boolValue)
            {
                if ((WaterSurfaceType)serialized.m_SurfaceType.enumValueIndex == WaterSurfaceType.Pool)
                    DrawPoolSimulationSection(serialized);
                else
                    DrawOceanSimulationSection(serialized);
            }
        }

        SerializedProperty m_MaterialOverride;
        SerializedProperty m_NearSmoothness;
        SerializedProperty m_FarSmoothness;
        SerializedProperty m_SmoothnessFadeStart;
        SerializedProperty m_SmoothnessFadeDistance;
        SerializedProperty m_NormalMap;
        SerializedProperty m_NormalMapScale;
        SerializedProperty m_NormalMapScrollSpeed;
        SerializedProperty m_NormalMapStrength;

        SerializedProperty m_MaxRefractionDistance;
        SerializedProperty m_AbsorptionDistance;
        SerializedProperty m_RefractionColor;

        SerializedProperty m_ScatteringColor;
        SerializedProperty m_AmbientScattering;
        SerializedProperty m_ProbeLightingFadeStart;
        SerializedProperty m_ProbeLightingFadeEnd;
        SerializedProperty m_HeightScattering;
        SerializedProperty m_DisplacementScattering;
        SerializedProperty m_DirectLightTipScattering;
        SerializedProperty m_DirectLightBodyScattering;
        SerializedProperty m_DepthTintStrength;
        SerializedProperty m_ShallowDepthTint;
        SerializedProperty m_DeepDepthTint;
        SerializedProperty m_DepthTintDistance;
        SerializedProperty m_DepthTintFalloff;
        SerializedProperty m_ShorelineSmoothness;
        SerializedProperty m_ShorelineSmoothnessInfluence;
        SerializedProperty m_ShorelineSmoothnessDepthRange;
        SerializedProperty m_ScreenSpaceReflections;

        void FetchAppearanceProperties(PropertyFetcher<WaterSurface> fetcher)
        {
            m_MaterialOverride = fetcher.Find(x => x.materialOverride);
            m_NearSmoothness = fetcher.Find(x => x.nearSmoothness);
            m_FarSmoothness = fetcher.Find(x => x.farSmoothness);
            m_SmoothnessFadeStart = fetcher.Find(x => x.smoothnessFadeStart);
            m_SmoothnessFadeDistance = fetcher.Find(x => x.smoothnessFadeDistance);
            m_NormalMap = fetcher.Find(x => x.normalMap);
            m_NormalMapScale = fetcher.Find(x => x.normalMapScale);
            m_NormalMapScrollSpeed = fetcher.Find(x => x.normalMapScrollSpeed);
            m_NormalMapStrength = fetcher.Find(x => x.normalMapStrength);

            m_AbsorptionDistance = fetcher.Find(x => x.absorptionDistance);
            m_MaxRefractionDistance = fetcher.Find(x => x.maxRefractionDistance);
            m_RefractionColor = fetcher.Find(x => x.refractionColor);

            m_ScatteringColor = fetcher.Find(x => x.scatteringColor);
            m_AmbientScattering = fetcher.Find(x => x.ambientScattering);
            m_ProbeLightingFadeStart = fetcher.Find(x => x.probeLightingFadeStart);
            m_ProbeLightingFadeEnd = fetcher.Find(x => x.probeLightingFadeEnd);
            m_HeightScattering = fetcher.Find(x => x.heightScattering);
            m_DisplacementScattering = fetcher.Find(x => x.displacementScattering);
            m_DirectLightTipScattering = fetcher.Find(x => x.directLightTipScattering);
            m_DirectLightBodyScattering = fetcher.Find(x => x.directLightBodyScattering);
            m_DepthTintStrength = fetcher.Find(x => x.depthTintStrength);
            m_ShallowDepthTint = fetcher.Find(x => x.shallowDepthTint);
            m_DeepDepthTint = fetcher.Find(x => x.deepDepthTint);
            m_DepthTintDistance = fetcher.Find(x => x.depthTintDistance);
            m_DepthTintFalloff = fetcher.Find(x => x.depthTintFalloff);
            m_ShorelineSmoothness = fetcher.Find(x => x.shorelineSmoothness);
            m_ShorelineSmoothnessInfluence = fetcher.Find(x => x.shorelineSmoothnessInfluence);
            m_ShorelineSmoothnessDepthRange = fetcher.Find(x => x.shorelineSmoothnessDepthRange);
            m_ScreenSpaceReflections = fetcher.Find(x => x.screenSpaceReflections);
        }

        static void DrawMaterialOverrideField(WaterSurfaceEditor serialized)
        {
            EditorGUILayout.PropertyField(serialized.m_MaterialOverride, k_MaterialOverride);

            Material material = serialized.m_MaterialOverride.objectReferenceValue as Material;
            if (material != null && (material.shader == null
                || !WaterMaterialResolver.TryResolve(material, false, false, out _, out _)))
                EditorGUILayout.HelpBox("Water only works with materials authored for the water shader.", MessageType.Error);

            EditorGUILayout.Space();
        }

        static internal void DrawAppearanceSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            DrawMaterialOverrideField(serialized);

            WaterSurfaceType surfaceType = (WaterSurfaceType)serialized.m_SurfaceType.enumValueIndex;

            EditorGUILayout.LabelField("Smoothness", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();
                Vector2 remap = new Vector2(serialized.m_FarSmoothness.floatValue, serialized.m_NearSmoothness.floatValue);
                EditorGUILayout.MinMaxSlider(k_SmoothnessRange, ref remap.x, ref remap.y, 0.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    serialized.m_FarSmoothness.floatValue = remap.x;
                    serialized.m_NearSmoothness.floatValue = remap.y;
                }

                WaterPairedPropertyDrawer.DrawFloatPair(
                    k_SmoothnessFadeRange,
                    k_SmoothnessFadeStart, serialized.m_SmoothnessFadeStart,
                    k_SmoothnessFadeDistance, serialized.m_SmoothnessFadeDistance);

                EditorGUILayout.PropertyField(serialized.m_ShorelineSmoothnessInfluence, k_ShorelineSmoothnessInfluence);
                if (serialized.m_ShorelineSmoothnessInfluence.hasMultipleDifferentValues
                    || serialized.m_ShorelineSmoothnessInfluence.floatValue > 0.0f)
                {
                    EditorGUILayout.PropertyField(serialized.m_ShorelineSmoothness, k_ShorelineSmoothness);
                    EditorGUILayout.PropertyField(serialized.m_ShorelineSmoothnessDepthRange, k_ShorelineSmoothnessDepthRange);
                }
            }

            EditorGUILayout.LabelField("Normal Map", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_NormalMap, k_NormalMap);
                EditorGUILayout.PropertyField(serialized.m_NormalMapStrength, k_NormalMapStrength);
                if (serialized.m_NormalMap.objectReferenceValue != null)
                {
                    EditorGUILayout.PropertyField(serialized.m_NormalMapScale, k_NormalMapScale);
                    EditorGUILayout.PropertyField(serialized.m_NormalMapScrollSpeed, k_NormalMapScrollSpeed);
                }
            }

            EditorGUILayout.LabelField("Refraction", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                CoreEditorUtils.ColorFieldLinear(serialized.m_RefractionColor, k_RefractionColor);
                EditorGUILayout.PropertyField(serialized.m_MaxRefractionDistance, k_MaxRefractionDistance);
                EditorGUILayout.PropertyField(serialized.m_AbsorptionDistance, k_AbsorptionDistance);
            }

            EditorGUILayout.LabelField("Scattering", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                CoreEditorUtils.ColorFieldLinear(serialized.m_ScatteringColor, k_ScatteringColor);
                EditorGUILayout.PropertyField(serialized.m_AmbientScattering, k_AmbientScattering);
                EditorGUILayout.PropertyField(serialized.m_HeightScattering, k_HeightScattering);
                EditorGUILayout.PropertyField(serialized.m_DisplacementScattering, k_DisplacementScattering);

                if (surfaceType != WaterSurfaceType.Pool)
                    EditorGUILayout.PropertyField(serialized.m_DirectLightTipScattering, k_DirectLightTipScattering);
                EditorGUILayout.PropertyField(serialized.m_DirectLightBodyScattering, k_DirectLightBodyScattering);
            }

            EditorGUILayout.LabelField("Probe Lighting", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_ProbeLightingFadeStart, k_ProbeLightingFadeStart);
                EditorGUILayout.PropertyField(serialized.m_ProbeLightingFadeEnd, k_ProbeLightingFadeEnd);
            }

            EditorGUILayout.LabelField("Surface Color", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_DepthTintStrength, k_DepthTintStrength);
                if (serialized.m_DepthTintStrength.hasMultipleDifferentValues
                    || serialized.m_DepthTintStrength.floatValue > 0.0f)
                {
                    CoreEditorUtils.ColorFieldLinear(serialized.m_ShallowDepthTint, k_ShallowDepthTint);
                    CoreEditorUtils.ColorFieldLinear(serialized.m_DeepDepthTint, k_DeepDepthTint);
                    EditorGUILayout.PropertyField(serialized.m_DepthTintDistance, k_DepthTintDistance);
                    EditorGUILayout.PropertyField(serialized.m_DepthTintFalloff, k_DepthTintFalloff);
                }
            }

            EditorGUILayout.LabelField("Reflections", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
                EditorGUILayout.PropertyField(serialized.m_ScreenSpaceReflections, k_ScreenSpaceReflections);
        }

        SerializedProperty m_CausticsEnabled;
        SerializedProperty m_CausticsStrength;
        SerializedProperty m_CausticsDirectionalShadowDimmer;
        SerializedProperty m_CausticsScale;
        SerializedProperty m_CausticsScrollSpeed;
        SerializedProperty m_CausticsTextureAverage;
        SerializedProperty m_CausticsFocalDepth;
        SerializedProperty m_CausticsDepthOfField;
        SerializedProperty m_CausticsDistortionStrength;
        SerializedProperty m_CausticsDistortionScale;

        void FetchCausticsProperties(PropertyFetcher<WaterSurface> o)
        {
            m_CausticsEnabled = o.Find(x => x.causticsEnabled);
            m_CausticsStrength = o.Find(x => x.causticsStrength);
            m_CausticsDirectionalShadowDimmer = o.Find(x => x.causticsDirectionalShadowDimmer);
            m_CausticsScale = o.Find(x => x.causticsScale);
            m_CausticsScrollSpeed = o.Find(x => x.causticsScrollSpeed);
            m_CausticsTextureAverage = o.Find(x => x.causticsTextureAverage);
            m_CausticsFocalDepth = o.Find(x => x.causticsFocalDepth);
            m_CausticsDepthOfField = o.Find(x => x.causticsDepthOfField);
            m_CausticsDistortionStrength = o.Find(x => x.causticsDistortionStrength);
            m_CausticsDistortionScale = o.Find(x => x.causticsDistortionScale);
        }

        static internal void DrawCausticsSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            EditorGUILayout.PropertyField(serialized.m_CausticsEnabled, EditorGUIUtility.TrTextContent("Enabled"));
            if (serialized.m_CausticsEnabled.hasMultipleDifferentValues || serialized.m_CausticsEnabled.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serialized.m_CausticsStrength, EditorGUIUtility.TrTextContent("Strength"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsDirectionalShadowDimmer, EditorGUIUtility.TrTextContent("Directional Shadow Dimmer"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsScale, EditorGUIUtility.TrTextContent("Scale"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsScrollSpeed, EditorGUIUtility.TrTextContent("Scroll Speed"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsTextureAverage, EditorGUIUtility.TrTextContent("Grey Point"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsFocalDepth, EditorGUIUtility.TrTextContent("Focal Depth"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsDepthOfField, EditorGUIUtility.TrTextContent("Depth Of Field"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsDistortionStrength, EditorGUIUtility.TrTextContent("Distortion Strength"));
                    EditorGUILayout.PropertyField(serialized.m_CausticsDistortionScale, EditorGUIUtility.TrTextContent("Distortion Scale"));
                }
            }
        }

        SerializedProperty m_FoamEnabled;
        SerializedProperty m_FoamResolution;
        SerializedProperty m_FoamSimulationFrequency;
        SerializedProperty m_FoamPrewarmingEnabled;
        SerializedProperty m_FoamSmoothness;
        SerializedProperty m_FoamTexture;
        SerializedProperty m_FoamTextureTiling;
        SerializedProperty m_FoamFeather;
        SerializedProperty m_FoamNormalStrength;
        SerializedProperty m_FoamColor;
        SerializedProperty m_WaveFoamMinimumLod;
        SerializedProperty m_DeepFoamColor;
        SerializedProperty m_DeepFoamStrength;
        SerializedProperty m_BubbleFoamColor;
        SerializedProperty m_BubbleFoamStrength;
        SerializedProperty m_BubbleFoamParallax;
        SerializedProperty m_ShorelineFoamStrength;
        SerializedProperty m_ShorelineFoamDistance;
        SerializedProperty m_ShorelineFoamWaves;
        SerializedProperty m_ShorelineFoamSpeed;
        SerializedProperty m_ShorelineFoamNoiseIntensity;
        SerializedProperty m_ShorelineFoamNoiseScale;
        SerializedProperty m_ShorelineFoamNoiseMap;

        void FetchFoamProperties(PropertyFetcher<WaterSurface> o)
        {
            m_FoamEnabled = o.Find(x => x.foamEnabled);
            m_FoamResolution = o.Find(x => x.foamResolution);
            m_FoamSimulationFrequency = o.Find(x => x.foamSimulationFrequency);
            m_FoamPrewarmingEnabled = o.Find(x => x.foamPrewarmingEnabled);
            m_FoamSmoothness = o.Find(x => x.foamSmoothness);
            m_FoamTexture = o.Find(x => x.foamTexture);
            m_FoamTextureTiling = o.Find(x => x.foamTextureTiling);
            m_FoamFeather = o.Find(x => x.foamFeather);
            m_FoamNormalStrength = o.Find(x => x.foamNormalStrength);
            m_FoamColor = o.Find(x => x.foamColor);
            m_WaveFoamMinimumLod = o.Find(x => x.waveFoamMinimumLod);
            m_DeepFoamColor = o.Find(x => x.deepFoamColor);
            m_DeepFoamStrength = o.Find(x => x.deepFoamStrength);
            m_BubbleFoamColor = o.Find(x => x.bubbleFoamColor);
            m_BubbleFoamStrength = o.Find(x => x.bubbleFoamStrength);
            m_BubbleFoamParallax = o.Find(x => x.bubbleFoamParallax);
            m_ShorelineFoamStrength = o.Find(x => x.shorelineFoamStrength);
            m_ShorelineFoamDistance = o.Find(x => x.shorelineFoamDistance);
            m_ShorelineFoamWaves = o.Find(x => x.shorelineFoamWaves);
            m_ShorelineFoamSpeed = o.Find(x => x.shorelineFoamSpeed);
            m_ShorelineFoamNoiseIntensity = o.Find(x => x.shorelineFoamNoiseIntensity);
            m_ShorelineFoamNoiseScale = o.Find(x => x.shorelineFoamNoiseScale);
            m_ShorelineFoamNoiseMap = o.Find(x => x.shorelineFoamNoiseMap);
        }

        static internal void DrawFoamSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                CoreEditorUtils.ColorFieldLinear(serialized.m_FoamColor, k_FoamColor);
                EditorGUILayout.PropertyField(serialized.m_FoamSmoothness, k_FoamSmoothness);
                EditorGUILayout.PropertyField(serialized.m_FoamTexture, k_FoamTexture);
                EditorGUILayout.PropertyField(serialized.m_FoamTextureTiling, k_FoamTextureTiling);
                EditorGUILayout.PropertyField(serialized.m_FoamFeather);
                EditorGUILayout.PropertyField(serialized.m_FoamNormalStrength);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shoreline", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_ShorelineFoamStrength, EditorGUIUtility.TrTextContent("Strength"));
                if (serialized.m_ShorelineFoamStrength.hasMultipleDifferentValues
                    || serialized.m_ShorelineFoamStrength.floatValue > 0.0f)
                {
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamDistance, EditorGUIUtility.TrTextContent("Distance"));
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamWaves, EditorGUIUtility.TrTextContent("Breakup"));
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamSpeed, EditorGUIUtility.TrTextContent("Speed"));
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamNoiseIntensity, EditorGUIUtility.TrTextContent("Noise Intensity"));
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamNoiseScale, EditorGUIUtility.TrTextContent("Noise Scale"));
                    EditorGUILayout.PropertyField(serialized.m_ShorelineFoamNoiseMap, EditorGUIUtility.TrTextContent("Noise Map"));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serialized.m_FoamEnabled, EditorGUIUtility.TrTextContent("Enabled"));
                if (serialized.m_FoamEnabled.hasMultipleDifferentValues || serialized.m_FoamEnabled.boolValue)
                {
                    EditorGUILayout.PropertyField(serialized.m_FoamResolution);
                    EditorGUILayout.PropertyField(serialized.m_FoamSimulationFrequency);
                    EditorGUILayout.PropertyField(serialized.m_FoamPrewarmingEnabled);
                    EditorGUILayout.PropertyField(serialized.m_WaveFoamMinimumLod);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Deep Foam", EditorStyles.boldLabel);
                    CoreEditorUtils.ColorFieldLinear(serialized.m_DeepFoamColor, new GUIContent("Color"));
                    EditorGUILayout.PropertyField(serialized.m_DeepFoamStrength);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Bubbles", EditorStyles.boldLabel);
                    CoreEditorUtils.ColorFieldLinear(serialized.m_BubbleFoamColor, new GUIContent("Color"));
                    EditorGUILayout.PropertyField(serialized.m_BubbleFoamStrength);
                    EditorGUILayout.PropertyField(serialized.m_BubbleFoamParallax);
                }
            }
        }

        SerializedProperty m_UnderwaterEnabled;
        SerializedProperty m_UnderwaterFarPlaneMultiplier;
        SerializedProperty m_UnderwaterDitheringEnabled;
        SerializedProperty m_UnderwaterDitheringIntensity;
        SerializedProperty m_UnderwaterExtinctionMultiplier;
        SerializedProperty m_UnderwaterSunBoost;
        SerializedProperty m_UnderwaterOutScatteringFactor;
        SerializedProperty m_UnderwaterOutScatteringExtinctionFactor;
        SerializedProperty m_UnderwaterAnisotropy;
        SerializedProperty m_UnderwaterDirectLightingMultiplier;
        SerializedProperty m_UnderwaterAmbientLightingMultiplier;
        SerializedProperty m_UnderwaterAmbientShadowInfluence;

        void FetchUnderwaterProperties(PropertyFetcher<WaterSurface> o)
        {
            m_UnderwaterEnabled = o.Find(x => x.underwaterEnabled);
            m_UnderwaterFarPlaneMultiplier = o.Find(x => x.underwaterFarPlaneMultiplier);
            m_UnderwaterDitheringEnabled = o.Find(x => x.underwaterDitheringEnabled);
            m_UnderwaterDitheringIntensity = o.Find(x => x.underwaterDitheringIntensity);
            m_UnderwaterExtinctionMultiplier = o.Find(x => x.underwaterExtinctionMultiplier);
            m_UnderwaterSunBoost = o.Find(x => x.underwaterSunBoost);
            m_UnderwaterOutScatteringFactor = o.Find(x => x.underwaterOutScatteringFactor);
            m_UnderwaterOutScatteringExtinctionFactor = o.Find(x => x.underwaterOutScatteringExtinctionFactor);
            m_UnderwaterAnisotropy = o.Find(x => x.underwaterAnisotropy);
            m_UnderwaterDirectLightingMultiplier = o.Find(x => x.underwaterDirectLightingMultiplier);
            m_UnderwaterAmbientLightingMultiplier = o.Find(x => x.underwaterAmbientLightingMultiplier);
            m_UnderwaterAmbientShadowInfluence = o.Find(x => x.underwaterAmbientShadowInfluence);
        }

        static internal void DrawUnderwaterSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            EditorGUILayout.PropertyField(serialized.m_UnderwaterEnabled, EditorGUIUtility.TrTextContent("Enabled"));
            if (serialized.m_UnderwaterEnabled.hasMultipleDifferentValues || serialized.m_UnderwaterEnabled.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterFarPlaneMultiplier, EditorGUIUtility.TrTextContent("Far Plane Multiplier"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterDitheringEnabled, EditorGUIUtility.TrTextContent("Dithering"));
                    if (serialized.m_UnderwaterDitheringEnabled.hasMultipleDifferentValues || serialized.m_UnderwaterDitheringEnabled.boolValue)
                        EditorGUILayout.PropertyField(serialized.m_UnderwaterDitheringIntensity, EditorGUIUtility.TrTextContent("Dithering Intensity"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Volume Lighting", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterExtinctionMultiplier, EditorGUIUtility.TrTextContent("Extinction Multiplier"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterSunBoost, EditorGUIUtility.TrTextContent("Sun Boost"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterOutScatteringFactor, EditorGUIUtility.TrTextContent("Out Scattering"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterOutScatteringExtinctionFactor, EditorGUIUtility.TrTextContent("Out Scattering Extinction"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterAnisotropy, EditorGUIUtility.TrTextContent("Anisotropy"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterDirectLightingMultiplier, EditorGUIUtility.TrTextContent("Direct Term"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterAmbientLightingMultiplier, EditorGUIUtility.TrTextContent("Ambient Term"));
                    EditorGUILayout.PropertyField(serialized.m_UnderwaterAmbientShadowInfluence, EditorGUIUtility.TrTextContent("Shadows Affect Ambient"));
                }
            }
        }

        SerializedProperty m_DebugMode;
        SerializedProperty m_SimulationMaskDebugMode;
        SerializedProperty m_CurrentDebugArrowDensity;

        void FetchMiscellaneousProperties(PropertyFetcher<WaterSurface> fetcher)
        {
            m_DebugMode = fetcher.Find(x => x.debugMode);
            m_SimulationMaskDebugMode = fetcher.Find(x => x.simulationMaskDebugMode);
            m_CurrentDebugArrowDensity = fetcher.Find(x => x.currentDebugArrowDensity);
        }

        static internal void DrawRenderingMode(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            WaterSurfaceType surfaceType = (WaterSurfaceType)serialized.m_SurfaceType.enumValueIndex;
            using (new BoldLabelScope())
                EditorGUILayout.PropertyField(serialized.m_DebugMode, k_DebugMode);

            WaterDebugMode debugMode = (WaterDebugMode)serialized.m_DebugMode.intValue;
            if (debugMode == WaterDebugMode.SimulationMask)
            {
                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.PropertyField(serialized.m_SimulationMaskDebugMode, k_SimulationMaskDebugMode);

                WaterSimulationMaskDebugMode maskMode = (WaterSimulationMaskDebugMode)serialized.m_SimulationMaskDebugMode.enumValueIndex;
                switch (maskMode)
                {
                    case WaterSimulationMaskDebugMode.RedChannel:
                        EditorGUILayout.HelpBox(
                            "The Red channel of the simulation mask attenuates the first wave cascade.",
                            MessageType.Info, true);
                        break;
                    case WaterSimulationMaskDebugMode.GreenChannel:
                        EditorGUILayout.HelpBox(
                            "The Green channel of the simulation mask attenuates the second wave cascade.",
                            MessageType.Info, true);
                        break;
                    case WaterSimulationMaskDebugMode.BlueChannel:
                        EditorGUILayout.HelpBox(
                            "The Blue channel of the simulation mask attenuates the third wave cascade.",
                            MessageType.Info, true);
                        break;
                }
            }
            else if (debugMode == WaterDebugMode.Current)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (surfaceType != WaterSurfaceType.Pool)
                    EditorGUILayout.PropertyField(serialized.m_CurrentDebugArrowDensity, k_CurrentDebugArrowDensity);
                    serialized.m_CurrentDebugArrowDensity.floatValue = Mathf.Max(serialized.m_CurrentDebugArrowDensity.floatValue, 0.1f);
                }
            }
        }

        static internal void DrawMiscellaneousSection(WaterSurfaceEditor serialized, UnityEditor.Editor owner)
        {
            DrawRenderingMode(serialized, owner);
        }
    }

    sealed class WaterSurfaceUI
    {
        public static readonly CED.IDrawer Inspector;

        public static readonly GUIContent generalHeader = EditorGUIUtility.TrTextContent("General");
        public static readonly GUIContent simulationHeader = EditorGUIUtility.TrTextContent("Simulation");
        public static readonly GUIContent appearanceHeader = EditorGUIUtility.TrTextContent("Appearance");
        public static readonly GUIContent causticsHeader = EditorGUIUtility.TrTextContent("Caustics");
        public static readonly GUIContent foamHeader = EditorGUIUtility.TrTextContent("Foam");
        public static readonly GUIContent underwaterHeader = EditorGUIUtility.TrTextContent("Underwater");
        public static readonly GUIContent miscellaneousHeader = EditorGUIUtility.TrTextContent("Miscellaneous");

        enum Expandable
        {
            General = 1 << 0,
            Simulation = 1 << 1,
            Caustics = 1 << 2,
            Appearance = 1 << 3,
            Foam = 1 << 4,
            Miscellaneous = 1 << 5,
            Underwater = 1 << 6,
        }

        internal enum AdditionalProperties
        {
            General = 1 << 0,
            Simulation = 1 << 1,
            Appearance = 1 << 3,
        }

        static readonly ExpandedState<Expandable, WaterSurface> k_ExpandedState = new ExpandedState<Expandable, WaterSurface>(0, "Water");
        internal static readonly AdditionalPropertiesState<AdditionalProperties, WaterSurface> k_AdditionalPropertiesState = new AdditionalPropertiesState<AdditionalProperties, WaterSurface>(0, "Water");

        [MenuItem("CONTEXT/WaterSurface/Open Preferences > Graphics...", false, 100)]
        static void ShowAllAdditionalProperties(MenuCommand menuCommand)
        {
            CoreRenderPipelinePreferences.Open();
        }

        static WaterSurfaceUI()
        {
            var emptyDrawer = CED.Group((s, e) => { });

            Inspector = CED.Group(
                CED.AdditionalPropertiesFoldoutGroup(generalHeader, Expandable.General, k_ExpandedState,
                    AdditionalProperties.General, k_AdditionalPropertiesState, CED.Group(WaterSurfaceEditor.DrawGeneralSection), emptyDrawer),
                CED.AdditionalPropertiesFoldoutGroup(simulationHeader, Expandable.Simulation, k_ExpandedState,
                    AdditionalProperties.Simulation, k_AdditionalPropertiesState, CED.Group(WaterSurfaceEditor.DrawSimulationSection), emptyDrawer),
                CED.AdditionalPropertiesFoldoutGroup(appearanceHeader, Expandable.Appearance, k_ExpandedState,
                    AdditionalProperties.Appearance, k_AdditionalPropertiesState, CED.Group(WaterSurfaceEditor.DrawAppearanceSection), emptyDrawer),
                CED.FoldoutGroup(causticsHeader, Expandable.Caustics, k_ExpandedState, WaterSurfaceEditor.DrawCausticsSection),
                CED.FoldoutGroup(foamHeader, Expandable.Foam, k_ExpandedState, WaterSurfaceEditor.DrawFoamSection),
                CED.FoldoutGroup(underwaterHeader, Expandable.Underwater, k_ExpandedState, WaterSurfaceEditor.DrawUnderwaterSection),
                CED.FoldoutGroup(miscellaneousHeader, Expandable.Miscellaneous, k_ExpandedState, WaterSurfaceEditor.DrawMiscellaneousSection)
            );
        }
    }
}
