#if BL_QUILL
using FireAlt.Core.Collections;
using FireAlt.Core.Utility;
using Unity.Assertions;
using Unity.Collections;
using Unity.Mathematics;

namespace FireAlt.Core.Quill
{
    public static class DrawerImpl
    {
        public static NativeArray<float3x3> SolidCircle(PooledNativeList<float3x3> pooledTriangles, float3 center, float radius, float3 up, int sideCount)
        {
            Assert.IsTrue(radius > 0f, "Radius must be greater than 0.");
            Assert.IsTrue(math.lengthsq(up) > 1e-6f, "Up vector must have a non-zero length.");

            var triangles = pooledTriangles.AsArray(sideCount);
            var upNormalized = math.normalize(up);
            var rotation = mathex.RotationFromUpToDirection(upNormalized, math.up());

            for (var side = 0; side < sideCount; side++)
            {
                var theta = (2f * math.PI * side) / sideCount;
                var nextTheta = (2f * math.PI * (side + 1)) / sideCount;

                var pointLocal = new float3(math.cos(theta) * radius, 0f, math.sin(theta) * radius);
                var nextPointLocal = new float3(math.cos(nextTheta) * radius, 0f, math.sin(nextTheta) * radius);

                var point = center + math.mul(rotation, pointLocal);
                var nextPoint = center + math.mul(rotation, nextPointLocal);

                triangles[side] = new float3x3(center, nextPoint, point);
            }

            return triangles;
        }

        public static NativeArray<float3x3> SolidPlane(PooledNativeList<float3x3> pooledTriangles, float3 center, float2 size, float3 up)
        {
            Assert.IsTrue(size.x > 0f, "Size X must be greater than 0.");
            Assert.IsTrue(size.y > 0f, "Size Y must be greater than 0.");
            Assert.IsTrue(math.lengthsq(up) > 1e-6f, "Up vector must have a non-zero length.");

            var triangles = pooledTriangles.AsArray(2);
            var upNormalized = math.normalize(up);
            var rotation = mathex.RotationFromUpToDirection(upNormalized, math.up());
            var halfSize = size * 0.5f;

            var p0 = center + math.mul(rotation, new float3(-halfSize.x, 0f, -halfSize.y));
            var p1 = center + math.mul(rotation, new float3(-halfSize.x, 0f, halfSize.y));
            var p2 = center + math.mul(rotation, new float3(halfSize.x, 0f, halfSize.y));
            var p3 = center + math.mul(rotation, new float3(halfSize.x, 0f, -halfSize.y));

            triangles[0] = new float3x3(p1, p2, p0);
            triangles[1] = new float3x3(p2, p3, p0);

            return triangles;
        }

        public static NativeArray<float3> Ellipse(PooledNativeList<float3> pooledLines, float3 center, float2 size, float3 up, int sideCount)
        {
            Assert.IsTrue(size.x > 0f, "Size X must be greater than 0.");
            Assert.IsTrue(size.y > 0f, "Size Y must be greater than 0.");
            Assert.IsTrue(sideCount >= 3, "SideCount must be greater than or equal to 3.");
            Assert.IsTrue(math.lengthsq(up) > 1e-6f, "Up vector must have a non-zero length.");

            var lines = pooledLines.AsArray(sideCount * 2);
            var upNormalized = math.normalize(up);
            var rotation = mathex.RotationFromUpToDirection(upNormalized, math.up());
            var extents = size * 0.5f;
            var previous = center + math.mul(rotation, new float3(extents.x, 0f, 0f));

            for (var side = 0; side < sideCount; side++)
            {
                var theta = (2f * math.PI * (side + 1)) / sideCount;
                var next = center + math.mul(rotation, new float3(math.cos(theta) * extents.x, 0f, math.sin(theta) * extents.y));
                var lineIndex = side * 2;
                lines[lineIndex] = previous;
                lines[lineIndex + 1] = next;
                previous = next;
            }

            return lines;
        }

