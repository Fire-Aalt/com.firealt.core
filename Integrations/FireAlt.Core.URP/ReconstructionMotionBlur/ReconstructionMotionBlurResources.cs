using System;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireAlt.Core.URP
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [CategoryInfo(Name = "R: Reconstruction Motion Blur", Order = 1000), HideInInspector]
    internal sealed class ReconstructionMotionBlurResources : IRenderPipelineResources
    {
        [SerializeField, HideInInspector] private int _version;

        [SerializeField, ResourcePath("Hidden/FireAlt/ReconstructionMotionBlur", SearchType.ShaderName)]
        private Shader _shader;

        public int version => _version;

        public bool isAvailableInPlayerBuild => true;

        public Shader Shader
        {
            get => _shader;
            set => this.SetValueAndNotify(ref _shader, value);
        }
    }
}
