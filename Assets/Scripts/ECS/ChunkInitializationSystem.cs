using Chunk;
using Unity.Burst;
using Unity.Entities;

namespace ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ChunkInitializationSystem : SystemBase
    {
        private EntityCommandBufferSystem _entityCommandBufferSystem;
        
        protected override void OnCreate()
        {
            // Get the command buffer system that runs at the beginning of the simulation group.
            _entityCommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        }
        
        protected override void OnUpdate()
        {
            // Find all entities that are new and need data.
            var newChunksQuery = SystemAPI.QueryBuilder().WithAll<NewChunkTag, ChunkCoordinate>().Build();

            if (newChunksQuery.IsEmpty) return;
            
            // Create the ECB from the dedicated system.
            var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer();
            
            // Add a VoxelDataBuffer to each new chunk entity and set its capacity.
            var addVoxelBufferJob = new AddVoxelBufferJob
            {
                CommandBuffer = commandBuffer.AsParallelWriter(),
                ChunkVoxelCount = ChunkUtils.GetChunkTotalSize(VoxelConstants.ChunkSize, VoxelConstants.ChunkSizeY)
            };

            var jobHandle = addVoxelBufferJob.ScheduleParallel(newChunksQuery, Dependency);
            _entityCommandBufferSystem.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle;
        }
        
        protected override void OnDestroy() { }
    }
    
    [BurstCompile]
    public partial struct AddVoxelBufferJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        public int ChunkVoxelCount;
        
        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex)
        {
            var buffer = CommandBuffer.AddBuffer<VoxelDataBuffer>(chunkIndex, entity);
            buffer.ResizeUninitialized(ChunkVoxelCount);
            
            CommandBuffer.RemoveComponent<NewChunkTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsDataGenerationTag>(chunkIndex, entity);
        }
    }
}