using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Physics;
using Random = Unity.Mathematics.Random;
using SphereCollider = Unity.Physics.SphereCollider;

namespace FireAlt.Core.Tests
{
    /// <summary>
    /// Compares SpatialSphereMap with the Unity Physics broadphase on sphere-only worlds.
    /// "Flat" is the same map forced to one level, which is a plain grid padded by the global max radius.
    /// </summary>
    public class SpatialSphereMapBenchmarks
    {
        private const string ReportPath = "Logs/SpatialSphereMapBenchmark.md";
        private const string SweepReportPath = "Logs/SpatialSphereMapCellScaleSweep.md";
        private const int WarmupRuns = 3;
        private const int MeasureRuns = 15;
        private const int BatchSize = 64;
        private const int LargeQueryCount = 256;
        private const float LargeQueryRadius = 8f;
        private const float BaseRadius = 0.5f;

        private enum Scenario
        {
            UniformCrowd,
            Pile,
            CrowdPlusEightBosses,
            CrowdPlusTenPercentDouble,
            ContinuousSizes,
        }

        private StringBuilder _report;

        [Test]
        [Performance]
        [Explicit("Benchmark test. Run manually.")]
        [Category("Benchmark")]
        public void CompareWithPhysicsBroadphase()
        {
            _report = new StringBuilder();
            _report.AppendLine($"Workers: {Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount}, " +
                $"median of {MeasureRuns} runs, times in ms. Queries run as parallel jobs, one query per sphere.");
            _report.AppendLine();
            _report.AppendLine("| Scenario | N | Operation | BVH | Flat grid | Sphere map | Map vs BVH | Map vs flat | Levels + loose |");
            _report.AppendLine("|---|---|---|---|---|---|---|---|---|");

            foreach (Scenario scenario in Enum.GetValues(typeof(Scenario)))
            {
                Run(scenario, 10_000);
                Run(scenario, 50_000);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, _report.ToString());
            UnityEngine.Debug.Log(_report.ToString());
        }

        [Test]
        [Performance]
        [Explicit("Benchmark test. Run manually.")]
        [Category("Benchmark")]
        public void SweepCellScale()
        {
            const int count = 50_000;
            var report = new StringBuilder();
            report.AppendLine("| Scenario | Cell scale | Build | Sphere cast | Overlap neighbours | Levels + loose |");
            report.AppendLine("|---|---|---|---|---|---|");
            foreach (Scenario scenario in Enum.GetValues(typeof(Scenario)))
            {
                var positions = new NativeArray<float3>(count, Allocator.Persistent);
                var radii = new NativeArray<float>(count, Allocator.Persistent);
                var directions = new NativeArray<float3>(count, Allocator.Persistent);
                var largeCenters = new NativeArray<float3>(1, Allocator.Persistent);
                var results = new NativeArray<int>(count, Allocator.Persistent);
                Generate(scenario, positions, radii, directions, largeCenters);
                foreach (var cellScale in new[] { 1f, 1.5f, 2f, 3f, 4f })
                {
                    var config = SpatialSphereMapConfig.Default;
                    config.CellScale = cellScale;
                    var map = new SpatialSphereMap(config);
                    var build = Measure(() => map.Build(positions, radii, default).Complete());
                    var cast = Measure(() => new MapCastJob
                    {
                        Map = map.AsReadOnly(), Positions = positions, Radii = radii, Directions = directions,
                        Results = results,
                    }.ScheduleParallel(count, BatchSize, default).Complete());
                    var overlap = Measure(() => new MapOverlapJob
                    {
                        Map = map.AsReadOnly(), Centers = positions, Radii = radii, ExtraRadius = BaseRadius,
                        Results = results,
                    }.ScheduleParallel(count, BatchSize, default).Complete());
                    var readOnly = map.AsReadOnly();
                    report.AppendLine($"| {scenario} | {cellScale} | {build:F3} | {cast:F3} | {overlap:F3} " +
                        $"| {readOnly.LevelCount} + {readOnly.LooseCount} |");
                    map.Dispose();
                }

                results.Dispose();
                largeCenters.Dispose();
                directions.Dispose();
                radii.Dispose();
                positions.Dispose();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SweepReportPath)!);
            File.WriteAllText(SweepReportPath, report.ToString());
            UnityEngine.Debug.Log(report.ToString());
        }

