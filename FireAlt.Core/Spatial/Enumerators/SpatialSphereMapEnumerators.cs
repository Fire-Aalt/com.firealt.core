using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct SpatialSphereHit
    {
        /// <summary> Index into the arrays the map was built from. </summary>
        public int Index;
        public float3 Center;
        public float Radius;
    }

    public struct SpatialSphereEntity
    {
        public Entity Entity;
        public int Index;
        public float3 Center;
        public float Radius;

        public void Deconstruct(out Entity entity, out float3 center)
        {
            entity = Entity;
            center = Center;
        }
    }

    public struct SpatialSphereCastHit
    {
        public int Index;
        public float Distance;
        public float3 Position;
        public float3 Normal;
    }

    /// <summary> Lets a cast skip spheres, for example the caster itself. </summary>
    public interface ISpatialSphereFilter
    {
        bool Accept(int index);
    }

    public interface ISpatialSphereEnumerator
    {
        SpatialSphereHit Current { get; }

        bool MoveNext();
    }

    public static class SpatialSphereMapEnumerators
    {
        public static AabbEnumerator SpheresInAabb(this in SpatialSphereMap.ReadOnly map, AABB aabb)
        {
            return new AabbEnumerator(map, aabb.Min, aabb.Max);
        }

        public static SphereEnumerator SpheresInSphere(this in SpatialSphereMap.ReadOnly map, float3 center,
            float radius)
        {
            return new SphereEnumerator(new AabbEnumerator(map, center - radius, center + radius), center, radius);
        }

        public static BoxEnumerator SpheresInBox(this in SpatialSphereMap.ReadOnly map, float3 center,
            quaternion rotation, float3 halfExtents)
        {
            var axes = new float3x3(rotation);
            var extents = math.abs(axes.c0) * halfExtents.x + math.abs(axes.c1) * halfExtents.y
                + math.abs(axes.c2) * halfExtents.z;
            return new BoxEnumerator(new AabbEnumerator(map, center - extents, center + extents), center,
                math.inverse(rotation), halfExtents);
        }

        public static EntityEnumerator<AabbEnumerator> EntitiesInAabb(this in EntitySpatialSphereMap.ReadOnly map,
            AABB aabb)
        {
            return new EntityEnumerator<AabbEnumerator>(map.Entities.AsReadOnly(), map.Spatial.SpheresInAabb(aabb));
        }

        public static EntityEnumerator<SphereEnumerator> EntitiesInSphere(
            this in EntitySpatialSphereMap.ReadOnly map, float3 center, float radius)
        {
            return new EntityEnumerator<SphereEnumerator>(map.Entities.AsReadOnly(),
                map.Spatial.SpheresInSphere(center, radius));
        }

        public static EntityEnumerator<BoxEnumerator> EntitiesInBox(this in EntitySpatialSphereMap.ReadOnly map,
            float3 center, quaternion rotation, float3 halfExtents)
        {
            return new EntityEnumerator<BoxEnumerator>(map.Entities.AsReadOnly(),
                map.Spatial.SpheresInBox(center, rotation, halfExtents));
        }

        /// <summary> Closest sphere hit by a sphere swept along a normalized direction. </summary>
        public static bool SphereCast(this in SpatialSphereMap.ReadOnly map, float3 origin, float radius,
            float3 direction, float maxDistance, out SpatialSphereCastHit hit)
        {
            var filter = new IgnoreIndexFilter { Index = -1 };
            return map.SphereCast(origin, radius, direction, maxDistance, ref filter, out hit);
        }

        public static bool SphereCast(this in SpatialSphereMap.ReadOnly map, float3 origin, float radius,
            float3 direction, float maxDistance, int ignoreIndex, out SpatialSphereCastHit hit)
        {
            var filter = new IgnoreIndexFilter { Index = ignoreIndex };
            return map.SphereCast(origin, radius, direction, maxDistance, ref filter, out hit);
        }

        public static bool SphereCast(this in EntitySpatialSphereMap.ReadOnly map, float3 origin, float radius,
            float3 direction, float maxDistance, Entity ignore, out SpatialSphereCastHit hit)
        {
            var filter = new IgnoreEntityFilter { Entities = map.Entities.AsReadOnly(), Entity = ignore };
            return map.Spatial.SphereCast(origin, radius, direction, maxDistance, ref filter, out hit);
        }

        public static bool SphereCast<T>(this in SpatialSphereMap.ReadOnly map, float3 origin, float radius,
            float3 direction, float maxDistance, ref T filter, out SpatialSphereCastHit hit)
            where T : struct, ISpatialSphereFilter
        {
            var end = origin + direction * maxDistance;
            var best = float.MaxValue;
            SpatialSphereHit bestSphere = default;
            foreach (var sphere in new AabbEnumerator(map, math.min(origin, end) - radius,
                         math.max(origin, end) + radius))
            {
                var offset = origin - sphere.Center;
                var reach = radius + sphere.Radius;
                var c = math.lengthsq(offset) - reach * reach;
                var distance = 0f;
                if (c > 0f)
                {
                    var b = math.dot(offset, direction);
                    var discriminant = b * b - c;
                    if (b >= 0f || discriminant < 0f)
                    {
                        continue;
                    }

                    distance = -b - math.sqrt(discriminant);
                    if (distance > maxDistance)
                    {
                        continue;
                    }
                }

                // Ties go to the lower index so the result does not depend on how the map was laid out.
                if ((distance < best || (distance == best && sphere.Index < bestSphere.Index))
                    && filter.Accept(sphere.Index))
                {
                    best = distance;
                    bestSphere = sphere;
                }
            }

            if (best == float.MaxValue)
            {
                hit = default;
                return false;
            }

            var normal = math.normalizesafe(origin + direction * best - bestSphere.Center, -direction);
            hit = new SpatialSphereCastHit
            {
                Index = bestSphere.Index,
                Distance = best,
                Normal = normal,
                Position = bestSphere.Center + normal * bestSphere.Radius,
            };
            return true;
        }

        private struct IgnoreIndexFilter : ISpatialSphereFilter
        {
            public int Index;

            public bool Accept(int index) => index != Index;
        }

        private struct IgnoreEntityFilter : ISpatialSphereFilter
        {
            public NativeArray<Entity>.ReadOnly Entities;
            public Entity Entity;

            public bool Accept(int index) => Entities[index] != Entity;
        }

        /// <summary> Spheres overlapping an axis-aligned box. </summary>
        public struct AabbEnumerator : ISpatialSphereEnumerator
        {
            private NativeArray<int>.ReadOnly _starts;
            private NativeArray<uint>.ReadOnly _keys;
            private NativeArray<float4>.ReadOnly _spheres;
            private NativeArray<int>.ReadOnly _indices;
            private NativeArray<SpatialSphereMapLevel>.ReadOnly _levels;
            private readonly SpatialSphereMapHeader _header;
            private readonly float3 _min;
            private readonly float3 _max;

            private SpatialSphereHit _current;
            private int _level;
            private int _x;
            private int _y;
            private int _x0;
            private int _x1;
            private int _y1;
            private int _slot;
            private int _slotEnd;
            private uint _key;
            private bool _loose;

            internal AabbEnumerator(in SpatialSphereMap.ReadOnly map, float3 min, float3 max)
            {
                _starts = map.Starts.AsReadOnly();
                _keys = map.Keys.AsReadOnly();
                _spheres = map.Spheres.AsReadOnly();
                _indices = map.Indices.AsReadOnly();
                _levels = map.Levels.AsReadOnly();
                _header = map.Header.Value;
                _min = min;
                _max = max;
                _current = default;
                _level = -1;
                _x = 0;
                _y = 0;
                _x0 = 0;
                _x1 = 0;
                _y1 = 0;
                _slot = 0;
                _slotEnd = 0;
                _key = 0;
                _loose = false;
            }

            public SpatialSphereHit Current => _current;

            public AabbEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                while (true)
                {
                    while (_slot < _slotEnd)
                    {
                        var slot = _slot++;
                        if (!_loose && _keys[slot] != _key)
                        {
                            continue;
                        }

                        var sphere = _spheres[slot];
                        var outside = math.max(math.max(_min - sphere.xyz, sphere.xyz - _max), 0f);
                        if (math.lengthsq(outside) > sphere.w * sphere.w)
                        {
                            continue;
                        }

                        _current = new SpatialSphereHit
                        {
                            Index = _indices[slot],
                            Center = sphere.xyz,
                            Radius = sphere.w,
                        };
                        return true;
                    }

                    if (!NextRange())
                    {
                        return false;
                    }
                }
            }

            private bool NextRange()
            {
                if (_loose)
                {
                    return false;
                }

                if (_level >= 0)
                {
                    if (++_x <= _x1)
                    {
                        SetCell();
                        return true;
                    }

                    if (++_y <= _y1)
                    {
                        _x = _x0;
                        SetCell();
                        return true;
                    }
                }

                while (++_level < _levels.Length)
                {
                    var level = _levels[_level];
                    var maxCell = (float2)level.MaxCell;
                    var low = (_min.xz - level.MaxRadius - _header.Origin) * level.InvCellSize;
                    var high = (_max.xz + level.MaxRadius - _header.Origin) * level.InvCellSize;
                    if (math.any(high < 0f) || math.any(low >= maxCell + 1f))
                    {
                        continue;
                    }

                    var first = (int2)math.floor(math.clamp(low, 0f, maxCell));
                    var last = (int2)math.floor(math.clamp(high, 0f, maxCell));
                    _x0 = first.x;
                    _x1 = last.x;
                    _y1 = last.y;
                    _x = first.x;
                    _y = first.y;
                    SetCell();
                    return true;
                }

                _loose = true;
                _slot = _header.GridCount;
                _slotEnd = _header.Count;
                return _slot < _slotEnd;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetCell()
            {
                var cell = new int2(_x, _y);
                var bucket = SpatialSphereMap.Bucket(_level, cell, _header.BucketMask);
                _key = SpatialSphereMap.Key(_level, cell);
                _slot = _starts[bucket];
                _slotEnd = _starts[bucket + 1];
            }
        }

        /// <summary> Spheres overlapping a sphere. </summary>
        public struct SphereEnumerator : ISpatialSphereEnumerator
        {
            private AabbEnumerator _spheres;
            private readonly float3 _center;
            private readonly float _radius;

            internal SphereEnumerator(AabbEnumerator spheres, float3 center, float radius)
            {
                _spheres = spheres;
                _center = center;
                _radius = radius;
            }

            public SpatialSphereHit Current => _spheres.Current;

            public SphereEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                while (_spheres.MoveNext())
                {
                    var sphere = _spheres.Current;
                    var reach = _radius + sphere.Radius;
                    if (math.distancesq(sphere.Center, _center) <= reach * reach)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary> Spheres overlapping an oriented box. </summary>
        public struct BoxEnumerator : ISpatialSphereEnumerator
        {
            private AabbEnumerator _spheres;
            private readonly float3 _center;
            private readonly quaternion _inverseRotation;
            private readonly float3 _halfExtents;

            internal BoxEnumerator(AabbEnumerator spheres, float3 center, quaternion inverseRotation,
                float3 halfExtents)
            {
                _spheres = spheres;
                _center = center;
                _inverseRotation = inverseRotation;
                _halfExtents = halfExtents;
            }

            public SpatialSphereHit Current => _spheres.Current;

            public BoxEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                while (_spheres.MoveNext())
                {
                    var sphere = _spheres.Current;
                    var local = math.rotate(_inverseRotation, sphere.Center - _center);
                    var outside = math.max(math.abs(local) - _halfExtents, 0f);
                    if (math.lengthsq(outside) <= sphere.Radius * sphere.Radius)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public struct EntityEnumerator<T>
            where T : struct, ISpatialSphereEnumerator
        {
            private NativeArray<Entity>.ReadOnly _entities;
            private T _spheres;

            internal EntityEnumerator(NativeArray<Entity>.ReadOnly entities, T spheres)
            {
                _entities = entities;
                _spheres = spheres;
            }

            public SpatialSphereEntity Current
            {
                get
                {
                    var sphere = _spheres.Current;
                    return new SpatialSphereEntity
                    {
                        Entity = _entities[sphere.Index],
                        Index = sphere.Index,
                        Center = sphere.Center,
                        Radius = sphere.Radius,
                    };
                }
            }

            public EntityEnumerator<T> GetEnumerator() => this;

            public bool MoveNext() => _spheres.MoveNext();
        }
    }
}
