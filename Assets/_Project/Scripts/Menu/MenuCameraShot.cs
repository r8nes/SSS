using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Menu
{
    [Serializable]
    public sealed class MenuCameraShot
    {
        [HorizontalGroup("From"), Required, SceneObjectsOnly]
        [SerializeField] private Transform _from;

        [HorizontalGroup("To"), Required, SceneObjectsOnly]
        [SerializeField] private Transform _to;

        [MinValue(0f), SuffixLabel("sec")]
        [InfoBox("0 uses the default duration from the config")]
        [SerializeField] private float _duration;

        public Transform From => _from;
        public Transform To => _to;
        public float Duration => _duration;

        public MenuCameraShot(Transform from, Transform to, float duration)
        {
            _from = from;
            _to = to;
            _duration = duration;
        }

#if UNITY_EDITOR
        [HorizontalGroup("From", 60), Button("View")]
        private void ViewFrom() => MenuCameraShotEditorTools.LookThrough(_from);

        [HorizontalGroup("From", 70), Button("Set here")]
        private void SetFrom() => MenuCameraShotEditorTools.MoveToSceneView(_from);

        [HorizontalGroup("To", 60), Button("View")]
        private void ViewTo() => MenuCameraShotEditorTools.LookThrough(_to);

        [HorizontalGroup("To", 70), Button("Set here")]
        private void SetTo() => MenuCameraShotEditorTools.MoveToSceneView(_to);
#endif
    }

#if UNITY_EDITOR
    internal static class MenuCameraShotEditorTools
    {
        public static void LookThrough(Transform point)
        {
            var view = UnityEditor.SceneView.lastActiveSceneView;

            if (point == null || view == null)
                return;

            view.orthographic = false;
            view.AlignViewToObject(point);
            view.Repaint();
        }

        public static void MoveToSceneView(Transform point)
        {
            var view = UnityEditor.SceneView.lastActiveSceneView;

            if (point == null || view == null)
                return;

            UnityEditor.Undo.RecordObject(point, "Set camera shot point");
            point.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
        }
    }
#endif
}
