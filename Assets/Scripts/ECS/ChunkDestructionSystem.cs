using Unity.Burst;
using Unity.Entities;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ChunkDestructionSystem : ISystem
    {
        private EntityQuery _chunksToDestroyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            
            _chunksToDestroyQuery = SystemAPI.QueryBuilder()
                    .WithAll<ToUnloadTag>()
                    .Build();
            
            state.RequireForUpdate(_chunksToDestroyQuery);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // state.Dependency.Complete();
            
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            ecb.DestroyEntity(_chunksToDestroyQuery, EntityQueryCaptureMode.AtPlayback);
        }
    }
}