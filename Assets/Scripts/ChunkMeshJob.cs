using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public struct ChunkMeshJob : IJobParallelFor
{
    public Mesh.MeshDataArray chunkMeshDataArray;
    public int chunkVoxelCount;
    public NativeArray<ChunkData> chunkDataArray;
    [ReadOnly] public NativeArray<VoxelData> voxelDataSlice;
    
    public void Execute(int index)
    {
        var chunkMeshData = chunkMeshDataArray[index];
        var voxelStartIndex = chunkVoxelCount * index;
        
        var chunkVoxelData = voxelDataSlice.Slice(voxelStartIndex, chunkVoxelCount);
        
        // TODO: Find out which is the best values to pre-allocate each nativelist.
        var vertices = new NativeList<Vector3>(0, Allocator.Temp);
        var triangles = new NativeList<int>(0, Allocator.Temp);
        var normals = new NativeList<Vector3>(0, Allocator.Temp);
        var uvs = new NativeList<Vector2>(0, Allocator.Temp);

        var vertexIndex = 0;

        for (var voxelIndex = 0; voxelIndex < chunkVoxelData.Length; voxelIndex++)
        {
            var voxelPosition = new Vector3(
                chunkVoxelData[voxelIndex].x + chunkDataArray[index].x,
                chunkVoxelData[voxelIndex].y, 
                chunkVoxelData[voxelIndex].z + chunkDataArray[index].z
            );

            // For each face of the 6 voxel faces.
            for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
            {
                // Add 4 vertices, normals, and UVs for the current face.
                for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                {
                    vertices.Add(
                        voxelPosition + VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]]);
                    normals.Add(VoxelUtils.Normals[faceIndex]);
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
