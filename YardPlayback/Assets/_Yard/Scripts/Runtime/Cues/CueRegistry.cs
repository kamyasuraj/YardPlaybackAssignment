using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Yard.Cues
{
    /// <summary>
    /// Maps each cue's <c>say</c> to its handler and its look. Built once by finding every
    /// <see cref="ICueHandler"/> marked <see cref="CueTypeAttribute"/>, so adding a cue type never edits the player.
    /// </summary>
    public class CueRegistry
    {
        readonly Dictionary<string, ICueHandler> handlers = new Dictionary<string, ICueHandler>();
        readonly Dictionary<string, CueLook> looks = new Dictionary<string, CueLook>();

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
                    var cueType = type.GetCustomAttribute<CueTypeAttribute>();
                    if (cueType == null) continue;
                    var look = LookFrom(type.GetCustomAttribute<CueLookAttribute>(), cueType.Say);
                    registry.Register(cueType.Say, (ICueHandler)Activator.CreateInstance(type), look);
                }
            }
            return registry;
        }

        public void Register(string say, ICueHandler handler, CueLook look)
        {
            if (handlers.TryGetValue(say, out var existing))
            {
                Debug.LogWarning($"[Yard] Two handlers for cue '{say}'. Keeping {existing.GetType().Name}, ignoring {handler.GetType().Name}.");
                return;
            }
            handlers[say] = handler;
            looks[say] = look;
        }

        public bool TryGet(string say, out ICueHandler handler)
        {
            handler = null;
            return say != null && handlers.TryGetValue(say, out handler);
        }

        public CueLook LookOf(string say) => say != null && looks.TryGetValue(say, out var look) ? look : DefaultLook(say);

        static CueLook LookFrom(CueLookAttribute attribute, string say) =>
            attribute != null && ColorUtility.TryParseHtmlString(attribute.ColorHex, out var color)
                ? new CueLook(color, attribute.Word)
                : DefaultLook(say);

        static CueLook DefaultLook(string say) =>
            new CueLook(CueLook.Neutral, string.IsNullOrEmpty(say) ? "" : say.Replace('_', ' ').ToUpperInvariant());

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
