using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;

    [SerializeField] private Transform hospitalOneSpawn;
    [SerializeField] private Transform hospitalTwoSpawn;

    void Start()
    {
        //Check if the network manager exists
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogError("NetworkManager does not exist");
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        bool isHost = (clientId == NetworkManager.ServerClientId);
        Transform spawn = isHost ? hospitalOneSpawn : hospitalTwoSpawn;

        var player = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        player.SpawnAsPlayerObject(clientId, true);
    }
}