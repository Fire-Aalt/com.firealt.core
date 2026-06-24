#if BL_QUILL
using BovineLabs.Quill;
using FireAlt.Core.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.Core.Quill
{
    public static class DrawerExtensions
    {
        // Wire (non-solid)
        
        public static void SquareXZ(this Drawer drawer, float3 position, float2 size, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            DrawerImpl.Square(position, size, math.up(), out var p0, out var p1, out var p2, out var p3);
            drawer.Quad(p0, p1, p2, p3, color, duration);
        }

        public static void Ellipse(this Drawer drawer, float3 center, float2 size, float3 up, int sideCount, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            sideCount = math.max(3, sideCount);
            using var pooledLines = NativeListPool<float3>.Rent(sideCount * 2);

            var lines = DrawerImpl.Ellipse(pooledLines, center, size, up, sideCount);
            drawer.Lines(lines, color, duration);
        }

        public static void EllipseXZ(this Drawer drawer, float3 center, float2 size, Color color, float duration = 0f, int sideCount = 32)
        {
            Ellipse(drawer, center, size, math.up(), sideCount, color, duration);
        }
        
        public static void CapsuleFromPoints(this Drawer drawer, float3 start, float3 end, float radius, int sideCount, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            DrawerImpl.CapsuleFromPoints(start, end, radius, out var center, out var rotation, out var height);
            drawer.Capsule(center, rotation, height, radius, sideCount, color, duration);
        }
        
        // Solid
        
        public static void SolidSquareXZ(this Drawer drawer, float3 position, float2 size, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            DrawerImpl.Square(position, size, math.up(), out var p0, out var p1, out var p2, out var p3);
            drawer.SolidQuad(p0, p1, p2, p3, color, duration);
        }
        
        public static void SolidRectangleXY(this Drawer drawer, float3 position, float2 size, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            DrawerImpl.Square(position, size, new float3(0f, 0f, -1f), out var p0, out var p1, out var p2, out var p3);
            drawer.SolidQuad(p0, p1, p2, p3, color, duration);
        }

        public static void SolidCircle(this Drawer drawer, float3 center, float radius, float3 up, int sideCount, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            using var pooledTriangles = NativeListPool<float3x3>.Rent();

            var triangles = DrawerImpl.SolidCircle(pooledTriangles, center, radius, up, sideCount);
            drawer.SolidTriangles(triangles, color, duration);
        }

        public static void SolidPlane(this Drawer drawer, float3 center, float2 size, float3 up, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            using var pooledTriangles = NativeListPool<float3x3>.Rent();

            var triangles = DrawerImpl.SolidPlane(pooledTriangles, center, size, up);
            drawer.SolidTriangles(triangles, color, duration);
        }

        public static void SolidSphere(this Drawer drawer, float3 center, float radius, int sideCount, Color color, float duration = 0f)
        {
            if (!drawer.IsEnabled) return;
            using var pooledTriangles = NativeListPool<float3x3>.Rent();
            
            var triangles = DrawerImpl.SolidSphere(pooledTriangles, center, radius, sideCount);
            drawer.SolidTriangles(triangles, color, duration);
        }
        
        // Path
        
        public static void Trajectory(this Drawer drawer, float3 initialPos, float gravity, float initialVelocity,
            float angleDeg, float angleDivergence, Color color)
        {
            if (!drawer.IsEnabled) return;
            using var pooledPoints = NativeListPool<float3>.Rent();
            
            DrawerImpl.Trajectory(pooledPoints.List, initialPos, gravity, initialVelocity, angleDeg, angleDivergence);
            drawer.Lines(pooledPoints.List.AsArray(), color);
        }
    }
}
#endif
