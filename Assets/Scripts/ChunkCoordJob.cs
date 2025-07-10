using DefaultNamespace;
using Unity.Collections;
using Unity.Jobs;

public struct ChunkCoordJob : IJob
{
    public byte genXAround;
    public byte genZAround;
    
    public NativeArray<ChunkCoord> chunkCoords;
    
    public void Execute()
    {
        byte iteration = 0;
        for (var currentX = -genXAround; currentX <= genXAround; currentX++)
        {
            for (var currentZ = -genZAround; currentZ <= genZAround; currentZ++)
            {
                chunkCoords[iteration] = new ChunkCoord((short)currentX, 0, (short)currentZ);
                iteration++;
            }
        }
    }
}
