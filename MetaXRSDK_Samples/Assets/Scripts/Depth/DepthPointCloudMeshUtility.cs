using UnityEngine;
using UnityEngine.Rendering;

public static class DepthPointCloudMeshUtility
{
    public static Mesh BuildPointMesh(Mesh currentMesh, int textureSize)
    {
        if (currentMesh != null)
        {
            Object.Destroy(currentMesh);
        }

        var pointCount = textureSize * textureSize;
        var positions = new Vector3[pointCount];
        var uvs = new Vector2[pointCount];
        var indices = new int[pointCount];
        var inverseTextureSize = 1f / textureSize;

        var index = 0;
        for (var y = 0; y < textureSize; y++)
        {
            var v = (y + 0.5f) * inverseTextureSize;
            for (var x = 0; x < textureSize; x++)
            {
                positions[index] = Vector3.zero;
                uvs[index] = new Vector2((x + 0.5f) * inverseTextureSize, v);
                indices[index] = index;
                index++;
            }
        }

        var mesh = new Mesh
        {
            name = $"DepthPointCloud_{textureSize}"
        };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = positions;
        mesh.uv = uvs;
        mesh.SetIndices(indices, MeshTopology.Points, 0, calculateBounds: false);
        mesh.UploadMeshData(false);

        return mesh;
    }

    public static void UpdateBounds(Mesh mesh, float maxDepthMeters)
    {
        if (mesh == null)
        {
            return;
        }

        var extent = Mathf.Max(1f, maxDepthMeters);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * extent * 4f);
    }
}
