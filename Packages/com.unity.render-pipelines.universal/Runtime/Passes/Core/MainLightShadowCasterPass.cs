using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    public partial class MainLightShadowCasterPass : ScriptableRenderPass
    {
        internal RTHandle m_MainLightShadowmapTexture;
        
        private int m_RenderTargetWidth;
        private int m_RenderTargetHeight;
        private int m_ShadowCasterCascadesCount;
        private bool m_CreateEmptyShadowmap;
        private bool m_SetKeywordForEmptyShadowmap;
        private float m_CascadeBorder;
        private float m_MaxShadowDistanceSq;
        private RenderTextureDescriptor m_MainLightShadowDescriptor;
        private readonly Vector4[] m_CascadeSplitDistances;
        private readonly Matrix4x4[] m_MainLightShadowMatrices;
        private readonly Vector4[] m_CascadeOffsetScales;
        private readonly Vector4[] m_DirLightPcssParams0;
        private readonly Vector4[] m_DirLightPcssParams1;
        private readonly Vector4[] m_DirLightPcssProjectionVectors;
        private readonly ProfilingSampler m_ProfilingSetupSampler = new("Setup Main Shadowmap");
        private readonly ShadowSliceData[] m_CascadeSlices;

        private const int k_EmptyShadowMapDimensions = 1;
        private const int k_MaxCascades = 4;
        private const int k_ShadowmapBufferBits = 16;
        private const string k_MainLightShadowMapTextureName = "_MainLightShadowmapTexture";
        private const float k_DirectionalWorldTexelDiagonal = 1.41421356237f;

        // HDRP stores cascade transition widths in squared-distance space because the shader
        // evaluates distanceSquared / radiusSquared. URP exposes one cascade-border value;
        // keep the existing settings layout and apply that authored value to every active split.
        private static float InterCascadeToSquaredRangeBorder(float interCascadeBorder, float previousCascadeRange, float cascadeRange)
        {
            if (cascadeRange <= 0.0f)
                return 0.0f;

            float rangeBorder = (cascadeRange - previousCascadeRange) * Mathf.Clamp01(interCascadeBorder) / cascadeRange;
            return 1.0f - (1.0f - rangeBorder) * (1.0f - rangeBorder);
        }

        private static float GetCascadeRelativeRange(int cascadeIndex, int cascadeCount, Vector3 cascadeSplits)
        {
            if (cascadeIndex >= cascadeCount - 1)
                return 1.0f;

            return cascadeIndex switch
            {
                0 => cascadeSplits.x,
                1 => cascadeSplits.y,
                2 => cascadeSplits.z,
                _ => 1.0f
            };
        }

        private static Vector4 GetCascadeSphereWithBorder(Vector4 cullingSphere, int cascadeIndex, int cascadeCount,
            Vector3 cascadeSplits, float interCascadeBorder)
        {
            float cascadeRange = GetCascadeRelativeRange(cascadeIndex, cascadeCount, cascadeSplits);
            float previousRange = cascadeIndex > 0
                ? GetCascadeRelativeRange(cascadeIndex - 1, cascadeCount, cascadeSplits)
                : 0.0f;

            cullingSphere.w = InterCascadeToSquaredRangeBorder(interCascadeBorder, previousRange, cascadeRange);
            return cullingSphere;
        }

        private static Vector4 s_EmptyShadowParams = new(0f, 0f, 1f, 0f);

        private static readonly Vector4 s_EmptyShadowmapSize = new(k_EmptyShadowMapDimensions,
            1f / k_EmptyShadowMapDimensions, k_EmptyShadowMapDimensions, k_EmptyShadowMapDimensions);
        
        private static class MainLightShadowConstantBuffer
        {
            public static readonly int _WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            public static readonly int _ShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            public static readonly int _CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            public static readonly int _CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            public static readonly int _CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            public static readonly int _CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");

            public static readonly int _CascadeShadowSplitSphereRadii =
                Shader.PropertyToID("_CascadeShadowSplitSphereRadii");

            public static readonly int _ShadowOffset0 = Shader.PropertyToID("_MainLightShadowOffset0");
            public static readonly int _ShadowOffset1 = Shader.PropertyToID("_MainLightShadowOffset1");
            public static readonly int _ShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");
            public static readonly int _MainLightShadowmapID = Shader.PropertyToID(k_MainLightShadowMapTextureName);
            public static readonly int _MainLightShadowmapRawID = Shader.PropertyToID("_MainLightShadowmapTextureRaw");
            public static readonly int _PCSSShadowsEnabled = Shader.PropertyToID("_PCSSShadowsEnabled");
            public static readonly int _CascadeOffsetScales = Shader.PropertyToID("_CascadeOffsetScales");
            public static readonly int _DirLightPcssParams0 = Shader.PropertyToID("_DirLightPcssParams0");
            public static readonly int _DirLightPcssParams1 = Shader.PropertyToID("_DirLightPcssParams1");
            public static readonly int _DirLightPcssProjs = Shader.PropertyToID("_DirLightPcssProjs");
            public static readonly int _MainLightBlockerTapCount = Shader.PropertyToID("_MainLightBlockerTapCount");
            public static readonly int _PcfSampleCount = Shader.PropertyToID("_PcfSampleCount");
            public static readonly int _PCSSFrameIndex = Shader.PropertyToID("_PCSSFrameIndex");
            public static readonly int _MainLightShadowTint = Shader.PropertyToID("_MainLightShadowTint");
        }

        private class PassData
        {
            internal bool emptyShadowmap;
            internal bool setKeywordForEmptyShadowmap;
            internal UniversalRenderingData renderingData;
            internal UniversalCameraData cameraData;
            internal UniversalLightData lightData;
            internal UniversalShadowData shadowData;
            internal MainLightShadowCasterPass pass;
            internal TextureHandle shadowmapTexture;
            internal readonly RendererList[] shadowRendererLists = new RendererList[k_MaxCascades];
            internal readonly RendererListHandle[] shadowRendererListsHandle = new RendererListHandle[k_MaxCascades];
        }
        
        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            profilingSampler = new ProfilingSampler("Draw Main Light Shadowmap");
            renderPassEvent = evt;

            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
            m_CascadeSplitDistances = new Vector4[k_MaxCascades];
            m_CascadeOffsetScales = new Vector4[k_MaxCascades];
            m_DirLightPcssParams0 = new Vector4[k_MaxCascades];
            m_DirLightPcssParams1 = new Vector4[k_MaxCascades];
            m_DirLightPcssProjectionVectors = new Vector4[k_MaxCascades];
        }
        
        public void Dispose() => m_MainLightShadowmapTexture?.Release();
        
        public bool Setup(ref RenderingData renderingData)
        {
            var frameData = renderingData.frameData;
            var universalRenderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var shadowData = frameData.Get<UniversalShadowData>();
            return Setup(universalRenderingData, cameraData, lightData, shadowData);
        }
        
        public bool Setup(UniversalRenderingData renderingData, UniversalCameraData cameraData,
            UniversalLightData lightData, UniversalShadowData shadowData)
        {
            bool shadowsEnabled = shadowData.mainLightShadowsEnabled;
            bool shadowsSupported = shadowData.supportsMainLightShadows;

#if UNITY_EDITOR
            if (CoreUtils.IsSceneLightingDisabled(cameraData.camera))
                return false;
#endif

            using var profScope = new ProfilingScope(m_ProfilingSetupSampler);

            bool stripShadowsOffVariants = cameraData.renderer.stripShadowsOffVariants;

            Clear();

            int shadowLightIndex = lightData.mainLightIndex;

            if (shadowLightIndex == -1 || (cameraData.camera.targetTexture &&
                                           cameraData.camera.targetTexture.format == RenderTextureFormat.Depth))
            {
                if (shadowsEnabled)
                    return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, null, cameraData,
                        shadowData);

                return false;
            }

            var shadowLight = lightData.visibleLights[shadowLightIndex];
            var light = shadowLight.light;
            if (shadowsSupported && light.shadows == LightShadows.None)
                return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, light, cameraData, shadowData);

            if (!shadowsEnabled)
            {
                // If (realtime) shadows are disabled, but the light casts baked shadows, we need to do empty rendering to setup the _MainLightShadowParams uniform,
                // which is also used when sampling baked shadows. This allows for using baked shadows even when realtime shadows are completely disabled.
                if (light.shadows != LightShadows.None &&
                    light.bakingOutput.isBaked &&
                    light.bakingOutput.mixedLightingMode != MixedLightingMode.IndirectOnly &&
                    light.bakingOutput.lightmapBakeType == LightmapBakeType.Mixed)
                {
                    return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, light, cameraData,
                        shadowData);
                }

                return false;
            }

            if (!shadowsSupported)
                return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, null, cameraData, shadowData);

            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }

            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out var _))
                return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, light, cameraData, shadowData);

            m_ShadowCasterCascadesCount = shadowData.mainLightShadowCascadesCount;
            m_RenderTargetWidth = shadowData.mainLightRenderTargetWidth;
            m_RenderTargetHeight = shadowData.mainLightRenderTargetHeight;

            ref readonly var shadowCullingInfos =
                ref shadowData.visibleLightsShadowCullingInfos.UnsafeElementAt(shadowLightIndex);

            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                ref readonly var sliceData = ref shadowCullingInfos.slices.UnsafeElementAt(cascadeIndex);

                m_CascadeSplitDistances[cascadeIndex] = sliceData.splitData.cullingSphere;
                m_CascadeSlices[cascadeIndex] = sliceData;

                if (!shadowCullingInfos.IsSliceValid(cascadeIndex))
                    return SetupForEmptyRendering(stripShadowsOffVariants, shadowsEnabled, light, cameraData,
                        shadowData);
            }

            UpdateTextureDescriptorIfNeeded();

            m_MaxShadowDistanceSq = cameraData.maxShadowDistance * cameraData.maxShadowDistance;
            m_CascadeBorder = shadowData.mainLightShadowCascadeBorder;
            m_CreateEmptyShadowmap = false;

            return true;
        }


        private void UpdateTextureDescriptorIfNeeded()
        {
            if (m_MainLightShadowDescriptor.width != m_RenderTargetWidth
                || m_MainLightShadowDescriptor.height != m_RenderTargetHeight
                || m_MainLightShadowDescriptor.depthBufferBits != k_ShadowmapBufferBits
                || m_MainLightShadowDescriptor.colorFormat != RenderTextureFormat.Shadowmap)
            {
                m_MainLightShadowDescriptor = new RenderTextureDescriptor(m_RenderTargetWidth, m_RenderTargetHeight,
                    RenderTextureFormat.Shadowmap, k_ShadowmapBufferBits);
            }
        }

        private bool SetupForEmptyRendering(bool stripShadowsOffVariants, bool shadowsEnabled, Light light,
            UniversalCameraData cameraData, UniversalShadowData shadowData)
        {
            if (!stripShadowsOffVariants)
                return false;

            m_CreateEmptyShadowmap = true;
            m_SetKeywordForEmptyShadowmap = shadowsEnabled;

            // Even though there are not real-time shadows, the light might be using shadowmasks,
            // which is why we need to update the shadow parameters, for example so shadow strength can be used.

            if (!light)
            {
                s_EmptyShadowParams = new Vector4(0, 0, 1, 0);
            }
            else
            {
                bool supportsSoftShadows = shadowData.supportsSoftShadows;

                float maxShadowDistanceSq = cameraData.maxShadowDistance;
                float mainLightShadowCascadeBorder = shadowData.mainLightShadowCascadeBorder;

                bool softShadows = ShadowUtils.IsSoftShadowFilteringSupportedForLight(light) && supportsSoftShadows;
                float softShadowsProp = ShadowUtils.SoftShadowQualityToShaderProperty(light, softShadows);

                ShadowUtils.GetScaleAndBiasForLinearDistanceFade(maxShadowDistanceSq, mainLightShadowCascadeBorder,
                    out float shadowFadeScale, out float shadowFadeBias);

                s_EmptyShadowParams =
                    new Vector4(light.shadowStrength, softShadowsProp, shadowFadeScale, shadowFadeBias);
            }

            return true;
        }

        private void Clear()
        {
            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < m_CascadeSplitDistances.Length; ++i)
                m_CascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
        }

        internal static void SetShadowParamsForEmptyShadowmap(RasterCommandBuffer rasterCommandBuffer)
        {
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, s_EmptyShadowmapSize);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams, s_EmptyShadowParams);
            rasterCommandBuffer.SetGlobalFloat(MainLightShadowConstantBuffer._PCSSShadowsEnabled, 0.0f);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0, Vector4.zero);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1, Vector4.zero);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2, Vector4.zero);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3, Vector4.zero);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, Vector4.zero);
            rasterCommandBuffer.SetGlobalVector(MainLightShadowConstantBuffer._MainLightShadowTint, Vector4.zero);
        }

        private void RenderMainLightCascadeShadowmap(RasterCommandBuffer cmd, ref PassData data, bool isRenderGraph)
        {
            var lightData = data.lightData;

            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            var shadowLight = lightData.visibleLights[shadowLightIndex];

            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.MainLightShadow)))
            {
                // Need to start by setting the Camera position and worldToCamera Matrix as that is not set for passes executed before normal rendering
                ShadowUtils.SetCameraPosition(cmd, data.cameraData.worldSpaceCameraPos);

                // For non-RG, need set the worldToCamera Matrix as that is not set for passes executed before normal rendering,
                // otherwise shadows will behave incorrectly when Scene and Game windows are open at the same time (UUM-63267).
                if (!isRenderGraph)
                    ShadowUtils.SetWorldToCameraAndCameraToWorldMatrices(cmd, data.cameraData.GetViewMatrix());

                for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                {
                    var shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, data.shadowData,
                        m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].resolution);

                    // Directional normal bias is evaluated on the receiver, per cascade, using
                    // HDRP's world-space texel scaling. Keep URP's authored depth component and
                    // the raster slope-scale bias used by RenderShadowSlice, but do not extrude
                    // caster vertices along their normals.
                    shadowBias.y = 0.0f;
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    cmd.SetKeyword(ShaderGlobalKeywords.CastingPunctualLightShadow, false);

                    var shadowRendererList = isRenderGraph
                        ? data.shadowRendererListsHandle[cascadeIndex]
                        : data.shadowRendererLists[cascadeIndex];
                    ShadowUtils.RenderShadowSlice(cmd, ref m_CascadeSlices[cascadeIndex], ref shadowRendererList,
                        m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].viewMatrix);
                }

                data.shadowData.isKeywordSoftShadowsEnabled =
                    ShadowUtils.IsSoftShadowFilteringSupportedForLight(shadowLight.light) &&
                    data.shadowData.supportsSoftShadows;
                cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadows,
                    data.shadowData.mainLightShadowCascadesCount == 1);
                cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowCascades,
                    data.shadowData.mainLightShadowCascadesCount > 1);
                ShadowUtils.SetSoftShadowQualityShaderKeywords(cmd, data.shadowData);

                SetupMainLightShadowReceiverConstants(cmd, ref shadowLight, shadowLightIndex, data.shadowData, data.cameraData);
            }
        }

        private void SetupMainLightShadowReceiverConstants(RasterCommandBuffer cmd, ref VisibleLight shadowLight,
            int shadowLightIndex, UniversalShadowData shadowData, UniversalCameraData cameraData)
        {
            var light = shadowLight.light;
            bool softShadows = ShadowUtils.IsSoftShadowFilteringSupportedForLight(light) &&
                               shadowData.supportsSoftShadows;

            int cascadeCount = m_ShadowCasterCascadesCount;
            for (int i = 0; i < cascadeCount; ++i)
                m_MainLightShadowMatrices[i] = m_CascadeSlices[i].shadowTransform;

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching
            var noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = SystemInfo.usesReversedZBuffer ? 1.0f : 0.0f;

            for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                m_MainLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / m_RenderTargetWidth;
            float invShadowAtlasHeight = 1.0f / m_RenderTargetHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            float softShadowsProp = ShadowUtils.SoftShadowQualityToShaderProperty(light, softShadows);

            ShadowUtils.GetScaleAndBiasForLinearDistanceFade(m_MaxShadowDistanceSq, m_CascadeBorder,
                out float shadowFadeScale, out float shadowFadeBias);

            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, m_MainLightShadowMatrices);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams,
                new Vector4(light.shadowStrength, softShadowsProp, shadowFadeScale, shadowFadeBias));

            if (m_ShadowCasterCascadesCount > 0)
            {
                // Radius squared remains in the existing radii vector. The otherwise redundant
                // sphere.w components carry HDRP-compatible squared-range transition borders.
                // A single URP authoring value drives every active cascade while the conversion
                // remains per-range, matching HDRP's shader-space representation.
                Vector3 cascadeSplits = shadowData.mainLightShadowCascadesSplit;
                Vector4 sphere0 = GetCascadeSphereWithBorder(m_CascadeSplitDistances[0], 0, cascadeCount, cascadeSplits, m_CascadeBorder);
                Vector4 sphere1 = cascadeCount > 1
                    ? GetCascadeSphereWithBorder(m_CascadeSplitDistances[1], 1, cascadeCount, cascadeSplits, m_CascadeBorder)
                    : Vector4.zero;
                Vector4 sphere2 = cascadeCount > 2
                    ? GetCascadeSphereWithBorder(m_CascadeSplitDistances[2], 2, cascadeCount, cascadeSplits, m_CascadeBorder)
                    : Vector4.zero;
                Vector4 sphere3 = cascadeCount > 3
                    ? GetCascadeSphereWithBorder(m_CascadeSplitDistances[3], 3, cascadeCount, cascadeSplits, m_CascadeBorder)
                    : Vector4.zero;

                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0, sphere0);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1, sphere1);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2, sphere2);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3, sphere3);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                    m_CascadeSplitDistances[0].w * m_CascadeSplitDistances[0].w,
                    cascadeCount > 1 ? m_CascadeSplitDistances[1].w * m_CascadeSplitDistances[1].w : 0.0f,
                    cascadeCount > 2 ? m_CascadeSplitDistances[2].w * m_CascadeSplitDistances[2].w : 0.0f,
                    cascadeCount > 3 ? m_CascadeSplitDistances[3].w * m_CascadeSplitDistances[3].w : 0.0f));
            }

            // Inside shader soft shadows are controlled through global keyword.
            // If any additional light has soft shadows it will force soft shadows on main light too.
            // As it is not trivial finding out which additional light has soft shadows, we will pass main light properties if soft shadows are supported.
            // This workaround will be removed once we will support soft shadows per light.
            if (shadowData.supportsSoftShadows)
            {
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset0,
                    new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight,
                        invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight));
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowOffset1,
                    new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight,
                        invHalfShadowAtlasWidth, invHalfShadowAtlasHeight));
            }

            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, new Vector4(invShadowAtlasWidth,
                invShadowAtlasHeight, m_RenderTargetWidth, m_RenderTargetHeight));

            SetupMainLightPCSSReceiverConstants(cmd, ref shadowLight, shadowLightIndex, shadowData, softShadows, cameraData);
        }

        private void SetupMainLightPCSSReceiverConstants(RasterCommandBuffer cmd, ref VisibleLight shadowLight,
            int shadowLightIndex, UniversalShadowData shadowData, bool softShadows, UniversalCameraData cameraData)
        {
            bool hasAdditionalData = shadowLight.light.TryGetComponent<UniversalAdditionalLightData>(out var additionalData);
            Color shadowTint = hasAdditionalData ? additionalData.shadowTint : Color.black;
            bool penumbraTint = hasAdditionalData && additionalData.penumbraTint &&
                (shadowTint.r != shadowTint.g || shadowTint.g != shadowTint.b);

            // HDRP converts only penumbra tint to a linear value before it reaches the shader.
            Vector3 shaderShadowTint = penumbraTint
                ? new Vector3(Mathf.Pow(shadowTint.r, 2.2f), Mathf.Pow(shadowTint.g, 2.2f), Mathf.Pow(shadowTint.b, 2.2f))
                : new Vector3(shadowTint.r, shadowTint.g, shadowTint.b);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._MainLightShadowTint,
                new Vector4(shaderShadowTint.x, shaderShadowTint.y, shaderShadowTint.z, penumbraTint ? 1.0f : 0.0f));

            float invShadowAtlasWidth = 1.0f / m_RenderTargetWidth;
            float invShadowAtlasHeight = 1.0f / m_RenderTargetHeight;

            for (int i = 0; i < k_MaxCascades; ++i)
            {
                m_CascadeOffsetScales[i] = Vector4.zero;
                m_DirLightPcssParams0[i] = Vector4.zero;
                m_DirLightPcssParams1[i] = Vector4.zero;
                m_DirLightPcssProjectionVectors[i] = Vector4.zero;
            }

            // Respect the effective URP bias source (pipeline setting or per-light override).
            // Reading Light.shadowNormalBias directly bypasses UniversalShadowData.bias and can
            // make adjacent cascades use an offset unrelated to the value used by the pipeline.
            float receiverNormalBias = shadowLight.light.shadowNormalBias;
            if (shadowData.bias != null && shadowLightIndex >= 0 && shadowLightIndex < shadowData.bias.Count)
                receiverNormalBias = shadowData.bias[shadowLightIndex].y;
            receiverNormalBias = Mathf.Max(receiverNormalBias, 0.0f);

            // These arrays are part of the existing fork's directional-PCSS constant layout.
            // Populate their cascade-independent fields unconditionally so hard and PCF paths
            // use the same per-cascade transforms and receiver-side bias as PCSS.
            for (int cascadeIndex = 0;
                 cascadeIndex < m_ShadowCasterCascadesCount && cascadeIndex < k_MaxCascades;
                 ++cascadeIndex)
            {
                var cascadeSlice = m_CascadeSlices[cascadeIndex];
                float sliceScaleX = cascadeSlice.resolution * invShadowAtlasWidth;
                float sliceScaleY = cascadeSlice.resolution * invShadowAtlasHeight;
                m_CascadeOffsetScales[cascadeIndex] = new Vector4(
                    cascadeSlice.offsetX * invShadowAtlasWidth,
                    cascadeSlice.offsetY * invShadowAtlasHeight,
                    sliceScaleX,
                    sliceScaleY);

                var shadowMatrix = m_MainLightShadowMatrices[cascadeIndex];
                Vector4 projectionVector = new(
                    2.0f * new Vector3(shadowMatrix.m00, shadowMatrix.m01, shadowMatrix.m02).magnitude /
                    Mathf.Max(sliceScaleX, 1e-5f),
                    2.0f * new Vector3(shadowMatrix.m10, shadowMatrix.m11, shadowMatrix.m12).magnitude /
                    Mathf.Max(sliceScaleY, 1e-5f),
                    2.0f * new Vector3(shadowMatrix.m20, shadowMatrix.m21, shadowMatrix.m22).magnitude,
                    0.0f);

                // HDRP worldTexelSize = 2 / projection.m00 / resolution * sqrt(2).
                // Store the final world-space normal offset in the otherwise-unused projection.w.
                float projectionScale = Mathf.Max(Mathf.Abs(cascadeSlice.projectionMatrix.m00), 1e-5f);
                float worldTexelSize = 2.0f / projectionScale /
                    Mathf.Max(cascadeSlice.resolution, 1) * k_DirectionalWorldTexelDiagonal;
                projectionVector.w = receiverNormalBias * worldTexelSize;
                m_DirLightPcssProjectionVectors[cascadeIndex] = projectionVector;
            }

            int frameIndex = cameraData.IsTemporalAAEnabled()
                ? TemporalAA.CalculateTaaFrameIndex(ref cameraData.taaSettings)
                : 0;
            frameIndex = Mathf.Max(frameIndex, 0);
            cmd.SetGlobalInt(MainLightShadowConstantBuffer._PCSSFrameIndex, frameIndex);

            var asset = UniversalRenderPipeline.asset;
            bool pcssEnabled =
                asset &&
                softShadows &&
                shadowData.supportsSoftShadows &&
                hasAdditionalData &&
                asset.softShadowQuality == SoftShadowQuality.High &&
                m_ShadowCasterCascadesCount > 0 &&
                m_RenderTargetWidth > 0 &&
                m_RenderTargetHeight > 0;

            cmd.SetGlobalFloat(MainLightShadowConstantBuffer._PCSSShadowsEnabled, pcssEnabled ? 1.0f : 0.0f);

            if (pcssEnabled)
            {
                float lightAngularDiameter = additionalData.softnessScale * additionalData.angularDiameter;
                float halfLightTangent = Mathf.Max(Mathf.Tan(0.5f * Mathf.Deg2Rad * lightAngularDiameter), 1e-5f);
                float maxPenumbraDistance = additionalData.dirLightMaxPenumbraSize / (2.0f * halfLightTangent);

                float minFilterAngularDiameter = Mathf.Max(additionalData.dirLightBlockerSearchAngularDiameter,
                    additionalData.dirLightMinFilterMaxAngularDiameter);
                float halfMinFilterTangent = Mathf.Max(
                    Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(minFilterAngularDiameter, lightAngularDiameter)),
                    1e-5f);
                float halfBlockerSearchTangent = Mathf.Max(
                    Mathf.Tan(0.5f * Mathf.Deg2Rad *
                        Mathf.Max(additionalData.dirLightBlockerSearchAngularDiameter, lightAngularDiameter)),
                    1e-5f);

                for (int cascadeIndex = 0;
                     cascadeIndex < m_ShadowCasterCascadesCount && cascadeIndex < k_MaxCascades;
                     ++cascadeIndex)
                {
                    Vector4 projectionVector = m_DirLightPcssProjectionVectors[cascadeIndex];
                    float projectionDepth = Mathf.Max(Mathf.Abs(projectionVector.z), 1e-5f);
                    float shadowmapDepthToRadialScale = Mathf.Max(Mathf.Abs(projectionVector.x) / projectionDepth, 1e-5f);
                    float dirLightDepthToRadius = Mathf.Max(halfLightTangent * shadowmapDepthToRadialScale, 1e-5f);

                    m_DirLightPcssParams0[cascadeIndex].x = dirLightDepthToRadius;
                    m_DirLightPcssParams0[cascadeIndex].y = 1.0f / dirLightDepthToRadius;
                    m_DirLightPcssParams0[cascadeIndex].z = maxPenumbraDistance;
                    m_DirLightPcssParams0[cascadeIndex].w = additionalData.dirLightMaxSamplingDistance;

                    m_DirLightPcssParams1[cascadeIndex].x = additionalData.dirLightMinFilterSizeTexels;
                    m_DirLightPcssParams1[cascadeIndex].y =
                        1.0f / Mathf.Max(halfMinFilterTangent * shadowmapDepthToRadialScale, 1e-5f);
                    m_DirLightPcssParams1[cascadeIndex].z =
                        1.0f / Mathf.Max(halfBlockerSearchTangent * shadowmapDepthToRadialScale, 1e-5f);
                    m_DirLightPcssParams1[cascadeIndex].w =
                        0.5f * additionalData.dirLightBlockerSamplingClumpExponent;
                }
            }

            cmd.SetGlobalVectorArray(MainLightShadowConstantBuffer._CascadeOffsetScales, m_CascadeOffsetScales);
            cmd.SetGlobalVectorArray(MainLightShadowConstantBuffer._DirLightPcssParams0, m_DirLightPcssParams0);
            cmd.SetGlobalVectorArray(MainLightShadowConstantBuffer._DirLightPcssParams1, m_DirLightPcssParams1);
            cmd.SetGlobalVectorArray(MainLightShadowConstantBuffer._DirLightPcssProjs, m_DirLightPcssProjectionVectors);
            cmd.SetGlobalInt(MainLightShadowConstantBuffer._MainLightBlockerTapCount,
                pcssEnabled ? additionalData.dirLightBlockerTapCount : 0);
            cmd.SetGlobalInt(MainLightShadowConstantBuffer._PcfSampleCount,
                pcssEnabled ? additionalData.dirLightFilterSampleCount : 0);

        }

        private void InitPassData(
            ref PassData passData,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            UniversalShadowData shadowData)
        {
            passData.pass = this;
            passData.emptyShadowmap = m_CreateEmptyShadowmap;
            passData.setKeywordForEmptyShadowmap = m_SetKeywordForEmptyShadowmap;
            passData.renderingData = renderingData;
            passData.cameraData = cameraData;
            passData.lightData = lightData;
            passData.shadowData = shadowData;
        }

        private void InitRendererLists(ref PassData passData, ScriptableRenderContext context, RenderGraph renderGraph,
            bool useRenderGraph)
        {
            int shadowLightIndex = passData.lightData.mainLightIndex;

            if (m_CreateEmptyShadowmap || shadowLightIndex == -1) return;

            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                // Directional split culling is indexed independently from the light. Build a
                // distinct settings value per list so deferred renderer-list creation cannot
                // observe a later cascade's split index.
                ShadowDrawingSettings settings = new(passData.renderingData.cullResults, shadowLightIndex)
                {
                    useRenderingLayerMaskTest = UniversalRenderPipeline.asset.useRenderingLayers,
                    splitIndex = cascadeIndex
                };

                if (useRenderGraph)
                    passData.shadowRendererListsHandle[cascadeIndex] =
                        renderGraph.CreateShadowRendererList(ref settings);
                else
                    passData.shadowRendererLists[cascadeIndex] = context.CreateShadowRendererList(ref settings);
            }
        }

        internal TextureHandle Render(RenderGraph graph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var shadowData = frameData.Get<UniversalShadowData>();

            TextureHandle shadowTexture;

            using (var builder = graph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                InitPassData(ref passData, renderingData, cameraData, lightData, shadowData);
                InitRendererLists(ref passData, default, graph, true);

                if (!m_CreateEmptyShadowmap)
                {
                    for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                        builder.UseRendererList(passData.shadowRendererListsHandle[cascadeIndex]);

                    shadowTexture = UniversalRenderer.CreateRenderGraphTexture(graph, m_MainLightShadowDescriptor,
                        k_MainLightShadowMapTextureName, true,
                        ShadowUtils.m_ForceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear);
                    builder.SetRenderAttachmentDepth(shadowTexture);
                }
                else
                {
                    shadowTexture = graph.defaultResources.defaultShadowTexture;
                }

                builder.AllowGlobalStateModification(true);

                if (shadowTexture.IsValid())
                {
                    builder.SetGlobalTextureAfterPass(shadowTexture,
                        MainLightShadowConstantBuffer._MainLightShadowmapID);
                    builder.SetGlobalTextureAfterPass(shadowTexture,
                        MainLightShadowConstantBuffer._MainLightShadowmapRawID);
                }


                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    var rasterCommandBuffer = context.cmd;

                    if (!data.emptyShadowmap)
                    {
                        data.pass.RenderMainLightCascadeShadowmap(rasterCommandBuffer, ref data, true);
                    }
                    else
                    {
                        if (data.setKeywordForEmptyShadowmap)
                            rasterCommandBuffer.EnableKeyword(ShaderGlobalKeywords.MainLightShadows);

                        SetShadowParamsForEmptyShadowmap(rasterCommandBuffer);
                    }
                });
            }


            return shadowTexture;
        }
    };
}
