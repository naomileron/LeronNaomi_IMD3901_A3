using System;
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

    // Animation scripts
    [SerializeField] private BedAnimation bedAnim;
    [SerializeField] private ButtonAnimation buttonAnim;
    [SerializeField] private CPRAnimation cprAnim;

    [SerializeField] private GameObject cprHandsRoot;

    // which hospital this patient belongs to (set by spawner)
    [SerializeField] private HospitalType hospital = HospitalType.Blue;

    // NEW: networked identity so clients know which RoomVolume this patient belongs to
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

    // called by CodeBlueSpawner right after Instantiate
    public void SetHospitalAndRoom(HospitalType h, int roomNumber)
    {
        hospital = h;

        if (IsServer)
        {
            HospitalNet.Value = (int)h;
            RoomNumber.Value = roomNumber;
        }
    }

    public GameObject GetCprHandsObject()
    {
        if (cprHandsRoot != null) return cprHandsRoot;
        return cprAnim != null ? cprAnim.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        // Hard stop both audio sources so nothing auto-plays
        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();

        if (cprHandsRoot != null)
            cprHandsRoot.SetActive(false);

        if (bedAnim != null)
            bedAnim.SetLowered(BedLowered.Value);

        BedLowered.OnValueChanged += OnBedLoweredChanged;

        if (IsServer)
        {
            startTimeServer = NetworkManager.Singleton.ServerTime.Time;
            Resolved.OnValueChanged += OnResolvedChangedServer;
        }
        else
        {
            // CLIENT: hook room volume + update audio when state changes
            TryHookRoomVolumeClient();

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
        {
            Resolved.OnValueChanged -= OnResolvedChangedServer;
        }
        else
        {
            Resolved.OnValueChanged -= OnResolvedChangedClient;
            Saved.OnValueChanged -= OnSavedChangedClient;

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

    private void TryHookRoomVolumeClient()
    {
        // Find matching room volume once on this client
        var volumes = FindObjectsByType<RoomVolume>(FindObjectsSortMode.None);
        HospitalType myHospital = (HospitalType)HospitalNet.Value;
        int myRoom = RoomNumber.Value;

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

        if (myRoomVolume != null)
        {
            myRoomVolume.OnLocalPlayerEntered += OnLocalPlayerEnteredRoom;
            myRoomVolume.OnLocalPlayerExited += OnLocalPlayerExitedRoom;
        }
    }

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
        UpdateMonitorAudioLocal();
    }

    private void OnSavedChangedClient(bool oldValue, bool newValue)
    {
        UpdateMonitorAudioLocal();
    }

    private void UpdateMonitorAudioLocal()
    {
        if (!localPlayerInRoom) return;

        // ensure only ONE source plays
        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();

        bool resolved = Resolved.Value;
        bool saved = Saved.Value;

        if (resolved && saved)
        {
            // Saved -> healthy heartbeat until you leave or despawn
            if (monitorAudioHealthy != null)
            {
                monitorAudioHealthy.loop = true;
                monitorAudioHealthy.Play();
            }
        }
        else
        {
            // Not resolved OR died -> keep default monitor (flatline included in clip)
            if (monitorAudio != null)
            {
                monitorAudio.loop = true;
                monitorAudio.Play();
            }
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
        }
    }

    [ClientRpc]
    private void PlayHandsCompressionClientRpc()
    {
        if (cprAnim != null)
            cprAnim.PlayCompress();
    }
}