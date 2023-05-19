// WorldGenerator.cs
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public MeshGeneratorV2 islandManagerPrefab;
    public int numberOfIslands;

    // World Seed
    public int worldSeed = 0;

    private void Start()
    {
        InitializeRandomSeed();
        GenerateWorld();
    }

    private void InitializeRandomSeed()
    {
        UnityEngine.Random.InitState(worldSeed);
    }

    private void GenerateWorld()
    {
        // G�n�rer une position al�atoire pour l'�le
        Vector3 islandPosition = new Vector3(0, 0, 0);

        // Instancier un nouvel IslandManager pour g�rer la g�n�ration de l'�le
        MeshGeneratorV2 newIslandManager = Instantiate(islandManagerPrefab, islandPosition, Quaternion.identity);
    }
}