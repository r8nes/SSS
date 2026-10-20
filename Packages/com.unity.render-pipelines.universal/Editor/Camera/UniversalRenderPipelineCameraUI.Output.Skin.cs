using UnityEngine;

namespace UnityEditor.Rendering.Universal
{
    static partial class UniversalRenderPipelineCameraUI
    {
        public partial class Output
        {
            public class Styles
            {
#if ENABLE_VR && ENABLE_XR_MODULE
                public static GUIContent[] xrTargetEyeOptions =
                {
                    EditorGUIUtility.TrTextContent("None"),
                    EditorGUIUtility.TrTextContent("Both"),
                };

                public static int[] xrTargetEyeValues = { 0, 1 };
                public static readonly GUIContent xrTargetEye = EditorGUIUtility.TrTextContent("Target Eye",
                    "Allows XR rendering if target eye sets to both eye. Disable XR for this camera otherwise.");
#endif
                // Using the pipeline Settings
                public static GUIContent[] displayedCameraOptions =
                {
                    EditorGUIUtility.TrTextContent("Off"),
                    EditorGUIUtility.TrTextContent("Use settings from Render Pipeline Asset"),
                };
                
                public static int[] cameraOptions = { 0, 1 };
                
                // Using the project settings
                public static GUIContent[] hdrOuputOptions =
                {
                    EditorGUIUtility.TrTextContent("Off"),
                    EditorGUIUtility.TrTextContent("Use Project Settings"),
                };
                public static int[] hdrOuputValues = { 0, 1 };

                public static readonly GUIContent targetTextureLabel = EditorGUIUtility.TrTextContent("Output Texture", "The texture to render this camera into, if none then this camera renders to screen.");

                public static string inspectorOverlayCameraText = L10n.Tr("Inspector Overlay Camera");
                public static readonly GUIContent allowDynamicResolution = EditorGUIUtility.TrTextContent("URP Dynamic Resolution", "Whether to support URP dynamic resolution.");
                public static readonly GUIContent allowHDROutput = EditorGUIUtility.TrTextContent("HDR Output", "Whether to support outputting to HDR displays.");

            }
        }
    }
}
