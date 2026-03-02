using Unity.Netcode;
using UnityEngine;

public class PatientBehviour : NetworkBehaviour
{
    //Interaction targets
    [SerializeField] private Collider bedButtonCollider;
    [SerializeField] private Collider patientCollider;

    //when the heart monitor sfx flatlines
    [SerializeField] private float flatlineTimeSeconds = 43f;

    //number of compressions needed to save the patient
    [SerializeField] private int compressionsToSave = 10;

    //audio
    [SerializeField] private AudioSource monitorAudio;

    //Networked state (server writes, clients read)
    public NetworkVariable<bool> BedLowered = new(false);
    public NetworkVariable<int> CompressionCount = new(0);
    public NetworkVariable<bool> Resolved = new(false); // saved or dead
    public NetworkVariable<bool> Saved = new(false);

    // server-only timing
    private double startTimeServer;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Start timer immediately when patient spawns
            startTimeServer = NetworkManager.Singleton.ServerTime.Time;

            // play monitor audio on all players at the same time
            PlayMonitorAudioClientRpc();
        }
    }

    [ClientRpc]
    private void PlayMonitorAudioClientRpc()
    {
        if (monitorAudio != null)
        {
            monitorAudio.Stop();
            monitorAudio.Play();
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (Resolved.Value) return;

        //Check if patient is alive based on the timer
        double elapsed = NetworkManager.Singleton.ServerTime.Time - startTimeServer;
        if (elapsed >= flatlineTimeSeconds)
        {
            // Patient dies
            Resolved.Value = true;
            Saved.Value = false;
            OnResolvedClientRpc(false);
        }
    }

    //Called by server when resolved (saved/dead) to let clients update visuals
    [ClientRpc]
    private void OnResolvedClientRpc(bool saved)
    {
        // TODO later: play saved/dead animations, change monitor visuals, etc.
        Debug.Log(saved ? "[Patient] Saved!" : "[Patient] Died (flatline).");
    }

    // ------------------------------------------------------------------
    // ? Server-side validation helpers (UPDATED)
    // ------------------------------------------------------------------
    // Why change?
    // Your raycast might hit a *child collider* of the button/patient object,
    // not the exact collider reference you dragged into the inspector.
    // This makes it still count as "the button" or "the patient" even if the
    // collider hit is nested under that object.

    public bool IsBedButton(Collider hit)
    {
        if (hit == null) return false;

        // If you assigned a specific collider, accept it OR any of its children
        if (bedButtonCollider != null)
            return hit == bedButtonCollider || hit.transform.IsChildOf(bedButtonCollider.transform);

        // (Optional fallback) If you ever forget to assign it, tags can still work
        return hit.CompareTag("bedButton");
    }

    public bool IsPatient(Collider hit)
    {
        if (hit == null) return false;

        // If you assigned a specific collider, accept it OR any of its children
        if (patientCollider != null)
            return hit == patientCollider || hit.transform.IsChildOf(patientCollider.transform);

        // (Optional fallback)
        return hit.CompareTag("patient");
    }

    //Actions from players (server-side)
    [ServerRpc(RequireOwnership = false)]
    public void LowerBedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (Resolved.Value) return;

        //Only do once
        if (!BedLowered.Value)
        {
            BedLowered.Value = true;
            // TODO later: animate bed lowering on all clients
            Debug.Log("[Patient] Bed lowered.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CompressionServerRpc(ServerRpcParams rpcParams = default)
    {
        if (Resolved.Value)
        {
            return;
        }

        //Bed must be lowered before player can do compressions
        if (!BedLowered.Value)
        {
            return;
        }

        int newCount = CompressionCount.Value + 1;
        CompressionCount.Value = newCount;

        //Set these values if the patient is saved
        if (newCount >= compressionsToSave)
        {
            Resolved.Value = true;
            Saved.Value = true;
            OnResolvedClientRpc(true);
        }
    }
}