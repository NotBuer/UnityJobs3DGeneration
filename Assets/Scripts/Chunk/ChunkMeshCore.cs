using System.Runtime.CompilerServices;
using LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace Chunk
{
    public static class ChunkMeshCore
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FirstPassGetVisibleFacesLocal(
            in int chunkVoxelCount,
            in NativeArray<VoxelData> voxelDataArray,
            in byte chunkSize,
            in byte chunkSizeY,
            in byte totalChunksPerAxis,
            in int voxelStartIndex,
            in int currentChunkWorldX,
            in int currentChunkWorldZ,
            ref int visibleFaces, 
            in bool firstPass)
        {
            for (var voxelIndex = 0; voxelIndex < chunkVoxelCount; voxelIndex++)
            {
                if (voxelDataArray[voxelStartIndex + voxelIndex].type == VoxelType.Air)
                    continue;
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
                            in chunkSize,
                            in chunkSizeY,
                            in voxelDataArray,
                            in totalChunksPerAxis,
                            in chunkVoxelCount,
                            in currentChunkWorldX,
                            in currentChunkWorldZ,
                            in voxelIndex,
                            in normal,
                            in voxelStartIndex,
                            in firstPass)) continue;
                    
                    visibleFaces++;
                }
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SecondPassGetVisibleFacesGlobal(
            in int chunkVoxelCount,
            in NativeArray<VoxelData> voxelDataArray,
            in byte chunkSize,
            in byte chunkSizeY,
            in byte totalChunksPerAxis,
            in int visibleFaces,
            in int voxelStartIndex,
            out UnsafeList<Vector3>* vertices,
            out UnsafeList<int>* triangles,
            out UnsafeList<Vector3>* normals,
            out UnsafeList<Color32>* colors,
            in int currentChunkWorldX,
            in int currentChunkWorldZ,
            in bool firstPass,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax)
        {
            var bufferVertexCount = Mathf.FloorToInt(visibleFaces * 1.3125f * VoxelUtils.FaceEdges);
            var bufferTriangleCount = Mathf.FloorToInt(visibleFaces * 1.3125f * VoxelUtils.FaceCount);
            
            vertices = UnsafeList<Vector3>.Create(bufferVertexCount, Allocator.Temp);
            triangles = UnsafeList<int>.Create(bufferTriangleCount, Allocator.Temp);
            normals = UnsafeList<Vector3>.Create(bufferVertexCount, Allocator.Temp);
            colors = UnsafeList<Color32>.Create(bufferVertexCount, Allocator.Temp);

            var vertexIndex = 0;
            
            for (var voxelIndex = 0; voxelIndex < chunkVoxelCount; voxelIndex++)
            {
                var voxelType = voxelDataArray[voxelStartIndex + voxelIndex].type;
                
                if (voxelType == VoxelType.Air)
                    continue;
                
                var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, chunkSize, chunkSizeY);
                
                var voxelPosition = new Vector3(
                    x + currentChunkWorldX,
                    y,
                    z + currentChunkWorldZ);
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
                            in chunkSize,
                            in chunkSizeY,
                            in voxelDataArray,
                            in totalChunksPerAxis,
                            in chunkVoxelCount,
                            in currentChunkWorldX,
                            in currentChunkWorldZ,
                            in voxelIndex,
                            in normal,
                            in voxelStartIndex,
                            in firstPass)) continue;
        
                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex =
                            voxelPosition +
                            VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                        
                        vertices->AddNoResize(vertex);
                        normals->AddNoResize(normal);
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
            in byte chunkSize,
            in byte chunkSizeY,
            in NativeArray<VoxelData> voxelDataArray,
            in byte totalChunksPerAxis,
            in int chunkVoxelCount,
            in int currentChunkWorldX,
            in int currentChunkWorldZ,
            in int voxelIndex,
            in Vector3Int normal,
            in int voxelStartIndex,
            in bool firstPass)
        {
            var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, chunkSize, chunkSizeY);
        
            var neighborY = y + normal.y;
            if (neighborY < 0 || neighborY >= chunkSizeY)
                return true;
            
            var neighborX = x + normal.x;
            var neighborZ = z + normal.z;
            
            // Neighbor voxel is within the current chunk's local XZ bounds
            if (neighborX >= 0 && neighborX < chunkSize && neighborZ >= 0 && neighborZ < chunkSize)
            {
                var neighborVoxelIndex = voxelStartIndex +
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborX, neighborY, neighborZ, chunkSize, chunkSizeY);
                
                return voxelDataArray[neighborVoxelIndex].type == VoxelType.Air;
            }
            
            if (firstPass) return false;
            
            // Neighbor might be in an adjacent chunk.
            var neighborGlobalX = currentChunkWorldX + neighborX;
            var neighborGlobalZ = currentChunkWorldZ + neighborZ;
            
            // Calculate target chunk grid coordinates.
            var gridOffset = totalChunksPerAxis / 2;
            var targetChunkGridX = Mathf.FloorToInt((float)neighborGlobalX / chunkSize) + gridOffset;
            var targetChunkGridZ = Mathf.FloorToInt((float)neighborGlobalZ / chunkSize) + gridOffset;
            
            // Ensure the target chunk is within world bounds.
            if (targetChunkGridX < 0 || targetChunkGridX >= totalChunksPerAxis ||
                targetChunkGridZ < 0 || targetChunkGridZ >= totalChunksPerAxis)
                return false;   
            
            // Calculate the target chunk's array index.
            var targetChunkIndex = targetChunkGridZ * totalChunksPerAxis + targetChunkGridX;
            
            var targetVoxelLocalX = (neighborGlobalX % chunkSize + chunkSize) % chunkSize;
            var targetVoxelLocalZ = (neighborGlobalZ % chunkSize + chunkSize) % chunkSize;
            
            var neighborAbsoluteIndex = (targetChunkIndex * chunkVoxelCount) + ChunkUtils.Flatten3DLocalCoordsToIndex(
                0, targetVoxelLocalX, neighborY, targetVoxelLocalZ, chunkSize, chunkSizeY);
            
            // Validate and check voxel visibility.
            return 
                neighborAbsoluteIndex >= 0 && 
                neighborAbsoluteIndex < voxelDataArray.Length &&
                voxelDataArray[neighborAbsoluteIndex].type == VoxelType.Air;
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
