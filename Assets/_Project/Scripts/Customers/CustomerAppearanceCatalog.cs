using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    [CreateAssetMenu(menuName = "Shooter Shop/Customers/Appearance Catalog", fileName = "CustomerAppearanceCatalog")]
    public sealed class CustomerAppearanceCatalog : ScriptableObject
    {
        [BoxGroup("Models"), Required, AssetsOnly, ListDrawerSettings(ShowFoldout = false, NumberOfItemsPerPage = 20)]
        [InfoBox("Built by Shooter Shop > Customers > Build Appearances")]
        [SerializeField] private List<CustomerAppearance> _appearances = new();

        [BoxGroup("Models"), SuffixLabel("m")]
        [InfoBox("Model position relative to the customer root, the root sits at the capsule center")]
        [SerializeField] private Vector3 _modelOffset = new(0f, -1f, 0f);

        [BoxGroup("Models"), MinValue(0.1f), SuffixLabel("m/s")]
        [InfoBox("Speed at which the walk clip looks natural, faster movement blends towards run")]
        [SerializeField] private float _walkClipSpeed = 1.4f;

        [BoxGroup("Aim pose"), ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Humanoid muscle values applied over the animation while a range visitor shoots")]
        [SerializeField] private List<MuscleValue> _aimPose = new()
        {
            new("Right Arm Down-Up", 0.25f),
            new("Right Arm Front-Back", -0.85f),
            new("Right Forearm Stretch", 0.95f),
            new("Right Hand Down-Up", 0f),
            new("Left Arm Down-Up", 0.22f),
            new("Left Arm Front-Back", -0.85f),
            new("Left Arm Twist In-Out", 0f),
            new("Left Forearm Stretch", 0.5f)
        };

        [BoxGroup("Aim pose"), Range(0f, 1f)]
        [SerializeField] private float _recoilStrength = 0.18f;

        [BoxGroup("Aim pose"), MinValue(1f)]
        [SerializeField] private float _recoilRecovery = 12f;

        [BoxGroup("Hand weapon")]
        [InfoBox("Offset of the held rental weapon display model from the right hand bone")]
        [SerializeField] private Vector3 _handWeaponPosition = new(0.08f, 0.02f, 0.03f);

        [BoxGroup("Hand weapon")]
        [SerializeField] private Vector3 _handWeaponRotation = new(0f, 90f, 90f);

        public Vector3 ModelOffset => _modelOffset;
        public float WalkClipSpeed => _walkClipSpeed;
        public IReadOnlyList<MuscleValue> AimPose => _aimPose;
        public float RecoilStrength => _recoilStrength;
        public float RecoilRecovery => _recoilRecovery;
        public Vector3 HandWeaponPosition => _handWeaponPosition;
        public Quaternion HandWeaponRotation => Quaternion.Euler(_handWeaponRotation);

        public int PickIndex() => _appearances.Count > 0 ? Random.Range(0, _appearances.Count) : -1;

        public bool TryGet(int index, out CustomerAppearance appearance)
        {
            appearance = index >= 0 && index < _appearances.Count ? _appearances[index] : null;
            return appearance != null;
        }

#if UNITY_EDITOR
        public void EditorSetAppearances(IEnumerable<CustomerAppearance> appearances)
        {
            _appearances = new List<CustomerAppearance>(appearances);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
