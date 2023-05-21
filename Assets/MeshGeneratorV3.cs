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
    private int MESH_SCALE = 2;
    // Ajoutez cette liste pour stocker vos objets inactifs
    private List<GameObject> pooledObjects;
    private int poolSize = 250; // Taille de votre pool d'objets

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
    private MeshCollider meshCollider;

    private void Start()
    {
        SetNullProperties();
        // Initialisez votre pool d'objets
        pooledObjects = new List<GameObject>(poolSize);
        // Modified code with null check
        for (int i = 0; i < poolSize; i++)
        {
            if (objects != null && objects.Length > 0)
            {
                GameObject obj = Instantiate(objects[Random.Range(0, objects.Length)], new Vector3(0, 0, 0), Quaternion.identity);
                obj.transform.parent = GameObject.Find("AllElement").transform;
                obj.SetActive(false);
                pooledObjects.Add(obj);
            }
            else
            {
                break;
            }
        }

        mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshCollider = GetComponent<MeshCollider>();
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
        boundaryPoints = MakeCircleShape();

        CreateMeshShape();
        CreateTriangles();
        ColorMap();
        UpdateMesh();
    }

    protected List<Vector2> MakeCircleShape()
    {
        var points = new List<Vector2>();
        for (float i = 0; i < Mathf.PI * 2; i += Mathf.PI * 2 / 50)
        {
            points.Add(new Vector2(Mathf.Cos(i) * (xSize / 2 + 0.001f), Mathf.Sin(i) * (zSize / 2 + 0.001f)));
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
                if (IsPointInPolygon(boundaryPoints, new Vector2(x - xSize / 2, z - zSize / 2)))
                {
                    // Set height of vertices
                    float noiseHeight = GenerateNoiseHeight(z, x, octaveOffsets);
                    SetMinMaxHeights(noiseHeight);
                    vertices.Add(new Vector3(x - xSize / 2, noiseHeight, z - zSize / 2));
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
            var option = new ConstraintOptions() { ConformingDelaunay = true };
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
        int currentPoolIndex = 0;

        for (int i = 0; i < vertices.Count; i += 10)
        {
            // Trouvez la position réelle des sommets dans le jeu
            Vector3 worldPt = transform.TransformPoint(mesh.vertices[i]);
            var noiseHeight = worldPt.y;
            // Arrêtez la génération si la différence de hauteur entre 2 sommets est trop raide
            if (Math.Abs(lastNoiseHeight - noiseHeight) < 25)
            {
                // Hauteur minimale pour la génération d'objets
                if (noiseHeight > 0)
                {
                    // Chance de générer
                    if (Random.Range(1, 6) == 1)  // Correction de la probabilité de génération
                    {
                        GameObject objectToSpawn = GetPooledObject();
                        if (objectToSpawn != null)
                        {
                            objectToSpawn.transform.position = new Vector3(worldPt.x, noiseHeight - 0.5f, worldPt.z); // Mettez à jour la position
                            objectToSpawn.transform.SetParent(transform); // Ajoutez cette ligne pour définir le parent
                            objectToSpawn.SetActive(true);
                        }
                        currentPoolIndex = (currentPoolIndex + 1) % poolSize; // Faites boucler l'indice de la pool
                    }
                }
            }
            lastNoiseHeight = noiseHeight;
        }
    }

    private GameObject GetPooledObject()
    {
        for (int i = 0; i < pooledObjects.Count; i++)
        {
            if (!pooledObjects[i].activeInHierarchy)
            {
                return pooledObjects[i];
            }
        }
        Debug.Log("all use");
        return null; // si tous les objets sont actifs, renvoie null
    }

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        if (meshCollider != null) // Vérifiez si meshCollider n'est pas null avant de l'utiliser
        {
            meshCollider.sharedMesh = mesh;
        }

        gameObject.transform.localScale = new Vector3(MESH_SCALE, MESH_SCALE, MESH_SCALE);

        MapEmbellishments();
    }
}