namespace Yard.Core
{
    /// <summary>
    /// The one clock every cue reads. While paused it gives 0; otherwise this frame's time × speed.
    /// Cues advance only by <see cref="Delta"/>, so Pause freezes motion and the cue's own timer,
    /// and a speed change mid-cue changes its pace without skipping or replaying anything.
    /// </summary>
    public class PlaybackClock
    {
        float frameSeconds;

        public bool Paused { get; set; }
        public float Speed { get; set; } = 1f;
        public float Delta => Paused ? 0f : frameSeconds * Speed;

        public void BeginFrame(float realFrameSeconds) => frameSeconds = realFrameSeconds;
    }
}
