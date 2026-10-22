using System.Collections.Generic;
using ShooterShop.Rentals;
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

        [BoxGroup("Weapon stance")]
        [SerializeField] private string _weaponLayer = "Weapon";

        [BoxGroup("Weapon stance")]
        [SerializeField] private string _pistolIdleState = "Pistol";

        [BoxGroup("Weapon stance")]
        [InfoBox("Empty when the pistol idle clip already contains the shots")]
        [SerializeField] private string _pistolFireState = "";

        [BoxGroup("Weapon stance")]
        [SerializeField] private string _rifleIdleState = "Rifle";

        [BoxGroup("Weapon stance")]
        [SerializeField] private string _rifleFireState = "Rifle Fire";

        public Vector3 ModelOffset => _modelOffset;
        public float WalkClipSpeed => _walkClipSpeed;
        public string WeaponLayer => _weaponLayer;

        public string IdleStateOf(RentalWeaponPose pose) => pose == RentalWeaponPose.Rifle ? _rifleIdleState : _pistolIdleState;

        public string FireStateOf(RentalWeaponPose pose) => pose == RentalWeaponPose.Rifle ? _rifleFireState : _pistolFireState;

        public int IndexOf(CustomerAppearance appearance) => _appearances.IndexOf(appearance);

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
