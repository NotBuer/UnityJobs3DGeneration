using System;
using System.Runtime.CompilerServices;
using Chunk;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace World
{
    public static class WorldUtils
    {
        /// <summary>
        /// Converts a 1D index into 2D grid coordinates (x, z), based on the total number of chunks per axis.
        /// X is the column (horizontal) position, obtained by the remainder of the index divided by the number of chunks per axis.
        /// Z is the row (vertical) position, obtained by the integer division of the index by the number of chunks per axis.
        /// </summary>
        /// <param name="index">A 1D index representing a position in the grid.</param>
        /// <param name="totalChunksPerAxis">The total number of chunks along one axis of the grid.</param>
        /// <returns>A tuple containing the 2D grid coordinates (x, z) corresponding to the given 1D index.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int z) UnflattenIndexToGrid(in ushort index, in byte totalChunksPerAxis)
            => (index % totalChunksPerAxis, index / totalChunksPerAxis);

        /// <summary>
        /// Converts 2D grid coordinates (x, z) into a 1D index, based on the total number of chunks per axis.
        /// The index is calculated by multiplying the z-coordinate (row) by the total number of chunks per axis
        /// and adding the x-coordinate (column).
        /// </summary>
        /// <param name="x">The column (horizontal) position in the grid.</param>
        /// <param name="z">The row (vertical) position in the grid.</param>
        /// <param name="totalChunksPerAxis">The total number of chunks along one axis of the grid.</param>
        /// <returns>A 1D index representing the position in the grid.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort FlattenGridToIndex(in int x, in int z, in byte totalChunksPerAxis)
            => (ushort)(z * totalChunksPerAxis + x);

        /// <summary>
        /// Retrieves the bottom-left corner position of the chunk containing the given coordinates (x, z).
        /// Iterates through the provided chunk data to find the chunk whose bounds include the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the position to locate.</param>
        /// <param name="z">The z-coordinate of the position to locate.</param>
        /// <param name="chunkSize">The size of each chunk along one axis.</param>
        /// <param name="chunkDataArray">A native array containing data about all chunks in the grid.</param>
        /// <returns>A Vector2Int representing the bottom-left corner coordinates of the chunk that contains the specified position.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the given position does not fall within any chunk.</exception>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int GetPositionInChunkCoordinate(
            in int x, in int z, in byte chunkSize, in NativeArray<ChunkData> chunkDataArray)
        {
            foreach (var chunkData in chunkDataArray)
            {   
                // Check if is inside from the chunk Bottom-Left to Top-Right bounding box.
                if (x >= chunkData.x && x <= chunkData.x + chunkSize &&
                    z >= chunkData.z && z <= chunkData.z + chunkSize)
                {
                    return new Vector2Int(chunkData.x, chunkData.z);
                }
            }
            throw new InvalidOperationException(
                $"{nameof(WorldUtils)}.{nameof(GetPositionInChunkCoordinate)} - Position couldn't be found in any chunk!!!");
        }
    }
}
