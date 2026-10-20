using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    /// <summary>
    /// Reusable URP shadow-cascade authoring UI for external editor tooling.
    /// Uses the same ShadowCascadeGUI control as the URP Asset inspector.
    /// </summary>
    public static class UniversalShadowCascadeEditorGUI
    {
        public static void Draw(
            float shadowDistance,
            SerializedProperty cascadeCountProperty,
            SerializedProperty cascade2SplitProperty,
            SerializedProperty cascade3SplitProperty,
            SerializedProperty cascade4SplitProperty)
        {
            if (cascadeCountProperty == null ||
                cascade2SplitProperty == null ||
                cascade3SplitProperty == null ||
                cascade4SplitProperty == null)
                return;

            int cascadeCount = Mathf.Clamp(cascadeCountProperty.intValue, 1, 4);
            int splitCount = cascadeCount - 1;
            float baseMetric = Mathf.Max(0f, shadowDistance);

            Vector4 splits = GetSplits(
                cascadeCount,
                cascade2SplitProperty,
                cascade3SplitProperty,
                cascade4SplitProperty);

            DrawSplitSliders(ref splits, splitCount, baseMetric);
            DrawCascadeBar(ref splits, cascadeCount, baseMetric);
            StoreSplits(
                splits,
                cascadeCount,
                cascade2SplitProperty,
                cascade3SplitProperty,
                cascade4SplitProperty);
        }


        public static void DrawSoftShadowQuality(SerializedProperty softShadowQualityProperty)
        {
            if (softShadowQualityProperty == null)
                return;

            GUIContent label = EditorGUIUtility.TrTextContent(
                "Soft Shadow Quality",
                "Selects the default per-light tier. Low and Medium use PCF; High uses directional contact-hardening shadows.");

            GUIContent[] options =
            {
                EditorGUIUtility.TrTextContent(nameof(SoftShadowQuality.Low)),
                EditorGUIUtility.TrTextContent(nameof(SoftShadowQuality.Medium)),
                EditorGUIUtility.TrTextContent(nameof(SoftShadowQuality.High))
            };

            int[] values =
            {
                (int)SoftShadowQuality.Low,
                (int)SoftShadowQuality.Medium,
                (int)SoftShadowQuality.High
            };

            int selected = Mathf.Clamp(
                softShadowQualityProperty.intValue,
                (int)SoftShadowQuality.Low,
                (int)SoftShadowQuality.High);

            Rect rect = EditorGUILayout.GetControlRect(true);
            EditorGUI.BeginProperty(rect, label, softShadowQualityProperty);
            selected = EditorGUI.IntPopup(rect, label, selected, options, values);
            softShadowQualityProperty.intValue = selected;
            EditorGUI.EndProperty();
        }

        private static Vector4 GetSplits(
            int cascadeCount,
            SerializedProperty cascade2SplitProperty,
            SerializedProperty cascade3SplitProperty,
            SerializedProperty cascade4SplitProperty)
        {
            return cascadeCount switch
            {
                4 => new Vector4(
                    cascade4SplitProperty.vector3Value.x,
                    cascade4SplitProperty.vector3Value.y,
                    cascade4SplitProperty.vector3Value.z,
                    1f),
                3 => new Vector4(
                    cascade3SplitProperty.vector2Value.x,
                    cascade3SplitProperty.vector2Value.y,
                    1f,
                    0f),
                2 => new Vector4(cascade2SplitProperty.floatValue, 1f, 0f, 0f),
                _ => new Vector4(1f, 0f, 0f, 0f)
            };
        }

        private static void DrawSplitSliders(ref Vector4 splits, int splitCount, float baseMetric)
        {
            const float splitBias = 0.001f;

            for (int i = 0; i < splitCount; i++)
            {
                float minimum = i == 0 ? 0f : splits[i - 1] + splitBias;
                float maximum = i == splitCount - 1 ? 1f : splits[i + 1] - splitBias;
                splits[i] = Mathf.Clamp(splits[i], minimum, maximum);

                float metric = splits[i] * baseMetric;
                metric = EditorGUILayout.Slider(
                    EditorGUIUtility.TrTextContent(
                        $"Split {i + 1}",
                        "The distance where this cascade ends and the next one starts."),
                    metric,
                    0f,
                    baseMetric);

                splits[i] = baseMetric > 0f
                    ? Mathf.Clamp(metric / baseMetric, minimum, maximum)
                    : minimum;
            }
        }

        private static void DrawCascadeBar(ref Vector4 splits, int cascadeCount, float baseMetric)
        {
            var cascades = new ShadowCascadeGUI.Cascade[cascadeCount];
            float previousSplit = 0f;

            for (int i = 0; i < cascadeCount - 1; i++)
            {
                cascades[i] = new ShadowCascadeGUI.Cascade
                {
                    size = i == 0 ? splits[i] : splits[i] - previousSplit,
                    borderSize = 0f,
                    cascadeHandleState = ShadowCascadeGUI.HandleState.Enabled,
                    borderHandleState = ShadowCascadeGUI.HandleState.Hidden
                };
                previousSplit = splits[i];
            }

            int last = cascadeCount - 1;
            cascades[last] = new ShadowCascadeGUI.Cascade
            {
                size = last == 0 ? 1f : 1f - splits[last - 1],
                borderSize = 0f,
                cascadeHandleState = ShadowCascadeGUI.HandleState.Hidden,
                borderHandleState = ShadowCascadeGUI.HandleState.Hidden
            };

            EditorGUI.BeginChangeCheck();
            ShadowCascadeGUI.DrawCascades(ref cascades, true, baseMetric);
            if (!EditorGUI.EndChangeCheck())
                return;

            float accumulated = 0f;
            for (int i = 0; i < cascadeCount - 1; i++)
            {
                accumulated += cascades[i].size;
                splits[i] = accumulated;
            }
        }

        private static void StoreSplits(
            Vector4 splits,
            int cascadeCount,
            SerializedProperty cascade2SplitProperty,
            SerializedProperty cascade3SplitProperty,
            SerializedProperty cascade4SplitProperty)
        {
            switch (cascadeCount)
            {
                case 2:
                    cascade2SplitProperty.floatValue = splits.x;
                    break;
                case 3:
                    cascade3SplitProperty.vector2Value = new Vector2(splits.x, splits.y);
                    break;
                case 4:
                    cascade4SplitProperty.vector3Value = new Vector3(splits.x, splits.y, splits.z);
                    break;
            }
        }
    }
}
