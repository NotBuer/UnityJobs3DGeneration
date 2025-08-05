using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort FlattenGridToIndex(in int x, in int z, in byte totalChunksPerAxis)
            => (ushort)(z * totalChunksPerAxis + x);
    }
}
