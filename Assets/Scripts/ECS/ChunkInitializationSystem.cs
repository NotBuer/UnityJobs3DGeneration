using Unity.Burst;
using Unity.Entities;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkSpawningSystem))]
    public partial struct ChunkInitializationSystem : ISystem
    {
        private EntityQuery _newChunksQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            
            // We create and cache the query in OnCreate for efficiency.
            _newChunksQuery = SystemAPI.QueryBuilder()
                .WithAll<NewChunkTag, ChunkCoordinate>()
                .Build();
            
            // This ensures the system doesn't run if there are no entities matching the query.
            // It's a good optimization to prevent unnecessary work.
            state.RequireForUpdate(_newChunksQuery);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get the command buffer system that will execute our commands.
            // Using EndInitializationEntityCommandBufferSystem is correct if you want the changes
            // to be visible early in the main simulation phase.
            var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
            
            // Create a parallel ECB writer for use in the job.
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Prepare the job.
            var addVoxelBufferJob = new AddVoxelBufferJob
            {
                CommandBuffer = ecb,
                ChunkVoxelCount = Chunk.ChunkUtils.GetChunkTotalSize(VoxelConstants.ChunkSize, VoxelConstants.ChunkSizeY)
            };

            // --- Dependency Chaining ---
            // Schedule the job. We pass the current system's dependency handle (`state.Dependency`)
            // to the job, and the job returns a new handle. We assign this new handle back to
            // state.Dependency. This correctly chains our job into the main dependency chain
            // for the frame, ensuring other systems wait for it to complete.
            state.Dependency = addVoxelBufferJob.ScheduleParallel(_newChunksQuery, state.Dependency);
        }
    }
    
    [BurstCompile]
    public partial struct AddVoxelBufferJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        [Unity.Collections.ReadOnly] public int ChunkVoxelCount;
        
        // The [ChunkIndexInQuery] attribute provides a stable index for the parallel command buffer.
        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex)
        {
            // Add a dynamic buffer to the entity for storing voxel data.
            var buffer = CommandBuffer.AddBuffer<VoxelDataBuffer>(chunkIndex, entity);
            // Pre-allocate the buffer's memory to avoid resizing later.
            buffer.ResizeUninitialized(ChunkVoxelCount);
            
            // We remove the NewChunkTag as this chunk has been processed by this system.
            CommandBuffer.RemoveComponent<NewChunkTag>(chunkIndex, entity);
            // We add a new tag to mark it for the next stage in the pipeline (e.g., terrain generation).
            CommandBuffer.AddComponent<NeedsDataGenerationTag>(chunkIndex, entity);
        }
    }
}