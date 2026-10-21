using UnityEngine;

namespace ShooterShop.Customers
{
    public sealed class AppearanceBody
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

        private readonly Transform _root;
        private Vector3 _lastPosition;
        private Vector2 _move;

        private AppearanceBody(Transform root, CustomerAppearance appearance)
        {
            _root = root;
            _lastPosition = root.position;
            Appearance = appearance;
            Appearance.Ragdoll.Freeze();
            Animator.SetBool(IsGrounded, true);

            for (var layer = 1; layer < Animator.layerCount; layer++)
                Animator.SetLayerWeight(layer, 0f);
        }

        public CustomerAppearance Appearance { get; }
        public Animator Animator => Appearance.Animator;
        public CustomerRagdoll Ragdoll => Appearance.Ragdoll;

        private CustomerAppearanceCatalog _catalog;
        private HumanPoseHandler _poseHandler;
        private HumanPose _pose;
        private int[] _aimMuscles;
        private int _recoilMuscle = -1;
        private float _recoil;

        public static AppearanceBody Create(CustomerAppearanceCatalog catalog, int index, Transform root)
        {
            if (!catalog.TryGet(index, out var prefab))
                return null;

            var appearance = Object.Instantiate(prefab, root, false);
            appearance.transform.SetLocalPositionAndRotation(catalog.ModelOffset, Quaternion.identity);
            return new AppearanceBody(root, appearance) { _catalog = catalog };
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
            public void Kick() => _recoil = 1f;

        public void ApplyAimPose(float deltaTime)
        {
            EnsurePoseHandler();

            var hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipsPosition = hips.position;
            var hipsRotation = hips.rotation;

            _poseHandler.GetHumanPose(ref _pose);

            var aimPose = _catalog.AimPose;

            for (var i = 0; i < aimPose.Count; i++)
                if (_aimMuscles[i] >= 0)
                    _pose.muscles[_aimMuscles[i]] = aimPose[i].Value;

            if (_recoilMuscle >= 0)
                _pose.muscles[_recoilMuscle] += _recoil * _catalog.RecoilStrength;

            _recoil = Mathf.MoveTowards(_recoil, 0f, deltaTime * _catalog.RecoilRecovery);
            _poseHandler.SetHumanPose(ref _pose);
            hips.SetPositionAndRotation(hipsPosition, hipsRotation);
        }

        public void Dispose() => _poseHandler?.Dispose();

        private void EnsurePoseHandler()
        {
            if (_poseHandler != null)
                return;

            _poseHandler = new HumanPoseHandler(Animator.avatar, Animator.transform);
            _pose = new HumanPose();

            var aimPose = _catalog.AimPose;
            _aimMuscles = new int[aimPose.Count];

            for (var i = 0; i < aimPose.Count; i++)
                _aimMuscles[i] = System.Array.IndexOf(HumanTrait.MuscleName, aimPose[i].Muscle);

            _recoilMuscle = System.Array.IndexOf(HumanTrait.MuscleName, "Right Arm Down-Up");
        }
    }
}
