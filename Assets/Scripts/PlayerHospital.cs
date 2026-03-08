using Unity.Netcode;
using UnityEngine;

public class PlayerHospital : NetworkBehaviour
{
    public NetworkVariable<HospitalType> Hospital = new NetworkVariable<HospitalType>(
        HospitalType.Blue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        if (IsServer)
        {
            Hospital.Value = (OwnerClientId == NetworkManager.ServerClientId)
                ? HospitalType.Blue
                : HospitalType.Green;
        }
    }
}
