using Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;
using World;

namespace ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkInitializationSystem))]
    public partial class DataGenerationSystem : SystemBase
    {
        private EntityCommandBufferSystem _entityCommandBufferSystem;

        protected override void OnCreate()
        {
            _entityCommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            var chunksToGenerateQuery =
                SystemAPI.QueryBuilder().WithAll<NeedsDataGenerationTag, ChunkCoordinate, VoxelDataBuffer>().Build();
            
            if (chunksToGenerateQuery.IsEmpty) return;
            
            var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer();
            
            // Schedule de data generation job.
            var dataGenerationJob = new DataGenerationJob
            {
                Frequency = 0.01f,
                Amplitude = 32f,
                Seed = WorldSeed.GetStableHash64("boer12345"),
                CommandBuffer = commandBuffer.AsParallelWriter()
            };
            
            var jobHandle = dataGenerationJob.ScheduleParallel(chunksToGenerateQuery, Dependency);
            _entityCommandBufferSystem.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle;
        }
        
        protected override void OnDestroy() { }
    }



    [BurstCompile]
    public partial struct DataGenerationJob : IJobEntity
    {
        [ReadOnly] public float Frequency;
        [ReadOnly] public float Amplitude;
        [ReadOnly] public ulong Seed;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord, 
            DynamicBuffer<VoxelDataBuffer> voxelBuffer)
        {
            var lower32 = (uint)(Seed & 0xFFFFFFFF);
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
                            (coord.Value.x + x) * Frequency + noiseOffset.x,
                            (coord.Value.y + z) * Frequency + noiseOffset.y
                        )
                    );
                    
                    // Normalize to 0-1 range for height calculations
                    noiseValue = (noiseValue + 1) * 0.5f;
                    
                    var height = Mathf.RoundToInt(noiseValue * Amplitude);
    
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