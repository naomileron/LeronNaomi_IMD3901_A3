using System.Collections;
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

    private enum SpawnStream
    {
        StreamA,
        StreamB
    }

    // Maps patient -> room
    private readonly Dictionary<NetworkObject, (HospitalType hospital, int roomNumber)> patientRoom =
        new Dictionary<NetworkObject, (HospitalType, int)>();

    // Maps patient -> stream
    private readonly Dictionary<NetworkObject, SpawnStream> patientStream =
        new Dictionary<NetworkObject, SpawnStream>();

    // Patients waiting to despawn when room becomes empty
    private readonly Dictionary<(HospitalType hospital, int roomNumber), List<NetworkObject>> pendingDespawnByRoom =
        new Dictionary<(HospitalType, int), List<NetworkObject>>();

    // Last room used by each stream
    private int PlayerOne = -1;
    private int PlayerTwo = -1;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

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

        var timer = FindFirstObjectByType<Timer>();
        if (timer != null)
            timer.StartMatchTimerServer();

        TriggerCodeBlue();
    }

    public void TriggerCodeBlue()
    {
        if (!IsServer) return;

        var modeConfig = FindFirstObjectByType<ConfigureGameMode>();
        bool coop = modeConfig != null && modeConfig.gameMode == GameModeType.Coop;

        if (coop)
        {
            // Two streams in the same hospital
            SpawnPatient(
                ref currentHOnePatient,
                hospitalOnePatient,
                hOneRoomSpawns,
                SpawnStream.StreamA,
                ref PlayerOne,
                currentHTwoPatient
            );

            SpawnPatient(
                ref currentHTwoPatient,
                hospitalOnePatient,
                hOneRoomSpawns,
                SpawnStream.StreamB,
                ref PlayerTwo,
                currentHOnePatient
            );

            return;
        }

        // Competitive: one stream per hospital
        SpawnPatient(
            ref currentHOnePatient,
            hospitalOnePatient,
            hOneRoomSpawns,
            SpawnStream.StreamA,
            ref PlayerOne,
            null
        );

        SpawnPatient(
            ref currentHTwoPatient,
            hospitalTwoPatient,
            hTwoRoomSpawns,
            SpawnStream.StreamB,
            ref PlayerTwo,
            null
        );
    }

    private void SpawnPatient(
        ref NetworkObject currentPatient,
        NetworkObject prefab,
        Transform[] spawnPoints,
        SpawnStream stream,
        ref int lastRoomUsed,
        NetworkObject otherActivePatient
    )
    {
        if (prefab == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int chosenIndex = ChooseSpawnIndex(spawnPoints, lastRoomUsed, otherActivePatient);
        Transform spawn = spawnPoints[chosenIndex];
        if (spawn == null) return;

        RoomSpawnPoint info = spawn.GetComponent<RoomSpawnPoint>();

        HospitalType hospital = (info != null) ? info.Hospital : HospitalType.Blue;
        int roomNumber = (info != null) ? info.RoomNumber : (chosenIndex + 1);

        NetworkObject patient = Instantiate(prefab, spawn.position, spawn.rotation);

        patient.Spawn(true);

        var pb = patient.GetComponent<PatientBehaviour>();
        if (pb != null)
        {
            pb.InitializeRoomIdentityServer(hospital, roomNumber);
            pb.OnResolvedServer += OnPatientResolvedServer;
        }

        currentPatient = patient;

        patientRoom[patient] = (hospital, roomNumber);
        patientStream[patient] = stream;

        lastRoomUsed = roomNumber;

        if (announcementSystem != null)
        {
            var modeConfig = FindFirstObjectByType<ConfigureGameMode>();
            bool coop = modeConfig != null && modeConfig.gameMode == GameModeType.Coop;

            if (coop)
            {
                float delay = (stream == SpawnStream.StreamB) ? 0.5f : 0f;

                StartCoroutine(
                    DelayedAnnouncement(hospital, roomNumber, delay)
                );
            }
            else
            {
                ClientRpcParams targets = BuildTargetsForHospital(hospital);

                announcementSystem.PlayRoomAnnouncementClientRpc(
                    hospital,
                    roomNumber,
                    targets
                );
            }
        }
    }

    private int ChooseSpawnIndex(Transform[] spawnPoints, int lastRoomUsed, NetworkObject otherActivePatient)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return 0;

        int otherRoom = -1;

        if (otherActivePatient != null && patientRoom.TryGetValue(otherActivePatient, out var otherRoomInfo))
        {
            otherRoom = otherRoomInfo.roomNumber;
        }

        List<int> validIndices = new List<int>();

        // Best case:
        // avoid the stream's own last room
        // avoid the other active patient's room (co-op only)
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var spawn = spawnPoints[i];
            if (spawn == null) continue;

            var info = spawn.GetComponent<RoomSpawnPoint>();
            int roomNumber = (info != null) ? info.RoomNumber : (i + 1);

            bool sameAsLastRoom = roomNumber == lastRoomUsed;
            bool sameAsOtherActiveRoom = roomNumber == otherRoom;

            if (!sameAsLastRoom && !sameAsOtherActiveRoom)
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count > 0)
        {
            return validIndices[Random.Range(0, validIndices.Count)];
        }

        // Fallback:
        // still avoid immediate repeat if possible
        validIndices.Clear();

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var spawn = spawnPoints[i];
            if (spawn == null) continue;

            var info = spawn.GetComponent<RoomSpawnPoint>();
            int roomNumber = (info != null) ? info.RoomNumber : (i + 1);

            if (roomNumber != lastRoomUsed)
            {
                validIndices.Add(i);
            }
        }

        if (validIndices.Count > 0)
        {
            return validIndices[Random.Range(0, validIndices.Count)];
        }

        // Final fallback:
        // if there is literally no other option, use anything
        return Random.Range(0, spawnPoints.Length);
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

        if (!patientStream.TryGetValue(netObj, out var stream))
            return;

        int scoreDelta = pb.Saved.Value ? 1 : -1;

        ScoreSystem score = GetPlayerScoreForHospital(roomInfo.hospital);
        if (score != null)
            score.AddScore(scoreDelta);

        var key = (roomInfo.hospital, roomInfo.roomNumber);

        RoomVolume room = FindRoomVolume(key.hospital, key.roomNumber);

        if (room != null && room.IsEmptyServer)
        {
            pb.OnResolvedServer -= OnPatientResolvedServer;

            patientRoom.Remove(netObj);
            patientStream.Remove(netObj);
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

        var modeConfig = FindFirstObjectByType<ConfigureGameMode>();
        bool coop = modeConfig != null && modeConfig.gameMode == GameModeType.Coop;

        if (coop)
        {
            // In coop, both streams spawn into the same hospital
            if (stream == SpawnStream.StreamA)
            {
                SpawnPatient(
                    ref currentHOnePatient,
                    hospitalOnePatient,
                    hOneRoomSpawns,
                    SpawnStream.StreamA,
                    ref PlayerOne,
                    currentHTwoPatient
                );
            }
            else
            {
                SpawnPatient(
                    ref currentHTwoPatient,
                    hospitalOnePatient,
                    hOneRoomSpawns,
                    SpawnStream.StreamB,
                    ref PlayerTwo,
                    currentHOnePatient
                );
            }

            return;
        }

        // Competitive mode
        if (stream == SpawnStream.StreamA)
        {
            SpawnPatient(
                ref currentHOnePatient,
                hospitalOnePatient,
                hOneRoomSpawns,
                SpawnStream.StreamA,
                ref PlayerOne,
                null
            );
        }
        else
        {
            SpawnPatient(
                ref currentHTwoPatient,
                hospitalTwoPatient,
                hTwoRoomSpawns,
                SpawnStream.StreamB,
                ref PlayerTwo,
                null
            );
        }
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
                patientStream.Remove(p);
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

    private IEnumerator DelayedAnnouncement(
    HospitalType hospital,
    int roomNumber,
    float delay)
    {
        yield return new WaitForSeconds(delay);

        if (announcementSystem == null) yield break;

        ClientRpcParams targets = BuildTargetsForHospital(hospital);

        announcementSystem.PlayRoomAnnouncementClientRpc(
            hospital,
            roomNumber,
            targets
        );
    }
}