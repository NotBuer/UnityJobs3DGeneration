using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ChunkDestructionSystem))]
    public partial struct ChunkSpawningSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            
            state.RequireForUpdate<ChunkSpawningState>();
            state.RequireForUpdate<PlayerPosition>();
            
            if (SystemAPI.HasSingleton<ChunkSpawningState>()) return;
            
            var stateEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(stateEntity, new ChunkSpawningState
            {
                LastPlayerChunkPos = new int2(int.MinValue, int.MinValue)
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkSpawningStateEntity = SystemAPI.GetSingletonEntity<ChunkSpawningState>();
            var chunkSpawningState = SystemAPI.GetComponent<ChunkSpawningState>(chunkSpawningStateEntity);
            
            var playerPos = SystemAPI.GetSingleton<PlayerPosition>().Value;
            
            var currentPlayerChunkPos = new int2(
                (int)math.floor(playerPos.x / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize,
                (int)math.floor(playerPos.z / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize);

            if (currentPlayerChunkPos.x == chunkSpawningState.LastPlayerChunkPos.x &&
                currentPlayerChunkPos.y == chunkSpawningState.LastPlayerChunkPos.y) return;
            
            state.EntityManager.SetComponentData(chunkSpawningStateEntity, new ChunkSpawningState
            {
                LastPlayerChunkPos = currentPlayerChunkPos
            });
            
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            
            var requiredChunks = new NativeHashSet<int2>(1024, Allocator.Temp);
            var activeChunks = new NativeHashMap<int2, Entity>(1024, Allocator.Temp);
            
            foreach (var (coord, entity) in 
                     SystemAPI.Query<RefRO<ChunkCoordinate>>().WithNone<ToUnloadTag>().WithEntityAccess())
            {
                activeChunks.TryAdd(coord.ValueRO.Value, entity);
            }
            
            const int radius = VoxelConstants.RenderDistance;

            for (var x = -radius; x <= radius; x++)
            {
                for (var z = -radius; z <= radius; z++)
                {
                    if (x * x + z * z > radius * radius) continue;

                    var requiredPos = new int2(
                        currentPlayerChunkPos.x + (x * VoxelConstants.ChunkSize),
                        currentPlayerChunkPos.y + (z * VoxelConstants.ChunkSize)
                    );

                    requiredChunks.Add(requiredPos);
                    
                    if (activeChunks.ContainsKey(requiredPos)) continue;
                    
                    var newChunk = ecb.CreateEntity();
                    ecb.AddComponent(newChunk, new ChunkCoordinate { Value = requiredPos });
                    ecb.AddComponent<NewChunkTag>(newChunk);
                }
            }
            
            foreach (var chunk in activeChunks)
            {
                if (!requiredChunks.Contains(chunk.Key))
                {
                    ecb.AddComponent<ToUnloadTag>(chunk.Value);
                }
            }
            
            requiredChunks.Dispose();
            activeChunks.Dispose();
        }
    }

    public struct PlayerPosition : IComponentData
    {
        public float3 Value;
    }

    public static class VoxelConstants
    {
        public const byte ChunkSize = 16;
        public const byte ChunkSizeY = 0XFF;
        public const int RenderDistance = 8;
    }
}
