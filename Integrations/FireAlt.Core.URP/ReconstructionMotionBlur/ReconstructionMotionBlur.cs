using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireAlt.Core.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/Reconstruction Motion Blur")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class ReconstructionMotionBlur : VolumeComponent
    {
        [Tooltip("Samples per pixel: 4, 6 or 8. Lower presets cost less but show more banding in long streaks.")]
        public MotionBlurQualityParameter quality = new(MotionBlurQuality.Low);

        [Tooltip("Blur length as a fraction of each frame's motion: 1 blurs everything that moved since the last frame.")]
        public ClampedFloatParameter intensity = new(0f, 0f, 2f);

        [Tooltip("The longest blur, as a fraction of the screen height. Lower values cost less.")]
        public ClampedFloatParameter clamp = new(0.05f, 0f, 0.2f);

        [Tooltip("How much of the camera's own motion is blurred. Object motion always blurs fully. Lower values ease motion sickness.")]
        public ClampedFloatParameter cameraBlur = new(1f, 0f, 1f);

        public bool IsActive() => intensity.value > 0f && clamp.value > 0f;

        public int SampleCount => quality.value switch
        {
            MotionBlurQuality.Low => 4,
            MotionBlurQuality.Medium => 6,
            _ => 8,
        };
    }
}
