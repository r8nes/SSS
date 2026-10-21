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

        [BoxGroup("Hand weapon"), Required, AssetsOnly]
        [InfoBox("Visual-only pistol attached to the right hand while a visitor holds a rental weapon")]
        [SerializeField] private GameObject _handWeapon;

        [BoxGroup("Hand weapon")]
        [SerializeField] private Vector3 _handWeaponPosition = new(0.08f, 0.02f, 0.03f);

        [BoxGroup("Hand weapon")]
        [SerializeField] private Vector3 _handWeaponRotation = new(0f, 90f, 90f);

        public Vector3 ModelOffset => _modelOffset;
        public GameObject HandWeapon => _handWeapon;
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
