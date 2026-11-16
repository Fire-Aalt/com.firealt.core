using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public static class PositionEnumerators
    {
        public static AABBPositionEnumerator PositionsInAabbXZ(this SpatialMapConfig config, AABB aabb)
        {
            var min = config.Quantized(aabb.Center.xz - aabb.Extents.xz);
            var max = config.Quantized(aabb.Center.xz + aabb.Extents.xz);
            
            return new AABBPositionEnumerator(min, max);
        }
        
        public static CirclePositionEnumerator PositionsInCircleXZ(this SpatialMapConfig config, float3 center, float radius)
        {
            var centerCell = (center.xz + SpatialMap.HalfSize) / config.QuantizeStep - config.OriginCell;
            var radiusInCells = math.max(0f, radius) / config.QuantizeStep;
            return new CirclePositionEnumerator(centerCell, radiusInCells);
        }
        
        public struct AABBPositionEnumerator : IEnumerator<uint2>
        {
            private readonly uint2 _min;
            private readonly uint2 _max;
                
            private uint2 _current;

            public uint2 Current => _current;

            object IEnumerator.Current => Current;

            public AABBPositionEnumerator(uint2 min, uint2 max)
            {
                _current = min;
                _min = min;
                _max = max;
                Reset();
            }

            public AABBPositionEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                _current.x++;
                if (_current.x > _max.x)
                {
                    _current.x = _min.x;
                    _current.y++;
                    
                    if (_current.y > _max.y)
                    {
                        return false;
                    }
                }

                return true;
            }

            public void Reset()
            {
                _current = _min;
                _current.x--;
            }

            public void Dispose()
            {
            }
        }

        public struct CirclePositionEnumerator : IEnumerator<uint2>
        {
            private readonly float2 _center;
            private readonly float _radiusSq;
            private readonly int _minY;
            private readonly int _maxY;

            private int _nextRowY;
            private int _rowY;
            private int _rowMaxX;
            private int _nextX;
            private uint2 _current;

            public uint2 Current => _current;

            object IEnumerator.Current => Current;

            public CirclePositionEnumerator(float2 center, float radius)
            {
                _center = center;
                var clampedRadius = math.max(0f, radius);
                _radiusSq = clampedRadius * clampedRadius;

                var rawMinY = (int)math.ceil(_center.y - clampedRadius - 1f);
                var rawMaxY = (int)math.floor(_center.y + clampedRadius);
                if (rawMaxY < 0 || rawMinY > SpatialMap.Size - 1)
                {
                    _minY = 1;
                    _maxY = 0;
                }
                else
                {
                    _minY = math.max(0, rawMinY);
                    _maxY = math.min(SpatialMap.Size - 1, rawMaxY);
                }

                _nextRowY = 0;
                _rowY = 0;
                _rowMaxX = 0;
                _nextX = 1;
                _current = default;
                Reset();
            }

            public CirclePositionEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                while (true)
                {
                    if (_nextX <= _rowMaxX)
                    {
                        _current = new uint2((uint)_nextX, (uint)_rowY);
                        _nextX++;
                        return true;
                    }

                    if (_nextRowY > _maxY)
                    {
                        return false;
                    }

                    _rowY = _nextRowY;
                    _nextRowY++;

                    var rowMinY = (float)_rowY;
                    var rowMaxY = rowMinY + 1f;
                    var dy = 0f;
                    if (_center.y < rowMinY)
                    {
                        dy = rowMinY - _center.y;
                    }
                    else if (_center.y > rowMaxY)
                    {
                        dy = _center.y - rowMaxY;
                    }

                    var maxDx = math.sqrt(math.max(0f, _radiusSq - (dy * dy)));
                    var xMin = (int)math.ceil(_center.x - maxDx - 1f);
                    var xMax = (int)math.floor(_center.x + maxDx);

                    if (xMax < 0 || xMin > SpatialMap.Size - 1)
                    {
                        continue;
                    }

                    _nextX = math.max(0, xMin);
                    _rowMaxX = math.min(SpatialMap.Size - 1, xMax);
                }
            }

            public void Reset()
            {
                _nextRowY = _minY;
                _rowY = _nextRowY;
                _rowMaxX = -1;
                _nextX = 0;
                _current = default;
            }

            public void Dispose()
            {
            }
        }
    }
}