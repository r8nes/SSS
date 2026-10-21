using System.Collections.Generic;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public static class MaterialOutline
    {
        private static readonly List<Material> Buffer = new();

        public static void Apply(IEnumerable<Renderer> renderers, Material outline, bool visible)
        {
            foreach (var target in renderers)
            {
                if (target == null)
                    continue;

                target.GetSharedMaterials(Buffer);
                Buffer.Remove(outline);

                if (visible)
                    Buffer.Add(outline);

                target.SetSharedMaterials(Buffer);
            }
        }
    }
}
