using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Camera;
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
        
        private Action _onWorldGenStart;
        private Action _onWorldGenEnd;
        
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
        
        private bool _generatingWorld = false;

        private byte _totalChunksPerAxis;
        private ushort _totalWorldGridDimension;
    
        private NativeArray<ChunkData> _chunkDataArray;
        private NativeArray<VoxelData> _voxelDataArray;
        private NativeArray<Bounds> _chunkBoundsArray;

        private JobHandle _meshesJobsHandle;
        
        private void OnValidate()
        {
            renderDistance = renderDistance % 2 != 0 ? renderDistance++ : renderDistance;
            parallelForInnerLoopBatchCount = parallelForInnerLoopBatchCount % 2 != 0 ? 
                parallelForInnerLoopBatchCount++ : parallelForInnerLoopBatchCount;
        }

        private void OnDrawGizmos()
        {
            if (_generatingWorld || !DebugCamera.Instance) return;

            foreach (var chunk in _chunkDataArray)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(
                    new Vector3(chunk.x + (float)chunkSize / 2, 0f, chunk.z + (float)chunkSize / 2),
                    new Vector3(chunkSize, 0f, chunkSize));
            }
        }

        private void Awake() 
        {
            _totalChunksPerAxis = (byte)(renderDistance * RenderDistanceAxisCount); 
            Debug.Log($"Number of total chunks per axis: {_totalChunksPerAxis}");
            _totalWorldGridDimension = (ushort)(_totalChunksPerAxis * _totalChunksPerAxis);
            Debug.Log($"Total world grid dimension size (in chunks): {_totalWorldGridDimension}");
            
            _onWorldGenStart += OnWorldGenStart;
            _onWorldGenEnd += OnWorldGenEnd;
            
            PreAllocateBuffers();
        }

        private void Start()
        {
            _onWorldGenStart.Invoke();
            
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

        private void Update()
        {
            if (!_meshesJobsHandle.IsCompleted) return;
            
            _meshesJobsHandle.Complete();
            _generatingWorld = false;
        }
        
        private void PreAllocateBuffers()
        {
            _chunkDataArray = new NativeArray<ChunkData>(
                _totalWorldGridDimension, Allocator.Persistent);
            
            _voxelDataArray = new NativeArray<VoxelData>(
                _chunkDataArray.Length * ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), Allocator.Persistent);
            
            _chunkBoundsArray = new NativeArray<Bounds>(
                _chunkDataArray.Length, Allocator.Persistent);
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
                _chunkDataArray, 
                _voxelDataArray);

            var dataJobHandle = dataJob.Schedule(_totalWorldGridDimension, parallelForInnerLoopBatchCount);

            for (ushort i = 0; i < _totalWorldGridDimension; i++)
            {
                var meshDataArray = Mesh.AllocateWritableMeshData(1);
                var boundsRef = new NativeReference<Bounds>(Allocator.TempJob);
                
                var meshJob = new SingleChunkMeshJob(
                    meshDataArray,
                    boundsRef,
                    ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), 
                    chunkSize, 
                    chunkSizeY, 
                    _totalChunksPerAxis,
                    _chunkDataArray, 
                    _voxelDataArray,
                    i);

                var meshJobHandle = meshJob.Schedule(dataJobHandle);
                _meshesJobsHandle = JobHandle.CombineDependencies(_meshesJobsHandle, meshJobHandle);
                
                StartCoroutine(
                    WaitForMeshAndRender_HybridJobs(meshJobHandle, meshDataArray, boundsRef, i));
            }
        }

        private IEnumerator WaitForMeshAndRender_HybridJobs(
            JobHandle jobHandle, 
            Mesh.MeshDataArray meshDataArray,
            NativeReference<Bounds> boundsRef,
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
            
           mesh.bounds = boundsRef.Value;
           boundsRef.Dispose();
           
           var chunkGameObject = new GameObject 
               { name = $"Chunk - X:{_chunkDataArray[index].x}, Z:{_chunkDataArray[index].z}" };
           
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
                _chunkDataArray, 
                _voxelDataArray);
            
            var chunkDataJobHandle = chunkDataJob.Schedule(_chunkDataArray.Length, parallelForInnerLoopBatchCount);
            
            var chunkMeshDataArray = Mesh.AllocateWritableMeshData(_chunkDataArray.Length);
    
            var chunkMeshJob = new ChunkMeshJob(
                chunkMeshDataArray,
                _chunkBoundsArray,
                ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY), 
                chunkSize, 
                chunkSizeY, 
                _totalChunksPerAxis,
                _chunkDataArray, 
                _voxelDataArray);
            
            var chunkMeshJobHandle = chunkMeshJob.Schedule(
                chunkMeshDataArray.Length, parallelForInnerLoopBatchCount, chunkDataJobHandle);
            
            _meshesJobsHandle = JobHandle.CombineDependencies(_meshesJobsHandle, chunkMeshJobHandle);
            
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
            chunkMeshes.AddRange(_chunkDataArray.Select(_ => new Mesh()));
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, chunkMeshes, 
                MeshUpdateFlags.DontValidateIndices | 
                MeshUpdateFlags.DontResetBoneBounds | 
                MeshUpdateFlags.DontNotifyMeshUsers | 
                MeshUpdateFlags.DontRecalculateBounds);
    
            for (byte i = 0; i < chunkMeshes.Count; i++)
            {
                chunkMeshes[i].bounds = _chunkBoundsArray[i];
                
                var chunkGameObject = new GameObject 
                    { name = $"Chunk - X:{_chunkDataArray[i].x}, Z:{_chunkDataArray[i].z}" };
                
                var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = chunkMeshes[i];
                
                var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.material = worldDefaultMaterial;

                yield return null;
            }
        }
        #endregion
        
        private void OnWorldGenStart()
        {
            _generatingWorld = true;
        }
        
        private void OnWorldGenEnd()
        {
            _generatingWorld = false;
        }
    
        private void OnApplicationQuit()
        {
            _onWorldGenStart -= OnWorldGenStart;
            _onWorldGenEnd -= _onWorldGenEnd;
            Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - Unsubscribed events!");
            
            StopAllCoroutines();
            Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - All coroutines stopped!");
            
            _chunkDataArray.Dispose();
            _voxelDataArray.Dispose();
            _chunkBoundsArray.Dispose();
            Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - All native containers disposed!");
        }
    }
}

