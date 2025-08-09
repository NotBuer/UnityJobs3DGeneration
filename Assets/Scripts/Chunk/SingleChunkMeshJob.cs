// using Unity.Burst;
// using Unity.Collections;
// using Unity.Jobs;
// using UnityEngine;
// using Voxel;
//
// namespace Chunk
// {
//     [BurstCompile]
//     public unsafe struct SingleChunkMeshJob : IJob
//     {
//         private Mesh.MeshDataArray _chunkMeshDataArray;
//         [WriteOnly] private NativeReference<Bounds> _chunkBoundsRef;
//         [ReadOnly] private readonly Vector2Int _chunkCoord;
//         [ReadOnly] private readonly int _chunkVoxelCount;
//         [ReadOnly] private readonly byte _chunkSize;
//         [ReadOnly] private readonly byte _chunkSizeY;
//         [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
//         [ReadOnly] private readonly NativeParallelHashMap<Vector2Int, NativeArray<VoxelData>>.ReadOnly _allChunkVoxelData;
//     
//         public SingleChunkMeshJob(
//             Mesh.MeshDataArray chunkMeshDataArray, 
//             NativeReference<Bounds> chunkBoundsRef,
//             Vector2Int chunkCoord,
//             int chunkVoxelCount,
//             byte chunkSize,
//             byte chunkSizeY,
//             NativeParallelHashMap<Vector2Int, NativeArray<VoxelData>> allChunkVoxelData)
//         {
//             _chunkMeshDataArray = chunkMeshDataArray;
//             _chunkBoundsRef = chunkBoundsRef;
//             _chunkCoord = chunkCoord;
//             _chunkVoxelCount = chunkVoxelCount;
//             _chunkSize = chunkSize;
//             _chunkSizeY = chunkSizeY;
//
//             _voxelDataArray = allChunkVoxelData[chunkCoord];
//             _allChunkVoxelData = allChunkVoxelData.AsReadOnly();
//         }
//         
//         public void Execute()
//         {
//             var chunkMeshData = _chunkMeshDataArray[0];
//
//             var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
//             var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
//
//             var visibleFaces = 0;
//             
//             ChunkMeshCore.FirstPassGetVisibleFaces(
//                 in _chunkVoxelCount,
//                 in _voxelDataArray,
//                 in _chunkCoord,
//                 in _chunkSize,
//                 in _chunkSizeY, 
//                 ref visibleFaces,
//                 true,
//                 in _allChunkVoxelData);
//             
//             ChunkMeshCore.SecondPassBuildMeshData(
//                 in _chunkVoxelCount,
//                 in _voxelDataArray,
//                 in _chunkSize,
//                 in _chunkSizeY,
//                 in visibleFaces,
//                 out var vertices,
//                 out var triangles,
//                 out var normals,
//                 out var colors,
//                 in _chunkCoord,
//                 false,
//                 ref boundsMin,
//                 ref boundsMax,
//                 in _allChunkVoxelData);
//             
//             _chunkBoundsRef.Value = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
//             
//             ChunkMeshCore.SetMeshDataBuffers(
//                 ref chunkMeshData,
//                 ref vertices,
//                 ref triangles,
//                 ref normals,
//                 ref colors);
//             
//             vertices->Dispose();
//             triangles->Dispose();
//             normals->Dispose();
//             colors->Dispose();
//         }
//     }   
// }
