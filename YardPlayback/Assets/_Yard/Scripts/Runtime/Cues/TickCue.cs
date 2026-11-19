using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// tick: <c>who</c> flashes three times with <c>flag</c> and <c>amount</c> as the flash's label.
    /// The amount is only shown; the new load comes from patch.set, which the player applies after.
    /// </summary>
    [CueType("tick"), Preserve]
    public class TickCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            who.SetFlashLabel(Label(cue));
            yield return ctx.Tween(1.3f, p => who.SetFlash(Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3f))), ease: false);
            who.SetFlash(0f);
            who.SetFlashLabel(null);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            if (who == null) return;
            who.SetFlash(0f);
            who.SetFlashLabel(null);
        }

        public string Describe(Cue cue) => $"{cue.who} {Label(cue)}";

        static string Label(Cue cue) =>
            string.IsNullOrEmpty(cue.flag) ? cue.amount.ToString() : $"{cue.flag} {cue.amount}";
    }
}
