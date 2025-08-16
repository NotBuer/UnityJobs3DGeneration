using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Voxel;

namespace ECS.Components
{
    // --- TAG COMPONENTS ---
    
    /// <summary>
    /// Tag added to a newly created chunk entity that needs its data buffer set up.
    /// </summary>
    public struct NewChunkTag : IComponentData { }

    /// <summary>
    /// Tag added to a chunk entity indicating that it requires voxel data generation.
    /// </summary>
    public struct NeedsDataGenerationTag : IComponentData { }
    
    /// <summary>
    /// Tag added to a chunk entity after its data is generated, indicating it's ready for meshing.
    /// </summary>
    public struct NeedsMeshingTag : IComponentData { }
    
    /// <summary>
    /// Tag added to a chunk entity after its mesh data is generated, but still needs to be rendered to screen.
    /// </summary>
    public struct NeedsRenderingTag : IComponentData { }
    
    /// <summary>
    /// Tag added to an active and rendered chunk.
    /// </summary>
    public struct ActiveChunkTag : IComponentData { }
    
    /// <summary>
    /// Tag added to a chunk entity outside the render distance and should be destroyed.
    /// </summary>
    public struct ToUnloadTag : IComponentData { }
    
    
    
    // --- DATA COMPONENTS ---

    /// <summary>
    /// The unique identifier for a chunk entity. This is its position in the world grid.
    /// </summary>
    public struct ChunkCoordinate : IComponentData
    {
        public int2 Value;
    }

    /// <summary>
    /// Represents the state of chunk spawning, including tracking the player's last known chunk position.
    /// </summary>
    public struct ChunkSpawningState : IComponentData
    {
        public int2 LastPlayerChunkPos;
    }

    /// <summary>
    /// A dynamic buffer attached to a chunk entity that holds its raw voxel data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct VoxelDataBuffer : IBufferElementData
    {
        public VoxelType Value;
    }

    /// <summary>
    /// A dynamic buffer attached to a chunk entity that holds the vertex data for the chunk's mesh.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ChunkVertexBuffer : IBufferElementData
    {
        public float3 Value;
    }

    /// <summary>
    /// A dynamic buffer attached to a chunk entity that holds triangle index data for the chunk's mesh.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ChunkTriangleBuffer : IBufferElementData
    {
        public int Value;
    }

    /// <summary>
    /// A dynamic buffer attached to a chunk entity that holds its normals data for the mesh.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ChunkNormalBuffer : IBufferElementData
    {
        public float3 Value;
    }

    /// <summary>
    /// A dynamic buffer attached to a chunk entity that holds the color information for each vertex in the chunk.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ChunkColorBuffer : IBufferElementData
    {
        public Color32 Value;
    }

    /// <summary>
    /// Component holding the bounding volume of a chunk's mesh, used for spatial calculations and culling.
    /// </summary>
    public struct ChunkMeshBounds : IComponentData
    {
        public Bounds Value;
    }

    /// <summary>
    /// A World component for world generation settings,
    /// used to define frequency, amplitude, seed... for procedural generation.
    /// </summary>
    public struct WorldConfiguration : IComponentData
    {
        public byte ChunkSize;
        public byte ChunkSizeY;
        public float Frequency;
        public float Amplitude;
        public ulong Seed;
    }

    /// <summary>
    /// Component that holds rendering resources required for voxel rendering, specifically the material to be used.
    /// </summary>
    public class VoxelRenderResources : IComponentData
    {
        public Material VoxelMaterial;
    }

    /// <summary>
    /// Used as a component to store the player's coordinates for systems, that need to access or update the player's spatial data.
    /// </summary>
    public struct PlayerPosition : IComponentData
    {
        public float3 Value;
    }
    
}
