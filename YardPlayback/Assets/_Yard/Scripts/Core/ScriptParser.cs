using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// It rejects rather than guesses, so the yard never shows a value the file doesn't hold.
    /// </summary>
    public static class ScriptParser
    {
        public const int MaxLength = 256 * 1024;
        public const int MaxDepth = 32;          // the brief's script nests 6 deep
        public const int MaxMarkers = 64;        // per opening / closing
        public const int MaxCues = 512;
        public const int MaxPatchEntries = 64;   // per patch.set / patch.gone
        public const int MaxText = 64;           // characters in any id, lane, say, flag or ticket

        /// <summary>The page reaches the player by this GameObject name, so no marker may take it.</summary>
        public const string ReservedId = "YardPlayer";

        // Every character strict JSON allows outside a string: whitespace, structure, numbers, and the letters
        // of true / false / null. Anything else (a quote of another kind, a comment) could hide text from the
        // scan, so it is refused whatever the JSON reader would make of it.
        const string StrictJsonOutsideStrings = " \t\r\n{}[]:,-+.0123456789eEtrufalsn";

        // Fields that must hold whole numbers. JsonUtility would quietly turn "three" into 0 and 2.5 into 2.
        static readonly HashSet<string> WholeNumberFields = new HashSet<string> { "load", "minute", "amount" };

        public static bool TryParse(string json, out YardScript script, out string error)
        {
            script = null;
            if (string.IsNullOrWhiteSpace(json)) return Fail("the script is empty", out error);
            if (json.Length > MaxLength) return Fail($"the script is {json.Length} characters; the limit is {MaxLength}", out error);
            if (!Scan(json, out error)) return false;

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
            if (!CheckText(parsed.ticket, "ticket", out error)) return false;
            if (!CheckMarkers(parsed.opening, "opening", out error)) return false;
            if (!CheckMarkers(parsed.closing, "closing", out error)) return false;
            if (parsed.cues.Length > MaxCues) return Fail($"the script has {parsed.cues.Length} cues; the limit is {MaxCues}", out error);

            for (int i = 0; i < parsed.cues.Length; i++)
            {
                var cue = parsed.cues[i];
                string where = $"cue {i + 1}";
                if (cue == null || string.IsNullOrEmpty(cue.say)) return Fail($"{where} has no 'say'", out error);
                if (!CheckText(cue.say, where + " 'say'", out error)) return false;
                if (!CheckText(cue.who, where + " 'who'", out error)) return false;
                if (!CheckText(cue.what, where + " 'what'", out error)) return false;
                if (!CheckText(cue.flag, where + " 'flag'", out error)) return false;

                if (cue.patch == null) cue.patch = new Patch();
                if (cue.patch.set == null) cue.patch.set = Array.Empty<LoadSet>();
                if (cue.patch.gone == null) cue.patch.gone = Array.Empty<string>();
                if (cue.patch.set.Length > MaxPatchEntries || cue.patch.gone.Length > MaxPatchEntries)
                    return Fail($"{where} patches more than {MaxPatchEntries} markers", out error);
                foreach (var set in cue.patch.set)
                    if (set != null && !CheckText(set.id, where + " patch.set id", out error)) return false;
                foreach (var id in cue.patch.gone)
                    if (!CheckText(id, where + " patch.gone id", out error)) return false;
            }

            script = parsed;
            error = null;
            return true;
        }

        static bool CheckMarkers(MarkerEntry[] markers, string where, out string error)
        {
            if (markers.Length > MaxMarkers) return Fail($"'{where}' has {markers.Length} markers; the limit is {MaxMarkers}", out error);
            var seen = new HashSet<string>();
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null || string.IsNullOrEmpty(m.id)) return Fail($"'{where}' entry {i + 1} has no 'id'", out error);
                if (!CheckText(m.id, $"'{where}' entry {i + 1} id", out error)) return false;
                if (m.id == ReservedId) return Fail($"'{where}' uses the id '{ReservedId}', which is reserved for the player", out error);
                if (!seen.Add(m.id)) return Fail($"'{where}' lists '{m.id}' twice", out error);
                if (!Lanes.IsValid(m.lane)) return Fail($"'{where}' marker '{m.id}' has lane '{Clip(m.lane)}'; expected 'north' or 'south'", out error);
            }
            error = null;
            return true;
        }

        static bool CheckText(string value, string what, out string error)
        {
            if (value != null && value.Length > MaxText)
                return Fail($"{what} is {value.Length} characters; the limit is {MaxText}", out error);
            error = null;
            return true;
        }

        // One pass over the raw text, before JsonUtility sees it: limits nesting (deep nesting could overflow
        // the native parser) and checks that load / minute / amount hold whole numbers. Text inside strings,
        // escaped quotes included, is skipped. It holds the script to strict JSON so nothing can hide from it:
        // no comments (a '/' outside a string), no escapes in keys, and stray closing brackets can't lower
        // the depth count. Unbalanced brackets are otherwise left for JsonUtility to reject.
        static bool Scan(string json, out string error)
        {
            int depth = 0;
            string lastKey = null;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int start = i + 1;
                    i = EndOfString(json, i);
                    string text = json.Substring(start, Math.Max(0, i - start));
                    bool isKey = NextNonSpace(json, i + 1) == ':';
                    if (isKey && text.IndexOf('\\') >= 0) return Fail($"the key \"{Clip(text)}\" uses an escape; write keys plainly", out error);
                    lastKey = isKey ? text : null;
                    continue;
                }
                if (c == '/') return Fail("the script has a '/' outside a string; comments are not allowed", out error);
                if (StrictJsonOutsideStrings.IndexOf(c) < 0) return Fail($"the script has '{c}' outside a string; it must be plain JSON", out error);
                if (c == '{' || c == '[')
                {
                    if (++depth > MaxDepth) return Fail($"the script nests deeper than {MaxDepth} levels", out error);
                }
                else if (c == '}' || c == ']')
                {
                    if (depth > 0) depth--;
                }
                else if (c == ':' && lastKey != null && WholeNumberFields.Contains(lastKey))
                {
                    int valueStart = SkipSpace(json, i + 1);
                    int valueEnd = valueStart;
                    while (valueEnd < json.Length && ",}] \t\r\n".IndexOf(json[valueEnd]) < 0) valueEnd++;
                    string raw = json.Substring(valueStart, valueEnd - valueStart);
                    if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                        return Fail($"'{lastKey}' must be a whole number, not {Clip(raw)}", out error);
                    i = valueEnd - 1;
                    lastKey = null;
                }
            }
            error = null;
            return true;
        }

        // Index of the closing quote of the string starting at `open`, honouring backslash escapes.
        static int EndOfString(string json, int open)
        {
            for (int i = open + 1; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return json.Length;
        }

        static int SkipSpace(string json, int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return i;
        }

        static char NextNonSpace(string json, int i)
        {
            i = SkipSpace(json, i);
            return i < json.Length ? json[i] : '\0';
        }

        // Error messages echo a little of the bad value, never all of it.
        static string Clip(string value) =>
            value == null ? "(none)" : value.Length <= 24 ? value : value.Substring(0, 24) + "…";

        static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
