using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldManager : MonoBehaviour
{
    public const float OriginPointGenerationOffset = 0.5f;
    
    private const byte RenderDistanceAxisCount = 2;
    
    [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
    [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
    [Range(1, 32)] [SerializeField] private byte chunksToGenerate = 1;

    private NativeArray<ChunkData> chunkDataArray;
    private NativeArray<VoxelData> voxelDataArray;
    
    private JobHandle chunkDataJobHandle;
    private JobHandle chunkMeshJobHandle;
    
    private void Awake()
    {
        var renderDistancePerAxis = 
            chunksToGenerate * RenderDistanceAxisCount * RenderDistanceAxisCount;
        
        chunkDataArray = new NativeArray<ChunkData>(
            renderDistancePerAxis, Allocator.Persistent);
        voxelDataArray = new NativeArray<VoxelData>(
            chunkDataArray.Length * (chunkSize * chunkSize * chunkSizeY), Allocator.Persistent);
    }

    private void Start()
    {
        var chunkDataJob = new ChunkDataJob()
        {
            chunksToGenerate = chunksToGenerate,
            chunkSize = chunkSize,
            chunkSizeY = chunkSizeY,
            chunkDataArray = chunkDataArray,
            voxelDataArray = voxelDataArray,
        };

        chunkDataJobHandle = chunkDataJob.Schedule();
        chunkDataJobHandle.Complete();
        
        var chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkDataArray.Length);

        var chunkMeshJob = new ChunkMeshJob()
        {
            chunkMeshDataArray = chunkMeshDataArray,
            chunkVoxelCount = chunkSize * chunkSize * chunkSizeY,
            chunkDataArray = chunkDataArray,
            voxelDataSlice = voxelDataArray
        };
        
        chunkMeshJobHandle = chunkMeshJob.Schedule(
            chunkMeshDataArray.Length, 2, chunkDataJobHandle);
        chunkMeshJobHandle.Complete();
        
        var chunkMeshes = new List<Mesh>(chunkMeshDataArray.Length);
        foreach (var chunkCoord in chunkDataArray) chunkMeshes.Add(new Mesh());
        Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, chunkMeshes);
        
        var materialDefault = new Material(Shader.Find("Standard"));

        for (byte i = 0; i < chunkMeshes.Count; i++)
        {
            chunkMeshes[i].RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
            
            var chunkGameObject = new GameObject { name = $"Chunk - X:{chunkDataArray[i].x}, Z:{chunkDataArray[i].z}" };
            // chunkGameObject.transform.SetParent(gameObject.transform);
            
            var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = chunkMeshes[i];
            
            var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.material = materialDefault;
        }
    }

    private void OnApplicationQuit()
    {
        chunkDataArray.Dispose();
        voxelDataArray.Dispose();
        Debug.Log("All native arrays disposed");
    }
}
