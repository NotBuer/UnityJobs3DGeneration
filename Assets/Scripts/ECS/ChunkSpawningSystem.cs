using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ChunkSpawningSystem : ISystem
    {
        private Vector2Int _lastPlayerChunkPos;
        private BeginInitializationEntityCommandBufferSystem _entityCommandBufferSystem;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<ChunkSpawningState>();
            // We still need to ensure the player exists before this system runs.
            state.RequireForUpdate<PlayerPosition>();
            
            // To manage our state, we create a singleton entity. This entity will hold
            // our ChunkSpawningState component. We check if it already exists to prevent
            // creating it multiple times on domain reloads.
            if (SystemAPI.HasSingleton<ChunkSpawningState>()) return;
            
            var stateEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(stateEntity, new ChunkSpawningState
            {
                // Initialize with a value that guarantees the first update will run.
                LastPlayerChunkPos = new int2(int.MinValue, int.MinValue)
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // --- State and Singleton Retrieval ---
            // Get the singleton component that holds our state.
            // We also get the entity itself so we can update the component later.
            var chunkSpawningStateEntity = SystemAPI.GetSingletonEntity<ChunkSpawningState>();
            var chunkSpawningState = SystemAPI.GetComponent<ChunkSpawningState>(chunkSpawningStateEntity);

            // Get player position.
            var playerPos = SystemAPI.GetSingleton<PlayerPosition>().Value;

            // --- Check if Update is Needed ---
            // The core logic is the same, but now we access state from our component.
            var currentPlayerChunkPos = new int2(
                (int)math.floor(playerPos.x / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize,
                (int)math.floor(playerPos.z / VoxelConstants.ChunkSize) * VoxelConstants.ChunkSize);

            if (currentPlayerChunkPos.x == chunkSpawningState.LastPlayerChunkPos.x &&
                currentPlayerChunkPos.y == chunkSpawningState.LastPlayerChunkPos.y)
            {
                return; // No change in chunk position, no work to do.
            }

            // Update the state in our singleton component for the next frame.
            // Using SetComponentData is efficient for this.
            state.EntityManager.SetComponentData(chunkSpawningStateEntity, new ChunkSpawningState
            {
                LastPlayerChunkPos = currentPlayerChunkPos
            });

            // --- Command Buffer and Chunk Logic ---
            // Get a command buffer. The modern way is to get the ECB singleton and create
            // a command buffer from it. This correctly integrates into the frame's dependency chain.
            var ecbSingleton = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Using Temp allocators for these collections is correct as they only live for this frame.
            var requiredChunks = new NativeHashSet<int2>(256, Allocator.Temp);
            var activeChunks = new NativeHashMap<int2, Entity>(256, Allocator.Temp);

            // Populate the map of currently active chunks.
            // Using SystemAPI.Query is the modern, efficient way to query inside an ISystem.
            foreach (var (coord, entity) in SystemAPI.Query<RefRO<ChunkCoordinate>>().WithEntityAccess())
            {
                activeChunks.Add(coord.ValueRO.Value, entity);
            }

            // --- Spawning and Unloading Logic ---
            // This logic remains largely unchanged, as it was already data-oriented.
            const int radius = VoxelConstants.RenderDistance;

            for (var x = -radius; x <= radius; x++)
            {
                for (var z = -radius; z <= radius; z++)
                {
                    // Simple distance check can be done with lengthsq for efficiency.
                    if (x * x + z * z > radius * radius) continue;

                    var requiredPos = new int2(
                        currentPlayerChunkPos.x + (x * VoxelConstants.ChunkSize),
                        currentPlayerChunkPos.y + (z * VoxelConstants.ChunkSize)
                    );

                    requiredChunks.Add(requiredPos);
                    
                    if (activeChunks.ContainsKey(requiredPos)) continue;
                    
                    // If the chunk is required but not active, create it.
                    var newChunk = ecb.CreateEntity();
                    ecb.AddComponent(newChunk, new ChunkCoordinate { Value = requiredPos });
                    ecb.AddComponent<NewChunkTag>(newChunk); // Tag for the next stage in the pipeline.
                }
            }

            // Iterate through active chunks to see which ones are no longer required.
            foreach (var (coord, entity) in SystemAPI.Query<RefRO<ChunkCoordinate>>().WithEntityAccess())
            {
                if (!requiredChunks.Contains(coord.ValueRO.Value))
                {
                    // This chunk is no longer in render distance, tag it for unloading.
                    ecb.AddComponent<ToUnloadTag>(entity);
                }
            }

            // The native containers must be disposed.
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
        public const int RenderDistance = 2;
    }
}
