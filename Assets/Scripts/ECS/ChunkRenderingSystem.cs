using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS
{
    // --- System Refactoring ---
    [BurstCompile]
    // This system runs in the PresentationSystemGroup, which is the correct place for rendering logic.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChunkMeshingSystem))]
    public partial struct ChunkRenderingSystem : ISystem
    {
        private EntityQuery _chunksWithMeshQuery;
        private EntityQuery _chunksWithoutMeshQuery;
        private EntityQuery _voxelResourceQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<VoxelRenderResources>();
            // Query for chunks that have generated mesh data and need to be rendered.
            _chunksWithMeshQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsRenderingTag, ChunkCoordinate, ChunkMeshBounds>()
                .WithAll<ChunkVertexBuffer, ChunkTriangleBuffer, ChunkNormalBuffer, ChunkColorBuffer>()
                .Build();
            
            // Query for chunks that are tagged for rendering but have no mesh data (i.e., empty chunks).
            _chunksWithoutMeshQuery = SystemAPI.QueryBuilder()
                .WithAll<NeedsRenderingTag, ChunkCoordinate>()
                .WithNone<ChunkVertexBuffer>()
                .Build();
            
            _voxelResourceQuery = SystemAPI.QueryBuilder().WithAll<VoxelRenderResources>().Build();

            // This system needs a material to work with. We create a singleton entity to hold it.
            // This is done once on creation.
            var resourceEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentObject(resourceEntity, new VoxelRenderResources
            {
                // In a real project, you would load this from an asset bundle or Resources folder.
                VoxelMaterial = new Material(Shader.Find("Custom/VoxelShader_WithLighting"))
            });
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // We must complete any incoming dependencies from the meshing job before we can read the buffers.
            state.Dependency.Complete();

            var ecb = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var singletonEntity = _voxelResourceQuery.GetSingletonEntity();
            var voxelResources = state.EntityManager.GetComponentObject<VoxelRenderResources>(singletonEntity);

            var entitiesWithMesh = _chunksWithMeshQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entitiesWithMesh)
            {
                // Get the mesh data buffers from the entity.
                var coord = SystemAPI.GetComponent<ChunkCoordinate>(entity);
                var bounds = SystemAPI.GetComponent<ChunkMeshBounds>(entity);
                var vertices = SystemAPI.GetBuffer<ChunkVertexBuffer>(entity);
                var triangles = SystemAPI.GetBuffer<ChunkTriangleBuffer>(entity);
                var normals = SystemAPI.GetBuffer<ChunkNormalBuffer>(entity);
                var colors = SystemAPI.GetBuffer<ChunkColorBuffer>(entity);
                
                // Create a new Unity Mesh. This must be done on the main thread.
                var mesh = new Mesh
                {
                    name = $"ChunkMesh {coord.Value.x}:{coord.Value.y}",
                    bounds = bounds.Value
                };
                
                // Define the vertex layout.
                var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory)
                {
                    [0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0),
                    [1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1),
                    [2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, stream: 2)
                };
                
                mesh.SetVertexBufferParams(vertices.Length, vertexAttributes);
                vertexAttributes.Dispose();
                
                mesh.SetVertexBufferData(vertices.AsNativeArray().Reinterpret<float3>(), 0, 0, vertices.Length, 0);
                mesh.SetVertexBufferData(normals.AsNativeArray().Reinterpret<float3>(), 0, 0, normals.Length, 1);
                mesh.SetVertexBufferData(colors.AsNativeArray().Reinterpret<Color32>(), 0, 0, colors.Length, 2);
                
                mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
                mesh.SetIndexBufferData(triangles.AsNativeArray().Reinterpret<int>(), 0, 0, triangles.Length);

                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length), MeshUpdateFlags.DontRecalculateBounds);

                // Add all necessary components for rendering.
                var renderMesh = new RenderMeshUnmanaged(mesh, voxelResources.VoxelMaterial);
                ecb.AddComponent(entity, renderMesh);
                ecb.AddComponent(entity, new LocalToWorld { Value = float4x4.Translate(new float3(coord.Value.x, 0, coord.Value.y)) });
                ecb.AddComponent(entity, new RenderBounds { Value = mesh.bounds.ToAABB() });
                ecb.AddComponent<MaterialMeshInfo>(entity);
                
                // Clean up the temporary mesh data buffers.
                ecb.RemoveComponent<ChunkVertexBuffer>(entity);
                ecb.RemoveComponent<ChunkTriangleBuffer>(entity);
                ecb.RemoveComponent<ChunkNormalBuffer>(entity);
                ecb.RemoveComponent<ChunkColorBuffer>(entity);
                ecb.RemoveComponent<ChunkMeshBounds>(entity);
                
                // Transition the chunk to its final, active state.
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