using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace FireAlt.Core.Utility
{
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "matching mathematics package")]
    [SuppressMessage("ReSharper", "SA1300", Justification = "matching mathematics package")]
    [SuppressMessage("ReSharper", "IdentifierTypo", Justification = "lower case causes issues")]
    public static class mathex
    {
        public static quaternion FromToRotation(quaternion a, quaternion b)
        {
            // a = normalize(a);
            // b = normalize(b);

            // Delta that maps a -> b under Unity's multiplication convention:
            // b = delta * a  => delta = b * inverse(a)
            var delta = mul(b, inverse(a));
            return normalize(delta);
        }
        
        public static float3 ForwardXZFromLocalToWorld(float4x4 localToWorld)
        {
            // Use right (local +X) projected to XZ
            var right = new float3(localToWorld.c0.x, localToWorld.c0.y, localToWorld.c0.z);
            var dir = new float3(right.x, 0f, right.z);
            var len = length(dir);
            if (len > EPSILON) return dir / len;
            
            return new float3(0f, 0f, 1f);
        }
        
        public static float AngleBetweenDegrees(float2 a, float2 b)
        {
            var la = length(a);
            var lb = length(b);
            if (la < EPSILON || lb < EPSILON) return 0f;

            var d = dot(a, b) / (la * lb);
            d = clamp(d, -1f, 1f);
            return degrees(acos(d));
        }
        
        public static float3 ForwardXZ(quaternion q)
        {
            var fwd = mul(q, new float3(1f, 0f, 0f));
            fwd.y = 0f;
            return normalizesafe(fwd);
        }
        
        public static float3 DirectionFromUpAngle(float2 directionXZ, float upAngleRad)
        {
            var len = sqrt(directionXZ.x * directionXZ.x + directionXZ.y * directionXZ.y);

            if (len > 0f)
            {
                var ux = directionXZ.x / len;
                var uz = directionXZ.y / len;

                var c = cos(upAngleRad);
                var s = sin(upAngleRad);

                return new float3(ux * c, s, uz * c);
            }
            return new float3(0f, upAngleRad < 0 ? 1f : -1f, 0f);
        }
        
        /// <summary>
        /// Starts at (1, 0), goes anticlockwise
        /// </summary>
        public static float AngleDegrees(float2 v)
        {
            return degrees(atan2(v.y, v.x));
        }
        
        public static LocalTransform CombineLocalTransforms(LocalTransform root, LocalTransform child)
        {
            return new LocalTransform
            {
                Position = root.Position + child.Position,
                Rotation = mul(root.Rotation, child.Rotation),
                Scale = root.Scale * child.Scale
            };
        }
        
        public static quaternion RotationFromUpToDirection(float3 dir, float3 up)
        {
            var len = length(dir);
            if (len <= 1e-6f) return Unity.Mathematics.quaternion.identity;

            var b = dir / len;
            var a = up;
            var dot = math.dot(a, b);

            // If nearly equal, return identity
            if (dot > 0.999999f)
            {
                return Unity.Mathematics.quaternion.identity;
            }

            // If nearly opposite, rotate 180 degrees around any axis perpendicular to 'a'
            if (dot < -0.999999f)
            {
                var axis = cross(a, new float3(1f, 0f, 0f));
                if (lengthsq(axis) < 1e-6f)
                {
                    axis = cross(a, new float3(0f, 0f, 1f));
                }
                axis = normalize(axis);
                return Unity.Mathematics.quaternion.AxisAngle(axis, PI); // 180 degrees
            }

            // General case: quaternion that rotates a -> b
            var v = cross(a, b);
            var s = sqrt((1f + dot) * 2f);
            var invs = 1f / s;
            var q = new float4(v * invs, s * 0.5f); // (x,y,z,w)
            return new quaternion(q);
        }
    }
}