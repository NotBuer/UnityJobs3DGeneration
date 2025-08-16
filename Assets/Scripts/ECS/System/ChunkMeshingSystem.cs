using System.Runtime.CompilerServices;
using Chunk;
using ECS.Components;
using ECS.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS.System
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
            state.RequireForUpdate<WorldConfiguration>();
            
            _meshingQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsMeshingTag, ChunkCoordinate, VoxelDataBuffer>()
                .Build();

            _readyChunksQuery = SystemAPI.QueryBuilder()
                .WithAll<ChunkCoordinate, VoxelDataBuffer>()
                .WithNone<ToUnloadTag, NeedsDataGenerationTag>()
                .Build();
            
            state.RequireForUpdate(_meshingQuery);
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var coordToEntityMap = new NativeParallelHashMap<int2, Entity>(
                _readyChunksQuery.CalculateEntityCount(), Allocator.TempJob);

            var populateCoordMapJob = new PopulateCoordMapJob
            {
                CoordToEntityMap = coordToEntityMap.AsParallelWriter()
            };
            
            var populateJobHandle = populateCoordMapJob.ScheduleParallel(_readyChunksQuery, state.Dependency);
            
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var meshingJob = new MeshingJob
            {
                CommandBuffer = ecb,
                VoxelDataBufferLookup = SystemAPI.GetBufferLookup<VoxelDataBuffer>(true),
                CoordToEntityMap = coordToEntityMap.AsReadOnly(),
                // ChunkSize = Settings.World.Data.ChunkSize,
                // ChunkSizeY = Settings.World.Data.ChunkSizeY
            };
            
            var meshingJobHandle = meshingJob.ScheduleParallel(_meshingQuery, populateJobHandle);
            
            coordToEntityMap.Dispose(meshingJobHandle);
            
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
        // [ReadOnly] public int ChunkSize;
        // [ReadOnly] public int ChunkSizeY;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord,
            in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        {
            var vertices = new NativeList<float3>(0xFFFF, Allocator.Temp);
            var triangles = new NativeList<int>(0xFFFF, Allocator.Temp);
            var normals = new NativeList<float3>(0xFFFF, Allocator.Temp);
            var colors = new NativeList<Color32>(0xFFFF, Allocator.Temp);

            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);
            
            for (var i = 0; i < localVoxelBuffer.Length; i++)
            {
                var voxelType = localVoxelBuffer[i].Value;
                if (voxelType == (byte)VoxelType.Air) continue;
                
                ChunkUtils.UnflattenIndex(i, Settings.World.Data.ChunkSize, out var x, out var y, out var z);
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
                    triangles.Add(vertexIndex + 3);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
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
            ChunkUtils.UnflattenIndex(voxelIndex, Settings.World.Data.ChunkSize, out var x, out var y, out var z);
            
            var neighborPos = new int3(x, y, z) + normal;
            
            if (neighborPos.y < 0 || neighborPos.y >= Settings.World.Data.ChunkSizeY) return true;
            
            if (neighborPos.x >= 0 && neighborPos.x < Settings.World.Data.ChunkSize && 
                neighborPos.z >= 0 && neighborPos.z < Settings.World.Data.ChunkSize)
            {
                ChunkUtils.FlattenIndex(neighborPos, Settings.World.Data.ChunkSize, out var localIndex);
                return localVoxelBuffer[localIndex].Value == (byte)VoxelType.Air;
            }
            
            var neighborChunkCoord = chunkCoord + new int2(
                normal.x * Settings.World.Data.ChunkSize, 
                normal.z * Settings.World.Data.ChunkSize
            );
            
            if (!CoordToEntityMap.TryGetValue(neighborChunkCoord, out var neighborChunkEntity)) return true;
            if (!VoxelDataBufferLookup.HasBuffer(neighborChunkEntity)) return true;
            
            var neighborVoxelBuffer = VoxelDataBufferLookup[neighborChunkEntity];
            var neighborLocalPos = new int3(
                (neighborPos.x % Settings.World.Data.ChunkSize + Settings.World.Data.ChunkSize) % Settings.World.Data.ChunkSize, 
                 neighborPos.y, 
                (neighborPos.z % Settings.World.Data.ChunkSize + Settings.World.Data.ChunkSize) % Settings.World.Data.ChunkSize);
            
            ChunkUtils.FlattenIndex(neighborLocalPos, Settings.World.Data.ChunkSize, out var neighborIndex);
            return neighborVoxelBuffer[neighborIndex].Value == (byte)VoxelType.Air;
        }
    }
}