        private void Run(Scenario scenario, int count)
        {
            var positions = new NativeArray<float3>(count, Allocator.Persistent);
            var radii = new NativeArray<float>(count, Allocator.Persistent);
            var directions = new NativeArray<float3>(count, Allocator.Persistent);
            var largeCenters = new NativeArray<float3>(LargeQueryCount, Allocator.Persistent);
            var largeRadii = new NativeArray<float>(LargeQueryCount, Allocator.Persistent);
            var bvhResults = new NativeArray<int>(count, Allocator.Persistent);
            var flatResults = new NativeArray<int>(count, Allocator.Persistent);
            var mapResults = new NativeArray<int>(count, Allocator.Persistent);
            var buildStaticTree = new NativeReference<int>(1, Allocator.Persistent);
            var collider = SphereCollider.Create(new SphereGeometry { Radius = 1f });
            var world = new PhysicsWorld(1, count, 0);
            var flatConfig = SpatialSphereMapConfig.Default;
            flatConfig.MaxLevels = 1;
            flatConfig.LooseThreshold = 0;
            var flat = new SpatialSphereMap(flatConfig);
            var map = new SpatialSphereMap(SpatialSphereMapConfig.Default);

            try
            {
                Generate(scenario, positions, radii, directions, largeCenters);
                var bodies = world.DynamicBodies;
                var velocities = world.MotionVelocities;
                var motions = world.MotionDatas;
                for (var i = 0; i < count; i++)
                {
                    bodies[i] = new RigidBody
                    {
                        WorldFromBody = new RigidTransform(quaternion.identity, positions[i]),
                        Collider = collider,
                        Entity = Entity.Null,
                        Scale = radii[i],
                    };
                    velocities[i] = default;
                    motions[i] = MotionData.Zero;
                }

                var statics = world.StaticBodies;
                statics[0] = RigidBody.Zero;

                var name = scenario.ToString();
                var bvhBuild = Measure(() => world.CollisionWorld.ScheduleBuildBroadphaseJobs(ref world, 1f / 60f,
                    float3.zero, buildStaticTree.AsReadOnly(), default).Complete());
                var flatBuild = Measure(() => flat.Build(positions, radii, default).Complete());
                var mapBuild = Measure(() => map.Build(positions, radii, default).Complete());
                var readOnly = map.AsReadOnly();
                var shape = $"{readOnly.LevelCount} + {readOnly.LooseCount}";
                Row(name, count, "Build", bvhBuild, flatBuild, mapBuild, shape);

                var bvhCast = Measure(() => new BvhCastJob
                {
                    World = world.CollisionWorld, Positions = positions, Radii = radii, Directions = directions,
                    Results = bvhResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                var flatCast = Measure(() => new MapCastJob
                {
                    Map = flat.AsReadOnly(), Positions = positions, Radii = radii, Directions = directions,
                    Results = flatResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                var mapCast = Measure(() => new MapCastJob
                {
                    Map = map.AsReadOnly(), Positions = positions, Radii = radii, Directions = directions,
                    Results = mapResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                Assert.That(mapResults.ToArray(), Is.EqualTo(flatResults.ToArray()));
                var agreement = Agreement(bvhResults, mapResults, count);
                Row(name, count, $"Sphere cast ({agreement:P1} agree on hit or miss)", bvhCast, flatCast, mapCast, shape);

                var bvhOverlap = Measure(() => new BvhOverlapJob
                {
                    World = world.CollisionWorld, Centers = positions, Radii = radii, ExtraRadius = BaseRadius,
                    Results = bvhResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                var flatOverlap = Measure(() => new MapOverlapJob
                {
                    Map = flat.AsReadOnly(), Centers = positions, Radii = radii, ExtraRadius = BaseRadius,
                    Results = flatResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                var mapOverlap = Measure(() => new MapOverlapJob
                {
                    Map = map.AsReadOnly(), Centers = positions, Radii = radii, ExtraRadius = BaseRadius,
                    Results = mapResults,
                }.ScheduleParallel(count, BatchSize, default).Complete());
                Assert.That(mapResults.ToArray(), Is.EqualTo(flatResults.ToArray()));
                AssertSameTotal(bvhResults, mapResults, count);
                Row(name, count, "Overlap neighbours", bvhOverlap, flatOverlap, mapOverlap, shape);

                var bvhLarge = Measure(() => new BvhOverlapJob
                {
                    World = world.CollisionWorld, Centers = largeCenters, Radii = largeRadii, ExtraRadius = LargeQueryRadius,
                    Results = bvhResults,
                }.ScheduleParallel(LargeQueryCount, 1, default).Complete());
                var flatLarge = Measure(() => new MapOverlapJob
                {
                    Map = flat.AsReadOnly(), Centers = largeCenters, Radii = largeRadii, ExtraRadius = LargeQueryRadius,
                    Results = flatResults,
                }.ScheduleParallel(LargeQueryCount, 1, default).Complete());
                var mapLarge = Measure(() => new MapOverlapJob
                {
                    Map = map.AsReadOnly(), Centers = largeCenters, Radii = largeRadii, ExtraRadius = LargeQueryRadius,
                    Results = mapResults,
                }.ScheduleParallel(LargeQueryCount, 1, default).Complete());
                AssertSameTotal(bvhResults, mapResults, LargeQueryCount);
                Row(name, count, $"{LargeQueryCount} overlaps, radius {LargeQueryRadius}", bvhLarge, flatLarge,
                    mapLarge, shape);
            }
            finally
            {
                map.Dispose();
                flat.Dispose();
                world.Dispose();
                collider.Dispose();
                buildStaticTree.Dispose();
                mapResults.Dispose();
                flatResults.Dispose();
                bvhResults.Dispose();
                largeRadii.Dispose();
                largeCenters.Dispose();
                directions.Dispose();
                radii.Dispose();
                positions.Dispose();
            }
        }

        private static void Generate(Scenario scenario, NativeArray<float3> positions, NativeArray<float> radii,
            NativeArray<float3> directions, NativeArray<float3> largeCenters)
        {
            var random = new Random(0x51A7u + (uint)scenario * 977u + (uint)positions.Length);
            var count = positions.Length;
            var footprint = 0f;
            for (var i = 0; i < count; i++)
            {
                var radius = BaseRadius * random.NextFloat(0.88f, 1.12f);
                switch (scenario)
                {
                    case Scenario.CrowdPlusEightBosses when i < 8:
                        radius *= 4f;
                        break;
                    case Scenario.CrowdPlusTenPercentDouble when i % 10 == 0:
                        radius *= 2f;
                        break;
                    case Scenario.ContinuousSizes:
                        radius = BaseRadius * math.exp2(random.NextFloat(-1f, 3f));
                        break;
                }

                radii[i] = radius;
                footprint += math.PI * radius * radius;
            }

            // Half of the ground is covered; the pile stacks the same crowd four layers deep.
            var layers = scenario == Scenario.Pile ? 4f : 1f;
            var halfSize = math.sqrt(footprint * 2f / layers) * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var height = scenario == Scenario.Pile ? random.NextFloat(0f, layers * 2f * BaseRadius) : 0f;
                var ground = random.NextFloat2(-halfSize, halfSize);
                positions[i] = new float3(ground.x, radii[i] + height, ground.y);
                var direction = random.NextFloat2Direction();
                directions[i] = new float3(direction.x, 0f, direction.y);
            }

            for (var i = 0; i < largeCenters.Length; i++)
            {
                var ground = random.NextFloat2(-halfSize, halfSize);
                largeCenters[i] = new float3(ground.x, BaseRadius, ground.y);
            }
        }

        private static double Measure(Action action)
        {
            for (var i = 0; i < WarmupRuns; i++)
            {
                action();
            }

            var samples = new double[MeasureRuns];
            var stopwatch = new Stopwatch();
            for (var i = 0; i < MeasureRuns; i++)
            {
                stopwatch.Restart();
                action();
                stopwatch.Stop();
                samples[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            Array.Sort(samples);
            return samples[MeasureRuns / 2];
        }

        private void Row(string scenario, int count, string operation, double bvh, double flat, double map,
            string shape)
        {
            _report.AppendLine($"| {scenario} | {count} | {operation} | {bvh:F3} | {flat:F3} | {map:F3} " +
                $"| {bvh / map:F1}x | {flat / map:F2}x | {shape} |");
            var prefix = $"{scenario}/{count}/{operation}";
            Unity.PerformanceTesting.Measure.Custom(new SampleGroup($"{prefix}/BVH", SampleUnit.Millisecond), bvh);
            Unity.PerformanceTesting.Measure.Custom(new SampleGroup($"{prefix}/Flat", SampleUnit.Millisecond), flat);
            Unity.PerformanceTesting.Measure.Custom(new SampleGroup($"{prefix}/Map", SampleUnit.Millisecond), map);
        }

        private static double Agreement(NativeArray<int> expected, NativeArray<int> actual, int count)
        {
            var same = 0;
            for (var i = 0; i < count; i++)
            {
                same += expected[i] >= 0 == actual[i] >= 0 ? 1 : 0;
            }

            return (double)same / count;
        }

        private static void AssertSameTotal(NativeArray<int> expected, NativeArray<int> actual, int count)
        {
            long expectedTotal = 0;
            long actualTotal = 0;
            for (var i = 0; i < count; i++)
            {
                expectedTotal += expected[i];
                actualTotal += actual[i];
            }

            Assert.That(expectedTotal, Is.GreaterThan(0));
            Assert.That(actualTotal, Is.EqualTo(expectedTotal).Within(0.1).Percent);
        }

        private static float CastRadius(float radius) => radius * 0.9f;

        private static float CastDistance(float radius) => radius * 0.1f + 0.3f;

        [BurstCompile(CompileSynchronously = true)]
        private struct MapCastJob : IJobFor
        {
            public SpatialSphereMap.ReadOnly Map;
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float> Radii;
            [ReadOnly] public NativeArray<float3> Directions;
            [WriteOnly] public NativeArray<int> Results;

            public void Execute(int index)
            {
                var radius = Radii[index];
                Results[index] = Map.SphereCast(Positions[index], CastRadius(radius), Directions[index],
                    CastDistance(radius), index, out var hit) ? hit.Index : -1;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct BvhCastJob : IJobFor
        {
            [ReadOnly] public CollisionWorld World;
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float> Radii;
            [ReadOnly] public NativeArray<float3> Directions;
            [WriteOnly] public NativeArray<int> Results;

            public void Execute(int index)
            {
                var radius = Radii[index];
                var collector = new ClosestOtherCollector(index);
                World.SphereCastCustom(Positions[index], CastRadius(radius), Directions[index], CastDistance(radius),
                    ref collector, CollisionFilter.Default);
                Results[index] = collector.NumHits > 0 ? collector.Closest : -1;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MapOverlapJob : IJobFor
        {
            public SpatialSphereMap.ReadOnly Map;
            [ReadOnly] public NativeArray<float3> Centers;
            [ReadOnly] public NativeArray<float> Radii;
            public float ExtraRadius;
            [WriteOnly] public NativeArray<int> Results;

            public void Execute(int index)
            {
                var radius = ExtraRadius + Radii[index];
                var hits = 0;
                foreach (var _ in Map.SpheresInSphere(Centers[index], radius))
                {
                    hits++;
                }

                Results[index] = hits;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct BvhOverlapJob : IJobFor
        {
            [ReadOnly] public CollisionWorld World;
            [ReadOnly] public NativeArray<float3> Centers;
            [ReadOnly] public NativeArray<float> Radii;
            public float ExtraRadius;
            [WriteOnly] public NativeArray<int> Results;

            public void Execute(int index)
            {
                var radius = ExtraRadius + Radii[index];
                var collector = new CountingCollector(radius);
                World.OverlapSphereCustom(Centers[index], radius, ref collector, CollisionFilter.Default);
                Results[index] = collector.NumHits;
            }
        }

        private struct ClosestOtherCollector : ICollector<ColliderCastHit>
        {
            private readonly int _ignored;
            public int Closest;

            public ClosestOtherCollector(int ignored)
            {
                _ignored = ignored;
                Closest = -1;
                MaxFraction = 1f;
                NumHits = 0;
            }

            public bool EarlyOutOnFirstHit => false;

            public float MaxFraction { get; private set; }

            public int NumHits { get; private set; }

            public bool AddHit(ColliderCastHit hit)
            {
                if (hit.RigidBodyIndex == _ignored)
                {
                    return false;
                }

                MaxFraction = hit.Fraction;
                Closest = hit.RigidBodyIndex;
                NumHits = 1;
                return true;
            }
        }

        private struct CountingCollector : ICollector<DistanceHit>
        {
            public CountingCollector(float maxDistance)
            {
                MaxFraction = maxDistance;
                NumHits = 0;
            }

            public bool EarlyOutOnFirstHit => false;

            public float MaxFraction { get; }

            public int NumHits { get; private set; }

            public bool AddHit(DistanceHit hit)
            {
                NumHits++;
                return true;
            }
        }
    }
}
