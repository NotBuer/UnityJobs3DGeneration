using System.Text;
using LowLevel;
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
    // This system runs in the presentation group to ensure it runs after all simulation logic.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ChunkRenderingSystem : SystemBase
    {
        private EntityCommandBufferSystem _entityCommandBufferSystem;
        
        protected override void OnCreate()
        {
            _entityCommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }
        
        protected override unsafe void OnUpdate()
        {
            // Must complete the dependency from the meshing system before can safely access the ChunkMesh components.
            Dependency.Complete();
            
            var commandBuffer = _entityCommandBufferSystem.CreateCommandBuffer();

            foreach (var (chunkMesh, coord, entity) 
                     in SystemAPI.Query<RefRW<ChunkMesh>, RefRO<ChunkCoordinate>>().WithAll<NeedsRenderingTag>().WithEntityAccess())
            {
                // For empty chunks, set them up for rendering and move on.
                if (!chunkMesh.ValueRO.IsCreated || chunkMesh.ValueRO.Vertices->IsEmpty)
                {
                    commandBuffer.AddComponent<LocalToWorld>(entity);
                    commandBuffer.AddComponent<RenderBounds>(entity);
                    commandBuffer.AddComponent<MaterialMeshInfo>(entity);
                    commandBuffer.RemoveComponent<NeedsRenderingTag>(entity);
                    chunkMesh.ValueRW.Dispose();
                    continue;
                }
                
                // Create a new mesh from job's data.
                var mesh = new Mesh
                {
                    name = new StringBuilder($"ChunkMesh ({coord.ValueRO.Value.x}, {coord.ValueRO.Value.y})").ToString(),
                    bounds = chunkMesh.ValueRO.Bounds
                };
                
                var vertexAttributes = new NativeArray<VertexAttributeDescriptor>
                    (3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            
                vertexAttributes[0] = new VertexAttributeDescriptor
                    (VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
                vertexAttributes[1] = new VertexAttributeDescriptor
                    (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
                vertexAttributes[2] = new VertexAttributeDescriptor
                    (VertexAttribute.Color, VertexAttributeFormat.UNorm8, dimension: 4, stream: 2);
                
                mesh.SetVertexBufferParams(chunkMesh.ValueRO.Vertices->Length, vertexAttributes);
                mesh.SetIndexBufferParams(chunkMesh.ValueRO.Triangles->Length, IndexFormat.UInt32);
                
                mesh.SetVertexBufferData(
                    NativeArrayUnsafe.AsNativeArray(chunkMesh.ValueRO.Vertices), 
                    0, 
                    0, 
                    chunkMesh.ValueRO.Vertices->Length, 
                    stream: 0);
                
                mesh.SetVertexBufferData(
                    NativeArrayUnsafe.AsNativeArray(chunkMesh.ValueRO.Normals),
                    0,
                    0,
                    chunkMesh.ValueRO.Normals->Length,
                    stream: 1);
                
                mesh.SetVertexBufferData(
                    NativeArrayUnsafe.AsNativeArray(chunkMesh.ValueRO.Colors),
                    0,
                    0,
                    chunkMesh.ValueRO.Colors->Length,
                    stream: 2);
                
                mesh.SetIndexBufferData(
                    NativeArrayUnsafe.AsNativeArray(chunkMesh.ValueRO.Triangles),
                    0,
                    0,
                    chunkMesh.ValueRO.Triangles->Length);

                vertexAttributes.Dispose();

                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0,
                    new SubMeshDescriptor(0, chunkMesh.ValueRO.Triangles->Length), flags:
                    MeshUpdateFlags.DontValidateIndices | 
                    MeshUpdateFlags.DontResetBoneBounds | 
                    MeshUpdateFlags.DontNotifyMeshUsers | 
                    MeshUpdateFlags.DontRecalculateBounds);
                
                // Add the essential rendering parts to the entity.
                // TODO: Find optimized way to pass the material.
                var renderMeshUnmanaged = new RenderMeshUnmanaged(
                    mesh,
                    new Material(Shader.Find("Custom/VoxelShader_WithLighting")));
                
                commandBuffer.AddComponent(entity, renderMeshUnmanaged);
                commandBuffer.AddComponent(entity, 
                    new LocalToWorld
                    {
                        Value = float4x4.Translate(new float3(coord.ValueRO.Value.x, 0, coord.ValueRO.Value.y))
                    });
                commandBuffer.AddComponent(entity, new RenderBounds { Value = mesh.bounds.ToAABB() });
                commandBuffer.AddComponent<MaterialMeshInfo>(entity);
                
                // Clean up
                chunkMesh.ValueRW.Dispose();
                commandBuffer.AddComponent<ActiveChunkTag>(entity);
                commandBuffer.RemoveComponent<NeedsRenderingTag>(entity);
            }
        }
        
        protected override void OnDestroy() { }
    }
}