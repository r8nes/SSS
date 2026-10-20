#if UNITY_EDITOR
using UnityEditor;
namespace Akila.FPSFramework.Animation
{

    [CustomEditor(typeof(ProceduralAnimation))]
    internal class ProceduralAnimationEditor : Editor
    {
        SerializedProperty Name;
        SerializedProperty length;
        SerializedProperty weight;
        SerializedProperty isolationMode;
        SerializedProperty updateMode;
        SerializedProperty triggerType;
        SerializedProperty triggerInputAction;
        SerializedProperty eventsProp;
        SerializedProperty customEvents;
        SerializedProperty connections;
        SerializedProperty optionsProp;

        void OnEnable()
        {
            Name = serializedObject.FindProperty("Name");
            length = serializedObject.FindProperty("length");
            weight = serializedObject.FindProperty("weight");

            isolationMode = serializedObject.FindProperty("isolationMode");
            updateMode = serializedObject.FindProperty("isolationUpdateMode");

            triggerType = serializedObject.FindProperty("triggerType");
            triggerInputAction = serializedObject.FindProperty("triggerInputAction");

            eventsProp = serializedObject.FindProperty("events");
            customEvents = serializedObject.FindProperty("customEvents");
            connections = serializedObject.FindProperty("connections");

            optionsProp = serializedObject.FindProperty("options");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var animation = (ProceduralAnimation)target;

            Undo.RecordObject(animation, $"Modified {animation}");
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Base", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(Name);
            EditorGUILayout.PropertyField(length);

            animation.weight = EditorGUILayout.Slider("Weight", animation.weight, 0f, 1f);

            DrawOptionsField();

            if (animation.Isolate)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(isolationMode);
                EditorGUILayout.PropertyField(updateMode);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (animation.IsOverridden)
            {
                EditorGUILayout.HelpBox(
                    "This animation will not trigger automatically because trigger type is set to None.\n" +
                    "It will only play if triggered manually from external scripts.",
                    MessageType.Info
                );
            }

            EditorGUI.BeginDisabledGroup(animation.IsOverridden);
            EditorGUILayout.PropertyField(triggerType);

            if (!animation.IsOverridden && animation.triggerType != ProceduralAnimation.TriggerType.None)
            {
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(triggerInputAction);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(eventsProp);

            // DEFAULT UNITY LISTS (native foldout + element drawing)
            EditorGUILayout.PropertyField(customEvents, true);
            EditorGUILayout.PropertyField(connections, true);

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(animation);
        }

        void DrawOptionsField()
        {
            var enumType = typeof(ProceduralAnimation.AnimationOptions);
            string[] names = System.Enum.GetNames(enumType);
            int currentMask = optionsProp.intValue;

            int newMask = EditorGUILayout.MaskField("Options", currentMask, names);

            if (newMask != currentMask)
                optionsProp.intValue = newMask;
        }
    }
}
#endif