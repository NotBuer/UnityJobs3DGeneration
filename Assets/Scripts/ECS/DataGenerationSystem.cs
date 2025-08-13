using Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkInitializationSystem))]
    public partial struct DataGenerationSystem : ISystem
    {
        private EntityQuery _chunksToGenerateQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldGenerationConfig>();
            
            _chunksToGenerateQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsDataGenerationTag, ChunkCoordinate>()
                // We also need write access to the buffer.
                .WithAllRW<VoxelDataBuffer>()
                .Build();
            
            state.RequireForUpdate(_chunksToGenerateQuery);
            
            if (SystemAPI.HasSingleton<WorldGenerationConfig>()) return;
            
            // Create the configuration singleton if it doesn't exist.
            var configEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(configEntity, new WorldGenerationConfig
            {
                Frequency = 0.01f,
                Amplitude = 32f,
                Seed = World.WorldSeed.GetStableHash64("boer12345")
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get the command buffer for our structural changes.
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            // Get the world generation parameters from our singleton.
            var config = SystemAPI.GetSingleton<WorldGenerationConfig>();

            // Prepare the data generation job with the config values.
            var dataGenerationJob = new DataGenerationJob
            {
                Config = config,
                CommandBuffer = ecb
            };

            // Schedule the job and chain the dependencies.
            state.Dependency = dataGenerationJob.ScheduleParallel(_chunksToGenerateQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct DataGenerationJob : IJobEntity
    {
        // By passing the whole config struct, we keep the job's fields tidy.
        [ReadOnly] public WorldGenerationConfig Config;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord, 
            DynamicBuffer<VoxelDataBuffer> voxelBuffer)
        {
            var lower32 = (uint)(Config.Seed & 0xFFFFFFFF);
            // var upper32 = (uint)((Seed >> 32) & 0xFFFFFFFF);
            var offsetX = (lower32 & 0xFFFF) * 0.001f;
            var offsetZ = ((lower32 >> 16) & 0xFFFF) * 0.001f;
            
            var noiseOffset = new float2(offsetX, offsetZ);
            
            for (byte x = 0; x < VoxelConstants.ChunkSize; x++)
            {
                for (byte z = 0; z < VoxelConstants.ChunkSize; z++)
                {
                    var noiseValue = noise.snoise(
                        new float2(
                            (coord.Value.x + x) * Config.Frequency + noiseOffset.x,
                            (coord.Value.y + z) * Config.Frequency + noiseOffset.y
                        )
                    );
                    
                    // Normalize to 0-1 range for height calculations
                    noiseValue = (noiseValue + 1) * 0.5f;
                    
                    var height = Mathf.RoundToInt(noiseValue * Config.Amplitude);
    
                    for (byte y = 0; y < VoxelConstants.ChunkSizeY; y++)
                    {
                        var voxelType = y switch
                        {
                            _ when y >= height - 2 && y <= height     => VoxelType.Grass,
                            _ when y >= height - 4 && y <= height - 2 => VoxelType.Dirt,
                            _ when y >= height - 6 && y <= height - 4 => VoxelType.Stone,
                            _ => VoxelType.Air
                        };
                        
                        voxelBuffer[
                            ChunkUtils.Flatten3DLocalCoordsToIndex(
                                0, x, y, z, VoxelConstants.ChunkSize, VoxelConstants.ChunkSizeY)] 
                            = new VoxelDataBuffer { Value = voxelType };
                    }
                }
            }
            
            CommandBuffer.RemoveComponent<NeedsDataGenerationTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsMeshingTag>(chunkIndex, entity);
        }
    }
}