using System;
using UnityEngine;

namespace Yard.Core
{
    public static class Lanes
    {
        public const string North = "north";
        public const string South = "south";

        public static bool IsValid(string lane) => lane == North || lane == South;
    }

    /// <summary>
    /// Turns the page's one-string script into a <see cref="YardScript"/>, or says why it can't.
    /// Never throws: bad input is rejected with a message and the caller keeps what it had.
    /// </summary>
    public static class ScriptParser
    {
        public const int MaxLength = 256 * 1024;

        public static bool TryParse(string json, out YardScript script, out string error)
        {
            script = null;
            if (string.IsNullOrWhiteSpace(json)) return Fail("the script is empty", out error);
            if (json.Length > MaxLength) return Fail($"the script is {json.Length} characters; the limit is {MaxLength}", out error);

            YardScript parsed;
            try
            {
                parsed = JsonUtility.FromJson<YardScript>(json);
            }
            catch (Exception e)
            {
                return Fail("the script is not valid JSON (" + e.Message + ")", out error);
            }

            if (parsed == null) return Fail("the script is not a JSON object", out error);
            if (parsed.opening == null) return Fail("the script has no 'opening'", out error);
            if (parsed.cues == null) return Fail("the script has no 'cues'", out error);
            if (parsed.closing == null) return Fail("the script has no 'closing'", out error);
            if (!CheckMarkers(parsed.opening, "opening", out error)) return false;
            if (!CheckMarkers(parsed.closing, "closing", out error)) return false;

            for (int i = 0; i < parsed.cues.Length; i++)
            {
                var cue = parsed.cues[i];
                if (cue == null || string.IsNullOrEmpty(cue.say)) return Fail($"cue {i + 1} has no 'say'", out error);
                if (cue.patch == null) cue.patch = new Patch();
                if (cue.patch.set == null) cue.patch.set = Array.Empty<LoadSet>();
                if (cue.patch.gone == null) cue.patch.gone = Array.Empty<string>();
            }

            script = parsed;
            error = null;
            return true;
        }

        static bool CheckMarkers(MarkerEntry[] markers, string where, out string error)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null || string.IsNullOrEmpty(m.id)) return Fail($"'{where}' entry {i + 1} has no 'id'", out error);
                if (!Lanes.IsValid(m.lane)) return Fail($"'{where}' marker '{m.id}' has lane '{m.lane}'; expected 'north' or 'south'", out error);
            }
            error = null;
            return true;
        }

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
