using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// checks if the player is still in the room (used chatGPT to figure this out)
public class RoomVolume : NetworkBehaviour
{
    public HospitalType Hospital;
    public int RoomNumber;

    public event Action<HospitalType, int> OnRoomBecameEmptyServer;

    // CLIENT local-only events (for audio gating)
    public event Action OnLocalPlayerEntered;
    public event Action OnLocalPlayerExited;

    [SerializeField] private Collider volumeCollider;
    public Collider VolumeCollider => volumeCollider;

    private readonly HashSet<ulong> playersInside = new HashSet<ulong>();
    private int localOwnerInsideCount = 0;

    public bool IsEmptyServer => playersInside.Count == 0;

    private void Awake()
    {
        if (volumeCollider == null)
            volumeCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // ---------- CLIENT LOCAL ENTER ----------
        var phClient = other.GetComponentInParent<PlayerHospital>();
        if (phClient != null && phClient.IsOwner)
        {
            localOwnerInsideCount++;
            if (localOwnerInsideCount == 1)
            {
                OnLocalPlayerEntered?.Invoke();
            }
        }

        // ---------- SERVER ENTER ----------
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        // Treat any player-owned PlayerObject as "a player"
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(netObj.OwnerClientId, out var client) &&
            client.PlayerObject == netObj)
        {
            playersInside.Add(netObj.OwnerClientId);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // ---------- CLIENT LOCAL EXIT ----------
        var phClient = other.GetComponentInParent<PlayerHospital>();
        if (phClient != null && phClient.IsOwner)
        {
            localOwnerInsideCount = Mathf.Max(0, localOwnerInsideCount - 1);
            if (localOwnerInsideCount == 0)
            {
                OnLocalPlayerExited?.Invoke();
            }
        }

        // ---------- SERVER EXIT ----------
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        if (playersInside.Remove(netObj.OwnerClientId))
        {
            if (playersInside.Count == 0)
            {
                OnRoomBecameEmptyServer?.Invoke(Hospital, RoomNumber);
            }
        }
    }
}