        public static NativeArray<float3x3> SolidSphere(PooledNativeList<float3x3> pooledTriangles, float3 center, float radius, int sideCount)
        {
            Assert.IsTrue(radius > 0f, "Radius must be greater than 0.");
            Assert.IsTrue(sideCount >= 3f, "SideCount must be greater than 3.");
            
            var longitudeCount = math.max(3, sideCount);
            var latitudeBands = math.max(2, sideCount);
            var ringCount = latitudeBands - 1;
            var triangleCount = 2 * longitudeCount * ringCount;

            using var pooledRings = NativeListPool<float3>.Rent();
            var rings = pooledRings.AsArray(ringCount * longitudeCount);
            
            var triangles = pooledTriangles.AsArray(triangleCount);
            
            for (var ring = 0; ring < ringCount; ring++)
            {
                var phi = math.PI * (ring + 1) / latitudeBands;
                var y = math.cos(phi) * radius;
                var ringRadius = math.sin(phi) * radius;
                var ringOffset = ring * longitudeCount;

                for (var side = 0; side < longitudeCount; side++)
                {
                    var theta = (2f * math.PI * side) / longitudeCount;
                    var x = math.cos(theta) * ringRadius;
                    var z = math.sin(theta) * ringRadius;
                    rings[ringOffset + side] = center + new float3(x, y, z);
                }
            }

            var top = center + new float3(0f, radius, 0f);
            var bottom = center + new float3(0f, -radius, 0f);
            var triangleIndex = 0;

            for (var side = 0; side < longitudeCount; side++)
            {
                var next = (side + 1) % longitudeCount;
                triangles[triangleIndex++] = new float3x3(top, rings[next], rings[side]);
            }

            for (var ring = 0; ring < ringCount - 1; ring++)
            {
                var upperOffset = ring * longitudeCount;
                var lowerOffset = (ring + 1) * longitudeCount;

                for (var side = 0; side < longitudeCount; side++)
                {
                    var next = (side + 1) % longitudeCount;
                    var upperCurrent = rings[upperOffset + side];
                    var upperNext = rings[upperOffset + next];
                    var lowerCurrent = rings[lowerOffset + side];
                    var lowerNext = rings[lowerOffset + next];

                    triangles[triangleIndex++] = new float3x3(upperCurrent, upperNext, lowerCurrent);
                    triangles[triangleIndex++] = new float3x3(upperNext, lowerNext, lowerCurrent);
                }
            }

            var lastRingOffset = (ringCount - 1) * longitudeCount;
            for (var side = 0; side < longitudeCount; side++)
            {
                var next = (side + 1) % longitudeCount;
                var current = rings[lastRingOffset + side];
                var nextPoint = rings[lastRingOffset + next];
                triangles[triangleIndex++] = new float3x3(bottom, current, nextPoint);
            }

            return triangles;
        }
        
        public static void CapsuleFromPoints(float3 start, float3 end, float radius, out float3 center, out quaternion rotation, out float height)
        {
            var dir = end - start;
            var length = math.length(dir);

            center = (start + end) * 0.5f;

            height = length + 2f * radius;
            rotation = mathex.RotationFromUpToDirection(dir, math.up());
            
            if (length <= 1e-6f)
            {
                height = 2f * radius;
                rotation = quaternion.identity;
            }
        }
        
        public static void Square(in float3 position, in float2 size, in float3 up, out float3 p0, out float3 p1, out float3 p2, out float3 p3)
        {
            var upNormalized = math.normalizesafe(up, math.up());
            var rotation = mathex.RotationFromUpToDirection(upNormalized, math.up());
            var halfSize = size * 0.5f;
            
            p0 = position + math.mul(rotation, new float3(-halfSize.x, 0f, -halfSize.y));
            p1 = position + math.mul(rotation, new float3(-halfSize.x, 0f, halfSize.y));
            p2 = position + math.mul(rotation, new float3(halfSize.x, 0f, halfSize.y));
            p3 = position + math.mul(rotation, new float3(halfSize.x, 0f, -halfSize.y));
        }
        
        public static void Trajectory(NativeList<float3> points, float3 initialPos, float gravity, float initialVelocity,
            float angleDeg, float angleDivergence)
        {
            const int trajectoryPreviewSamples = 256;
            
            if (angleDivergence == 0f)
            {
                Trajectory(points, initialPos, gravity, initialVelocity, angleDeg);
            }
            else
            {
                for (var i = 0; i < trajectoryPreviewSamples; i++)
                {
                    var t = (float)i / (trajectoryPreviewSamples - 1);
                    var divergentAngleDeg = angleDeg + math.lerp(-angleDivergence, angleDivergence, t);

                    Trajectory(points, initialPos, gravity, initialVelocity, divergentAngleDeg);
                }
            }
        }
        
        private static void Trajectory(NativeList<float3> points, float3 initialPos, float gravity, float initialVelocity, float angleDeg)
        {
            const int trajectoryPreviewLinesCount = 64;
            
            var maxDistance = TrajectoryUtils.GetMaxTrajectoryDistance(initialPos.y, gravity, initialVelocity, angleDeg);
            if (math.abs(maxDistance) <= 0.0001f)
            {
                return;
            }
            TrajectoryUtils.EvaluateProjectileMotion(points, initialPos, gravity, initialVelocity, angleDeg, maxDistance, trajectoryPreviewLinesCount);
        }
    }
}
#endif
