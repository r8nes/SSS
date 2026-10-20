using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor
{
    /// <summary>
    /// Material drawer for the HDRP-compatible contact-shadow receiver policy.
    /// Used by shaders whose custom inspector does not derive from BaseShaderGUI.
    /// </summary>
    public sealed class ContactShadowReceiverDrawer : MaterialPropertyDrawer
    {
        static readonly GUIContent[] k_Options =
        {
            EditorGUIUtility.TrTextContent("Default"),
            EditorGUIUtility.TrTextContent("Off"),
            EditorGUIUtility.TrTextContent("Force")
        };

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            int value = Mathf.Clamp(Mathf.RoundToInt(prop.floatValue), 0, 2);
            value = EditorGUI.Popup(position, label, value, k_Options);
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = value;
                ApplyKeywords(prop);
            }
        }

        public override void Apply(MaterialProperty prop)
        {
            ApplyKeywords(prop);
        }

        static void ApplyKeywords(MaterialProperty prop)
        {
            foreach (Object target in prop.targets)
            {
                if (target is not Material material)
                    continue;

                int value = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(prop.name)), 0, 2);
                CoreUtils.SetKeyword(material, ShaderKeywordStrings._CONTACT_SHADOW_RECEIVER_OFF, value == 1);
                CoreUtils.SetKeyword(material, ShaderKeywordStrings._CONTACT_SHADOW_RECEIVER_FORCE, value == 2);
            }
        }
    }
}
