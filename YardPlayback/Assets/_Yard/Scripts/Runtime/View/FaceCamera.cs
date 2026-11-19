using UnityEngine;

namespace Yard
{
    /// <summary>
    /// Keeps a world-space label square to the camera in Play mode, and the same size on screen
    /// wherever it is in the yard (so the far lane's numbers read as well as the near lane's).
    /// In Edit mode the label keeps the rotation and size it was saved with.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaceCamera : MonoBehaviour
    {
        [Tooltip("Keep the label the same size on screen wherever it is in the yard.")]
        [SerializeField] bool keepScreenSize = true;
        [Tooltip("Camera distance at which the label shows at its saved size.")]
        [SerializeField] float referenceDistance = 12f;

        Vector3 savedScale;

        void Awake() => savedScale = transform.localScale;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
            if (!keepScreenSize || referenceDistance <= 0f) return;
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            transform.localScale = savedScale * (distance / referenceDistance);
        }
    }
}
