using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// tick (cyan): three cyan halos ripple out from <c>who</c> while it flashes, with <c>flag</c> and
    /// <c>amount</c> as the flash's label. The amount is only shown; the new load comes from patch.set,
    /// which the player applies after.
    /// </summary>
    [CueType("tick"), CueLook("#26D9F2", "TICK"), Preserve]
    public class TickCue : ICueHandler
    {
        const int Halos = 3;
        static readonly Color Cyan = new Color(0.15f, 0.85f, 0.95f);
        static readonly Color FlashCyan = new Color(0.75f, 1f, 1f);

        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            who.SetFlashLabel(Label(cue));
            int fired = 0;
            yield return ctx.Tween(1.5f, p =>
            {
                while (fired < Halos && p >= (float)fired / Halos)
                {
                    ctx.Fx.Burst(who.transform.position, Cyan, 1.2f, 4.2f, 0.9f, 0.7f);
                    fired++;
                }
                who.SetFlash(Mathf.Abs(Mathf.Sin(p * Mathf.PI * Halos)), FlashCyan);
                who.SetFlashLabelAlpha(Easing.Envelope(p, 0.15f));
            }, ease: false);
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
