using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PatientBehaviour : NetworkBehaviour
{
    // Interaction targets
    [SerializeField] private Collider bedButtonCollider;
    [SerializeField] private Collider patientCollider;

    // When the heart monitor sfx flatlines
    [SerializeField] private float flatlineTimeSeconds = 43f;

    // Number of compressions needed to save the patient
    [SerializeField] private int compressionsToSave = 10;

    [SerializeField] private float minSecondsBetweenCompressions = 0.25f;
    private double lastCompressionTimeServer = -999;

    // Audio
    [SerializeField] private AudioSource monitorAudio;
    [SerializeField] private AudioSource monitorAudioHealthy;
    [SerializeField] private AudioSource monitorAudioFlatline;

    // Animation scripts
    [SerializeField] private BedAnimation bedAnim;
    [SerializeField] private ButtonAnimation buttonAnim;
    [SerializeField] private CPRAnimation cprAnim;

    [SerializeField] private GameObject cprHandsRoot;

    // which hospital this patient belongs to (set by spawner)
    [SerializeField] private HospitalType hospital = HospitalType.Blue;

    // Networked identity so clients know which RoomVolume this patient belongs to
    public NetworkVariable<int> RoomNumber = new(0);
    public NetworkVariable<int> HospitalNet = new((int)HospitalType.Blue);

    // Networked state (server writes, clients read)
    public NetworkVariable<bool> BedLowered = new(false);
    public NetworkVariable<int> CompressionCount = new(0);
    public NetworkVariable<bool> Resolved = new(false);
    public NetworkVariable<bool> Saved = new(false);

    // Server-only resolved event
    public event Action<PatientBehaviour> OnResolvedServer;
    private bool resolvedEventFired = false;

    // Server timing
    private double startTimeServer;

    // CLIENT: audio gating by room
    private RoomVolume myRoomVolume;
    private bool localPlayerInRoom;
    private bool roomBindRequested;

    private void Awake()
    {
        Debug.Log("[Patient] Awake fired", this);
    }

    public void InitializeRoomIdentityServer(HospitalType h, int roomNumber)
    {
        if (!IsServer) return;

        hospital = h;
        HospitalNet.Value = (int)h;
        RoomNumber.Value = roomNumber;
    }

    public void SetHospitalAndRoom(HospitalType h, int roomNumber)
    {
        InitializeRoomIdentityServer(h, roomNumber);
    }

    public GameObject GetCprHandsObject()
    {
        if (cprHandsRoot != null) return cprHandsRoot;
        return cprAnim != null ? cprAnim.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Patient] OnNetworkSpawn IsServer={IsServer} IsClient={IsClient} netId={NetworkObjectId}", this);

        // Stop audio locally
        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();

        if (cprHandsRoot != null)
            cprHandsRoot.SetActive(false);

        if (bedAnim != null)
            bedAnim.SetLowered(BedLowered.Value);

        BedLowered.OnValueChanged += OnBedLoweredChanged;

        // SERVER setup
        if (IsServer)
        {
            startTimeServer = NetworkManager.Singleton.ServerTime.Time;
            Resolved.OnValueChanged += OnResolvedChangedServer;
        }

        // CLIENT setup (IMPORTANT: host is also a client)
        if (IsClient)
        {
            BindRoomVolumeWhenReadyClient();

            Resolved.OnValueChanged += OnResolvedChangedClient;
            Saved.OnValueChanged += OnSavedChangedClient;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // stop locally
        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        BedLowered.OnValueChanged -= OnBedLoweredChanged;

        if (IsServer)
            Resolved.OnValueChanged -= OnResolvedChangedServer;

        if (IsClient)
        {
            Resolved.OnValueChanged -= OnResolvedChangedClient;
            Saved.OnValueChanged -= OnSavedChangedClient;

            HospitalNet.OnValueChanged -= OnRoomIdentityChangedClient;
            RoomNumber.OnValueChanged -= OnRoomIdentityChangedClient;

            if (myRoomVolume != null)
            {
                myRoomVolume.OnLocalPlayerEntered -= OnLocalPlayerEnteredRoom;
                myRoomVolume.OnLocalPlayerExited -= OnLocalPlayerExitedRoom;
            }
        }
    }

    private void OnBedLoweredChanged(bool oldValue, bool newValue)
    {
        if (bedAnim != null)
            bedAnim.SetLowered(newValue);
    }

    private void OnResolvedChangedServer(bool oldValue, bool newValue)
    {
        if (!IsServer) return;

        if (!oldValue && newValue && !resolvedEventFired)
        {
            resolvedEventFired = true;
            OnResolvedServer?.Invoke(this);
        }
    }

    // ---------------- CLIENT ROOM AUDIO ----------------

    private void BindRoomVolumeWhenReadyClient()
    {
        if (roomBindRequested) return;
        roomBindRequested = true;

        // Try immediately
        if (TryHookRoomVolumeClient())
            return;

        // Retry when identity arrives
        HospitalNet.OnValueChanged += OnRoomIdentityChangedClient;
        RoomNumber.OnValueChanged += OnRoomIdentityChangedClient;

        // Safety retry next frame (covers cases where values arrive right after spawn)
        StartCoroutine(DelayedRoomHookClient());
    }

    private IEnumerator DelayedRoomHookClient()
    {
        yield return null;
        TryHookRoomVolumeClient();
    }

    private void OnRoomIdentityChangedClient(int oldValue, int newValue)
    {
        if (TryHookRoomVolumeClient())
        {
            HospitalNet.OnValueChanged -= OnRoomIdentityChangedClient;
            RoomNumber.OnValueChanged -= OnRoomIdentityChangedClient;
        }
    }

    // returns true if successfully bound
    private bool TryHookRoomVolumeClient()
    {
        // Already hooked
        if (myRoomVolume != null) return true;

        HospitalType myHospital = (HospitalType)HospitalNet.Value;
        int myRoom = RoomNumber.Value;

        // Debug once per attempt (fine)
        Debug.Log($"[Patient] Hook attempt netId={NetworkObjectId} hospital={myHospital} room={myRoom}", this);

        // Not initialized yet (NetVars haven't arrived)
        if (myRoom <= 0)
        {
            Debug.LogWarning($"[Patient] Hook not ready (room <= 0) netId={NetworkObjectId} hospital={myHospital} room={myRoom}", this);
            return false;
        }

        var volumes = FindObjectsByType<RoomVolume>(FindObjectsSortMode.None);
        if (volumes == null || volumes.Length == 0)
        {
            Debug.LogWarning($"[Patient] No RoomVolumes found in scene netId={NetworkObjectId}", this);
            return false;
        }

        for (int i = 0; i < volumes.Length; i++)
        {
            var rv = volumes[i];
            if (rv == null) continue;

            if (rv.Hospital == myHospital && rv.RoomNumber == myRoom)
            {
                myRoomVolume = rv;
                break;
            }
        }

        if (myRoomVolume == null)
        {
            Debug.LogWarning($"[Patient] No matching RoomVolume for ({myHospital},{myRoom}) netId={NetworkObjectId}", this);
            return false;
        }

        // Safety: avoid double-subscribe if something weird happens
        myRoomVolume.OnLocalPlayerEntered -= OnLocalPlayerEnteredRoom;
        myRoomVolume.OnLocalPlayerExited -= OnLocalPlayerExitedRoom;

        myRoomVolume.OnLocalPlayerEntered += OnLocalPlayerEnteredRoom;
        myRoomVolume.OnLocalPlayerExited += OnLocalPlayerExitedRoom;

        Debug.Log($"[Patient] BOUND OK => ({myHospital},{myRoom}) netId={NetworkObjectId} roomVolume={myRoomVolume.name}", this);

        // If your RoomVolume uses VolumeCollider for initial overlap, warn if missing
        if (myRoomVolume.VolumeCollider == null)
        {
            Debug.LogWarning($"[Patient] RoomVolume '{myRoomVolume.name}' has no VolumeCollider assigned (initial overlap check may fail).", myRoomVolume);
        }

        // If player spawned inside the room, start audio immediately
        ForceInitialRoomCheckClient();

        return true;
    }

    //private void OnLocalPlayerEnteredRoom()
    //{
    //    localPlayerInRoom = true;
    //    UpdateMonitorAudioLocal();

    //    Debug.Log($"[Patient] ENTER CALLBACK netId={NetworkObjectId} room=({(HospitalType)HospitalNet.Value},{RoomNumber.Value})", this);

    //    if (monitorAudio != null)
    //    {
    //        monitorAudio.Stop();
    //        monitorAudio.Play();
    //        Debug.Log("[Patient] FORCED monitorAudio.Play()", this);
    //    }
    //    else
    //    {
    //        Debug.LogWarning("[Patient] monitorAudio is NULL", this);
    //    }
    //}
    private void OnLocalPlayerEnteredRoom()
    {
        localPlayerInRoom = true;
        UpdateMonitorAudioLocal();
    }

    private void OnLocalPlayerExitedRoom()
    {
        localPlayerInRoom = false;

        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();
    }

    private void OnResolvedChangedClient(bool oldValue, bool newValue)
    {
        // only matters if we're in the room
        if (!localPlayerInRoom) return;

        UpdateMonitorAudioLocal();
    }

    private void OnSavedChangedClient(bool oldValue, bool newValue)
    {
        if (!localPlayerInRoom) return;

        UpdateMonitorAudioLocal();
    }

    private void UpdateMonitorAudioLocal()
    {
        if (!localPlayerInRoom) return;

        bool resolved = Resolved.Value;
        bool saved = Saved.Value;

        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();
        if (monitorAudioFlatline != null) monitorAudioFlatline.Stop();

        // Default mute states
        if (monitorAudio != null) monitorAudio.mute = false;
        if (monitorAudioHealthy != null) monitorAudioHealthy.mute = true;
        if (monitorAudioFlatline != null) monitorAudioFlatline.mute = true;

        if (resolved && saved)
        {
            if (monitorAudio != null) monitorAudio.mute = true;
            if (monitorAudioFlatline != null) monitorAudioFlatline.mute = true;

            if (monitorAudioHealthy == null || monitorAudioHealthy.clip == null)
            {
                Debug.LogError("[Patient] monitorAudioHealthy missing or has no clip.", this);
                return;
            }

            monitorAudioHealthy.mute = false;
            monitorAudioHealthy.loop = true;
            monitorAudioHealthy.Play();
            return;
        }

        if (resolved && !saved)
        {
            if (monitorAudio != null) monitorAudio.mute = true;
            if (monitorAudioHealthy != null) monitorAudioHealthy.mute = true;

            if (monitorAudioFlatline != null && monitorAudioFlatline.clip != null)
            {
                monitorAudioFlatline.mute = false;
                monitorAudioFlatline.loop = true;
                monitorAudioFlatline.Play();
                return;
            }

            // fallback if no flatline source
            if (monitorAudio != null && monitorAudio.clip != null)
            {
                monitorAudio.mute = false;
                monitorAudio.loop = true;
                monitorAudio.Play();
            }
            return;
        }

        // active / not resolved yet
        if (monitorAudio == null || monitorAudio.clip == null)
        {
            Debug.LogError("[Patient] monitorAudio missing or has no clip.", this);
            return;
        }

        monitorAudio.mute = false;
        monitorAudio.loop = true;
        monitorAudio.Play();
    }

    private void ForceInitialRoomCheckClient()
    {
        if (myRoomVolume == null) return;
        if (myRoomVolume.VolumeCollider == null) return;

        // Find local player
        var players = FindObjectsByType<PlayerHospital>(FindObjectsSortMode.None);
        PlayerHospital local = null;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsOwner)
            {
                local = players[i];
                break;
            }
        }

        if (local == null) return;

        // If already inside room volume, simulate enter
        if (myRoomVolume.VolumeCollider.bounds.Contains(local.transform.position))
        {
            OnLocalPlayerEnteredRoom();
        }
    }

    // ---------------- SERVER UPDATE ----------------

    private void Update()
    {
        if (!IsServer) return;
        if (Resolved.Value) return;

        double elapsed = NetworkManager.Singleton.ServerTime.Time - startTimeServer;

        if (elapsed >= flatlineTimeSeconds)
        {
            Saved.Value = false;
            Resolved.Value = true;
        }
    }

    // ---------------- HIT TESTING ----------------

    public bool IsBedButton(Collider hit)
    {
        if (hit == null) return false;

        if (bedButtonCollider != null)
            return hit == bedButtonCollider ||
                   hit.transform.IsChildOf(bedButtonCollider.transform);

        return hit.CompareTag("bedButton");
    }

    public bool IsPatient(Collider hit)
    {
        if (hit == null) return false;

        if (patientCollider != null)
            return hit == patientCollider ||
                   hit.transform.IsChildOf(patientCollider.transform);

        return hit.CompareTag("patient");
    }

    // ---------------- BED LOWER ----------------

    [ServerRpc(RequireOwnership = false)]
    public void LowerBedServerRpc(ServerRpcParams rpcParams = default)
    {
        PlayBedButtonPressClientRpc();

        if (Resolved.Value) return;

        if (!BedLowered.Value)
        {
            BedLowered.Value = true;
            PlayBedLowerClientRpc(true);
        }
    }

    [ClientRpc]
    private void PlayBedLowerClientRpc(bool lowered)
    {
        if (bedAnim != null)
            bedAnim.SetLowered(lowered);
    }

    [ClientRpc]
    private void PlayBedButtonPressClientRpc()
    {
        if (buttonAnim != null)
            buttonAnim.PlayPress();
    }

    // ---------------- CPR ----------------

    [ServerRpc(RequireOwnership = false)]
    public void CompressionServerRpc(ServerRpcParams rpcParams = default)
    {
        if (Resolved.Value) return;
        if (!BedLowered.Value) return;

        if (minSecondsBetweenCompressions > 0f)
        {
            double now = NetworkManager.Singleton.ServerTime.Time;

            if (now - lastCompressionTimeServer < minSecondsBetweenCompressions)
                return;

            lastCompressionTimeServer = now;
        }

        PlayHandsCompressionClientRpc();

        int newCount = CompressionCount.Value + 1;
        CompressionCount.Value = newCount;

        if (newCount >= compressionsToSave)
        {
            Saved.Value = true;
            Resolved.Value = true;
            Debug.Log($"[Patient][SERVER] SAVED netId={NetworkObjectId} compressions={newCount}", this);
        }

        Debug.Log($"[Patient] Audio update inRoom={localPlayerInRoom} resolved={Resolved.Value} saved={Saved.Value}", this);
    }

    [ClientRpc]
    private void PlayHandsCompressionClientRpc()
    {
        if (cprAnim != null)
            cprAnim.PlayCompress();
    }
}