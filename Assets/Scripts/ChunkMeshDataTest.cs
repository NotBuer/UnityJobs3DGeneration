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
        var chunkMeshDataArray = Mesh.AllocateWritableMeshData(1);
        var chunkMeshData = chunkMeshDataArray[0];

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
        chunkMesh.RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
        
        var chunkGameObject = new GameObject("Chunk");
        chunkGameObject.transform.SetParent(this.gameObject.transform);
        
        var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = chunkMesh;
        
        var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.material = new Material(Shader.Find("Standard"));
        
        // var meshCollider = chunkGameObject.AddComponent<MeshCollider>();
        // meshCollider.sharedMesh = chunkMesh;
    }
}
