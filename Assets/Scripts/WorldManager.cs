using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldManager : MonoBehaviour
{
    private const byte RenderDistanceAxisCount = 2;

    [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
    [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
    [Range(1, 32)] [SerializeField] private byte chunksToGenerate = 1;
    
    private Dictionary<byte, ChunkCoord> ChunkCoordsDictionary;

    private NativeArray<ChunkCoord> chunkCoordsArray;
    private JobHandle genChunkCoordsJobHandle;
    
    private JobHandle genChunkMeshJobHandle;
    
    private void Awake()
    {
        var renderDistancePerAxis = 
            chunksToGenerate * RenderDistanceAxisCount * RenderDistanceAxisCount;
        chunkCoordsArray = new NativeArray<ChunkCoord>(renderDistancePerAxis, Allocator.TempJob);
        
        ChunkCoordsDictionary = new Dictionary<byte, ChunkCoord>(renderDistancePerAxis);
    }

    private void Start()
    {
        var genChunkCoordJob = new ChunkCoordJob()
        {
            chunksToGenerate = chunksToGenerate,
            chunkSize = chunkSize,
            chunkCoords = chunkCoordsArray,
        };

        genChunkCoordsJobHandle = genChunkCoordJob.Schedule();
        genChunkCoordsJobHandle.Complete();

        for (byte i = 0; i < chunkCoordsArray.Length; i++)
        {
            ChunkCoordsDictionary.Add(i, new ChunkCoord(chunkCoordsArray[i].x, chunkCoordsArray[i].z));
        }
        
        chunkCoordsArray.Dispose(genChunkCoordsJobHandle);
        
        var chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkCoordsArray.Length);

        var genChunkMeshJob = new ChunkMeshJob()
        {
            chunkMeshDataArray = chunkMeshDataArray,
            chunkSize = chunkSize,
            chunkSizeY = chunkSizeY,
        };
        
        genChunkMeshJobHandle = genChunkMeshJob.Schedule(
            chunkMeshDataArray.Length, 
            2, genChunkCoordsJobHandle);
        genChunkMeshJobHandle.Complete();

        var materialDefault = new Material(Shader.Find("Standard"));
        
        var chunkMeshes = new List<Mesh>(chunkMeshDataArray.Length);
        foreach (var chunkCoord in chunkCoordsArray) chunkMeshes.Add(new Mesh());
        
        Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, chunkMeshes);

        for (byte i = 0; i < chunkMeshes.Count; i++)
        {
            chunkMeshes[i].RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
            
            var chunkGameObject = new GameObject();
            chunkGameObject.transform.SetParent(gameObject.transform);
            chunkGameObject.transform.position = new Vector3(ChunkCoordsDictionary[i].x, 0, ChunkCoordsDictionary[i].z);
            
            var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = chunkMeshes[i];
            
            var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.material = materialDefault;
        }
        
        // foreach (var chunkMesh in chunkMeshes)
        // {
        //     chunkMesh.RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
        //     
        //     var chunkGameObject = new GameObject();
        //     chunkGameObject.transform.SetParent(gameObject.transform);
        //     chunkGameObject.transform.position = ChunkCoordsDictionary.Values
        //     
        //     var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        //     meshFilter.mesh = chunkMesh;
        //     
        //     var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
        //     meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        //     meshRenderer.material = materialDefault;
        // }
    }

    private void OnApplicationQuit()
    {
        chunkCoordsArray.Dispose();
        Debug.Log("All native arrays disposed");
    }
}
