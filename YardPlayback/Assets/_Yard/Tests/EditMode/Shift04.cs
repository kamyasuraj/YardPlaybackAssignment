using System.IO;
using NUnit.Framework;
using Yard.Core;

namespace Yard.Tests
{
    /// <summary>The brief's script, read from the same file the scene plays.</summary>
    static class Shift04
    {
        public const string Path = "Assets/_Yard/Data/shift-04.json";

        public static string Json => File.ReadAllText(Path);

        public static YardScript Parse()
        {
            Assert.IsTrue(ScriptParser.TryParse(Json, out var script, out var error), error);
            return script;
        }

        public static int IndexOf(YardScript script, string say)
        {
            for (int i = 0; i < script.cues.Length; i++)
                if (script.cues[i].say == say) return i;
            return -1;
        }
    }
}
