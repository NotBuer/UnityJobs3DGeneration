using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public struct ChunkMeshJob : IJobParallelFor
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
            
            // TODO: Find out which is the best values to pre-allocate each NativeList's.
            var vertices = new NativeList<Vector3>(0, Allocator.Temp);
            var triangles = new NativeList<int>(0, Allocator.Temp);
            var normals = new NativeList<Vector3>(0, Allocator.Temp);
            var colors = new NativeList<Color32>(0, Allocator.Temp);
    
            var vertexIndex = 0;
            
            var currentChunkWorldX = _chunkDataArray[index].x;
            var currentChunkWorldZ = _chunkDataArray[index].z;

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    
            BuildChunkMeshData(
                ref vertexIndex, 
                in voxelStartIndex, 
                in currentChunkWorldX,
                in currentChunkWorldZ,
                ref vertices,
                ref triangles,
                ref normals,
                ref colors,
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
    
            vertices.Dispose();
            triangles.Dispose();
            normals.Dispose();
            colors.Dispose();
        }
        
        private void BuildChunkMeshData(
            ref int vertexIndex, 
            in int voxelStartIndex, 
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            ref NativeList<Vector3> vertices,
            ref NativeList<int> triangles,
            ref NativeList<Vector3> normals,
            ref NativeList<Color32> colors,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax)
        {
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
    
                // For each face of the 6 voxel faces.
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    var neighborVoxelType = VoxelType.Air;
    
                    ApplyFaceCulling(
                        ref neighborVoxelType,
                        in currentChunkWorldX,
                        in currentChunkWorldZ,
                        in x, in y, in z,
                        in normal,
                        in voxelStartIndex);
    
                    // If the neighbor voxel isn't air (it is solid), we can skip this face.
                    if (neighborVoxelType != VoxelType.Air) continue;
                    
                    // Add 4 vertices, normals, and UVs for the current face.
                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex =
                            voxelPosition +
                            VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                            
                        vertices.Add(vertex);
                        normals.Add(normal);
                        
                        ApplyColorData(
                            in voxelType,
                            ref colors);

                        boundsMin = Vector3.Min(boundsMin, vertex);
                        boundsMax = Vector3.Max(boundsMax, vertex);
                    }
    
                    // Add 2 triangles for the face using an anti-clockwise direction.
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 3);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
    
                    vertexIndex += VoxelUtils.FaceEdges;
                }
            }
        }

        private void ApplyFaceCulling(
            ref VoxelType neighborVoxelType,
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            in int x, in int y, in int z,
            in Vector3Int normal,
            in int voxelStartIndex)
        {
            var neighborGlobalX = currentChunkWorldX + x + normal.x;
            var neighborGlobalY = y + normal.y;
            var neighborGlobalZ = currentChunkWorldZ + z + normal.z;
            
            // First, check Y bounds (outside the world vertically is always air).
            if (neighborGlobalY < 0 || neighborGlobalY >= _chunkSizeY)
            {
                neighborVoxelType = VoxelType.Air;
            }
            else
            {
                // Determine if the neighbor is within the current chunk's XZ bounds (locally 0 to chunkSize-1).
                var isNeighborInSameChunkXZ = 
                    (x + normal.x >= 0 && x + normal.x < _chunkSize &&
                     z + normal.z >= 0 && z + normal.z < _chunkSize);

                if (isNeighborInSameChunkXZ)
                {
                    // Neighbor voxel is within the current chunk's local XZ bounds
                    var neighborLocalX = x + normal.x;
                    var neighborLocalY = y + normal.y;
                    var neighborLocalZ = z + normal.z;
                    
                    var neighborVoxelLocalIndex = ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborLocalX, neighborLocalY, neighborLocalZ, _chunkSize, _chunkSizeY);
                    
                    neighborVoxelType = _voxelDataArray[voxelStartIndex + neighborVoxelLocalIndex].type;
                }
                else // Neighbor is in an adjacent chunk (or outside the generated area horizontally)
                {
                    // Calculate target chunk grid coordinates.
                    var targetChunkGridX = Mathf.FloorToInt(neighborGlobalX / _chunkSize);
                    var targetChunkGridZ = Mathf.FloorToInt(neighborGlobalZ / _chunkSize);
                    
                    // If the target chunk's grid coordinates are out the generated world bounds, skip it. 
                    if (targetChunkGridX < -_chunksToGenerate || targetChunkGridX >= _chunksToGenerate ||
                        targetChunkGridZ < -_chunksToGenerate || targetChunkGridZ >= _chunksToGenerate) return;
                    
                    // Calculate target local voxel coordinates within that chunk.
                    var targetVoxelLocalX = (int)((neighborGlobalX % _chunkSize + _chunkSize) % _chunkSize);
                    var targetVoxelLocalY = (int)((neighborGlobalY % _chunkSizeY + _chunkSizeY) % _chunkSizeY);
                    var targetVoxelLocalZ = (int)((neighborGlobalZ % _chunkSize + _chunkSize) % _chunkSize);
                    
                    // Convert chunk grid coordinates to linear index in chunkDataArray
                    var targetChunkIndex = (targetChunkGridZ + _chunksToGenerate) * 
                                           (_chunksToGenerate * 2) + // 2 Axis
                                           (targetChunkGridX + _chunksToGenerate);
                    
                    // If the target chunk are out the valid bounds of generated chunks, skip it.
                    if (targetChunkIndex < 0 || targetChunkIndex >= _chunkDataArray.Length) return;
                    
                    var targetVoxelAbsoluteIndex = (targetChunkIndex * _chunkVoxelCount) +
                                                   ChunkUtils.Flatten3DLocalCoordsToIndex(
                                                       0, targetVoxelLocalX, targetVoxelLocalY, targetVoxelLocalZ,
                                                       _chunkSize, _chunkSizeY);
                        
                    // Check if the target voxel absolute index is within bounds of the overall voxelDataArray.
                    if (targetVoxelAbsoluteIndex >= 0 && targetVoxelAbsoluteIndex < _voxelDataArray.Length)
                    {
                        neighborVoxelType = _voxelDataArray[targetVoxelAbsoluteIndex].type;
                    }
                }
            }
        }

        private void ApplyColorData(
            in VoxelType voxelType,
            ref NativeList<Color32> colors)
        {
            colors.Add(voxelType switch
            {
                VoxelType.Grass => VoxelUtils.GrassColor,
                VoxelType.Dirt => VoxelUtils.DirtColor,
                VoxelType.Stone => VoxelUtils.StoneColor,
                _ => throw new ArgumentOutOfRangeException(nameof(voxelType), voxelType, null)
            });
        }

        private void SetMeshDataBuffers(
            ref Mesh.MeshData chunkMeshData,
            ref NativeList<Vector3> vertices,
            ref NativeList<int> triangles,
            ref NativeList<Vector3> normals,
            ref NativeList<Color32> colors,
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
            
            chunkMeshData.SetVertexBufferParams(vertices.Length, vertexAttributes);
            vertexAttributes.Dispose();
            
            chunkMeshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
            
            var meshVertices = chunkMeshData.GetVertexData<Vector3>(0);
            meshVertices.CopyFrom(vertices.AsArray());
            
            var meshNormals = chunkMeshData.GetVertexData<Vector3>(1);
            meshNormals.CopyFrom(normals.AsArray());

            var meshColors = chunkMeshData.GetVertexData<Color32>(2);
            meshColors.CopyFrom(colors.AsArray());
            
            var meshTriangles = chunkMeshData.GetIndexData<int>();
            meshTriangles.CopyFrom(triangles.AsArray());
            
            chunkMeshData.subMeshCount = 1;
            chunkMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length), 
                MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds );
            
            _chunkBoundsArray[chunkIndex] = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
        }
    }
}