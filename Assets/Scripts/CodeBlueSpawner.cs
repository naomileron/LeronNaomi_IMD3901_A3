using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeBlueSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject hospitalOnePatient;
    [SerializeField] private NetworkObject hospitalTwoPatient;

    [SerializeField] private Transform[] hOneRoomSpawns;
    [SerializeField] private Transform[] hTwoRoomSpawns;

    [SerializeField] private AnnouncementSystem announcementSystem;

    // Room volumes in scene
    [SerializeField] private RoomVolume[] roomVolumes;

    private NetworkObject currentHOnePatient;
    private NetworkObject currentHTwoPatient;

    private bool started;

    // Maps patient -> room
    private readonly Dictionary<NetworkObject, (HospitalType hospital, int roomNumber)> patientRoom =
        new Dictionary<NetworkObject, (HospitalType, int)>();

    // Patients waiting to despawn when room becomes empty
    private readonly Dictionary<(HospitalType hospital, int roomNumber), List<NetworkObject>> pendingDespawnByRoom =
        new Dictionary<(HospitalType, int), List<NetworkObject>>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        //Debug.Log("[CodeBlueSpawner] OnNetworkSpawn (SERVER)");

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Auto-find room volumes if none assigned
        if (roomVolumes == null || roomVolumes.Length == 0)
        {
            roomVolumes = FindObjectsByType<RoomVolume>(FindObjectsSortMode.None);
            //Debug.Log($"[CodeBlueSpawner] Auto-found {roomVolumes.Length} RoomVolumes");
        }

        foreach (var rv in roomVolumes)
        {
            if (rv == null) continue;

            //Debug.Log($"[CodeBlueSpawner] Hook RoomVolume ({rv.Hospital},{rv.RoomNumber})");
            rv.OnRoomBecameEmptyServer += OnRoomBecameEmptyServer;
        }

        TryStart();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        if (roomVolumes != null)
        {
            foreach (var rv in roomVolumes)
            {
                if (rv == null) continue;
                rv.OnRoomBecameEmptyServer -= OnRoomBecameEmptyServer;
            }
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

        int connected = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (connected < 2)
        {
            //Debug.Log($"[CodeBlueSpawner] Waiting for players {connected}/2");
            return;
        }

        started = true;

        //Debug.Log("[CodeBlueSpawner] Both players connected. First Code Blue.");
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
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];

        RoomSpawnPoint info = spawn.GetComponent<RoomSpawnPoint>();

        HospitalType hospital =
            (info != null) ? info.Hospital : HospitalType.Blue;

        int roomNumber =
            (info != null) ? info.RoomNumber : (randomIndex + 1);

        NetworkObject patient =
            Instantiate(prefab, spawn.position, spawn.rotation);

        var pb = patient.GetComponent<PatientBehaviour>();
        if (pb != null)
            pb.SetHospitalAndRoom(hospital, roomNumber);

        patient.Spawn(true);

        currentPatient = patient;

        // existing logic continues unchanged
        patientRoom[patient] = (hospital, roomNumber);

        if (pb != null)
            pb.OnResolvedServer += OnPatientResolvedServer;

        if (announcementSystem != null)
        {
            ClientRpcParams targets = BuildTargetsForHospital(hospital);
            announcementSystem.PlayRoomAnnouncementClientRpc(
                hospital,
                roomNumber,
                targets
            );
        }
    }

    private RoomVolume FindRoomVolume(HospitalType hospital, int roomNumber)
    {
        if (roomVolumes == null) return null;

        foreach (var rv in roomVolumes)
        {
            if (rv == null) continue;
            if (rv.Hospital == hospital && rv.RoomNumber == roomNumber)
                return rv;
        }

        return null;
    }

    private void OnPatientResolvedServer(PatientBehaviour pb)
    {
        if (!IsServer) return;

        var netObj = pb.GetComponent<NetworkObject>();
        if (netObj == null) return;

        if (!patientRoom.TryGetValue(netObj, out var roomInfo))
        {
            //Debug.LogWarning("[CodeBlueSpawner] Resolved patient missing room mapping.");
            return;
        }

        int scoreDelta = pb.Saved.Value ? 1 : -1;

        ScoreSystem score = GetPlayerScoreForHospital(roomInfo.hospital);
        if (score != null)
        {
            score.AddScore(scoreDelta);
            //Debug.Log($"[CodeBlueSpawner][SERVER] Score updated hospital={roomInfo.hospital} delta={scoreDelta}");
        }
        else
        {
            //Debug.LogWarning($"[CodeBlueSpawner][SERVER] No PlayerScore found for hospital={roomInfo.hospital}");
        }

        var key = (roomInfo.hospital, roomInfo.roomNumber);

        //Debug.Log($"[CodeBlueSpawner] Patient resolved netId={netObj.NetworkObjectId} key={key}");

        //check if room already empty
        RoomVolume room = FindRoomVolume(key.hospital, key.roomNumber);

        if (room != null && room.IsEmptyServer)
        {
            //Debug.Log($"[CodeBlueSpawner] Room already empty -> immediate despawn netId={netObj.NetworkObjectId}");

            patientRoom.Remove(netObj);
            netObj.Despawn(true);
        }
        else
        {
            // fallback to normal delayed despawn
            if (!pendingDespawnByRoom.TryGetValue(key, out var list))
            {
                list = new List<NetworkObject>();
                pendingDespawnByRoom[key] = list;
            }

            if (!list.Contains(netObj))
                list.Add(netObj);
        }

        // Spawn next patient
        if (roomInfo.hospital == HospitalType.Blue)
            SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns);
        else
            SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns);
    }

    private void OnRoomBecameEmptyServer(HospitalType hospital, int roomNumber)
    {
        if (!IsServer) return;

        var key = (hospital, roomNumber);

        //Debug.Log($"[CodeBlueSpawner] ROOM EMPTY event {key}");

        if (!pendingDespawnByRoom.TryGetValue(key, out var list))
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            if (p != null && p.IsSpawned)
            {
                //Debug.Log($"[CodeBlueSpawner] DESPAWN patient netId={p.NetworkObjectId}");

                var pb = p.GetComponent<PatientBehaviour>();
                if (pb != null) pb.OnResolvedServer -= OnPatientResolvedServer;

                patientRoom.Remove(p);
                p.Despawn(true);
            }

            list.RemoveAt(i);
        }

        pendingDespawnByRoom.Remove(key);
    }

    private ScoreSystem GetPlayerScoreForHospital(HospitalType hospital)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var ph = playerObj.GetComponent<PlayerHospital>();
            if (ph == null) continue;

            if (ph.Hospital.Value == hospital)
                return playerObj.GetComponent<ScoreSystem>();
        }

        return null;
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
}