using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public unsafe struct SingleChunkMeshJob : IJob
    {
        private Mesh.MeshDataArray _chunkMeshDataArray;
        [WriteOnly] private NativeReference<Bounds> _chunkBoundsRef;
        [ReadOnly] private readonly int _chunkVoxelCount;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        [ReadOnly] private readonly byte _totalChunksPerAxis;
        [ReadOnly] private readonly NativeArray<ChunkData> _chunkDataArray;
        [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
        [ReadOnly] private readonly ushort _index;
    
        public SingleChunkMeshJob(
            Mesh.MeshDataArray chunkMeshDataArray, 
            NativeReference<Bounds> chunkBoundsRef,
            int chunkVoxelCount,
            byte chunkSize,
            byte chunkSizeY,
            byte totalChunksPerAxis,
            NativeArray<ChunkData> chunkDataArray, 
            NativeArray<VoxelData> voxelDataArray,
            ushort index)
        {
            _chunkMeshDataArray = chunkMeshDataArray;
            _chunkBoundsRef = chunkBoundsRef;
            _chunkVoxelCount = chunkVoxelCount;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _totalChunksPerAxis = totalChunksPerAxis;
            _chunkDataArray = chunkDataArray;
            _voxelDataArray = voxelDataArray;
            _index = index;
        }
        
        public void Execute()
        {
            var chunkMeshData = _chunkMeshDataArray[0];
            
            var voxelStartIndex = _chunkVoxelCount * _index;
            
            var currentChunkWorldX = _chunkDataArray[_index].x;
            var currentChunkWorldZ = _chunkDataArray[_index].z;

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var visibleFaces = 0;
            
            ChunkMeshCore.FirstPassGetVisibleFacesLocal(
                in _chunkVoxelCount,
                in _voxelDataArray,
                in _chunkSize,
                in _chunkSizeY,
                in _totalChunksPerAxis,
                in voxelStartIndex,
                in currentChunkWorldX,
                in currentChunkWorldZ,
                ref visibleFaces,
                true);
            
            ChunkMeshCore.SecondPassGetVisibleFacesGlobal(
                in _chunkVoxelCount,
                in _voxelDataArray,
                in _chunkSize,
                in _chunkSizeY,
                in _totalChunksPerAxis,
                in visibleFaces,
                in voxelStartIndex,
                out var vertices,
                out var triangles,
                out var normals,
                out var colors,
                in currentChunkWorldX,
                in currentChunkWorldZ,
                false,
                ref boundsMin,
                ref boundsMax);
            
            _chunkBoundsRef.Value = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
            
            ChunkMeshCore.SetMeshDataBuffers(
                ref chunkMeshData,
                ref vertices,
                ref triangles,
                ref normals,
                ref colors);
            
            vertices->Dispose();
            triangles->Dispose();
            normals->Dispose();
            colors->Dispose();
        }
    }   
}
