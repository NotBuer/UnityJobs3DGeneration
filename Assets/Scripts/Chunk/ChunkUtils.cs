using System.Runtime.CompilerServices;

namespace Chunk
{
    public static class ChunkUtils
    {
        /// <summary>
        /// Flattens 3D local coordinates into a 1D index relative to the start of the chunk.
        /// </summary>
        /// <param name="voxelX">The X-coordinate of the voxel within the chunk.</param>
        /// <param name="voxelY">The Y-coordinate of the voxel within the chunk.</param>
        /// <param name="voxelZ">The Z-coordinate of the voxel within the chunk.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="chunkSizeY">The height of the chunk in number of voxels.</param>
        /// <returns>A single integer representing the 1D index of the voxel, relative to its chunk.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Flatten3DLocalCoordsToIndex(
            int voxelX, int voxelY, int voxelZ, int chunkSize, int chunkSizeY)
            => (voxelX * chunkSize * chunkSizeY) +
               (voxelZ * chunkSizeY) +
               voxelY;
        
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Flatten3DLocalCoordsToIndex(
            int voxelIndex, int voxelX, int voxelY, int voxelZ, int chunkSize, int chunkSizeY) 
            => voxelIndex + 
               (voxelX * chunkSize * chunkSizeY) +
               (voxelZ * chunkSizeY) + 
               voxelY;
    
        /// <summary>
        /// Converts a 1D index within a chunk into its corresponding 3D local coordinates.
        /// </summary>
        /// <param name="localVoxelIndex">The 1D index of the voxel within the chunk's linear array.</param>
        /// <param name="chunkSize">The size (length and width) of the chunk in number of voxels.</param>
        /// <param name="chunkSizeY">The height of the chunk in number of voxels.</param>
        /// <returns>A tuple containing the X, Y, and Z local coordinates of the voxel within the chunk.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int x, int y, int z) UnflattenIndexTo3DLocalCoords(
            int localVoxelIndex, int chunkSize, int chunkSizeY)
            => (
                localVoxelIndex / (chunkSize * chunkSizeY),
                localVoxelIndex % chunkSizeY,
                (localVoxelIndex / chunkSizeY) % chunkSize 
            );
    }
}
