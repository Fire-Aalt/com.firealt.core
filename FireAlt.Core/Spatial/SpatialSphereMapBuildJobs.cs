using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FireAlt.Core
{
    internal struct SpatialSphereMapChunkBounds
    {
        public float MinRadius;
        public float2 BoundsMin;
        public float2 BoundsMax;
    }

    internal unsafe struct SpatialSphereMapChunkBins
    {
        public fixed int Counts[SpatialSphereMap.MaxLevelCount];
        public fixed float MaxRadius[SpatialSphereMap.MaxLevelCount];
    }

    internal static class SpatialSphereMapChunks
    {
        private const int MIN_CHUNK_SIZE = 4096;

        // Small inputs are not worth spreading over every worker; the surplus chunks get an empty range.
        public static void Range(int index, int scheduledChunks, int count, out int start, out int end)
        {
            var chunks = Count(scheduledChunks, count);
            if (index >= chunks)
            {
                start = 0;
                end = 0;
                return;
            }

            start = (int)((long)index * count / chunks);
            end = (int)((long)(index + 1) * count / chunks);
        }

        public static int Count(int scheduledChunks, int count)
        {
            return math.clamp(count / MIN_CHUNK_SIZE, 1, scheduledChunks);
        }
    }

    [BurstCompile]
    internal struct SpatialSphereMapPrepareJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        public NativeList<uint> Keys;
        public NativeList<float4> Spheres;
        public NativeList<float> HalfHeights;
        public NativeList<int> Indices;
        public NativeList<byte> Bins;
        public NativeList<int> Buckets;
        public NativeList<uint> UnsortedKeys;

        public void Execute()
        {
            var count = Positions.Length;
            Keys.ResizeUninitialized(count);
            Spheres.ResizeUninitialized(count);
            HalfHeights.ResizeUninitialized(count);
            Indices.ResizeUninitialized(count);
            Bins.ResizeUninitialized(count);
            Buckets.ResizeUninitialized(count);
            UnsortedKeys.ResizeUninitialized(count);
        }
    }

    [BurstCompile]
    internal struct SpatialSphereMapBoundsJob : IJobFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> Radii;
        [NativeDisableParallelForRestriction] public NativeArray<SpatialSphereMapChunkBounds> ChunkBounds;
        public int Chunks;

        public void Execute(int index)
        {
            SpatialSphereMapChunks.Range(index, Chunks, Positions.Length, out var start, out var end);
            var stats = new SpatialSphereMapChunkBounds
            {
                MinRadius = float.MaxValue,
                BoundsMin = float.MaxValue,
                BoundsMax = float.MinValue,
            };
            for (var i = start; i < end; i++)
            {
                var position = Positions[i].xz;
                stats.MinRadius = math.min(stats.MinRadius, Radii[i]);
                stats.BoundsMin = math.min(stats.BoundsMin, position);
                stats.BoundsMax = math.max(stats.BoundsMax, position);
            }

            ChunkBounds[index] = stats;
        }
    }

    [BurstCompile]
    internal unsafe struct SpatialSphereMapBinJob : IJobFor
    {
        public const float MIN_BASE_RADIUS = 1e-6f;

        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeArray<SpatialSphereMapChunkBounds> ChunkBounds;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Bins;
        [NativeDisableParallelForRestriction] public NativeArray<SpatialSphereMapChunkBins> ChunkBins;
        public int Chunks;
        public int MaxLevels;

        public void Execute(int index)
        {
            var count = Radii.Length;
            SpatialSphereMapChunks.Range(index, Chunks, count, out var start, out var end);
            var inverseBase = InverseBaseRadius(ChunkBounds, SpatialSphereMapChunks.Count(Chunks, count));
            var stats = default(SpatialSphereMapChunkBins);
            for (var i = start; i < end; i++)
            {
                // Bin = exponent of radius / smallest radius, so one bin spans a factor of two.
                var radius = math.max(Radii[i], 0f);
                var ratio = radius * inverseBase;
                var bin = ratio < 2f ? 0 : math.min(MaxLevels - 1, (int)(math.asuint(ratio) >> 23) - 127);
                Bins[i] = (byte)bin;
                stats.Counts[bin]++;
                stats.MaxRadius[bin] = math.max(stats.MaxRadius[bin], radius);
            }

            ChunkBins[index] = stats;
        }

        public static float InverseBaseRadius(NativeArray<SpatialSphereMapChunkBounds> chunkBounds, int chunks)
        {
            var minRadius = float.MaxValue;
            for (var i = 0; i < chunks; i++)
            {
                minRadius = math.min(minRadius, chunkBounds[i].MinRadius);
            }

            return 1f / math.max(minRadius, MIN_BASE_RADIUS);
        }
    }

    [BurstCompile]
    internal unsafe struct SpatialSphereMapLevelsJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<SpatialSphereMapChunkBounds> ChunkBounds;
        [ReadOnly] public NativeArray<SpatialSphereMapChunkBins> ChunkBins;
        public NativeList<int> Starts;
        public NativeList<SpatialSphereMapLevel> Levels;
        public NativeArray<int> BinLevels;
        public NativeReference<SpatialSphereMapHeader> Header;
        public SpatialSphereMapConfig Config;
        public int Chunks;

        public void Execute()
        {
            var count = Positions.Length;
            Levels.Clear();
            if (count == 0)
            {
                Starts.ResizeUninitialized(2);
                Starts[0] = 0;
                Starts[1] = 0;
                Header.Value = default;
                return;
            }

            var chunks = SpatialSphereMapChunks.Count(Chunks, count);
            var boundsMin = new float2(float.MaxValue);
            var boundsMax = new float2(float.MinValue);
            var binCounts = new NativeArray<int>(SpatialSphereMap.MaxLevelCount, Allocator.Temp);
            var binMaxRadius = new NativeArray<float>(SpatialSphereMap.MaxLevelCount, Allocator.Temp);
            for (var i = 0; i < chunks; i++)
            {
                boundsMin = math.min(boundsMin, ChunkBounds[i].BoundsMin);
                boundsMax = math.max(boundsMax, ChunkBounds[i].BoundsMax);
                var bins = ChunkBins[i];
                for (var bin = 0; bin < SpatialSphereMap.MaxLevelCount; bin++)
                {
                    binCounts[bin] += bins.Counts[bin];
                    binMaxRadius[bin] = math.max(binMaxRadius[bin], bins.MaxRadius[bin]);
                }
            }

            var extent = boundsMax - boundsMin;
            var minCellForExtent = math.max(math.cmax(extent) / (SpatialSphereMap.CellLimit - 1),
                SpatialSphereMapBinJob.MIN_BASE_RADIUS);

            // Coarsest bin first: a finer bin either joins the level above it, stays loose, or opens its own level.
            var binOwners = new NativeArray<int>(SpatialSphereMap.MaxLevelCount, Allocator.Temp);
            var ownerCounts = new NativeArray<int>(SpatialSphereMap.MaxLevelCount, Allocator.Temp);
            var owner = -1;
            for (var bin = SpatialSphereMap.MaxLevelCount - 1; bin >= 0; bin--)
            {
                binOwners[bin] = -1;
                if (binCounts[bin] == 0)
                {
                    continue;
                }

                if (owner >= 0)
                {
                    var cellSize = CellSize(binMaxRadius[bin], minCellForExtent);
                    var ownerCellSize = CellSize(binMaxRadius[owner], minCellForExtent);
                    var area = math.max(extent.x, ownerCellSize) * math.max(extent.y, ownerCellSize);
                    var addedPerCell = binCounts[bin] * (ownerCellSize * ownerCellSize - cellSize * cellSize) / area;
                    if (addedPerCell < Config.MergeOccupancy)
                    {
                        binOwners[owner] = owner;
                        binOwners[bin] = owner;
                        ownerCounts[owner] += binCounts[bin];
                        continue;
                    }
                }

                // A sparse bin only becomes a level if a finer bin joins it; otherwise it stays loose.
                owner = bin;
                ownerCounts[bin] = binCounts[bin];
                if (binCounts[bin] > Config.LooseThreshold)
                {
                    binOwners[bin] = bin;
                }
            }

            var gridCount = 0;
            for (var bin = 0; bin < SpatialSphereMap.MaxLevelCount; bin++)
            {
                if (binOwners[bin] != bin)
                {
                    continue;
                }

                var cellSize = CellSize(binMaxRadius[bin], minCellForExtent);
                var inverseCellSize = 1f / cellSize;
                BinLevels[bin] = Levels.Length;
                Levels.Add(new SpatialSphereMapLevel
                {
                    CellSize = cellSize,
                    InvCellSize = inverseCellSize,
                    MaxRadius = binMaxRadius[bin],
                    MaxCell = math.min((int2)math.floor(extent * inverseCellSize), SpatialSphereMap.CellLimit),
                    Count = ownerCounts[bin],
                });
                gridCount += ownerCounts[bin];
            }

            for (var bin = 0; bin < SpatialSphereMap.MaxLevelCount; bin++)
            {
                if (binOwners[bin] != bin)
                {
                    BinLevels[bin] = binOwners[bin] < 0 ? -1 : BinLevels[binOwners[bin]];
                }
            }

            var bucketCount = math.ceilpow2(math.max(gridCount, 1));
            Starts.ResizeUninitialized(bucketCount + 1);
            Header.Value = new SpatialSphereMapHeader
            {
                Origin = boundsMin,
                BucketMask = bucketCount - 1,
                GridCount = gridCount,
                Count = count,
            };
        }

        private float CellSize(float maxRadius, float minCellForExtent)
        {
            return math.max(math.max(Config.CellScale * 2f * maxRadius, Config.MinCellSize), minCellForExtent);
        }
    }

    [BurstCompile]
    internal struct SpatialSphereMapKeyJob : IJobFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<byte> Bins;
        [ReadOnly] public NativeArray<SpatialSphereMapLevel> Levels;
        [ReadOnly] public NativeArray<int> BinLevels;
        [ReadOnly] public NativeReference<SpatialSphereMapHeader> Header;
        [NativeDisableParallelForRestriction] public NativeArray<int> Buckets;
        [NativeDisableParallelForRestriction] public NativeArray<uint> UnsortedKeys;
        public int Chunks;

        public void Execute(int index)
        {
            SpatialSphereMapChunks.Range(index, Chunks, Positions.Length, out var start, out var end);
            var header = Header.Value;
            for (var i = start; i < end; i++)
            {
                var levelIndex = BinLevels[Bins[i]];
                if (levelIndex < 0)
                {
                    Buckets[i] = -1;
                    continue;
                }

                var level = Levels[levelIndex];
                var cell = (int2)math.floor((Positions[i].xz - header.Origin) * level.InvCellSize);
                cell = math.clamp(cell, 0, level.MaxCell);
                Buckets[i] = SpatialSphereMap.Bucket(levelIndex, cell, header.BucketMask);
                UnsortedKeys[i] = SpatialSphereMap.Key(levelIndex, cell);
            }
        }
    }

    /// <summary> Counting sort by bucket, so the spheres of one cell end up next to each other. </summary>
    [BurstCompile]
    internal struct SpatialSphereMapPlaceJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeArray<float> InputHalfHeights;
        [ReadOnly] public NativeArray<int> Buckets;
        [ReadOnly] public NativeArray<uint> UnsortedKeys;
        public NativeArray<SpatialSphereMapLevel> Levels;
        public NativeReference<SpatialSphereMapHeader> Header;
        public NativeArray<int> Starts;
        public NativeArray<uint> Keys;
        public NativeArray<float4> Spheres;
        public NativeArray<float> HalfHeights;
        public NativeArray<int> Indices;

        public void Execute()
        {
            var count = Positions.Length;
            if (count == 0)
            {
                return;
            }

            var bucketCount = Starts.Length - 1;
            for (var i = 0; i <= bucketCount; i++)
            {
                Starts[i] = 0;
            }

            for (var i = 0; i < count; i++)
            {
                var bucket = Buckets[i];
                if (bucket >= 0)
                {
                    Starts[bucket + 1]++;
                }
            }

            for (var i = 1; i <= bucketCount; i++)
            {
                Starts[i] += Starts[i - 1];
            }

            var cursors = new NativeArray<int>(bucketCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<int>.Copy(Starts, cursors, bucketCount);
            var header = Header.Value;
            var looseCursor = header.GridCount;
            var capsules = InputHalfHeights.Length != 0;
            for (var i = 0; i < count; i++)
            {
                var bucket = Buckets[i];
                var halfHeight = capsules ? math.max(InputHalfHeights[i], 0f) : 0f;
                int slot;
                if (bucket < 0)
                {
                    slot = looseCursor++;
                    Keys[slot] = 0;
                    header.LooseMaxHalfHeight = math.max(header.LooseMaxHalfHeight, halfHeight);
                }
                else
                {
                    slot = cursors[bucket]++;
                    var key = UnsortedKeys[i];
                    Keys[slot] = key;
                    if (halfHeight > 0f)
                    {
                        var levelIndex = (int)(key >> (SpatialSphereMap.CellBits * 2));
                        var level = Levels[levelIndex];
                        level.MaxHalfHeight = math.max(level.MaxHalfHeight, halfHeight);
                        Levels[levelIndex] = level;
                    }
                }

                Spheres[slot] = new float4(Positions[i], math.max(Radii[i], 0f));
                HalfHeights[slot] = halfHeight;
                Indices[slot] = i;
            }

            Header.Value = header;
        }
    }
}
