#region OLD_VERSION
// using System;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Jobs;
// using UnityEngine;
// using UnityEngine.Rendering;
// using Voxel;
//
// namespace Chunk
// {
//     [BurstCompile]
//     public struct ChunkMeshJob : IJobParallelFor
//     {
//         private Mesh.MeshDataArray _chunkMeshDataArray;
//         private NativeArray<Bounds> _chunkBoundsArray;
//         [ReadOnly] private readonly int _chunkVoxelCount;
//         [ReadOnly] private readonly byte _chunkSize;
//         [ReadOnly] private readonly int _chunkSizeY;
//         [ReadOnly] private readonly byte _chunksToGenerate;
//         [ReadOnly] private NativeArray<ChunkData> _chunkDataArray;
//         [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
//     
//         public ChunkMeshJob(
//             Mesh.MeshDataArray chunkMeshDataArray, 
//             NativeArray<Bounds> chunkBoundsArray,
//             int chunkVoxelCount,
//             byte chunkSize,
//             byte chunkSizeY,
//             byte chunksToGenerate,
//             NativeArray<ChunkData> chunkDataArray, 
//             NativeArray<VoxelData> voxelDataArray)
//         {
//             _chunkMeshDataArray = chunkMeshDataArray;
//             _chunkBoundsArray = chunkBoundsArray;
//             _chunkVoxelCount = chunkVoxelCount;
//             _chunkSize = chunkSize;
//             _chunkSizeY = chunkSizeY;
//             _chunksToGenerate = chunksToGenerate;
//             _chunkDataArray = chunkDataArray;
//             _voxelDataArray = voxelDataArray;
//         }
//         
//         public void Execute(int index)
//         {
//             var chunkMeshData = _chunkMeshDataArray[index];
//             
//             var voxelStartIndex = _chunkVoxelCount * index;
//             
//             // var vertices = new NativeList<Vector3>(short.MaxValue, Allocator.Temp);
//             // var triangles = new NativeList<int>(short.MaxValue, Allocator.Temp);
//             // var normals = new NativeList<Vector3>(short.MaxValue, Allocator.Temp);
//             // var colors = new NativeList<Color32>(short.MaxValue, Allocator.Temp);
//             var vertices = new NativeList<Vector3>(0, Allocator.Temp);
//             var triangles = new NativeList<int>(0, Allocator.Temp);
//             var normals = new NativeList<Vector3>(0, Allocator.Temp);
//             var colors = new NativeList<Color32>(0, Allocator.Temp);
//             
//             var currentChunkWorldX = _chunkDataArray[index].x;
//             var currentChunkWorldZ = _chunkDataArray[index].z;
//
//             var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
//             var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
//     
//             BuildChunkMeshData(
//                 in voxelStartIndex, 
//                 in currentChunkWorldX,
//                 in currentChunkWorldZ,
//                 ref vertices,
//                 ref triangles,
//                 ref normals,
//                 ref colors,
//                 ref boundsMin,
//                 ref boundsMax);
//             
//             SetMeshDataBuffers(
//                 ref chunkMeshData,
//                 ref vertices,
//                 ref triangles,
//                 ref normals,
//                 ref colors,
//                 in boundsMin,
//                 in boundsMax,
//                 in index);
//     
//             vertices.Dispose();
//             triangles.Dispose();
//             normals.Dispose();
//             colors.Dispose();
//         }
//         
//         private void BuildChunkMeshData(
//             in int voxelStartIndex, 
//             in float currentChunkWorldX,
//             in float currentChunkWorldZ,
//             ref NativeList<Vector3> vertices,
//             ref NativeList<int> triangles,
//             ref NativeList<Vector3> normals,
//             ref NativeList<Color32> colors,
//             ref Vector3 boundsMin,
//             ref Vector3 boundsMax)
//         {
//             var vertexIndex = 0;
//             for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
//             {
//                 var voxelType = _voxelDataArray[voxelStartIndex + voxelIndex].type;
//                 
//                 if (voxelType == VoxelType.Air)
//                     continue;
//                 
//                 var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);
//     
//                 var voxelPosition = new Vector3(
//                     x + currentChunkWorldX,
//                     y,
//                     z + currentChunkWorldZ);
//     
//                 // For each face of the 6 voxel faces.
//                 for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
//                 {
//                     var normal = VoxelUtils.Normals[faceIndex];
//                     
//                     var neighborVoxelType = VoxelType.Air;
//     
//                     ApplyFaceCulling(
//                         ref neighborVoxelType,
//                         in currentChunkWorldX,
//                         in currentChunkWorldZ,
//                         in x, in y, in z,
//                         in normal,
//                         in voxelStartIndex);
//     
//                     // If the neighbor voxel isn't air (it is solid), we can skip this face.
//                     if (neighborVoxelType != VoxelType.Air) continue;
//                     
//                     // Add 4 vertices, normals, and UVs for the current face.
//                     for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
//                     {
//                         var vertex =
//                             voxelPosition +
//                             VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
//                             
//                         vertices.Add(vertex);
//                         normals.Add(normal);
//                         colors.Add(GetVoxelColor(in voxelType));
//
//                         boundsMin = Vector3.Min(boundsMin, vertex);
//                         boundsMax = Vector3.Max(boundsMax, vertex);
//                     }
//     
//                     // Add 2 triangles for the face using an anti-clockwise direction.
//                     triangles.Add(vertexIndex);
//                     triangles.Add(vertexIndex + 3);
//                     triangles.Add(vertexIndex + 2);
//                     triangles.Add(vertexIndex);
//                     triangles.Add(vertexIndex + 2);
//                     triangles.Add(vertexIndex + 1);
//     
//                     vertexIndex += VoxelUtils.FaceEdges;
//                 }
//             }
//         }
//
//         private void ApplyFaceCulling(
//             ref VoxelType neighborVoxelType,
//             in float currentChunkWorldX,
//             in float currentChunkWorldZ,
//             in int x, in int y, in int z,
//             in Vector3Int normal,
//             in int voxelStartIndex)
//         {
//             var neighborGlobalX = currentChunkWorldX + x + normal.x;
//             var neighborGlobalY = y + normal.y;
//             var neighborGlobalZ = currentChunkWorldZ + z + normal.z;
//             
//             // First, check Y bounds (outside the world vertically is always air).
//             if (neighborGlobalY < 0 || neighborGlobalY >= _chunkSizeY)
//             {
//                 neighborVoxelType = VoxelType.Air;
//             }
//             else
//             {
//                 // Determine if the neighbor is within the current chunk's XZ bounds (locally 0 to chunkSize-1).
//                 var isNeighborInSameChunkXZ = 
//                     (x + normal.x >= 0 && x + normal.x < _chunkSize &&
//                      z + normal.z >= 0 && z + normal.z < _chunkSize);
//
//                 if (isNeighborInSameChunkXZ)
//                 {
//                     // Neighbor voxel is within the current chunk's local XZ bounds
//                     var neighborLocalX = x + normal.x;
//                     var neighborLocalY = y + normal.y;
//                     var neighborLocalZ = z + normal.z;
//                     
//                     var neighborVoxelLocalIndex = ChunkUtils.Flatten3DLocalCoordsToIndex(
//                         0, neighborLocalX, neighborLocalY, neighborLocalZ, _chunkSize, _chunkSizeY);
//                     
//                     neighborVoxelType = _voxelDataArray[voxelStartIndex + neighborVoxelLocalIndex].type;
//                 }
//                 else // Neighbor is in an adjacent chunk (or outside the generated area horizontally)
//                 {
//                     // Calculate target chunk grid coordinates.
//                     var targetChunkGridX = Mathf.FloorToInt(neighborGlobalX / _chunkSize);
//                     var targetChunkGridZ = Mathf.FloorToInt(neighborGlobalZ / _chunkSize);
//                     
//                     // If the target chunk's grid coordinates are out the generated world bounds, skip it. 
//                     if (targetChunkGridX < -_chunksToGenerate || targetChunkGridX >= _chunksToGenerate ||
//                         targetChunkGridZ < -_chunksToGenerate || targetChunkGridZ >= _chunksToGenerate) return;
//                     
//                     // Calculate target local voxel coordinates within that chunk.
//                     var targetVoxelLocalX = (int)((neighborGlobalX % _chunkSize + _chunkSize) % _chunkSize);
//                     // var targetVoxelLocalY = (int)((neighborGlobalY % _chunkSizeY + _chunkSizeY) % _chunkSizeY);
//                     var targetVoxelLocalZ = (int)((neighborGlobalZ % _chunkSize + _chunkSize) % _chunkSize);
//                     
//                     // Convert chunk grid coordinates to linear index in chunkDataArray
//                     var targetChunkIndex = (targetChunkGridZ + _chunksToGenerate) * 
//                                            (_chunksToGenerate * 2) + // 2 Axis
//                                            (targetChunkGridX + _chunksToGenerate);
//                     
//                     // If the target chunk are out the valid bounds of generated chunks, skip it.
//                     if (targetChunkIndex < 0 || targetChunkIndex >= _chunkDataArray.Length) return;
//                     
//                     var targetVoxelAbsoluteIndex = (targetChunkIndex * _chunkVoxelCount) +
//                                                    ChunkUtils.Flatten3DLocalCoordsToIndex(
//                                                        0, targetVoxelLocalX, neighborGlobalY, targetVoxelLocalZ,
//                                                        _chunkSize, _chunkSizeY);
//                         
//                     // Check if the target voxel absolute index is within the bounds of the overall voxelDataArray.
//                     if (targetVoxelAbsoluteIndex >= 0 && targetVoxelAbsoluteIndex < _voxelDataArray.Length)
//                     {
//                         neighborVoxelType = _voxelDataArray[targetVoxelAbsoluteIndex].type;
//                     }
//                 }
//             }
//         }
//         
//         private Color32 GetVoxelColor(
//              in VoxelType voxelType)
//          {
//              return voxelType switch
//              {
//                  VoxelType.Grass => VoxelUtils.GrassColor,
//                  VoxelType.Dirt => VoxelUtils.DirtColor,
//                  VoxelType.Stone => VoxelUtils.StoneColor,
//                  
//                  _ => throw new ArgumentOutOfRangeException(nameof(voxelType), voxelType, null)
//              };
//          }
//
//         private void SetMeshDataBuffers(
//             ref Mesh.MeshData chunkMeshData,
//             ref NativeList<Vector3> vertices,
//             ref NativeList<int> triangles,
//             ref NativeList<Vector3> normals,
//             ref NativeList<Color32> colors,
//             in Vector3 boundsMin,
//             in Vector3 boundsMax,
//             in int chunkIndex)
//         {
//             var vertexAttributes = new NativeArray<VertexAttributeDescriptor>
//                 (3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
//             
//             vertexAttributes[0] = new VertexAttributeDescriptor
//                 (VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
//             vertexAttributes[1] = new VertexAttributeDescriptor
//                 (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
//             vertexAttributes[2] = new VertexAttributeDescriptor
//                 (VertexAttribute.Color, VertexAttributeFormat.UNorm8, dimension: 4, stream: 2);
//             
//             chunkMeshData.SetVertexBufferParams(vertices.Length, vertexAttributes);
//             vertexAttributes.Dispose();
//             
//             chunkMeshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
//             
//             chunkMeshData.GetVertexData<Vector3>(0).CopyFrom(vertices.AsArray());
//             chunkMeshData.GetVertexData<Vector3>(1).CopyFrom(normals.AsArray());
//             chunkMeshData.GetVertexData<Color32>(2).CopyFrom(colors.AsArray());
//             chunkMeshData.GetIndexData<int>().CopyFrom(triangles.AsArray());
//             
//             chunkMeshData.subMeshCount = 1;
//             chunkMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length), 
//                 MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | 
//                 MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds );
//             
//             _chunkBoundsArray[chunkIndex] = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
//         }
//     }
// }
#endregion

