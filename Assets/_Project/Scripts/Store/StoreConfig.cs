using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Store
{
    [CreateAssetMenu(menuName = "Shooter Shop/Store Config", fileName = "StoreConfig")]
    public sealed class StoreConfig : ScriptableObject
    {
        [BoxGroup("Delivery"), Required, AssetsOnly]
        [SerializeField] private DeliveryBox _boxPrefab;

        [BoxGroup("Delivery"), MinValue(0), SuffixLabel("sec")]
        [SerializeField] private float _deliveryDelay = 3f;

        [BoxGroup("Delivery"), MinValue(0.3f), SuffixLabel("m")]
        [SerializeField] private float _boxSpacing = 0.7f;

        [BoxGroup("Carrying")]
        [InfoBox("Path from the player root to the transform the box follows")]
        [SerializeField] private string _holdPivotPath = "CameraRoot/CameraPivot";

        [BoxGroup("Carrying")]
        [SerializeField] private Vector3 _holdOffset = new(0f, -0.45f, 0.8f);

        [BoxGroup("Carrying"), MinValue(1), SuffixLabel("m")]
        [SerializeField] private float _dropRayLength = 4f;

        [BoxGroup("Carrying")]
        [SerializeField] private LayerMask _dropSurfaces = ~0;

        public DeliveryBox BoxPrefab => _boxPrefab;
        public float DeliveryDelay => _deliveryDelay;
        public float BoxSpacing => _boxSpacing;
        public string HoldPivotPath => _holdPivotPath;
        public Vector3 HoldOffset => _holdOffset;
        public float DropRayLength => _dropRayLength;
        public LayerMask DropSurfaces => _dropSurfaces;
    }
}
