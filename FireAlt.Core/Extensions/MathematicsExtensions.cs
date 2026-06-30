using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.Core.Extensions
{
    public static class MathematicsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AsVector4(this float2 a)
        {
            return new Vector4(a.x, a.y, 0, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 AsVector4(this float4 a)
        {
            return new Vector4(a.x, a.y, a.z, a.w);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 AsFloat3(this float2 a, float z = 0f)
        {
            return new float3(a.x, a.y, z);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 ToBool2(this float2 value)
        {
            return new bool2(value.x == 1f, value.y == 1f);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ToFloat2(this bool2 value)
        {
            return new float2(value.x ? 1f : 0f, value.y ? 1f : 0f);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 xz(this Vector3 vector) => new(vector.x, vector.z);
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 oxy(this float2 a) => new(0, a.x, a.y);
                
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 xoy(this float2 a) => new(a.x, 0, a.y);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 xyo(this float2 a) => new(a.x, a.y, 0);

        /// <summary>
        /// Obtain 3-dimensional scale vector of the provided 4x4 transformation matrix, the components
        /// of which represent the lengths of the three orthonormal basis vectors forming the 3x3 rotational sub-matrix,
        /// respectively.
        /// </summary>
        /// <param name="matrix">The 4x4 transformation matrix.</param>
        /// <returns>The three scale components of the provided transformation matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DecomposeScale(this float4x4 matrix)
        {
            return new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
        }
        
        /// <summary>
        /// Works for negative scales
        /// </summary>
        /// <param name="m"></param>
        /// <param name="matrixRotation"></param>
        /// <returns></returns>
        public static float3 ExtractCorrectScale(this float4x4 m, quaternion matrixRotation)
        {
            var r = new float3x3(matrixRotation);

            var sx = math.dot(m.c0.xyz, r.c0);
            var sy = math.dot(m.c1.xyz, r.c1);
            var sz = math.dot(m.c2.xyz, r.c2);

            return new float3(sx, sy, sz);
        }
    }
}