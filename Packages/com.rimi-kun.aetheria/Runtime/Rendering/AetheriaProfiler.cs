using System;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Aetheria.Runtime.Rendering
{
    internal static class AetheriaProfiler
    {
        private const string RootName = "Aetheria";
        
        private static readonly ProfilingSampler RootSampler = new(RootName);
        private static readonly ProfilerMarker RootCpuMarker = new(RootName);

        public readonly struct Marker
        {
            private readonly ProfilingSampler[] _samplers;
            private readonly ProfilerMarker[] _cpuGroupMarkers;

            internal Marker(string path)
            {
                path = NormalizeName(path);
                Name = GetLeafName(path);
                _samplers = CreateGroupSamplerPath(path);
                _cpuGroupMarkers = CreateCpuGroupMarkerPath(path);
                Sampler = new ProfilingSampler(Name);
                CpuMarker = new ProfilerMarker(Name);
            }

            public string Name { get; }
            internal ProfilingSampler Sampler { get; }
            public ProfilerMarker CpuMarker { get; }

            public GpuScope ScopeGpu(RenderGraph renderGraph) => new(renderGraph, _samplers);
            public CpuScope ScopeCpu() => new(_cpuGroupMarkers, CpuMarker);

            public ProfiledPass<IComputeRenderGraphBuilder> AddComputePass<TPassData>(
                RenderGraph renderGraph,
                out TPassData passData,
                [CallerFilePath] string file = "",
                [CallerLineNumber] int line = 0)
                where TPassData : class, new()
            {
                var scope = ScopeGpu(renderGraph);
                
                try
                {
                    var builder = renderGraph.AddComputePass(Name, out passData, Sampler, file, line);
                    return new ProfiledPass<IComputeRenderGraphBuilder>(builder, scope);
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }

            public ProfiledPass<IRasterRenderGraphBuilder> AddRasterPass<TPassData>(
                RenderGraph renderGraph,
                out TPassData passData,
                [CallerFilePath] string file = "",
                [CallerLineNumber] int line = 0)
                where TPassData : class, new()
            {
                var scope = ScopeGpu(renderGraph);
                
                try
                {
                    var builder = renderGraph.AddRasterRenderPass(Name, out passData, Sampler, file, line);
                    return new ProfiledPass<IRasterRenderGraphBuilder>(builder, scope);
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }

            public ProfiledPass<IUnsafeRenderGraphBuilder> AddUnsafePass<TPassData>(
                RenderGraph renderGraph,
                out TPassData passData,
                [CallerFilePath] string file = "",
                [CallerLineNumber] int line = 0)
                where TPassData : class, new()
            {
                var scope = ScopeGpu(renderGraph);
                
                try
                {
                    var builder = renderGraph.AddUnsafePass(Name, out passData, Sampler, file, line);
                    return new ProfiledPass<IUnsafeRenderGraphBuilder>(builder, scope);
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }

            public override string ToString() => Name;
        }

        public readonly struct ProfiledPass<TBuilder> : IDisposable
            where TBuilder : IDisposable
        {
            private readonly TBuilder _builder;
            private readonly GpuScope _gpuScope;

            internal ProfiledPass(TBuilder builder, GpuScope gpuScope)
            {
                _builder = builder;
                _gpuScope = gpuScope;
            }

            public TBuilder Builder => _builder;

            public void Dispose()
            {
                try
                {
                    _builder.Dispose();
                }
                finally
                {
                    _gpuScope.Dispose();
                }
            }
        }

        public readonly struct CpuScope : IDisposable
        {
            private readonly ProfilerMarker[] _groupMarkers;
            private readonly ProfilerMarker _marker;

            internal CpuScope(ProfilerMarker[] groupMarkers, ProfilerMarker marker)
            {
                _groupMarkers = groupMarkers;
                _marker = marker;

                RootCpuMarker.Begin();
                
                for (int i = 0; i < _groupMarkers.Length; i++)
                    _groupMarkers[i].Begin();
                
                _marker.Begin();
            }

            public void Dispose()
            {
                _marker.End();
                
                for (int i = _groupMarkers.Length - 1; i >= 0; i--)
                    _groupMarkers[i].End();
                
                RootCpuMarker.End();
            }
        }

        public readonly struct GpuScope : IDisposable
        {
            private readonly RenderGraph _renderGraph;
            private readonly ProfilingSampler[] _samplers;

            internal GpuScope(RenderGraph renderGraph, ProfilingSampler[] samplers)
            {
                _renderGraph = renderGraph;
                _samplers = samplers;
                
                if (_renderGraph == null)
                    return;

                _renderGraph.BeginProfilingSampler(RootSampler);
                
                for (int i = 0; i < _samplers.Length; i++)
                    _renderGraph.BeginProfilingSampler(_samplers[i]);
            }

            public void Dispose()
            {
                if (_renderGraph == null)
                    return;

                for (int i = _samplers.Length - 1; i >= 0; i--)
                    _renderGraph.EndProfilingSampler(_samplers[i]);

                _renderGraph.EndProfilingSampler(RootSampler);
            }
        }

        public static Marker CreateMarker(string name) => new(name);

        public static Marker CreateMarker(string group, string name) =>
            new(JoinName(group, name));

        private static string JoinName(string group, string name) =>
            NormalizeName(group) + "/" + NormalizeName(name);

        private static ProfilingSampler[] CreateGroupSamplerPath(string path)
        {
            int groupEnd = path.LastIndexOf('/');
            
            if (groupEnd < 0)
                return Array.Empty<ProfilingSampler>();

            int segmentCount = 1;
            
            for (int i = 0; i < groupEnd; i++)
            {
                if (path[i] == '/')
                    segmentCount++;
            }

            var samplers = new ProfilingSampler[segmentCount];
            int segmentStart = 0;
            int samplerIndex = 0;

            for (int i = 0; i <= groupEnd; i++)
            {
                if (i != groupEnd && path[i] != '/')
                    continue;

                samplers[samplerIndex++] = new ProfilingSampler(path.Substring(segmentStart, i - segmentStart));
                segmentStart = i + 1;
            }

            return samplers;
        }

        private static ProfilerMarker[] CreateCpuGroupMarkerPath(string path)
        {
            int groupEnd = path.LastIndexOf('/');
            
            if (groupEnd < 0)
                return Array.Empty<ProfilerMarker>();

            int segmentCount = 1;
            
            for (int i = 0; i < groupEnd; i++)
            {
                if (path[i] == '/')
                    segmentCount++;
            }

            var markers = new ProfilerMarker[segmentCount];
            int segmentStart = 0;
            int markerIndex = 0;

            for (int i = 0; i <= groupEnd; i++)
            {
                if (i != groupEnd && path[i] != '/')
                    continue;

                markers[markerIndex++] = new ProfilerMarker(path.Substring(segmentStart, i - segmentStart));
                segmentStart = i + 1;
            }

            return markers;
        }

        private static string NormalizeName(string name) =>
            string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Profiler name cannot be null or empty.", nameof(name))
                : name.Trim().Trim('/');

        private static string GetLeafName(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator >= 0 ? path[(separator + 1)..] : path;
        }
    }
}