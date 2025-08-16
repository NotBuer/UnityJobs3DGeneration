using ECS.Components;
using ECS.World;
using Unity.Burst;
using Unity.Entities;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldBootstrapSystem : ISystem
    {
        private bool _worldConfigCached;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _worldConfigCached = false;
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            Settings.Reset();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_worldConfigCached) return;
            if (!SystemAPI.TryGetSingletonEntity<WorldConfiguration>(out var entity)) return;
            
            Settings.World.Data = state.EntityManager.GetComponentData<WorldConfiguration>(entity);
            
            _worldConfigCached = true;
        }
    }
}