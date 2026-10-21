using ShooterShop.Customers;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;
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

        [BoxGroup("Delivery"), Required, AssetsOnly]
        [InfoBox("Walks from the street entrance to the delivery point and drops the boxes")]
        [SerializeField] private Customer _courierPrefab;

        [BoxGroup("Delivery"), MinValue(0.5f), SuffixLabel("m/s")]
        [SerializeField] private float _courierSpeed = 3f;

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

        [BoxGroup("Carrying")]
        [SerializeField] private Key _dropKey = Key.G;

        [BoxGroup("Placing")]
        [InfoBox("Must match the Akila Interact binding, holding it keeps placing units")]
        [SerializeField] private Key _placeKey = Key.F;

        [BoxGroup("Placing"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _holdRepeatDelay = 0.3f;

        [BoxGroup("Placing"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _holdRepeatInterval = 0.15f;

        [BoxGroup("Carrying")]
        [SerializeField] private Key _throwKey = Key.T;

        [BoxGroup("Carrying"), MinValue(1), SuffixLabel("m/s")]
        [SerializeField] private float _throwSpeed = 7f;

        [BoxGroup("Prices"), MinValue(1), SuffixLabel("%")]
        [SerializeField] private int _markupStep = 10;

        [BoxGroup("Prices"), MinValue(0), SuffixLabel("%")]
        [SerializeField] private int _maxMarkup = 30;

        public DeliveryBox BoxPrefab => _boxPrefab;
        public Customer CourierPrefab => _courierPrefab;
        public float CourierSpeed => _courierSpeed;
        public Key DropKey => _dropKey;
        public Key PlaceKey => _placeKey;
        public float HoldRepeatDelay => _holdRepeatDelay;
        public float HoldRepeatInterval => _holdRepeatInterval;
        public Key ThrowKey => _throwKey;
        public float ThrowSpeed => _throwSpeed;
        public int MarkupStep => _markupStep;
        public int MaxMarkup => _maxMarkup;
        public float DeliveryDelay => _deliveryDelay;
        public float BoxSpacing => _boxSpacing;
        public string HoldPivotPath => _holdPivotPath;
        public Vector3 HoldOffset => _holdOffset;
        public float DropRayLength => _dropRayLength;
        public LayerMask DropSurfaces => _dropSurfaces;
    }
}
