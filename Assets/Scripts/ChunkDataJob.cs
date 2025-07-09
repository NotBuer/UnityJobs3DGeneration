using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct ChunkDataJob : IJob
{
    public NativeArray<Vector3> vertices;
    public NativeArray<Vector3> triangles;
    
    public void Execute()
    {
        
    }
}
