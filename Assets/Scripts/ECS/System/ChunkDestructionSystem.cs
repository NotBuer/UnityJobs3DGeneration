using ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(WorldBootstrapSystem))]
    public partial struct ChunkDestructionSystem : ISystem
    {
        private EntityQuery _chunksToDestroyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldConfiguration>();
            
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
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            ecb.DestroyEntity(_chunksToDestroyQuery, EntityQueryCaptureMode.AtPlayback);
        }
    }
}