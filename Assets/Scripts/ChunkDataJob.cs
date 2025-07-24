using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct ChunkDataJob : IJob
{
    public byte chunksToGenerate;
    public byte chunkSize;
    public byte chunkSizeY;
    public NativeArray<ChunkData> chunkDataArray;
    [WriteOnly] public NativeArray<VoxelData> voxelDataArray;
    
    public void Execute()
    {
        var chunkIndex = 0;
        var voxelIndex = 0;
        
        for (var chunkX = -chunksToGenerate; chunkX < chunksToGenerate; chunkX++)
        {
            for (var chunkZ = -chunksToGenerate; chunkZ < chunksToGenerate; chunkZ++)
            {
                var currentChunkX = chunkX * chunkSize;
                var currentChunkZ = chunkZ * chunkSize;

                chunkDataArray[chunkIndex] = new ChunkData(
                    currentChunkX, 
                    currentChunkZ
                );
                
                for (byte voxelX = 0; voxelX < chunkSize; voxelX++)
                {
                    for (byte voxelZ = 0; voxelZ < chunkSize; voxelZ++)
                    {
                        for (byte voxelY = 0; voxelY < chunkSizeY; voxelY++)
                        {
                            voxelDataArray[voxelIndex++] = new VoxelData(
                                voxelX, 
                                voxelY, 
                                voxelZ
                            );
                        }
                    }
                }
                
                chunkIndex++;
            }
        }
    }
}
