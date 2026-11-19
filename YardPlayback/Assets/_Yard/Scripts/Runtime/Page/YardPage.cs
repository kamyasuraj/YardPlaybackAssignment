using UnityEngine;

namespace Yard
{
    /// <summary>
    /// The page's way in. The scene's GameObject named <c>YardPlayer</c> carries only this component and has
    /// no children, so a page calling <c>SendMessage("YardPlayer", method, value)</c> reaches exactly these six
    /// methods and nothing else. Each one forwards to the player. The page contract (index.html):
    /// <list type="bullet">
    /// <item><c>Load(string json)</c>: the whole script as one string; a bad script is rejected and the current one stays.</item>
    /// <item><c>Play()</c>: the page's Start; plays from the opening (or resumes if paused).</item>
    /// <item><c>Pause()</c>, <c>Resume()</c>.</item>
    /// <item><c>SetSpeed(number)</c>: the page's Speed; 1 or 2.</item>
    /// <item><c>Skip()</c>: straight to <c>closing</c>; a second press stays there.</item>
    /// </list>
    /// When playback finishes, the page hears it through <see cref="PageLink"/>.
    /// It has no Unity message methods (Awake, Start, Update…), so none can be triggered by name.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardPage : MonoBehaviour
    {
        [Tooltip("The player these calls go to.")]
        [SerializeField] YardPlayer player;

        public void Load(string json)
        {
            if (player != null) player.Load(json);
        }

        public void Play()
        {
            if (player != null) player.Play();
        }

        public void Pause()
        {
            if (player != null) player.Pause();
        }

        public void Resume()
        {
            if (player != null) player.Resume();
        }

        public void SetSpeed(float speed)
        {
            if (player != null) player.SetSpeed(speed);
        }

        public void Skip()
        {
            if (player != null) player.Skip();
        }
    }
}
