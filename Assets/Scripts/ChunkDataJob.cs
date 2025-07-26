using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct ChunkDataJob : IJobParallelFor
{
    [ReadOnly] private readonly byte _chunksToGenerate;
    [ReadOnly] private readonly byte _chunkSize;
    [ReadOnly] private readonly byte _chunkSizeY;
	[ReadOnly] private readonly float _frequency;
	[ReadOnly] private readonly float _amplitude;
    [WriteOnly] private NativeArray<ChunkData> _chunkDataArray;
    [WriteOnly, NativeDisableParallelForRestriction] private NativeArray<VoxelData> _voxelDataArray;

    public ChunkDataJob(
        byte chunksToGenerate, 
        byte chunkSize, 
        byte chunkSizeY, 
        float frequency,
        float amplitude,
        NativeArray<ChunkData> chunkDataArray, 
        NativeArray<VoxelData> voxelDataArray) : this()
    {
        _chunksToGenerate = chunksToGenerate;
        _chunkSize = chunkSize;
        _chunkSizeY = chunkSizeY;
        _frequency = frequency;
        _amplitude = amplitude;
        _chunkDataArray = chunkDataArray;
        _voxelDataArray = voxelDataArray;
    }

    public void Execute(int index)
    {
        var chunkPerAxis = _chunksToGenerate * 2;
        var chunkX = (index % chunkPerAxis) - _chunksToGenerate;
        var chunkZ = (index / chunkPerAxis) - _chunksToGenerate;
        
        var currentChunkX = chunkX * _chunkSize;
        var currentChunkZ = chunkZ * _chunkSize;

        _chunkDataArray[index] = new ChunkData(currentChunkX, currentChunkZ);

        var chunkVoxelStartIndex = index * (_chunkSize * _chunkSize * _chunkSizeY);
        
        for (byte voxelX = 0; voxelX < _chunkSize; voxelX++)
        {
            for (byte voxelZ = 0; voxelZ < _chunkSize; voxelZ++)
            {
                var height = Mathf.RoundToInt(
                    Mathf.PerlinNoise(
                        (currentChunkX + voxelX) * _frequency,
                        (currentChunkZ + voxelZ) * _frequency) * _amplitude
                );

                for (byte voxelY = 0; voxelY < _chunkSizeY; voxelY++)
                {
                    var voxelType = VoxelType.Air;

                    if (voxelY <= height)
                    {
                        voxelType = VoxelType.Grass;
                    }
                    
                    var voxelIndex = ChunkUtils.Flatten3DLocalCoordsToIndex(
                        chunkVoxelStartIndex, voxelX, voxelY, voxelZ, _chunkSize, _chunkSizeY);

                    _voxelDataArray[voxelIndex] = new VoxelData(voxelType);
                }
            }
        }
    }
}
