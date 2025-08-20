using Chunk;
using ECS.Components;
using ECS.World;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DataGenerationSystem : ISystem
    {
        private EntityQuery _chunksToGenerateQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldConfiguration>();
            
            _chunksToGenerateQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsDataGenerationTag, ChunkCoordinate>()
                .WithAllRW<VoxelDataBuffer>()
                .Build();
            
            state.RequireForUpdate(_chunksToGenerateQuery);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var dataGenerationJob = new DataGenerationJob
            {
                CommandBuffer = ecb
            };
            
            state.Dependency = dataGenerationJob.ScheduleParallel(_chunksToGenerateQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct DataGenerationJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord, 
            DynamicBuffer<VoxelDataBuffer> voxelBuffer)
        {
            var lower32 = (uint)(Settings.World.Data.Seed & 0xFFFFFFFF);
            // var upper32 = (uint)((Seed >> 32) & 0xFFFFFFFF);
            var offsetX = (lower32 & 0xFFFF) * 0.001f;
            var offsetZ = ((lower32 >> 16) & 0xFFFF) * 0.001f;
            
            var noiseOffset = new float2(offsetX, offsetZ);
            
            for (byte x = 0; x < Settings.World.Data.ChunkSize; x++)
            {
                for (byte z = 0; z < Settings.World.Data.ChunkSize; z++)
                {
                    var noiseValue = noise.snoise(
                        new float2(
                            (coord.Value.x + x) * Settings.World.Data.Frequency + noiseOffset.x,
                            (coord.Value.y + z) * Settings.World.Data.Frequency + noiseOffset.y
                        )
                    );
                    
                    // Normalize to 0-1 range for height calculations
                    noiseValue = (noiseValue + 1) * 0.5f;
                    
                    var height = Mathf.RoundToInt(noiseValue * Settings.World.Data.Amplitude);
    
                    for (byte y = 0; y < Settings.World.Data.ChunkSizeY; y++)
                    {
                        var voxelType = y switch
                        {
                            _ when y >= height - 2 && y <= height     => VoxelType.Grass,
                            _ when y >= height - 4 && y <= height - 2 => VoxelType.Dirt,
                            _ when y >= height - 6 && y <= height - 4 => VoxelType.Stone,
                            _ => VoxelType.Air
                        };

                        ChunkUtils.FlattenIndex(new int3(x, y, z), Settings.World.Data.ChunkSize, out var index);
                        voxelBuffer[index] = new VoxelDataBuffer { Value = voxelType };
                    }
                }
            }
            
            CommandBuffer.RemoveComponent<NeedsDataGenerationTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsMeshingTag>(chunkIndex, entity);
        }
    }
}