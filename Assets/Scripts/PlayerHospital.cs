using Unity.Netcode;
using UnityEngine;

//Server spawns player -> Server assigns hospital -> value automatically syncs to all players
public class PlayerHospital : NetworkBehaviour
{
    //variable that automatically syncs from the server to all clients
    public NetworkVariable<HospitalType> Hospital = new NetworkVariable<HospitalType>(
        HospitalType.Blue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    //runs when the player object spawns in the network
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerHospital] OnNetworkSpawn. IsServer={IsServer} OwnerClientId={OwnerClientId}");

        //only the server can decide hospital assignments
        if (!IsServer)
        {
            return;
        }

        if (IsServer)
        {
            Hospital.Value = (OwnerClientId == NetworkManager.ServerClientId)
                ? HospitalType.Blue
                : HospitalType.Green;
            Debug.Log($"[PlayerHospital] Assigned hospital {Hospital.Value} to OwnerClientId={OwnerClientId}");
        }
    }
}
