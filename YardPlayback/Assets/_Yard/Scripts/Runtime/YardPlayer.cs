using UnityEngine;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// The one object an outside page drives, by its GameObject name <c>YardPlayer</c>.
    /// Every control is a method with no argument or one string/number, so SendMessage can call it.
    /// </summary>
    [DisallowMultipleComponent]
    public class YardPlayer : MonoBehaviour
    {
        [Tooltip("Loaded when the scene starts (the brief's shift-04 script). Load replaces it.")]
        [SerializeField] TextAsset startScript;

        ScriptPlayback playback;
        YardAnimator animator;
        YardHud hud;

        /// <summary>Playback state and yard, for tests and the page bridge.</summary>
        public ScriptPlayback Playback => playback;

        void Awake()
        {
            var view = GetComponent<YardView>();
            hud = GetComponent<YardHud>();
            if (view == null || hud == null || !view.IsSetUp || !hud.IsSetUp)
            {
                Debug.LogError("[Yard] This scene's yard isn't set up. Run the menu Yard > Build Scene and Prefabs.");
                enabled = false;
                return;
            }
            view.Prepare();

            var clock = new PlaybackClock();
            animator = new YardAnimator(CueRegistry.Discover(), new CueContext(view, hud, clock));
            playback = new ScriptPlayback(animator, clock);
            playback.StateChanged += state => hud.SetState(state, clock.Speed);
            playback.CueStarted += OnCueStarted;
            playback.CueFinished += (index, cue) => Debug.Log($"[Yard] cue {index + 1}/{playback.Script.cues.Length} {cue.say}: done");
            playback.Warning += message => Debug.LogWarning("[Yard] " + message);
            playback.Finished += OnFinished;
            hud.SetState(playback.State, clock.Speed);
        }

        void Start()
        {
            if (startScript != null) Load(startScript.text);
        }

        void Update() => playback.Tick(Time.deltaTime);

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

        /// <summary>1 = normal, 2 = double. The cue on screen carries on at the new pace.</summary>
        public void SetSpeed(float speed)
        {
            if (playback == null) return;
            if (!playback.SetSpeed(speed))
            {
                Debug.LogWarning($"[Yard] speed {speed} ignored; use a positive number");
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
            Debug.Log($"[Yard] cue {index + 1}/{count} {cue.say}: start ({description})");
        }

        void OnFinished(string payload)
        {
            var script = playback.Script;
            if (script.cues.Length > 0) hud.SetCue(script.cues.Length - 1, script.cues.Length, animator.Describe(script.cues[script.cues.Length - 1]));
            Debug.Log($"[Yard] finished; matches closing: {playback.Yard.Matches(script.closing)}; payload {payload}");
        }
    }
}
