using Aetheria.Runtime.Components.Volumetrics;
using UnityEditor;
using UnityEngine;

namespace Aetheria.Editor
{
    [CustomPropertyDrawer(typeof(VolumeMaskParameters))]
    internal sealed class VolumeMaskParametersDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            int rows = 6;
            var nearClamp = property.FindPropertyRelative("nearClampEnabled");
            
            if (nearClamp != null && (nearClamp.boolValue || nearClamp.hasMultipleDifferentValues))
                rows += 2;
            
            return (rows + 1) * EditorGUIUtility.singleLineHeight + rows * Spacing;
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
                    
                    Draw(property, "samplingMode", ref rect);
                    Draw(property, "remapMin", ref rect);
                    Draw(property, "remapMax", ref rect);
                    Draw(property, "power", ref rect);
                    Draw(property, "strength", ref rect);
                    
                    var nearClamp = Draw(property, "nearClampEnabled", ref rect);
                    
                    if (nearClamp != null && (nearClamp.boolValue || nearClamp.hasMultipleDifferentValues))
                    {
                        Draw(property, "nearClampStartDistance", ref rect);
                        Draw(property, "nearClampFadeDistance", ref rect);
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        private static SerializedProperty Draw(SerializedProperty root, string name, ref Rect rect)
        {
            var child = root.FindPropertyRelative(name);
            
            if (child == null) return null;
            
            EditorGUI.PropertyField(rect, child);
            rect.y += EditorGUIUtility.singleLineHeight + Spacing;
            
            return child;
        }
    }
}