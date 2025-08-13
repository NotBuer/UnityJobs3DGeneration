using System.Runtime.CompilerServices;
using Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DataGenerationSystem))]
    public partial struct ChunkMeshingSystem : ISystem
    {
        private EntityQuery _meshingQuery;
        private EntityQuery _readyChunksQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _meshingQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsMeshingTag, ChunkCoordinate, VoxelDataBuffer>()
                .Build();

            _readyChunksQuery = SystemAPI.QueryBuilder()
                .WithAll<ChunkCoordinate, VoxelDataBuffer>()
                .WithNone<ToUnloadTag, NeedsDataGenerationTag, NeedsMeshingTag>()
                .Build();
            
            state.RequireForUpdate(_meshingQuery);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // --- Job 1: Populate Neighbor Map ---
            // This map is essential for the meshing job to look up neighbor chunks.
            var coordToEntityMap = new NativeParallelHashMap<int2, Entity>(
                _readyChunksQuery.CalculateEntityCount(), Allocator.TempJob);

            var populateCoordMapJob = new PopulateCoordMapJob
            {
                CoordToEntityMap = coordToEntityMap.AsParallelWriter()
            };
            
            // Schedule the first job, depending on the state of the world before this system.
            var populateJobHandle = populateCoordMapJob.ScheduleParallel(_readyChunksQuery, state.Dependency);

            // --- Job 2: Generate Mesh ---
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var meshingJob = new MeshingJob
            {
                CommandBuffer = ecb,
                VoxelDataBufferLookup = SystemAPI.GetBufferLookup<VoxelDataBuffer>(true),
                CoordToEntityMap = coordToEntityMap.AsReadOnly(),
                ChunkSize = VoxelConstants.ChunkSize,
                ChunkSizeY = VoxelConstants.ChunkSizeY
            };
            
            // CRITICAL: Schedule the second job. Its dependency is the handle from the FIRST job.
            // This ensures the map is fully populated before the meshing job tries to read from it.
            var meshingJobHandle = meshingJob.ScheduleParallel(_meshingQuery, populateJobHandle);

            // We must dispose the map. We pass the handle of the *last* job that uses it.
            coordToEntityMap.Dispose(meshingJobHandle);

            // Finally, update the system's dependency to be the handle of our final job.
            state.Dependency = meshingJobHandle;
        }
    }

    [BurstCompile]
    public partial struct PopulateCoordMapJob : IJobEntity
    {
        public NativeParallelHashMap<int2, Entity>.ParallelWriter CoordToEntityMap;

        public void Execute(Entity entity, in ChunkCoordinate coord)
        {
            CoordToEntityMap.TryAdd(coord.Value, entity);
        }
    }

        [BurstCompile]
    public partial struct MeshingJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        [ReadOnly] public BufferLookup<VoxelDataBuffer> VoxelDataBufferLookup;
        [ReadOnly] public NativeParallelHashMap<int2, Entity>.ReadOnly CoordToEntityMap;
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int ChunkSizeY;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord,
            in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        {
            // Use Temp allocator for mesh data; it's short-lived.
            var vertices = new NativeList<float3>(Allocator.Temp);
            var triangles = new NativeList<int>(Allocator.Temp);
            var normals = new NativeList<float3>(Allocator.Temp);
            var colors = new NativeList<Color32>(Allocator.Temp);

            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);
            
            for (var i = 0; i < localVoxelBuffer.Length; i++)
            {
                var voxelType = localVoxelBuffer[i].Value;
                if (voxelType == (byte)VoxelType.Air) continue;
                
                ChunkUtils.UnflattenIndexTo3DLocalCoords(i, ChunkSize, ChunkSizeY, out var x, out var y, out var z);
                var voxelPosition = new float3(x, y, z);
                
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    if (!IsFaceVisible(i, VoxelUtils.NormalsInt3[faceIndex], coord.Value, localVoxelBuffer)) continue;

                    var vertexIndex = vertices.Length;
                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex = voxelPosition + VoxelUtils.VerticesFloat3[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                        vertices.Add(vertex);
                        normals.Add(VoxelUtils.NormalsInt3[faceIndex]);
                        VoxelUtils.GetVoxelColor(voxelType, out var color);
                        colors.Add(color);
                        
                        boundsMin = math.min(boundsMin, vertex);
                        boundsMax = math.max(boundsMax, vertex);
                    }
                    
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 3);
                }
            }

            if (vertices.Length > 0)
            {
                // Add mesh data to dynamic buffers on the entity.
                var vertBuffer = CommandBuffer.AddBuffer<ChunkVertexBuffer>(chunkIndex, entity);
                vertBuffer.CopyFrom(vertices.AsArray().Reinterpret<ChunkVertexBuffer>());

                var triBuffer = CommandBuffer.AddBuffer<ChunkTriangleBuffer>(chunkIndex, entity);
                triBuffer.CopyFrom(triangles.AsArray().Reinterpret<ChunkTriangleBuffer>());

                var normBuffer = CommandBuffer.AddBuffer<ChunkNormalBuffer>(chunkIndex, entity);
                normBuffer.CopyFrom(normals.AsArray().Reinterpret<ChunkNormalBuffer>());
                
                var colorBuffer = CommandBuffer.AddBuffer<ChunkColorBuffer>(chunkIndex, entity);
                colorBuffer.CopyFrom(colors.AsArray().Reinterpret<ChunkColorBuffer>());

                var bounds = new Bounds();
                bounds.SetMinMax(boundsMin, boundsMax);
                CommandBuffer.AddComponent(chunkIndex, entity, new ChunkMeshBounds { Value = bounds });
            }
            
            // Transition the chunk to the next state.
            CommandBuffer.RemoveComponent<NeedsMeshingTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsRenderingTag>(chunkIndex, entity);
        }
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFaceVisible(int voxelIndex, int3 normal, int2 chunkCoord, in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        {
            ChunkUtils.UnflattenIndexTo3DLocalCoords
                (voxelIndex, ChunkSize, ChunkSizeY, out var x, out var y, out var z);
            
            var neighborPos = new int3(x, y, z) + normal;
            
            if (neighborPos.y < 0 || neighborPos.y >= ChunkSizeY) return true;
            
            if (neighborPos.x >= 0 && neighborPos.x < ChunkSize && neighborPos.z >= 0 && neighborPos.z < ChunkSize)
            {
                return localVoxelBuffer[
                        ChunkUtils.Flatten3DLocalCoordsToIndex
                            (0, neighborPos.x, neighborPos.y, neighborPos.z, ChunkSize, ChunkSizeY)]
                    .Value == (byte)VoxelType.Air;
            }
            
            var neighborChunkCoord = chunkCoord + new int2(normal.x, normal.z);
            if (!CoordToEntityMap.TryGetValue(neighborChunkCoord, out var neighborChunkEntity)) return true;
            if (!VoxelDataBufferLookup.HasBuffer(neighborChunkEntity)) return true;
            
            var neighborVoxelBuffer = VoxelDataBufferLookup[neighborChunkEntity];
            var neighborLocalPos = new int3(
                (neighborPos.x % ChunkSize + ChunkSize) % ChunkSize, 
                 neighborPos.y, 
                (neighborPos.z % ChunkSize + ChunkSize) % ChunkSize);
            
            return neighborVoxelBuffer[
                ChunkUtils.Flatten3DLocalCoordsToIndex
                    (0, neighborLocalPos.x, neighborLocalPos.y, neighborLocalPos.z, ChunkSize, ChunkSizeY)]
                .Value == (byte)VoxelType.Air;
        }
        
        // [BurstCompile]
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private bool IsFaceVisible(
        //     in int voxelIndex,
        //     in int3 normal,
        //     in int2 chunkCoord,
        //     in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        // {
        //     ChunkUtils.UnflattenIndexTo3DLocalCoords(
        //         voxelIndex, ChunkSize, ChunkSizeY, out var x, out var y, out var z);
        //     
        //     var neighborPos = new int3(x, y, z) + normal;
        //     
        //     // Face is visible if it's at the vertical edge of the world.
        //     if (neighborPos.y < 0 || neighborPos.y >= ChunkSizeY) return true;
        //     
        //     // Check if the neighbor is within the bounds of the current chunk.
        //     if (neighborPos.x >= 0 && neighborPos.x < ChunkSize && neighborPos.z >= 0 && neighborPos.z < ChunkSize)
        //     {
        //         return localVoxelBuffer[
        //             ChunkUtils.Flatten3DLocalCoordsToIndex(
        //                 0, neighborPos.x, neighborPos.y, neighborPos.z, ChunkSize, ChunkSizeY)]
        //             .Value == VoxelType.Air;
        //     }
        //     
        //     // If not in the local chunk, find the neighbor chunk.
        //     var neighborChunkCoord = new int2(
        //         chunkCoord.x + (normal.x * ChunkSize),
        //         chunkCoord.y + (normal.z * ChunkSize)
        //     );
        //     
        //     // If the neighbor chunk doesn't exist in our map of ready chunks, the face is visible.
        //     if (!CoordToEntityMap.TryGetValue(neighborChunkCoord, out var neighborChunkEntity)) return true;
        //     
        //     // Defensive checks:
        //     if (neighborChunkEntity == Entity.Null)
        //         return true;
        //
        //     // BufferLookup has HasBuffer (safe in burst) — use it before indexing:
        //     if (!VoxelDataBufferLookup.HasBuffer(neighborChunkEntity))
        //         return true;
        //     
        //     var neighborVoxelBuffer = VoxelDataBufferLookup[neighborChunkEntity];
        //     
        //     var neighborLocalPos = new int3(
        //         (neighborPos.x % ChunkSize + ChunkSize) % ChunkSize,
        //          neighborPos.y,
        //         (neighborPos.z % ChunkSize + ChunkSize) % ChunkSize
        //     );
        //     
        //     return neighborVoxelBuffer[
        //             ChunkUtils.Flatten3DLocalCoordsToIndex(
        //                 0, neighborLocalPos.x, neighborLocalPos.y, neighborLocalPos.z, ChunkSize, ChunkSizeY)]
        //         .Value == VoxelType.Air;
        // }
    }
}