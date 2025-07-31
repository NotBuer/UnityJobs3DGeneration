using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace Chunk
{ 
    [BurstCompile]
    public struct ChunkDataJob : IJobParallelFor
    {
        [ReadOnly] private readonly byte _chunksToGenerate;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        [ReadOnly] private readonly ulong _seed;
    	[ReadOnly] private readonly float _frequency;
    	[ReadOnly] private readonly float _amplitude;
        [WriteOnly] private NativeArray<ChunkData> _chunkDataArray;
        [WriteOnly, NativeDisableParallelForRestriction] private NativeArray<VoxelData> _voxelDataArray;

        private readonly float2 _noiseOffset;
    
        public ChunkDataJob(
            byte chunksToGenerate, 
            byte chunkSize, 
            byte chunkSizeY,
            ulong seed,
            float frequency,
            float amplitude,
            NativeArray<ChunkData> chunkDataArray, 
            NativeArray<VoxelData> voxelDataArray) : this()
        {
            _chunksToGenerate = chunksToGenerate;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _seed = seed;
            _frequency = frequency;
            _amplitude = amplitude;
            _chunkDataArray = chunkDataArray;
            _voxelDataArray = voxelDataArray;
            
            // 0xFFFFFFFF = uint.MaxValue(4294967295)
            // 0xFFFF = ushort.MaxValue(65535)
            
            /*
            SEED PROCESSING EXPLANATION:
        
            1. 0xFFFFFFFF is a 32-bit mask (hexadecimal representation)
               - In binary: 11111111111111111111111111111111 (32 ones)
               - In decimal: 4,294,967,295
        
            2. Splitting the 64-bit seed:
               - Lower 32 bits: seed64 & 0xFFFFFFFF
                 Example:
                     seed64 = 0x123456789ABCDEF0
                     lower32 = 0x9ABCDEF0
        
               - Upper 32 bits: (seed64 >> 32) & 0xFFFFFFFF
                 Example:
                     upper32 = 0x12345678
            */
            var lower32 = (uint)(_seed & 0xFFFFFFFF);
            var upper32 = (uint)((_seed >> 32) & 0xFFFFFFFF);

            /*
            OFFSET CALCULATION:
    
            We use only the lower 32 bits to generate offsets:
            - offsetX: Uses bits 0-15 (lowest 16 bits)
              - lower32 & 0xFFFF → isolates bits 0-15
              - Range: 0-65,535
    
            - offsetZ: Uses bits 16-31 (next 16 bits)
              - (lower32 >> 16) & 0xFFFF → shifts bits 16-31 to position 0-15
              - Range: 0-65,535
    
            Multiplier 0.001f scales to:
              - offsetX range: 0.0 - 65.535
              - offsetZ range: 0.0 - 65.535
    
            This keeps values small and safe for noise input.
            */
            var offsetX = (lower32 & 0xFFFF) * 0.001f;
            var offsetZ = ((lower32 >> 16) & 0xFFFF) * 0.001f;
            
            _noiseOffset = new float2(offsetX, offsetZ);
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

            // TODO: Try use this to place vegetation, ores, etc...
            // var random = 
            //     new Unity.Mathematics.Random((uint)(_seed ^ (currentChunkX * 397) ^ (currentChunkZ * 397)));
            
            for (byte voxelX = 0; voxelX < _chunkSize; voxelX++)
            {
                for (byte voxelZ = 0; voxelZ < _chunkSize; voxelZ++)
                {
                    var noiseValue = noise.snoise(
                        new float2(
                            (currentChunkX + voxelX) * _frequency + _noiseOffset.x,
                            (currentChunkZ + voxelZ) * _frequency + _noiseOffset.y
                        )
                    );
                    
                    // Normalize to 0-1 range for height calculations
                    noiseValue = (noiseValue + 1) * 0.5f;
                    
                    var height = Mathf.RoundToInt(noiseValue * _amplitude);
    
                    for (byte voxelY = 0; voxelY < _chunkSizeY; voxelY++)
                    {
                        var voxelType = voxelY switch
                        {
                            _ when voxelY >= height - 2 && voxelY <= height     => VoxelType.Grass,
                            _ when voxelY >= height - 4 && voxelY <= height - 2 => VoxelType.Dirt,
                            _ when voxelY >= height - 6 && voxelY <= height - 4 => VoxelType.Stone,
                            _ => VoxelType.Air
                        };

                        var voxelIndex = ChunkUtils.Flatten3DLocalCoordsToIndex(
                            chunkVoxelStartIndex, voxelX, voxelY, voxelZ, _chunkSize, _chunkSizeY);
    
                        _voxelDataArray[voxelIndex] = new VoxelData(voxelType);
                    }
                }
            }
        }
    }
}

