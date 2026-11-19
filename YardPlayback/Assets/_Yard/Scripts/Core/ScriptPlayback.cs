using System;
using System.Collections;

namespace Yard.Core
{
    public enum PlaybackState { Empty, Ready, Playing, Paused, Finished }

    /// <summary>
    /// Plays a script's cues in order, each one finishing before the next starts. The file is the
    /// truth: loads change only through patch.set, and the end is held to <c>closing</c>.
    /// Pure C#: the scene plugs in through <see cref="ICueAnimator"/> and calls <see cref="Tick"/> each frame.
    /// </summary>
    public class ScriptPlayback
    {
        /// <summary>Playback time between cues, so each one reads on its own.</summary>
        public const float BeatSeconds = 0.35f;

        readonly ICueAnimator animator;
        readonly RoutineRunner runner = new RoutineRunner();

        int cursor;          // first cue that hasn't fully finished (animation + patch shown)
        int playedThrough;   // last cue whose Play animation finished
        int patchedThrough;  // last cue whose patch is already in the yard state

        public ScriptPlayback(ICueAnimator animator, PlaybackClock clock)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            Clock = clock ?? new PlaybackClock();
        }

        public PlaybackClock Clock { get; }
        public PlaybackState State { get; private set; } = PlaybackState.Empty;
        public YardScript Script { get; private set; }
        public YardState Yard { get; private set; }

        /// <summary>Index of the cue on screen; -1 before the first one starts.</summary>
        public int CurrentCue { get; private set; } = -1;

        /// <summary>Who is left and each load, as JSON. Set when playback finishes.</summary>
        public string Payload { get; private set; }

        public event Action<int, Cue> CueStarted;
        public event Action<int, Cue> CueFinished;
        public event Action<PlaybackState> StateChanged;
        public event Action<string> Finished;
        public event Action<string> Warning;

        /// <summary>Loads a script. On bad input returns false and keeps the current script.</summary>
        public bool Load(string json, out string error)
        {
            if (!ScriptParser.TryParse(json, out var parsed, out error)) return false;

            runner.Stop();
            Script = parsed;
            Yard = YardState.FromOpening(parsed);
            cursor = 0;
            playedThrough = -1;
            patchedThrough = -1;
            CurrentCue = -1;
            Payload = null;
            Clock.Paused = false;
            animator.Reset(Script, Yard);

            foreach (var cue in Script.cues)
                if (!animator.CanPlay(cue.say)) Warn($"No handler for cue type '{cue.say}'. Its patch still applies.");

            SetState(PlaybackState.Ready);
            return true;
        }

        public void Play()
        {
            if (State == PlaybackState.Paused) { Resume(); return; }
            if (State != PlaybackState.Ready) return;
            Clock.Paused = false;
            SetState(PlaybackState.Playing);
            runner.Start(Run());
        }

        public void Pause()
        {
            if (State != PlaybackState.Playing) return;
            Clock.Paused = true;
            SetState(PlaybackState.Paused);
        }

        public void Resume()
        {
            if (State != PlaybackState.Paused) return;
            Clock.Paused = false;
            SetState(PlaybackState.Playing);
        }

        /// <summary>Changes pace only: the cue on screen carries on from where it is.</summary>
        public bool SetSpeed(float speed)
        {
            if (!(speed > 0f) || float.IsInfinity(speed)) return false;   // also rejects NaN
            Clock.Speed = speed;
            return true;
        }

        /// <summary>Jumps straight to <c>closing</c>. Leaves no motion, and a second press changes nothing.</summary>
        public void Skip()
        {
            if (Script == null) return;

            runner.Stop();
            animator.HaltMotion();
            for (int i = cursor; i < Script.cues.Length; i++)
            {
                if (i > playedThrough) animator.Snap(Script.cues[i]);
                if (i > patchedThrough) ApplyPatch(i);
            }

            cursor = Script.cues.Length;
            playedThrough = patchedThrough = Script.cues.Length - 1;
            if (Script.cues.Length > 0) CurrentCue = Script.cues.Length - 1;

            Yard.EnforceClosing(Script.closing);
            animator.SyncInstant(Yard);
            Finish();
        }

        /// <summary>Call once per frame with the frame's real time.</summary>
        public void Tick(float frameSeconds)
        {
            Clock.BeginFrame(frameSeconds);
            if (State == PlaybackState.Playing) runner.Tick();
        }

        IEnumerator Run()
        {
            for (int i = cursor; i < Script.cues.Length; i++)
            {
                var cue = Script.cues[i];
                CurrentCue = i;
                CueStarted?.Invoke(i, cue);

                if (animator.CanPlay(cue.say)) yield return animator.Play(cue);
                playedThrough = i;

                var result = ApplyPatch(i);
                yield return animator.AnimatePatch(cue, result, Yard);
                cursor = i + 1;
                CueFinished?.Invoke(i, cue);

                if (i < Script.cues.Length - 1) yield return Wait(BeatSeconds);
            }

            if (!Yard.Matches(Script.closing))
            {
                // The file wins: the end is held to `closing`, shown with motion rather than a pop.
                Warn("The end state differs from 'closing'; moving to 'closing' (the file wins).");
                yield return Wait(BeatSeconds);
                Yard.EnforceClosing(Script.closing);
                yield return animator.AnimateClosing(Yard);
                animator.SyncInstant(Yard);
            }
            Finish();
        }

        PatchResult ApplyPatch(int index)
        {
            var cue = Script.cues[index];
            var result = Yard.ApplyPatch(cue.patch);
            patchedThrough = index;
            foreach (var id in result.UnknownIds)
                Warn($"Cue {index + 1} ('{cue.say}') patches '{id}', which is not in the yard. Ignored.");
            return result;
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Clock.Delta) yield return null;
        }

        void Finish()
        {
            bool first = State != PlaybackState.Finished;
            Clock.Paused = false;
            Payload = Yard.ToPayloadJson(Script.ticket);
            SetState(PlaybackState.Finished);
            if (first) Finished?.Invoke(Payload);
        }

        void SetState(PlaybackState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        void Warn(string message) => Warning?.Invoke(message);
    }
}
