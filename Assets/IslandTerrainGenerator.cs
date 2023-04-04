using System.Collections.Generic;
using TriangleNet.Geometry;
using TriangleNet.Topology;
using UnityEngine;

public class IslandTerrainGenerator : MonoBehaviour
{
    // Maximum size of the terrain.
    public int SideSize = 50;

    // Minimum distance the poisson-disc-sampled points are from each other.
    public float minPointRadius = 4.0f;

    // Number of random points to generate.
    public int randomPoints = 100;

    // Triangles in each chunk.
    public int trianglesInChunk = 20000;

    // Perlin noise parameters
    public float elevationScale = 100.0f;
    public float sampleSize = 1.0f;
    public int octaves = 8;
    public float frequencyBase = 2;
    public float persistence = 1.1f;

    // World Seed
    public int worldSeed = 0;

    // Detail mesh parameters
    public Transform detailMesh;
    public int detailMeshesToGenerate = 50;

    // Prefab which is generated for each chunk of the mesh.
    public Transform chunkPrefab = null;

    // Elevations at each point in the mesh
    private List<float> elevations;

    private List<Vector2> boundaryPoints;
    // Fast triangle querier for arbitrary points
    private TriangleBin bin;
    // The delaunay mesh
    private TriangleNet.Mesh mesh = null;

    private Dictionary<Vector2, Transform> generatedIslands = new Dictionary<Vector2, Transform>();
    public Transform player;
    private GameObject islandParent;

    private void Start()
    {
        InitializeRandomSeed();
        CreationOfIsland(new Vector3(player.position.x, 0, player.position.z));
    }

    public void CreationOfIsland(Vector3 position)
    {
        boundaryPoints = GetBoundaryPoints();
        islandParent = new GameObject("GeneratedIsland");
        GenerateAllPoint(position);
    }

    private void InitializeRandomSeed()
    {
        UnityEngine.Random.InitState(worldSeed);
    }

    protected List<Vector2> GetBoundaryPoints()
    {
        var points = new List<Vector2>();
        for (float i = 0; i < Mathf.PI * 2; i += Mathf.PI * 2 / 50)
        {
            points.Add(new Vector2(SideSize / 2 + Mathf.Cos(i) * (SideSize / 2 + 0.001f), SideSize / 2 + Mathf.Sin(i) * (SideSize / 2 + 0.001f)));
        }

        return points;
    }

    public static bool IsPointInPolygon(List<Vector2> polygon, Vector2 point)
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

    public void GenerateAllPoint(Vector3 position)
    {
        elevations = new List<float>();

        float[] seed = new float[octaves];

        for (int i = 0; i < octaves; i++)
        {
            seed[i] = Random.Range(0.0f, 100.0f);
        }

        PoissonDiscSampler sampler = new PoissonDiscSampler(SideSize, SideSize, minPointRadius);
        Polygon polygon = new Polygon();

        // Add uniformly-spaced points
        foreach (Vector2 sample in sampler.Samples())
        {
            Debug.Log(sample);
            if (IsPointInPolygon(boundaryPoints, sample))
                polygon.Add(new Vertex((double)sample.x, (double)sample.y));
        }

        // Add some randomly sampled points
        for (int i = 0; i < randomPoints; i++)
        {
            var randP = new Vector2(Random.Range(0.0f, SideSize), Random.Range(0.0f, SideSize));
            if (IsPointInPolygon(boundaryPoints, randP))
                polygon.Add(new Vertex(randP.x, randP.y));
        }

        TriangleNet.Meshing.ConstraintOptions options = new TriangleNet.Meshing.ConstraintOptions() { ConformingDelaunay = true };
        mesh = (TriangleNet.Mesh)polygon.Triangulate(options);

        bin = new TriangleBin(mesh, SideSize, SideSize, minPointRadius * 2.0f);

        // Sample perlin noise to get elevations
        foreach (Vertex vert in mesh.Vertices)
        {
            float elevation = 0.0f;
            float amplitude = Mathf.Pow(persistence, octaves);
            float frequency = 1.0f;
            float maxVal = 0.0f;

            for (int o = 0; o < octaves; o++)
            {
                float sample = (Mathf.PerlinNoise(seed[o] + (float)vert.x * sampleSize / (float)SideSize * frequency,
                                                  seed[o] + (float)vert.y * sampleSize / (float)SideSize * frequency) - 0.5f) * amplitude;
                elevation += sample;
                maxVal += amplitude;
                amplitude /= persistence;
                frequency *= frequencyBase;
            }

            elevation = elevation / maxVal;
            elevations.Add(elevation * elevationScale);
        }

        MakeMesh(position);
    }

