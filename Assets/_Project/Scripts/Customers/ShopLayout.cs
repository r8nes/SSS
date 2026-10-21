using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShooterShop.Customers
{
    public sealed class ShopLayout : MonoBehaviour
    {
        [BoxGroup("Path"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _entrance;

        [BoxGroup("Path"), Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _exit;

        [BoxGroup("Queue"), Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("The first point is the cash register, the rest form the line behind it")]
        [FormerlySerializedAs("_counterSlots")]
        [SerializeField] private List<Transform> _queuePoints = new();

        public Transform Entrance => _entrance;
        public Transform Exit => _exit;
        public IReadOnlyList<Transform> QueuePoints => _queuePoints;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawPoint(_entrance, Color.green, "Entrance");
            DrawPoint(_exit, Color.red, "Exit");

            for (var i = 0; i < _queuePoints.Count; i++)
            {
                DrawPoint(_queuePoints[i], i == 0 ? Color.yellow : Color.cyan, i == 0 ? "Register" : $"Queue {i}");

                if (i > 0 && _queuePoints[i] != null && _queuePoints[i - 1] != null)
                    Gizmos.DrawLine(_queuePoints[i].position, _queuePoints[i - 1].position);
            }
        }

        private static void DrawPoint(Transform point, Color color, string label)
        {
            if (point == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position + Vector3.up * 0.1f, 0.3f);
            Gizmos.DrawLine(point.position + Vector3.up * 0.1f, point.position + Vector3.up * 0.1f + point.forward * 0.6f);
            UnityEditor.Handles.Label(point.position + Vector3.up * 0.6f, label);
        }
#endif
    }
}
