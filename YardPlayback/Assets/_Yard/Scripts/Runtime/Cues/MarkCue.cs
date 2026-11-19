using System.Collections;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>mark (red): a flag named <c>flag</c> grows out of <c>who</c>, its cloth runs up the pole with a small bounce, and it stays pinned.</summary>
    [CueType("mark"), CueLook("#F2383A", "MARK"), Preserve]
    public class MarkCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            who.ShowFlag(cue.flag);
            who.SetFlagRaise(0f);
            yield return ctx.Tween(0.45f, who.SetFlagGrow);
            yield return ctx.Tween(0.75f, p => who.SetFlagRaise(Easing.OutBack(p)), ease: false);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            if (who == null) return;
            who.ShowFlag(cue.flag);
            who.SetFlagGrow(1f);
            who.SetFlagRaise(1f);
        }

        public string Describe(Cue cue) => $"{cue.who} flagged {cue.flag}";
    }
}
