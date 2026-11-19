using System.Collections;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>clock: the minute rolls to the cue's minute (old number fades out, new one fades in with a bump).</summary>
    [CueType("clock"), CueLook("#E8E8EC", "CLOCK"), Preserve]
    public class ClockCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            string from = ctx.Hud.MinuteText;
            string to = cue.minute.ToString();
            yield return ctx.Tween(0.8f, p => ctx.Hud.SetMinuteRoll(from, to, p), ease: false);
        }

        public void Snap(CueContext ctx, Cue cue) => ctx.Hud.SetMinute(cue.minute);

        public string Describe(Cue cue) => $"minute {cue.minute}";
    }
}
