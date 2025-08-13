using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace LowLevel
{
    [BurstCompile]
    public static unsafe class DynamicBufferUnsafe
    {
        /// <summary>
        /// Creates a dynamic buffer for the specified entity using the provided command buffer and copies data from a source native list into the new buffer.
        /// </summary>
        /// <typeparam name="TSource">The type of the source elements in the native list.</typeparam>
        /// <typeparam name="TDest">The type of the elements in the destination dynamic buffer. Must implement <see cref="IBufferElementData"/> and be unmanaged.</typeparam>
        /// <param name="commandBuffer">The parallel writer for the entity command buffer into which the dynamic buffer will be added.</param>
        /// <param name="sortKey">The sort key used to maintain the execution order of commands in the command buffer.</param>
        /// <param name="entity">The entity to which the dynamic buffer should be added.</param>
        /// <param name="list">The native list containing the source data to copy to the new dynamic buffer.</param>
        /// <param name="buffer">The resulting dynamic buffer created and populated with data from <paramref name="list"/>.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddAndCopyBufferFrom<TSource, TDest>(
            ref EntityCommandBuffer.ParallelWriter commandBuffer, 
            in int sortKey, 
            in Entity entity, 
            in NativeList<TSource> list,
            out DynamicBuffer<TDest> buffer)
            where TDest : unmanaged, IBufferElementData
            where TSource : unmanaged
        {
            buffer = commandBuffer.AddBuffer<TDest>(sortKey, entity);
            buffer.ResizeUninitialized(list.Length);
            UnsafeUtility.MemCpy(
                buffer.GetUnsafePtr(),
                list.GetUnsafeReadOnlyPtr(),
                list.Length * UnsafeUtility.SizeOf<TDest>());
        }
    }
}