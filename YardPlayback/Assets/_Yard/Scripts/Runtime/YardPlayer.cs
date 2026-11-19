using System.Collections.Generic;
using UnityEngine;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// The player: plays a script in the yard. The in-scene controls call it directly; an outside page
    /// reaches it through <see cref="YardPage"/> on the GameObject named <c>YardPlayer</c>, which exposes
    /// only the page's six controls.
    /// </summary>
    [DisallowMultipleComponent]
    public class YardPlayer : MonoBehaviour
    {
        [Tooltip("Loaded when the scene starts (the brief's shift-04 script), unless a Load arrived first. Load replaces it.")]
        [SerializeField] TextAsset startScript;

        ScriptPlayback playback;
        YardAnimator animator;
        YardView view;
        YardHud hud;
        FxLayer fx;
        CameraDirector director;
        bool started;

        /// <summary>Playback state and yard, for tests and the page bridge.</summary>
        public ScriptPlayback Playback => playback;

        void Awake()
        {
            if (playback != null) return;   // set up once, even if Awake is called again by name
#if UNITY_WEBGL && !UNITY_EDITOR
            // In the browser the page's script box must take typing; the yard needs no keyboard.
            WebGLInput.captureAllKeyboardInput = false;
#endif
            view = GetComponent<YardView>();
            hud = GetComponent<YardHud>();
            fx = GetComponent<FxLayer>();
            director = GetComponent<CameraDirector>();
            if (view == null || hud == null || fx == null || !view.IsSetUp || !hud.IsSetUp || !fx.IsSetUp)
            {
                Debug.LogError("[Yard] This scene's yard isn't set up. Run the menu Yard > Build Scene and Prefabs.");
                enabled = false;
                return;
            }
            view.Prepare();
            fx.Prepare(view);
            if (director != null && director.IsSetUp) director.Prepare();
            else director = null;

            var clock = new PlaybackClock();
            animator = new YardAnimator(CueRegistry.Discover(), new CueContext(view, hud, clock, fx, director));
            playback = new ScriptPlayback(animator, clock);
            playback.StateChanged += state => hud.SetState(state, clock.Speed);
            playback.CueStarted += OnCueStarted;
            playback.CueFinished += OnCueFinished;
            playback.Warning += message => Debug.LogWarning("[Yard] " + message);
            playback.Finished += OnFinished;
            hud.SetState(playback.State, clock.Speed);
        }

        void Start()
        {
            if (started) return;
            started = true;
            // A page may Load before the first frame; its script wins over the built-in one.
            if (startScript != null && playback != null && playback.Script == null) Load(startScript.text);
        }

        void Update()
        {
            if (playback == null) return;
            playback.Tick(Time.deltaTime);
            // The direction layer, HUD fades and camera run on playback time: frozen while paused, quicker at 2×.
            float dt = playback.Clock.Delta;
            fx.Tick(dt);
            hud.Tick(dt);
            if (director != null) director.Tick(dt);
        }

        // ---- Controls: the in-scene buttons and the outside page call these ----

        /// <summary>Loads a script given as one JSON string. Bad input is rejected and the current script stays.</summary>
        public void Load(string json)
        {
            if (playback == null) return;
            if (playback.Load(json, out var error))
            {
                hud.ShowError(null);
                Debug.Log($"[Yard] loaded '{playback.Script.ticket}': {playback.Script.opening.Length} markers, {playback.Script.cues.Length} cues");
            }
            else
            {
                hud.ShowError("Load rejected: " + error);
                Debug.LogError("[Yard] load rejected: " + error);
            }
        }

        /// <summary>Starts playback from the opening; if paused, resumes.</summary>
        public void Play() => playback?.Play();

        public void Pause() => playback?.Pause();

        public void Resume() => playback?.Resume();

        /// <summary>1 = normal, 2 = double (the brief's 1× and 2×); anything else is ignored. The cue on screen carries on at the new pace.</summary>
        public void SetSpeed(float speed)
        {
            if (playback == null) return;
            if ((speed != 1f && speed != 2f) || !playback.SetSpeed(speed))
            {
                Debug.LogWarning($"[Yard] speed {speed} ignored; use 1 or 2");
                return;
            }
            hud.SetState(playback.State, playback.Clock.Speed);
            Debug.Log($"[Yard] speed {speed}x");
        }

        /// <summary>Snaps to the closing state at once. Pressing it again stays there.</summary>
        public void Skip()
        {
            if (playback == null) return;
            if (playback.State == PlaybackState.Finished) Debug.Log("[Yard] skip: already on closing");
            playback.Skip();
        }

        void OnCueStarted(int index, Cue cue)
        {
            int count = playback.Script.cues.Length;
            string description = animator.Describe(cue);
            hud.SetCue(index, count, description);
            fx.Focus(Targets(cue), animator.LookOf(cue));
            Debug.Log($"[Yard] cue {index + 1}/{count} {cue.say}: start ({description})");
        }

        void OnCueFinished(int index, Cue cue)
        {
            fx.Unfocus();
            if (director != null) director.Release();
            Debug.Log($"[Yard] cue {index + 1}/{playback.Script.cues.Length} {cue.say}: done");
        }

        void OnFinished(string payload)
        {
            var script = playback.Script;
            int count = script.cues.Length;
            if (count > 0) hud.SetCueInstant(count - 1, count, animator.Describe(script.cues[count - 1]));
            fx.Unfocus();
            Debug.Log($"[Yard] finished; matches closing: {playback.Yard.Matches(script.closing)}; payload {payload}");
            PageLink.NotifyFinished(payload);
        }

        // The markers a cue is about: its `who` and, if different, its `what`.
        List<MarkerView> Targets(Cue cue)
        {
            var targets = new List<MarkerView>(2);
            var who = view.Get(cue.who);
            if (who != null) targets.Add(who);
            var what = view.Get(cue.what);
            if (what != null && what != who) targets.Add(what);
            return targets;
        }
    }
}
