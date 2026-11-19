using System.Collections;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>shift_over: the yard freezes. The screen dims and "SHIFT OVER" shows; nothing moves after this.</summary>
    [CueType("shift_over"), CueLook("#E8E8EC", "SHIFT OVER"), Preserve]
    public class ShiftOverCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            yield return ctx.Tween(0.9f, ctx.Hud.SetFreeze);
        }

        public void Snap(CueContext ctx, Cue cue) => ctx.Hud.SetFreeze(1f);

        public string Describe(Cue cue) => "the yard freezes";
    }
}
