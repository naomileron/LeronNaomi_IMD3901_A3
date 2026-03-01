using Unity.Netcode;
using UnityEngine;

public class CodeBlueManager : NetworkBehaviour
{
    //patient prefabs
    [SerializeField] private NetworkObject hospitalOnePatient;
    [SerializeField] private NetworkObject hospitalTwoPatient;

    //Array of spawn points for each hospital
    [SerializeField] private Transform[] hOneRoomSpawns;
    [SerializeField] private Transform[] hTwoRoomSpawns;

    //Variables for keeping track of the current patient
    private NetworkObject currentHOnePatient;
    private NetworkObject currentHTwoPatient;

    // TEMP TEST BUTTON
    [ContextMenu("Trigger Code Blue")]
    public void TriggerCodeBlue()
    {
        if (!IsServer) return;

        SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns);
        SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns);
    }

    void SpawnPatient(ref NetworkObject currentPatient, NetworkObject prefab, Transform[] spawnPoints)
    {
        // despawn the previous patient
        if (currentPatient != null && currentPatient.IsSpawned)
        {
            currentPatient.Despawn(true);
        }

        //spawn in a random room
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];
        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);

        patient.Spawn(true);

        //keep track of current patient
        currentPatient = patient;

        Debug.Log($"Code Blue spawned at {spawn.name}");
    }
}