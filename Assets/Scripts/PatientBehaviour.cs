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

    // Animation scripts (these should be references to the INSTANCES inside the patient setup prefab)
    [SerializeField] private BedAnimation bedAnim;
    [SerializeField] private ButtonAnimation buttonAnim;

    // Rename this field/type if your script is called CPRAnimation instead of HandsAnimation
    [SerializeField] private CPRAnimation handsAnim;

    // Networked state (server writes, clients read)
    public NetworkVariable<bool> BedLowered = new(false);
    public NetworkVariable<int> CompressionCount = new(0);
    public NetworkVariable<bool> Resolved = new(false); // saved or dead
    public NetworkVariable<bool> Saved = new(false);

    // Server-only timing
    private double startTimeServer;

    public override void OnNetworkSpawn()
    {
        // Apply current bed state immediately for this client
        if (bedAnim != null)
            bedAnim.SetLowered(BedLowered.Value);

        // Subscribe to changes so bed updates on all clients
        BedLowered.OnValueChanged += OnBedLoweredChanged;

        if (IsServer)
        {
            // Start timer immediately when patient spawns
            startTimeServer = NetworkManager.Singleton.ServerTime.Time;

            // Play monitor audio on all players at the same time
            PlayMonitorAudioClientRpc();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        BedLowered.OnValueChanged -= OnBedLoweredChanged;
    }

    private void OnBedLoweredChanged(bool oldValue, bool newValue)
    {
        if (bedAnim != null)
            bedAnim.SetLowered(newValue);
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

        // Check if patient is alive based on server time
        double elapsed = NetworkManager.Singleton.ServerTime.Time - startTimeServer;
        if (elapsed >= flatlineTimeSeconds)
        {
            Resolved.Value = true;
            Saved.Value = false;
            OnResolvedClientRpc(false);
        }
    }

    // Called by server when resolved (saved/dead) to let clients update visuals
    [ClientRpc]
    private void OnResolvedClientRpc(bool saved)
    {
        Debug.Log(saved ? "[Patient] Saved!" : "[Patient] Died (flatline).");
        // TODO later: play saved/dead animations, change monitor visuals, etc.
    }

    public bool IsBedButton(Collider hit)
    {
        if (hit == null) return false;

        if (bedButtonCollider != null)
            return hit == bedButtonCollider || hit.transform.IsChildOf(bedButtonCollider.transform);

        return hit.CompareTag("bedButton");
    }

    public bool IsPatient(Collider hit)
    {
        if (hit == null) return false;

        if (patientCollider != null)
            return hit == patientCollider || hit.transform.IsChildOf(patientCollider.transform);

        return hit.CompareTag("patient");
    }

    // Actions from players (server-side)
    [ServerRpc(RequireOwnership = false)]
    public void LowerBedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (Resolved.Value) return;

        // Always play button press animation (event)
        PlayBedButtonPressClientRpc();

        // Only lower once (persistent)
        if (!BedLowered.Value)
        {
            BedLowered.Value = true;
            Debug.Log("[Patient] Bed lowered.");
        }
    }

    [ClientRpc]
    private void PlayBedButtonPressClientRpc()
    {
        if (buttonAnim != null)
            buttonAnim.PlayPress();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CompressionServerRpc(ServerRpcParams rpcParams = default)
    {
        if (Resolved.Value) return;

        // Bed must be lowered before compressions are valid
        if (!BedLowered.Value) return;

        // Optional anti-spam / rhythm pacing
        if (minSecondsBetweenCompressions > 0f)
        {
            double now = NetworkManager.Singleton.ServerTime.Time;
            if (now - lastCompressionTimeServer < minSecondsBetweenCompressions)
                return;

            lastCompressionTimeServer = now;
        }

        // Play hands compression animation (event)
        PlayHandsCompressionClientRpc();

        int newCount = CompressionCount.Value + 1;
        CompressionCount.Value = newCount;

        if (newCount >= compressionsToSave)
        {
            Resolved.Value = true;
            Saved.Value = true;
            OnResolvedClientRpc(true);
        }
    }

    [ClientRpc]
    private void PlayHandsCompressionClientRpc()
    {
        if (handsAnim != null)
            handsAnim.PlayCompression(); // rename if your method name differs
    }
}