using System;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// Identifies standalone probe baking operations that bypass the regular Lightmapping bake lifecycle.
    /// </summary>
    public enum StandaloneProbeBakeType
    {
        AdaptiveProbeVolumes,
        ReflectionProbes
    }

    /// <summary>
    /// Provides a shared pre/post lifecycle for standalone probe baking.
    /// </summary>
    public static class StandaloneProbeBaking
    {
        public delegate bool PreBakeHandler(StandaloneProbeBakeType bakeType);

        public static event PreBakeHandler preBake;
        public static event Action<StandaloneProbeBakeType> postBake;

        static StandaloneProbeBakeType? s_ActiveBake;

        /// <summary>
        /// Bakes a reflection probe after preparing the environment through the standalone bake lifecycle.
        /// </summary>
        /// <param name="probe">The reflection probe to bake.</param>
        /// <param name="path">The asset path for the baked cubemap.</param>
        /// <returns>Whether environment preparation and the reflection probe bake succeeded.</returns>
        public static bool BakeReflectionProbe(ReflectionProbe probe, string path)
        {
            if (!Begin(StandaloneProbeBakeType.ReflectionProbes))
                return false;

            try
            {
                return UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
            }
            finally
            {
                End(StandaloneProbeBakeType.ReflectionProbes);
            }
        }

        internal static bool Begin(StandaloneProbeBakeType bakeType)
        {
            if (s_ActiveBake.HasValue)
                return false;

            var callbacks = preBake?.GetInvocationList();
            if (callbacks != null)
            {
                foreach (PreBakeHandler callback in callbacks)
                {
                    bool accepted;
                    try
                    {
                        accepted = callback(bakeType);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        accepted = false;
                    }

                    if (!accepted)
                    {
                        InvokePostBake(bakeType);
                        return false;
                    }
                }
            }

            s_ActiveBake = bakeType;
            return true;
        }

        internal static void End(StandaloneProbeBakeType bakeType)
        {
            if (s_ActiveBake != bakeType)
                return;

            s_ActiveBake = null;
            InvokePostBake(bakeType);
        }

        static void InvokePostBake(StandaloneProbeBakeType bakeType)
        {
            var callbacks = postBake?.GetInvocationList();
            if (callbacks == null)
                return;

            foreach (Action<StandaloneProbeBakeType> callback in callbacks)
            {
                try
                {
                    callback(bakeType);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
