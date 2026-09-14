using FireAlt.Core.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Tests
{
    public class CameraSpaceUIDocumentScalerTests
    {
        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2560, 1080)]
        public void PerspectiveLayoutFitsViewport(int width, int height)
        {
            var result = CameraSpaceUIDocumentScaler.CalculateLayout(new Vector2(width, height),
                new Vector2(1920, 1080), PanelScreenMatchMode.MatchWidthOrHeight, 0.5f,
                false, 60f, 5f, 0.5f, 100f);

            Assert.That(result.Size.x, Is.GreaterThan(0));
            Assert.That(result.Size.y * result.Scale / 100f,
                Is.EqualTo(2f * 0.5f * Mathf.Tan(30f * Mathf.Deg2Rad)).Within(0.0001f));
        }

        [Test]
        public void OrthographicLayoutFitsVisiblePlane()
        {
            var result = CameraSpaceUIDocumentScaler.CalculateLayout(new Vector2(1920, 1080),
                new Vector2(1920, 1080), PanelScreenMatchMode.MatchWidthOrHeight, 0.5f,
                true, 60f, 7f, 0.5f, 100f);

            Assert.That(result.Size, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(result.Size.y * result.Scale / 100f, Is.EqualTo(14f).Within(0.0001f));
        }

        [Test]
        public void PlaneDistanceChangesPerspectiveScale()
        {
            var near = CameraSpaceUIDocumentScaler.CalculateLayout(new Vector2(1920, 1080),
                new Vector2(1920, 1080), PanelScreenMatchMode.MatchWidthOrHeight, 0.5f,
                false, 60f, 5f, 0.5f, 100f);
            var far = CameraSpaceUIDocumentScaler.CalculateLayout(new Vector2(1920, 1080),
                new Vector2(1920, 1080), PanelScreenMatchMode.MatchWidthOrHeight, 0.5f,
                false, 60f, 5f, 1f, 100f);

            Assert.That(far.Scale, Is.EqualTo(near.Scale * 2f).Within(0.0001f));
        }

        [Test]
        public void EnableDisableAndPanelReloadUseCurrentCameraAndPlane()
        {
            var cameraObject = new GameObject("Camera");
            var panelObject = new GameObject("Panel");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(2f, 3f, 4f);
                panelObject.SetActive(false);
                var renderer = panelObject.AddComponent<PanelRenderer>();
                renderer.panelSettings = settings;
                var scaler = panelObject.AddComponent<CameraSpaceUIDocumentScaler>();
                scaler.referenceCamera = camera;
                scaler.planeDistance = 0.5f;

                panelObject.SetActive(true);
                scaler.ForceUpdate();
                Assert.That(panelObject.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4.5f)));

                ((IPanelComponent)renderer).PerformValidation(true);
                cameraObject.transform.position = new Vector3(6f, 7f, 8f);
                scaler.ForceUpdate();
                Assert.That(panelObject.transform.position, Is.EqualTo(new Vector3(6f, 7f, 8.5f)));

                panelObject.SetActive(false);
                panelObject.SetActive(true);
                scaler.ForceUpdate();
                Assert.That(panelObject.transform.position, Is.EqualTo(new Vector3(6f, 7f, 8.5f)));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
