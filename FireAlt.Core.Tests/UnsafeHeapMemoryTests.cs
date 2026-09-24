using System;
using FireAlt.Core.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core.Tests
{
    public class UnsafeHeapMemoryTests
    {
        [Test]
        public void AllocateAndFree_ReusesBestFitBlock_AndTracksRange()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), 4, Allocator.Persistent);

            try
            {
                var a = heap.Allocate(4);
                var b = heap.Allocate(3);

                Assert.That(a, Is.EqualTo(new MemoryPtr(0, 4)));
                Assert.That(b, Is.EqualTo(new MemoryPtr(4, 3)));
                Assert.That(heap.TryGetValidRange(out var initialRange), Is.True);
                Assert.That(initialRange, Is.EqualTo(new int2(0, 6)));

                heap.Free(a);
                var c = heap.Allocate(2);

                Assert.That(c, Is.EqualTo(new MemoryPtr(0, 2)));
                Assert.That(heap.TryGetValidRange(out var range), Is.True);
                Assert.That(range, Is.EqualTo(new int2(0, 6)));
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void ExpandCapacity_PreservesExistingAllocationDataAndIndices()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), 2, Allocator.Persistent);

            try
            {
                var first = heap.Allocate(2);
                heap.ElementAt<int>(first, 0) = 11;
                heap.ElementAt<int>(first, 1) = 22;

                var second = heap.Allocate(64);
                heap.ElementAt<int>(second, 0) = 101;
                heap.ElementAt<int>(second, 63) = 202;

                Assert.That(first, Is.EqualTo(new MemoryPtr(0, 2)));
                Assert.That(heap.ElementAt<int>(first, 0), Is.EqualTo(11));
                Assert.That(heap.ElementAt<int>(first, 1), Is.EqualTo(22));
                Assert.That(heap.ElementAt<int>(second, 0), Is.EqualTo(101));
                Assert.That(heap.ElementAt<int>(second, 63), Is.EqualTo(202));
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void FreeTailBlocks_ShrinksUsedLength_AndUpdatesRange()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), Allocator.Persistent);

            try
            {
                var a = heap.Allocate(2);
                var b = heap.Allocate(3);
                var c = heap.Allocate(4);

                Assert.That(heap.UsedLength, Is.EqualTo(9));
                Assert.That(heap.TryGetValidRange(out var fullRange), Is.True);
                Assert.That(fullRange, Is.EqualTo(new int2(0, 8)));

                heap.Free(c);
                Assert.That(heap.UsedLength, Is.EqualTo(5));
                Assert.That(heap.TryGetValidRange(out var afterTailFree), Is.True);
                Assert.That(afterTailFree, Is.EqualTo(new int2(0, 4)));

                heap.Free(a);
                Assert.That(heap.TryGetValidRange(out var onlyMiddleLive), Is.True);
                Assert.That(onlyMiddleLive, Is.EqualTo(new int2(2, 4)));

                heap.Free(b);
                Assert.That(heap.UsedLength, Is.EqualTo(0));
                Assert.That(heap.TryGetValidRange(out _), Is.False);
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void GetValidRange_ReturnsMinMaxAcrossSparseLiveAllocations()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), Allocator.Persistent);

            try
            {
                var a = heap.Allocate(2);
                var b = heap.Allocate(3);
                var c = heap.Allocate(2);

                heap.Free(b);

                Assert.That(heap.TryGetValidRange(out var sparseRange), Is.True);
                Assert.That(sparseRange, Is.EqualTo(new int2(a.StartIndex, c.EndIndex)));

                heap.Free(a);
                Assert.That(heap.TryGetValidRange(out var tailOnlyRange), Is.True);
                Assert.That(tailOnlyRange, Is.EqualTo(new int2(c.StartIndex, c.EndIndex)));
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void Free_WithInvalidMemoryPtr_Throws()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), Allocator.Persistent);

            try
            {
                var ptr = heap.Allocate(3);

                Assert.Throws<ArgumentException>(() => heap.Free(new MemoryPtr(ptr.StartIndex, 2)));

                heap.Free(ptr);
                Assert.Throws<InvalidOperationException>(() => heap.Free(ptr));
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void Allocate_FromNativeArray_CopiesData()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), Allocator.Persistent);
            var source = new NativeArray<int>(new[] { 5, 6, 7, 8 }, Allocator.Temp);

            try
            {
                var ptr = heap.Allocate(source);

                Assert.That(ptr, Is.EqualTo(new MemoryPtr(0, 4)));
                Assert.That(heap.ElementAt<int>(ptr, 0), Is.EqualTo(5));
                Assert.That(heap.ElementAt<int>(ptr, 1), Is.EqualTo(6));
                Assert.That(heap.ElementAt<int>(ptr, 2), Is.EqualTo(7));
                Assert.That(heap.ElementAt<int>(ptr, 3), Is.EqualTo(8));
            }
            finally
            {
                heap.Dispose();
            }
        }

        [Test]
        public void StrideMismatch_Throws()
        {
            var heap = new UnsafeHeapMemory(UnsafeUtility.SizeOf<int>(), Allocator.Persistent);
            var shorts = new NativeArray<short>(new short[] { 1, 2, 3 }, Allocator.Temp);

            try
            {
                Assert.Throws<ArgumentException>(() => heap.Allocate(shorts));
            }
            finally
            {
                shorts.Dispose();
                heap.Dispose();
            }
        }

        [Test]
        public void InvalidStride_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new UnsafeHeapMemory(0, Allocator.Persistent));
        }
    }
}
