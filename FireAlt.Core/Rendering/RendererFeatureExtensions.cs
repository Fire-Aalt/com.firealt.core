using UnityEngine;

namespace FireAlt.Core.Rendering
{
    public static class RendererFeatureExtensions
    {
        public static bool DescriptorMatches(this RenderTextureDescriptor descriptor, RenderTextureDescriptor other)
        {
            return descriptor.width == other.width
                && descriptor.height == other.height
                && descriptor.graphicsFormat == other.graphicsFormat
                && descriptor.depthBufferBits == other.depthBufferBits
                && descriptor.useMipMap == other.useMipMap
                && descriptor.autoGenerateMips == other.autoGenerateMips
                && descriptor.msaaSamples == other.msaaSamples;
        }
    }
}
