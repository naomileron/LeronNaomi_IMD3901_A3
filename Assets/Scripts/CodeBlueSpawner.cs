using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeBlueManager : NetworkBehaviour
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

    // TEMP TEST BUTTON
    [ContextMenu("Trigger Code Blue")]
    public void TriggerCodeBlue()
    {
        Debug.Log($"[CodeBlueManager] TriggerCodeBlue clicked. IsServer={IsServer} IsHost={NetworkManager.Singleton.IsHost}");

        if (!IsServer) return;

        SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns, HospitalType.Blue);
        SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns, HospitalType.Green);
    }

    // makes a list of client IDs that should receive the announcement,
    // ensuring that competitors don't hear the other hospital
    private ClientRpcParams BuildTargetsForHospital(HospitalType hospital)
    {
        List<ulong> targets = new List<ulong>();

        Debug.Log($"[CodeBlueManager] BuildTargetsForHospital({hospital}) connectedClients={NetworkManager.Singleton.ConnectedClientsList.Count}");

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var ph = playerObj.GetComponent<PlayerHospital>();
            Debug.Log($"[CodeBlueManager] playerObj={playerObj.name} has PlayerHospital? {ph != null}");
            if (ph == null) continue;

            Debug.Log($"[CodeBlueManager] clientId={client.ClientId} playerHospital={ph.Hospital.Value}");

            if (ph.Hospital.Value == hospital)
            {
                targets.Add(client.ClientId);
            }
        }

        Debug.Log($"[CodeBlueManager] Targets for {hospital} = {targets.Count}");

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

        // pick a random room from THIS hospital's spawn list
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];

        //roomNumber comes from the chosen index, not from RoomSpawnPoint
        int roomNumber = randomIndex + 1;

        Debug.Log($"[CodeBlueManager] {hospital} chose index={randomIndex} -> spawnName={spawn.name} -> roomNumber={roomNumber}");

        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);
        patient.Spawn(true);

        currentPatient = patient;

        // play announcement only for players in that hospital
        if (announcementSystem != null)
        {
            ClientRpcParams targets = BuildTargetsForHospital(hospital);
            announcementSystem.PlayRoomAnnouncementClientRpc(hospital, roomNumber, targets);
        }
    }
}