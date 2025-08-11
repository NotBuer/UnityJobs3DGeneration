using Unity.Entities;
using Unity.Mathematics;
using Voxel;

namespace ECS
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
    /// A dynamic buffer attached to a chunk entity that holds its raw voxel data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct VoxelDataBuffer : IBufferElementData
    {
        public VoxelType Value;
    }
    
}
