using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public class ChunkMeshDataTest : MonoBehaviour
{
    [SerializeField] private byte chunkSizeX = 16;
    [SerializeField] private byte chunkSizeZ = 16;
    [SerializeField] private byte chunkSizeY = 255;

    private JobHandle generateChunkJobHandle;
    private Mesh.MeshDataArray chunkMeshDataArray;
    
    private void Start()
    {
        chunkMeshDataArray = Mesh.AllocateWritableMeshData(chunkSizeX *  chunkSizeZ * chunkSizeY);

        var generateChunkMeshJob = new GenerateChunkMeshJob()
        {
            chunkMeshDataArray = chunkMeshDataArray,
            chunkSizeX = chunkSizeX,
            chunkSizeZ = chunkSizeZ,
            chunkSizeY = chunkSizeY,
        };

        generateChunkJobHandle = generateChunkMeshJob.Schedule();

        generateChunkJobHandle.Complete();
        
        var meshes = new List<Mesh>(chunkSizeX * chunkSizeZ * chunkSizeY);
        var positions = new List<Vector3>(chunkSizeX * chunkSizeZ * chunkSizeY);
        for (byte x = 0; x < chunkSizeX; x++)
        {
            for (byte z = 0; z < chunkSizeZ; z++)
            {
                for (byte y = 0; y < chunkSizeY; y++)
                {
                    meshes.Add(new Mesh { name = $"Mesh Coord: {x} {y} {z}" });
                    positions.Add(new Vector3(x, y, z));
                }
            }
        }
        
        Mesh.ApplyAndDisposeWritableMeshData(chunkMeshDataArray, meshes);
        meshes.ForEach(x =>
        {
            x.RecalculateBounds();
            x.RecalculateNormals();
        });

        var posIterator = 0;
        foreach (var mesh in meshes)
        {
            var gameObject = new GameObject($"{mesh.name}");
            gameObject.transform.SetParent(this.gameObject.transform);
            gameObject.transform.position = positions[posIterator];
            
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            // meshRenderer.material = new Material(Shader.Find("Standard"));

            posIterator++;
        }
    }

    private struct GenerateChunkMeshJob : IJob
    {
        public Mesh.MeshDataArray chunkMeshDataArray;
        public byte chunkSizeX;
        public byte chunkSizeZ;
        public byte chunkSizeY;
        
        public void Execute()
        {
            var iterator = 0;
            for (byte x = 0; x < chunkSizeX; x++)
            {
                for (byte z = 0; z < chunkSizeZ; z++)
                {
                    for (byte y = 0; y < chunkSizeY; y++)
                    {
                        var meshData = chunkMeshDataArray[iterator];
                        VoxelMeshUtils.SetVoxelMeshData(new Vector3(x, y, z), ref meshData);
                        iterator++;
                    }
                }
            }
        }
    }
}
