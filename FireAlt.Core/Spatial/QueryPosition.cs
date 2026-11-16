using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct QueryPosition
    {
        public float3 Position;
        public float2 SpatialPosition => Position.xz;

        public QueryPosition(float3 position)
        {
            Position = position;
        }
    }
}