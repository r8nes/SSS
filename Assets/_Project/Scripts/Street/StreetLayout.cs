using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Street
{
    public sealed class StreetLayout : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Points on the baked street NavMesh that pedestrians walk between")]
        [SerializeField] private List<Transform> _waypoints = new();

        public IReadOnlyList<Transform> Waypoints => _waypoints;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.8f, 1f);

            foreach (var point in _waypoints)
                if (point != null)
                    Gizmos.DrawWireSphere(point.position + Vector3.up * 0.1f, 0.4f);
        }
#endif
    }
}
