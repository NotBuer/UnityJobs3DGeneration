using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    [BurstCompile]
    public static class VoxelUtils
    {
        public const byte FaceCount = 6;
        public const byte FaceEdges = 4;
        
        public static readonly Vector3[] Vertices = new Vector3[8]
        {
            new(-0.5f, -0.5f,  0.5f), // Back bottom-left
            new( 0.5f, -0.5f,  0.5f), // Back bottom-right
            new( 0.5f, -0.5f, -0.5f), // Front bottom-right
            new(-0.5f, -0.5f, -0.5f), // Front bottom-left
            new(-0.5f,  0.5f,  0.5f), // Back top-left
            new( 0.5f,  0.5f,  0.5f), // Back top-right
            new( 0.5f,  0.5f, -0.5f), // Front top-right
            new(-0.5f,  0.5f, -0.5f)  // Front top-left     
        };

        public static readonly Vector3Int[] Normals = new Vector3Int[6]
        {
            new( 0,  0, -1), // Front Face
            new( 0,  0,  1), // Back Face
            new(-1,  0,  0), // Left Face
            new( 1,  0,  0), // Right Face
            new( 0, -1,  0), // Bottom Face
            new( 0,  1,  0)  // Top Face
        };
        
        public static readonly int3[] NormalsInt3 = new int3[6]
        {
            new( 0,  0, -1), // Front Face
            new( 0,  0,  1), // Back Face
            new(-1,  0,  0), // Left Face
            new( 1,  0,  0), // Right Face
            new( 0, -1,  0), // Bottom Face
            new( 0,  1,  0)  // Top Face
        };

        public static readonly byte[] FaceVertices = new byte[6 * 4]
        {
            3, 2, 6, 7, // Front Face (Z-)
            1, 0, 4, 5, // Back Face (Z+)
            3, 7, 4, 0, // Left Face (X-)
            1, 5, 6, 2, // Right Face (X+)
            3, 0, 1, 2, // Bottom Face (Y-)
            4, 7, 6, 5  // Top Face (Y+)   
        };
        
        private static readonly Color32 GrassColor = new(120, 200, 100, 255);
        private static readonly Color32 DirtColor = new(139, 69, 19, 255);
        private static readonly Color32 StoneColor = new(150, 150, 150, 255);
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 GetVoxelColor(
            in VoxelType voxelType)
        {
            return voxelType switch
            {
                VoxelType.Grass => GrassColor,
                VoxelType.Dirt => DirtColor,
                VoxelType.Stone => StoneColor,
                
                _ => throw new ArgumentOutOfRangeException(nameof(voxelType), voxelType, null)
            };
        }
    }
}
