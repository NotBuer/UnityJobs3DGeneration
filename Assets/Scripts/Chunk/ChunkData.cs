using System;
using System.Runtime.InteropServices;

namespace Chunk
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkData : IEquatable<ChunkData>
    {
        public int x;
        public int z;

        public ChunkData(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public bool Equals(ChunkData other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, z);
        }
    }   
}