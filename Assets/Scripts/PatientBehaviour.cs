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
    [SerializeField] private AudioSource monitorAudio;          // distressed/active
    [SerializeField] private AudioSource monitorAudioHealthy;   // saved
    [SerializeField] private AudioSource monitorAudioFlatline;  // dead

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
    private Coroutine roomHookRoutine;

    // --- Called by spawner AFTER patient.Spawn() ---
    public void InitializeRoomIdentityServer(HospitalType h, int roomNumber)
    {
        if (!IsServer) return;

        hospital = h;
        HospitalNet.Value = (int)h;
        RoomNumber.Value = roomNumber;
    }

    public GameObject GetCprHandsObject()
    {
        if (cprHandsRoot != null) return cprHandsRoot;
        return cprAnim != null ? cprAnim.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        // Stop audio locally so nothing auto-plays
        StopAllAudioLocal();

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

        // CLIENT setup (host is also a client)
        if (IsClient)
        {
            Resolved.OnValueChanged += OnResolvedChangedClient;
            Saved.OnValueChanged += OnSavedChangedClient;

            BindRoomVolumeWhenReadyClient();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        StopAllAudioLocal();
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

            if (myRoomVolume != null)
            {
                myRoomVolume.OnLocalPlayerEntered -= OnLocalPlayerEnteredRoom;
                myRoomVolume.OnLocalPlayerExited -= OnLocalPlayerExitedRoom;
            }

            if (roomHookRoutine != null)
                StopCoroutine(roomHookRoutine);
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

    // ---------------- CLIENT ROOM BINDING ----------------

    private void BindRoomVolumeWhenReadyClient()
    {
        if (roomBindRequested) return;
        roomBindRequested = true;

        // Retry for a short time until RoomNumber arrives and volume exists
        roomHookRoutine = StartCoroutine(RoomHookRetryClient());
    }

    private IEnumerator RoomHookRetryClient()
    {
        // Try for up to ~2 seconds
        for (int i = 0; i < 120; i++)
        {
            if (TryHookRoomVolumeClient())
                yield break;

            yield return null;
        }

        // Only warn after we truly failed for a while
        Debug.LogWarning(
            $"[Patient] Failed to bind RoomVolume after retries netId={NetworkObjectId} hospital={(HospitalType)HospitalNet.Value} room={RoomNumber.Value}",
            this
        );
    }

    private bool TryHookRoomVolumeClient()
    {
        if (myRoomVolume != null) return true;

        HospitalType myHospital = (HospitalType)HospitalNet.Value;
        int myRoom = RoomNumber.Value;

        // IMPORTANT: room==0 right after spawn is normal; do not warn
        if (myRoom <= 0) return false;

        var volumes = FindObjectsByType<RoomVolume>(FindObjectsSortMode.None);
        if (volumes == null || volumes.Length == 0) return false;

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
            // Now worth warning: identity is valid but no matching volume exists
            Debug.LogWarning($"[Patient] No matching RoomVolume for ({myHospital},{myRoom}) netId={NetworkObjectId}", this);
            return false;
        }

        // Ensure we don't double-subscribe
        myRoomVolume.OnLocalPlayerEntered -= OnLocalPlayerEnteredRoom;
        myRoomVolume.OnLocalPlayerExited -= OnLocalPlayerExitedRoom;

        myRoomVolume.OnLocalPlayerEntered += OnLocalPlayerEnteredRoom;
        myRoomVolume.OnLocalPlayerExited += OnLocalPlayerExitedRoom;

        // If player spawned inside the room, start immediately
        ForceInitialRoomCheckClient();

        return true;
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

        if (myRoomVolume.VolumeCollider.bounds.Contains(local.transform.position))
        {
            OnLocalPlayerEnteredRoom();
        }
    }

    // ---------------- CLIENT ROOM CALLBACKS ----------------

    private void OnLocalPlayerEnteredRoom()
    {
        localPlayerInRoom = true;
        UpdateMonitorAudioLocal();
    }

    private void OnLocalPlayerExitedRoom()
    {
        localPlayerInRoom = false;
        StopAllAudioLocal();
    }

    private void OnResolvedChangedClient(bool oldValue, bool newValue)
    {
        if (!localPlayerInRoom) return;
        UpdateMonitorAudioLocal();
    }

    private void OnSavedChangedClient(bool oldValue, bool newValue)
    {
        if (!localPlayerInRoom) return;
        UpdateMonitorAudioLocal();
    }

    // ---------------- AUDIO ----------------

    private void StopAllAudioLocal()
    {
        if (monitorAudio != null) monitorAudio.Stop();
        if (monitorAudioHealthy != null) monitorAudioHealthy.Stop();
        if (monitorAudioFlatline != null) monitorAudioFlatline.Stop();
    }

    private void UpdateMonitorAudioLocal()
    {
        if (!localPlayerInRoom) return;

        bool resolved = Resolved.Value;
        bool saved = Saved.Value;

        // Stop all to prevent overlap
        StopAllAudioLocal();

        // Default mute states
        if (monitorAudio != null) monitorAudio.mute = false;
        if (monitorAudioHealthy != null) monitorAudioHealthy.mute = true;
        if (monitorAudioFlatline != null) monitorAudioFlatline.mute = true;

        // Saved -> healthy
        if (resolved && saved)
        {
            if (monitorAudioHealthy != null && monitorAudioHealthy.clip != null)
            {
                monitorAudioHealthy.mute = false;
                monitorAudioHealthy.loop = true;
                monitorAudioHealthy.Play();
            }
            return;
        }

        // Dead -> flatline
        if (resolved && !saved)
        {
            if (monitorAudioFlatline != null && monitorAudioFlatline.clip != null)
            {
                monitorAudioFlatline.mute = false;
                monitorAudioFlatline.loop = true;
                monitorAudioFlatline.Play();
            }
            else if (monitorAudio != null && monitorAudio.clip != null)
            {
                // fallback if flatline source missing
                monitorAudio.mute = false;
                monitorAudio.loop = true;
                monitorAudio.Play();
            }
            return;
        }

        // Active -> distressed monitor
        if (monitorAudio != null && monitorAudio.clip != null)
        {
            monitorAudio.mute = false;
            monitorAudio.loop = true;
            monitorAudio.Play();
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