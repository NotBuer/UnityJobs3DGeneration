using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxel;
using Chunk;

namespace World
{
    public class WorldManager : MonoBehaviour
    {
        private const byte RenderDistanceAxisCount = 2;
        
        [Header("Performance")] 
        [SerializeField] private bool buildChunkUsingParallelBatchJobs;
        
        [Header("World Settings")]
        [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
        [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
        [Range(2, 16)] [SerializeField] private byte renderDistance = 2;
    	[SerializeField] private float frequency = 0.01f;
        [SerializeField] private float amplitude = 32f;
        [SerializeField] private Material worldDefaultMaterial;

        private byte totalChunksPerAxis;
        private byte totalWorldGridDimension;
    
        private NativeArray<ChunkData> chunkDataArray;
        private NativeArray<VoxelData> voxelDataArray;
        private NativeArray<Bounds> chunkBoundsArray;
        
        private JobHandle chunkDataJobHandle;
        private JobHandle chunkMeshJobHandle;

        private void OnValidate()
        {
            renderDistance = (byte)(renderDistance % 2 == 0 ? renderDistance : renderDistance + 1);
        }

        private void Awake() 
        {
            totalChunksPerAxis = (byte)(renderDistance * RenderDistanceAxisCount); 
            Debug.Log($"Number of total chunks per axis: {totalChunksPerAxis}");
            totalWorldGridDimension = (byte)(totalChunksPerAxis * totalChunksPerAxis);
            Debug.Log($"Total world grid dimension size (in chunks): {totalWorldGridDimension}");
            
            PreAllocateBuffers();
        }
        
        private void Start()
        {
            if (buildChunkUsingParallelBatchJobs)
            {
                SetupJobsAndScheduling_ParallelBatchJobs();
                return;
            }
            
            SetupJobsAndScheduling_SingleTaskJobs();
        }
        
        private void PreAllocateBuffers()
        {
            chunkDataArray = new NativeArray<ChunkData>(
                totalWorldGridDimension, Allocator.Persistent);
            
            voxelDataArray = new NativeArray<VoxelData>(
                chunkDataArray.Length * ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), Allocator.Persistent);
            
            chunkBoundsArray = new NativeArray<Bounds>(
                chunkDataArray.Length, Allocator.Persistent);
        }

        private void SetupJobsAndScheduling_ParallelBatchJobs()
        {
            var chunkDataJob = new ChunkDataJob(
                totalChunksPerAxis, 
                chunkSize, 
                chunkSizeY, 
                WorldSeed.Instance.CurrentSeedHashCode,
                frequency, 
                amplitude, 
                chunkDataArray, 
                voxelDataArray);
            
            chunkDataJobHandle = chunkDataJob.Schedule(chunkDataArray.Length, 1);
            
            var chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkDataArray.Length);
    
            var chunkMeshJob = new ChunkMeshJob(
                chunkMeshDataArray,
                chunkBoundsArray,
                ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), 
                chunkSize, 
                chunkSizeY, 
                totalChunksPerAxis,
                chunkDataArray, 
                voxelDataArray);
            
            chunkMeshJobHandle = chunkMeshJob.Schedule(chunkMeshDataArray.Length, 1, chunkDataJobHandle);
            
            StartCoroutine(WaitForMeshAndRender(
                JobHandle.CombineDependencies(chunkDataJobHandle, chunkMeshJobHandle), chunkMeshDataArray));
        }

        private void SetupJobsAndScheduling_SingleTaskJobs()
        {
            StartCoroutine(YieldedScheduleSingleTaskJobs());
        }

        private IEnumerator YieldedScheduleSingleTaskJobs()
        {
            
            yield return null;
        }

        private IEnumerator WaitForMeshAndRender(JobHandle jobHandle, Mesh.MeshDataArray meshDataArray)
        {
            yield return new WaitUntil(() => jobHandle.IsCompleted);
            jobHandle.Complete();
            
            var chunkMeshes = new List<Mesh>(meshDataArray.Length);
            chunkMeshes.AddRange(chunkDataArray.Select(_ => new Mesh()));
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, chunkMeshes, 
                MeshUpdateFlags.DontValidateIndices | 
                MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | 
                MeshUpdateFlags.DontRecalculateBounds);
    
            for (byte i = 0; i < chunkMeshes.Count; i++)
            {
                chunkMeshes[i].bounds = chunkBoundsArray[i];
                
                var chunkGameObject = new GameObject { name = $"Chunk - X:{chunkDataArray[i].x}, Z:{chunkDataArray[i].z}" };
                
                var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = chunkMeshes[i];
                
                var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.material = worldDefaultMaterial;

                yield return null;
            }
        }
    
        private void OnApplicationQuit()
        {
            StopAllCoroutines();
            Debug.Log("All coroutines stopped!");
            
            chunkDataArray.Dispose();
            voxelDataArray.Dispose();
            chunkBoundsArray.Dispose();
            Debug.Log("All native arrays disposed!");
        }
    }
}

