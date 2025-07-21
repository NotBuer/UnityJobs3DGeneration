using UnityEngine;

public static class VoxelUtils
{
    public const byte FaceCount = 6;
    public const byte FaceEdges = 4;

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
        Vector3.back, // Back Face
        Vector3.forward, // Front Face
        Vector3.left, // Left Face
        Vector3.right, // Right Face
        Vector3.down, // Bottom Face
        Vector3.up // Top Face
    };

    public static readonly int[,] FaceVertices = new int[6, 4]
    {
        { 0, 4, 5, 1 }, // Back Face (Z+)
        { 2, 6, 7, 3 }, // Front Face (Z-)
        { 3, 7, 4, 0 }, // Left Face (X-)
        { 1, 5, 6, 2 }, // Right Face (X+)
        { 3, 0, 1, 2 }, // Bottom Face (Y-)
        { 4, 7, 6, 5 }  // Top Face (Y+)
    };

    public static readonly Vector2[] Uvs = new Vector2[4]
    {
        new(0.0f, 0.0f), // Bottom-left
        new(1.0f, 0.0f), // Bottom-right
        new(1.0f, 1.0f), // Top-right
        new(0.0f, 1.0f), // Top-left
    };
}
