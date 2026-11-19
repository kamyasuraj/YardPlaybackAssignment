using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>slip: <c>who</c> stumbles (a lurch and a dying wobble) and recovers in place. No numbers change.</summary>
    [CueType("slip"), Preserve]
    public class SlipCue : ICueHandler
    {
        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            if (who == null) yield break;

            yield return ctx.Tween(1.2f, p =>
            {
                float decay = 1f - p;
                float wobble = Mathf.Sin(p * Mathf.PI * 6f) * decay;
                who.Body.localRotation = Quaternion.Euler(wobble * 6f, 0f, wobble * 14f);
                who.Body.localPosition = new Vector3(Mathf.Sin(p * Mathf.PI * 3f) * decay * 0.25f, Mathf.Abs(wobble) * 0.08f, 0f);
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
