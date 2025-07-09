using System;
using System.Runtime.InteropServices;

namespace DefaultNamespace
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkCoord
    {
        public short x;
        public short y;
        public short z;

        public ChunkCoord(short x, short y, short z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}