using UnityEngine;

namespace Yard.Cues
{
    /// <summary>Easing curves. Every visible change in the yard goes through one of these, so nothing pops.</summary>
    public static class Easing
    {
        /// <summary>Smoothstep: eases in and out.</summary>
        public static float InOut(float p)
        {
            p = Mathf.Clamp01(p);
            return p * p * (3f - 2f * p);
        }

        /// <summary>Accelerates: slow start, fast finish (a fall).</summary>
        public static float In(float p)
        {
            p = Mathf.Clamp01(p);
            return p * p;
        }

        /// <summary>Decelerates: fast start, gentle stop.</summary>
        public static float Out(float p)
        {
            p = Mathf.Clamp01(p);
            return 1f - (1f - p) * (1f - p);
        }

        /// <summary>Goes slightly past 1, then settles back: a small bounce.</summary>
        public static float OutBack(float p)
        {
            p = Mathf.Clamp01(p);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float q = p - 1f;
            return 1f + c3 * q * q * q + c1 * q * q;
        }

        /// <summary>0 → 1 → 0 as one smooth hump.</summary>
        public static float Pulse(float p) => Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);

        /// <summary>Rises over the first <paramref name="edge"/> of p, holds at 1, falls over the last.</summary>
        public static float Envelope(float p, float edge) =>
            edge <= 0f ? 1f : InOut(Mathf.Min(p / edge, (1f - p) / edge));
    }
}
