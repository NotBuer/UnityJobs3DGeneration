using System;
using System.Runtime.InteropServices;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ChunkCoord
{
    public int x;
    public int z;

    public ChunkCoord(int x, int z)
    {
        this.x = x;
        this.z = z;
    }
}