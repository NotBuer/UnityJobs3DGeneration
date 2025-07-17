using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class ChunkMeshDataTest : MonoBehaviour
{
    [SerializeField] private byte chunkSize = 16;
    [SerializeField] private byte chunkSizeY = 255;
    [SerializeField] private byte chunksToGenerate = 1;

    private JobHandle generateChunkMeshJobHandle;
    
    private void Start()
    {
        var chunkMeshDataArray = Mesh.AllocateWritableMeshData(1);

        var genChunkMeshJob = new ChunkMeshJob()
        {
            chunkMeshDataArray = chunkMeshDataArray,
            chunkSize = chunkSize,
            chunkSizeY = chunkSizeY,
        };

        generateChunkMeshJobHandle = genChunkMeshJob.Schedule(chunkMeshDataArray.Length, 2);

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
    }
}
