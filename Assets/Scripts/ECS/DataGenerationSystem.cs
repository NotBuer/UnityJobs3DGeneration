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
    public partial struct DataGenerationSystem : ISystem
    {
        private EntityQuery _chunksToGenerateQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldGenerationConfig>();
            
            _chunksToGenerateQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsDataGenerationTag, ChunkCoordinate>()
                .WithAllRW<VoxelDataBuffer>()
                .Build();
            
            state.RequireForUpdate(_chunksToGenerateQuery);
            
            if (SystemAPI.HasSingleton<WorldGenerationConfig>()) return;
            
            var configEntity = state.EntityManager.CreateEntity();

            // var fixedCurrentSeed = new FixedString64Bytes("boer12345");
            state.EntityManager.AddComponentData(configEntity, new WorldGenerationConfig
            {
                Frequency = 0.01f,
                Amplitude = 32f,
                Seed = uint.MaxValue/*World.WorldSeed.GetStableHash64(ref fixedCurrentSeed)*/
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var config = SystemAPI.GetSingleton<WorldGenerationConfig>();
            
            var dataGenerationJob = new DataGenerationJob
            {
                Config = config,
                CommandBuffer = ecb
            };
            
            state.Dependency = dataGenerationJob.ScheduleParallel(_chunksToGenerateQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct DataGenerationJob : IJobEntity
    {
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

                        ChunkUtils.FlattenIndex(new int3(x, y, z), VoxelConstants.ChunkSize, out var index);
                        voxelBuffer[index] = new VoxelDataBuffer { Value = voxelType };
                    }
                }
            }
            
            CommandBuffer.RemoveComponent<NeedsDataGenerationTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsMeshingTag>(chunkIndex, entity);
        }
    }
}