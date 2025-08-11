using System.Runtime.CompilerServices;
using ECS;
using LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public static class ChunkMeshCore
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FirstPassGetVisibleFaces(
            in int chunkSizeInVoxels,
            in NativeArray<VoxelData> voxelDataArray,
            in NativeArray<VoxelData> voxelDataLocalViewArray,
            in Vector2Int chunkCoord,
            in byte chunkSize,
            in byte chunkSizeY,
            ref int visibleFaces, 
            in NativeParallelHashMap<Vector2Int, int>.ReadOnly coordTableHashMap)
        {
            for (var voxelIndex = 0; voxelIndex < chunkSizeInVoxels; voxelIndex++)
            {
                if (voxelDataLocalViewArray[voxelIndex].type == VoxelType.Air)
                    continue;
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    if (IsFaceVisible(
                            in chunkSizeInVoxels,
                            in chunkSize,
                            in chunkSizeY,
                            in voxelDataArray,
                            in voxelDataLocalViewArray,
                            in chunkCoord,
                            in voxelIndex,
                            in VoxelUtils.Normals[faceIndex],
                            in coordTableHashMap))
                    {
                        visibleFaces++;   
                    }
                }
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SecondPassBuildMeshData(
            in int chunkSizeInVoxels,
            in NativeArray<VoxelData> voxelDataArray,
            in NativeArray<VoxelData> voxelDataLocalViewArray,
            in Vector2Int chunkCoord,
            in byte chunkSize,
            in byte chunkSizeY,
            in int visibleFaces,
            out UnsafeList<Vector3>* vertices,
            out UnsafeList<int>* triangles,
            out UnsafeList<Vector3>* normals,
            out UnsafeList<Color32>* colors,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax,
            in NativeParallelHashMap<Vector2Int, int>.ReadOnly coordTableHashMap)
        {
            vertices = UnsafeList<Vector3>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.Temp);
            triangles = UnsafeList<int>.Create(visibleFaces * VoxelUtils.FaceCount, Allocator.Temp);
            normals = UnsafeList<Vector3>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.Temp);
            colors = UnsafeList<Color32>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.Temp);

            var vertexIndex = 0;
            
            for (var voxelIndex = 0; voxelIndex < chunkSizeInVoxels; voxelIndex++)
            {
                var voxelType = voxelDataLocalViewArray[voxelIndex].type;
                
                if (voxelType == VoxelType.Air)
                    continue;
                
                ChunkUtils.UnflattenIndexTo3DLocalCoords(
                    voxelIndex, chunkSize, chunkSizeY, out var x, out var y, out var z);
                
                var voxelPosition = new Vector3(
                    x + chunkCoord.x,
                    y,
                    z + chunkCoord.y);
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    if (!IsFaceVisible(
                            in chunkSizeInVoxels,
                            in chunkSize,
                            in chunkSizeY,
                            in voxelDataArray,
                            in voxelDataLocalViewArray,
                            in chunkCoord,
                            in voxelIndex,
                            in VoxelUtils.Normals[faceIndex],
                            in coordTableHashMap)) continue;
        
                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex =
                            voxelPosition +
                            VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                        
                        vertices->AddNoResize(vertex);
                        normals->AddNoResize(VoxelUtils.Normals[faceIndex]);
                        colors->AddNoResize(VoxelUtils.GetVoxelColor(in voxelType));
                        
                        boundsMin = Vector3.Min(boundsMin, vertex);
                        boundsMax = Vector3.Max(boundsMax, vertex);
                    }
                    
                    // Add 2 triangles for the face using an anti-clockwise direction.
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 3);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex + 1);
                    
                    vertexIndex += VoxelUtils.FaceEdges;
                }
            }
        }
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFaceVisible(
            in int chunkSizeInVoxels, 
            in byte chunkSize,
            in byte chunkSizeY,
            in NativeArray<VoxelData> voxelDataArray,
            in NativeArray<VoxelData> voxelDataChunkViewArray,
            in Vector2Int chunkCoord,
            in int voxelIndex,
            in Vector3Int normal,
            in NativeParallelHashMap<Vector2Int, int>.ReadOnly coordTableHashMap)
        {
            ChunkUtils.UnflattenIndexTo3DLocalCoords(
                voxelIndex, chunkSize, chunkSizeY, out var x, out var y, out var z);
        
            var neighborY = y + normal.y;
            if (neighborY < 0 || neighborY >= chunkSizeY) return true;
            
            var neighborX = x + normal.x;
            var neighborZ = z + normal.z;
            
            // Neighbor voxel is within the current chunk's local XZ bounds
            if (neighborX >= 0 && neighborX < chunkSize && neighborZ >= 0 && neighborZ < chunkSize)
            {
                var neighborVoxelIndex =
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborX, neighborY, neighborZ, chunkSize, chunkSizeY);
                
                return voxelDataChunkViewArray[neighborVoxelIndex].type == VoxelType.Air;
            }
            
            var neighborWorldPos = new Vector2Int(chunkCoord.x + neighborX, chunkCoord.y + neighborZ);
            var neighborChunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)neighborWorldPos.x / chunkSize) * chunkSize,
                Mathf.FloorToInt((float)neighborWorldPos.y / chunkSize) * chunkSize);
            
            // Look up the neighbor chunk's voxel data in the coord table.
            if (!coordTableHashMap.TryGetValue(neighborChunkCoord, out var neighborChunkIndex)) return true;
            
            // Calculate the voxel's local coordinates within its own chunk.
            var targetVoxelLocalX = (neighborWorldPos.x % chunkSize + chunkSize) % chunkSize;
            var targetVoxelLocalZ = (neighborWorldPos.y % chunkSize + chunkSize) % chunkSize;
            
            var neighborGlobalIndex = (neighborChunkIndex * chunkSizeInVoxels) + ChunkUtils.Flatten3DLocalCoordsToIndex(
                0, targetVoxelLocalX, neighborY, targetVoxelLocalZ, chunkSize, chunkSizeY);
                
            return voxelDataArray[neighborGlobalIndex].type == VoxelType.Air;
        }
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetMeshDataBuffers(
            ref Mesh.MeshData chunkMeshData,
            ref UnsafeList<Vector3>* vertices,
            ref UnsafeList<int>* triangles,
            ref UnsafeList<Vector3>* normals,
            ref UnsafeList<Color32>* colors)
        {
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>
                (3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            
            vertexAttributes[0] = new VertexAttributeDescriptor
                (VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
            vertexAttributes[1] = new VertexAttributeDescriptor
                (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
            vertexAttributes[2] = new VertexAttributeDescriptor
                (VertexAttribute.Color, VertexAttributeFormat.UNorm8, dimension: 4, stream: 2);
            
            chunkMeshData.SetVertexBufferParams(vertices->Length, vertexAttributes);
            vertexAttributes.Dispose();
            
            chunkMeshData.SetIndexBufferParams(triangles->Length, IndexFormat.UInt32);
            
            chunkMeshData.GetVertexData<Vector3>(0).CopyFrom(NativeArrayUnsafe.AsNativeArray(vertices));
            chunkMeshData.GetVertexData<Vector3>(1).CopyFrom(NativeArrayUnsafe.AsNativeArray(normals));
            chunkMeshData.GetVertexData<Color32>(2).CopyFrom(NativeArrayUnsafe.AsNativeArray(colors));
            chunkMeshData.GetIndexData<int>().CopyFrom(NativeArrayUnsafe.AsNativeArray(triangles));
            
            chunkMeshData.subMeshCount = 1;
            chunkMeshData.SetSubMesh(0, 
                new SubMeshDescriptor(0, triangles->Length), 
                MeshUpdateFlags.DontValidateIndices | 
                MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | 
                MeshUpdateFlags.DontRecalculateBounds);
        }
    }
}
