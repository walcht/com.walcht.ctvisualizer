using UnityEngine;

public static class WireframeCubeMesh {

    public static Mesh GenerateMesh() {
        Mesh mesh = new Mesh();

        ///
        ///    0 ----- 1
        ///    |\      |\
        ///    | 4 ----- 5
        ///    3 |---- 2 |
        ///     \|      \|
        ///      7 ----- 6       
        ///
        Vector3[] vertices = {
            new(-0.5f, -0.5f, 0.5f),    // 0
            new(0.5f, -0.5f, 0.5f),     // 1
            new(0.5f, 0.5f, 0.5f),      // 2
            new(-0.5f, 0.5f, 0.5f),     // 3
            new(-0.5f, -0.5f, -0.5f),   // 4
            new(0.5f, -0.5f, -0.5f),    // 5
            new(0.5f, 0.5f, -0.5f),     // 6
            new(-0.5f, 0.5f, -0.5f),    // 7
        };
        int[] indices = {
            0, 1,  // back face
            1, 2,
            2, 3,
            3, 0,
            4, 5,  // front face
            5, 6,
            6, 7,
            7, 4,
            0, 4,  // connectors
            1, 5,
            2, 6,
            3, 7
        };
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        return mesh;
    }

}
