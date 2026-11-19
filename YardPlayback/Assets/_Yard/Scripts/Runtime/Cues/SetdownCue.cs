using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// setdown (magenta), the moment of the clip: the camera eases in, the forks lift a touch, then
    /// drop fast; the crate lands in a puff of dust and the eye moves to it; the forklift backs away.
    /// The player then applies patch.set, and anything in patch.gone leaves through the EXIT gate.
    /// A crate that stays and belongs to the other lane is set down there: the forklift drives across,
    /// drops it, and returns to its own lane.
    /// </summary>
    [CueType("setdown"), CueLook("#F24DD9", "SET DOWN"), Preserve]
    public class SetdownCue : ICueHandler
    {
        const float BackOff = 0.9f;
        const float Anticipation = 0.12f;
        static readonly Color Dust = new Color(0.78f, 0.7f, 0.58f);

        public IEnumerator Play(CueContext ctx, Cue cue)
        {
            var who = ctx.Find(cue, cue.who, "who");
            var what = ctx.Find(cue, cue.what, "what");
            if (who == null) yield break;

            if (ctx.Director != null) ctx.Director.PushIn((what != null ? what : who).transform);

            Vector3 home = who.Rest;
            Vector3 dropAt = DropPoint(ctx, cue, who, what);
            if (dropAt != home)
                yield return ctx.Tween(0.8f, p => who.transform.position = Vector3.Lerp(home, dropAt, p));

            if (what != null && what.CarriedBy == who)
            {
                float from = who.ForkHeight;
                yield return ctx.Tween(0.3f, p => who.SetForkHeight(from + Anticipation * p));
                float top = who.ForkHeight;
                yield return ctx.Tween(0.4f, p =>
                {
                    who.SetForkHeight(Mathf.Lerp(top, who.ForkLow, Easing.In(p)));
                    what.SetLabelShift(1f - p);
                }, ease: false);

                what.DetachTo(Ground(who, dropAt));
                ctx.Fx.Burst(what.transform.position, Dust, 1f, 3.2f, 0.8f, 0.75f);
                ctx.Fx.Emphasize(what);
                yield return ctx.Tween(0.3f, p =>
                {
                    float s = Easing.Pulse(p);
                    what.Body.localScale = new Vector3(1f + 0.15f * s, 1f - 0.2f * s, 1f + 0.15f * s);
                }, ease: false);
                what.Body.localScale = Vector3.one;
            }

            Vector3 backed = dropAt + Vector3.left * BackOff;
            yield return ctx.Tween(0.8f, p => who.transform.position = Vector3.Lerp(dropAt, backed, p));
            Vector3 end = home + Vector3.left * BackOff;
            if (backed != end)
                yield return ctx.Tween(0.8f, p => who.transform.position = Vector3.Lerp(backed, end, p));
            who.SetRest(end);
        }

        public void Snap(CueContext ctx, Cue cue)
        {
            var who = ctx.View.Get(cue.who);
            var what = ctx.View.Get(cue.what);
            if (who == null) return;

            Vector3 dropAt = DropPoint(ctx, cue, who, what);
            if (what != null && what.CarriedBy == who)
            {
                who.SetForkHeight(who.ForkLow);
                what.DetachTo(Ground(who, dropAt));
            }
            if (what != null) what.SetLabelShift(0f);
            who.SetRest(who.Rest + Vector3.left * BackOff);
        }

        public string Describe(Cue cue) => $"{cue.who} drops {cue.what}";

        // Where `who` stands to drop: where it rests, or level with the crate's own lane when the crate
        // stays in the yard and belongs to the other lane (the file keeps each marker in its lane).
        static Vector3 DropPoint(CueContext ctx, Cue cue, MarkerView who, MarkerView what)
        {
            var at = who.Rest;
            bool carried = what != null && what.CarriedBy == who;
            bool leaves = what != null && Array.IndexOf(cue.patch.gone, what.Id) >= 0;
            if (carried && !leaves && what.Lane != who.Lane) at.z = ctx.View.LaneZ(what.Lane);
            return at;
        }

        // The spot under the forks when `who` stands at `dropAt`.
        static Vector3 Ground(MarkerView who, Vector3 dropAt) =>
            new Vector3(dropAt.x + who.ForkReach, YardView.GroundY, dropAt.z);
    }
}
