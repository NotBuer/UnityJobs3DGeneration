using System.Collections.Generic;
using System.Linq;
using Chunk;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;

namespace World
{
    public class WorldManager : MonoBehaviour
    {
        private const byte RenderDistanceAxisCount = 2;
        
        [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
        [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
        [Range(1, 6)] [SerializeField] private byte chunksToGenerate = 1;
    	[SerializeField] private float frequency = 0.01f;
        [SerializeField] private float amplitude = 32f;
    
        private NativeArray<ChunkData> chunkDataArray;
        private NativeArray<VoxelData> voxelDataArray;
        private NativeArray<Bounds> chunkBoundsArray;
        
        private JobHandle chunkDataJobHandle;
        private JobHandle chunkMeshJobHandle;
        
        private void Awake()
        {
            var renderDistancePerAxis =
                chunksToGenerate * RenderDistanceAxisCount * chunksToGenerate * RenderDistanceAxisCount;
            
            chunkDataArray = new NativeArray<ChunkData>(
                renderDistancePerAxis, Allocator.Persistent);
            
            voxelDataArray = new NativeArray<VoxelData>(
                chunkDataArray.Length * (chunkSize * chunkSize * chunkSizeY), Allocator.Persistent);
            
            chunkBoundsArray = new NativeArray<Bounds>(
                chunkDataArray.Length, Allocator.Persistent);
        }
    
        private void Start()
        {
            var chunkDataJob = new ChunkDataJob(
                chunksToGenerate, chunkSize, chunkSizeY, WorldSeed.Instance.CurrentSeedHashCode,
                frequency, amplitude, chunkDataArray, voxelDataArray);
            
            chunkDataJobHandle = chunkDataJob.Schedule(chunkDataArray.Length, 1);
            chunkDataJobHandle.Complete();
            
            var chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkDataArray.Length);
    
            var chunkMeshJob = new ChunkMeshJob(
                chunkMeshDataArray,
                chunkBoundsArray,
                chunkSize * chunkSize * chunkSizeY, 
                chunkSize, chunkSizeY, chunksToGenerate, chunkDataArray, voxelDataArray);
            
            chunkMeshJobHandle = chunkMeshJob.Schedule(
                chunkMeshDataArray.Length, 1, chunkDataJobHandle);
            chunkMeshJobHandle.Complete();
            
            var chunkMeshes = new List<Mesh>(chunkMeshDataArray.Length);
            chunkMeshes.AddRange(chunkDataArray.Select(_ => new Mesh()));
            Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, chunkMeshes);
            
            var materialDefault = new Material(Shader.Find("Custom/UnlitVertexColor"));
    
            for (byte i = 0; i < chunkMeshes.Count; i++)
            {
                chunkMeshes[i].bounds = chunkBoundsArray[i];
                
                var chunkGameObject = new GameObject { name = $"Chunk - X:{chunkDataArray[i].x}, Z:{chunkDataArray[i].z}" };
                
                var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = chunkMeshes[i];
                
                var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.material = materialDefault;
            }
        }
    
        private void OnApplicationQuit()
        {
            chunkDataArray.Dispose();
            voxelDataArray.Dispose();
            chunkBoundsArray.Dispose();
            Debug.Log("All native arrays disposed");
        }
    }
}

