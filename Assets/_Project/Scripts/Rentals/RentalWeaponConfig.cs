using Akila.FPSFramework;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Rentals
{
    [CreateAssetMenu(menuName = "Shooter Shop/Rentals/Rental Weapon", fileName = "RentalWeapon")]
    public sealed class RentalWeaponConfig : ScriptableObject
    {
        [Required]
        [SerializeField] private string _displayName;

        [Required, AssetsOnly]
        [InfoBox("Networked Akila inventory item the player gets in hands")]
        [SerializeField] private InventoryItem _item;

        [EnumToggleButtons]
        [InfoBox("Which stance range visitors use with this weapon")]
        [SerializeField] private RentalWeaponPose _pose = RentalWeaponPose.Pistol;

        [Required, AssetsOnly]
        [InfoBox("Visual-only model shown on the rack and the counter")]
        [SerializeField] private GameObject _displayModel;

        [BoxGroup("Held by visitors"), SuffixLabel("m")]
        [InfoBox("Display model offset from the visitor's right hand, in the aim space (z forward, y up)")]
        [SerializeField] private Vector3 _handOffset = new(0f, 0.03f, 0.1f);

        [BoxGroup("Held by visitors")]
        [SerializeField] private Vector3 _handRotation;

        [BoxGroup("Held by visitors"), MinValue(0f), SuffixLabel("m")]
        [InfoBox("Distance from the display model center to the muzzle, where the flash and tracer start")]
        [SerializeField] private float _muzzleDistance = 0.15f;

        [BoxGroup("Held by visitors")]
        [SerializeField] private AudioProfile _fireSound;

        public float MuzzleDistance => _muzzleDistance;
        public AudioProfile FireSound => _fireSound;
        public Vector3 HandOffset => _handOffset;
        public Quaternion HandRotation => Quaternion.Euler(_handRotation);
        public string DisplayName => _displayName;
        public InventoryItem Item => _item;
        public GameObject DisplayModel => _displayModel;
        public RentalWeaponPose Pose => _pose;
    }
}
