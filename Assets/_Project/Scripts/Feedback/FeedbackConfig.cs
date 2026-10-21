using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Feedback
{
    [CreateAssetMenu(menuName = "Shooter Shop/Feedback Config", fileName = "FeedbackConfig")]
    public sealed class FeedbackConfig : ScriptableObject
    {
        [BoxGroup("Place on stand"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _placeDuration = 0.35f;

        [BoxGroup("Place on stand"), MinValue(0f), SuffixLabel("m")]
        [SerializeField] private float _placeArcHeight = 0.35f;

        [BoxGroup("Place on stand"), Range(0f, 1f)]
        [SerializeField] private float _placeStartScale = 0.4f;

        [BoxGroup("Place on stand")]
        [SerializeField] private Ease _placeEase = Ease.OutQuad;

        [BoxGroup("Outline"), Required, AssetsOnly]
        [InfoBox("Used for runtime objects like rental weapons on holders")]
        [SerializeField] private Material _outlineMaterial;

        [BoxGroup("Outline"), Required, AssetsOnly]
        [InfoBox("Translucent preview of a held item where it will be placed")]
        [SerializeField] private Material _ghostMaterial;

        [BoxGroup("Place on stand"), Range(0f, 0.5f)]
        [InfoBox("Squash and stretch after a unit lands in its slot")]
        [SerializeField] private float _settleStrength = 0.18f;

        [BoxGroup("Place on stand"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _settleDuration = 0.3f;

        [BoxGroup("Take from stand"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _vanishDuration = 0.2f;

        [BoxGroup("Pop in"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _popDuration = 0.3f;

        [BoxGroup("Pop in")]
        [SerializeField] private Ease _popEase = Ease.OutBack;

        [BoxGroup("Box"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _punchDuration = 0.25f;

        [BoxGroup("Box"), Range(0f, 1f)]
        [SerializeField] private float _punchStrength = 0.15f;

        [BoxGroup("Box"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _landDuration = 0.3f;

        [BoxGroup("Box")]
        [SerializeField] private Ease _landEase = Ease.OutBounce;

        [BoxGroup("Notifications"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _notificationDuration = 4f;

        [BoxGroup("Notifications"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _notificationFade = 0.25f;

        [BoxGroup("Notifications"), MinValue(1)]
        [SerializeField] private int _maxNotifications = 4;

        [BoxGroup("Colors")]
        [SerializeField] private Color _infoColor = new(0.55f, 0.75f, 1f);

        [BoxGroup("Colors")]
        [SerializeField] private Color _goodColor = new(0.45f, 0.95f, 0.45f);

        [BoxGroup("Colors")]
        [SerializeField] private Color _warningColor = new(1f, 0.55f, 0.3f);

        [BoxGroup("World popups")]
        [SerializeField] private ScreenLabelStyle _popupStyle = new(30f, Color.white, 0f, false);

        [BoxGroup("World popups"), MinValue(0.2f), SuffixLabel("sec")]
        [SerializeField] private float _popupDuration = 1.4f;

        [BoxGroup("World popups"), MinValue(0f), SuffixLabel("m")]
        [SerializeField] private float _popupRise = 0.8f;

        [BoxGroup("Throw"), MinValue(0f), SuffixLabel("m")]
        [SerializeField] private float _throwArcHeight = 0.6f;

        [BoxGroup("World markers"), MinValue(0f), SuffixLabel("m")]
        [SerializeField] private float _markerBobHeight = 0.12f;

        [BoxGroup("World markers"), MinValue(0f)]
        [SerializeField] private float _markerBobSpeed = 2.5f;

        [BoxGroup("World markers"), MinValue(1f), SuffixLabel("m")]
        [InfoBox("Boxes lying this close to the delivery point show the delivery marker")]
        [SerializeField] private float _deliveryMarkerRadius = 6f;

        [BoxGroup("Screen labels"), AssetsOnly]
        [InfoBox("Leave empty to use the TextMeshPro default font")]
        [SerializeField] private TMP_FontAsset _screenLabelFont;

        [BoxGroup("Screen labels"), MinValue(50), SuffixLabel("px")]
        [SerializeField] private float _screenLabelWidth = 360f;

        [BoxGroup("Screen labels"), Range(0f, 0.5f)]
        [SerializeField] private float _screenLabelOutline = 0.25f;

        [BoxGroup("Screen labels"), MinValue(0), SuffixLabel("px")]
        [SerializeField] private float _screenEdgeMargin = 60f;

        [BoxGroup("Screen labels")]
        [InfoBox("Below the gameplay UI so dialogs stay on top")]
        [SerializeField] private int _screenLabelSortingOrder = -10;

        [BoxGroup("Screen labels")]
        [SerializeField] private ScreenLabelStyle _markerStyle = new(26f, new Color(1f, 0.85f, 0.35f), 0f, true);

        [BoxGroup("Screen labels")]
        [SerializeField] private ScreenLabelStyle _customerStyle = new(20f, Color.white, 14f, false);

        [BoxGroup("Screen labels"), SuffixLabel("m")]
        [SerializeField] private Vector3 _markerOffset = new(0f, 0.4f, 0f);

        public TMP_FontAsset ScreenLabelFont => _screenLabelFont;
        public float ScreenLabelWidth => _screenLabelWidth;
        public float ScreenLabelOutline => _screenLabelOutline;
        public float ScreenEdgeMargin => _screenEdgeMargin;
        public int ScreenLabelSortingOrder => _screenLabelSortingOrder;
        public ScreenLabelStyle MarkerStyle => _markerStyle;
        public ScreenLabelStyle CustomerStyle => _customerStyle;
        public Vector3 MarkerOffset => _markerOffset;
        public float MarkerBobHeight => _markerBobHeight;
        public float MarkerBobSpeed => _markerBobSpeed;
        public float DeliveryMarkerRadius => _deliveryMarkerRadius;
        public float NotificationDuration => _notificationDuration;
        public float NotificationFade => _notificationFade;
        public int MaxNotifications => _maxNotifications;
        public ScreenLabelStyle PopupStyle => _popupStyle;
        public float PopupDuration => _popupDuration;
        public float PopupRise => _popupRise;
        public float ThrowArcHeight => _throwArcHeight;

        public Color ColorOf(NotificationKind kind) => kind switch
        {
            NotificationKind.Good => _goodColor,
            NotificationKind.Warning => _warningColor,
            _ => _infoColor
        };

        public Material OutlineMaterial => _outlineMaterial;
        public Material GhostMaterial => _ghostMaterial;
        public float SettleStrength => _settleStrength;
        public float SettleDuration => _settleDuration;
        public float PlaceDuration => _placeDuration;
        public float PlaceArcHeight => _placeArcHeight;
        public float PlaceStartScale => _placeStartScale;
        public Ease PlaceEase => _placeEase;
        public float VanishDuration => _vanishDuration;
        public float PopDuration => _popDuration;
        public Ease PopEase => _popEase;
        public float PunchDuration => _punchDuration;
        public float PunchStrength => _punchStrength;
        public float LandDuration => _landDuration;
        public Ease LandEase => _landEase;
    }
}
