using Unity.Collections;
using Unity.Jobs;

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
            // if (chunkX == 0) continue;
            
            for (var chunkZ = -chunksToGenerate; chunkZ < chunksToGenerate; chunkZ++)
            {
                // if (chunkZ == 0) continue;

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

                // var startX = 0;
                // var startZ = 0;
                // var endX = 0;
                // var endZ = 0;
                //
                // switch (currentChunkX)
                // {
                //     case < 0:
                //         startX = currentChunkX;
                //         endX = currentChunkX + chunkSize;
                //         break;
                //     case > 0:
                //         startX = currentChunkX - chunkSize;
                //         endX = currentChunkX;
                //         break;
                // }
                //
                // switch (currentChunkZ)
                // {
                //     case < 0:
                //         startZ = currentChunkZ;
                //         endZ = currentChunkZ + chunkSize;
                //         break;
                //     case > 0:
                //         startZ = currentChunkZ - chunkSize;
                //         endZ = currentChunkZ;
                //         break;
                // }
                //
                // for (var voxelX = startX; voxelX < endX; voxelX++)
                // {
                //     for (var voxelZ = startZ; voxelZ < endZ; voxelZ++)
                //     {
                //         for (var voxelY = 0; voxelY < chunkSizeY; voxelY++)
                //         {
                //             voxelDataArray[voxelIndex++] = new VoxelData(
                //                 voxelX, 
                //                 voxelY, 
                //                 voxelZ
                //             );
                //         }
                //     }
                // }

                chunkIndex++;
            }
        }
    }
}
