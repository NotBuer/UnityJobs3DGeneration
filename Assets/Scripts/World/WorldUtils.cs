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
        /// Calculates the chunk coordinates (x, z) in the world grid from a given world position.
        /// The coordinates are determined by dividing the world position by the chunk size and flooring the result
        /// to ensure the values correspond to the lower bounds (bottom-left snapping) of the chunk the position belongs to.
        /// </summary>
        /// <param name="worldPosition">The 3D position in the world space.</param>
        /// <param name="chunkSize">The size of each chunk along one axis.</param>
        /// <returns>A Vector2Int representing the chunk coordinates (x, z) in the grid.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int GetChunkCoordinateFromWorldPosition(Vector3 worldPosition, in byte chunkSize)
            => new(
                x: Mathf.FloorToInt(worldPosition.x / chunkSize) * chunkSize,
                y: Mathf.FloorToInt(worldPosition.z / chunkSize) * chunkSize);

        /// <summary>
        /// Converts a grid position into the corresponding chunk position by scaling the grid coordinates using the chunk size.
        /// </summary>
        /// <param name="gridPosition">The grid position represented as a 2D vector (x, y).</param>
        /// <param name="chunkSize">The size of each chunk along one axis in grid units.</param>
        /// <returns>A 2D vector representing the chunk position corresponding to the given grid position.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int GetGridPositionAsChunkPosition(Vector2Int gridPosition, in byte chunkSize)
            => new(x: gridPosition.x * chunkSize, y: gridPosition.y * chunkSize);
    }
}
