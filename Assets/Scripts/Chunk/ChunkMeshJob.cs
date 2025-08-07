using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public unsafe struct ChunkMeshJob : IJobParallelFor
    {
        private Mesh.MeshDataArray _chunkMeshDataArray;
        [WriteOnly] private NativeArray<Bounds> _chunkBoundsArray;
        [ReadOnly] private readonly int _chunkVoxelCount;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        [ReadOnly] private readonly byte _totalChunksPerAxis;
        [ReadOnly] private readonly NativeArray<ChunkData> _chunkDataArray;
        [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
    
        public ChunkMeshJob(
            Mesh.MeshDataArray chunkMeshDataArray, 
            NativeArray<Bounds> chunkBoundsArray,
            int chunkVoxelCount,
            byte chunkSize,
            byte chunkSizeY,
            byte totalChunksPerAxis,
            NativeArray<ChunkData> chunkDataArray, 
            NativeArray<VoxelData> voxelDataArray)
        {
            _chunkMeshDataArray = chunkMeshDataArray;
            _chunkBoundsArray = chunkBoundsArray;
            _chunkVoxelCount = chunkVoxelCount;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _totalChunksPerAxis = totalChunksPerAxis;
            _chunkDataArray = chunkDataArray;
            _voxelDataArray = voxelDataArray;
        }
        
        public void Execute(int index)
        {
            var chunkMeshData = _chunkMeshDataArray[index];
            
            var voxelStartIndex = _chunkVoxelCount * index;
            
            var currentChunkWorldX = _chunkDataArray[index].x;
            var currentChunkWorldZ = _chunkDataArray[index].z;

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var visibleFaces = 0;
            
            MeshJobCore.FirstPassGetVisibleFacesLocal(
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
            
            MeshJobCore.SecondPassGetVisibleFacesGlobal(
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
            
            MeshJobCore.SetMeshDataBuffers(
                ref chunkMeshData,
                ref vertices,
                ref triangles,
                ref normals,
                ref colors);
            
            _chunkBoundsArray[index] = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
            
            vertices->Dispose();
            triangles->Dispose();
            normals->Dispose();
            colors->Dispose();
        }
    }
}