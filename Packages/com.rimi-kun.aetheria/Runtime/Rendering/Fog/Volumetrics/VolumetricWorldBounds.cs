using System.Runtime.InteropServices;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct VolumetricMediaBounds
    {
        public readonly Vector4 CenterAndTextured;
        public readonly Vector4 AxisX;
        public readonly Vector4 AxisY;
        public readonly Vector4 AxisZ;

        public VolumetricMediaBounds(
            Vector3 center,
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ,
            bool textured)
        {
            CenterAndTextured = new Vector4(center.x, center.y, center.z, textured ? 1f : 0f);
            AxisX = new Vector4(axisX.x, axisX.y, axisX.z, 0f);
            AxisY = new Vector4(axisY.x, axisY.y, axisY.z, 0f);
            AxisZ = new Vector4(axisZ.x, axisZ.y, axisZ.z, 0f);
        }
    }

    internal readonly struct VolumetricWorldBounds
    {
        public readonly Bounds AxisAligned;
        private readonly Vector3 _center;
        private readonly Vector3 _axisX;
        private readonly Vector3 _axisY;
        private readonly Vector3 _axisZ;

        private VolumetricWorldBounds(
            Bounds axisAligned,
            Vector3 center,
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ)
        {
            AxisAligned = axisAligned;
            _center = center;
            _axisX = axisX;
            _axisY = axisY;
            _axisZ = axisZ;
        }

        public static VolumetricWorldBounds FromAxisAligned(Bounds bounds) => new(
            bounds,
            bounds.center,
            new Vector3(bounds.extents.x, 0f, 0f),
            new Vector3(0f, bounds.extents.y, 0f),
            new Vector3(0f, 0f, bounds.extents.z));

        public static VolumetricWorldBounds FromLocalBounds(
            Matrix4x4 localToWorld,
            Bounds localBounds,
            Bounds axisAligned)
        {
            var extents = localBounds.extents;
            return new VolumetricWorldBounds(
                axisAligned,
                localToWorld.MultiplyPoint3x4(localBounds.center),
                localToWorld.MultiplyVector(Vector3.right * extents.x),
                localToWorld.MultiplyVector(Vector3.up * extents.y),
                localToWorld.MultiplyVector(Vector3.forward * extents.z));
        }

        public VolumetricMediaBounds ToGpuData(bool textured) => new(
            _center,
            _axisX,
            _axisY,
            _axisZ,
            textured);
    }
}