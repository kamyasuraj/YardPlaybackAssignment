using System;
using System.Collections;
using UnityEngine;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// What a cue handler works with: the yard, the HUD, the direction layer (rings, effects),
    /// the camera and the playback clock. Tween and Wait run on playback time, so Pause freezes a
    /// cue mid-motion and 1×/2× changes its pace without skipping or replaying anything.
    /// </summary>
    public class CueContext
    {
        public CueContext(YardView view, YardHud hud, PlaybackClock clock, FxLayer fx, CameraDirector director)
        {
            View = view;
            Hud = hud;
            Clock = clock;
            Fx = fx;
            Director = director;
        }

        public YardView View { get; }
        public YardHud Hud { get; }
        public PlaybackClock Clock { get; }
        public FxLayer Fx { get; }

        /// <summary>The camera director, or null if the scene has no camera to direct.</summary>
        public CameraDirector Director { get; }

        /// <summary>Calls apply(p) every frame, p going 0 → 1 over <paramref name="seconds"/> of playback time.</summary>
        public IEnumerator Tween(float seconds, Action<float> apply, bool ease = true)
        {
            apply(0f);
            for (float t = 0f; t < seconds;)
            {
                yield return null;
                t += Clock.Delta;
                float p = Mathf.Clamp01(t / seconds);
                apply(ease ? Easing.InOut(p) : p);
            }
        }

        public IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Clock.Delta) yield return null;
        }

        /// <summary>Smoothstep: eases in and out.</summary>
        public static float Ease(float p) => Easing.InOut(p);

        /// <summary>The marker with this id, or null with a warning naming the cue and role.</summary>
        public MarkerView Find(Cue cue, string id, string role)
        {
            var marker = View.Get(id);
            if (marker == null) Debug.LogWarning($"[Yard] '{cue.say}': {role} '{id}' is not in the yard; cue shown without it.");
            return marker;
        }
    }
}
