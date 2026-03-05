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

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Auto-find room volumes if none assigned
        if (roomVolumes == null || roomVolumes.Length == 0)
        {
            roomVolumes = FindObjectsByType<RoomVolume>(FindObjectsSortMode.None);
        }

        foreach (var rv in roomVolumes)
        {
            if (rv == null) continue;
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

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        TryStart();
    }

    private void TryStart()
    {
        if (started) return;

        int connected = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (connected < 2) return;

        started = true;

        // IMPORTANT: Make sure this type matches your timer script name
        var timer = FindFirstObjectByType<Timer>();
        if (timer != null)
            timer.StartMatchTimerServer();

        TriggerCodeBlue();
    }

    public void TriggerCodeBlue()
    {
        if (!IsServer) return;

        SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns);
        SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns);
    }

    private void SpawnPatient(ref NetworkObject currentPatient, NetworkObject prefab, Transform[] spawnPoints)
    {
        if (prefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawn = spawnPoints[randomIndex];
        if (spawn == null) return;

        RoomSpawnPoint info = spawn.GetComponent<RoomSpawnPoint>();

        HospitalType hospital = (info != null) ? info.Hospital : HospitalType.Blue;
        int roomNumber = (info != null) ? info.RoomNumber : (randomIndex + 1);

        // Instantiate
        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);

        // Spawn FIRST so NetVars are safe to write
        patient.Spawn(true);

        // Now configure + hook events
        var pb = patient.GetComponent<PatientBehaviour>();
        if (pb != null)
        {
            pb.InitializeRoomIdentityServer(hospital, roomNumber);
            pb.OnResolvedServer += OnPatientResolvedServer;
        }

        currentPatient = patient;

        // Track room mapping (server-side)
        patientRoom[patient] = (hospital, roomNumber);

        // Announcement
        if (announcementSystem != null)
        {
            ClientRpcParams targets = BuildTargetsForHospital(hospital);
            announcementSystem.PlayRoomAnnouncementClientRpc(hospital, roomNumber, targets);
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
        if (pb == null) return;

        var netObj = pb.GetComponent<NetworkObject>();
        if (netObj == null) return;

        if (!patientRoom.TryGetValue(netObj, out var roomInfo))
            return;

        // Score
        int scoreDelta = pb.Saved.Value ? 1 : -1;
        ScoreSystem score = GetPlayerScoreForHospital(roomInfo.hospital);
        if (score != null)
            score.AddScore(scoreDelta);

        var key = (roomInfo.hospital, roomInfo.roomNumber);

        // Despawn now if room empty, else wait for room empty event
        RoomVolume room = FindRoomVolume(key.hospital, key.roomNumber);

        if (room != null && room.IsEmptyServer)
        {
            pb.OnResolvedServer -= OnPatientResolvedServer;
            patientRoom.Remove(netObj);
            netObj.Despawn(true);
        }
        else
        {
            if (!pendingDespawnByRoom.TryGetValue(key, out var list))
            {
                list = new List<NetworkObject>();
                pendingDespawnByRoom[key] = list;
            }

            if (!list.Contains(netObj))
                list.Add(netObj);
        }

        // Spawn next
        if (roomInfo.hospital == HospitalType.Blue)
            SpawnPatient(ref currentHOnePatient, hospitalOnePatient, hOneRoomSpawns);
        else
            SpawnPatient(ref currentHTwoPatient, hospitalTwoPatient, hTwoRoomSpawns);
    }

    private void OnRoomBecameEmptyServer(HospitalType hospital, int roomNumber)
    {
        if (!IsServer) return;

        var key = (hospital, roomNumber);

        if (!pendingDespawnByRoom.TryGetValue(key, out var list))
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            if (p != null && p.IsSpawned)
            {
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
}