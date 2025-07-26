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
    [ReadOnly] private NativeArray<ChunkData> _chunkDataArray;
    [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;

    public ChunkMeshJob(
        Mesh.MeshDataArray chunkMeshDataArray, 
        int chunkVoxelCount,
        byte chunkSize,
        byte chunkSizeY,
        NativeArray<ChunkData> chunkDataArray, 
        NativeArray<VoxelData> voxelDataArray)
    {
        _chunkMeshDataArray = chunkMeshDataArray;
        _chunkVoxelCount = chunkVoxelCount;
        _chunkSize = chunkSize;
        _chunkSizeY = chunkSizeY;
        _chunkDataArray = chunkDataArray;
        _voxelDataArray = voxelDataArray;
    }

    public void Execute(int index)
    {
        var chunkMeshData = _chunkMeshDataArray[index];
        var voxelStartIndex = _chunkVoxelCount * index;
        
        var chunkVoxelDataSlice = _voxelDataArray.Slice(voxelStartIndex, _chunkVoxelCount);
        
        // TODO: Find out which is the best values to pre-allocate each nativelist.
        var vertices = new NativeList<Vector3>(0, Allocator.Temp);
        var triangles = new NativeList<int>(0, Allocator.Temp);
        var normals = new NativeList<Vector3>(0, Allocator.Temp);
        var uvs = new NativeList<Vector2>(0, Allocator.Temp);

        var vertexIndex = 0;

        for (var voxelIndex = 0; voxelIndex < chunkVoxelDataSlice.Length; voxelIndex++)
        {
            if (chunkVoxelDataSlice[voxelIndex]._type == VoxelType.Air)
                continue;
            
            var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);

            var voxelPosition = new Vector3(
                x + _chunkDataArray[index].x,
                y,
                z + _chunkDataArray[index].z);

            // For each face of the 6 voxel faces.
            for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
            {
                var normal = VoxelUtils.Normals[faceIndex];
                
                var neighborLocalX = x + normal.x;
                var neighborLocalY = y + normal.y;
                var neighborLocalZ = z + normal.z;
                
                var neighborVoxelType = VoxelType.Air;
                
                // Check if the neighbor voxel is within the chunk bounds.
                if (neighborLocalX >= 0 && neighborLocalX < _chunkSize &&
                    neighborLocalY >= 0 && neighborLocalY < _chunkSizeY &&
                    neighborLocalZ >= 0 && neighborLocalZ < _chunkSize)
                {
                    var neighborVoxelIndex = ChunkUtils.Flatten3DLocalCoordsToIndex(
                        neighborLocalX, neighborLocalY, neighborLocalZ, _chunkSize, _chunkSizeY);
                    
                    neighborVoxelType = chunkVoxelDataSlice[neighborVoxelIndex]._type;
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
