using System;
using System.Runtime.CompilerServices;
using Chunk;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace World
{
    [BurstCompile]
    public static class WorldUtils
    {
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
        public static Vector2Int GetChunkCoordinateFromWorldPosition(in Vector3 worldPosition, in byte chunkSize)
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
        public static Vector2Int GetGridPositionAsChunkPosition(in Vector2Int gridPosition, in byte chunkSize)
            => new(x: gridPosition.x * chunkSize, y: gridPosition.y * chunkSize);
    }
}
