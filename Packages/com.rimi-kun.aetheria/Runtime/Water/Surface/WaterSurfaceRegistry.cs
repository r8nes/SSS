using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace Aetheria.Runtime.Water
{
    internal static class WaterSurfaceRegistry
    {
        private static readonly List<WaterSurface> s_ActiveSurfaces = new(WaterLimits.MaxSurfaceCount);
        private static readonly List<RetiredResources> s_RetiredResources = new(WaterLimits.MaxSurfaceCount);

        private static int s_RenderOwnerCount;

        private readonly struct RetiredResources
        {
            internal readonly WaterSimulationState simulation;
            internal readonly GraphicsBuffer simulationConstantBuffer;
            internal readonly GraphicsBuffer spectrumConstantBuffer;
            internal readonly GraphicsBuffer surfaceConstantBuffer;

            internal RetiredResources(
                WaterSimulationState simulation,
                GraphicsBuffer simulationConstantBuffer,
                GraphicsBuffer spectrumConstantBuffer,
                GraphicsBuffer surfaceConstantBuffer)
            {
                this.simulation = simulation;
                this.simulationConstantBuffer = simulationConstantBuffer;
                this.spectrumConstantBuffer = spectrumConstantBuffer;
                this.surfaceConstantBuffer = surfaceConstantBuffer;
            }
        }

        internal static void Register(WaterSurface surface)
        {
            if (!surface || s_ActiveSurfaces.Contains(surface))
                return;

            s_ActiveSurfaces.Add(surface);
        }

        internal static void Unregister(WaterSurface surface)
        {
            if (!surface) return;

            int index = s_ActiveSurfaces.IndexOf(surface);
            
            if (index < 0) return;

            int last = s_ActiveSurfaces.Count - 1;
            s_ActiveSurfaces[index] = s_ActiveSurfaces[last];
            s_ActiveSurfaces.RemoveAt(last);
        }

        internal static void RegisterRenderOwner() => ++s_RenderOwnerCount;

        internal static void UnregisterRenderOwner()
        {
            s_RenderOwnerCount = Mathf.Max(0, s_RenderOwnerCount - 1);
            
            if (s_RenderOwnerCount == 0)
                ReleaseRetiredResources();
        }

        internal static void RetireResources(
            WaterSimulationState simulation,
            GraphicsBuffer simulationConstantBuffer,
            GraphicsBuffer spectrumConstantBuffer,
            GraphicsBuffer surfaceConstantBuffer)
        {
            if (simulation == null && simulationConstantBuffer == null && spectrumConstantBuffer == null && surfaceConstantBuffer == null)
                return;

            if (s_RenderOwnerCount == 0)
            {
                simulation?.Release();
                simulationConstantBuffer?.Release();
                spectrumConstantBuffer?.Release();
                surfaceConstantBuffer?.Release();
                return;
            }

            s_RetiredResources.Add(new RetiredResources(
                simulation,
                simulationConstantBuffer,
                spectrumConstantBuffer,
                surfaceConstantBuffer));
        }

        internal static void ReleaseRetiredResources()
        {
            for (int i = 0; i < s_RetiredResources.Count; ++i)
            {
                var resources = s_RetiredResources[i];
                resources.simulation?.Release();
                resources.simulationConstantBuffer?.Release();
                resources.spectrumConstantBuffer?.Release();
                resources.surfaceConstantBuffer?.Release();
            }

            s_RetiredResources.Clear();
        }

        internal static int CopyActiveSurfacesTo(WaterSurface[] destination)
        {
            int write = 0;
            
            for (int i = s_ActiveSurfaces.Count - 1; i >= 0; --i)
            {
                var surface = s_ActiveSurfaces[i];
                
                if (!surface)
                    s_ActiveSurfaces.RemoveAt(i);
            }

            for (int i = 0; i < s_ActiveSurfaces.Count && write < destination.Length; ++i)
            {
                var surface = s_ActiveSurfaces[i];
                
                if (!surface.isActiveAndEnabled)
                    continue;

                destination[write++] = surface;
            }

            for (int i = write; i < destination.Length; ++i)
                destination[i] = null;

            return write;
        }
        internal static WaterSurface FindClosestSurface(Vector3 position)
        {
            WaterSurface closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = s_ActiveSurfaces.Count - 1; i >= 0; --i)
            {
                WaterSurface surface = s_ActiveSurfaces[i];
                if (!surface) { s_ActiveSurfaces.RemoveAt(i); continue; }
                if (!surface.isActiveAndEnabled) continue;
                float distance = Mathf.Abs(Vector3.Dot(position - surface.transform.position, surface.transform.up));
                if (distance >= closestDistance) continue;
                closest = surface;
                closestDistance = distance;
            }
            return closest;
        }
    }
}
