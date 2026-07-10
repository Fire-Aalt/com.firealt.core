using System;
using Unity.Mathematics;

namespace FireAlt.Core
{
    /// <summary> A two-dimensional axis-aligned bounding box represented by its center and extents. </summary>
    [Serializable]
    public struct AABB2D : IEquatable<AABB2D>
    {
        private const float MIN_SIZE = 0.0001f;

        /// <summary> The location of the center of the AABB. </summary>
        public float2 Center;

        /// <summary> The positive distance from the center to the maximum corner. </summary>
        public float2 Extents;

        /// <summary> Initializes a new instance of the <see cref="AABB2D" /> struct from minimum and maximum coordinates. </summary>
        /// <param name="min"> The minimum coordinate of the AABB. </param>
        /// <param name="max"> The maximum coordinate of the AABB. </param>
        public AABB2D(float2 min, float2 max)
        {
            Extents = (max - min) * 0.5f;
            Center = min + Extents;
        }

        /// <summary> The full size of the AABB. </summary>
        public float2 Size => Extents * 2f;

        /// <summary> The width of the AABB. </summary>
        public float Width => Size.x;

        /// <summary> The height of the AABB. </summary>
        public float Height => Size.y;

        /// <summary> The minimum coordinate of the AABB. </summary>
        public float2 Min => Center - Extents;

        /// <summary> The maximum coordinate of the AABB. </summary>
        public float2 Max => Center + Extents;

        /// <summary> Returns a string representation of the AABB. </summary>
        /// <returns> A string representation of the AABB. </returns>
        public override string ToString()
        {
            return $"AABB2D(Center:{Center}, Extents:{Extents})";
        }

        /// <summary> Returns whether the AABB contains a point, including its boundary. </summary>
        /// <param name="point"> The point to test. </param>
        /// <returns> True if the point is contained by the AABB. </returns>
        public bool Contains(float2 point) => math.all(point >= Min & point <= Max);

        /// <summary> Returns whether the AABB completely contains another AABB, including its boundary. </summary>
        /// <param name="other"> The AABB to test. </param>
        /// <returns> True if <paramref name="other" /> is contained by the AABB. </returns>
        public bool Contains(AABB2D other) => math.all((Min <= other.Min) & (Max >= other.Max));

        /// <summary> Returns whether the AABB overlaps another AABB, including contact at their boundaries. </summary>
        /// <param name="other"> The AABB to test. </param>
        /// <returns> True if the AABBs overlap. </returns>
        public bool Overlaps(AABB2D other) => Overlaps(other.Min, other.Max);

        /// <summary> Returns whether the AABB overlaps the rectangle described by minimum and maximum coordinates. </summary>
        /// <param name="min"> The minimum coordinate of the rectangle. </param>
        /// <param name="max"> The maximum coordinate of the rectangle. </param>
        /// <returns> True if the rectangles overlap, including contact at their boundaries. </returns>
        public bool Overlaps(float2 min, float2 max)
        {
            return math.all((min <= Max) & (max >= Min));
        }

        /// <summary> Converts a point to normalized coordinates relative to the AABB. </summary>
        /// <param name="point"> The point to convert. </param>
        /// <returns> The normalized, unclamped position where the minimum is zero and the maximum is one. </returns>
        public float2 Normalize(float2 point)
        {
            return (point - Min) / math.max(Size, new float2(MIN_SIZE));
        }

        /// <summary> Returns the minimum and maximum coordinates packed as min-x, min-y, max-x, max-y. </summary>
        /// <returns> The packed minimum and maximum coordinates. </returns>
        public float4 ToMinMax()
        {
            return new float4(Min, Max);
        }

        /// <inheritdoc />
        public bool Equals(AABB2D other)
        {
            return Center.Equals(other.Center) && Extents.Equals(other.Extents);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is AABB2D other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Center, Extents);
        }

        public static bool operator ==(AABB2D left, AABB2D right) => left.Equals(right);

        public static bool operator !=(AABB2D left, AABB2D right) => !left.Equals(right);
    }
}
