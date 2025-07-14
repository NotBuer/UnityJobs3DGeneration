using Unity.Collections;
using Unity.Jobs;

public struct ChunkCoordJob : IJob
{
    public byte chunksToGenerate;
    public byte chunkSize;
    public NativeArray<ChunkCoord> chunkCoords;
    
    public void Execute()
    {
        var index = 0;
        for (var x = -chunksToGenerate; x <= chunksToGenerate; x++)
        {
            if (x == 0) continue;
            
            for (var z = -chunksToGenerate; z <= chunksToGenerate; z++)
            {
                if (z == 0) continue;

                chunkCoords[index++] = new ChunkCoord(x * chunkSize, z * chunkSize);
            }
        }
    }
}
