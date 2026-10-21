using System.Collections.Generic;
using Mirror;
using ShooterShop.Catalog;
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

        public bool HasLooseBoxesNear(Vector3 position, float radius)
        {
            foreach (var box in _boxes)
                if (box != null && !box.IsCarried && Vector3.Distance(box.transform.position, position) <= radius)
                    return true;

            return false;
        }

        public DeliveryBox FindCarriedNear(Vector3 position, ProductConfig product, float radius)
        {
            DeliveryBox closest = null;
            var bestDistance = radius;

            foreach (var box in _boxes)
            {
                if (box == null || !box.IsCarried || box.Product != product)
                    continue;

                var distance = Vector3.Distance(box.transform.position, position);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    closest = box;
                }
            }

            return closest;
        }

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

            if (_inputLock.IsLocked || Keyboard.current == null)
                return;

            var carried = LocalCarriedBox;

            if (carried == null)
                return;

            if (Keyboard.current[_config.DropKey].wasPressedThisFrame)
                carried.RequestDrop();
            else if (Keyboard.current[_config.ThrowKey].wasPressedThisFrame && FindPivot(LocalPlayerNetId) is { } pivot)
                carried.RequestThrow(pivot.forward);
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

        public (Vector3 Position, Quaternion Rotation, float Duration) FindThrowPose(DeliveryBox box, Vector3 direction)
        {
            const float step = 0.03f;
            const float maxTime = 3f;

            var halfHeight = box.transform.lossyScale.y * 0.5f;
            var rotation = Quaternion.Euler(0f, box.transform.eulerAngles.y, 0f);
            var position = box.transform.position;
            var velocity = direction.normalized * _config.ThrowSpeed;

            for (var time = 0f; time < maxTime; time += step)
            {
                var next = position + velocity * step;
                velocity += Physics.gravity * step;
                var segment = next - position;

                if (Physics.Raycast(position, segment.normalized, out var hit, segment.magnitude, _config.DropSurfaces, QueryTriggerInteraction.Ignore))
                {
                    var landing = hit.point + hit.normal * 0.05f;
                    var (floor, _) = FindDropPoseAt(landing, rotation, halfHeight);
                    return (floor, rotation, Mathf.Max(0.15f, time));
                }

                position = next;
            }

            var (fallback, _) = FindDropPoseAt(position, rotation, halfHeight);
            return (fallback, rotation, maxTime);
        }

        private (Vector3 Position, Quaternion Rotation) FindDropPoseAt(Vector3 origin, Quaternion rotation, float halfHeight) =>
            Physics.Raycast(origin + Vector3.up * 0.2f, Vector3.down, out var hit, _config.DropRayLength, _config.DropSurfaces, QueryTriggerInteraction.Ignore)
                ? (hit.point + Vector3.up * halfHeight, rotation)
                : (origin, rotation);

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
