using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct SpatialSphereMapConfig
    {
        /// <summary> Cell size of a level, in multiples of the largest diameter stored in it. </summary>
        public float CellScale;

        /// <summary> Lower bound for any cell size. Matters for point data, where radii give no scale. </summary>
        public float MinCellSize;

        /// <summary> 1 collapses everything into a single grid padded by the global max radius. </summary>
        public int MaxLevels;

        /// <summary> Radius bins with at most this many spheres skip the grid and are scanned linearly. </summary>
        public int LooseThreshold;

        /// <summary>
        /// A radius bin is folded into the next coarser level when that adds fewer than this many spheres per cell.
        /// Probing one more level costs about as much as testing a few extra spheres. 0 keeps every bin separate.
        /// </summary>
        public float MergeOccupancy;

        public static SpatialSphereMapConfig Default => new()
        {
            CellScale = 2f,
            MinCellSize = 0.01f,
            MaxLevels = SpatialSphereMap.MaxLevelCount,
            LooseThreshold = 16,
            MergeOccupancy = 4f,
        };
    }

    public struct SpatialSphereMapLevel
    {
        public float CellSize;
        public float InvCellSize;
        public float MaxRadius;
        public int2 MaxCell;
        public int Count;
    }

    internal struct SpatialSphereMapHeader
    {
        public float2 Origin;
        public int BucketMask;
        public int GridCount;
        public int Count;
    }

    /// <summary>
    /// Spatial hash for spheres of mixed sizes on the XZ plane. Every sphere is stored once, by its center.
    /// Radii are binned by powers of two above the smallest radius of the build, and the populated bins become
    /// grids whose cell size and query padding come from their own contents, so large spheres never widen the
    /// search for small ones. Rebuilt from scratch on every Build; query it through
    /// <see cref="SpatialSphereMapEnumerators" />.
    /// </summary>
    public struct SpatialSphereMap : IDisposable
    {
        public const int MaxLevelCount = 16;

        internal const int CellBits = 14;
        internal const int CellLimit = (1 << CellBits) - 1;

        private NativeList<int> _starts;
        private NativeList<uint> _keys;
        private NativeList<float4> _spheres;
        private NativeList<int> _indices;
        private NativeList<SpatialSphereMapLevel> _levels;
        private NativeReference<SpatialSphereMapHeader> _header;
        private NativeList<byte> _bins;
        private NativeList<int> _buckets;
        private NativeList<uint> _unsortedKeys;
        private NativeArray<SpatialSphereMapChunkBounds> _chunkBounds;
        private NativeArray<SpatialSphereMapChunkBins> _chunkBins;
        private NativeArray<int> _binLevels;
        private readonly SpatialSphereMapConfig _config;

        public SpatialSphereMap(SpatialSphereMapConfig config, Allocator allocator = Allocator.Persistent)
        {
            _config = config;
            _starts = new NativeList<int>(2, allocator);
            _starts.Add(0);
            _starts.Add(0);
            _keys = new NativeList<uint>(64, allocator);
            _spheres = new NativeList<float4>(64, allocator);
            _indices = new NativeList<int>(64, allocator);
            _levels = new NativeList<SpatialSphereMapLevel>(MaxLevelCount, allocator);
            _header = new NativeReference<SpatialSphereMapHeader>(allocator);
            _bins = new NativeList<byte>(64, allocator);
            _buckets = new NativeList<int>(64, allocator);
            _unsortedKeys = new NativeList<uint>(64, allocator);
            _chunkBounds = new NativeArray<SpatialSphereMapChunkBounds>(JobsUtility.ThreadIndexCount, allocator);
            _chunkBins = new NativeArray<SpatialSphereMapChunkBins>(JobsUtility.ThreadIndexCount, allocator);
            _binLevels = new NativeArray<int>(MaxLevelCount, allocator);
        }

        public bool IsCreated => _starts.IsCreated;

        public SpatialSphereMapConfig Config => _config;

        public void Dispose()
        {
            _starts.Dispose();
            _keys.Dispose();
            _spheres.Dispose();
            _indices.Dispose();
            _levels.Dispose();
            _header.Dispose();
            _bins.Dispose();
            _buckets.Dispose();
            _unsortedKeys.Dispose();
            _chunkBounds.Dispose();
            _chunkBins.Dispose();
            _binLevels.Dispose();
        }

        public JobHandle Build(NativeList<float3> positions, NativeList<float> radii, JobHandle dependency)
        {
            return Build(positions.AsDeferredJobArray(), radii.AsDeferredJobArray(), dependency);
        }

        /// <summary> Deferred arrays are supported. Both inputs are only read while the jobs run. </summary>
        public JobHandle Build(NativeArray<float3> positions, NativeArray<float> radii, JobHandle dependency)
        {
            var chunks = math.clamp(JobsUtility.JobWorkerCount, 1, _chunkBounds.Length);
            dependency = new SpatialSphereMapPrepareJob
            {
                Positions = positions,
                Keys = _keys,
                Spheres = _spheres,
                Indices = _indices,
                Bins = _bins,
                Buckets = _buckets,
                UnsortedKeys = _unsortedKeys,
            }.Schedule(dependency);
            dependency = new SpatialSphereMapBoundsJob
            {
                Positions = positions,
                Radii = radii,
                ChunkBounds = _chunkBounds,
                Chunks = chunks,
            }.ScheduleParallel(chunks, 1, dependency);
            dependency = new SpatialSphereMapBinJob
            {
                Radii = radii,
                ChunkBounds = _chunkBounds,
                Bins = _bins.AsDeferredJobArray(),
                ChunkBins = _chunkBins,
                Chunks = chunks,
                MaxLevels = math.clamp(_config.MaxLevels, 1, MaxLevelCount),
            }.ScheduleParallel(chunks, 1, dependency);
            dependency = new SpatialSphereMapLevelsJob
            {
                Positions = positions,
                ChunkBounds = _chunkBounds,
                ChunkBins = _chunkBins,
                Starts = _starts,
                Levels = _levels,
                BinLevels = _binLevels,
                Header = _header,
                Config = _config,
                Chunks = chunks,
            }.Schedule(dependency);
            dependency = new SpatialSphereMapKeyJob
            {
                Positions = positions,
                Bins = _bins.AsDeferredJobArray(),
                Levels = _levels.AsDeferredJobArray(),
                BinLevels = _binLevels,
                Header = _header,
                Buckets = _buckets.AsDeferredJobArray(),
                UnsortedKeys = _unsortedKeys.AsDeferredJobArray(),
                Chunks = chunks,
            }.ScheduleParallel(chunks, 1, dependency);
            return new SpatialSphereMapPlaceJob
            {
                Positions = positions,
                Radii = radii,
                Buckets = _buckets.AsDeferredJobArray(),
                UnsortedKeys = _unsortedKeys.AsDeferredJobArray(),
                Header = _header,
                Starts = _starts.AsDeferredJobArray(),
                Keys = _keys.AsDeferredJobArray(),
                Spheres = _spheres.AsDeferredJobArray(),
                Indices = _indices.AsDeferredJobArray(),
            }.Schedule(dependency);
        }

        public ReadOnly AsReadOnly()
        {
            return new ReadOnly
            {
                Starts = _starts,
                Keys = _keys,
                Spheres = _spheres,
                Indices = _indices,
                Levels = _levels,
                Header = _header,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Key(int level, int2 cell)
        {
            return ((uint)level << (CellBits * 2)) | ((uint)cell.y << CellBits) | (uint)cell.x;
        }

        // X is added unhashed so that neighbouring cells of a row land in neighbouring buckets.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Bucket(int level, int2 cell, int mask)
        {
            return (int)(((uint)cell.x + (uint)cell.y * 0x9E3779B1u + (uint)level * 0x85EBCA6Bu) & (uint)mask);
        }

        public struct ReadOnly
        {
            [ReadOnly] internal NativeList<int> Starts;
            [ReadOnly] internal NativeList<uint> Keys;
            [ReadOnly] internal NativeList<float4> Spheres;
            [ReadOnly] internal NativeList<int> Indices;
            [ReadOnly] internal NativeList<SpatialSphereMapLevel> Levels;
            [ReadOnly] internal NativeReference<SpatialSphereMapHeader> Header;

            public int Count => Header.Value.Count;

            /// <summary> Number of grid levels the last build produced. </summary>
            public int LevelCount => Levels.Length;

            /// <summary> Spheres kept outside the grids because their radius bin was too sparse. </summary>
            public int LooseCount => Header.Value.Count - Header.Value.GridCount;

            public SpatialSphereMapLevel GetLevel(int index) => Levels[index];
        }
    }
}
