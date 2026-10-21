using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Menu
{
    public sealed class MenuCameraTour : MonoBehaviour
    {
        [Required, SceneObjectsOnly]
        [SerializeField] private Camera _camera;

        [ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Each shot slowly moves the camera from one point to the other, the screen fades to black between shots")]
        [SerializeField] private List<MenuCameraShot> _shots = new();

        public Camera Camera => _camera;
        public IReadOnlyList<MenuCameraShot> Shots => _shots;

#if UNITY_EDITOR
        private static readonly Color FromColor = new(0.3f, 1f, 0.4f);
        private static readonly Color ToColor = new(1f, 0.35f, 0.3f);
        private static readonly Color PathColor = new(1f, 0.8f, 0.2f);

        private void OnDrawGizmos()
        {
            var fieldOfView = _camera != null ? _camera.fieldOfView : 60f;
            var aspect = _camera != null ? _camera.aspect : 16f / 9f;

            for (var i = 0; i < _shots.Count; i++)
            {
                var shot = _shots[i];

                if (shot?.From == null || shot.To == null)
                    continue;

                DrawCamera(shot.From, FromColor, fieldOfView, aspect, $"Shot {i + 1}: From");
                DrawCamera(shot.To, ToColor, fieldOfView, aspect, $"Shot {i + 1}: To");

                Gizmos.color = PathColor;
                Gizmos.DrawLine(shot.From.position, shot.To.position);

                var direction = shot.To.position - shot.From.position;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    var middle = shot.From.position + direction * 0.5f;
                    var back = -direction.normalized * 0.25f;
                    var side = Vector3.Cross(direction.normalized, Vector3.up) * 0.12f;
                    Gizmos.DrawLine(middle, middle + back + side);
                    Gizmos.DrawLine(middle, middle + back - side);
                }
            }
        }

        private static void DrawCamera(Transform point, Color color, float fieldOfView, float aspect, string label)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position, 0.08f);

            var matrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(point.position, point.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 1.2f, 0.05f, aspect);
            Gizmos.matrix = matrix;

            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(point.position + Vector3.up * 0.25f, label);
        }
#endif
    }
}
