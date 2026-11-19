using System.Collections;
using Yard.Core;
using Yard.Cues;

namespace Yard
{
    /// <summary>Plays cues in the scene: each cue goes to the handler registered for its <c>say</c>.</summary>
    public class YardAnimator : ICueAnimator
    {
        readonly CueRegistry registry;
        readonly CueContext ctx;

        public YardAnimator(CueRegistry registry, CueContext ctx)
        {
            this.registry = registry;
            this.ctx = ctx;
        }

        public void Reset(YardScript script, YardState state)
        {
            ctx.View.Rebuild(script, state);
            ctx.Hud.ResetFor(script);
        }

        public bool CanPlay(string say) => registry.TryGet(say, out _);

        public IEnumerator Play(Cue cue) => registry.TryGet(cue.say, out var handler) ? handler.Play(ctx, cue) : null;

        public IEnumerator AnimatePatch(Cue cue, PatchResult result, YardState state) => ctx.View.AnimatePatch(ctx, result, state);

        public void Snap(Cue cue)
        {
            if (registry.TryGet(cue.say, out var handler)) handler.Snap(ctx, cue);
        }

        public void HaltMotion()
        {
            ctx.View.HaltMotion();
            ctx.Hud.PulseMinute(0f);
        }

        public void SyncInstant(YardState state) => ctx.View.SyncInstant(state);

        /// <summary>A short line for the HUD, written by the cue's own handler.</summary>
        public string Describe(Cue cue)
        {
            if (registry.TryGet(cue.say, out var handler)) return $"{cue.say}  ·  {handler.Describe(cue)}";
            return string.IsNullOrEmpty(cue.who) ? cue.say : $"{cue.say}  ·  {cue.who}";
        }
    }
}
