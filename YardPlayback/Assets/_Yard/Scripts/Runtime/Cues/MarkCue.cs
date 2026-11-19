using System.Collections;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>mark: a flag named <c>flag</c> is raised on <c>who</c> and stays pinned.</summary>
    [CueType("mark"), Preserve]
    public class MarkCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            who.ShowFlag(cue.flag);
            yield return ctx.Tween(0.9f, who.SetFlagRaise);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            if (who == null) return;
            who.ShowFlag(cue.flag);
            who.SetFlagRaise(1f);
        }

        public string Describe(Cue cue) => $"{cue.who} flagged {cue.flag}";
    }
}
