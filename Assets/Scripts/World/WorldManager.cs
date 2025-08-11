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
        
        [Header("Performance")]
        [Range(1, 128)] [SerializeField] private byte innerLoopBatchCount = 16;
        
        [Header("World Settings")]
        [Range(1, 32)] [SerializeField] private byte chunkSize = 16;
        [Range(1, 255)] [SerializeField] private byte chunkSizeY = 255;
        [Range(2, 16)] [SerializeField] private byte renderDistance = 2;
    	[SerializeField] private float frequency = 0.01f;
        [SerializeField] private float amplitude = 32f;
        [SerializeField] private Material worldDefaultMaterial;
        
        private bool _isWorldInitialized;

        private byte _totalChunksPerAxis;
        private ushort _totalWorldGridDimension;
        private int _chunkSizeInVoxels;

        private Vector2Int _lastPlayerChunkPosition;
        
        private Dictionary<Vector2Int, ChunkState> _chunkStateDictionary;
        private Dictionary<Vector2Int, GameObject> _chunkObjectDictionary;
        
        private Dictionary<Vector2Int, NativeArray<VoxelData>> _worldVoxelData;

        private JobHandle _meshesJobsHandle;
        
        private void OnValidate()
        {
            renderDistance = renderDistance % 2 != 0 ? renderDistance++ : renderDistance;
        }

        private void Awake() 
        {
            _totalChunksPerAxis = (byte)(renderDistance * RenderDistanceAxisCount); 
            _totalWorldGridDimension = (ushort)(_totalChunksPerAxis + 1 * _totalChunksPerAxis + 1);
            _chunkSizeInVoxels = ChunkUtils.GetChunkTotalSize(chunkSize, chunkSizeY);
            
            _chunkStateDictionary = new Dictionary<Vector2Int, ChunkState>(_totalWorldGridDimension);
            _chunkObjectDictionary = new Dictionary<Vector2Int, GameObject>(_totalWorldGridDimension);
            _worldVoxelData = new Dictionary<Vector2Int, NativeArray<VoxelData>>(_totalWorldGridDimension);
            
            _lastPlayerChunkPosition = Vector2Int.zero;
            
            _isWorldInitialized = true;
        }

        private void Start()
        {
            WorldChunksUpdate();
        }

        private void Update()
        {
            if (!_isWorldInitialized) return;
            
            var currentPlayerChunkPosition = WorldUtils.GetChunkCoordinateFromWorldPosition
                (DebugCamera.Instance.transform.position, in chunkSize);

            if (_lastPlayerChunkPosition != currentPlayerChunkPosition)
            {
                _lastPlayerChunkPosition = currentPlayerChunkPosition;
                WorldChunksUpdate();   
            }
        }
        
        private void WorldChunksUpdate()
        {
            var chunksToLoad = new HashSet<Vector2Int>();
            var chunksToUnload = new HashSet<Vector2Int>();
            var requiredChunks = new HashSet<Vector2Int>();
            
            var renderDistanceSqr = renderDistance * renderDistance;
            for (var x = -renderDistance; x <= renderDistance; x++)
            {
                for (var z = -renderDistance; z <= renderDistance; z++)
                {
                    var gridPosition = new Vector2Int(x, z);
                    if (gridPosition.sqrMagnitude <= renderDistanceSqr)
                    {
                        requiredChunks.Add(
                            _lastPlayerChunkPosition + 
                            WorldUtils.GetGridPositionAsChunkPosition(in gridPosition, in chunkSize));
                    }
                }
            }

            foreach (var chunkCoord in _chunkStateDictionary.Keys)
                if (!requiredChunks.Contains(chunkCoord))
                    chunksToUnload.Add(chunkCoord);

            foreach (var chunkCoord in chunksToUnload)
                UnloadChunk(in chunkCoord);

            foreach (var chunkCoord in requiredChunks)
                if (!_chunkStateDictionary.ContainsKey(chunkCoord))
                    chunksToLoad.Add(chunkCoord);

            if (chunksToLoad.Count > 0) LoadChunkTransaction(in chunksToLoad);
        }

        private void LoadChunkTransaction(in HashSet<Vector2Int> chunksToLoad)
        {
            var transactionSize = chunksToLoad.Count;
            
            var chunkCoordsArray = new NativeArray<Vector2Int>(chunksToLoad.ToArray(), Allocator.TempJob);
            var voxelDataArray = new NativeArray<VoxelData>(transactionSize * _chunkSizeInVoxels, Allocator.TempJob);
            var coordTableHashMap = new NativeParallelHashMap<Vector2Int, int>(transactionSize,Allocator.TempJob);
            
            var dataJob = new ChunkDataJob(
                _chunkSizeInVoxels,
                chunkSize,
                chunkSizeY,
                WorldSeed.Instance.CurrentSeedHashCode,
                frequency,
                amplitude,
                chunkCoordsArray,
                voxelDataArray,
                coordTableHashMap.AsParallelWriter());
            
            var dataJobHandle = dataJob.Schedule(transactionSize, innerLoopBatchCount);
            
            var meshDataArray = Mesh.AllocateWritableMeshData(transactionSize);
            var boundsArray = new NativeArray<Bounds>(transactionSize, Allocator.TempJob);
            
            var meshJob = new ChunkMeshJob(
                _chunkSizeInVoxels,
                chunkSize,
                chunkSizeY,
                chunkCoordsArray,
                voxelDataArray,
                coordTableHashMap.AsReadOnly(),
                meshDataArray,
                boundsArray);
            
            var meshJobHandle = meshJob.Schedule(transactionSize, innerLoopBatchCount, dataJobHandle);
            
            StartCoroutine(
                WaitForMeshAndRender(
                        chunksToLoad,
                        meshJobHandle,
                        meshDataArray, 
                        boundsArray, 
                        chunkCoordsArray, 
                        voxelDataArray, 
                        coordTableHashMap));
        }
        
        private IEnumerator WaitForMeshAndRender(
            HashSet<Vector2Int> chunksToLoad,
            JobHandle jobHandle,
            Mesh.MeshDataArray meshDataArray,
            NativeArray<Bounds> boundsArray,
            NativeArray<Vector2Int> chunkCoordsArray,
            NativeArray<VoxelData> voxelDataArray,
            NativeParallelHashMap<Vector2Int, int> coordTableHashMap)
        {
            foreach (var chunkCoord in chunksToLoad)
                _chunkStateDictionary[chunkCoord] = ChunkState.Loading;
            
            yield return new WaitUntil(() => jobHandle.IsCompleted);
            jobHandle.Complete();
            
            var meshes = new Mesh[chunksToLoad.Count];
            for (var i = 0; i < meshes.Length; i++)
            {
                meshes[i] = new Mesh
                {
                    bounds = boundsArray[i]
                };
            }
            
            boundsArray.Dispose();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

            for (var i = 0; i < meshes.Length; i++)
            {
                var chunkCoord = chunkCoordsArray[i];
                
                // If the chunk was marked for unloading while meantime, discard the result.
                if (!_chunkStateDictionary.ContainsKey(chunkCoord) ||
                    _chunkStateDictionary[chunkCoord] == ChunkState.ToUnload)
                {
                    Destroy(meshes[i]);
                    continue;
                }
                
                // No need to render empty chunks.
                if (meshes[i].vertexCount == 0)
                    continue;
                
                // Persist the generated voxel data from the temporary array to main-thread dictionary.
                _worldVoxelData[chunkCoord] = new NativeArray<VoxelData>(
                    voxelDataArray.GetSubArray(i * _chunkSizeInVoxels, _chunkSizeInVoxels),
                    Allocator.Persistent);
                
                var chunkObject = new GameObject 
                    { name = $"Chunk - X:{chunkCoord.x}, Z:{chunkCoord.y}" };
                
                var mf = chunkObject.AddComponent<MeshFilter>();
                mf.mesh = meshes[i];
                var mr = chunkObject.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.material = worldDefaultMaterial;
                
                _chunkObjectDictionary[chunkCoord] = chunkObject;
                _chunkStateDictionary[chunkCoord] = ChunkState.Active;
            }
            
            chunkCoordsArray.Dispose();
            voxelDataArray.Dispose();
            coordTableHashMap.Dispose();
        }
        
        private void UnloadChunk(in Vector2Int chunkCoord)
        {
            if (_chunkStateDictionary.TryGetValue(chunkCoord, out var state) && state == ChunkState.Loading)
            {
                _chunkStateDictionary[chunkCoord] = ChunkState.ToUnload;
                return;
            }
            
            if (_chunkObjectDictionary.TryGetValue(chunkCoord, out var chunkObject))
            {
                Destroy(chunkObject);
                _chunkObjectDictionary.Remove(chunkCoord);
            }

            if (_worldVoxelData.TryGetValue(chunkCoord, out var voxelData))
            {
                voxelData.Dispose();
                _worldVoxelData.Remove(chunkCoord);
            }
            
            _chunkStateDictionary.Remove(chunkCoord);
        }
        
        private void OnApplicationQuit()
        {
            // Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - Unsubscribed events!");
            
            StopAllCoroutines();
            
            Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - All coroutines stopped!");

            foreach (var voxelData in _worldVoxelData.Values)
                if (voxelData.IsCreated) voxelData.Dispose();
            
            Debug.Log($"{nameof(WorldManager)}.{nameof(OnApplicationQuit)} - All native containers disposed!");
        }
    }
}

