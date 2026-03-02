using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeBlueSpawner : NetworkBehaviour
{
    // patient prefabs
    [SerializeField] private NetworkObject hospitalOnePatient;
    [SerializeField] private NetworkObject hospitalTwoPatient;

    // Array of spawn points for each hospital
    [SerializeField] private Transform[] hOneRoomSpawns;
    [SerializeField] private Transform[] hTwoRoomSpawns;

    // reference to the announcement system gameobject in the scene
    [SerializeField] private AnnouncementSystem announcementSystem;

    // Variables for keeping track of the current patient
    private NetworkObject currentHOnePatient;
    private NetworkObject currentHTwoPatient;

    //prevents starting twice
    private bool started = false;

    //slight delay after both players connect
    [SerializeField] private float startDelay = 0.25f;

    public override void OnNetworkSpawn()
    {
        // Only the server/host runs the game flow
        if (!IsServer) return;

        // Listen for clients connecting
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // In case the client is already connected by the time this spawns
        TryStartWhenReady();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"[CodeBlueManager] Client connected: {clientId}. Total={NetworkManager.Singleton.ConnectedClientsList.Count}");
        TryStartWhenReady();
    }

    // Checks if both players are present. If yes, starts the first code blue.
    private void TryStartWhenReady()
    {
        if (started) return;

        // For your assignment (2 players): host + 1 client
        int connected = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (connected < 2)
        {
            Debug.Log("[CodeBlueManager] Waiting for both players...");
            return;
        }

        started = true;
        StartCoroutine(StartFirstCodeBlue());
    }

    private IEnumerator StartFirstCodeBlue()
    {
        // Small delay so player objects/cameras/audio are fully ready
        yield return new WaitForSeconds(startDelay);

        Debug.Log("[CodeBlueManager] Both players connected. Spawning initial patients.");
        TriggerCodeBlue(); // spawns one patient in each hospital
    }

    // Spawns a patient in each hospital
    public void TriggerCodeBlue()
    {
        if (!IsServer) return;

        SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns, HospitalType.Blue);
        SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns, HospitalType.Green);
    }

    // makes a list of client IDs that should receive the announcement,
    // ensuring that competitors don't hear the other hospital
    private ClientRpcParams BuildTargetsForHospital(HospitalType hospital)
    {
        List<ulong> targets = new List<ulong>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var ph = playerObj.GetComponent<PlayerHospital>();
            if (ph == null) continue;

            if (ph.Hospital.Value == hospital)
                targets.Add(client.ClientId);
        }

        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targets.ToArray()
            }
        };
    }

    void SpawnPatient(ref NetworkObject currentPatient,
                      NetworkObject prefab,
                      Transform[] spawnPoints,
                      HospitalType hospital)
    {
        // despawn the previous patient
        if (currentPatient != null && currentPatient.IsSpawned)
        {
            currentPatient.Despawn(true);
        }

        // spawn in a random room
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];

        int roomNumber = randomIndex + 1;

        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);
        patient.Spawn(true);

        currentPatient = patient;

        // play announcement only for the player in that hospital
        if (announcementSystem != null)
        {
            ClientRpcParams targets = BuildTargetsForHospital(hospital);
            announcementSystem.PlayRoomAnnouncementClientRpc(hospital, roomNumber, targets);
        }
        else
        {
            Debug.LogWarning("[CodeBlueManager] AnnouncementSystem not assigned.");
        }
    }
}
