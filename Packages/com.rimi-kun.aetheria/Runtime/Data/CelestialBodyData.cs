using System.Runtime.InteropServices;
using UnityEngine;

namespace Aetheria.Runtime.Data
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CelestialBodyData
    {
        public const int EmissionType = 0;
        public const int ReflectiveType = 1;

        public Vector3 color;
        public float radius;

        public Vector3 forward;
        public float distanceFromCamera;

        public Vector3 right;
        public float angularRadius;

        public Vector3 up;
        public int type;

        public Vector3 surfaceColor;
        public float earthshine;

        public Vector4 cookieAtlasScaleOffset;

        public Vector3 sunDirection;
        public float limbDarkening;
        public float cookieRotation;
        public float lunarLambertWeight;

        public Vector3 flareColor;
        public float flareFalloff;

        public float flareCosInner;
        public float flareCosOuter;
        public float flareSize;
        public float padding;
    }
}