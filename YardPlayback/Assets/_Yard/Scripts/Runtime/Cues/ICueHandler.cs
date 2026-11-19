using System.Collections;
using Yard.Core;

namespace Yard.Cues
{
    /// <summary>
    /// One cue type. To add a cue type, write a class that implements this and mark it
    /// <c>[CueType("name"), Preserve]</c>: the registry finds it, and the player is untouched.
    /// Handlers never change loads or remove markers; the player applies <c>cue.patch</c> afterwards.
    /// </summary>
    public interface ICueHandler
    {
        /// <summary>Animate the cue. Move things only through ctx.Tween / ctx.Wait (playback time).</summary>
        IEnumerator Play(CueContext ctx, Cue cue);

        /// <summary>Put the yard in the cue's end look at once, from the state before the cue (used by Skip).</summary>
        void Snap(CueContext ctx, Cue cue);

        /// <summary>A short line for the HUD, e.g. "fork-1 takes crate-a".</summary>
        string Describe(Cue cue);
    }
}
