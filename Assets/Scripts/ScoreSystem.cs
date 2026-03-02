using Unity.Netcode;
using UnityEngine;

public class ScoreSystem : NetworkBehaviour
{
    // Synced automatically to all clients
    public NetworkVariable<int> Score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Server is the only thing allowed to change score
    public void AddScore(int amount)
    {
        if (!IsServer)
        {
            //Debug.LogWarning("[ScoreSystem] AddScore called on client (ignored).");
            return;
        }

        Score.Value += amount;

        //Debug.Log($"[ScoreSystem][SERVER] Player {OwnerClientId} score change {amount} -> {Score.Value}");
    }
}