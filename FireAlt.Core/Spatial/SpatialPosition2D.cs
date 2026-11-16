using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct SpatialPosition2D : ISpatialPosition
    {
        public float2 Position;

        float2 ISpatialPosition.Position => Position;

        public static implicit operator SpatialPosition2D(float2 position) => new() { Position = position };
    }
}