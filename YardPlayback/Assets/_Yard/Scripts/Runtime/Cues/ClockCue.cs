using System.Collections;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>clock: the minute label shows the cue's minute.</summary>
    [CueType("clock"), Preserve]
    public class ClockCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            ctx.Hud.SetMinute(cue.minute);
            yield return ctx.Tween(0.7f, p => ctx.Hud.PulseMinute(1f - p), ease: false);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            ctx.Hud.SetMinute(cue.minute);
            ctx.Hud.PulseMinute(0f);
        }

        public string Describe(Cue cue) => $"minute {cue.minute}";
    }
}
