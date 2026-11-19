using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// slip (orange): <c>who</c> skids — a sideways lurch and a hard tilt that die away, leaving skid
    /// marks on the lane — and ends where it started. No numbers change.
    /// </summary>
    [CueType("slip"), CueLook("#FF8C1A", "SLIP"), Preserve]
    public class SlipCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            ctx.Fx.Skid(who);
            yield return ctx.Tween(1.4f, p =>
            {
                float decay = 1f - p;
                float lurch = Mathf.Sin(p * Mathf.PI * 2.5f) * decay;
                float wobble = Mathf.Sin(p * Mathf.PI * 7f) * decay;
                who.Body.localRotation = Quaternion.Euler(wobble * 8f, 0f, wobble * 22f);
                who.Body.localPosition = new Vector3(lurch * 0.35f, Mathf.Abs(wobble) * 0.12f, Mathf.Sin(p * Mathf.PI * 3f) * decay * 0.3f);
            }, ease: false);
            who.ClearTransients();
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            if (who != null) who.ClearTransients();
        }

        public string Describe(Cue cue) => $"{cue.who} slips";
    }
}
