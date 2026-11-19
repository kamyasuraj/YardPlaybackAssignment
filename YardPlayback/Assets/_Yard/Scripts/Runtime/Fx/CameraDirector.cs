using UnityEngine;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// A gentle push-in for the moment of the clip: the camera narrows a little and turns partly toward
    /// the subject, then eases back. Mild by design, so both lanes stay in frame. Runs on playback time;
    /// <see cref="Snap"/> returns to the home shot at once (Skip).
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [Tooltip("Field of view at full push-in. The home value is the camera's own.")]
        [SerializeField] float pushFieldOfView = 55f;
        [Tooltip("How far the camera turns toward the subject at full push-in (0 = not at all, 1 = straight at it).")]
        [Range(0f, 1f)]
        [SerializeField] float turnToward = 0.15f;
        [Tooltip("Seconds of playback time to push in or ease back.")]
        [SerializeField] float seconds = 0.9f;

        float homeFieldOfView;
        Quaternion homeRotation;
        Transform subject;
        float k;
        float goal;
        bool ready;

        public bool IsSetUp => cam != null;

        public void Prepare()
        {
            homeFieldOfView = cam.fieldOfView;
            homeRotation = cam.transform.rotation;
            ready = true;
        }

        public void PushIn(Transform target)
        {
            subject = target;
            goal = 1f;
        }

        public void Release() => goal = 0f;

        public void Snap()
        {
            k = goal = 0f;
            Apply();
        }

        public void Tick(float dt)
        {
            if (!ready) return;
            k = Mathf.MoveTowards(k, goal, dt / Mathf.Max(0.01f, seconds));
            Apply();
        }

        void Apply()
        {
            if (!ready) return;
            float s = Easing.InOut(k);
            cam.fieldOfView = Mathf.Lerp(homeFieldOfView, pushFieldOfView, s);
            var rotation = homeRotation;
            if (subject != null && s > 0f)
            {
                var toward = Quaternion.LookRotation(subject.position - cam.transform.position);
                rotation = Quaternion.Slerp(homeRotation, toward, turnToward * s);
            }
            cam.transform.rotation = rotation;
        }
    }
}
