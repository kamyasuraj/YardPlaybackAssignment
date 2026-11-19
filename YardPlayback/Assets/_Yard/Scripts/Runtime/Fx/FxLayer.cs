using System;
using System.Collections.Generic;
using UnityEngine;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// Directs the eye. A ring in the cue's colour grows under the marker(s) in the current cue, the
    /// other markers dim, and the cue's word shows above; short effects (halos, dust, skid marks) play
    /// on top. Everything runs on playback time — it freezes on Pause and keeps pace at 2× — and
    /// <see cref="Clear"/> removes it all at once for Skip.
    /// </summary>
    public class FxLayer : MonoBehaviour
    {
        const float FocusSeconds = 0.35f;
        const float RingDiameter = 2.6f;
        const float RingAlpha = 0.45f;
        const float GroundHeight = 0.03f;
        const float DimAmount = 0.6f;
        const float ChipCanvasHeight = 170f;   // north lane: the word sits just above the load number (label-canvas units)
        // South lane (the near one): the word sits just in front of the marker, inside its own lane,
        // so it never reaches up over the north lane.
        static readonly Vector3 ChipInFront = new Vector3(0f, 0.45f, -1.45f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color SkidColor = new Color(0.05f, 0.05f, 0.06f);

        [SerializeField] Renderer discPrefab;
        [SerializeField] Renderer streakPrefab;
        [SerializeField] ActionChip chipPrefab;
        [SerializeField] Transform fxRoot;

        sealed class FocusEntry
        {
            public Renderer Ring;
            public Color Color;
            public float K;
            public float Goal;
        }

        sealed class Effect
        {
            public Renderer Renderer;
            public float Time;
            public float Seconds;
            public Action<Renderer, float> Apply;
        }

        readonly Dictionary<MarkerView, FocusEntry> focus = new Dictionary<MarkerView, FocusEntry>();
        readonly List<MarkerView> faded = new List<MarkerView>();
        readonly List<Effect> effects = new List<Effect>();
        MaterialPropertyBlock block;
        YardView view;
        ActionChip chip;
        MarkerView chipTarget;
        Vector3 chipPosition;
        bool chipPlaced;
        float chipK;
        float chipGoal;

        public bool IsSetUp => discPrefab != null && streakPrefab != null && chipPrefab != null && fxRoot != null;

        public void Prepare(YardView yard)
        {
            view = yard;
            block = new MaterialPropertyBlock();
            chip = Instantiate(chipPrefab, fxRoot);
            chip.name = "Action chip";
            chip.SetVisible(0f);
        }

        /// <summary>Rings <paramref name="targets"/> in the cue's colour, dims the rest, shows the cue's word over the first.</summary>
        public void Focus(IList<MarkerView> targets, CueLook look)
        {
            foreach (var entry in focus.Values) entry.Goal = 0f;
            chipTarget = null;
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (!focus.TryGetValue(target, out var entry))
                {
                    entry = new FocusEntry { Ring = Spawn(discPrefab) };
                    focus[target] = entry;
                    DrawRing(entry, target, 0f);
                }
                entry.Color = look.Color;
                entry.Goal = 1f;
                if (chipTarget == null) chipTarget = target;
            }
            chip.Set(look.Word, look.Color);
            chipGoal = chipTarget != null ? 1f : 0f;
        }

        /// <summary>Moves the word to <paramref name="target"/>; the rings stay where they are.</summary>
        public void Emphasize(MarkerView target)
        {
            if (target != null) chipTarget = target;
        }

        public void Unfocus()
        {
            foreach (var entry in focus.Values) entry.Goal = 0f;
            chipGoal = 0f;
        }

        /// <summary>A flat ring spreading out from <paramref name="at"/> on the ground, fading as it goes.</summary>
        public void Burst(Vector3 at, Color color, float fromDiameter, float toDiameter, float seconds, float alpha)
        {
            var disc = Spawn(discPrefab);
            var origin = new Vector3(at.x, GroundHeight + 0.006f, at.z);
            float height = disc.transform.localScale.y;
            Add(disc, seconds, (r, p) =>
            {
                float d = Mathf.Lerp(fromDiameter, toDiameter, Easing.Out(p));
                r.transform.position = origin;
                r.transform.localScale = new Vector3(d, height, d);
                Tint(r, color, alpha * Mathf.Clamp01(p / 0.1f) * (1f - p));
            });
        }

        /// <summary>Two dark streaks on the lane under <paramref name="marker"/>, fading: skid marks.</summary>
        public void Skid(MarkerView marker)
        {
            var at = marker.transform.position;
            foreach (float side in new[] { -0.32f, 0.32f })
            {
                var streak = Spawn(streakPrefab);
                float height = streak.transform.localScale.y;
                streak.transform.position = new Vector3(at.x - 0.15f, GroundHeight - 0.004f, at.z + side);
                streak.transform.localScale = new Vector3(1.9f, height, 0.12f);
                Add(streak, 2.4f, (r, p) => Tint(r, SkidColor, 0.8f * Mathf.Clamp01(p / 0.08f) * (1f - Easing.In(p))));
            }
        }

        /// <summary>Advances everything by <paramref name="dt"/> seconds of playback time (0 while paused).</summary>
        public void Tick(float dt)
        {
            if (view == null) return;

            float strongest = 0f;
            faded.Clear();
            foreach (var pair in focus)
            {
                var entry = pair.Value;
                entry.K = Mathf.MoveTowards(entry.K, entry.Goal, dt / FocusSeconds);
                float s = Easing.InOut(entry.K);
                DrawRing(entry, pair.Key, s);
                strongest = Mathf.Max(strongest, s);
                if (entry.K <= 0f && entry.Goal <= 0f) faded.Add(pair.Key);
            }
            foreach (var key in faded)
            {
                Destroy(focus[key].Ring.gameObject);
                focus.Remove(key);
            }

            foreach (var marker in view.ActiveMarkers)
            {
                float own = focus.TryGetValue(marker, out var entry) ? Easing.InOut(entry.K) : 0f;
                marker.SetDim(DimAmount * strongest * (1f - own));
            }

            chipK = Mathf.MoveTowards(chipK, chipGoal, dt / FocusSeconds);
            if (chipTarget != null)
            {
                var at = chipTarget.transform.position;
                var goal = view.LaneOf(at) == Lanes.South ? at + ChipInFront : chipTarget.LabelPoint(ChipCanvasHeight);
                chipPosition = chipPlaced ? Vector3.Lerp(chipPosition, goal, 1f - Mathf.Exp(-10f * dt)) : goal;
                chipPlaced = true;
                chip.transform.position = chipPosition;
            }
            chip.SetVisible(Easing.InOut(chipK));
            if (chipK <= 0f) chipPlaced = false;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                effect.Time += dt;
                float p = Mathf.Clamp01(effect.Time / effect.Seconds);
                effect.Apply(effect.Renderer, p);
                if (p < 1f) continue;
                Destroy(effect.Renderer.gameObject);
                effects.RemoveAt(i);
            }
        }

        /// <summary>Removes every ring, dim, word and effect at once (Skip cuts; nothing is left moving).</summary>
        public void Clear()
        {
            foreach (var entry in focus.Values)
                if (entry.Ring != null) Destroy(entry.Ring.gameObject);
            focus.Clear();
            foreach (var effect in effects)
                if (effect.Renderer != null) Destroy(effect.Renderer.gameObject);
            effects.Clear();
            chipK = chipGoal = 0f;
            chipTarget = null;
            chipPlaced = false;
            if (chip != null) chip.SetVisible(0f);
            if (view != null)
                foreach (var marker in view.ActiveMarkers) marker.SetDim(0f);
        }

        void DrawRing(FocusEntry entry, MarkerView target, float s)
        {
            var at = target != null ? target.transform.position : entry.Ring.transform.position;
            var ring = entry.Ring.transform;
            ring.position = new Vector3(at.x, GroundHeight, at.z);
            float d = RingDiameter * s;
            ring.localScale = new Vector3(d, ring.localScale.y, d);
            Tint(entry.Ring, entry.Color, RingAlpha * s);
        }

        Renderer Spawn(Renderer prefab) => Instantiate(prefab, fxRoot);

        void Tint(Renderer renderer, Color color, float alpha)
        {
            color.a = alpha;
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        // Effects are drawn at p = 0 straight away, so nothing shows at its prefab size for a frame.
        void Add(Renderer renderer, float seconds, Action<Renderer, float> apply)
        {
            apply(renderer, 0f);
            effects.Add(new Effect { Renderer = renderer, Seconds = Mathf.Max(0.01f, seconds), Apply = apply });
        }
    }
}
