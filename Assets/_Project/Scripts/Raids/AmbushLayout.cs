using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Raids
{
    public sealed class AmbushLayout : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Where the bandits of the street ambush take cover, extra bandits share points")]
        [SerializeField] private List<Transform> _points = new();

        public IReadOnlyList<Transform> Points => _points;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            foreach (var point in _points)
            {
                if (point == null)
                    continue;

                Gizmos.DrawWireSphere(point.position + Vector3.up * 0.1f, 0.4f);
                UnityEditor.Handles.Label(point.position + Vector3.up * 0.8f, "Bandit");
            }
        }
#endif
    }
}
