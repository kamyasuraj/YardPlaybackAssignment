using UnityEngine;
using UnityEngine.UI;
using Yard.Core;

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

        public bool IsSetUp => minute != null && ticket != null && cueLine != null && status != null
            && error != null && freezeTitle != null && freeze != null
            && play != null && pause != null && speed1 != null && speed2 != null && skip != null;

        public void ResetFor(YardScript script)
        {
            minute.text = "–";
            ticket.text = "TICKET  " + script.ticket;
            cueLine.text = $"cue 0/{script.cues.Length}  ·  ready";
            PulseMinute(0f);
            SetFreeze(0f);
            ShowError(null);
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

        public void SetCue(int index, int count, string description) =>
            cueLine.text = $"cue {index + 1}/{count}  ·  {description}";

        public void SetMinute(int value) => minute.text = value.ToString();

        public void PulseMinute(float k)
        {
            minute.rectTransform.localScale = Vector3.one * (1f + 0.3f * k);
            minute.color = Color.Lerp(Color.white, AccentColor, k);
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
