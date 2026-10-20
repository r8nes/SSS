using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering.Universal
{
    internal sealed class FilmGrainPostProcessEffect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Setup(Material material, PostProcessData resources, FilmGrain filmGrain, int width, int height, bool hasFinalPass)
        {
            if (!hasFinalPass && filmGrain.IsActive())
                SetupFinalPass(material, resources, filmGrain, width, height);
        }

        public void SetupFinalPass(Material material, PostProcessData resources, FilmGrain filmGrain, int width, int height)
        {
            if (!filmGrain.IsActive())
                return;

            PostProcessUtils.ConfigureFilmGrain(
                resources,
                filmGrain,
                width, height,
                material
            );
        }
    }
}