#region NEW_VERSION
using System;
using LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public unsafe struct ChunkMeshJob : IJobParallelFor
    {
        private Mesh.MeshDataArray _chunkMeshDataArray;
        private NativeArray<Bounds> _chunkBoundsArray;
        [ReadOnly] private readonly int _chunkVoxelCount;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly int _chunkSizeY;
        [ReadOnly] private readonly byte _chunksToGenerate;
        [ReadOnly] private NativeArray<ChunkData> _chunkDataArray;
        [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
    
        public ChunkMeshJob(
            Mesh.MeshDataArray chunkMeshDataArray, 
            NativeArray<Bounds> chunkBoundsArray,
            int chunkVoxelCount,
            byte chunkSize,
            byte chunkSizeY,
            byte chunksToGenerate,
            NativeArray<ChunkData> chunkDataArray, 
            NativeArray<VoxelData> voxelDataArray)
        {
            _chunkMeshDataArray = chunkMeshDataArray;
            _chunkBoundsArray = chunkBoundsArray;
            _chunkVoxelCount = chunkVoxelCount;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _chunksToGenerate = chunksToGenerate;
            _chunkDataArray = chunkDataArray;
            _voxelDataArray = voxelDataArray;
        }
        
        public void Execute(int index)
        {
            var chunkMeshData = _chunkMeshDataArray[index];
            
            var voxelStartIndex = _chunkVoxelCount * index;
            
            var currentChunkWorldX = _chunkDataArray[index].x;
            var currentChunkWorldZ = _chunkDataArray[index].z;

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var visibleFaces = 0;
            
            FirstPassGetVisibleFacesLocal(
                in voxelStartIndex,
                in currentChunkWorldX,
                in currentChunkWorldZ,
                ref visibleFaces,
                true);
            
            SecondPassGetVisibleFacesGlobal(
                in visibleFaces,
                in voxelStartIndex,
                out var vertices,
                out var triangles,
                out var normals,
                out var colors,
                in currentChunkWorldX,
                in currentChunkWorldZ,
                false,
                ref boundsMin,
                ref boundsMax);
            
            SetMeshDataBuffers(
                ref chunkMeshData,
                ref vertices,
                ref triangles,
                ref normals,
                ref colors,
                in boundsMin,
                in boundsMax,
                in index);
            
            vertices->Dispose();
            triangles->Dispose();
            normals->Dispose();
            colors->Dispose();
        }
        
        private void FirstPassGetVisibleFacesLocal(
            in int voxelStartIndex,
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            ref int visibleFaces, 
            in bool firstPass)
        {
            for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
            {
                if (_voxelDataArray[voxelStartIndex + voxelIndex].type == VoxelType.Air)
                    continue;
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
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

        private void SecondPassGetVisibleFacesGlobal(
            in int visibleFaces,
            in int voxelStartIndex,
            out UnsafeList<Vector3>* vertices,
            out UnsafeList<int>* triangles,
            out UnsafeList<Vector3>* normals,
            out UnsafeList<Color32>* colors,
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
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
            
            for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
            {
                var voxelType = _voxelDataArray[voxelStartIndex + voxelIndex].type;
                
                if (voxelType == VoxelType.Air)
                    continue;
                
                var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);
                
                var voxelPosition = new Vector3(
                    x + currentChunkWorldX,
                    y,
                    z + currentChunkWorldZ);
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
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
                        colors->AddNoResize(GetVoxelColor(in voxelType));
                        
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

        private bool IsFaceVisible(
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            in int voxelIndex,
            in Vector3Int normal,
            in int voxelStartIndex,
            in bool firstPass)
        {
            var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);
        
            var neighborY = y + normal.y;
            if (neighborY < 0 || neighborY >= _chunkSizeY)
                return true;
            
            var neighborX = x + normal.x;
            var neighborZ = z + normal.z;
            
            // Neighbor voxel is within the current chunk's local XZ bounds
            if (neighborX >= 0 && neighborX < _chunkSize &&
                neighborZ >= 0 && neighborZ < _chunkSize)
            {
                var neighborVoxelIndex = voxelStartIndex +
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborX, neighborY, neighborZ, _chunkSize, _chunkSizeY);
                
                return _voxelDataArray[neighborVoxelIndex].type == VoxelType.Air;
            }
            
            if (firstPass) return false;
            
            var neighborGlobalX = currentChunkWorldX + neighborX;
            var neighborGlobalZ = currentChunkWorldZ + neighborZ;
            
            // Calculate target chunk grid coordinates.
            var targetChunkGridX = Mathf.FloorToInt(neighborGlobalX / _chunkSize);
            var targetChunkGridZ = Mathf.FloorToInt(neighborGlobalZ / _chunkSize);
            
            // If the target chunk's grid coordinates are out the generated world bounds, skip it. 
            if (targetChunkGridX < -_chunksToGenerate || targetChunkGridX >= _chunksToGenerate ||
                targetChunkGridZ < -_chunksToGenerate || targetChunkGridZ >= _chunksToGenerate)
                return false;   
            
            // Calculate target local voxel coordinates within that chunk.
            var targetVoxelLocalX = (int)(neighborGlobalX % _chunkSize + _chunkSize) % _chunkSize;
            // var targetVoxelLocalY = (int)((neighborY % _chunkSizeY + _chunkSizeY) % _chunkSizeY);
            var targetVoxelLocalZ = (int)(neighborGlobalZ % _chunkSize + _chunkSize) % _chunkSize;
                    
            // Convert chunk grid coordinates to linear index in chunkDataArray
            var targetChunkIndex = (targetChunkGridZ + _chunksToGenerate) * 
                                   (_chunksToGenerate * 2) + // 2 Axis
                                   (targetChunkGridX + _chunksToGenerate);
                    
            // If the target chunk are out the valid bounds of generated chunks, skip it.
            if (targetChunkIndex < 0 || targetChunkIndex >= _chunkDataArray.Length)
                return false;
                    
            var targetVoxelAbsoluteIndex = 
                (targetChunkIndex * _chunkVoxelCount) +
                ChunkUtils.Flatten3DLocalCoordsToIndex(
                    0, targetVoxelLocalX, neighborY, targetVoxelLocalZ, _chunkSize, _chunkSizeY);
                        
            // Check if the target voxel absolute index is within the bounds of the overall voxelDataArray.
            if (targetVoxelAbsoluteIndex >= 0 && targetVoxelAbsoluteIndex < _voxelDataArray.Length)
            {
                return _voxelDataArray[targetVoxelAbsoluteIndex].type == VoxelType.Air;
            }
            
            return false;
        }
        
        private Color32 GetVoxelColor(
            in VoxelType voxelType)
        {
            return voxelType switch
            {
                VoxelType.Grass => VoxelUtils.GrassColor,
                VoxelType.Dirt => VoxelUtils.DirtColor,
                VoxelType.Stone => VoxelUtils.StoneColor,
                
                _ => throw new ArgumentOutOfRangeException(nameof(voxelType), voxelType, null)
            };
        }

        private void SetMeshDataBuffers(
            ref Mesh.MeshData chunkMeshData,
            ref UnsafeList<Vector3>* vertices,
            ref UnsafeList<int>* triangles,
            ref UnsafeList<Vector3>* normals,
            ref UnsafeList<Color32>* colors,
            in Vector3 boundsMin,
            in Vector3 boundsMax,
            in int chunkIndex)
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
            
            _chunkBoundsArray[chunkIndex] = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
        }
    }
}
#endregion