using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    private const byte RenderDistanceAxisCount = 2;

    [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
    [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
    [Range(1, 32)] [SerializeField] private byte chunksToGenerate = 1;

    private NativeArray<ChunkCoord> chunkCoordsArray;
    private JobHandle genChunkCoordsJobHandle;
    
    private JobHandle generateChunkMeshJobHandle;
    
    private void Awake()
    {
        var renderDistancePerAxis = 
            chunksToGenerate * RenderDistanceAxisCount * RenderDistanceAxisCount;
        chunkCoordsArray = new NativeArray<ChunkCoord>(renderDistancePerAxis, Allocator.Persistent);
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

        foreach (var chunkCoord in chunkCoordsArray)
        {
            Debug.Log($"{chunkCoord.x}, {chunkCoord.z}");
        }
    }

    private void OnApplicationQuit()
    {
        chunkCoordsArray.Dispose();
        Debug.Log("All native arrays disposed");
    }
}
