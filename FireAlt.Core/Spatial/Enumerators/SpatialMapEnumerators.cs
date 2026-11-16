using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public static class SpatialMapEnumerators
    {
        public static IndexEnumerator<PositionEnumerators.AABBPositionEnumerator> IndicesInAabbXZ(this SpatialMap.ReadOnly spatialMap, AABB aabb)
        {
            var posEnumerator = spatialMap.Config.PositionsInAabbXZ(aabb);
            return new IndexEnumerator<PositionEnumerators.AABBPositionEnumerator>(spatialMap.IndexMap, posEnumerator);
        }
        
        public static IndexEnumerator<PositionEnumerators.CirclePositionEnumerator> IndicesInCircleXZ(this SpatialMap.ReadOnly spatialMap, float3 center, float radius)
        {
            var posEnumerator = spatialMap.Config.PositionsInCircleXZ(center, radius);
            return new IndexEnumerator<PositionEnumerators.CirclePositionEnumerator>(spatialMap.IndexMap, posEnumerator);
        }
        
        public static EntityEnumerator<IndexEnumerator<PositionEnumerators.AABBPositionEnumerator>> EntitiesInAabbXZ(this EntitySpatialMap.ReadOnly spatialMap, AABB aabb)
        {
            var indexEnumerator = spatialMap.Spatial.IndicesInAabbXZ(aabb);
            return new EntityEnumerator<IndexEnumerator<PositionEnumerators.AABBPositionEnumerator>>(spatialMap.Entities, spatialMap.Positions.AsArray(), indexEnumerator);
        }
        
        public static EntityEnumerator<IndexEnumerator<PositionEnumerators.CirclePositionEnumerator>> EntitiesInCircleXZ(this EntitySpatialMap.ReadOnly spatialMap, float3 center, float radius)
        {
            var indexEnumerator = spatialMap.Spatial.IndicesInCircleXZ(center, radius);
            return new EntityEnumerator<IndexEnumerator<PositionEnumerators.CirclePositionEnumerator>>(spatialMap.Entities, spatialMap.Positions.AsArray(), indexEnumerator);
        }
        
        public struct IndexEnumerator<T> : IEnumerator<int> 
            where T : unmanaged, IEnumerator<uint2>
        {
            private readonly NativeParallelMultiHashMap<uint, int> _indexMap;
            private T _posEnumerator;
            private NativeParallelMultiHashMap<uint, int>.Enumerator _indexEnumerator;
                
            private int _current;
                
            public int Current => _current;

            object IEnumerator.Current => Current;

            public IndexEnumerator(NativeParallelMultiHashMap<uint, int> indexMap, T posEnumerator)
            {
                _indexMap = indexMap;
                _posEnumerator = posEnumerator;
                _indexEnumerator = default;
                _current = -1;

                Reset();
            }

            public IndexEnumerator<T> GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_indexEnumerator.MoveNext())
                {
                    _current = _indexEnumerator.Current;
                    return true;
                }

                while (_posEnumerator.MoveNext())
                {
                    var hash = SpatialMap.Hash(_posEnumerator.Current);
                    _indexEnumerator = _indexMap.GetValuesForKey(hash);
                        
                    if (_indexEnumerator.MoveNext())
                    {
                        _current = _indexEnumerator.Current;
                        return true;
                    }
                }
                    
                return false;
            }

            public void Reset()
            {
                _current = -1;
                _posEnumerator.Reset();
                _indexEnumerator = default;
                    
                if (_posEnumerator.MoveNext())
                {
                    _indexEnumerator = _indexMap.GetValuesForKey(SpatialMap.Hash(_posEnumerator.Current));
                }
            }

            public void Dispose()
            {
            }
        }
        
        public struct EntityEnumerator<T> : IEnumerator<(Entity, float3)> 
            where T : unmanaged, IEnumerator<int>
        {
            private readonly NativeList<Entity> _entities;
            private readonly NativeArray<SpatialPosition> _positions;
            private T _indexEnumerator;
                
            private (Entity, float3) _current;
            
            public (Entity, float3) Current => _current;

            object IEnumerator.Current => Current;

            public EntityEnumerator(NativeList<Entity> entities, NativeArray<SpatialPosition> positions, T indexEnumerator)
            {
                _entities = entities;
                _positions = positions;
                _indexEnumerator = indexEnumerator;
                _current = default;

                Reset();
            }

            public EntityEnumerator<T> GetEnumerator() => this;

            public bool MoveNext()
            {
                if (_indexEnumerator.MoveNext())
                {
                    var index = _indexEnumerator.Current;
                    _current = (_entities[index], _positions[index].Position);
                    return true;
                }
                
                return false;
            }

            public void Reset()
            {
                _indexEnumerator.Reset();
                _current = (Entity.Null, default);
            }

            public void Dispose()
            {
            }
        }
    }
}