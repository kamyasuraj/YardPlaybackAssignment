using System;

namespace Yard.Cues
{
    /// <summary>Names the cue type a handler plays: <c>[CueType("carry")]</c> handles <c>"say": "carry"</c>.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CueTypeAttribute : Attribute
    {
        public CueTypeAttribute(string say) { Say = say; }

        public string Say { get; }
    }
}
