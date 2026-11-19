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
        [Tooltip("X past the lane's end where a marker goes when it leaves the yard.")]
        [SerializeField] float exitX = 9.5f;

        readonly Dictionary<string, MarkerView> placeholders = new Dictionary<string, MarkerView>();
        readonly Dictionary<string, MarkerView> spawned = new Dictionary<string, MarkerView>();
        readonly Dictionary<string, MarkerView> active = new Dictionary<string, MarkerView>();
        readonly Dictionary<string, float> laneEnd = new Dictionary<string, float>();
        readonly HashSet<string> cargo = new HashSet<string>();

        public bool IsSetUp => markersRoot != null && northLane != null && southLane != null
            && forkliftPrefab != null && cratePrefab != null && flagPrefab != null;

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

        /// <summary>Lays the yard out as the script opens.</summary>
        public void Rebuild(YardScript script, YardState state)
        {
            // Anything a cue names as `what` is cargo (a crate); the rest are forklifts.
            cargo.Clear();
            foreach (var cue in script.cues)
                if (!string.IsNullOrEmpty(cue.what)) cargo.Add(cue.what);

            foreach (var view in placeholders.Values) if (view != null) view.Hide();
            foreach (var view in spawned.Values) if (view != null) view.Hide();
            active.Clear();
            laneEnd.Clear();

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
            foreach (var pair in unplaced) Place(pair.Key, pair.Value, NextSlot(pair.Value.Lane));
        }

        public void HaltMotion()
        {
            foreach (var view in active.Values)
                if (view != null) view.ClearTransients();
        }

        /// <summary>Shows a patch the state already holds: set loads pulse, then gone markers leave the yard.</summary>
        public IEnumerator AnimatePatch(CueContext ctx, PatchResult result, YardState state)
        {
            var updated = new List<MarkerView>();
            foreach (var id in result.LoadsSet)
            {
                var view = Get(id);
                var marker = state.Get(id);
                if (view == null || marker == null) continue;
                view.SetLoad(marker.Load);
                updated.Add(view);
            }
            if (updated.Count > 0)
                yield return ctx.Tween(0.6f, p => { foreach (var view in updated) view.SetLoadPulse(1f - p); }, ease: false);

            var leaving = new List<MarkerView>();
            var from = new List<Vector3>();
            foreach (var id in result.Removed)
            {
                var view = Get(id);
                if (view == null) continue;
                view.ReleaseCargo();
                if (view.CarriedBy != null) view.DetachTo(view.transform.position);
                leaving.Add(view);
                from.Add(view.transform.position);
            }
            if (leaving.Count == 0) yield break;

            yield return ctx.Tween(1.4f, p =>
            {
                float shrink = Mathf.Clamp01((p - 0.55f) / 0.45f);
                for (int i = 0; i < leaving.Count; i++)
                {
                    var start = from[i];
                    leaving[i].transform.position = Vector3.Lerp(start, new Vector3(exitX, start.y, start.z), p);
                    leaving[i].SetFade(1f - shrink);
                }
            });
            foreach (var view in leaving) Retire(view.Id);
        }

        /// <summary>Makes the yard equal the state at once: hides, adds, re-lanes and relabels with no motion.</summary>
        public void SyncInstant(YardState state)
        {
            var present = new HashSet<string>();
            foreach (var marker in state.Markers) present.Add(marker.Id);
            foreach (var id in new List<string>(active.Keys))
                if (!present.Contains(id)) Retire(id);

            laneEnd.Clear();
            foreach (var view in active.Values)
                if (view != null) NoteLaneEnd(view.Lane, view.Rest.x);

            foreach (var marker in state.Markers)
            {
                var view = Get(marker.Id);
                if (view == null)
                {
                    Place(Acquire(marker.Id), marker, NextSlot(marker.Lane));
                    continue;
                }
                view.SetLoad(marker.Load);
                view.SetLoadPulse(0f);
                if (view.Lane == marker.Lane) continue;
                if (view.CarriedBy != null) view.DetachTo(view.transform.position);
                view.Lane = marker.Lane;
                view.SetRest(NextSlot(marker.Lane));
            }
        }

        // The view for an id: its placeholder if the scene has a matching one, else one spawned from the prefab.
        MarkerView Acquire(string id)
        {
            bool forklift = !cargo.Contains(id);
            if (placeholders.TryGetValue(id, out var placeholder) && placeholder != null && placeholder.HasForks == forklift)
                return placeholder;
            if (spawned.TryGetValue(id, out var existing) && existing != null)
            {
                if (existing.HasForks == forklift) return existing;
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
            NoteLaneEnd(marker.Lane, rest.x);
        }

        void Retire(string id)
        {
            if (!active.TryGetValue(id, out var view)) return;
            active.Remove(id);
            if (view != null) view.Hide();
        }

        Vector3 NextSlot(string lane)
        {
            float x = laneEnd.TryGetValue(lane, out float end) ? end + slotSpacing : firstSlotX;
            laneEnd[lane] = x;
            return new Vector3(x, GroundY, LaneZ(lane));
        }

        void NoteLaneEnd(string lane, float x)
        {
            if (!laneEnd.TryGetValue(lane, out float end) || x > end) laneEnd[lane] = x;
        }

        string LaneOf(Vector3 position) =>
            Mathf.Abs(position.z - northLane.position.z) <= Mathf.Abs(position.z - southLane.position.z) ? Lanes.North : Lanes.South;

        float LaneZ(string lane) => (lane == Lanes.South ? southLane : northLane).position.z;

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
