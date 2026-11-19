using System;
using System.Collections;
using System.Collections.Generic;

namespace Yard.Core
{
    /// <summary>
    /// Steps a routine, and any IEnumerator it yields, one frame at a time. Playback owns it,
    /// so <see cref="Stop"/> kills everything in flight at once: Skip leaves no motion behind.
    /// </summary>
    public class RoutineRunner
    {
        const int MaxStepsPerTick = 10000;

        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        int version;

        public bool IsRunning => stack.Count > 0;

        public void Start(IEnumerator routine)
        {
            version++;
            stack.Clear();
            if (routine != null) stack.Push(routine);
        }

        public void Stop()
        {
            version++;
            stack.Clear();
        }

        /// <summary>Runs until the routine waits for the next frame (yields anything but an IEnumerator) or ends.</summary>
        public void Tick()
        {
            int started = version;
            for (int steps = 0; stack.Count > 0; steps++)
            {
                if (steps > MaxStepsPerTick) throw new InvalidOperationException("RoutineRunner: the routine never waited for a frame.");
                var top = stack.Peek();
                bool more = top.MoveNext();
                if (version != started) return;   // stopped or restarted from inside the routine
                if (!more) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
        }
    }
}
