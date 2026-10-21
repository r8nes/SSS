using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Store
{
    public sealed class StoreLayout : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _deliveryPoint;

        [Required, SceneObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<ProductStand> _stands = new();

        public Transform DeliveryPoint => _deliveryPoint;
        public IReadOnlyList<ProductStand> Stands => _stands;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_deliveryPoint == null)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireCube(_deliveryPoint.position + Vector3.up * 0.25f, new Vector3(2f, 0.5f, 1f));
            Gizmos.DrawLine(_deliveryPoint.position, _deliveryPoint.position + _deliveryPoint.right * 2f);
            UnityEditor.Handles.Label(_deliveryPoint.position + Vector3.up * 0.8f, "Delivery");
        }
#endif
    }
}
