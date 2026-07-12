using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Rendering
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(UIDocument))]
    public class CameraSpaceUIDocumentScaler : MonoBehaviour
    {
        private const float ORTHO_GRAPHIC_CAMERA_REF_SIZE = 5.39f;
        private const float PERSPECTIVE_CAMERA_REF_FOV = 56.6f;
        private const float PERSPECTIVE_CAMERA_REF_DISTANCE = 10f;
        
        [SerializeField, HideInInspector]
        private UIDocument _uiDocument;
        
        [Header("Camera")]
        [Tooltip("Camera used for scaling / distance reference.")]
        public Camera referenceCamera;
        [Tooltip("Z axis distance. Can be used for sorting planes")]
        public float planeDistance = 100f;
        
        [Header("Scale With Screen Size")]
        [Tooltip("Match Width Or Height: Scale the ui document with the width as reference, the height as reference, or something in between.\nExpand: Expand the ui document either horizontally or vertically, so the size of the ui document will never be smaller than the reference.\nShrink: Crop the ui document either horizontally or vertically, so the size of the ui document will never be larger than the reference.")]
        [SerializeField] private PanelScreenMatchMode _screenMatchMode;

        [Tooltip("Determines if the scaling is using the width or height as reference, or a mix in between.")]
        [Range(0, 1)]
        [SerializeField] private float _matchWidthOrHeight;

        [Tooltip("Target logical UI resolution (width x height) in UI Toolkit pixels.")]
        [SerializeField] private Vector2 _referenceResolution = new(1920, 1080);

        private Vector2 _curResolution;
        private float _curCameraScaleFactor;
        private VisualElement _curRoot;
        private Vector2 _prevRootSize;

        private void OnValidate()
        {
            _uiDocument = GetComponent<UIDocument>();
#if UNITY_6000_6_OR_NEWER
            _uiDocument.worldSpaceSizeMode = WorldSpaceSizeMode.Dynamic;
#else
            _uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
#endif
            _uiDocument.pivot = Pivot.Center;
        }

        private void Update()
        {
            if (referenceCamera == null) 
                return;
            
            var newRoot = _uiDocument.rootVisualElement;
            if (newRoot == null) 
                return;
            
            var cameraTransform = referenceCamera.transform;
            var newScreenResolution = referenceCamera.pixelRect.size;
            var newCameraScaleFactor = referenceCamera.orthographic ? GetOrthographicCameraScale() : GetPerspectiveCameraScale();
            
            transform.position = cameraTransform.position + cameraTransform.rotation * new Vector3(0f, 0f, planeDistance);
            transform.rotation = cameraTransform.rotation;
            
            var shouldUpdate = false;
            
            // UI Document layout recalculation can be expansive so during runtime only update when cached values changed
            if (Application.isPlaying)
            {
                var rootChanged = newRoot != _curRoot 
                                  || newRoot.resolvedStyle.width != _prevRootSize.x 
                                  || newRoot.resolvedStyle.height != _prevRootSize.y;
                
                if (_curResolution != newScreenResolution || _curCameraScaleFactor != newCameraScaleFactor || rootChanged)
                {
                    shouldUpdate = true;
                }
            }
            else
            {
                shouldUpdate = true;
            }
            
            if (shouldUpdate)
            {
                _curResolution = newScreenResolution;
                _curCameraScaleFactor = newCameraScaleFactor;
                _curRoot = newRoot;
                
                ForceUpdate();
                _prevRootSize = new Vector2(newRoot.resolvedStyle.width, newRoot.resolvedStyle.height);
            }
        }

        /// <summary>
        /// Forces recalculation
        /// </summary>
        public void ForceUpdate()
        {
            HandleScaleWithScreenSize();
        }
        
        private void HandleScaleWithScreenSize()
        {
            var scaleFactor = 0f;
            
            switch (_screenMatchMode)
            {
                case PanelScreenMatchMode.MatchWidthOrHeight:
                {
                    const float logBase = 2f;
                    
                    // We take the log of the relative width and height before taking the average.
                    // Then we transform it back in the original space.
                    // the reason to transform in and out of logarithmic space is to have better behavior.
                    // If one axis has twice resolution and the other has half, it should even out if widthOrHeight value is at 0.5.
                    // In normal space the average would be (0.5 + 2) / 2 = 1.25
                    // In logarithmic space the average is (-1 + 1) / 2 = 0
                    float logWidth = Mathf.Log(_curResolution.x / _referenceResolution.x, logBase);
                    float logHeight = Mathf.Log(_curResolution.y / _referenceResolution.y, logBase);
                    float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, _matchWidthOrHeight);
                    scaleFactor = Mathf.Pow(logBase, logWeightedAverage);
                    break;
                }
                case PanelScreenMatchMode.Expand:
                {
                    scaleFactor = Mathf.Min(_curResolution.x / _referenceResolution.x, _curResolution.y / _referenceResolution.y);
                    break;
                }
                case PanelScreenMatchMode.Shrink:
                {
                    scaleFactor = Mathf.Max(_curResolution.x / _referenceResolution.x, _curResolution.y / _referenceResolution.y);
                    break;
                }
            }

            SetScaleFactor(scaleFactor);
        }

        private void SetScaleFactor(float factor)
        {
            var resolutionRatio = _referenceResolution / _curResolution;
            var finalScaleFactor = resolutionRatio * factor;

            // Only depends on height as observed from CanvasScaler
            var transformUniformScale = finalScaleFactor.y * _curCameraScaleFactor;
            transform.localScale = referenceCamera.transform.lossyScale * transformUniformScale;
            
            var scaledResolution = new Vector2(_referenceResolution.x * finalScaleFactor.y, _referenceResolution.y * finalScaleFactor.x);
            _curRoot.style.width = scaledResolution.x;
            _curRoot.style.height = scaledResolution.y;
        }
        
        private float GetOrthographicCameraScale()
        {
            return referenceCamera.orthographicSize / ORTHO_GRAPHIC_CAMERA_REF_SIZE;
        }
        
        private float GetPerspectiveCameraScale()
        {
            const float deg2Rad = Mathf.PI / 180f;
            
            var currentDistance = Vector3.Distance(referenceCamera.transform.position, transform.position);

            const float halfBase = 0.5f * PERSPECTIVE_CAMERA_REF_FOV * deg2Rad;
            var halfCurrent = 0.5f * referenceCamera.fieldOfView * deg2Rad;

            var tanBase = Mathf.Tan(halfBase);
            var tanCurrent = Mathf.Tan(halfCurrent);
            
            var cameraScale = (currentDistance * tanCurrent) / (PERSPECTIVE_CAMERA_REF_DISTANCE * tanBase);
            return cameraScale;
        }
        
        public static Vector2 CameraToPanel(VisualElement panelRoot, Vector2 cameraPixelPosition, Vector2 cameraPixelSize)
        {
            // Flip Y axis to match how UI Toolkit treats it
            cameraPixelPosition.y = cameraPixelSize.y - cameraPixelPosition.y;
            var panelResolution = new Vector2(panelRoot.resolvedStyle.width, panelRoot.resolvedStyle.height);

            var normalizedPos = new Vector2(Mathf.InverseLerp(0f, cameraPixelSize.x, cameraPixelPosition.x),
                Mathf.InverseLerp(0f, cameraPixelSize.y, cameraPixelPosition.y));
            
            return normalizedPos * panelResolution;
        }
    }
}
