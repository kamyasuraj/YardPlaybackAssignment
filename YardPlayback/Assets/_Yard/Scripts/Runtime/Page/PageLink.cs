using System;
using System.Runtime.InteropServices;

namespace Yard
{
    /// <summary>
    /// How the page hears that playback finished. In a WebGL build the payload goes to the page as a browser
    /// event: <c>window</c> receives <c>yard:finished</c> with <c>event.detail</c> = the payload JSON string,
    /// <c>{"ticket":"shift-04","left":[{"id":"fork-1","lane":"north","load":2}, …]}</c> (who is left and each load).
    /// Inside Unity the same payload is raised on <see cref="Finished"/> (the editor's page controls listen).
    /// </summary>
    public static class PageLink
    {
        /// <summary>The finished payload, as the page receives it.</summary>
        public static event Action<string> Finished;

        public static void NotifyFinished(string payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YardPage_Finished(payload);
#endif
            Finished?.Invoke(payload);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YardPage_Finished(string payload);
#endif
    }
}
