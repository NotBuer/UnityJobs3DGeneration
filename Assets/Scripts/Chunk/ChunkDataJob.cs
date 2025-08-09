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
        [ReadOnly] private readonly int _chunkSizeInVoxels;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        [ReadOnly] private readonly ulong _seed;
    	[ReadOnly] private readonly float _frequency;
    	[ReadOnly] private readonly float _amplitude;
        
        [ReadOnly] private NativeArray<Vector2Int> _chunkCoordsArray;
        
        [WriteOnly] [NativeDisableParallelForRestriction] 
        private NativeArray<VoxelData> _voxelDataArray;
        
        private NativeParallelHashMap<Vector2Int, int>.ParallelWriter _coordTableHashMap;

        private readonly float2 _noiseOffset;
    
        public ChunkDataJob(
            int chunkSizeInVoxels,
            byte chunkSize, 
            byte chunkSizeY,
            ulong seed,
            float frequency,
            float amplitude,
            NativeArray<Vector2Int> chunkCoordsArray,
            NativeArray<VoxelData> voxelDataArray,
            NativeParallelHashMap<Vector2Int, int>.ParallelWriter coordTableHashMap) : this()
        {
            _chunkSizeInVoxels = chunkSizeInVoxels;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _seed = seed;
            _frequency = frequency;
            _amplitude = amplitude;
            _chunkCoordsArray = chunkCoordsArray;
            _voxelDataArray = voxelDataArray;
            _coordTableHashMap = coordTableHashMap;
            
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
            var chunkCoord = _chunkCoordsArray[index];
            _coordTableHashMap.TryAdd(chunkCoord, index);
            
            for (byte x = 0; x < _chunkSize; x++)
            {
                for (byte z = 0; z < _chunkSize; z++)
                {
                    var noiseValue = noise.snoise(
                        new float2(
                            (chunkCoord.x + x) * _frequency + _noiseOffset.x,
                            (chunkCoord.y + z) * _frequency + _noiseOffset.y
                        )
                    );
                    
                    // Normalize to 0-1 range for height calculations
                    noiseValue = (noiseValue + 1) * 0.5f;
                    
                    var height = Mathf.RoundToInt(noiseValue * _amplitude);
    
                    for (byte y = 0; y < _chunkSizeY; y++)
                    {
                        var voxelType = y switch
                        {
                            _ when y >= height - 2 && y <= height     => VoxelType.Grass,
                            _ when y >= height - 4 && y <= height - 2 => VoxelType.Dirt,
                            _ when y >= height - 6 && y <= height - 4 => VoxelType.Stone,
                            _ => VoxelType.Air
                        };
    
                        _voxelDataArray[ChunkUtils.Flatten3DLocalCoordsToIndex(
                            index * _chunkSizeInVoxels, x, y, z, _chunkSize, _chunkSizeY)] = new VoxelData(voxelType);
                    }
                }
            }
        }
    }
}

