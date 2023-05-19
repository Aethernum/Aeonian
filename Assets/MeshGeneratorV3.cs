using System;
using System.Collections.Generic;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(MeshFilter))]
public class MeshGeneratorV3 : MonoBehaviour
{
    private Mesh mesh;
    private int MESH_SCALE = 100;
    public GameObject[] objects;
    [SerializeField] private AnimationCurve heightCurve;
    private List<Vector3> vertices;
    private List<int> triangles;

    private Color[] colors;
    [SerializeField] private Gradient gradient;

    private float minTerrainheight;
    private float maxTerrainheight;

    public int xSize;
    public int zSize;

    public float scale;
    public int octaves;
    public float lacunarity;

    public int seed;

    private float lastNoiseHeight;

    private List<Vector2> boundaryPoints;

    private void Start()
    {
        // Use this method if you havn't filled out the properties in the inspector
        // SetNullProperties();

        mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        CreateNewMap();
    }

    private void SetNullProperties()
    {
        if (xSize <= 0) xSize = 50;
        if (zSize <= 0) zSize = 50;
        if (octaves <= 0) octaves = 5;
        if (lacunarity <= 0) lacunarity = 2;
        if (scale <= 0) scale = 50;
    }

    public void CreateNewMap()
    {
        boundaryPoints = GetBoundaryPoints();

        CreateMeshShape();
        CreateTriangles();
        ColorMap();
        UpdateMesh();
    }

    protected List<Vector2> GetBoundaryPoints()
    {
        var points = new List<Vector2>();
        for (float i = 0; i < Mathf.PI * 2; i += Mathf.PI * 2 / 50)
        {
            points.Add(new Vector2(xSize / 2 + Mathf.Cos(i) * (xSize / 2 + 0.001f), zSize / 2 + Mathf.Sin(i) * (zSize / 2 + 0.001f)));
        }

        return points;
    }

    private static bool IsPointInPolygon(List<Vector2> polygon, Vector2 point)
    {
        int polygonLength = polygon.Count, i = 0;
        bool inside = false;
        // x, y for tested point.
        float pointX = point.x, pointY = point.y;
        // start / end point for the current polygon segment.
        float startX, startY, endX, endY;
        Vector2 endPoint = polygon[polygonLength - 1];
        endX = endPoint.x;
        endY = endPoint.y;
        while (i < polygonLength)
        {
            startX = endX; startY = endY;
            endPoint = polygon[i++];
            endX = endPoint.x; endY = endPoint.y;
            //
            inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
                      && /* if so, test if it is under the segment */
                      ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
        }
        return inside;
    }

    private void CreateMeshShape()
    {
        // Creates seed
        Vector2[] octaveOffsets = GetOffsetSeed();

        if (scale <= 0) scale = 0.0001f;
        // Create vertices
        vertices = new List<Vector3>();

        for (int z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                if (IsPointInPolygon(boundaryPoints, new Vector2(x, z)))
                {
                    // Set height of vertices
                    float noiseHeight = GenerateNoiseHeight(z, x, octaveOffsets);
                    SetMinMaxHeights(noiseHeight);
                    vertices.Add(new Vector3(x, noiseHeight, z));
                }
            }
        }
    }

    private Vector2[] GetOffsetSeed()
    {
        seed = UnityEngine.Random.Range(0, 1000);

        // changes area of map
        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        for (int o = 0; o < octaves; o++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[o] = new Vector2(offsetX, offsetY);
        }
        return octaveOffsets;
    }

    private float GenerateNoiseHeight(int z, int x, Vector2[] octaveOffsets)
    {
        float amplitude = 20;
        float frequency = 1;
        float persistence = 0.5f;
        float noiseHeight = 0;

        // loop over octaves
        for (int y = 0; y < octaves; y++)
        {
            float mapZ = z / scale * frequency + octaveOffsets[y].y;
            float mapX = x / scale * frequency + octaveOffsets[y].x;

            //The *2-1 is to create a flat floor level
            float perlinValue = (Mathf.PerlinNoise(mapZ, mapX)) * 2 - 1;
            noiseHeight += heightCurve.Evaluate(perlinValue) * amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }
        return noiseHeight;
    }

    private void SetMinMaxHeights(float noiseHeight)
    {
        // Set min and max height of map for color gradient
        if (noiseHeight > maxTerrainheight)
            maxTerrainheight = noiseHeight;
        if (noiseHeight < minTerrainheight)
            minTerrainheight = noiseHeight;
    }

    private void CreateTriangles()
    {
        try
        {
            // Create a new input geometry.
            Polygon geometry = new Polygon(vertices.Count);

            // Add all vertices to the geometry.
            for (int i = 0; i < vertices.Count; i++)
            {
                geometry.Add(new Vertex(vertices[i].x, vertices[i].z, i)); // Index is used as vertex mark
            }
            // Create a new mesh.
            var quality = new QualityOptions() { MinimumAngle = 25.0 };
            var mesh = (TriangleNet.Mesh)geometry.Triangulate();
            // Use a Dictionary to map vertices to their indices.
            Dictionary<Vertex, int> vertexIndices = new Dictionary<Vertex, int>();
            int nextIndex = 0;
            foreach (var vertex in mesh.Vertices)
            {
                vertexIndices[vertex] = nextIndex;
                nextIndex++;
            }

            // Create a list to hold triangle indices.
            triangles = new List<int>();

            // Add all triangle indices to the list.
            foreach (var triangle in mesh.Triangles)
            {
                triangles.Add(vertexIndices[triangle.GetVertex(2)]); // The vertices of the triangle are stored in counterclockwise order.
                triangles.Add(vertexIndices[triangle.GetVertex(1)]);
                triangles.Add(vertexIndices[triangle.GetVertex(0)]);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create triangles: {ex.Message}");
        }
    }

    private void ColorMap()
    {
        colors = new Color[vertices.Count];

        // Loop over vertices and apply a color from the depending on height (y axis value)
        for (int i = 0, z = 0; z < vertices.Count; z++)
        {
            float height = Mathf.InverseLerp(minTerrainheight, maxTerrainheight, vertices[i].y);
            colors[i] = gradient.Evaluate(height);
            i++;
        }
    }

    private void MapEmbellishments()
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            // find actual position of vertices in the game
            Vector3 worldPt = transform.TransformPoint(mesh.vertices[i]);
            var noiseHeight = worldPt.y;
            // Stop generation if height difference between 2 vertices is too steep
            if (System.Math.Abs(lastNoiseHeight - worldPt.y) < 25)
            {
                // min height for object generation
                if (noiseHeight > 100)
                {
                    // Chance to generate
                    if (Random.Range(1, 5) == 1)
                    {
                        GameObject objectToSpawn = objects[Random.Range(0, objects.Length)];
                        var spawnAboveTerrainBy = noiseHeight * 2;
                        Instantiate(objectToSpawn, new Vector3(0, 0, 0), Quaternion.identity);
                    }
                }
            }
            lastNoiseHeight = noiseHeight;
        }
    }

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        //GetComponent<MeshCollider>().sharedMesh = mesh;
        //gameObject.transform.localScale = new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE);

        MapEmbellishments();
    }
}