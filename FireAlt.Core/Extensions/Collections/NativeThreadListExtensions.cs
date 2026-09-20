using System.Runtime.CompilerServices;
using FireAlt.Core.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;

namespace FireAlt.Core.Extensions
{
    public static class NativeThreadListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NativeArray<int> GetStartIndexArray<T>(this NativeThreadList<T> nativeThreadList, ref SystemState state)
            where T : unmanaged
        {
            return nativeThreadList._unsafeParallelList->GetStartIndexArray(ref state);
        }
        
        public static NativeArray<int> GetStartIndexArray<T>(this UnsafeThreadList<T> unsafeThreadList, ref SystemState state)
            where T : unmanaged
        {
            var lengths = CollectionHelper.CreateNativeArray<int>(JobsUtility.ThreadIndexCount, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            int count = 0;
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                lengths[i] = count;
                count += unsafeThreadList.GetPerThreadList(i).List.m_length;
            }

            return lengths;
        }
    }
}