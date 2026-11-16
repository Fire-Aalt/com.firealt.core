using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace FireAlt.Core
{
    public readonly struct FindNearestNInput
    {
        public readonly float3 QueryPos;
        public readonly float QueryRadius; 
        public readonly int N;
        public readonly bool Sort;

        public FindNearestNInput(float3 queryPos, float queryRadius, int n, bool sort = false)
        {
            QueryPos = queryPos;
            QueryRadius = queryRadius;
            N = n;
            Sort = sort;
        }
    }
    
    public readonly struct FindNearestNCollisionInput
    {
        public readonly CollisionWorld CollisionWorld;
        public readonly CollisionFilter CollisionFilter;

        public FindNearestNCollisionInput(CollisionWorld collisionWorld, CollisionFilter collisionFilter)
        {
            CollisionWorld = collisionWorld;
            CollisionFilter = collisionFilter;
        }
    }
    
    public static class SpatialMapExtensions
    {
        public static JobHandle ScheduleFindNearestNJob(this EntitySpatialMap spatialMap,
            ref NativeList<EntityWithDistance> results, in FindNearestNInput input, JobHandle dependency)
        {
            ClearAndResize(ref results, input.N);

            return new FindNearestNJob
            {
                QueryPos = new QueryPosition(input.QueryPos),
                QueryMaxRadiusSq = input.QueryRadius * input.QueryRadius,
                N = input.N,
                Sort = input.Sort,
                SpatialMap = spatialMap.AsReadOnly(),
                ClosestEntities = results,
            }.Schedule(dependency);
        }

        public static JobHandle ScheduleFindNearestNNoRayCollisionJob(this EntitySpatialMap spatialMap,
            ref NativeList<EntityWithDistance> results, in FindNearestNInput input,
            in FindNearestNCollisionInput collisionInput, JobHandle dependency)
        {
            ClearAndResize(ref results, input.N);
            
            return new FindNearestNJob
            {
                QueryPos = new QueryPosition(input.QueryPos),
                QueryMaxRadiusSq = input.QueryRadius * input.QueryRadius,
                N = input.N,
                Sort = input.Sort,
                SpatialMap = spatialMap.AsReadOnly(),
                ClosestEntities = results,
                CollisionWorld = collisionInput.CollisionWorld,
                CollisionFilter = collisionInput.CollisionFilter,
                CheckNoRayCollision = true,
            }.Schedule(dependency);
        }
        
        private static void ClearAndResize(ref NativeList<EntityWithDistance> results, int n)
        {
            results.Clear();
            if (results.Capacity < n)
            {
                results.Capacity = n;
            }
        }
        
        [BurstCompile]
        private struct FindNearestNJob : IJob
        {
            public QueryPosition QueryPos;
            public float QueryMaxRadiusSq;
            public int N;
            public bool Sort;

            public EntitySpatialMap.ReadOnly SpatialMap;
            public NativeList<EntityWithDistance> ClosestEntities;
            
            public bool CheckNoRayCollision;
            [NativeDisableContainerSafetyRestriction]
            public CollisionWorld CollisionWorld;
            public CollisionFilter CollisionFilter;
            
            private uint2 _searchCell;
            private IndexWithDistance _worstResult;
            private bool _anyCellConsideredInRing;
            private bool _anyCellCanBeReached;

            public void Execute()
            {
                _searchCell = SpatialMap.Quantized(QueryPos.SpatialPosition);

                // radius = 0 case: single cell
                TrySearchCell(_searchCell);
                _searchCell += new uint2(1, 0);
                
                for (uint radius = 1; radius <= ushort.MaxValue; ++radius)
                {
                    _anyCellCanBeReached = false;
                    _anyCellConsideredInRing = false;

                    var rightLength = radius * 2 - 1;
                    var bottomLeftLength = radius * 2;
                    var topLength = radius * 2 + 1;
                    
                    var v0 = _searchCell;
                    var v1 = v0 - new uint2(0, rightLength);
                    var v2 = v1 - new uint2(bottomLeftLength, 0);
                    var v3 = v2 + new uint2(0, bottomLeftLength);

                    for (uint i = 0; i < topLength; i++)
                    {
                        if (i < rightLength)
                        {
                            TrySearchCell(v0 - new uint2(0, i));
                        }
                        if (i < bottomLeftLength)
                        {
                            TrySearchCell(v1 - new uint2(i, 0));
                            TrySearchCell(v2 + new uint2(0, i));
                        }
                        TrySearchCell(v3 + new uint2(i, 0));
                    }

                    if (!_anyCellCanBeReached)
                    {
                        break;
                    }
                    if (ClosestEntities.Length == N && !_anyCellConsideredInRing)
                    {
                        // If we already have N and this whole ring had no cells that could beat worstDistSq,
                        // then no farther rings can either (cells only get farther), so break.
                        break;
                    }
                    if (ClosestEntities.Length == SpatialMap.Count)
                    {
                        break;
                    }
                    
                    _searchCell = v3 + new uint2(topLength, 0);
                }

                if (Sort)
                {
                    ClosestEntities.Sort(new EntityWithDistanceComparer());
                }
            }
            
            private void TrySearchCell(uint2 quantized)
            {
                var minCellDistSq = MinDistSqToCell(QueryPos.SpatialPosition, SpatialMap.QuantizedToCell(quantized));
                if (minCellDistSq > QueryMaxRadiusSq)
                {
                    return;
                }
                _anyCellCanBeReached = true;

                if (ClosestEntities.Length == N && minCellDistSq > _worstResult.Distance)
                {
                    return;
                }
                _anyCellConsideredInRing = true;

                var hash = SpatialMap.Hash(quantized);
                foreach (var index in SpatialMap.IndexMap.GetValuesForKey(hash))
                {
                    var targetPos = SpatialMap.Positions[index].Position;
                    var dsq = math.distancesq(targetPos, QueryPos.Position);

                    if (ClosestEntities.Length < N)
                    {
                        if (!NoRayCollisionCheck(targetPos)) continue;
                        
                        ClosestEntities.Add(new EntityWithDistance
                        {
                            Entity = SpatialMap.Entities[index],
                            Distance = dsq
                        });
                        if (ClosestEntities.Length == N)
                        {
                            UpdateWorstResult();
                        }
                    }
                    else if (dsq < _worstResult.Distance)
                    {
                        if (!NoRayCollisionCheck(targetPos)) continue;
                        
                        ClosestEntities[_worstResult.Index] = new EntityWithDistance
                        {
                            Entity = SpatialMap.Entities[index],
                            Distance = dsq
                        };
                        UpdateWorstResult();
                    }
                }
            }

            private bool NoRayCollisionCheck(float3 targetPos)
            {
                if (CheckNoRayCollision)
                {
                    var collisionRay = new RaycastInput
                    {
                        Start = QueryPos.Position,
                        End = targetPos,
                        Filter = CollisionFilter
                    };
                    if (CollisionWorld.CastRay(collisionRay))
                    {
                        return false;
                    }
                }
                return true;
            }

            private float MinDistSqToCell(float2 queryPos, int2 snappedCell)
            {
                var min = (float2)snappedCell * SpatialMap.QuantizeStep;
                var max = min + new float2(SpatialMap.QuantizeStep, SpatialMap.QuantizeStep);
                float2 closest;
                closest.x = math.clamp(queryPos.x, min.x, max.x);
                closest.y = math.clamp(queryPos.y, min.y, max.y);
                return math.distancesq(queryPos, closest);
            }
            
            private void UpdateWorstResult()
            {
                _worstResult = new IndexWithDistance { Distance = ClosestEntities[^1].Distance, Index = ClosestEntities.Length - 1 };
                for (int k = N - 2; k >= 0; --k)
                {
                    var closestDist = ClosestEntities[k];
                    if (closestDist.Distance > _worstResult.Distance)
                    {
                        _worstResult = new IndexWithDistance { Distance = closestDist.Distance, Index = k };
                    }
                }
            }
        }
    }
}