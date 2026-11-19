using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// The yard as laid out in the scene. On Load the script takes over the marker placeholders under
    /// Markers: each opening entry binds the placeholder with its id (lane and load from the file),
    /// placeholders the script doesn't name are hidden, and an entry with no placeholder is spawned
    /// from its prefab. The file decides who is in the yard; the scene only supplies the look.
    /// </summary>
    public class YardView : MonoBehaviour
    {
        /// <summary>Top of the lane surface; markers rest here.</summary>
        public const float GroundY = 0.02f;

        [SerializeField] Transform markersRoot;
        [SerializeField] Transform northLane;
        [SerializeField] Transform southLane;
        [SerializeField] MarkerView forkliftPrefab;
        [SerializeField] MarkerView cratePrefab;
        [SerializeField] FlagView flagPrefab;
        [Tooltip("X of a lane's first slot, for markers the script places (no placeholder in that lane).")]
        [SerializeField] float firstSlotX = -5f;
        [Tooltip("Gap between slots in a lane.")]
        [SerializeField] float slotSpacing = 3.5f;
        [Tooltip("Lowest X a marker may rest at (clear of the lane sign).")]
        [SerializeField] float slotMinX = -6.5f;
        [Tooltip("Highest X a marker may rest at (clear of the EXIT gate).")]
        [SerializeField] float slotMaxX = 7f;
        [Tooltip("X past the lane's end where a marker goes when it leaves the yard.")]
        [SerializeField] float exitX = 9.5f;

        const float LeaveSeconds = 1.8f;
        const float RollSeconds = 0.7f;
        const float ClosingSeconds = 1.2f;

        readonly Dictionary<string, MarkerView> placeholders = new Dictionary<string, MarkerView>();
        readonly Dictionary<string, MarkerView> spawned = new Dictionary<string, MarkerView>();
        readonly Dictionary<string, MarkerView> active = new Dictionary<string, MarkerView>();
        readonly HashSet<string> carried = new HashSet<string>();   // named as a cue's `what`
        readonly HashSet<string> actors = new HashSet<string>();    // named as a cue's `who`

        public bool IsSetUp => markersRoot != null && northLane != null && southLane != null
            && forkliftPrefab != null && cratePrefab != null && flagPrefab != null;

        /// <summary>Highest X a marker may rest at; a carry turns back rather than drive past it.</summary>
        public float SlotMaxX => slotMaxX;

        /// <summary>Finds the placeholders in the scene. Call once, before the first Load.</summary>
        public void Prepare()
        {
            EnsureEventSystem();
            placeholders.Clear();
            foreach (var view in markersRoot.GetComponentsInChildren<MarkerView>(true))
                placeholders[view.name] = view;
        }

        /// <summary>The marker the current script knows by this id, or null.</summary>
        public MarkerView Get(string id) => id != null && active.TryGetValue(id, out var view) ? view : null;

        /// <summary>Every marker the current script has in the yard (including one on its way out).</summary>
        public IEnumerable<MarkerView> ActiveMarkers => active.Values;

        /// <summary>The Z of a lane's centre line.</summary>
        public float LaneZ(string lane) => (lane == Lanes.South ? southLane : northLane).position.z;

        /// <summary>The lane a world position is in (the nearer centre line).</summary>
        public string LaneOf(Vector3 position) =>
            Mathf.Abs(position.z - northLane.position.z) <= Mathf.Abs(position.z - southLane.position.z) ? Lanes.North : Lanes.South;

        /// <summary>Lays the yard out as the script opens.</summary>
        public void Rebuild(YardScript script, YardState state)
        {
            // The shape for each id comes from the roles cues give it (see Acquire).
            carried.Clear();
            actors.Clear();
            foreach (var cue in script.cues)
            {
                if (!string.IsNullOrEmpty(cue.what)) carried.Add(cue.what);
                if (!string.IsNullOrEmpty(cue.who)) actors.Add(cue.who);
            }

            // Hiding first also takes every crate off every forklift, so destroying a spawned
            // forklift below can never take a crate (or a placeholder) with it.
            foreach (var view in placeholders.Values) if (view != null) view.Hide();
            foreach (var view in spawned.Values) if (view != null) view.Hide();
            active.Clear();

            // Spawned markers the new script doesn't name are destroyed, so repeated Loads can't pile them up.
            var named = new HashSet<string>();
            foreach (var marker in state.Markers) named.Add(marker.Id);
            foreach (var id in new List<string>(spawned.Keys))
            {
                if (named.Contains(id)) continue;
                var view = spawned[id];
                spawned.Remove(id);
                if (view != null) Destroy(view.gameObject);
            }

            // A placeholder already in the file's lane keeps the spot it was given in the scene;
            // everything else takes the next free slot in the lane the file names.
            var unplaced = new List<KeyValuePair<MarkerView, MarkerState>>();
            foreach (var marker in state.Markers)
            {
                var view = Acquire(marker.Id);
                bool isPlaceholder = placeholders.TryGetValue(marker.Id, out var placeholder) && placeholder == view;
                if (isPlaceholder && LaneOf(view.AuthoredPosition) == marker.Lane)
                    Place(view, marker, new Vector3(view.AuthoredPosition.x, GroundY, view.AuthoredPosition.z));
                else
                    unplaced.Add(new KeyValuePair<MarkerView, MarkerState>(view, marker));
            }
            PlaceInFileOrder(unplaced, Lanes.North);
            PlaceInFileOrder(unplaced, Lanes.South);
        }

        // Markers without a spot of their own go after the lane's last marker, in the order the file lists
        // them, a slot apart; closer together if the lane is short of room. Only if even that won't fit does
        // each take the freest spot on the lane.
        void PlaceInFileOrder(List<KeyValuePair<MarkerView, MarkerState>> unplaced, string lane)
        {
            const float tightest = 1f;
            var queue = unplaced.FindAll(pair => pair.Value.Lane == lane);
            if (queue.Count == 0) return;

            bool empty = true;
            float last = 0f;
            foreach (var view in active.Values)
            {
                if (view == null || view.Lane != lane) continue;
                last = empty ? view.Rest.x : Mathf.Max(last, view.Rest.x);
                empty = false;
            }

            float first = empty ? Mathf.Clamp(firstSlotX, slotMinX, slotMaxX) : last + slotSpacing;
            float step = slotSpacing;
            if (first + step * (queue.Count - 1) > slotMaxX)
            {
                step = empty ? (slotMaxX - slotMinX) / Mathf.Max(1, queue.Count - 1) : (slotMaxX - last) / queue.Count;
                first = empty ? slotMinX : last + step;
            }

            for (int i = 0; i < queue.Count; i++)
            {
                var (view, marker) = (queue[i].Key, queue[i].Value);
                var rest = step >= tightest ? new Vector3(first + step * i, GroundY, LaneZ(lane)) : NextSlot(lane, view);
                Place(view, marker, rest);
            }
        }

        public void HaltMotion()
        {
            foreach (var view in active.Values)
                if (view != null) view.ClearTransients();
        }

        /// <summary>Shows a patch the state already holds: set loads roll to their new value, then gone markers leave the yard.</summary>
        public IEnumerator AnimatePatch(CueContext ctx, PatchResult result, YardState state)
        {
            var rolls = new List<(MarkerView view, string from, string to)>();
            foreach (var id in result.LoadsSet)
            {
                var view = Get(id);
                var marker = state.Get(id);
                if (view == null || marker == null) continue;
                rolls.Add((view, view.LoadText, marker.Load.ToString()));
            }
            if (rolls.Count > 0)
            {
                yield return ctx.Tween(RollSeconds, p =>
                {
                    foreach (var roll in rolls) roll.view.SetLoadRoll(roll.from, roll.to, p);
                }, ease: false);
                foreach (var roll in rolls) FinishRoll(roll.view, roll.to);
            }

            var leaving = new List<MarkerView>();
            foreach (var id in result.Removed)
            {
                var view = Get(id);
                if (view != null) leaving.Add(view);
            }
            if (leaving.Count == 0) yield break;

            // A crate that stays while the forklift carrying it leaves is set down in its own lane.
            var settling = new List<(MarkerView view, Vector3 from, Vector3 to)>();
            foreach (var view in active.Values)
            {
                if (view == null || view.CarriedBy == null || leaving.Contains(view) || !leaving.Contains(view.CarriedBy)) continue;
                var start = view.transform.position;
                view.DetachTo(start);
                var end = new Vector3(start.x, GroundY, LaneZ(view.Lane));
                view.SetRest(end, moveNow: false);
                settling.Add((view, start, end));
            }

            var from = StartLeaving(leaving);
            // Slow enough to follow: it slides the length of the lane, through the EXIT gate, then fades.
            yield return ctx.Tween(LeaveSeconds, p =>
            {
                Leave(leaving, from, p);
                float settle = Easing.InOut(Mathf.Clamp01(p / 0.4f));
                foreach (var s in settling) s.view.transform.position = Vector3.Lerp(s.from, s.to, settle);
            }, ease: false);
            foreach (var s in settling) s.view.SnapToRest();
            foreach (var view in leaving) Retire(view.Id);
        }

        /// <summary>
        /// Brings the yard to <paramref name="state"/> with motion (the end held to <c>closing</c>): loads roll,
        /// a marker that changes lane drives across, one that isn't in the state leaves through the EXIT gate,
        /// and one that only the state names fades in at a free slot.
        /// </summary>
        public IEnumerator AnimateClosing(CueContext ctx, YardState state)
        {
            var present = new HashSet<string>();
            foreach (var marker in state.Markers) present.Add(marker.Id);
            var leaving = new List<MarkerView>();
            foreach (var pair in active)
                if (pair.Value != null && !present.Contains(pair.Key)) leaving.Add(pair.Value);

            var rolls = new List<(MarkerView view, string from, string to)>();
            var moves = new List<(MarkerView view, Vector3 from, Vector3 to)>();
            var arriving = new List<MarkerView>();
            foreach (var marker in state.Markers)
            {
                var view = Get(marker.Id);
                if (view == null)
                {
                    view = Acquire(marker.Id);
                    Place(view, marker, NextSlot(marker.Lane, view));
                    view.SetFade(0f);
                    arriving.Add(view);
                    continue;
                }

                string load = marker.Load.ToString();
                if (view.LoadText != load) rolls.Add((view, view.LoadText, load));

                bool laneChanges = view.Lane != marker.Lane;
                bool carrierLeaves = view.CarriedBy != null && !present.Contains(view.CarriedBy.Id);
                if (!laneChanges && !carrierLeaves) continue;

                var start = view.transform.position;
                if (view.CarriedBy != null) view.DetachTo(start);
                view.Lane = marker.Lane;
                var end = laneChanges ? NextSlot(marker.Lane, view) : new Vector3(start.x, GroundY, LaneZ(marker.Lane));
                view.SetRest(end, moveNow: false);
                moves.Add((view, start, end));
            }

            var from = StartLeaving(leaving);
            yield return ctx.Tween(ClosingSeconds, p =>
            {
                float eased = Easing.InOut(p);
                foreach (var roll in rolls) roll.view.SetLoadRoll(roll.from, roll.to, p);
                foreach (var move in moves) move.view.transform.position = Vector3.Lerp(move.from, move.to, eased);
                foreach (var view in arriving) view.SetFade(eased);
                Leave(leaving, from, p);
            }, ease: false);

            foreach (var roll in rolls) FinishRoll(roll.view, roll.to);
            foreach (var move in moves) move.view.SnapToRest();
            foreach (var view in arriving) view.SetFade(1f);
            foreach (var view in leaving) Retire(view.Id);
        }

        /// <summary>Makes the yard equal the state at once: hides, adds, re-lanes, relabels and settles with no motion.</summary>
        public void SyncInstant(YardState state)
        {
            var present = new HashSet<string>();
            foreach (var marker in state.Markers) present.Add(marker.Id);
            foreach (var id in new List<string>(active.Keys))
                if (!present.Contains(id)) Retire(id);

            foreach (var marker in state.Markers)
            {
                var view = Get(marker.Id);
                if (view == null)
                {
                    view = Acquire(marker.Id);
                    Place(view, marker, NextSlot(marker.Lane, view));
                    continue;
                }
                view.SetLoad(marker.Load);
                view.ResetLoadLook();
                if (view.Lane != marker.Lane)
                {
                    if (view.CarriedBy != null) view.DetachTo(view.transform.position);
                    view.Lane = marker.Lane;
                    view.SetRest(NextSlot(marker.Lane, view));
                }
                // Anything caught mid-move (a cut during the closing motion) lands where it rests.
                if (view.CarriedBy == null) view.SnapToRest();
            }
        }

        // The view for an id: its placeholder if the scene has a matching one, else one spawned from the prefab.
        // Shape only, never an outcome: an id a cue carries (`what`) is a crate; one a cue names as `who` is a
        // forklift; an id no cue names keeps its placeholder's shape, or is a crate if its id says so.
        MarkerView Acquire(string id)
        {
            bool? role = carried.Contains(id) ? false : actors.Contains(id) ? true : (bool?)null;
            if (placeholders.TryGetValue(id, out var placeholder) && placeholder != null && (role == null || placeholder.HasForks == role.Value))
                return placeholder;
            bool forklift = role ?? id.IndexOf("crate", System.StringComparison.OrdinalIgnoreCase) < 0;
            if (spawned.TryGetValue(id, out var existing) && existing != null)
            {
                if (existing.HasForks == forklift) return existing;
                spawned.Remove(id);
                Destroy(existing.gameObject);
            }
            var view = Instantiate(forklift ? forkliftPrefab : cratePrefab, markersRoot);
            spawned[id] = view;
            return view;
        }

        void Place(MarkerView view, MarkerState marker, Vector3 rest)
        {
            view.Bind(marker.Id, marker.Lane, rest, flagPrefab);
            view.SetLoad(marker.Load);
            active[marker.Id] = view;
        }

        void Retire(string id)
        {
            if (!active.TryGetValue(id, out var view)) return;
            active.Remove(id);
            if (view != null) view.Hide();
        }

        static void FinishRoll(MarkerView view, string load)
        {
            view.SetLoadText(load);
            view.ResetLoadLook();
        }

        // Takes the leaving markers off any forks (and anything off theirs) and notes where each starts.
        static List<Vector3> StartLeaving(List<MarkerView> leaving)
        {
            var from = new List<Vector3>(leaving.Count);
            foreach (var view in leaving)
            {
                view.ReleaseCargo();
                if (view.CarriedBy != null) view.DetachTo(view.transform.position);
                from.Add(view.transform.position);
            }
            return from;
        }

        // p 0 → 1: slides each leaving marker down its lane through the EXIT gate, fading out over the last quarter.
        void Leave(List<MarkerView> leaving, List<Vector3> from, float p)
        {
            float gone = Easing.InOut(Mathf.Clamp01((p - 0.75f) / 0.25f));
            for (int i = 0; i < leaving.Count; i++)
            {
                var start = from[i];
                leaving[i].transform.position = Vector3.Lerp(start, new Vector3(exitX, start.y, start.z), Easing.InOut(p));
                leaving[i].SetFade(1f - gone);
            }
        }

        // A free resting spot in a lane: the next slot after the last marker while that still fits on the
        // lane; once the lane is full, the spot farthest from every marker already there (ties go toward
        // the EXIT end). Never off the lane.
        Vector3 NextSlot(string lane, MarkerView except)
        {
            var taken = new List<float>();
            foreach (var view in active.Values)
                if (view != null && view != except && view.Lane == lane && view.CarriedBy == null) taken.Add(view.Rest.x);

            float x;
            if (taken.Count == 0)
            {
                x = Mathf.Clamp(firstSlotX, slotMinX, slotMaxX);
            }
            else
            {
                float last = float.MinValue;
                foreach (float t in taken) last = Mathf.Max(last, t);
                x = last + slotSpacing;
                if (x > slotMaxX)
                {
                    float bestGap = -1f;
                    for (float candidate = slotMaxX; candidate >= slotMinX - 0.001f; candidate -= 0.25f)
                    {
                        float gap = float.MaxValue;
                        foreach (float t in taken) gap = Mathf.Min(gap, Mathf.Abs(t - candidate));
                        if (gap > bestGap) { bestGap = gap; x = candidate; }
                    }
                }
            }
            return new Vector3(x, GroundY, LaneZ(lane));
        }

        // The project uses the Input System only, so UI clicks need its UI input module. Created at
        // runtime because the module's default actions don't survive being saved from a script.
        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.SetActive(false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            go.SetActive(true);
        }
    }
}
