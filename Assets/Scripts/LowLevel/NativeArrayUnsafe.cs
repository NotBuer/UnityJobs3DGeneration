using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace LowLevel  
{
    /// <summary>
    /// Provides unsafe utility methods for interacting with NativeArray and other Native Collections.
    /// </summary>
    /// <remarks>
    /// This class includes operations that allow for direct manipulation or conversion of native collections
    /// in an unsafe context. The methods are primarily focused on performance-critical scenarios and require the user
    /// to handle memory management and safety explicitly. These utilities leverage Burst compilation and aggressive
    /// inlining for optimized execution.
    /// </remarks>
    [BurstCompile]
    public static unsafe class NativeArrayUnsafe
    {
        [BurstCompile]
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

