using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshDataTest : MonoBehaviour
{
    private const byte FaceCount = 6;
    private const byte FaceEdges = 4;
    private const byte VertexCount = FaceCount * FaceEdges;
    private const byte IndexCount = 36;
    
    private JobHandle generateVoxelDataJobHandle;
    private Mesh.MeshDataArray meshDataArray;
    
    private struct GenerateVoxelDataJob : IJob
    {
        public Mesh.MeshDataArray meshDataArray;
        
        public void Execute()
        {
            var meshData = meshDataArray[0];
            
            meshData.SetVertexBufferParams(
                VertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
        
            meshData.SetIndexBufferParams(IndexCount, IndexFormat.UInt16);
    
            var positions = meshData.GetVertexData<Vector3>();
            var normals = meshData.GetVertexData<Vector3>(1);
            var indices = meshData.GetIndexData<ushort>();
            
            var p0 = new Vector3(-0.5f, -0.5f,  0.5f); // Back bottom-left
            var p1 = new Vector3( 0.5f, -0.5f,  0.5f); // Back bottom-right
            var p2 = new Vector3( 0.5f, -0.5f, -0.5f); // Front bottom-right
            var p3 = new Vector3(-0.5f, -0.5f, -0.5f); // Front bottom-left
            var p4 = new Vector3(-0.5f,  0.5f,  0.5f); // Front top-left
            var p5 = new Vector3( 0.5f,  0.5f,  0.5f); // Front top-right
            var p6 = new Vector3( 0.5f,  0.5f, -0.5f); // Back top-right
            var p7 = new Vector3(-0.5f,  0.5f, -0.5f); // Back top-left
    
            byte v = 0;
            
            // Bottom face
            positions[v] = p0; positions[v + 1] = p1; positions[v + 2] = p2; positions[v + 3] = p3;
            normals[v] = Vector3.down; normals[v + 1] = Vector3.down; normals[v + 2] = Vector3.down; normals[v + 3] = Vector3.down;
            v += 4;
            
            // Top face
            positions[v] = p7; positions[v + 1] = p6; positions[v + 2] = p5; positions[v + 3] = p4;
            normals[v] = Vector3.up; normals[v + 1] = Vector3.up; normals[v + 2] = Vector3.up; normals[v + 3] = Vector3.up;
            v += 4;
            
            // Left face
            positions[v] = p7; positions[v + 1] = p4; positions[v + 2] = p0; positions[v + 3] = p3;
            normals[v] = Vector3.left; normals[v + 1] = Vector3.left; normals[v + 2] = Vector3.left; normals[v + 3] = Vector3.left;
            v += 4;
            
            // Right face
            positions[v] = p5; positions[v + 1] = p6; positions[v + 2] = p2; positions[v + 3] = p1;
            normals[v] = Vector3.right; normals[v + 1] = Vector3.right; normals[v + 2] = Vector3.right; normals[v + 3] = Vector3.right;
            v += 4;
            
            // Front face
            positions[v] = p4; positions[v + 1] = p5; positions[v + 2] = p1; positions[v + 3] = p0;
            normals[v] = Vector3.forward; normals[v + 1] = Vector3.forward; normals[v + 2] = Vector3.forward; normals[v + 3] = Vector3.forward;
            v += 4;
            
            // Back face
            positions[v] = p6; positions[v + 1] = p7; positions[v + 2] = p3; positions[v + 3] = p2;
            normals[v] = Vector3.back; normals[v + 1] = Vector3.back; normals[v + 2] = Vector3.back; normals[v + 3] = Vector3.back;
    
            byte i = 0;
            for (ushort j = 0; j < VertexCount; j += FaceEdges)
            {
                // Counter-clockwise direction
                indices[i++] = j;
                indices[i++] = (ushort)(j + 3);
                indices[i++] = (ushort)(j + 2);
                
                indices[i++] = j;
                indices[i++] = (ushort)(j + 2);
                indices[i++] = (ushort)(j + 1);
            }
            
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, IndexCount));
        }
    }
    
    private void Start()
    {
        meshDataArray = Mesh.AllocateWritableMeshData(1);
        
        var generateVoxelJob = new GenerateVoxelDataJob()
        {
            meshDataArray = meshDataArray,
        };

        generateVoxelDataJobHandle = generateVoxelJob.Schedule();
        
        generateVoxelDataJobHandle.Complete();

        if (!generateVoxelDataJobHandle.IsCompleted) return;
        
        var mesh = new Mesh { name = "Voxel Cube" };
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontValidateIndices);
        mesh.RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
        GetComponent<MeshFilter>().mesh = mesh;
    }
    
}
