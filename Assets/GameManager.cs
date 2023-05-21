using Cinemachine;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject islandPrefab; // assign your island prefab in the inspector
    public GameObject playerPrefab;// assign your player prefab in the inspector

    public CinemachineFreeLook cinemachine;

    private void Awake()
    {
    }

    private void Start()
    {
        Debug.Log(Camera.main);
        GenerateIslandAndPlayer();
    }

    private void GenerateIslandAndPlayer()
    {
        // Generate island at position 0,0,0
        GameObject islandInstance = Instantiate(islandPrefab, Vector3.zero, Quaternion.identity);

        // Assuming the player should be instantiated just above the island,
        // we can use the island's position and add to the y value.
        // This could be different based on the specific dimensions of your prefabs.
        Vector3 playerPosition = islandInstance.transform.position + new Vector3(0, 20, 0);

        // Generate player on the island
        GameObject playerInstance = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
        cinemachine.Follow = playerInstance.transform;
        cinemachine.LookAt = playerInstance.transform;
        cinemachine.GetRig(0).LookAt = playerInstance.transform;
        cinemachine.GetRig(1).LookAt = playerInstance.transform;
        cinemachine.GetRig(2).LookAt = playerInstance.transform;
    }
}