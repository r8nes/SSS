#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akila.FPSFramework.Internal
{
    [CustomPropertyDrawer(typeof(Akila.FPSFramework.Audio))]
    internal class AudioPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProp = property.FindPropertyRelative("audioProfile");

            // Uses the field name from the MonoBehaviour
            label.text = property.displayName;

            EditorGUI.PropertyField(position, valueProp, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
#endif