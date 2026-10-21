using System.Collections.Generic;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public static class OutlineMeshes
    {
        private const float WeldPrecision = 10000f;

        private static readonly Dictionary<Mesh, Mesh> Smoothed = new();

        public static bool TryGetSmoothed(Mesh source, out Mesh smoothed)
        {
            if (Smoothed.TryGetValue(source, out smoothed) && smoothed != null)
                return true;

            if (!source.isReadable && !Application.isEditor)
            {
                smoothed = null;
                return false;
            }

            smoothed = CreateSmoothed(source);
            Smoothed[source] = smoothed;
            return true;
        }

        public static Mesh CreateSmoothed(Mesh source)
        {
            var copy = Object.Instantiate(source);
            copy.name = $"{source.name} (Outline)";

            var vertices = copy.vertices;
            var normals = copy.normals;
            var sums = new Dictionary<Vector3Int, Vector3>();

            for (var i = 0; i < vertices.Length; i++)
            {
                var key = Weld(vertices[i]);
                sums.TryGetValue(key, out var sum);
                sums[key] = sum + normals[i];
            }

            for (var i = 0; i < vertices.Length; i++)
            {
                var averaged = sums[Weld(vertices[i])];
                normals[i] = averaged.sqrMagnitude > 1e-8f ? averaged.normalized : normals[i];
            }

            copy.normals = normals;
            return copy;
        }

        private static Vector3Int Weld(Vector3 position) =>
            new(Mathf.RoundToInt(position.x * WeldPrecision), Mathf.RoundToInt(position.y * WeldPrecision), Mathf.RoundToInt(position.z * WeldPrecision));
    }
}
