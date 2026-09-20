using FireAlt.Core.Internal;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// A job-safe bit-mask container for tracking active indices and querying the active span quickly.
    /// </summary>
    /// <remarks>
    /// Use this when you need compact on/off state for integer indices and frequent boundary queries without scanning every slot.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Mark and unmark states with <see cref="Set"/> and <see cref="Unset"/> as work becomes active/inactive.</description></item>
    /// <item><description>Check membership with <see cref="IsSet"/> during validation or filtering.</description></item>
    /// <item><description>Process only active bounds by calling <see cref="TryGetFirstSet"/>, <see cref="TryGetLastSet"/>, or <c>TryGetRange</c> before iterating.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Count = {Count}, Length = {Length}, Levels = {LevelCount}, IsCreated = {IsCreated}")]
    public struct NativeBitMaskRange : IDisposable
    {
        private UnsafeBitMaskRange _range;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeBitMaskRange>();
#endif

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _range.Length;
            }
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _range.Count;
            }
        }

        public int LevelCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _range.LevelCount;
            }
        }

        public bool IsCreated => _range.IsCreated;

        public NativeBitMaskRange(AllocatorManager.AllocatorHandle allocator)
            : this(0, allocator)
        {
        }

        public NativeBitMaskRange(int length, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            _range = new UnsafeBitMaskRange(length, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _range.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(int index)
        {
            CheckWrite();
            return _range.Set(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unset(int index)
        {
            CheckWrite();
            return _range.Unset(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int index)
        {
            CheckRead();
            return _range.IsSet(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _range.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetFirstSet(out int index)
        {
            CheckRead();
            return _range.TryGetFirstSet(out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLastSet(out int index)
        {
            CheckRead();
            return _range.TryGetLastSet(out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRange(out int startIndex, out int endIndex)
        {
            CheckRead();
            return _range.TryGetRange(out startIndex, out endIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRange(out int2 range)
        {
            CheckRead();
            return _range.TryGetRange(out range);
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            _range.Dispose();
            _range = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSafety(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionChecks.InitNativeContainer<ulong>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativeBitMaskRange>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }
}
