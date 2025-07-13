using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class ChunkMeshDataTest : MonoBehaviour
{
    [SerializeField] private byte chunkSizeX = 16;
    [SerializeField] private byte chunkSizeZ = 16;
    [SerializeField] private byte chunkSizeY = 255;
    [SerializeField] private byte chunksToGenerate = 1;

    private JobHandle generateChunkMeshJobHandle;
    
    private void Start()
    {
        // chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkSizeX *  chunkSizeZ * chunkSizeY);
        var chunkMeshDataArray = Mesh.AllocateWritableMeshData(1);
        var chunkMeshData = chunkMeshDataArray[0];

        // var generateChunkMeshJob = new GenerateChunkMeshJob()
        // {
        //     chunkMeshDataArray = chunkMeshDataArray,
        //     chunkSizeX = chunkSizeX,
        //     chunkSizeZ = chunkSizeZ,
        //     chunkSizeY = chunkSizeY,
        // };

        var generateChunkMeshJob = new ChunkMeshJob()
        {
            chunkMeshData = chunkMeshData,
            chunkSizeX = chunkSizeX,
            chunkSizeZ = chunkSizeZ,
            chunkSizeY = chunkSizeY,
        };

        generateChunkMeshJobHandle = generateChunkMeshJob.Schedule();

        generateChunkMeshJobHandle.Complete();

        var chunkMesh = new Mesh {name = "Chunk Mesh Job Test" };
        Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, chunkMesh);
        chunkMesh.RecalculateBounds();
        
        var chunkGameObject = new GameObject("Chunk");
        chunkGameObject.transform.SetParent(this.gameObject.transform);
        
        var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = chunkMesh;
        
        var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.material = new Material(Shader.Find("Standard"));
        
        var meshCollider = chunkGameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = chunkMesh;
        
        
        // var meshes = new List<Mesh>(chunkSizeX * chunkSizeZ * chunkSizeY);
        // for (byte x = 0; x < chunkSizeX; x++)
        // {
        //     for (byte z = 0; z < chunkSizeZ; z++)
        //     {
        //         for (byte y = 0; y < chunkSizeY; y++)
        //         {
        //             meshes.Add(new Mesh { name = $"Mesh Coord: {x} {y} {z}" });
        //         }
        //     }
        // }
        
        // Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, meshes);
        // meshes.ForEach(x =>
        // {
        //     x.RecalculateBounds();
        //     x.RecalculateNormals();
        // });

        // var posIterator = 0;
        // var shaderDefault = Shader.Find("Standard");
        // foreach (var mesh in meshes)
        // {
        //     var gameObject = new GameObject($"{mesh.name}");
        //     gameObject.transform.SetParent(this.gameObject.transform);
        //     
        //     var meshFilter = gameObject.AddComponent<MeshFilter>();
        //     meshFilter.mesh = mesh;
        //     
        //     var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        //     meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        //     meshRenderer.material = new Material(shaderDefault);
        //
        //     posIterator++;
        // }
    }

    // private struct GenerateChunkMeshJob : IJob
    // {
    //     public Mesh.MeshDataArray chunkMeshDataArray;
    //     public byte chunkSizeX;
    //     public byte chunkSizeZ;
    //     public byte chunkSizeY;
    //     
    //     public void Execute()
    //     {
    //         var iterator = 0;
    //         for (byte x = 0; x < chunkSizeX; x++)
    //         {
    //             for (byte z = 0; z < chunkSizeZ; z++)
    //             {
    //                 for (byte y = 0; y < chunkSizeY; y++)
    //                 {
    //                     var meshData = chunkMeshDataArray[iterator];
    //                     VoxelData.SetVoxelMeshData(new Vector3(x, y, z), ref meshData);
    //                     iterator++;
    //                 }
    //             }
    //         }
    //     }
    // }
}
