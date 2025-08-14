using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Chunk
{
    [BurstCompile]
    public static class ChunkUtils
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetChunkTotalSize(in byte chunkSize, in byte chunkSizeY) =>
            chunkSize * chunkSize * chunkSizeY;
        
        /// <summary>
        /// Flattens 3D local coordinates within a chunk into a 1D index.
        /// </summary>
        /// <param name="voxelIndex">The initial index offset for the chunk's voxel data in a larger array.</param>
        /// <param name="voxelX">The X-coordinate of the voxel within the chunk.</param>
        /// <param name="voxelY">The Y-coordinate of the voxel within the chunk.</param>
        /// <param name="voxelZ">The Z-coordinate of the voxel within the chunk.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="chunkSizeY">The height of the chunk in number of voxels.</param>
        /// <returns>A single integer representing the 1D index of the voxel within the chunk's linear array.</returns>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Flatten3DLocalCoordsToIndex(
            int voxelIndex, int voxelX, int voxelY, int voxelZ, int chunkSize, int chunkSizeY) 
            => voxelIndex + 
               (voxelX * chunkSize * chunkSizeY) +
               (voxelZ * chunkSizeY) + 
               voxelY;

        /// <summary>
        /// Converts a 1D local voxel index to 3D local coordinates within a chunk.
        /// </summary>
        /// <param name="localVoxelIndex">The 1D index of the voxel in the chunk.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="chunkSizeY">The height of the chunk in number of voxels.</param>
        /// <param name="x">The X-coordinate of the voxel within the chunk, returned by this method.</param>
        /// <param name="y">The Y-coordinate of the voxel within the chunk, returned by this method.</param>
        /// <param name="z">The Z-coordinate of the voxel within the chunk, returned by this method.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnflattenIndexTo3DLocalCoords(
            int localVoxelIndex, int chunkSize, int chunkSizeY,
            out int x, out int y, out int z)
        {
            x = localVoxelIndex / (chunkSize * chunkSizeY);
            y = localVoxelIndex % chunkSizeY;
            z = (localVoxelIndex / chunkSizeY) % chunkSize;
        }

        /// <summary>
        /// Calculates the flattened 1D index from the given 3D position within a chunk.
        /// </summary>
        /// <param name="pos">The 3D local coordinates within the chunk.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="index">The resulting flattened 1D index that corresponds to the provided local 3D coordinates.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FlattenIndex(in int3 pos, byte chunkSize, out int index)
        {
            index = pos.x + (pos.z * chunkSize) + (pos.y * chunkSize * chunkSize);
        }

        /// <summary>
        /// Converts a flattened 1D index within a chunk back into 3D local coordinates.
        /// </summary>
        /// <param name="index">The flattened 1D index to be converted.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="x">The output X-coordinate of the voxel within the chunk.</param>
        /// <param name="y">The output Y-coordinate of the voxel within the chunk.</param>
        /// <param name="z">The output Z-coordinate of the voxel within the chunk.</param>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnflattenIndex(int index, byte chunkSize, out int x, out int y, out int z)
        {
            y = index / (chunkSize * chunkSize);
            index -= (y * chunkSize * chunkSize);
            z = index / chunkSize;
            x = index % chunkSize;
        }
    }
}
