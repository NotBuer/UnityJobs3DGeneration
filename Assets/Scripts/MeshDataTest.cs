using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshDataTest : MonoBehaviour
{
    private const byte FaceCount = 6;
    private const byte FaceEdges = 4;
    private const byte VertexCount = FaceCount * FaceEdges;
    private const byte IndexCount = 36;
    
    private void Start()
    {
        CreateCube();
        // CreateTetrahedron();
    }

    private void CreateCube()
    {
       	var dataArray = Mesh.AllocateWritableMeshData(1);
        var data = dataArray[0];
        
        data.SetVertexBufferParams(
            VertexCount,
            new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
        
        data.SetIndexBufferParams(IndexCount, IndexFormat.UInt16);

        var positions = data.GetVertexData<Vector3>();
        var normals = data.GetVertexData<Vector3>(1);
        var indices = data.GetIndexData<ushort>();
        
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
            indices[i++] = j; // 0
            indices[i++] = (ushort)(j + 3); // 1 + 0
            indices[i++] = (ushort)(j + 2); // 2 + 0
            
            indices[i++] = j; // 0
            indices[i++] = (ushort)(j + 2); // 4 + 0
            indices[i++] = (ushort)(j + 1); // 5 + 0
        }

        data.subMeshCount = 1;
        data.SetSubMesh(0, new SubMeshDescriptor(0, IndexCount));
        
        var mesh = new Mesh { name = "Voxel Cube" };
        Mesh.ApplyAndDisposeWritableMeshData(dataArray, mesh, MeshUpdateFlags.DontValidateIndices);
        mesh.RecalculateBounds(MeshUpdateFlags.DontValidateIndices);
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void CreateTetrahedron()
    {
        var dataArray = Mesh.AllocateWritableMeshData(1);
        var data = dataArray[0];
        
        // Tetrahedron vertices with positions and normals.
        // 4 faces with 3 unique vertices in each -- the faces
        // don't share the vertices since normals have to be
        // different for each face.
        data.SetVertexBufferParams(12,
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));

        // Four tetrahedron vertex positions:
        var sqrt075 = Mathf.Sqrt(0.75f);
        var p0 = new Vector3(0, 0, 0);
        var p1 = new Vector3(1, 0, 0);
        var p2 = new Vector3(0.5f, 0, sqrt075);
        var p3 = new Vector3(0.5f, sqrt075, sqrt075 / 3);

        // The first vertex buffer data stream is just positions;
        // fill them in.
        var pos = data.GetVertexData<Vector3>();
        pos[0] = p0; pos[1] = p1; pos[2] = p2;
        pos[3] = p0; pos[4] = p2; pos[5] = p3;
        pos[6] = p2; pos[7] = p1; pos[8] = p3;
        pos[9] = p0; pos[10] = p3; pos[11] = p1;
        
        // Note: normals will be calculated later in RecalculateNormals.
        // Tetrahedron index buffer: 4 triangles, 3 indices per triangle.
        // All vertices are unique so the index buffer is just a
        // 0,1,2,...,11 sequence.
        data.SetIndexBufferParams(12, IndexFormat.UInt16);
        var indexBuffer = data.GetIndexData<ushort>();
        for (ushort i = 0; i < indexBuffer.Length; ++i)
            indexBuffer[i] = i;
        
        // One sub-mesh with all the indices.
        data.subMeshCount = 1;
        data.SetSubMesh(0, new SubMeshDescriptor(0, indexBuffer.Length));
        
        // Create the mesh and apply data to it:
        var mesh = new Mesh();
        mesh.name = "Tetrahedron";
        Mesh.ApplyAndDisposeWritableMeshData(dataArray, mesh);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}
