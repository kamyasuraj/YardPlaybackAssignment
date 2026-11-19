using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Yard.Cues
{
    /// <summary>
    /// Maps each cue's <c>say</c> to its handler. Built once by finding every <see cref="ICueHandler"/>
    /// marked <see cref="CueTypeAttribute"/>, so adding a cue type never edits the player.
    /// </summary>
    public class CueRegistry
    {
        readonly Dictionary<string, ICueHandler> handlers = new Dictionary<string, ICueHandler>();

        public IEnumerable<string> CueTypes => handlers.Keys;

        public static CueRegistry Discover()
        {
            var registry = new CueRegistry();
            var home = typeof(ICueHandler).Assembly;
            string homeName = home.GetName().Name;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != home && !References(assembly, homeName)) continue;
                foreach (var type in TypesOf(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(ICueHandler).IsAssignableFrom(type)) continue;
                    var attribute = type.GetCustomAttribute<CueTypeAttribute>();
                    if (attribute == null) continue;
                    registry.Register(attribute.Say, (ICueHandler)Activator.CreateInstance(type));
                }
            }
            return registry;
        }

        public void Register(string say, ICueHandler handler)
        {
            if (handlers.TryGetValue(say, out var existing))
            {
                Debug.LogWarning($"[Yard] Two handlers for cue '{say}'. Keeping {existing.GetType().Name}, ignoring {handler.GetType().Name}.");
                return;
            }
            handlers[say] = handler;
        }

        public bool TryGet(string say, out ICueHandler handler)
        {
            handler = null;
            return say != null && handlers.TryGetValue(say, out handler);
        }

        static bool References(Assembly assembly, string name)
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
                if (reference.Name == name) return true;
            return false;
        }

        static Type[] TypesOf(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types; }
        }
    }
}
