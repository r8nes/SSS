using ShooterShop.Rentals;
using UnityEngine;

namespace ShooterShop.Customers
{
    public sealed class AppearanceBody
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

        private readonly Transform _root;
        private readonly CustomerAppearanceCatalog _catalog;
        private readonly Transform _rightHand;
        private Vector3 _lastPosition;
        private Vector2 _move;

        private AppearanceBody(Transform root, CustomerAppearance appearance, CustomerAppearanceCatalog catalog)
        {
            _root = root;
            _catalog = catalog;
            _lastPosition = root.position;
            Appearance = appearance;
            Appearance.Ragdoll.Freeze();
            Animator.SetBool(IsGrounded, true);
            _rightHand = Animator.GetBoneTransform(HumanBodyBones.RightHand);

            for (var layer = 1; layer < Animator.layerCount; layer++)
                Animator.SetLayerWeight(layer, 0f);
        }

        public CustomerAppearance Appearance { get; }
        public Animator Animator => Appearance.Animator;
        public CustomerRagdoll Ragdoll => Appearance.Ragdoll;

        public static AppearanceBody Create(CustomerAppearanceCatalog catalog, int index, Transform root)
        {
            if (!catalog.TryGet(index, out var prefab))
                return null;

            var appearance = Object.Instantiate(prefab, root, false);
            appearance.transform.SetLocalPositionAndRotation(catalog.ModelOffset, Quaternion.identity);
            return new AppearanceBody(root, appearance, catalog);
        }

        public void TickLocomotion(float deltaTime, float walkBlendValue, float smoothing)
        {
            if (deltaTime <= 0f)
                return;

            var velocity = (_root.position - _lastPosition) / deltaTime;
            _lastPosition = _root.position;

            var local = _root.InverseTransformDirection(velocity) * (walkBlendValue / Mathf.Max(0.1f, _catalog.WalkClipSpeed));
            _move = Vector2.Lerp(_move, new Vector2(local.x, local.z), deltaTime * smoothing);
            Animator.SetFloat(MoveX, _move.x);
            Animator.SetFloat(MoveY, _move.y);
        }

        public void PlayWeaponPose(RentalWeaponPose pose, bool isActive)
        {
            var layer = Animator.GetLayerIndex(_catalog.WeaponLayer);

            if (layer < 0)
                return;

            Animator.SetLayerWeight(layer, isActive ? 1f : 0f);

            if (isActive)
                Animator.CrossFadeInFixedTime(_catalog.IdleStateOf(pose), 0.15f, layer);
        }

        public void PlayFire(RentalWeaponPose pose)
        {
            var layer = Animator.GetLayerIndex(_catalog.WeaponLayer);
            var state = _catalog.FireStateOf(pose);

            if (layer >= 0 && !string.IsNullOrEmpty(state))
                Animator.CrossFadeInFixedTime(state, 0.05f, layer, 0f);
        }

        public void AlignWeapon(Transform weapon, RentalWeaponConfig config, Vector3? aimPoint)
        {
            if (weapon == null || _rightHand == null)
                return;

            var direction = aimPoint.HasValue ? aimPoint.Value - _rightHand.position : _root.forward;

            if (direction.sqrMagnitude < 0.0001f)
                direction = _root.forward;

            var aim = Quaternion.LookRotation(direction.normalized, _root.up);
            weapon.SetPositionAndRotation(_rightHand.position + aim * config.HandOffset, aim * config.HandRotation);
        }
    }
}
