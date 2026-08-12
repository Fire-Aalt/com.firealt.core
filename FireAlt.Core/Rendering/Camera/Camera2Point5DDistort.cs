using UnityEngine;

namespace FireAlt.Core.Rendering
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class Camera2Point5DDistort : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private Camera _camera;

        private void OnValidate()
        {
            TryGetComponent(out _camera);
        }

        //// Technically this only needs to happen once at start-up,
        //// or when the window is being resized, but it's cheap
        //// enough to do every frame in the absence of a built-in
        //// OnProjectionChanged event.
        private void LateUpdate()
        {
#if BL_BRIDGE
            if (Application.isPlaying) return;
#endif
            // Get the default projection matrix for this camera.
            DistortProjectionMatrix(_camera, true);
        }

        public static void DistortProjectionMatrix(Camera camera, bool resetMatrix)
        {
            if (resetMatrix)
            {
                camera.ResetProjectionMatrix();
            }
            var mat = camera.projectionMatrix;
            // Scale the vertical axis by 1/sin(angle).
            mat[1, 1] *= Mathf.Sqrt(2);

            camera.projectionMatrix = mat;
        }

        private void OnDisable()
        {
            _camera.ResetProjectionMatrix();
        }
    }
}

