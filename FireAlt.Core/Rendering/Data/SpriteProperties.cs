using System;
using System.Diagnostics;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.Core.Rendering
{
    [Serializable]
    public struct SpriteProperties : IEquatable<SpriteProperties>
    {
        public float2 normalizedPivot;
        public float2 rectScale;
        public float4 uvAtlas;

        public float2 UvScale => new float2(uvAtlas.x, uvAtlas.y);
        public float2 UvBias => new float2(uvAtlas.z, uvAtlas.w);
        
        public SpriteProperties(Sprite sprite)
        {
            if (sprite != null)
            {
                CheckSprite(sprite);
                uvAtlas = RendererUtility.GetUvAtlas(sprite);
                normalizedPivot = RendererUtility.GetNormalizedPivot(sprite);
                rectScale = RendererUtility.GetRectScale(sprite, uvAtlas);
            }
            else
            {
                uvAtlas = new float4(1f, 1f, 0, 0);
                normalizedPivot = new float2(0.5f, 0.5f);
                rectScale = new float2(1f, 1f);
            }
        }
        
        public bool Equals(SpriteProperties other)
        {
            return normalizedPivot.Equals(other.normalizedPivot) && rectScale.Equals(other.rectScale) && uvAtlas.Equals(other.uvAtlas);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(normalizedPivot, rectScale, uvAtlas);
        }
        
        [AssertionMethod]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        public static void CheckSprite(Sprite sprite)
        {
            if (sprite == null || (sprite.packed && (sprite.packingMode != SpritePackingMode.Rectangle || sprite.packingRotation != SpritePackingRotation.None)))
            {
                throw new ArgumentException($"Sprite {sprite.name} must use rectangular packing with rotation disabled.");
            }
        }
    }
}