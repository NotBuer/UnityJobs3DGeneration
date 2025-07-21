using System;
using System.Runtime.InteropServices;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct VoxelData : IEquatable<VoxelData>
{
    public byte x;
    public byte y;
    public byte z;

    public VoxelData(byte x, byte y, byte z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public bool Equals(VoxelData other)
    {
        return x == other.x && y == other.y && z == other.z;
    }
    
    public override bool Equals(object obj)
    {
        return obj is VoxelData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, z);
    }
}
