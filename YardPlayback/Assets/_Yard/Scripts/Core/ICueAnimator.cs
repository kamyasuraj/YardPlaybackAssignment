using System.Collections;

namespace Yard.Core
{
    /// <summary>
    /// The visual side of playback: the Unity scene implements it, and tests fake it.
    /// Nothing here changes loads or presence; playback does that through the yard state.
    /// </summary>
    public interface ICueAnimator
    {
        /// <summary>Build the yard as the script opens.</summary>
        void Reset(YardScript script, YardState state);

        /// <summary>Is there a handler for this cue type?</summary>
        bool CanPlay(string say);

        /// <summary>Animate one cue; advance time only through the playback clock.</summary>
        IEnumerator Play(Cue cue);

        /// <summary>Show a cue's patch once the state holds it: loads that were set, markers that left.</summary>
        IEnumerator AnimatePatch(Cue cue, PatchResult result, YardState state);

        /// <summary>Put the yard in this cue's end look at once (used by Skip).</summary>
        void Snap(Cue cue);

        /// <summary>Clear any motion in flight: offsets, flashes, pulses.</summary>
        void HaltMotion();

        /// <summary>Make the view equal the state with no motion.</summary>
        void SyncInstant(YardState state);
    }
}
