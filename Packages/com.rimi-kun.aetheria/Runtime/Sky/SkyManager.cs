using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aetheria.Runtime.Overrides;
using UnityEngine.Rendering;

namespace Aetheria.Runtime.Sky
{
    public static class SkyManager
    {
        private static Dictionary<int, Type> _skyTypes;

        public static IReadOnlyDictionary<int, Type> SkyTypes
        {
            get
            {
                EnsureRegistry();
                return _skyTypes;
            }
        }

        public static SkySettings GetSkySetting(VolumeStack stack)
        {
            if (stack == null)
                return null;

            var visualEnvironment = stack.GetComponent<VisualEnvironment>();
            if (!visualEnvironment || !visualEnvironment.active)
                return null;

            EnsureRegistry();
            if (!_skyTypes.TryGetValue(visualEnvironment.skyType.value, out var skyType))
                return null;

            var settings = stack.GetComponent(skyType) as SkySettings;
            return settings != null && settings.IsActive() ? settings : null;
        }

        private static void EnsureRegistry()
        {
            if (_skyTypes != null)
                return;

            _skyTypes = new Dictionary<int, Type>();
            foreach (var type in GetAllSkyTypes().OrderBy(SkySettings.GetUniqueID))
            {
                int uniqueID = SkySettings.GetUniqueID(type);
                if (uniqueID <= 0)
                    throw new InvalidOperationException(
                        $"Aetheria sky type '{type.FullName}' must declare a positive SkyUniqueID.");
                if (_skyTypes.ContainsKey(uniqueID))
                    throw new InvalidOperationException(
                        $"Aetheria sky ID {uniqueID} is already registered by '{_skyTypes[uniqueID].FullName}'.");

                _skyTypes.Add(uniqueID, type);
            }
        }

        private static IEnumerable<Type> GetAllSkyTypes()
        {
            var baseType = typeof(SkySettings);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                foreach (var type in types)
                {
                    if (type != null && !type.IsAbstract && baseType.IsAssignableFrom(type))
                        yield return type;
                }
            }
        }
    }
}
