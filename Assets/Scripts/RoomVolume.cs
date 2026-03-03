using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// checks if the player is still in the room (used chatGPT to figure this out)
public class RoomVolume : NetworkBehaviour
{
    public HospitalType Hospital;
    public int RoomNumber;

    // SERVER: used by spawner to despawn once room becomes empty
    public event Action<HospitalType, int> OnRoomBecameEmptyServer;

    // CLIENT (local): used for "only play audio when I'm in this room"
    public event Action OnLocalPlayerEntered;
    public event Action OnLocalPlayerExited;

    private readonly HashSet<ulong> playersInside = new HashSet<ulong>();

    // local owner-only presence count (handles multiple colliders)
    private int localOwnerInsideCount = 0;

    public bool IsEmptyServer => playersInside.Count == 0;

    private void OnTriggerEnter(Collider other)
    {
        // ---------- CLIENT LOCAL ENTER ----------
        // Fire only for the local owner player (for audio gating)
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