using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FireAlt.Core.Tests
{
    public class SpatialMapTests
    {
        [Test]
        public void Build_GroupsIndicesByCell_AndRebuildReplacesContents()
        {
            var map = new SpatialMap<SpatialPosition2D>(1f);
            var positions = new NativeList<SpatialPosition2D>(Allocator.Persistent);

            try
            {
                positions.Add(new float2(0.5f, 0.5f));
                positions.Add(new float2(0.9f, 0.1f));
                positions.Add(new float2(-3f, 7f));
                map.Build(positions, float2.zero, default).Complete();

                var readOnly = map.AsReadOnly();
                Assert.That(IndicesAt(readOnly, new float2(0.2f, 0.7f)), Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(IndicesAt(readOnly, new float2(-2.5f, 7.5f)), Is.EquivalentTo(new[] { 2 }));
                Assert.That(IndicesAt(readOnly, new float2(10f, 10f)), Is.Empty);

                positions.Clear();
                for (var i = 0; i < 200; i++)
                {
                    positions.Add(new float2(i + 0.5f, -5f));
                }

                map.Build(positions, new float2(40f, 0f), default).Complete();

                readOnly = map.AsReadOnly();
                Assert.That(readOnly.IndexMap.Count(), Is.EqualTo(200));
                Assert.That(IndicesAt(readOnly, new float2(0.2f, 0.7f)), Is.Empty);
                for (var i = 0; i < 200; i++)
                {
                    Assert.That(IndicesAt(readOnly, new float2(i + 0.25f, -4.5f)), Is.EquivalentTo(new[] { i }));
                }
            }
            finally
            {
                positions.Dispose();
                map.Dispose();
            }
        }

        [Test]
        public void ScheduleFindNearestNJob_ReturnsClosestEntitiesWithinRadius()
        {
            var map = new EntitySpatialMap(1f);
            var results = new NativeList<EntityWithDistance>(Allocator.Persistent);

            try
            {
                for (var i = 0; i < 50; i++)
                {
                    map.Positions.Add(new SpatialPosition { Position = new float3(i, 0f, 0f) });
                    map.Entities.Add(new Entity { Index = i, Version = 1 });
                }

                map.Spatial.Build(map.Positions, float2.zero, default).Complete();

                var input = new FindNearestNInput(new float3(10.2f, 0f, 0f), 5f, 3, true);
                map.ScheduleFindNearestNJob(ref results, input, default).Complete();

                Assert.That(results.Length, Is.EqualTo(3));
                Assert.That(results[0].Entity.Index, Is.EqualTo(10));
                Assert.That(results[1].Entity.Index, Is.EqualTo(11));
                Assert.That(results[2].Entity.Index, Is.EqualTo(9));

                input = new FindNearestNInput(new float3(10.2f, 0f, 30f), 5f, 3);
                map.ScheduleFindNearestNJob(ref results, input, default).Complete();

                Assert.That(results.Length, Is.EqualTo(0));
            }
            finally
            {
                results.Dispose();
                map.Positions.Dispose();
                map.Entities.Dispose();
                map.Dispose();
            }
        }

        private static List<int> IndicesAt(SpatialMap.ReadOnly map, float2 position)
        {
            var indices = new List<int>();
            foreach (var index in map.IndexMap.GetValuesForKey(map.Hash(map.Quantized(position))))
            {
                indices.Add(index);
            }

            return indices;
        }
    }
}
