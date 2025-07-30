using System;
using System.Runtime.InteropServices;
using UnityEngine.Serialization;

namespace Voxel
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelData
    {
        public VoxelType type;

        public VoxelData(VoxelType type)
        {
            this.type = type;
        }
    }
}
