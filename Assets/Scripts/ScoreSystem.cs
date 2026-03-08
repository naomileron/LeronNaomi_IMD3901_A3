using Unity.Netcode;
using UnityEngine;

public class ScoreSystem : NetworkBehaviour
{
    //Syncs automatically to all clients
    public NetworkVariable<int> Score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    //Server is the only thing allowed to change score
    public void AddScore(int amount)
    {
        if (!IsServer)
        {
            return;
        }

        Score.Value += amount;
    }
}