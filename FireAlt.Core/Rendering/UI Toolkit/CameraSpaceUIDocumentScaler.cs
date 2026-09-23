using System.Reflection;
using UnityEngine;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace FireAlt.Core.Rendering
{
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(PanelRenderer))]
    [NoAutoStaticsCleanup]
    public class CameraSpaceUIDocumentScaler : MonoBehaviour
    {
        private static readonly PropertyInfo PIXELS_PER_UNIT = typeof(PanelSettings).GetProperty("pixelsPerUnit",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [Header("Camera")]
        public Camera referenceCamera;
        public float planeDistance = 100f;

        [Header("Scale With Screen Size")]
        [SerializeField] private PanelScreenMatchMode _screenMatchMode;
        [Range(0, 1)] [SerializeField] private float _matchWidthOrHeight;
        [SerializeField] private Vector2 _referenceResolution = new(1920, 1080);

        private PanelRenderer _renderer;
        private VisualElement _root;
        private Vector2 _lastSize;
        private float _lastScale;

        private void OnEnable()
        {
            _renderer = GetComponent<PanelRenderer>();
            _renderer.worldSpaceSizeMode = WorldSpaceSizeMode.Dynamic;
            _renderer.pivot = Pivot.Center;
            _renderer.RegisterUIReloadCallback(OnUIReload);
            ((IPanelComponent)_renderer).PerformValidation(true);
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
            ForceUpdate();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            _renderer?.UnregisterUIReloadCallback(OnUIReload);
            _root = null;
            _renderer = null;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) ForceUpdate();
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == referenceCamera) UpdateLayout(false);
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            _root = root;
            _lastSize = default;
            ForceUpdate();
        }

        public void ForceUpdate()
        {
            UpdateLayout(true);
        }

        private void UpdateLayout(bool force)
        {
            if (referenceCamera == null || _renderer == null) return;

            var cameraTransform = referenceCamera.transform;
            transform.position = cameraTransform.position + cameraTransform.rotation * Vector3.forward * planeDistance;
            transform.rotation = cameraTransform.rotation;

            if (_root == null || _renderer.panelSettings == null) return;

            var ppu = PIXELS_PER_UNIT == null ? _renderer.panelSettings.referenceSpritePixelsPerUnit :
                (float)PIXELS_PER_UNIT.GetValue(_renderer.panelSettings);
            var (size, scale) = CalculateLayout(referenceCamera.pixelRect.size, _referenceResolution,
                _screenMatchMode, _matchWidthOrHeight, referenceCamera.orthographic,
                referenceCamera.fieldOfView, referenceCamera.orthographicSize, planeDistance, ppu);
            if (size.x <= 0 || size.y <= 0) return;
            if (!force && _lastSize == size && Mathf.Approximately(_lastScale, scale)) return;

            _root.style.width = size.x;
            _root.style.height = size.y;
            transform.localScale = Vector3.one * scale;
            _lastSize = size;
            _lastScale = scale;
        }

        public static (Vector2 Size, float Scale) CalculateLayout(Vector2 viewport, Vector2 referenceResolution,
            PanelScreenMatchMode matchMode, float match, bool orthographic, float fieldOfView,
            float orthographicSize, float distance, float pixelsPerUnit)
        {
            if (viewport.x <= 0 || viewport.y <= 0 || referenceResolution.x <= 0 ||
                referenceResolution.y <= 0 || pixelsPerUnit <= 0 || distance <= 0) return default;

            var width = viewport.x / referenceResolution.x;
            var height = viewport.y / referenceResolution.y;
            var factor = matchMode switch
            {
                PanelScreenMatchMode.Expand => Mathf.Min(width, height),
                PanelScreenMatchMode.Shrink => Mathf.Max(width, height),
                _ => Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width, 2f), Mathf.Log(height, 2f), match)),
            };
            var size = viewport / factor;
            var visibleHeight = orthographic ? 2f * orthographicSize :
                2f * distance * Mathf.Tan(0.5f * fieldOfView * Mathf.Deg2Rad);
            return (size, visibleHeight * pixelsPerUnit / size.y);
        }

        public static Vector2 CameraToPanel(VisualElement panelRoot, Vector2 cameraPixelPosition, Vector2 cameraPixelSize)
        {
            cameraPixelPosition.y = cameraPixelSize.y - cameraPixelPosition.y;
            var panelResolution = new Vector2(panelRoot.resolvedStyle.width, panelRoot.resolvedStyle.height);
            var normalizedPos = new Vector2(Mathf.InverseLerp(0f, cameraPixelSize.x, cameraPixelPosition.x),
                Mathf.InverseLerp(0f, cameraPixelSize.y, cameraPixelPosition.y));
            return normalizedPos * panelResolution;
        }
    }
}
