using System.Collections.Generic;
using Mirror;
using ShooterShop.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace ShooterShop.Store
{
    public sealed class CarryService : ITickable
    {
        private readonly StoreConfig _config;
        private readonly IPlayerInputLock _inputLock;
        private readonly List<DeliveryBox> _boxes = new();
        private readonly Dictionary<uint, Transform> _pivots = new();

        public CarryService(StoreConfig config, IPlayerInputLock inputLock)
        {
            _config = config;
            _inputLock = inputLock;
        }

        public DeliveryBox LocalCarriedBox => FindCarried(LocalPlayerNetId);
        public bool IsLocalPlayerCarrying => LocalCarriedBox != null;

        private static uint LocalPlayerNetId => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;

        public void Register(DeliveryBox box) => _boxes.Add(box);

        public void Unregister(DeliveryBox box) => _boxes.Remove(box);

        public bool IsCarrying(uint netId) => FindCarried(netId) != null;

        public void Tick()
        {
            for (var i = _boxes.Count - 1; i >= 0; i--)
            {
                var box = _boxes[i];

                if (box == null)
                    _boxes.RemoveAt(i);
                else if (box.IsCarried)
                    Follow(box);
            }

            if (!_inputLock.IsLocked && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                LocalCarriedBox?.RequestDrop();
        }

        public (Vector3 Position, Quaternion Rotation) FindDropPose(DeliveryBox box)
        {
            var origin = box.transform.position + Vector3.up * 0.2f;
            var rotation = Quaternion.Euler(0f, box.transform.eulerAngles.y, 0f);
            var halfHeight = box.transform.lossyScale.y * 0.5f;

            return Physics.Raycast(origin, Vector3.down, out var hit, _config.DropRayLength, _config.DropSurfaces, QueryTriggerInteraction.Ignore)
                ? (hit.point + Vector3.up * halfHeight, rotation)
                : (box.transform.position, rotation);
        }

        private DeliveryBox FindCarried(uint netId)
        {
            if (netId == 0)
                return null;

            foreach (var box in _boxes)
                if (box != null && box.CarrierNetId == netId)
                    return box;

            return null;
        }

        private void Follow(DeliveryBox box)
        {
            var pivot = FindPivot(box.CarrierNetId);

            if (pivot == null)
                return;

            box.transform.SetPositionAndRotation(
                pivot.TransformPoint(_config.HoldOffset),
                Quaternion.Euler(0f, pivot.eulerAngles.y, 0f));
        }

        private Transform FindPivot(uint netId)
        {
            if (_pivots.TryGetValue(netId, out var cached) && cached != null)
                return cached;

            if (!NetworkClient.spawned.TryGetValue(netId, out var identity) || identity == null)
                return null;

            var pivot = identity.transform.Find(_config.HoldPivotPath) ?? identity.transform;
            _pivots[netId] = pivot;
            return pivot;
        }
    }
}
