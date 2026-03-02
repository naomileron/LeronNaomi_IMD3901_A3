using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//checks if the player is still in the room (used chatGPT to figure this out)
public class RoomVolume : NetworkBehaviour
{
    public HospitalType Hospital;
    public int RoomNumber;

    public event Action<HospitalType, int> OnRoomBecameEmptyServer;

    private readonly HashSet<ulong> playersInside = new HashSet<ulong>();

    public bool IsEmptyServer => playersInside.Count == 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        // Treat any player-owned PlayerObject as "a player"
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(netObj.OwnerClientId, out var client) &&
            client.PlayerObject == netObj)
        {
            playersInside.Add(netObj.OwnerClientId);

            Debug.Log($"[RoomVolume] ENTER key=({Hospital},{RoomNumber}) clientId={netObj.OwnerClientId} insideCount={playersInside.Count} obj={other.name}"); // DEBUG
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        if (playersInside.Remove(netObj.OwnerClientId))
        {
            Debug.Log($"[RoomVolume] EXIT key=({Hospital},{RoomNumber}) clientId={netObj.OwnerClientId} insideCount={playersInside.Count} obj={other.name}"); // DEBUG

            if (playersInside.Count == 0)
            {
                Debug.Log($"[RoomVolume] EMPTY key=({Hospital},{RoomNumber}) firing OnRoomBecameEmptyServer"); // DEBUG
                OnRoomBecameEmptyServer?.Invoke(Hospital, RoomNumber);
            }
        }
    }
}