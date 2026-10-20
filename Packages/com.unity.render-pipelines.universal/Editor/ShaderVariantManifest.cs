using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    /// <summary>
    /// Supplies the runtime shader configurations supported by a project. Providers are discovered
    /// before URP keyword prefiltering, so build results do not depend on the currently previewed quality.
    /// </summary>
    public interface IShaderVariantManifestProvider
    {
        void PopulateShaderVariantManifest(ShaderVariantManifestBuilder builder);
    }

    /// <summary>Collects complete, allowed keyword configurations rather than independent keyword sets.</summary>
    public sealed class ShaderVariantManifestBuilder
    {
        readonly ShaderVariantManifest m_Manifest;

        internal ShaderVariantManifestBuilder(ShaderVariantManifest manifest) => m_Manifest = manifest;

        public void RequireScreenSpaceReflectionConsumer() => m_Manifest.requireScreenSpaceReflectionConsumer = true;
        public void RequireScreenSpaceOcclusionConsumer() => m_Manifest.requireScreenSpaceOcclusionConsumer = true;

        public void AddSoftShadowQuality(SoftShadowQuality quality)
        {
            if (quality != SoftShadowQuality.UsePipelineSettings)
                m_Manifest.softShadowQualities.Add(quality);
        }

        public void AddShaderVariant(string shaderName, string passName, string[] controlledKeywords, params string[] enabledKeywords)
            => m_Manifest.AddAllowedVariant(false, shaderName, passName, controlledKeywords, enabledKeywords);

        public void AddComputeShaderVariant(string shaderName, string kernelName, string[] controlledKeywords, params string[] enabledKeywords)
            => m_Manifest.AddAllowedVariant(true, shaderName, kernelName, controlledKeywords, enabledKeywords);
    }

    internal sealed class ShaderVariantManifest
    {
        internal sealed class Rule
        {
            internal readonly HashSet<string> controlledKeywords = new(StringComparer.Ordinal);
            internal readonly List<HashSet<string>> allowedVariants = new();
        }

        static ShaderVariantManifest s_Current;
        readonly Dictionary<string, Rule> m_ShaderRules = new(StringComparer.Ordinal);
        readonly Dictionary<string, Rule> m_ComputeShaderRules = new(StringComparer.Ordinal);

        internal bool requireScreenSpaceReflectionConsumer;
        internal bool requireScreenSpaceOcclusionConsumer;
        internal readonly HashSet<SoftShadowQuality> softShadowQualities = new();

        internal static ShaderVariantManifest current => s_Current ??= Gather();

        internal static void Refresh() => s_Current = Gather();

        static ShaderVariantManifest Gather()
        {
            var manifest = new ShaderVariantManifest();
            var builder = new ShaderVariantManifestBuilder(manifest);
            foreach (Type providerType in TypeCache.GetTypesDerivedFrom<IShaderVariantManifestProvider>())
            {
                if (providerType.IsAbstract || providerType.IsInterface)
                    continue;

                try
                {
                    if (Activator.CreateInstance(providerType) is IShaderVariantManifestProvider provider)
                        provider.PopulateShaderVariantManifest(builder);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Failed to build shader variant manifest with '{providerType.FullName}': {exception}");
                }
            }

            return manifest;
        }

        internal void AddAllowedVariant(bool compute, string shaderName, string passOrKernelName, string[] controlled, string[] enabled)
        {
            if (string.IsNullOrWhiteSpace(shaderName) || string.IsNullOrWhiteSpace(passOrKernelName) || controlled == null)
                throw new ArgumentException("A shader variant rule requires a shader, pass or kernel, and controlled keywords.");

            Dictionary<string, Rule> rules = compute ? m_ComputeShaderRules : m_ShaderRules;
            string key = GetRuleKey(shaderName, passOrKernelName);
            if (!rules.TryGetValue(key, out Rule rule))
                rules.Add(key, rule = new Rule());

            foreach (string keyword in controlled)
                if (!string.IsNullOrWhiteSpace(keyword))
                    rule.controlledKeywords.Add(keyword);

            rule.allowedVariants.Add(enabled == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(enabled, StringComparer.Ordinal));
        }

        internal bool Allows(Shader shader, ShaderSnippetData snippet, ShaderKeywordSet keywordSet)
        {
            if (!m_ShaderRules.TryGetValue(GetRuleKey(shader.name, snippet.passName), out Rule rule))
                return true;

            return Allows(rule, keywordSet, shader.keywordSpace.FindKeyword);
        }

        internal bool Allows(ComputeShader shader, string kernelName, ShaderKeywordSet keywordSet)
        {
            if (!m_ComputeShaderRules.TryGetValue(GetRuleKey(shader.name, kernelName), out Rule rule))
                return true;

            return Allows(rule, keywordSet, shader.keywordSpace.FindKeyword);
        }

        static bool Allows(Rule rule, ShaderKeywordSet keywordSet, Func<string, LocalKeyword> findKeyword)
        {
            var keywordStates = new Dictionary<string, bool>(rule.controlledKeywords.Count, StringComparer.Ordinal);
            foreach (string keywordName in rule.controlledKeywords)
            {
                LocalKeyword keyword = findKeyword(keywordName);
                if (!keyword.isValid)
                    return true;

                keywordStates.Add(keywordName, keywordSet.IsEnabled(keyword));
            }

            foreach (HashSet<string> allowed in rule.allowedVariants)
            {
                bool matches = true;
                foreach (KeyValuePair<string, bool> keyword in keywordStates)
                {
                    if (keyword.Value != allowed.Contains(keyword.Key))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return true;
            }

            return false;
        }

        static string GetRuleKey(string shaderName, string passOrKernelName) => shaderName + "\n" + passOrKernelName;
    }

    sealed class ManifestComputeShaderStripper : IPreprocessComputeShaders
    {
        public int callbackOrder => -1000;

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            ShaderVariantManifest manifest = ShaderVariantManifest.current;
            for (int index = data.Count - 1; index >= 0; --index)
                if (!manifest.Allows(shader, kernelName, data[index].shaderKeywordSet))
                    data.RemoveAt(index);
        }
    }
}
