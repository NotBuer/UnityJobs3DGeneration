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
        [SerializeField] private bool useHybridJobs;
        [Range(2, 256)] [SerializeField] private byte parallelForInnerLoopBatchCount = 32;
        
        [Header("World Settings")]
        [Range(1, 16)] [SerializeField] private byte chunkSize = 16;
        [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
        [Range(2, 16)] [SerializeField] private byte renderDistance = 2;
    	[SerializeField] private float frequency = 0.01f;
        [SerializeField] private float amplitude = 32f;
        [SerializeField] private Material worldDefaultMaterial;

        private byte _totalChunksPerAxis;
        private ushort _totalWorldGridDimension;
    
        private NativeArray<ChunkData> chunkDataArray;
        private NativeArray<VoxelData> voxelDataArray;
        private NativeArray<Bounds> chunkBoundsArray;
        
        private JobHandle chunkDataJobHandle;
        private JobHandle chunkMeshJobHandle;
        
        private void OnValidate()
        {
            renderDistance = renderDistance % 2 != 0 ? renderDistance++ : renderDistance;
            parallelForInnerLoopBatchCount = parallelForInnerLoopBatchCount % 2 != 0 ? 
                parallelForInnerLoopBatchCount++ : parallelForInnerLoopBatchCount;
        }

        private void Awake() 
        {
            _totalChunksPerAxis = (byte)(renderDistance * RenderDistanceAxisCount); 
            Debug.Log($"Number of total chunks per axis: {_totalChunksPerAxis}");
            _totalWorldGridDimension = (ushort)(_totalChunksPerAxis * _totalChunksPerAxis);
            Debug.Log($"Total world grid dimension size (in chunks): {_totalWorldGridDimension}");
            
            PreAllocateBuffers();
        }
        
        private void Start()
        {
            if (useHybridJobs)
            {
                Debug.Log("World generation pipeline -> Hybrid ('IJobParallelFor + IJob') jobs.");
                SetupJobsAndScheduling_HybridJobs();
            }
            else
            {
                Debug.Log("World generation pipeline -> Batched ('IJobParallelFor') jobs.");
                SetupJobsAndScheduling_ParallelBatchJobs();
            }
        }
        
        private void PreAllocateBuffers()
        {
            chunkDataArray = new NativeArray<ChunkData>(
                _totalWorldGridDimension, Allocator.Persistent);
            
            voxelDataArray = new NativeArray<VoxelData>(
                chunkDataArray.Length * ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), Allocator.Persistent);
            
            chunkBoundsArray = new NativeArray<Bounds>(
                chunkDataArray.Length, Allocator.Persistent);
        }

        #region  HYBRID_JOBS_PIPELINE
        private void SetupJobsAndScheduling_HybridJobs()
        {
            var dataJob = new ChunkDataJob(
                _totalChunksPerAxis, 
                chunkSize, 
                chunkSizeY,
                WorldSeed.Instance.CurrentSeedHashCode,
                frequency, 
                amplitude, 
                chunkDataArray, 
                voxelDataArray);

            var dataJobHandle = dataJob.Schedule(_totalWorldGridDimension, parallelForInnerLoopBatchCount);

            for (ushort i = 0; i < _totalWorldGridDimension; i++)
            {
                var meshDataArray = Mesh.AllocateWritableMeshData(1);
                var boundsArray = new NativeArray<Bounds>(1, Allocator.TempJob);
                
                var meshJob = new SingleChunkMeshJob(
                    meshDataArray,
                    boundsArray,
                    ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), 
                    chunkSize, 
                    chunkSizeY, 
                    _totalChunksPerAxis,
                    chunkDataArray, 
                    voxelDataArray,
                    i);

                var meshJobHandle = meshJob.Schedule(dataJobHandle);
                
                StartCoroutine(
                    WaitForMeshAndRender_HybridJobs(meshJobHandle, meshDataArray, boundsArray, i));
            }
        }

        private IEnumerator WaitForMeshAndRender_HybridJobs(
            JobHandle jobHandle, 
            Mesh.MeshDataArray meshDataArray,
            NativeArray<Bounds> boundsArray,
            ushort index)
        {
            yield return new WaitUntil(() => jobHandle.IsCompleted);
            jobHandle.Complete();
        
            var mesh = new Mesh();
            
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, 
                MeshUpdateFlags.DontValidateIndices | 
                MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | 
                MeshUpdateFlags.DontRecalculateBounds);
            
           mesh.bounds = boundsArray[0];
           boundsArray.Dispose();
           
           var chunkGameObject = new GameObject 
               { name = $"Chunk - X:{chunkDataArray[index].x}, Z:{chunkDataArray[index].z}" };
           
           var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
           meshFilter.mesh = mesh;
           
           var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
           meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
           meshRenderer.material = worldDefaultMaterial;
        }
        #endregion

        #region PARALLEL_BATCH_JOBS_PIPELINE
        private void SetupJobsAndScheduling_ParallelBatchJobs()
        {
            var chunkDataJob = new ChunkDataJob(
                _totalChunksPerAxis, 
                chunkSize, 
                chunkSizeY, 
                WorldSeed.Instance.CurrentSeedHashCode,
                frequency, 
                amplitude, 
                chunkDataArray, 
                voxelDataArray);
            
            chunkDataJobHandle = chunkDataJob.Schedule(chunkDataArray.Length, parallelForInnerLoopBatchCount);
            
            var chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkDataArray.Length);
    
            var chunkMeshJob = new ChunkMeshJob(
                chunkMeshDataArray,
                chunkBoundsArray,
                ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), 
                chunkSize, 
                chunkSizeY, 
                _totalChunksPerAxis,
                chunkDataArray, 
                voxelDataArray);
            
            chunkMeshJobHandle = chunkMeshJob.Schedule(
                chunkMeshDataArray.Length, parallelForInnerLoopBatchCount, chunkDataJobHandle);
            
            StartCoroutine(WaitForMeshAndRender_ParallelBatchJobs(
                JobHandle.CombineDependencies(chunkDataJobHandle, chunkMeshJobHandle), chunkMeshDataArray));
        }
        
        private IEnumerator WaitForMeshAndRender_ParallelBatchJobs(
            JobHandle jobHandle, 
            Mesh.MeshDataArray meshDataArray)
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
                
                var chunkGameObject = new GameObject 
                    { name = $"Chunk - X:{chunkDataArray[i].x}, Z:{chunkDataArray[i].z}" };
                
                var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = chunkMeshes[i];
                
                var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.material = worldDefaultMaterial;

                yield return null;
            }
        }
        #endregion
        
    
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

