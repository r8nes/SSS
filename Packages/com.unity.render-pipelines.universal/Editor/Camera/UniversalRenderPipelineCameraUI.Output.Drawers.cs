using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    using CED = CoreEditorDrawer<UniversalRenderPipelineSerializedCamera>;

    static partial class UniversalRenderPipelineCameraUI
    {
        public partial class Output
        {

            public static readonly CED.IDrawer Drawer;
            static Output()
            {
                Drawer = CED.Conditional(
                    (serialized, owner) => (CameraRenderType)serialized.cameraType.intValue == CameraRenderType.Base,
                    CED.FoldoutGroup(
                        CameraUI.Output.Styles.header,
                        Expandable.Output,
                        k_ExpandedState,
                        FoldoutOption.Indent,
                        CED.Group(
                            DrawerOutputTargetTexture
                            ),
                        CED.Conditional(
                            (serialized, owner) => serialized.serializedObject.targetObject is Camera camera && camera.targetTexture == null,
                            CED.Group(
                                DrawerOutputMultiDisplay
                            )
                            ),
#if ENABLE_VR && ENABLE_XR_MODULE
                        CED.Group(DrawerOutputXRRendering),
#endif
                        CED.Group(
                            DrawerOutputNormalizedViewPort
                            ),
                        CED.Conditional(
                            (serialized, owner) => serialized.serializedObject.targetObject is Camera camera && camera.targetTexture == null,
                            CED.Group(
                                CED.Conditional(
                                    (serialized, owner) => PlayerSettings.allowHDRDisplaySupport,
                                    CED.Group(DrawerOutputHDROutput)
                                    ),
                                CED.Group(DrawerOutputAllowDynamicResolution)
                            )
                        )
                    )
                );
            }

            static void DrawerOutputMultiDisplay(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    p.baseCameraSettings.DrawMultiDisplay();
                    if (checkScope.changed)
                    {
                        UpdateStackCamerasOutput(p, camera =>
                        {
                            bool isChanged = false;
                            // Force same target display
                            int targetDisplay = p.baseCameraSettings.targetDisplay.intValue;
                            if (camera.targetDisplay != targetDisplay)
                            {
                                camera.targetDisplay = targetDisplay;
                                isChanged = true;
                            }

                            // Force same target display
                            StereoTargetEyeMask stereoTargetEye = (StereoTargetEyeMask)p.baseCameraSettings.targetEye.intValue;
                            if (camera.stereoTargetEye != stereoTargetEye)
                            {
                                camera.stereoTargetEye = stereoTargetEye;
                                isChanged = true;
                            }

                            return isChanged;
                        });
                    }
                }
            }

            static void DrawerOutputAllowDynamicResolution(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    CameraUI.Output.Drawer_Output_AllowDynamicResolution(p, owner, Styles.allowDynamicResolution);
                    if (checkScope.changed)
                    {
                        UpdateStackCamerasOutput(p, camera =>
                        {
                            bool allowDynamicResolution = p.allowDynamicResolution.boolValue;

                            if (camera.allowDynamicResolution == p.allowDynamicResolution.boolValue)
                                return false;

                            EditorUtility.SetDirty(camera);

                            camera.allowDynamicResolution = allowDynamicResolution;
                            return true;
                        });
                    }
                }
            }

            static void DrawerOutputNormalizedViewPort(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    CameraUI.Output.Drawer_Output_NormalizedViewPort(p, owner);
                    if (checkScope.changed)
                    {
                        UpdateStackCamerasOutput(p, camera =>
                        {
                            Rect rect = p.baseCameraSettings.normalizedViewPortRect.rectValue;
                            if (camera.rect != rect)
                            {
                                camera.rect = p.baseCameraSettings.normalizedViewPortRect.rectValue;
                                return true;
                            }

                            return false;
                        });
                    }
                }
            }

            static void UpdateStackCamerasOutput(UniversalRenderPipelineSerializedCamera p, Func<Camera, bool> updateOutputProperty)
            {
                int cameraCount = p.cameras.arraySize;
                for (int i = 0; i < cameraCount; ++i)
                {
                    SerializedProperty cameraProperty = p.cameras.GetArrayElementAtIndex(i);
                    Camera overlayCamera = cameraProperty.objectReferenceValue as Camera;
                    if (overlayCamera != null)
                    {
                        Undo.RecordObject(overlayCamera, Styles.inspectorOverlayCameraText);
                        if (updateOutputProperty(overlayCamera))
                            EditorUtility.SetDirty(overlayCamera);
                    }
                }
            }

            static void DrawerOutputTargetTexture(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(p.baseCameraSettings.targetTexture, Styles.targetTextureLabel);

                    var texture = p.baseCameraSettings.targetTexture.objectReferenceValue as RenderTexture;
                    if (checkScope.changed)
                    {
                        UpdateStackCamerasOutput(p, camera =>
                        {
                            if (camera.targetTexture == texture)
                                return false;

                            camera.targetTexture = texture;
                            return true;
                        });
                    }
                }
            }

#if ENABLE_VR && ENABLE_XR_MODULE
            static void DrawerOutputXRRendering(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                Rect controlRect = EditorGUILayout.GetControlRect(true);
                EditorGUI.BeginProperty(controlRect, Styles.xrTargetEye, p.allowXRRendering);
                {
                    using (var checkScope = new EditorGUI.ChangeCheckScope())
                    {
                        int selectedValue = !p.allowXRRendering.boolValue ? 0 : 1;
                        bool allowXRRendering = EditorGUI.IntPopup(controlRect, Styles.xrTargetEye, selectedValue, Styles.xrTargetEyeOptions, Styles.xrTargetEyeValues) == 1;
                        if (checkScope.changed)
                            p.allowXRRendering.boolValue = allowXRRendering;
                    }
                }
                EditorGUI.EndProperty();
            }

#endif

            static void DrawerOutputHDROutput(UniversalRenderPipelineSerializedCamera p, Editor owner)
            {
                Rect controlRect = EditorGUILayout.GetControlRect(true);
                EditorGUI.BeginProperty(controlRect, Styles.allowHDROutput, p.allowHDROutput);
                {
                    using (var checkScope = new EditorGUI.ChangeCheckScope())
                    {
                        int selectedValue = !p.allowHDROutput.boolValue ? 0 : 1;
                        var allowHDROutput = EditorGUI.IntPopup(controlRect, Styles.allowHDROutput, selectedValue, Styles.hdrOuputOptions, Styles.hdrOuputValues) == 1;

                        if (checkScope.changed)
                            p.allowHDROutput.boolValue = allowHDROutput;
                    }
                }
                EditorGUI.EndProperty();
            }

        }
    }
}
