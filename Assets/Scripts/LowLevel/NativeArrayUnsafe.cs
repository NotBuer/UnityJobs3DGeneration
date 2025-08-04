using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace LowLevel  
{
    public static unsafe class NativeArrayUnsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<T> AsNativeArray<T>(UnsafeList<T>* list)
            where T : unmanaged
        {
            var array =
                NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                    list->Ptr, list->Length, Allocator.None);
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(
                ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            
            return array;
        } 
    }
}

