using ECS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ChunkRenderingSystem : ISystem
    {
        private EntityQuery _chunksWithMeshQuery;
        private EntityQuery _chunksWithoutMeshQuery;
        private EntityQuery _voxelResourceQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldConfiguration>();
            
            state.RequireForUpdate<VoxelRenderResources>();
            
            _chunksWithMeshQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsRenderingTag, ChunkCoordinate, ChunkMeshBounds>()
                .WithAll<ChunkVertexBuffer, ChunkTriangleBuffer, ChunkNormalBuffer, ChunkColorBuffer>()
                .Build();
            
            _chunksWithoutMeshQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsRenderingTag, ChunkCoordinate>()
                .WithNone<ChunkVertexBuffer>()
                .Build();
            
            _voxelResourceQuery = SystemAPI.QueryBuilder().WithAll<VoxelRenderResources>().Build();
            
            var resourceEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentObject(resourceEntity, new VoxelRenderResources
            {
                VoxelMaterial = new Material(Shader.Find("Custom/VoxelShader_WithLighting"))
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            var ecb = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var singletonEntity = _voxelResourceQuery.GetSingletonEntity();
            var voxelResources = state.EntityManager.GetComponentObject<VoxelRenderResources>(singletonEntity);

            var materialRef = new UnityObjectRef<Material>
            {
                Value = voxelResources.VoxelMaterial
            };

            var entitiesWithMesh = _chunksWithMeshQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entitiesWithMesh)
            {
                var coord = SystemAPI.GetComponent<ChunkCoordinate>(entity);
                var bounds = SystemAPI.GetComponent<ChunkMeshBounds>(entity);
                var vertices = SystemAPI.GetBuffer<ChunkVertexBuffer>(entity);
                var triangles = SystemAPI.GetBuffer<ChunkTriangleBuffer>(entity);
                var normals = SystemAPI.GetBuffer<ChunkNormalBuffer>(entity);
                var colors = SystemAPI.GetBuffer<ChunkColorBuffer>(entity);

                var meshRef = new UnityObjectRef<Mesh>
                {
                    Value = new Mesh
                    {
                        name = $"ChunkMesh {coord.Value.x}:{coord.Value.y}",
                        bounds = bounds.Value
                    }
                };
                
                // TODO: Cache this on initialization and dispose it a destroy time, no need to create it on the fly.
                var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory)
                {
                    [0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0),
                    [1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1),
                    [2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, stream: 2)
                };
                
                meshRef.Value.SetVertexBufferParams(vertices.Length, vertexAttributes);
                vertexAttributes.Dispose();
                
                meshRef.Value.SetVertexBufferData(vertices.AsNativeArray().Reinterpret<float3>(), 0, 0, vertices.Length, 0);
                meshRef.Value.SetVertexBufferData(normals.AsNativeArray().Reinterpret<float3>(), 0, 0, normals.Length, 1);
                meshRef.Value.SetVertexBufferData(colors.AsNativeArray().Reinterpret<Color32>(), 0, 0, colors.Length, 2);
                
                meshRef.Value.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
                meshRef.Value.SetIndexBufferData(triangles.AsNativeArray().Reinterpret<int>(), 0, 0, triangles.Length);
                
                meshRef.Value.subMeshCount = 1;
                meshRef.Value.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length), flags:
                    MeshUpdateFlags.DontRecalculateBounds | 
                    MeshUpdateFlags.DontValidateIndices | 
                    MeshUpdateFlags.DontNotifyMeshUsers);
                
                var renderMeshDescription = new RenderMeshDescription(
                    shadowCastingMode: ShadowCastingMode.On,
                    receiveShadows: true);
                
                var renderMeshArray = new RenderMeshArray(
                    new [] { materialRef.Value }, 
                    new [] { meshRef.Value }
                );
                
                var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0, 0);
                
                RenderMeshUtility.AddComponents(
                    entity, state.EntityManager, renderMeshDescription, renderMeshArray, materialMeshInfo);
                
                state.EntityManager.SetComponentData(entity, new LocalToWorld
                {
                    Value = float4x4.Translate(new float3(coord.Value.x, 0, coord.Value.y))
                });
                
                ecb.RemoveComponent<ChunkVertexBuffer>(entity);
                ecb.RemoveComponent<ChunkTriangleBuffer>(entity);
                ecb.RemoveComponent<ChunkNormalBuffer>(entity);
                ecb.RemoveComponent<ChunkColorBuffer>(entity);
                ecb.RemoveComponent<ChunkMeshBounds>(entity);
                
                ecb.RemoveComponent<NeedsRenderingTag>(entity);
                ecb.AddComponent<ActiveChunkTag>(entity);
            }
            entitiesWithMesh.Dispose();
            
            var emptyChunkEntities = _chunksWithoutMeshQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in emptyChunkEntities)
            {
                ecb.RemoveComponent<NeedsRenderingTag>(entity);
                ecb.AddComponent<ActiveChunkTag>(entity);
            }
            emptyChunkEntities.Dispose();
        }
    }
}