#if BL_QUILL
using BovineLabs.Quill;
using FireAlt.Core.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace FireAlt.Core.Quill
{
    [BurstCompile]
    public static class GlobalDrawEx
    {
        // Wire (non-solid)
        
        public static void SquareXZ(float3 position, float2 size, Color color, float duration = 0f)
        {
            DrawerImpl.Square(position, size, math.up(), out var p0, out var p1, out var p2, out var p3);
            GlobalDraw.Quad(p0, p1, p2, p3, color, duration);
        }

        public static void EllipseXZ(float3 center, float2 size, Color color, float duration = 0f, int sideCount = 32)
        {
            Ellipse(center, size, math.up(), sideCount, color, duration);
        }

        public static void Ellipse(float3 center, float2 size, float3 up, int sideCount, Color color, float duration = 0f)
        {
            sideCount = math.max(3, sideCount);
            using var pooledLines = NativeListPool<float3>.Rent(sideCount * 2);

            var lines = DrawerImpl.Ellipse(pooledLines, center, size, up, sideCount);
            GlobalDraw.Lines(lines, color, duration);
        }
        
        public static void CapsuleFromPoints(float3 start, float3 end, float radius, int sideCount, Color color, float duration = 0f)
        {
            DrawerImpl.CapsuleFromPoints(start, end, radius, out var center, out var rotation, out var height);
            GlobalDraw.Capsule(center, rotation, height, radius, sideCount, color, duration);
        }
        
        // Solid
        
        public static void SolidSquareXZ(float3 position, float2 size, Color color, float duration = 0f)
        {
            DrawerImpl.Square(position, size, math.up(), out var p0, out var p1, out var p2, out var p3);
            GlobalDraw.SolidQuad(p0, p1, p2, p3, color, duration);
        }
        
        public static void SolidSquareXY(float3 position, float2 size, Color color, float duration = 0f)
        {
            DrawerImpl.Square(position, size, new float3(0f, 0f, -1f), out var p0, out var p1, out var p2, out var p3);
            GlobalDraw.SolidQuad(p0, p1, p2, p3, color, duration);
        }

        public static void SolidCircle(float3 center, float radius, float3 up, int sideCount, Color color, float duration = 0f)
        {
            using var pooledTriangles = NativeListPool<float3x3>.Rent();

            var triangles = DrawerImpl.SolidCircle(pooledTriangles, center, radius, up, sideCount);
            GlobalDraw.SolidTriangles(triangles, color, duration);
        }

        public static void SolidPlane(float3 center, float2 size, float3 up, Color color, float duration = 0f)
        {
            using var pooledTriangles = NativeListPool<float3x3>.Rent();

            var triangles = DrawerImpl.SolidPlane(pooledTriangles, center, size, up);
            GlobalDraw.SolidTriangles(triangles, color, duration);
        }

        public static void SolidSphere(float3 center, float radius, int sideCount, Color color, float duration = 0f)
        {
            using var pooledTriangles = NativeListPool<float3x3>.Rent();
            
            var triangles = DrawerImpl.SolidSphere(pooledTriangles, center, radius, sideCount);
            GlobalDraw.SolidTriangles(triangles, color, duration);
        }
        
        // Path

        public static void Trajectory(Transform origin, float defaultGravity, PhysicsBodyAuthoring physicsBodyAuthoring, 
            float initialVelocity, float angleDeg, float angleDivergence, Color color)
        {
            var gravity = defaultGravity;
            if (physicsBodyAuthoring != null)
            {
                gravity *= physicsBodyAuthoring.GravityFactor;
            }
            
            Trajectory(origin.position, gravity, initialVelocity, angleDeg, angleDivergence, color);
        }
        
        [BurstCompile]
        public static void Trajectory(in float3 initialPos, float gravity, float initialVelocity,
            float angleDeg, float angleDivergence, in Color color)
        {
            using var pooledPoints = NativeListPool<float3>.Rent();
            
            DrawerImpl.Trajectory(pooledPoints.List, initialPos, gravity, initialVelocity, angleDeg, angleDivergence);
            GlobalDraw.Lines(pooledPoints.List.AsArray(), color);
        }
    }
}
#endif
