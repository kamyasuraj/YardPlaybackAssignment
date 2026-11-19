using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>carry: <c>who</c> drives to <c>what</c>, lifts it onto its forks and carries it forward.</summary>
    [CueType("carry"), Preserve]
    public class CarryCue : ICueHandler
    {
        const float CarryDistance = 2f;

        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            var what = ctx.Find(cue, cue.what, "what");
            if (who == null || what == null || !who.HasForks) yield break;

            Vector3 start = who.Rest;
            Vector3 pickup = PickupPoint(who, what);
            yield return ctx.Tween(1.3f, p => who.transform.position = Vector3.Lerp(start, pickup, p));

            what.AttachTo(who);
            yield return ctx.Tween(0.7f, p => who.SetForkHeight(Mathf.Lerp(who.ForkLow, who.ForkHigh, p)));

            Vector3 end = pickup + Vector3.right * CarryDistance;
            yield return ctx.Tween(1.1f, p => who.transform.position = Vector3.Lerp(pickup, end, p));
            who.SetRest(end);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            var what = ctx.View.Get(cue.what);
            if (who == null || what == null || !who.HasForks) return;

            Vector3 end = PickupPoint(who, what) + Vector3.right * CarryDistance;
            who.SetForkHeight(who.ForkLow);
            what.AttachTo(who);
            who.SetForkHeight(who.ForkHigh);
            who.SetRest(end);
        }

        public string Describe(Cue cue) => $"{cue.who} takes {cue.what}";

        // Where `who` stops so its forks sit under `what`. Uses rest positions, which only change
        // when a cue completes, so Play and Snap always agree.
        static Vector3 PickupPoint(MarkerView who, MarkerView what) =>
            new Vector3(what.Rest.x - who.ForkReach, who.Rest.y, what.Rest.z);
    }
}
