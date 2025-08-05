using LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace Chunk
{
    [BurstCompile]
    public unsafe struct SingleChunkMeshJob : IJob
    {
        private Mesh.MeshDataArray _chunkMeshDataArray;
        [WriteOnly] private NativeArray<Bounds> _chunkBoundsArray;
        [ReadOnly] private readonly int _chunkVoxelCount;
        [ReadOnly] private readonly byte _chunkSize;
        [ReadOnly] private readonly byte _chunkSizeY;
        [ReadOnly] private readonly byte _totalChunksPerAxis;
        [ReadOnly] private readonly NativeArray<ChunkData> _chunkDataArray;
        [ReadOnly] private readonly NativeArray<VoxelData> _voxelDataArray;
        [ReadOnly] private readonly ushort _index;
    
        public SingleChunkMeshJob(
            Mesh.MeshDataArray chunkMeshDataArray, 
            NativeArray<Bounds> chunkBoundsArray,
            int chunkVoxelCount,
            byte chunkSize,
            byte chunkSizeY,
            byte totalChunksPerAxis,
            NativeArray<ChunkData> chunkDataArray, 
            NativeArray<VoxelData> voxelDataArray,
            ushort index)
        {
            _chunkMeshDataArray = chunkMeshDataArray;
            _chunkBoundsArray = chunkBoundsArray;
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
            var voxelStartIndex = _chunkVoxelCount * _index;
            
            var currentChunkWorldX = _chunkDataArray[_index].x;
            var currentChunkWorldZ = _chunkDataArray[_index].z;

            var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var visibleFaces = 0;
            
            FirstPassGetVisibleFacesLocal(
                in voxelStartIndex,
                in currentChunkWorldX,
                in currentChunkWorldZ,
                ref visibleFaces,
                true);
            
            SecondPassGetVisibleFacesGlobal(
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
            
            SetMeshDataBuffers(
                ref _chunkMeshDataArray,
                ref vertices,
                ref triangles,
                ref normals,
                ref colors,
                in boundsMin,
                in boundsMax);
            
            vertices->Dispose();
            triangles->Dispose();
            normals->Dispose();
            colors->Dispose();
        }
        
        private void FirstPassGetVisibleFacesLocal(
            in int voxelStartIndex,
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            ref int visibleFaces, 
            in bool firstPass)
        {
            for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
            {
                if (_voxelDataArray[voxelStartIndex + voxelIndex].type == VoxelType.Air)
                    continue;
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
                            in currentChunkWorldX,
                            in currentChunkWorldZ,
                            in voxelIndex,
                            in normal,
                            in voxelStartIndex,
                            in firstPass)) continue;
                    
                    visibleFaces++;
                }
            }
        }
        
        private void SecondPassGetVisibleFacesGlobal(
            in int visibleFaces,
            in int voxelStartIndex,
            out UnsafeList<Vector3>* vertices,
            out UnsafeList<int>* triangles,
            out UnsafeList<Vector3>* normals,
            out UnsafeList<Color32>* colors,
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            in bool firstPass,
            ref Vector3 boundsMin,
            ref Vector3 boundsMax)
        {
            var bufferVertexCount = Mathf.FloorToInt(visibleFaces * 1.3125f * VoxelUtils.FaceEdges);
            var bufferTriangleCount = Mathf.FloorToInt(visibleFaces * 1.3125f * VoxelUtils.FaceCount);
            
            vertices = UnsafeList<Vector3>.Create(bufferVertexCount, Allocator.Temp);
            triangles = UnsafeList<int>.Create(bufferTriangleCount, Allocator.Temp);
            normals = UnsafeList<Vector3>.Create(bufferVertexCount, Allocator.Temp);
            colors = UnsafeList<Color32>.Create(bufferVertexCount, Allocator.Temp);

            var vertexIndex = 0;
            
            for (var voxelIndex = 0; voxelIndex < _chunkVoxelCount; voxelIndex++)
            {
                var voxelType = _voxelDataArray[voxelStartIndex + voxelIndex].type;
                
                if (voxelType == VoxelType.Air)
                    continue;
                
                var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);
                
                var voxelPosition = new Vector3(
                    x + currentChunkWorldX,
                    y,
                    z + currentChunkWorldZ);
        
                for (byte faceIndex = 0; faceIndex < VoxelUtils.FaceCount; faceIndex++)
                {
                    var normal = VoxelUtils.Normals[faceIndex];
                    
                    if (!IsFaceVisible(
                            in currentChunkWorldX,
                            in currentChunkWorldZ,
                            in voxelIndex,
                            in normal,
                            in voxelStartIndex,
                            in firstPass)) continue;
        
                    for (byte j = 0; j < VoxelUtils.FaceEdges; j++)
                    {
                        var vertex =
                            voxelPosition +
                            VoxelUtils.Vertices[VoxelUtils.FaceVertices[faceIndex * VoxelUtils.FaceEdges + j]];
                        
                        vertices->AddNoResize(vertex);
                        normals->AddNoResize(normal);
                        colors->AddNoResize(GetVoxelColor(in voxelType));
                        
                        boundsMin = Vector3.Min(boundsMin, vertex);
                        boundsMax = Vector3.Max(boundsMax, vertex);
                    }
                    
                    // Add 2 triangles for the face using an anti-clockwise direction.
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 3);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex);
                    triangles->AddNoResize(vertexIndex + 2);
                    triangles->AddNoResize(vertexIndex + 1);
                    
                    vertexIndex += VoxelUtils.FaceEdges;
                }
            }
        }
        
        private bool IsFaceVisible(
            in float currentChunkWorldX,
            in float currentChunkWorldZ,
            in int voxelIndex,
            in Vector3Int normal,
            in int voxelStartIndex,
            in bool firstPass)
        {
            var (x, y, z) = ChunkUtils.UnflattenIndexTo3DLocalCoords(voxelIndex, _chunkSize, _chunkSizeY);
        
            var neighborY = y + normal.y;
            if (neighborY < 0 || neighborY >= _chunkSizeY)
                return true;
            
            var neighborX = x + normal.x;
            var neighborZ = z + normal.z;
            
            // Neighbor voxel is within the current chunk's local XZ bounds
            if (neighborX >= 0 && neighborX < _chunkSize && neighborZ >= 0 && neighborZ < _chunkSize)
            {
                var neighborVoxelIndex = voxelStartIndex +
                    ChunkUtils.Flatten3DLocalCoordsToIndex(
                        0, neighborX, neighborY, neighborZ, _chunkSize, _chunkSizeY);
                
                return _voxelDataArray[neighborVoxelIndex].type == VoxelType.Air;
            }
            
            if (firstPass) return false;
            
            // Neighbor might be in an adjacent chunk.
            var neighborGlobalX = currentChunkWorldX + neighborX;
            var neighborGlobalZ = currentChunkWorldZ + neighborZ;
            
            // Calculate target chunk grid coordinates.
            var gridOffset = _totalChunksPerAxis / 2;
            var targetChunkGridX = Mathf.FloorToInt(neighborGlobalX / _chunkSize) + gridOffset;
            var targetChunkGridZ = Mathf.FloorToInt(neighborGlobalZ / _chunkSize) + gridOffset;
            
            // Ensure the target chunk is within world bounds.
            if (targetChunkGridX < 0 || targetChunkGridX >= _totalChunksPerAxis ||
                targetChunkGridZ < 0 || targetChunkGridZ >= _totalChunksPerAxis)
                return false;   
            
            // Calculate the target chunk's array index.
            var targetChunkIndex = targetChunkGridZ * _totalChunksPerAxis + targetChunkGridX;
            
            var targetVoxelLocalX = (int)(neighborGlobalX % _chunkSize + _chunkSize) % _chunkSize;
            var targetVoxelLocalZ = (int)(neighborGlobalZ % _chunkSize + _chunkSize) % _chunkSize;
            
            var neighborAbsoluteIndex = (targetChunkIndex * _chunkVoxelCount) + ChunkUtils.Flatten3DLocalCoordsToIndex(
                0, targetVoxelLocalX, neighborY, targetVoxelLocalZ, _chunkSize, _chunkSizeY);
            
            // Validate and check voxel visibility.
            return 
                neighborAbsoluteIndex >= 0 && 
                neighborAbsoluteIndex < _voxelDataArray.Length &&
                _voxelDataArray[neighborAbsoluteIndex].type == VoxelType.Air;
        }
        
        private static Color32 GetVoxelColor(
            in VoxelType voxelType)
        {
            return voxelType switch
            {
                VoxelType.Grass => VoxelUtils.GrassColor,
                VoxelType.Dirt => VoxelUtils.DirtColor,
                VoxelType.Stone => VoxelUtils.StoneColor,
                _ => new Color32(255, 0, 255, 255) // Default to magenta for unknown types
            };
        }

        private void SetMeshDataBuffers(
            ref Mesh.MeshDataArray chunkMeshDataArray,
            ref UnsafeList<Vector3>* vertices,
            ref UnsafeList<int>* triangles,
            ref UnsafeList<Vector3>* normals,
            ref UnsafeList<Color32>* colors,
            in Vector3 boundsMin,
            in Vector3 boundsMax)
        {
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>
                (3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            
            vertexAttributes[0] = new VertexAttributeDescriptor
                (VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
            vertexAttributes[1] = new VertexAttributeDescriptor
                (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
            vertexAttributes[2] = new VertexAttributeDescriptor
                (VertexAttribute.Color, VertexAttributeFormat.UNorm8, dimension: 4, stream: 2);
            
            var chunkMeshData = chunkMeshDataArray[0];
            
            chunkMeshData.SetVertexBufferParams(vertices->Length, vertexAttributes);
            vertexAttributes.Dispose();
            
            chunkMeshData.SetIndexBufferParams(triangles->Length, IndexFormat.UInt32);
            
            chunkMeshData.GetVertexData<Vector3>(0).CopyFrom(NativeArrayUnsafe.AsNativeArray(vertices));
            chunkMeshData.GetVertexData<Vector3>(1).CopyFrom(NativeArrayUnsafe.AsNativeArray(normals));
            chunkMeshData.GetVertexData<Color32>(2).CopyFrom(NativeArrayUnsafe.AsNativeArray(colors));
            chunkMeshData.GetIndexData<int>().CopyFrom(NativeArrayUnsafe.AsNativeArray(triangles));
            
            chunkMeshData.subMeshCount = 1;
            chunkMeshData.SetSubMesh(0, 
                new SubMeshDescriptor(0, triangles->Length), 
                MeshUpdateFlags.DontValidateIndices | 
                MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | 
                MeshUpdateFlags.DontRecalculateBounds);
            
            _chunkBoundsArray[0] = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
        }
    }   
}
