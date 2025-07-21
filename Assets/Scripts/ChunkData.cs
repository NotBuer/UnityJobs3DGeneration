using System;
using System.Runtime.InteropServices;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ChunkData : IEquatable<ChunkData>
{
    public float x;
    public float z;

    public ChunkData(float x, float z)
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