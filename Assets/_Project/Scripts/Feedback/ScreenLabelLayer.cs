using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ShooterShop.Feedback
{
    public sealed class ScreenLabelLayer : IScreenLabels, ILateTickable, IDisposable
    {
        private readonly IViewCamera _camera;
        private readonly FeedbackConfig _config;
        private readonly List<ScreenLabel> _labels = new();
        private RectTransform _root;

        public ScreenLabelLayer(IViewCamera camera, FeedbackConfig config)
        {
            _camera = camera;
            _config = config;
        }

        public ScreenLabel Create(Transform anchor, Vector3 offset, ScreenLabelStyle style)
        {
            EnsureRoot();

            var view = new GameObject("Screen Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            view.rectTransform.SetParent(_root, false);
            view.rectTransform.sizeDelta = new Vector2(_config.ScreenLabelWidth, 100f);
            view.rectTransform.pivot = new Vector2(0.5f, 0f);
            view.alignment = TextAlignmentOptions.Bottom;
            view.textWrappingMode = TextWrappingModes.Normal;
            view.raycastTarget = false;
            view.fontStyle = FontStyles.Bold;
            view.outlineWidth = _config.ScreenLabelOutline;
            view.outlineColor = Color.black;

            if (_config.ScreenLabelFont != null)
                view.font = _config.ScreenLabelFont;

            var label = new ScreenLabel(view, anchor, offset, style);
            _labels.Add(label);
            return label;
        }

        public void Release(ScreenLabel label)
        {
            if (label == null || !_labels.Remove(label))
                return;

            if (label.View != null)
                Object.Destroy(label.View.gameObject);
        }

        public void LateTick()
        {
            if (_root == null)
                return;

            var camera = _camera.Current;

            for (var i = _labels.Count - 1; i >= 0; i--)
            {
                var label = _labels[i];

                if (label.View == null || label.Anchor == null)
                {
                    Release(label);
                    continue;
                }

                Place(label, camera);
            }
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);

            _labels.Clear();
        }

        private void Place(ScreenLabel label, Camera camera)
        {
            var view = label.View;
            var isShown = label.IsVisible && camera != null && !string.IsNullOrEmpty(view.text);

            if (isShown)
            {
                var world = label.WorldPosition;
                var distance = Vector3.Distance(camera.transform.position, world);
                var screen = camera.WorldToScreenPoint(world);
                var isBehind = screen.z < 0f;
                var style = label.Style;

                if (style.MaxDistance > 0f && distance > style.MaxDistance)
                    isShown = false;
                else if (style.PinToScreenEdge)
                    view.rectTransform.position = PinToEdge(screen, isBehind);
                else if (isBehind || !IsOnScreen(screen))
                    isShown = false;
                else
                    view.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
            }

            if (view.gameObject.activeSelf != isShown)
                view.gameObject.SetActive(isShown);
        }

        private Vector3 PinToEdge(Vector3 screen, bool isBehind)
        {
            var margin = _config.ScreenEdgeMargin;
            var center = new Vector2(Screen.width, Screen.height) * 0.5f;
            var point = new Vector2(screen.x, screen.y);

            if (isBehind)
                point = center - (point - center);

            if (isBehind || !IsOnScreen(screen))
            {
                var direction = point - center;

                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.down;

                var half = center - Vector2.one * margin;
                var scale = Mathf.Min(half.x / Mathf.Abs(direction.x + 0.0001f), half.y / Mathf.Abs(direction.y + 0.0001f));
                point = center + direction * scale;
            }

            point.x = Mathf.Clamp(point.x, margin, Screen.width - margin);
            point.y = Mathf.Clamp(point.y, margin, Screen.height - margin);
            return new Vector3(point.x, point.y, 0f);
        }

        private static bool IsOnScreen(Vector3 screen) =>
            screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            var canvasObject = new GameObject("Screen Labels", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _config.ScreenLabelSortingOrder;

            _root = (RectTransform)canvasObject.transform;
        }
    }
}
