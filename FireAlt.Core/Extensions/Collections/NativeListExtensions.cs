using FireAlt.Core.Internal;
using Unity.Collections;
using Unity.Mathematics;

namespace FireAlt.Core.Extensions
{
    public static class NativeListExtensions
    {
        public static string ToListString<T>(this NativeList<T> list)
            where T : unmanaged
        {
            var s = "";
            foreach (var value in list)
            {
                s += value + ", ";
            }
            return s;
        }
        
        public static void Remove<T>(this NativeList<T> list, T element)
            where T : unmanaged
        {
            for (int i = list.Length - 1; i >= 0; i--)
            {
                if (list[i].GetHashCode() == element.GetHashCode() && list[i].Equals(element))
                {
                    list.RemoveAt(i);
                }
            }
        }
        
        public static void EnsureCapacity<T>(this NativeList<T> list, int minCapacity, bool setLengthNoClear = false) where T : unmanaged
        {
            var targetCapacity = math.max(list.Capacity, minCapacity);

            if (list.Capacity < targetCapacity)
            {
                var newCapacity = math.ceilpow2(math.max(targetCapacity, list.Capacity * 2));
                list.Capacity = newCapacity;
            }
            
            if (setLengthNoClear)
            {
                list.SetLengthNoClear(minCapacity);
            }
        }
        
        public static unsafe void SetLengthNoClear<T>(this NativeList<T> list, int length) where T : unmanaged
        {
            var data = list.GetListData();
            data->Length = length;
        }
        
        public static void Reverse<T>(this NativeList<T> list) where T : unmanaged
        {
            int count = list.Length;
            int halfLength = count / 2;
            
            for (int i = 0; i < halfLength; i++)
            {
                int oppositeIndex = count - i - 1;
                
                (list[i], list[oppositeIndex]) = (list[oppositeIndex], list[i]);
            }
        }
        
        public static T[] ToManagedArray<T>(this NativeList<T> nativeList) where T : unmanaged
        {
            var managedArray = new T[nativeList.Length];
        
            for (int i = 0; i < nativeList.Length; i++)
            {
                managedArray[i] = nativeList[i];
            }
            return managedArray;
        }
    }
}