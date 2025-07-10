using DefaultNamespace;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    private const byte ChunkSizeX = 16;
    private const byte ChunkSizeZ = 16;
    private const byte ChunkSizeY = 255;
    
    [SerializeField] private byte genXAround;
    [SerializeField] private byte genZAround;
    [SerializeField] private bool lockChunkGeneration;
    
    // private ChunkDataJob chunkDataJob = null;
    private JobHandle chunkDataJobHandle = default;
    
    // private ChunkCoordJob chunkCoordJob = null;
    private JobHandle chunkCoordJobHandle = default;

    private NativeArray<ChunkCoord> ChunkCoordsArray;
    private NativeArray<Vector3> ChunkVerticesArray;
    private NativeArray<Vector3> ChunkTrianglesArray;

    private void Awake()
    {        
        var chunksToGen = ((genXAround * 2) + 1) * ((genZAround * 2) + 1);
        ChunkCoordsArray = new NativeArray<ChunkCoord>(chunksToGen, Allocator.Persistent);
        ChunkVerticesArray = new NativeArray<Vector3>(384, Allocator.Persistent);
        ChunkTrianglesArray = new NativeArray<Vector3>(192, Allocator.Persistent);
    }

    private void Start()
    {
        var chunkDataJob = new ChunkDataJob()
        {
            vertices = ChunkVerticesArray,
            triangles = ChunkTrianglesArray
        };
        
        var chunkCoordJob = new ChunkCoordJob()
        {
            genXAround = genXAround,
            genZAround = genZAround,
            chunkCoords = ChunkCoordsArray,
        };

        chunkDataJobHandle = chunkDataJob.Schedule();
        chunkCoordJobHandle = chunkCoordJob.Schedule(chunkDataJobHandle);
    }

    private void LateUpdate()
    {
        LateCheckAndLockChunkGeneration();
    }

    private void LateCheckAndLockChunkGeneration()
    {
        chunkDataJobHandle.Complete();
        chunkCoordJobHandle.Complete();
        
        if (lockChunkGeneration) return;

        if (!chunkCoordJobHandle.IsCompleted || !chunkDataJobHandle.IsCompleted) return;
        
        foreach (var chunkCoord in ChunkCoordsArray)
        {
            chunkCoord.DebugLogCoord();
        }
        
        lockChunkGeneration = true;
    }

    private void OnApplicationQuit()
    {
        ChunkCoordsArray.Dispose();
        ChunkVerticesArray.Dispose();
        ChunkTrianglesArray.Dispose();
        Debug.Log("All native arrays cleared!");
    }
}
