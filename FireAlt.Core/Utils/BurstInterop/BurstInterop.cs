// <copyright file="BurstTrampoline.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Scripting.LifecycleManagement;

namespace FireAlt.Core.Utility
{
    /// <summary>
    /// Packs managed callback arguments into a single pointer and size payload so the same unmanaged wrapper can dispatch any signature.
    /// </summary>
    [NoAutoStaticsCleanup]
    public readonly unsafe struct BurstInterop
    {
        private static GCHandle _cachedWrapperHandle;

        private static IntPtr _cachedWrapperPtr;

        [NativeDisableUnsafePtrRestriction]
        private readonly IntPtr _managedFunctionPtr;

        [NativeDisableUnsafePtrRestriction]
        private readonly IntPtr _wrapperPtr;

        /// <summary>
        /// Initializes a new instance of the <see cref="BurstInterop"/> struct.
        /// </summary>
        /// <param name="managedFunctionPtr">
        /// Callback with a single payload pointer and payload size.
        /// </param>
        public BurstInterop(delegate*<void*, int, void> managedFunctionPtr)
        {
            Initialize();
            _wrapperPtr = _cachedWrapperPtr;
            _managedFunctionPtr = new IntPtr(managedFunctionPtr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void WrapperDelegate(void* managedFunctionPtr, void* argumentsPtr, int argumentsSize);

        public bool IsCreated => _managedFunctionPtr != default;

        [MonoPInvokeCallback(typeof(WrapperDelegate))]
        private static void Wrapper(void* managedFunctionPtr, void* argumentsPtr, int argumentsSize)
        {
            ((delegate*<void*, int, void>)managedFunctionPtr)(argumentsPtr, argumentsSize);
        }

        private static void Initialize()
        {
            if (_cachedWrapperPtr != default)
            {
                return;
            }

            WrapperDelegate wrapperDelegate = Wrapper;
            _cachedWrapperHandle = GCHandle.Alloc(wrapperDelegate);
            _cachedWrapperPtr = Marshal.GetFunctionPointerForDelegate(wrapperDelegate);
        }

        public void Invoke(void* argumentsPtr, int argumentsSize)
        {
            if (_managedFunctionPtr == default)
            {
                throw new NullReferenceException("Trying to invoke a null function pointer.");
            }

            ((delegate* unmanaged[Cdecl]<void*, void*, int, void>)_wrapperPtr)(
                (void*)_managedFunctionPtr,
                argumentsPtr,
                argumentsSize);
        }

        public void Invoke<T>(ref T arguments)
            where T : unmanaged
        {
            fixed (T* argumentsPtr = &arguments)
            {
                Invoke(argumentsPtr, UnsafeUtility.SizeOf<T>());
            }
        }

        public static ref T ArgumentsFromPtr<T>(void* argumentsPtr, int size)
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (size != UnsafeUtility.SizeOf<T>())
            {
                throw new InvalidOperationException("The requested argument type size does not match the provided one.");
            }
#endif
            return ref *(T*)argumentsPtr;
        }
    }
}
