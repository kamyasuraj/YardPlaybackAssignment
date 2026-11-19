using System;
using UnityEngine;

namespace Yard.Cues
{
    /// <summary>
    /// How a cue type looks on screen, so it can't be confused with the others with the sound off:
    /// its colour (focus ring, word chip) and its word. <c>[CueLook("#3DDC84", "CARRY")]</c>.
    /// Optional: a cue type without it gets a neutral colour and its <c>say</c> in capitals.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CueLookAttribute : Attribute
    {
        public CueLookAttribute(string colorHex, string word)
        {
            ColorHex = colorHex;
            Word = word;
        }

        public string ColorHex { get; }
        public string Word { get; }
    }

    public readonly struct CueLook
    {
        public static readonly Color Neutral = new Color(0.9f, 0.9f, 0.92f);

        public CueLook(Color color, string word)
        {
            Color = color;
            Word = word;
        }

        public Color Color { get; }
        public string Word { get; }
    }
}
