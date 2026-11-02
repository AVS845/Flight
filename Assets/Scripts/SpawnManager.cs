using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpawnManager : NetworkBehaviour
{
    public GameObject playerPrefab;
    public float checkRadius = 1.5f;
    public float spawnRadius;
    public LayerMask playerLayer;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            SpawnPlayerForClient(client.ClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        SpawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null) return;

        Vector3 safePosition = GetSafeSpawnPosition();

        GameObject playerInstance = Instantiate(playerPrefab, safePosition, Quaternion.identity);

        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        netObj.SpawnAsPlayerObject(clientId, true);
    }

    private Vector3 GetSafeSpawnPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 spawnPoint = new (Random.Range(-spawnRadius, spawnRadius), Random.Range(-spawnRadius, spawnRadius), 0);
            if (!Physics.CheckSphere(spawnPoint, checkRadius, playerLayer))
            {
                return spawnPoint;
            }
        }

        return Vector3.zero;
    }
}
