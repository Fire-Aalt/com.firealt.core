using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace FireAlt.Core
{
    /// <summary> Geometry of an upright capsule: a sphere swept by a half-height up and down the world Y axis. </summary>
    public static class SpatialCapsule
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClosestOnAxis(float3 center, float halfHeight, float3 point)
        {
            center.y = math.clamp(point.y, center.y - halfHeight, center.y + halfHeight);
            return center;
        }

        /// <summary>
        /// Distance a point starting at <paramref name="offset" /> from the capsule center travels along the normalized
        /// <paramref name="direction" /> before it comes within <paramref name="reach" /> of the capsule axis.
        /// 0 when it starts within reach, positive infinity when it never gets there.
        /// </summary>
        public static float CastDistance(float3 offset, float reach, float halfHeight, float3 direction)
        {
            if (halfHeight <= 0f)
            {
                return SphereEntry(offset, reach, direction);
            }

            var radial = offset;
            radial.y -= math.clamp(offset.y, -halfHeight, halfHeight);
            if (math.lengthsq(radial) <= reach * reach)
            {
                return 0f;
            }

            var cap = new float3(0f, halfHeight, 0f);
            var best = math.min(SphereEntry(offset - cap, reach, direction), SphereEntry(offset + cap, reach, direction));

            var b = math.dot(offset.xz, direction.xz);
            var c = math.lengthsq(offset.xz) - reach * reach;
            if (c > 0f && b < 0f)
            {
                var a = math.lengthsq(direction.xz);
                var discriminant = b * b - a * c;
                if (discriminant >= 0f)
                {
                    var distance = (-b - math.sqrt(discriminant)) / a;
                    if (math.abs(offset.y + direction.y * distance) <= halfHeight)
                    {
                        best = math.min(best, distance);
                    }
                }
            }

            return best;
        }

        /// <summary> Squared distance from a box centered on the origin to a segment center ± axis * halfHeight. </summary>
        public static float BoxDistanceSq(float3 center, float3 axis, float halfHeight, float3 halfExtents)
        {
            if (halfHeight <= 0f)
            {
                return PointBoxDistanceSq(center, halfExtents);
            }

            // The squared distance is a convex quadratic between the points where the segment crosses a face plane,
            // so the minimum is found piece by piece.
            var times = new FixedList64Bytes<float>();
            times.Add(-halfHeight);
            times.Add(halfHeight);
            for (var i = 0; i < 3; i++)
            {
                if (axis[i] == 0f)
                {
                    continue;
                }

                AddCrossing(ref times, (halfExtents[i] - center[i]) / axis[i], halfHeight);
                AddCrossing(ref times, (-halfExtents[i] - center[i]) / axis[i], halfHeight);
            }

            for (var i = 1; i < times.Length; i++)
            {
                var time = times[i];
                var j = i - 1;
                while (j >= 0 && times[j] > time)
                {
                    times[j + 1] = times[j];
                    j--;
                }

                times[j + 1] = time;
            }

            var best = float.MaxValue;
            for (var i = 0; i + 1 < times.Length; i++)
            {
                var start = times[i];
                var end = times[i + 1];
                var middle = center + axis * ((start + end) * 0.5f);
                var outside = math.abs(middle) > halfExtents;
                var slope = math.select(float3.zero, axis, outside);
                var offset = math.select(float3.zero, center - math.sign(middle) * halfExtents, outside);
                var slopeSq = math.dot(slope, slope);
                var time = slopeSq > 0f ? math.clamp(-math.dot(slope, offset) / slopeSq, start, end) : start;
                best = math.min(best, PointBoxDistanceSq(center + axis * time, halfExtents));
            }

            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float PointBoxDistanceSq(float3 point, float3 halfExtents)
        {
            return math.lengthsq(math.max(math.abs(point) - halfExtents, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SphereEntry(float3 offset, float reach, float3 direction)
        {
            var c = math.lengthsq(offset) - reach * reach;
            if (c <= 0f)
            {
                return 0f;
            }

            var b = math.dot(offset, direction);
            var discriminant = b * b - c;
            if (b >= 0f || discriminant < 0f)
            {
                return float.PositiveInfinity;
            }

            return -b - math.sqrt(discriminant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddCrossing(ref FixedList64Bytes<float> times, float time, float halfHeight)
        {
            if (time > -halfHeight && time < halfHeight)
            {
                times.Add(time);
            }
        }
    }
}
