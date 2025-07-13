using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public struct ChunkMeshJob : IJob
{
    public Mesh.MeshData chunkMeshData;
    public byte chunkSizeX;
    public byte chunkSizeZ;
    public byte chunkSizeY;
    
    public void Execute()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        var vertexIndex = 0;
        
        for (byte x = 0; x < chunkSizeX; x++)
        {
            for (byte z = 0; z < chunkSizeZ; z++)
            {
                for (byte y = 0; y < chunkSizeY; y++)
                {
                    var voxelPosition = new Vector3(x, y, z);

                    // For each face of the 6 voxel faces.
                    for (byte faceIndex = 0; faceIndex < VoxelData.FaceCount; faceIndex++)
                    {
                        // Add 4 vertices, normals, and UVs for the current face.
                        for (byte j = 0; j < VoxelData.FaceEdges; j++)
                        {
                            vertices.Add(voxelPosition + VoxelData.Vertices[VoxelData.FaceVertices[faceIndex, j]]);
                            normals.Add(VoxelData.Normals[faceIndex]);
                            uvs.Add(VoxelData.Uvs[j]);
                        }
                    }
                    
                    // Add 2 triangles for the face using an anti-clockwise direction.
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 3);
                    triangles.Add(vertexIndex + 2);
                    
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);

                    vertexIndex += 4;
                }
            }
        }
        
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 2, stream: 2);
        
        chunkMeshData.SetVertexBufferParams(vertices.Count, vertexAttributes);
        vertexAttributes.Dispose();
        
        chunkMeshData.SetIndexBufferParams(triangles.Count, IndexFormat.UInt32);
        
        var meshVertices = chunkMeshData.GetVertexData<Vector3>(0);
        meshVertices.CopyFrom(vertices.ToArray());
        
        var meshNormals = chunkMeshData.GetVertexData<Vector3>(1);
        meshNormals.CopyFrom(normals.ToArray());
        
        var meshUvs = chunkMeshData.GetVertexData<Vector2>(2);
        meshUvs.CopyFrom(uvs.ToArray());
        
        var meshTriangles = chunkMeshData.GetIndexData<int>();
        meshTriangles.CopyFrom(triangles.ToArray());
        
        chunkMeshData.subMeshCount = 1;
        chunkMeshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Count), MeshUpdateFlags.DontRecalculateBounds);
    }
}
