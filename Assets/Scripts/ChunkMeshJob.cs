using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public struct ChunkMeshJob : IJobParallelFor
{
    private Mesh.MeshDataArray _chunkMeshDataArray;
    [ReadOnly] private readonly int _chunkVoxelCount;
    [ReadOnly] private readonly byte _chunkSize;
    [ReadOnly] private readonly int _chunkSizeY;
    [ReadOnly] private readonly byte _chunksToGenerate;
    [ReadOnly] private NativeArray<ChunkData> _chunkDataArray;
    [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;

    public ChunkMeshJob(
        Mesh.MeshDataArray chunkMeshDataArray, 
        int chunkVoxelCount,
        byte chunkSize,
        byte chunkSizeY,
        byte chunksToGenerate,
        NativeArray<ChunkData> chunkDataArray, 
        NativeArray<VoxelData> voxelDataArray)
    {
        _chunkMeshDataArray = chunkMeshDataArray;
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
        var uvs = new NativeList<Vector2>(0, Allocator.Temp);

        var vertexIndex = 0;
        
        var currentChunkWorldX = _chunkDataArray[index].x;
        var currentChunkWorldZ = _chunkDataArray[index].z;
        var chunksPerAxis = _chunksToGenerate * 2;

        for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
        {
            if (_voxelDataArray[voxelStartIndex + voxelIndex]._type == VoxelType.Air)
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

                var neighborGlobalX = currentChunkWorldX + x + normal.x;
                var neighborGlobalY = y + normal.y;
                var neighborGlobalZ = currentChunkWorldZ + z + normal.z;

                var neighborVoxelType = VoxelType.Air;
                
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
                        
                        neighborVoxelType = _voxelDataArray[voxelStartIndex + neighborVoxelLocalIndex]._type;
                    }
                    else // Neighbor is in an adjacent chunk (or outside the generated area horizontally)
                    {
                        // Calculate target chunk grid coordinates.
                        var targetChunkGridX = Mathf.FloorToInt(neighborGlobalX / _chunkSize);
                        var targetChunkGridZ = Mathf.FloorToInt(neighborGlobalZ / _chunkSize);
                        
                        // Check if the target chunk's grid coordinates are within the generated world bounds. 
                        if (targetChunkGridX >= -_chunksToGenerate && targetChunkGridX < _chunksToGenerate &&
                            targetChunkGridZ >= -_chunksToGenerate && targetChunkGridZ < _chunksToGenerate)
                        {
                            // Calculate target local voxel coordinates within that chunk.
                            var targetVoxelLocalX = (int)((neighborGlobalX % _chunkSize + _chunkSize) % _chunkSize);
                            var targetVoxelLocalY = (int)((neighborGlobalY % _chunkSizeY + _chunkSizeY) % _chunkSizeY);
                            var targetVoxelLocalZ = (int)((neighborGlobalZ % _chunkSize + _chunkSize) % _chunkSize);
                        
                            // Convert chunk grid coordinates to linear index in chunkDataArray
                            var targetChunkIndex = (targetChunkGridZ + _chunksToGenerate) * chunksPerAxis +
                                                   (targetChunkGridX + _chunksToGenerate);
                        
                            // Check if the target chunk is within the valid bounds of generated chunks.
                            if (targetChunkIndex >= 0 && targetChunkIndex < _chunkDataArray.Length)
                            {
                                var targetVoxelAbsoluteIndex = (targetChunkIndex * _chunkVoxelCount) +
                                                               ChunkUtils.Flatten3DLocalCoordsToIndex(
                                                                   0, targetVoxelLocalX, targetVoxelLocalY, targetVoxelLocalZ,
                                                                   _chunkSize, _chunkSizeY);
                            
                                // Check if the target voxel absolute index is within bounds of the overall voxelDataArray.
                                if (targetVoxelAbsoluteIndex >= 0 && targetVoxelAbsoluteIndex < _voxelDataArray.Length)
                                {
                                    neighborVoxelType = _voxelDataArray[targetVoxelAbsoluteIndex]._type;
                                }
                            }
                        }
                    }
                }

                // If the neighbor voxel isn't air (it is solid), we can skip this face.
                if (neighborVoxelType != VoxelType.Air) continue;
                
                // Add 4 vertices, normals, and UVs for the current face.
                for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                {
                    vertices.Add(
                        voxelPosition + VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]]);
                    normals.Add(normal);
                    uvs.Add(VoxelUtils.Uvs[j]);
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
        
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>
            (3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        
        vertexAttributes[0] = new VertexAttributeDescriptor
            (VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
        vertexAttributes[1] = new VertexAttributeDescriptor
            (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
        vertexAttributes[2] = new VertexAttributeDescriptor
            (VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 2, stream: 2);
        
        chunkMeshData.SetVertexBufferParams(vertices.Length, vertexAttributes);
        vertexAttributes.Dispose();
        
        chunkMeshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        
        var meshVertices = chunkMeshData.GetVertexData<Vector3>(0);
        meshVertices.CopyFrom(vertices.AsArray());
        
        var meshNormals = chunkMeshData.GetVertexData<Vector3>(1);
        meshNormals.CopyFrom(normals.AsArray());
        
        var meshUvs = chunkMeshData.GetVertexData<Vector2>(2);
        meshUvs.CopyFrom(uvs.AsArray());
        
        var meshTriangles = chunkMeshData.GetIndexData<int>();
        meshTriangles.CopyFrom(triangles.AsArray());
        
        chunkMeshData.subMeshCount = 1;
        chunkMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length), MeshUpdateFlags.DontRecalculateBounds);

        vertices.Dispose();
        triangles.Dispose();
        normals.Dispose();
        uvs.Dispose();
    }
}
