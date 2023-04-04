// WorldGenerator.cs
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public IslandManager islandManagerPrefab;
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
        // Générer une position aléatoire pour l'île
        Vector3 islandPosition = new Vector3(0, 0, 0);

        // Instancier un nouvel IslandManager pour gérer la génération de l'île
        IslandManager newIslandManager = Instantiate(islandManagerPrefab, islandPosition, Quaternion.identity);
    }
}