    public void MakeMesh(Vector3 position)
    {
        IEnumerator<Triangle> triangleEnumerator = mesh.Triangles.GetEnumerator();

        for (int chunkStart = 0; chunkStart < mesh.Triangles.Count; chunkStart += trianglesInChunk)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            int chunkEnd = chunkStart + trianglesInChunk;
            for (int i = chunkStart; i < chunkEnd; i++)
            {
                if (!triangleEnumerator.MoveNext())
                {
                    break;
                }

                Triangle triangle = triangleEnumerator.Current;

                Vector3 v0 = GetPoint3D(triangle.vertices[2].id);
                Vector3 v1 = GetPoint3D(triangle.vertices[1].id);
                Vector3 v2 = GetPoint3D(triangle.vertices[0].id);

                triangles.Add(vertices.Count);
                triangles.Add(vertices.Count + 1);
                triangles.Add(vertices.Count + 2);

                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);

                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(0.0f, 0.0f));
            }

            Mesh chunkMesh = new Mesh();
            chunkMesh.vertices = vertices.ToArray();
            chunkMesh.uv = uvs.ToArray();
            chunkMesh.triangles = triangles.ToArray();
            chunkMesh.normals = normals.ToArray();

            Transform terrain = Instantiate<Transform>(chunkPrefab, position, Quaternion.identity);

            terrain.GetComponent<MeshFilter>().mesh = chunkMesh;
            terrain.GetComponent<MeshCollider>().sharedMesh = chunkMesh;
            terrain.transform.parent = islandParent.transform;
        }
    }

    /* Returns a point's local coordinates. */

    public Vector3 GetPoint3D(int index)
    {
        Vertex vertex = mesh.vertices[index];
        float elevation = elevations[index];
        return new Vector3((float)vertex.x - SideSize / 2.0f, elevation, (float)vertex.y - SideSize / 2.0f);
    }

    /* Returns the triangle containing the given point. If no triangle was found, then null is returned.
       The list will contain exactly three point indices. */

    public List<int> GetTriangleContainingPoint(Vector2 point)
    {
        Triangle triangle = bin.getTriangleForPoint(new Point(point.x, point.y));
        if (triangle == null)
        {
            return null;
        }

        return new List<int>(new int[] { triangle.vertices[0].id, triangle.vertices[1].id, triangle.vertices[2].id });
    }

    /* Returns a pretty good approximation of the height at a given point in worldspace */

    public float GetElevation(float x, float y)
    {
        x += SideSize / 2.0f;
        y += SideSize / 2.0f;

        if (x < 0 || x > SideSize ||
                y < 0 || y > SideSize)
        {
            return 0.0f;
        }

        Vector2 point = new Vector2(x, y);
        List<int> triangle = GetTriangleContainingPoint(point);

        if (triangle == null)
        {
            // This can happen sometimes because the triangulation does not actually fit entirely within the bounds of the grid;
            // not great error handling, but let's return an invalid value
            return float.MinValue;
        }

        Vector3 p0 = GetPoint3D(triangle[0]);
        Vector3 p1 = GetPoint3D(triangle[1]);
        Vector3 p2 = GetPoint3D(triangle[2]);

        Vector3 normal = Vector3.Cross(p0 - p1, p1 - p2).normalized;
        float elevation = p0.y + (normal.x * (p0.x - x) + normal.z * (p0.z - y)) / normal.y;

        return elevation;
    }

    /* Scatters detail meshes within the bounds of the terrain. */

    public void ScatterDetailMeshes(Vector3 islandPosition)
    {
        for (int i = 0; i < detailMeshesToGenerate; i++)
        {
            // Obtain a random position
            float x = Random.Range(0, SideSize);
            float z = Random.Range(0, SideSize);
            float elevation = GetElevation(x, z);
            Vector3 position = islandPosition + new Vector3(x, elevation, z);

            if (elevation == float.MinValue)
            {
                // Value returned when we couldn't find a triangle, just skip this one
                continue;
            }

            // We always want the mesh to remain upright, so only vary the rotation in the x-z plane
            float angle = Random.Range(0, 360.0f);
            Quaternion randomRotation = Quaternion.AngleAxis(angle, Vector3.up);

            Instantiate<Transform>(detailMesh, position, randomRotation, islandParent.transform);
        }
    }

    public void OnDrawGizmos()
    {
        if (mesh == null)
        {
            // Probably in the editor
            return;
        }

        Gizmos.color = Color.red;
        /*for (int i = 0; ++i < boundaryPoints.Count; ++i)
        {
            Debug.Log("itération");
            Gizmos.DrawSphere(new Vector3(boundaryPoints[i].x, 0, boundaryPoints[i].y), 10f);
        }*/
    }
}