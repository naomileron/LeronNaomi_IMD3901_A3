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

    [SerializeField] private float minSecondsBetweenCompressions = 0.0f;
    private double lastCompressionTimeServer = -999;

    // Audio
    [SerializeField] private AudioSource monitorAudio;

    // Animation scripts
    [SerializeField] private BedAnimation bedAnim;
    [SerializeField] private ButtonAnimation buttonAnim;
    [SerializeField] private CPRAnimation cprAnim;

    [SerializeField] private GameObject cprHandsRoot;

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

    public GameObject GetCprHandsObject()
    {
        if (cprHandsRoot != null) return cprHandsRoot;
        return cprAnim != null ? cprAnim.gameObject : null;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Patient] OnNetworkSpawn server={IsServer} netId={NetworkObjectId}");

        if (cprHandsRoot != null)
            cprHandsRoot.SetActive(false);

        if (bedAnim != null)
            bedAnim.SetLowered(BedLowered.Value);

        BedLowered.OnValueChanged += OnBedLoweredChanged;

        if (IsServer)
        {
            startTimeServer = NetworkManager.Singleton.ServerTime.Time;

            Debug.Log($"[Patient][SERVER] Spawned netId={NetworkObjectId}");

            PlayMonitorAudioClientRpc();

            Resolved.OnValueChanged += OnResolvedChangedServer;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        BedLowered.OnValueChanged -= OnBedLoweredChanged;

        if (IsServer)
            Resolved.OnValueChanged -= OnResolvedChangedServer;

        Debug.Log($"[Patient] OnDestroy netId={NetworkObjectId}");
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

            Debug.Log($"[Patient][SERVER] Resolved TRUE netId={NetworkObjectId} Saved={Saved.Value}");

            OnResolvedServer?.Invoke(this);
        }
    }

    // ---------------- AUDIO ----------------

    [ClientRpc]
    private void PlayMonitorAudioClientRpc()
    {
        Debug.Log($"[Patient] Monitor START netId={NetworkObjectId}");

        if (monitorAudio != null)
        {
            monitorAudio.Stop();
            monitorAudio.Play();
        }
    }

    [ClientRpc]
    private void StopMonitorAudioClientRpc()
    {
        Debug.Log($"[Patient] Monitor STOP netId={NetworkObjectId}");

        if (monitorAudio != null)
            monitorAudio.Stop();
    }

    // ---------------- SERVER UPDATE ----------------

    private void Update()
    {
        if (!IsServer) return;
        if (Resolved.Value) return;

        double elapsed =
            NetworkManager.Singleton.ServerTime.Time - startTimeServer;

        if (elapsed >= flatlineTimeSeconds)
        {
            Debug.Log($"[Patient][SERVER] FLATLINE netId={NetworkObjectId}");

            Saved.Value = false;
            Resolved.Value = true;

            StopMonitorAudioClientRpc();
            OnResolvedClientRpc(false);
        }
    }

    // ---------------- CLIENT FEEDBACK ----------------

    [ClientRpc]
    private void OnResolvedClientRpc(bool saved)
    {
        Debug.Log(saved
            ? $"[Patient] Saved! netId={NetworkObjectId}"
            : $"[Patient] Died (flatline) netId={NetworkObjectId}");
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
        Debug.Log($"[Patient][SERVER] LowerBed netId={NetworkObjectId}");

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

        Debug.Log($"[Patient][SERVER] Compression netId={NetworkObjectId}");

        if (minSecondsBetweenCompressions > 0f)
        {
            double now = NetworkManager.Singleton.ServerTime.Time;

            if (now - lastCompressionTimeServer <
                minSecondsBetweenCompressions)
                return;

            lastCompressionTimeServer = now;
        }

        PlayHandsCompressionClientRpc();

        int newCount = CompressionCount.Value + 1;
        CompressionCount.Value = newCount;

        Debug.Log($"[Patient][SERVER] CompressionCount {newCount}/{compressionsToSave}");

        if (newCount >= compressionsToSave)
        {
            Debug.Log($"[Patient][SERVER] SAVED netId={NetworkObjectId}");

            Saved.Value = true;
            Resolved.Value = true;

            StopMonitorAudioClientRpc();
            OnResolvedClientRpc(true);
        }
    }

    [ClientRpc]
    private void PlayHandsCompressionClientRpc()
    {
        if (cprAnim != null)
            cprAnim.PlayCompress();
    }
}