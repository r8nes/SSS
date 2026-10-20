using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Aetheria.Runtime.Rendering.Fog.Volumetrics
{
    internal static class VolumetricUtility
    {
        public static int AppendVectorHash(int hash, Vector4 value)
        {
            unchecked
            {
                hash = hash * 31 + Mathf.RoundToInt(value.x * 10000f);
                hash = hash * 31 + Mathf.RoundToInt(value.y * 10000f);
                hash = hash * 31 + Mathf.RoundToInt(value.z * 10000f);
                hash = hash * 31 + Mathf.RoundToInt(value.w * 10000f);
                return hash;
            }
        }
    }

    internal sealed class VolumetricBufferSet<T> : IDisposable where T : struct
    {
        private static readonly T[] EmptyData = { default };

        private bool _hasUploadedState;
        private int _uploadedStateHash;

        public GraphicsBuffer Data { get; private set; }

        public void Upload(List<T> source, int count, int stateHash)
        {
            EnsureCapacity(Mathf.Max(1, count));
            if (_hasUploadedState && stateHash == _uploadedStateHash)
                return;

            if (count > 0)
            {
                Data.SetData(source, 0, 0, count);
            }
            else
            {
                Data.SetData(EmptyData);
            }

            _uploadedStateHash = stateHash;
            _hasUploadedState = true;
        }

        private void EnsureCapacity(int requiredCount)
        {
            int dataStride = Marshal.SizeOf<T>();
            bool dataValid = Data != null && Data.IsValid() && Data.count >= requiredCount && Data.stride == dataStride;

            if (!dataValid)
            {
                Data?.Release();
                Data = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredCount, dataStride);
            }

            if (!dataValid)
                _hasUploadedState = false;
        }

        public void Dispose()
        {
            Data?.Release();
            Data = null;
            _hasUploadedState = false;
            _uploadedStateHash = 0;
        }
    }
}