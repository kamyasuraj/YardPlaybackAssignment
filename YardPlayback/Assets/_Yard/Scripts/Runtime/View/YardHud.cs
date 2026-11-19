using UnityEngine;
using UnityEngine.UI;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// The screen layer, laid out in the scene under Screen: minute, ticket, current cue, playback state,
    /// the five controls (wired to YardPlayer in each button's On Click) and the shift-over freeze.
    /// </summary>
    public class YardHud : MonoBehaviour
    {
        public static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.9f);
        public static readonly Color ButtonColor = new Color(0.2f, 0.21f, 0.26f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.78f, 0.15f, 1f);
        public static readonly Color InkColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        public static readonly Color MutedColor = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f, 1f);
        static readonly Color FreezeColor = new Color(0.02f, 0.03f, 0.05f, 0.45f);
        const float CueFadeSeconds = 0.15f;

        [SerializeField] Text minute;
        [SerializeField] Text ticket;
        [SerializeField] Text cueLine;
        [SerializeField] Text status;
        [SerializeField] Text error;
        [SerializeField] Text freezeTitle;
        [SerializeField] Image freeze;
        [SerializeField] Button play;
        [SerializeField] Button pause;
        [SerializeField] Button speed1;
        [SerializeField] Button speed2;
        [SerializeField] Button skip;

        string cueTarget;
        bool cueSwapping;
        float cueAlpha = 1f;

        public bool IsSetUp => minute != null && ticket != null && cueLine != null && status != null
            && error != null && freezeTitle != null && freeze != null
            && play != null && pause != null && speed1 != null && speed2 != null && skip != null;

        public string MinuteText => minute.text;

        // Script text (the ticket, ids in the cue line, a rejected Load's message) shows exactly as written.
        void Awake()
        {
            foreach (var text in new[] { minute, ticket, cueLine, status, error, freezeTitle })
                if (text != null) text.supportRichText = false;
        }

        public void ResetFor(YardScript script)
        {
            minute.text = "–";
            ResetMinuteLook();
            ticket.text = "TICKET  " + script.ticket;
            SetCueInstant($"cue 0/{script.cues.Length}  ·  ready");
            SetFreeze(0f);
            ShowError(null);
        }

        /// <summary>Advances the cue-line crossfade by <paramref name="dt"/> seconds of playback time.</summary>
        public void Tick(float dt)
        {
            if (cueSwapping)
            {
                cueAlpha = Mathf.MoveTowards(cueAlpha, 0f, dt / CueFadeSeconds);
                if (cueAlpha <= 0f)
                {
                    cueLine.text = cueTarget;
                    cueSwapping = false;
                }
            }
            else if (cueAlpha < 1f)
            {
                cueAlpha = Mathf.MoveTowards(cueAlpha, 1f, dt / CueFadeSeconds);
            }
            ApplyCueAlpha();
        }

        /// <summary>Ends any fade at once (Skip cuts).</summary>
        public void Halt()
        {
            if (cueSwapping) cueLine.text = cueTarget;
            cueSwapping = false;
            cueAlpha = 1f;
            ApplyCueAlpha();
            ResetMinuteLook();
        }

        public void SetState(PlaybackState state, float speed)
        {
            status.text = $"{StateName(state)}  ·  {speed:0.##}×";
            bool loaded = state != PlaybackState.Empty;
            Style(play, state == PlaybackState.Ready || state == PlaybackState.Paused, false);
            Style(pause, state == PlaybackState.Playing, false);
            Style(speed1, loaded, Mathf.Approximately(speed, 1f));
            Style(speed2, loaded, Mathf.Approximately(speed, 2f));
            Style(skip, loaded, false);
        }

        /// <summary>Crossfades the cue line to the new cue: the old text fades out, the new one fades in.</summary>
        public void SetCue(int index, int count, string description)
        {
            string text = $"cue {index + 1}/{count}  ·  {description}";
            if (text == cueLine.text && !cueSwapping) return;
            cueTarget = text;
            cueSwapping = true;
        }

        /// <summary>Sets the cue line at once (on Load and when Skip lands on the end).</summary>
        public void SetCueInstant(string text)
        {
            cueTarget = text;
            cueLine.text = text;
            cueSwapping = false;
            cueAlpha = 1f;
            ApplyCueAlpha();
        }

        public void SetCueInstant(int index, int count, string description) =>
            SetCueInstant($"cue {index + 1}/{count}  ·  {description}");

        public void SetMinute(int value)
        {
            minute.text = value.ToString();
            ResetMinuteLook();
        }

        /// <summary>Rolls the minute from <paramref name="from"/> to <paramref name="to"/>: fade out, swap unseen, fade in with a bump.</summary>
        public void SetMinuteRoll(string from, string to, float p)
        {
            minute.text = p < 0.5f ? from : to;
            float bump = Easing.Pulse(p);
            var color = Color.Lerp(Color.white, AccentColor, bump);
            color.a = Easing.InOut(Mathf.Abs(1f - 2f * p));
            minute.color = color;
            minute.rectTransform.localScale = Vector3.one * (1f + 0.25f * bump);
        }

        void ResetMinuteLook()
        {
            minute.color = Color.white;
            minute.rectTransform.localScale = Vector3.one;
        }

        void ApplyCueAlpha()
        {
            var color = cueLine.color;
            color.a = Easing.InOut(cueAlpha);
            cueLine.color = color;
        }

        /// <summary>0 = live yard, 1 = frozen: dimmed, with "SHIFT OVER" showing.</summary>
        public void SetFreeze(float k)
        {
            freeze.color = new Color(FreezeColor.r, FreezeColor.g, FreezeColor.b, FreezeColor.a * k);
            freezeTitle.color = new Color(1f, 1f, 1f, k);
        }

        public void ShowError(string message) => error.text = message ?? "";

        public static string StateName(PlaybackState state)
        {
            switch (state)
            {
                case PlaybackState.Ready: return "READY";
                case PlaybackState.Playing: return "PLAYING";
                case PlaybackState.Paused: return "PAUSED";
                case PlaybackState.Finished: return "FINISHED";
                default: return "NO SCRIPT";
            }
        }

        /// <summary>A control that does nothing right now is faded, so it's clear which ones respond.</summary>
        public static void Style(Button button, bool enabled, bool selected)
        {
            button.interactable = enabled;
            var fill = selected ? AccentColor : ButtonColor;
            fill.a = enabled ? 1f : 0.45f;
            ((Image)button.targetGraphic).color = fill;
            var ink = selected ? InkColor : Color.white;
            ink.a = enabled ? 1f : 0.3f;
            button.GetComponentInChildren<Text>().color = ink;
        }
    }
}
