using System;
using System.Runtime.InteropServices;

namespace Voxel
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelData
    {
        public VoxelType _type;

        public VoxelData(VoxelType type)
        {
            _type = type;
        }
    }
}
