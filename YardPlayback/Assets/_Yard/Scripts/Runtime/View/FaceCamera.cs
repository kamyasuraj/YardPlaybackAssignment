using UnityEngine;

namespace Yard
{
    /// <summary>
    /// Keeps a world-space label square to the camera in Play mode, even if the camera moves.
    /// In Edit mode the label keeps the rotation it was saved with, which already faces the scene camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
