#if BL_QUILL
using BovineLabs.Quill;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.Core
{
    public static class SpatialMapDrawExtensions
    {
        public static JobHandle ScheduleDrawCellsJob(this SpatialMap<SpatialPosition> spatialMap, in Drawer drawer,
            int gridSize, Color color, JobHandle dependency)
        {
            var readOnlyMap = spatialMap.AsReadOnly();
            var viewCenterXZ = readOnlyMap.CellToWorld(readOnlyMap.OriginCell);
            
            return new DrawCellsJob
            {
                Drawer = drawer,
                ViewCenter = new float3(viewCenterXZ.x, 0f, viewCenterXZ.y),
                GridSize = gridSize,
                QuantizeStep = readOnlyMap.QuantizeStep,
                Color = color
            }.Schedule(dependency);
        }
        
        [BurstCompile]
        private struct DrawCellsJob : IJob
        {
            public Drawer Drawer;

            public int GridSize;
            public float QuantizeStep;
            public float3 ViewCenter;

            public Color Color;
            
            public void Execute()
            {
                var normalizedSize = (int)(GridSize / QuantizeStep);
                
                for (int x = -normalizedSize; x < normalizedSize + 1; x++)
                {
                    var a = new float3(ViewCenter.x + x * QuantizeStep, 0f, ViewCenter.z - normalizedSize * QuantizeStep);
                    var b = new float3(ViewCenter.x + x * QuantizeStep, 0f, ViewCenter.z + normalizedSize * QuantizeStep);
                    
                    Drawer.Line(a, b, Color);
                }
                
                for (int z = -normalizedSize; z < normalizedSize + 1; z++)
                {
                    var a = new float3(ViewCenter.x - normalizedSize * QuantizeStep, 0f, ViewCenter.z + z * QuantizeStep);
                    var b = new float3(ViewCenter.x + normalizedSize * QuantizeStep, 0f, ViewCenter.z + z * QuantizeStep);

                    Drawer.Line(a, b, Color);
                }
            }
        }
    }
}
#endif
