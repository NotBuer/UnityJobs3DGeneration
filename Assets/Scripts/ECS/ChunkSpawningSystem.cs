using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ChunkSpawningSystem : ISystem
    {
        private Vector2Int _lastPlayerChunkPos;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Ensure this system only runs after the player has been set up.
            state.RequireForUpdate<PlayerPosition>();
            _lastPlayerChunkPos = new Vector2Int(int.MinValue, int.MinValue);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var playerPos = SystemAPI.GetSingleton<PlayerPosition>().Value;
            var currentPlayerChunkPos = new Vector2Int(
                Mathf.FloorToInt(playerPos.x / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize,
                Mathf.FloorToInt(playerPos.z / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize);

            if (currentPlayerChunkPos.x == _lastPlayerChunkPos.x && currentPlayerChunkPos.y == _lastPlayerChunkPos.y)
                return;

            _lastPlayerChunkPos = currentPlayerChunkPos;
            
            // Determine which chunks are required.
            var requiredChunks = new NativeHashSet<int2>(0XFF, Allocator.Temp);
            var activeChunks = new NativeHashMap<int2, Entity>(0XFF, Allocator.Temp);

            foreach (var (coord, entity) in SystemAPI.Query<RefRO<ChunkCoordinate>>().WithEntityAccess())
            {
                activeChunks.Add(coord.ValueRO.Value, entity);
            }

            const int radius = VoxelConstants.RenderDistance;
            const int radiusSqr = radius * radius;

            for (var x = -radius; x <= radius; x++)
            {
                for (var z = -radius; z <= radius; z++)
                {
                    var offset = new int2(x, z);
                    if (!(math.lengthsq(offset) <= radiusSqr)) continue;
                    
                    var requiredPos = new int2(
                        _lastPlayerChunkPos.x + (x * VoxelConstants.ChunkSize),
                        _lastPlayerChunkPos.y + (z * VoxelConstants.ChunkSize)
                    );
                    requiredChunks.Add(requiredPos);

                    if (activeChunks.ContainsKey(requiredPos)) continue;
                    
                    // If this required chunk doesn't exist yet, create it.
                    var newChunk = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(newChunk, new ChunkCoordinate { Value = requiredPos });
                    state.EntityManager.AddComponent<NewChunkTag>(newChunk);
                }   
            }
            
            foreach (var (coord, entity) in SystemAPI.Query<RefRO<ChunkCoordinate>>().WithEntityAccess())
            {
                if (requiredChunks.Contains(coord.ValueRO.Value)) continue;

                // Add a tag that another system will use to clean up the chunk's data and destroy it.
                state.EntityManager.AddComponent<ToUnloadTag>(entity);
            }

            requiredChunks.Dispose();
            activeChunks.Dispose();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }        
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
