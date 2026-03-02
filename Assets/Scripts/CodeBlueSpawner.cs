using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeBlueManager : NetworkBehaviour
{
    [SerializeField] private NetworkObject hospitalOnePatient;
    [SerializeField] private NetworkObject hospitalTwoPatient;

    [SerializeField] private Transform[] hOneRoomSpawns;
    [SerializeField] private Transform[] hTwoRoomSpawns;

    [SerializeField] private AnnouncementSystem announcementSystem;

    private NetworkObject currentHOnePatient;
    private NetworkObject currentHTwoPatient;

    private bool started;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        //in case both are already connected by the time this spawns
        TryStart();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        TryStart();
    }

    void TryStart()
    {
        if (started) return;

        //assignment requires 2 players only
        int connected = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (connected < 2)
        {
            Debug.Log($"[CodeBlueManager] Waiting for players. Connected={connected}/2");
            return;
        }

        started = true;

        Debug.Log("[CodeBlueManager] Both players connected. Spawning first code blue.");
        TriggerCodeBlue();
    }

    public void TriggerCodeBlue()
    {
        if (!IsServer) return;

        SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns);
        SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns);
    }

    void SpawnPatient(ref NetworkObject currentPatient, NetworkObject prefab, Transform[] spawnPoints)
    {
        if (currentPatient != null && currentPatient.IsSpawned)
        {
            currentPatient.Despawn(true);
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];

        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);
        patient.Spawn(true);

        currentPatient = patient;

        RoomSpawnPoint info = spawn.GetComponent<RoomSpawnPoint>();

        HospitalType hospital = (info != null) ? info.Hospital : HospitalType.Blue;
        int roomNumber = (info != null) ? info.RoomNumber : (randomIndex + 1);

        Debug.Log($"[CodeBlueManager] Code Blue spawned at {spawn.name} hospital={hospital} room={roomNumber}");

        if (announcementSystem != null)
        {
            ClientRpcParams targets = BuildTargetsForHospital(hospital);
            announcementSystem.PlayRoomAnnouncementClientRpc(hospital, roomNumber, targets);
        }
        else
        {
            Debug.LogWarning("AnnouncementSystem not assigned.");
        }
    }

    ClientRpcParams BuildTargetsForHospital(HospitalType hospital)
    {
        List<ulong> targets = new List<ulong>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var ph = playerObj.GetComponent<PlayerHospital>();
            if (ph == null) continue;

            if (ph.Hospital.Value == hospital)
            {
                targets.Add(client.ClientId);
            }
        }

        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targets.ToArray()
            }
        };
    }
}