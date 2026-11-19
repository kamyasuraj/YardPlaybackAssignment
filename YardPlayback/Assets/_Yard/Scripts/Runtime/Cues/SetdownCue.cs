using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// setdown: <c>who</c> drops <c>what</c> (forks fall, the crate lands) and backs away.
    /// The player then applies patch.set, and anything in patch.gone leaves the yard.
    /// </summary>
    [CueType("setdown"), Preserve]
    public class SetdownCue : ICueHandler
    {
        const float BackOff = 0.9f;

        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            var what = ctx.Find(cue, cue.what, "what");
            if (who == null) yield break;

            if (what != null && what.CarriedBy == who)
            {
                float from = who.ForkHeight;
                yield return ctx.Tween(0.6f, p => who.SetForkHeight(Mathf.Lerp(from, who.ForkLow, p * p)), ease: false);
                yield return ctx.Tween(0.25f, p =>
                {
                    float s = Mathf.Sin(p * Mathf.PI);
                    what.Body.localScale = new Vector3(1f + 0.15f * s, 1f - 0.2f * s, 1f + 0.15f * s);
                }, ease: false);
                what.Body.localScale = Vector3.one;
                what.DetachTo(Ground(who));
            }

            Vector3 start = who.Rest;
            Vector3 end = start + Vector3.left * BackOff;
            yield return ctx.Tween(0.8f, p => who.transform.position = Vector3.Lerp(start, end, p));
            who.SetRest(end);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            var what = ctx.View.Get(cue.what);
            if (who == null) return;

            if (what != null && what.CarriedBy == who)
            {
                who.SetForkHeight(who.ForkLow);
                what.DetachTo(Ground(who));
            }
            who.SetRest(who.Rest + Vector3.left * BackOff);
        }

        public string Describe(Cue cue) => $"{cue.who} drops {cue.what}";

        // The spot under the forks, from `who`'s rest position before this cue.
        static Vector3 Ground(MarkerView who) =>
            new Vector3(who.Rest.x + who.ForkReach, YardView.GroundY, who.Rest.z);
    }
}
