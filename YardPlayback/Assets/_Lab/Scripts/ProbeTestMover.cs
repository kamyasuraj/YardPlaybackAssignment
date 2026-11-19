using UnityEngine;

namespace Lab
{
    /// <summary>
    /// Moves a non-static test object along waypoints in Play mode, through bright and dark parts of the lab,
    /// to show it takes its light from the light probes (and its one real-time shadow from the key light).
    /// Eases in and out at each waypoint and waits briefly there.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProbeTestMover : MonoBehaviour
    {
        [Tooltip("World-space points visited in order, then back to the first.")]
        [SerializeField] Vector3[] waypoints = new Vector3[0];
        [Tooltip("Metres per second at full speed.")]
        [SerializeField] float speed = 1.5f;
        [Tooltip("Seconds to wait at each waypoint.")]
        [SerializeField] float pause = 1f;

        int target;
        float t;
        float wait;
        Vector3 from;

        void Start()
        {
            if (waypoints.Length == 0) { enabled = false; return; }
            transform.position = waypoints[0];
            from = waypoints[0];
            target = waypoints.Length > 1 ? 1 : 0;
        }

        void Update()
        {
            if (waypoints.Length < 2) return;
            if (wait > 0f) { wait -= Time.deltaTime; return; }

            var to = waypoints[target];
            float length = Mathf.Max(0.01f, Vector3.Distance(from, to));
            t = Mathf.Min(1f, t + Time.deltaTime * speed / length);
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            if (t < 1f) return;

            from = to;
            target = (target + 1) % waypoints.Length;
            t = 0f;
            wait = pause;
        }
    }
}
