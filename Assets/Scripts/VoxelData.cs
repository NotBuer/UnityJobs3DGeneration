using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelData
{
    public const byte FaceCount = 6;
    public const byte FaceEdges = 4;
    private const byte VertexCount = FaceCount * FaceEdges;
    private const byte IndexCount = 36;

    public static readonly Vector3[] Vertices = new Vector3[8]
    {
        new(-0.5f, -0.5f,  0.5f), // Back bottom-left
        new( 0.5f, -0.5f,  0.5f), // Back bottom-right
        new( 0.5f, -0.5f, -0.5f), // Front bottom-right
        new(-0.5f, -0.5f, -0.5f), // Front bottom-left
        new(-0.5f,  0.5f,  0.5f), // Back top-left
        new( 0.5f,  0.5f,  0.5f), // Back top-right
        new( 0.5f,  0.5f, -0.5f), // Front top-right
        new(-0.5f,  0.5f, -0.5f)  // Front top-left     
    };

    public static readonly Vector3[] Normals = new Vector3[6]
    {
        Vector3.forward, // Back Face
        Vector3.back, // Front Face
        Vector3.left, // Left Face
        Vector3.right, // Right Face
        Vector3.down, // Bottom Face
        Vector3.up // Top Face
    };

    public static readonly int[,] FaceVertices = new int[6, 4]
    {
        { 0, 1, 4, 5 }, // Back Face (Z+)
        { 3, 2, 7, 6 }, // Front Face (Z-)
        { 3, 0, 7, 4 }, // Left Face (X-)
        { 1, 2, 5, 6 }, // Right Face (X+)
        { 3, 2, 0, 1 }, // Bottom Face (Y-)
        { 4, 5, 7, 6 }  // Top Face (Y+)
    };

    public static readonly Vector2[] Uvs = new Vector2[4]
    {
        new(0.0f, 0.0f),
        new(1.0f, 0.0f),
        new(0.0f, 1.0f),
        new(1.0f, 1.0f),
    };
    
    // public static void SetVoxelMeshData(Vector3 position, ref Mesh.MeshData meshData)
    // {
    //     meshData.SetVertexBufferParams(
    //         VertexCount,
    //         new VertexAttributeDescriptor(VertexAttribute.Position, stream: 0),
    //         new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
    //     
    //     meshData.SetIndexBufferParams(IndexCount, IndexFormat.UInt16);
    //
    //     var positions = meshData.GetVertexData<Vector3>();
    //     var normals = meshData.GetVertexData<Vector3>(1);
    //     var indices = meshData.GetIndexData<ushort>();
    //     
    //     var p0 = new Vector3(-0.5f, -0.5f,  0.5f) + position; // Back bottom-left
    //     var p1 = new Vector3( 0.5f, -0.5f,  0.5f) + position; // Back bottom-right
    //     var p2 = new Vector3( 0.5f, -0.5f, -0.5f) + position; // Front bottom-right
    //     var p3 = new Vector3(-0.5f, -0.5f, -0.5f) + position; // Front bottom-left
    //     var p4 = new Vector3(-0.5f,  0.5f,  0.5f) + position; // Back top-left
    //     var p5 = new Vector3( 0.5f,  0.5f,  0.5f) + position; // Back top-right
    //     var p6 = new Vector3( 0.5f,  0.5f, -0.5f) + position; // Front top-right
    //     var p7 = new Vector3(-0.5f,  0.5f, -0.5f) + position; // Front top-left     
    //     
    //     byte v = 0;
    //         
    //     // Bottom face
    //     positions[v] = p0; positions[v + 1] = p1; positions[v + 2] = p2; positions[v + 3] = p3;
    //     normals[v] = Vector3.down; normals[v + 1] = Vector3.down; normals[v + 2] = Vector3.down; normals[v + 3] = Vector3.down;
    //     v += 4;
    //         
    //     // Top face
    //     positions[v] = p7; positions[v + 1] = p6; positions[v + 2] = p5; positions[v + 3] = p4;
    //     normals[v] = Vector3.up; normals[v + 1] = Vector3.up; normals[v + 2] = Vector3.up; normals[v + 3] = Vector3.up;
    //     v += 4;
    //         
    //     // Left face
    //     positions[v] = p7; positions[v + 1] = p4; positions[v + 2] = p0; positions[v + 3] = p3;
    //     normals[v] = Vector3.left; normals[v + 1] = Vector3.left; normals[v + 2] = Vector3.left; normals[v + 3] = Vector3.left;
    //     v += 4;
    //         
    //     // Right face
    //     positions[v] = p5; positions[v + 1] = p6; positions[v + 2] = p2; positions[v + 3] = p1;
    //     normals[v] = Vector3.right; normals[v + 1] = Vector3.right; normals[v + 2] = Vector3.right; normals[v + 3] = Vector3.right;
    //     v += 4;
    //         
    //     // Front face
    //     positions[v] = p4; positions[v + 1] = p5; positions[v + 2] = p1; positions[v + 3] = p0;
    //     normals[v] = Vector3.forward; normals[v + 1] = Vector3.forward; normals[v + 2] = Vector3.forward; normals[v + 3] = Vector3.forward;
    //     v += 4;
    //         
    //     // Back face
    //     positions[v] = p6; positions[v + 1] = p7; positions[v + 2] = p3; positions[v + 3] = p2;
    //     normals[v] = Vector3.back; normals[v + 1] = Vector3.back; normals[v + 2] = Vector3.back; normals[v + 3] = Vector3.back;
    //     
    //     byte i = 0;
    //     for (ushort j = 0; j < VertexCount; j += FaceEdges)
    //     {
    //         // Counter-clockwise direction
    //         indices[i++] = j;
    //         indices[i++] = (ushort)(j + 3);
    //         indices[i++] = (ushort)(j + 2);
    //             
    //         indices[i++] = j;
    //         indices[i++] = (ushort)(j + 2);
    //         indices[i++] = (ushort)(j + 1);
    //     }
    //         
    //     meshData.subMeshCount = 1;
    //     meshData.SetSubMesh(0, new SubMeshDescriptor(0, IndexCount));
    // }
}
