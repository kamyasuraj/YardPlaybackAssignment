using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yard.Core
{
    public class MarkerState
    {
        public string Id;
        public string Lane;
        public int Load;
    }

    /// <summary>
    /// The yard's truth: who is present, in which lane, with what load.
    /// Loads change only through <see cref="ApplyPatch"/> (patch.set). Nothing here reads Cue.amount.
    /// </summary>
    public class YardState
    {
        readonly List<MarkerState> markers = new List<MarkerState>();

        public IReadOnlyList<MarkerState> Markers => markers;

        public static YardState FromOpening(YardScript script)
        {
            var state = new YardState();
            foreach (var entry in script.opening) state.Add(entry.id, entry.lane, entry.load);
            return state;
        }

        public MarkerState Get(string id) => markers.Find(m => m.Id == id);

        public bool Has(string id) => Get(id) != null;

        /// <summary>patch.set writes loads; patch.gone removes markers from the yard.</summary>
        public PatchResult ApplyPatch(Patch patch)
        {
            var result = new PatchResult();
            if (patch == null) return result;

            if (patch.set != null)
            {
                foreach (var set in patch.set)
                {
                    if (set == null) continue;
                    var marker = Get(set.id);
                    if (marker == null) { result.UnknownIds.Add(set.id); continue; }
                    marker.Load = set.load;
                    result.LoadsSet.Add(marker.Id);
                }
            }

            if (patch.gone != null)
            {
                foreach (var id in patch.gone)
                {
                    var marker = Get(id);
                    if (marker == null) { result.UnknownIds.Add(id); continue; }
                    markers.Remove(marker);
                    result.Removed.Add(id);
                }
            }

            return result;
        }

        /// <summary>Makes who is present, their lanes and their loads equal <paramref name="closing"/>. Idempotent.</summary>
        public void EnforceClosing(MarkerEntry[] closing)
        {
            var keep = new HashSet<string>();
            foreach (var entry in closing) keep.Add(entry.id);
            markers.RemoveAll(m => !keep.Contains(m.Id));

            foreach (var entry in closing)
            {
                var marker = Get(entry.id);
                if (marker == null) { Add(entry.id, entry.lane, entry.load); continue; }
                marker.Lane = entry.lane;
                marker.Load = entry.load;
            }
        }

        public bool Matches(MarkerEntry[] closing)
        {
            if (closing == null || closing.Length != markers.Count) return false;
            foreach (var entry in closing)
            {
                var marker = Get(entry.id);
                if (marker == null || marker.Lane != entry.lane || marker.Load != entry.load) return false;
            }
            return true;
        }

        /// <summary>Who is left and each load, as JSON: {"ticket":..,"left":[{"id","lane","load"}]}.</summary>
        public string ToPayloadJson(string ticket)
        {
            var payload = new FinishedPayload { ticket = ticket, left = new MarkerEntry[markers.Count] };
            for (int i = 0; i < markers.Count; i++)
                payload.left[i] = new MarkerEntry { id = markers[i].Id, lane = markers[i].Lane, load = markers[i].Load };
            return JsonUtility.ToJson(payload);
        }

        void Add(string id, string lane, int load)
        {
            if (Has(id)) return;
            markers.Add(new MarkerState { Id = id, Lane = lane, Load = load });
        }
    }

    public class PatchResult
    {
        public readonly List<string> LoadsSet = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> UnknownIds = new List<string>();
    }

    [Serializable]
    public class FinishedPayload
    {
        public string ticket;
        public MarkerEntry[] left;
    }
}
