using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace FireAlt.Core.Tests
{
    [DisableAutoCreation]
    public partial class SpatialSphereMapTestSystem : SystemBase
    {
        public EntitySpatialSphereMap Map;
        private EntitySpatialSphereMapBuilder _builder;

        protected override void OnCreate()
        {
            Map = new EntitySpatialSphereMap(SpatialSphereMapConfig.Default);
            _builder = new EntitySpatialSphereMapBuilder(ref CheckedStateRef,
                GetEntityQuery(typeof(LocalTransform), typeof(SpatialSphereShape)));
        }

        protected override void OnUpdate()
        {
            Dependency = _builder.GatherAndBuild(ref CheckedStateRef, ref Map, Dependency);
        }

        protected override void OnDestroy()
        {
            Map.Dispose();
        }
    }

    public struct SkipFirst : ISpatialSphereFilter
    {
        public bool Accept(int index) => index != 0;
    }

    public class SpatialSphereMapTests
    {
        private const int QueriesPerCase = 150;

        private NativeList<float3> _positions;
        private NativeList<float> _radii;
        private SpatialSphereMap _map;

        [SetUp]
        public void SetUp()
        {
            _positions = new NativeList<float3>(Allocator.Persistent);
            _radii = new NativeList<float>(Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_map.IsCreated)
            {
                _map.Dispose();
            }

            _map = default;
            _positions.Dispose();
            _radii.Dispose();
        }

        [Test]
        public void EmptyMap_ReturnsNothing()
        {
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            Assert.That(map.Count, Is.EqualTo(0));
            Assert.That(Collect(map.SpheresInAabb(Box(-100f, 100f))), Is.Empty);
            Assert.That(Collect(map.SpheresInSphere(0f, 100f)), Is.Empty);
            Assert.That(map.SphereCast(0f, 1f, new float3(1f, 0f, 0f), 10f, -1, out _), Is.False);
        }

        [Test]
        public void SingleSphere_IsFoundOnlyWhenTouched()
        {
            Add(new float3(5f, 1f, -3f), 0.5f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            Assert.That(Collect(map.SpheresInSphere(new float3(6.4f, 1f, -3f), 1f)), Is.EquivalentTo(new[] { 0 }));
            Assert.That(Collect(map.SpheresInSphere(new float3(6.6f, 1f, -3f), 1f)), Is.Empty);
            Assert.That(Collect(map.SpheresInSphere(new float3(5f, 2.6f, -3f), 1f)), Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void UniformCrowd_MatchesBruteForce(int seed)
        {
            var random = new Random((uint)seed);
            AddCrowd(ref random, 2000, 40f, 3f, 0.44f, 0.56f);

            AssertMatchesBruteForce(SpatialSphereMapConfig.Default, (uint)seed);
        }

        [TestCase(1, 16)]
        [TestCase(2, 0)]
        [TestCase(3, 16)]
        public void MixedSizes_MatchesBruteForce(int seed, int looseThreshold)
        {
            var random = new Random((uint)seed);
            AddCrowd(ref random, 1500, 40f, 3f, 0.44f, 0.56f);
            AddCrowd(ref random, 200, 40f, 3f, 1f, 1.2f);
            AddCrowd(ref random, 5, 40f, 3f, 4f, 5f);
            AddCrowd(ref random, 50, 40f, 3f, 0f, 0f);

            var config = SpatialSphereMapConfig.Default;
            config.LooseThreshold = looseThreshold;
            AssertMatchesBruteForce(config, (uint)seed);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ContinuousSizes_MatchesBruteForce(int seed)
        {
            var random = new Random((uint)seed);
            for (var i = 0; i < 2000; i++)
            {
                Add(random.NextFloat3(new float3(-60f, 0f, -60f), new float3(60f, 6f, 60f)),
                    math.exp2(random.NextFloat(-3f, 3f)));
            }

            AssertMatchesBruteForce(SpatialSphereMapConfig.Default, (uint)seed);
        }

        [Test]
        public void SingleLevelConfig_MatchesBruteForce()
        {
            var random = new Random(7);
            AddCrowd(ref random, 1500, 40f, 3f, 0.44f, 0.56f);
            AddCrowd(ref random, 40, 40f, 3f, 2f, 2.5f);

            var config = SpatialSphereMapConfig.Default;
            config.MaxLevels = 1;
            config.LooseThreshold = 0;
            AssertMatchesBruteForce(config, 7);

            var map = _map.AsReadOnly();
            Assert.That(map.LevelCount, Is.EqualTo(1));
            Assert.That(map.GetLevel(0).MaxRadius, Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void Points_MatchBruteForce()
        {
            var random = new Random(11);
            AddCrowd(ref random, 3000, 50f, 0f, 0f, 0f);

            var config = SpatialSphereMapConfig.Default;
            config.MinCellSize = 1.5f;
            AssertMatchesBruteForce(config, 11);
        }

        [Test]
        public void CoincidentSpheres_AreAllReturned()
        {
            for (var i = 0; i < 500; i++)
            {
                Add(new float3(3f, 0f, 3f), 0.5f);
            }

            Build(SpatialSphereMapConfig.Default);

            Assert.That(Collect(_map.AsReadOnly().SpheresInSphere(new float3(3.2f, 0f, 3f), 0.1f)).Count, Is.EqualTo(500));
        }

        [Test]
        public void DistantClusters_MatchBruteForce()
        {
            var random = new Random(5);
            AddCrowd(ref random, 500, 10f, 1f, 0.02f, 0.03f);
            var first = _positions.Length;
            AddCrowd(ref random, 500, 10f, 1f, 0.02f, 0.03f);
            for (var i = first; i < _positions.Length; i++)
            {
                _positions[i] += new float3(50000f, 0f, -30000f);
            }

            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            for (var i = 0; i < _positions.Length; i += 37)
            {
                var center = _positions[i];
                Assert.That(Collect(map.SpheresInSphere(center, 0.5f)), Is.EquivalentTo(BruteSphere(center, 0.5f)));
            }
        }

        [Test]
        public void QueryOutsideBounds_StillFindsReachingSphere()
        {
            var random = new Random(3);
            AddCrowd(ref random, 400, 10f, 0f, 0.4f, 0.5f);
            Add(new float3(10f, 0f, 0f), 6f);
            Add(new float3(-10f, 0f, 10f), 0.5f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            foreach (var center in new[]
                     {
                         new float3(15.5f, 0f, 0f), new float3(-10.6f, 0f, 10.6f), new float3(0f, 0f, 40f),
                         new float3(-10f, 0f, 10.9f),
                     })
            {
                Assert.That(Collect(map.SpheresInSphere(center, 0.3f)), Is.EquivalentTo(BruteSphere(center, 0.3f)));
            }
        }

        [Test]
        public void Levels_FollowTheData()
        {
            var random = new Random(9);
            AddCrowd(ref random, 1000, 30f, 0f, 0.44f, 0.56f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();
            Assert.That(map.LevelCount, Is.EqualTo(1));
            Assert.That(map.LooseCount, Is.EqualTo(0));
            Assert.That(map.GetLevel(0).MaxRadius, Is.LessThanOrEqualTo(0.56f));

            AddCrowd(ref random, 5, 30f, 0f, 2.2f, 2.3f);
            Rebuild();
            map = _map.AsReadOnly();
            Assert.That(map.LevelCount, Is.EqualTo(1));
            Assert.That(map.LooseCount, Is.EqualTo(5));
            Assert.That(map.GetLevel(0).MaxRadius, Is.LessThanOrEqualTo(0.56f));

            AddCrowd(ref random, 100, 30f, 0f, 2.2f, 2.3f);
            Rebuild();
            map = _map.AsReadOnly();
            Assert.That(map.LevelCount, Is.EqualTo(2));
            Assert.That(map.LooseCount, Is.EqualTo(0));
            Assert.That(map.GetLevel(0).MaxRadius, Is.LessThanOrEqualTo(0.56f));
            Assert.That(map.GetLevel(1).MaxRadius, Is.InRange(2.2f, 2.3f));
            Assert.That(map.GetLevel(1).CellSize, Is.GreaterThan(map.GetLevel(0).CellSize));
        }

        [Test]
        public void SparseSizeBins_MergeIntoOneLevel()
        {
            var random = new Random(13);
            for (var i = 0; i < 2000; i++)
            {
                Add(random.NextFloat3(new float3(-150f, 0f, -150f), new float3(150f, 0f, 150f)),
                    math.exp2(random.NextFloat(-2f, 2f)));
            }

            AssertMatchesBruteForce(SpatialSphereMapConfig.Default, 13);
            Assert.That(_map.AsReadOnly().LevelCount, Is.EqualTo(1));
            Assert.That(_map.AsReadOnly().LooseCount, Is.EqualTo(0));
            _map.Dispose();

            var separate = SpatialSphereMapConfig.Default;
            separate.MergeOccupancy = 0f;
            AssertMatchesBruteForce(separate, 13);
            Assert.That(_map.AsReadOnly().LevelCount, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void TinyOutlier_JoinsTheCrowdLevel()
        {
            var random = new Random(17);
            AddCrowd(ref random, 1000, 30f, 0f, 0.44f, 0.56f);
            Add(new float3(1f, 0f, 1f), 0.001f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            Assert.That(map.LevelCount, Is.EqualTo(1));
            Assert.That(map.LooseCount, Is.EqualTo(0));
            Assert.That(Collect(map.SpheresInSphere(new float3(1f, 0f, 1f), 0.01f)), Does.Contain(1000));
        }

        [Test]
        public void SphereCast_HonoursFilter()
        {
            Add(new float3(2f, 0f, 0f), 0.5f);
            Add(new float3(4f, 0f, 0f), 0.5f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();
            var direction = new float3(1f, 0f, 0f);

            Assert.That(map.SphereCast(0f, 0.5f, direction, 10f, out var first), Is.True);
            Assert.That(first.Index, Is.EqualTo(0));
            Assert.That(first.Distance, Is.EqualTo(1f).Within(1e-5f));

            var filter = new SkipFirst();
            Assert.That(map.SphereCast(0f, 0.5f, direction, 10f, ref filter, out var second), Is.True);
            Assert.That(second.Index, Is.EqualTo(1));
            Assert.That(map.SphereCast(0f, 0.5f, direction, 2.5f, ref filter, out _), Is.False);
        }

        [Test]
        public void EntityBuilder_GathersEveryEntityOfTheQuery()
        {
            using var world = new World("SpatialSphereMapTests");
            var system = world.CreateSystemManaged<SpatialSphereMapTestSystem>();
            var manager = world.EntityManager;
            var random = new Random(21);
            var expected = new Dictionary<Entity, float4>();
            for (var i = 0; i < 700; i++)
            {
                var entity = i % 3 == 0
                    ? manager.CreateEntity(typeof(LocalTransform), typeof(SpatialSphereShape), typeof(LocalToWorld))
                    : manager.CreateEntity(typeof(LocalTransform), typeof(SpatialSphereShape));
                var transform = LocalTransform.FromPositionRotationScale(
                    random.NextFloat3(new float3(-30f, 0f, -30f), new float3(30f, 3f, 30f)),
                    random.NextQuaternionRotation(), random.NextFloat(0.5f, 2f));
                var shape = new SpatialSphereShape
                {
                    Center = i % 5 == 0 ? random.NextFloat3(-0.5f, 0.5f) : float3.zero,
                    Radius = random.NextFloat(0.3f, 0.6f),
                };
                manager.SetComponentData(entity, transform);
                manager.SetComponentData(entity, shape);
                expected.Add(entity, new float4(transform.TransformPoint(shape.Center), shape.Radius * transform.Scale));
            }

            manager.CreateEntity(typeof(LocalTransform));
            system.Update();
            manager.CompleteAllTrackedJobs();

            Assert.That(system.Map.Entities.Length, Is.EqualTo(700));
            for (var i = 0; i < 700; i++)
            {
                var sphere = expected[system.Map.Entities[i]];
                Assert.That(math.distance(system.Map.Positions[i], sphere.xyz), Is.LessThan(1e-4f));
                Assert.That(system.Map.Radii[i], Is.EqualTo(sphere.w).Within(1e-5f));
            }

            var map = system.Map.AsReadOnly();
            for (var i = 0; i < 40; i++)
            {
                var center = random.NextFloat3(new float3(-30f, 0f, -30f), new float3(30f, 3f, 30f));
                var found = new List<Entity>();
                foreach (var (entity, position) in map.EntitiesInSphere(center, 3f))
                {
                    Assert.That(math.distance(position, expected[entity].xyz), Is.LessThan(1e-4f));
                    found.Add(entity);
                }

                var brute = new List<Entity>();
                foreach (var pair in expected)
                {
                    if (math.distance(pair.Value.xyz, center) <= 3f + pair.Value.w)
                    {
                        brute.Add(pair.Key);
                    }
                }

                Assert.That(found, Is.EquivalentTo(brute));
            }

            var caster = system.Map.Entities[0];
            Assert.That(map.SphereCast(expected[caster].xyz, 0.1f, new float3(1f, 0f, 0f), 100f, caster,
                out var hit), Is.True);
            Assert.That(system.Map.Entities[hit.Index], Is.Not.EqualTo(caster));
        }

        [Test]
        public void Rebuild_ReplacesContents()
        {
            var random = new Random(4);
            AddCrowd(ref random, 800, 20f, 0f, 0.4f, 0.6f);
            Build(SpatialSphereMapConfig.Default);
            Assert.That(_map.AsReadOnly().Count, Is.EqualTo(800));

            _positions.Clear();
            _radii.Clear();
            Add(new float3(100f, 0f, 100f), 1f);
            Rebuild();
            var map = _map.AsReadOnly();

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(Collect(map.SpheresInAabb(Box(-50f, 50f))), Is.Empty);
            Assert.That(Collect(map.SpheresInSphere(new float3(100f, 0f, 100f), 0.1f)), Is.EquivalentTo(new[] { 0 }));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void SphereCast_MatchesBruteForce(int seed)
        {
            var random = new Random((uint)seed);
            AddCrowd(ref random, 1500, 40f, 2f, 0.44f, 0.56f);
            AddCrowd(ref random, 60, 40f, 2f, 1f, 1.2f);
            AddCrowd(ref random, 4, 40f, 2f, 3f, 4f);
            Build(SpatialSphereMapConfig.Default);
            var map = _map.AsReadOnly();

            var hits = 0;
            for (var i = 0; i < 600; i++)
            {
                var ignore = random.NextInt(_positions.Length);
                var origin = _positions[ignore];
                var radius = _radii[ignore] * 0.9f;
                var direction = math.normalize(random.NextFloat3Direction() * new float3(1f, 0.2f, 1f));
                var distance = random.NextFloat(0.1f, 6f);

                var expected = BruteCast(origin, radius, direction, distance, ignore, out var expectedDistance);
                var found = map.SphereCast(origin, radius, direction, distance, ignore, out var hit);

                Assert.That(found, Is.EqualTo(expected >= 0));
                if (!found)
                {
                    continue;
                }

                hits++;
                Assert.That(hit.Distance, Is.EqualTo(expectedDistance).Within(1e-4f));
                Assert.That(math.length(hit.Normal), Is.EqualTo(1f).Within(1e-3f));
                Assert.That(math.distance(hit.Position, _positions[hit.Index]),
                    Is.EqualTo(_radii[hit.Index]).Within(1e-3f));
            }

            Assert.That(hits, Is.GreaterThan(100));
        }

        private void AssertMatchesBruteForce(SpatialSphereMapConfig config, uint seed)
        {
            Build(config);
            var map = _map.AsReadOnly();
            Assert.That(map.Count, Is.EqualTo(_positions.Length));

            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);
            foreach (var position in _positions)
            {
                boundsMin = math.min(boundsMin, position);
                boundsMax = math.max(boundsMax, position);
            }

            var margin = math.cmax(boundsMax - boundsMin) * 0.1f + 1f;
            var random = new Random(seed * 7919u + 1u);
            var total = 0;
            for (var i = 0; i < QueriesPerCase; i++)
            {
                var center = random.NextFloat3(boundsMin - margin, boundsMax + margin);
                var radius = i % 10 == 0 ? random.NextFloat(5f, 20f) : random.NextFloat(0f, 2f);
                var sphere = Collect(map.SpheresInSphere(center, radius));
                Assert.That(sphere, Is.EquivalentTo(BruteSphere(center, radius)));
                total += sphere.Count;

                var halfSize = random.NextFloat3(0f, i % 10 == 0 ? 15f : 2f);
                var box = Collect(map.SpheresInAabb(Box(center - halfSize, center + halfSize)));
                Assert.That(box, Is.EquivalentTo(BruteAabb(center - halfSize, center + halfSize)));
                total += box.Count;

                var rotation = random.NextQuaternionRotation();
                var oriented = new List<int>();
                foreach (var hit in map.SpheresInBox(center, rotation, halfSize))
                {
                    oriented.Add(hit.Index);
                }

                Assert.That(oriented, Is.EquivalentTo(BruteBox(center, rotation, halfSize)));
            }

            Assert.That(total, Is.GreaterThan(0));
        }

        private static AABB Box(float3 min, float3 max)
        {
            return new AABB { Center = (min + max) * 0.5f, Extents = (max - min) * 0.5f };
        }

        private void Add(float3 position, float radius)
        {
            _positions.Add(position);
            _radii.Add(radius);
        }

        private void AddCrowd(ref Random random, int count, float halfSize, float height, float minRadius,
            float maxRadius)
        {
            for (var i = 0; i < count; i++)
            {
                Add(random.NextFloat3(new float3(-halfSize, 0f, -halfSize), new float3(halfSize, height, halfSize)),
                    random.NextFloat(minRadius, maxRadius));
            }
        }

        private void Build(SpatialSphereMapConfig config)
        {
            _map = new SpatialSphereMap(config);
            Rebuild();
        }

        private void Rebuild()
        {
            _map.Build(_positions, _radii, default).Complete();
        }

        private static List<int> Collect(SpatialSphereMapEnumerators.AabbEnumerator entries)
        {
            var indices = new List<int>();
            foreach (var entry in entries)
            {
                indices.Add(entry.Index);
            }

            return indices;
        }

        private static List<int> Collect(SpatialSphereMapEnumerators.SphereEnumerator entries)
        {
            var indices = new List<int>();
            foreach (var entry in entries)
            {
                indices.Add(entry.Index);
            }

            return indices;
        }

        private List<int> BruteSphere(float3 center, float radius)
        {
            var indices = new List<int>();
            for (var i = 0; i < _positions.Length; i++)
            {
                var reach = radius + _radii[i];
                if (math.distancesq(_positions[i], center) <= reach * reach)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private List<int> BruteAabb(float3 min, float3 max)
        {
            var indices = new List<int>();
            for (var i = 0; i < _positions.Length; i++)
            {
                var outside = math.max(math.max(min - _positions[i], _positions[i] - max), 0f);
                if (math.lengthsq(outside) <= _radii[i] * _radii[i])
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private List<int> BruteBox(float3 center, quaternion rotation, float3 halfExtents)
        {
            var indices = new List<int>();
            var inverse = math.inverse(rotation);
            for (var i = 0; i < _positions.Length; i++)
            {
                var local = math.rotate(inverse, _positions[i] - center);
                var outside = math.max(math.abs(local) - halfExtents, 0f);
                if (math.lengthsq(outside) <= _radii[i] * _radii[i])
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private int BruteCast(float3 origin, float radius, float3 direction, float maxDistance, int ignore,
            out float bestDistance)
        {
            var best = -1;
            bestDistance = float.MaxValue;
            for (var i = 0; i < _positions.Length; i++)
            {
                if (i == ignore)
                {
                    continue;
                }

                var offset = origin - _positions[i];
                var reach = radius + _radii[i];
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

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }
    }
}
