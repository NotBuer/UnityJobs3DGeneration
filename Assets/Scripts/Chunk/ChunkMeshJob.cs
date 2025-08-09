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
        [ReadOnly] private readonly int _chunkSizeInVoxels;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        
        [ReadOnly, NativeDisableParallelForRestriction] 
        private readonly NativeArray<Vector2Int> _chunkCoordsArray;
        
        [ReadOnly, NativeDisableParallelForRestriction] 
        private NativeArray<VoxelData> _voxelDataArray;
        
        [NativeDisableParallelForRestriction] 
        private readonly NativeParallelHashMap<Vector2Int, int>.ReadOnly _coordTableHashMap;
        
        [NativeDisableParallelForRestriction] private Mesh.MeshDataArray _chunkMeshDataArray;
        [WriteOnly, NativeDisableParallelForRestriction] private NativeArray<Bounds> _chunkBoundsArray;

        public ChunkMeshJob(
            int chunkSizeInVoxels,
            byte chunkSize, 
            byte chunkSizeY,
            NativeArray<Vector2Int> chunkCoordsArray,
            NativeArray<VoxelData> voxelDataArray,
            NativeParallelHashMap<Vector2Int, int>.ReadOnly coordTableHashMap,
            Mesh.MeshDataArray chunkMeshDataArray,
            NativeArray<Bounds> chunkBoundsArray) : this()
        {
            _chunkSizeInVoxels = chunkSizeInVoxels;
            _chunkSize = chunkSize;
            _chunkSizeY = chunkSizeY;
            _chunkCoordsArray = chunkCoordsArray;
            _voxelDataArray = voxelDataArray;
            _coordTableHashMap = coordTableHashMap;
            _chunkMeshDataArray = chunkMeshDataArray;
            _chunkBoundsArray = chunkBoundsArray;
        }
        
        public void Execute(int index)
        {
            var chunkCoord = _chunkCoordsArray[index];
            
            var voxelDataLocalViewArray = _voxelDataArray.GetSubArray(index * _chunkSizeInVoxels, _chunkSizeInVoxels); 

            var visibleFaces = 0;
            
            ChunkMeshCore.FirstPassGetVisibleFaces(
                in _chunkSizeInVoxels,
                in _voxelDataArray,
                in voxelDataLocalViewArray,
                in chunkCoord,
                in _chunkSize,
                in _chunkSizeY,
                ref visibleFaces,
                in _coordTableHashMap);
            
            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
            ChunkMeshCore.SecondPassBuildMeshData(
                in _chunkSizeInVoxels,
                in _voxelDataArray,
                in voxelDataLocalViewArray,
                in chunkCoord,
                in _chunkSize,
                in _chunkSizeY,
                in visibleFaces,
                out var vertices,
                out var triangles,
                out var normals,
                out var colors,
                ref boundsMin,
                ref boundsMax,
                in _coordTableHashMap);
            
            var chunkMeshData = _chunkMeshDataArray[index];
            
            ChunkMeshCore.SetMeshDataBuffers(
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