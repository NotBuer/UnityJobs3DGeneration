using System.Runtime.CompilerServices;
using Chunk;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS
{
    public unsafe struct ChunkMesh : IComponentData
    {
        public UnsafeList<Vector3>* Vertices;
        public UnsafeList<int>* Triangles;
        public UnsafeList<Vector3>* Normals;
        public UnsafeList<Color32>* Colors;
        public Bounds Bounds;

        public bool IsCreated => Vertices != null && Vertices->IsCreated;

        public void Dispose()
        {
            if (Vertices != null && Vertices->IsCreated) Vertices->Dispose();
            if (Triangles != null && Triangles->IsCreated) Triangles->Dispose();
            if (Normals != null && Normals->IsCreated) Normals->Dispose();
            if (Colors != null && Colors->IsCreated) Colors->Dispose();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DataGenerationSystem))]
    public partial class ChunkMeshingSystem : SystemBase
    {
        private EntityCommandBufferSystem _entityCommandBufferSystem;
        private EntityQuery _meshingQuery;
        private EntityQuery _allChunksQuery;
        
        protected override void OnCreate()
        {
            _entityCommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            
            _meshingQuery = 
                SystemAPI.QueryBuilder().WithAll<NeedsMeshingTag, ChunkCoordinate, VoxelDataBuffer>().Build();
            
            _allChunksQuery =
                SystemAPI.QueryBuilder().WithAll<ChunkCoordinate>().Build();
        }
        
        protected override void OnUpdate()
        {
            if (_meshingQuery.IsEmpty) return;
            
            // The job will use this map to find the neighbor entities.
            var coordToEntityMap = new NativeParallelHashMap<int2, Entity>
                (_allChunksQuery.CalculateEntityCount(), Allocator.TempJob);
            var coordPopulateJob = new PopulateCoordMapJob
            {
                CoordToEntityMap = coordToEntityMap.AsParallelWriter()
            };
            var coordPopulateJobHandle = coordPopulateJob.ScheduleParallel(_allChunksQuery, Dependency);

            var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer();
            var meshingJob = new MeshingJob
            {
                ChunkSize = VoxelConstants.ChunkSize,
                ChunkSizeY = VoxelConstants.ChunkSizeY,
                ChunkVoxelCount = ChunkUtils.GetChunkTotalSize(VoxelConstants.ChunkSize, VoxelConstants.ChunkSizeY),
                CommandBuffer = commandBuffer.AsParallelWriter(),
                CoordToEntityMap = coordToEntityMap,
                VoxelDataBufferLookup = SystemAPI.GetBufferLookup<VoxelDataBuffer>(true)
            };
            var meshingJobHandle = meshingJob.ScheduleParallel(_meshingQuery, coordPopulateJobHandle);
            
            _entityCommandBufferSystem.AddJobHandleForProducer(meshingJobHandle);
            coordToEntityMap.Dispose(meshingJobHandle);
            Dependency = meshingJobHandle;
        }
        
        protected override void OnDestroy() { }
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
    public unsafe partial struct MeshingJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        [ReadOnly] public BufferLookup<VoxelDataBuffer> VoxelDataBufferLookup;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> CoordToEntityMap;

        [ReadOnly] public byte ChunkSize;
        [ReadOnly] public byte ChunkSizeY;
        [ReadOnly] public int ChunkVoxelCount;
        
        public void Execute(
            Entity entity, 
            [ChunkIndexInQuery] int chunkIndex, 
            in ChunkCoordinate coord,
            in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        {
            var visibleFaces = 0;
            for (var voxelIndex = 0; voxelIndex < ChunkVoxelCount; voxelIndex++)
            {
                if (localVoxelBuffer[voxelIndex].Value == VoxelType.Air) continue;
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    if (IsFaceVisible(
                            in voxelIndex,
                            in VoxelUtils.NormalsInt3[faceIndex],
                            in coord.Value,
                            in localVoxelBuffer))
                    {
                        visibleFaces++;
                    }
                }
            }

            if (visibleFaces == 0)
            {
                CommandBuffer.RemoveComponent<NeedsMeshingTag>(chunkIndex, entity);
                CommandBuffer.AddComponent<NeedsRenderingTag>(chunkIndex, entity);
                CommandBuffer.AddComponent(chunkIndex, entity, new ChunkMesh());
                return;
            }

            var vertices = 
                UnsafeList<Vector3>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.TempJob);
            var triangles = 
                UnsafeList<int>.Create(visibleFaces * VoxelUtils.FaceCount, Allocator.TempJob);
            var normals = 
                UnsafeList<Vector3>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.TempJob);
            var colors = 
                UnsafeList<Color32>.Create(visibleFaces * VoxelUtils.FaceEdges, Allocator.TempJob);

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var vertexIndex = 0;
            
            for (var voxelIndex = 0; voxelIndex < ChunkVoxelCount; voxelIndex++)
            {
                var voxelType = localVoxelBuffer[voxelIndex].Value;
                
                if (voxelType == VoxelType.Air) continue;
                
                ChunkUtils.UnflattenIndexTo3DLocalCoords(
                    voxelIndex, ChunkSize, ChunkSizeY, out var x, out var y, out var z);
                
                var voxelPosition = new Vector3(
                    x + coord.Value.x,
                    y,
                    z + coord.Value.y);
                
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    if (!IsFaceVisible(
                            in voxelIndex,
                            in VoxelUtils.NormalsInt3[faceIndex],
                            in coord.Value,
                            in localVoxelBuffer)) continue;

                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex =
                            voxelPosition + 
                            VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                        
                        vertices->AddNoResize(vertex);
                        normals->AddNoResize(VoxelUtils.Normals[faceIndex]);
                        colors->AddNoResize(VoxelUtils.GetVoxelColor(in voxelType));
                        
                        boundsMin = Vector3.Min(boundsMin, vertex);
                        boundsMax = Vector3.Max(boundsMax, vertex);
                    }
                    
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 3);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex + 1);
                    
                    vertexIndex += VoxelUtils.FaceEdges;
                }
            }
            
            CommandBuffer.AddComponent(chunkIndex, entity, new ChunkMesh
            {
                Vertices = vertices,
                Triangles = triangles,
                Normals = normals,
                Colors = colors,
                Bounds = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin)
            });
            
            // Transition the state.
            CommandBuffer.RemoveComponent<NeedsMeshingTag>(chunkIndex, entity);
            CommandBuffer.AddComponent<NeedsRenderingTag>(chunkIndex, entity);
        }
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFaceVisible(
            in int voxelIndex,
            in int3 normal,
            in int2 chunkCoord,
            in DynamicBuffer<VoxelDataBuffer> localVoxelBuffer)
        {
            ChunkUtils.UnflattenIndexTo3DLocalCoords(
                voxelIndex, ChunkSize, ChunkSizeY, out var x, out var y, out var z);
            
            var neighborPos = new int3(x, y, z) + normal;
            
            if (neighborPos.y < 0 || neighborPos.y >= ChunkSizeY) return true;
            
            if (neighborPos.x >= 0 && neighborPos.x < ChunkSize && neighborPos.z >= 0 && neighborPos.z < ChunkSize)
            {
                return localVoxelBuffer[
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborPos.x, neighborPos.y, neighborPos.z, ChunkSize, ChunkSizeY)]
                    .Value == VoxelType.Air;
            }

            var neighborChunkCoord = new int2(
                chunkCoord.x + (normal.x * ChunkSize),
                chunkCoord.y + (normal.z * ChunkSize)
            );
            
            if (!CoordToEntityMap.TryGetValue(neighborChunkCoord, out var neighborChunkEntity)) return true;

            var neighborVoxelBuffer = VoxelDataBufferLookup[neighborChunkEntity];
            var neighborLocalPos = new int3(
                (neighborPos.x + ChunkSize) % ChunkSize,
                 neighborPos.y,
                (neighborPos.z + ChunkSize) % ChunkSize
            );
            
            return neighborVoxelBuffer[
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborLocalPos.x, neighborLocalPos.y, neighborLocalPos.z, ChunkSize, ChunkSizeY)]
                .Value == VoxelType.Air;
        }
    }
}