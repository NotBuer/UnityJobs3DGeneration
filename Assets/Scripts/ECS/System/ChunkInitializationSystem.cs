using ECS.Components;
using ECS.World;
using Unity.Burst;
using Unity.Entities;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ChunkSpawningSystem))]
    public partial struct ChunkInitializationSystem : ISystem
    {
        private EntityQuery _newChunksQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldConfiguration>();
            
            _newChunksQuery = SystemAPI.QueryBuilder()
                .WithAll<NewChunkTag, ChunkCoordinate>()
                .Build();
            
            state.RequireForUpdate(_newChunksQuery);
        }
        
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var addVoxelBufferJob = new AddVoxelBufferJob
            {
                CommandBuffer = ecb,
                ChunkVoxelCount = Chunk.ChunkUtils.GetChunkTotalSize(
                    Settings.World.Data.ChunkSize, Settings.World.Data.ChunkSizeY)
            };

            state.Dependency = addVoxelBufferJob.ScheduleParallel(_newChunksQuery, state.Dependency);
        }
    }
    
    [BurstCompile]
    public partial struct AddVoxelBufferJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        [Unity.Collections.ReadOnly] public int ChunkVoxelCount;
        
        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex)
        {
            var buffer = CommandBuffer.AddBuffer<VoxelDataBuffer>(chunkIndex, entity);
            buffer.ResizeUninitialized(ChunkVoxelCount);
            
            CommandBuffer.RemoveComponent<NewChunkTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsDataGenerationTag>(chunkIndex, entity);
        }
    }
}
