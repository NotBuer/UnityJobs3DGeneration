using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;

namespace World
{
    [BurstCompile]
    public static class WorldUtils
    {
        /// <summary>
        /// Determines the chunk coordinates corresponding to a given world position.
        /// The chunk coordinates are calculated based on the world position divided by the chunk size.
        /// </summary>
        /// <param name="worldPosition">The position in 3D world space that needs to be converted to chunk coordinates.</param>
        /// <param name="chunkSize">The size of each chunk, used to calculate the chunk boundaries.</param>
        /// <param name="position">The resulting chunk coordinates represented as integer values.</param>
        /// <returns>A Vector2Int representing the chunk coordinates derived from the world position.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetChunkCoordinateFromWorldPosition(
            in Vector3 worldPosition, in byte chunkSize, out Vector2Int position)
            => position = new Vector2Int(
                x: Mathf.FloorToInt(worldPosition.x / chunkSize) * chunkSize,
                y: Mathf.FloorToInt(worldPosition.z / chunkSize) * chunkSize);

        /// <summary>
        /// Converts a grid position to a corresponding chunk position in world coordinates.
        /// This is determined by multiplying the grid position by the size of each chunk,
        /// effectively scaling the grid coordinates to the world space dimensions of the chunk.
        /// </summary>
        /// <param name="gridPosition">The position in the 2D grid space, typically represented as grid-based coordinates.</param>
        /// <param name="chunkSize">The size of each chunk along one axis, used for scaling the grid position.</param>
        /// <param name="position">The resulting chunk position in world space, calculated from the grid position.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetGridPositionAsChunkPosition(
            in Vector2Int gridPosition, in byte chunkSize, out Vector2Int position)
                => position = new Vector2Int(x: gridPosition.x * chunkSize, y: gridPosition.y * chunkSize);
    }
}
