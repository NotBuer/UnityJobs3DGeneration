using System;
using DefaultNamespace;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [SerializeField] private int worldSizeX;
    [SerializeField] private int worldSizeZ;
    
    // private ChunkDataJob chunkDataJob = null;
    private JobHandle chunkDataJobHandle = default;
    
    // private ChunkCoordJob chunkCoordJob = null;
    private JobHandle chunkCoordJobHandle = default;
    
    private void Start()
    {
        var chunkDataJob = new ChunkDataJob()
        {
            vertices = new NativeArray<Vector3>(384, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
            triangles = new NativeArray<Vector3>(192, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
        };

        var chunkCoordJob = new ChunkCoordJob()
        {
            chunkCoord = new ChunkCoord(0, 0, 0)
        };

        chunkDataJobHandle = chunkDataJob.Schedule();
        chunkCoordJobHandle = chunkCoordJob.Schedule(chunkDataJobHandle);
    }

    private void LateUpdate()
    {
        // chunkCoordJobHandle.Complete();
        // chunkDataJobHandle.Complete();
        
        if (chunkCoordJobHandle.IsCompleted && chunkDataJobHandle.IsCompleted)
            Debug.Log("Jobs completed successfully!");
    }
}
