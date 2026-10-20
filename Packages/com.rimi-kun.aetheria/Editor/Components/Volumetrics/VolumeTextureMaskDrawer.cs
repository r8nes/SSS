using Aetheria.Runtime.Components.Volumetrics;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    [CustomPropertyDrawer(typeof(VolumeTextureMask))]
    internal sealed class VolumeTextureMaskDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;
            
            float line = EditorGUIUtility.singleLineHeight;
            
            var parameters = property.FindPropertyRelative("parameters");
            
            return line * 4f + Spacing * 4f + EditorGUI.GetPropertyHeight(parameters, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            float line = EditorGUIUtility.singleLineHeight;
            
            var rect = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            
            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    rect.y += line + Spacing;
                    
                    DrawLine(property.FindPropertyRelative("texture"), ref rect);
                    DrawLine(property.FindPropertyRelative("tiling"), ref rect);
                    DrawLine(property.FindPropertyRelative("scrollingSpeed"), ref rect);
                    
                    var parameters = property.FindPropertyRelative("parameters");
                    rect.height = EditorGUI.GetPropertyHeight(parameters, true);
                    
                    EditorGUI.PropertyField(rect, parameters, true);
                }
            }
            EditorGUI.EndProperty();
        }

        private static void DrawLine(SerializedProperty property, ref Rect rect)
        {
            EditorGUI.PropertyField(rect, property);
            rect.y += EditorGUIUtility.singleLineHeight + Spacing;
        }
    }
}